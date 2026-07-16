using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using KosovaPOS.Models;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;

namespace KosovaPOS.Services
{
    /// <summary>
    /// Barcode printing service optimized for HPRT LPQ80 thermal label printer.
    /// Uses TSPL/TSPL2 raw commands for precise label printing.
    /// Label size is configurable via .env file.
    /// </summary>
    public class BarcodePrinterService
    {
        // Printer resolution (HPRT LPQ80 uses 203 DPI = 8 dots per mm)
        private const int DOTS_PER_MM = 8;
        
        // Label size in mm (configurable via .env, default 55x25mm)
        private readonly int _labelWidthMm;
        private readonly int _labelHeightMm;
        private readonly int _gapMm;
        
        // Label size in dots
        private readonly int _labelWidthDots;
        private readonly int _labelHeightDots;
        
        private readonly string _businessName;
        private readonly string _printerName;
        
        public BarcodePrinterService()
        {
            _businessName = Environment.GetEnvironmentVariable("BUSINESS_NAME") ?? "Dyqani Juaj";
            _printerName = Environment.GetEnvironmentVariable("BARCODE_PRINTER") ?? "HPRT LPQ80";
            
            // Read label size from environment, with defaults for 55x25mm labels
            _labelWidthMm = int.TryParse(Environment.GetEnvironmentVariable("BARCODE_LABEL_WIDTH_MM"), out var w) ? w : 55;
            _labelHeightMm = int.TryParse(Environment.GetEnvironmentVariable("BARCODE_LABEL_HEIGHT_MM"), out var h) ? h : 25;
            _gapMm = int.TryParse(Environment.GetEnvironmentVariable("BARCODE_GAP_MM"), out var g) ? g : 3;
            
            // Calculate dots
            _labelWidthDots = _labelWidthMm * DOTS_PER_MM;
            _labelHeightDots = _labelHeightMm * DOTS_PER_MM;
        }
        
        /// <summary>
        /// Prints barcode labels for an article using TSPL raw commands.
        /// </summary>
        /// <param name="article">The article to print barcode for</param>
        /// <param name="copies">Number of copies to print</param>
        /// <param name="printerName">Optional specific printer name</param>
        public void PrintBarcode(Article article, int copies = 1, string? printerName = null)
        {
            if (article == null)
                throw new ArgumentNullException(nameof(article), "Artikulli nuk mund të jetë null");
            
            if (string.IsNullOrEmpty(article.Barcode))
                throw new ArgumentException("Artikulli duhet të ketë barkodë", nameof(article));
            
            if (copies < 1 || copies > 1000)
                throw new ArgumentOutOfRangeException(nameof(copies), "Numri i kopjeve duhet të jetë midis 1 dhe 1000");
            
            string targetPrinter = printerName ?? _printerName;
            
            // Generate TSPL command for the label
            byte[] tsplCommand = GenerateTSPLLabel(article, copies);
            
            // Send raw command to printer
            RawPrinterHelper.SendBytesToPrinter(targetPrinter, tsplCommand);
        }
        
        /// <summary>
        /// Generates TSPL/TSPL2 commands for the barcode label.
        /// </summary>
        private byte[] GenerateTSPLLabel(Article article, int copies)
        {
            var sb = new StringBuilder();
            
            // Initialize printer and set label size
            // SIZE width mm, height mm
            sb.AppendLine($"SIZE {_labelWidthMm} mm, {_labelHeightMm} mm");
            
            // Set gap between labels (adjust if using gap labels vs continuous)
            sb.AppendLine($"GAP {_gapMm} mm, 0 mm");
            
            // Set print direction and mirror
            sb.AppendLine("DIRECTION 1,0");
            
            // Reference point (origin)
            sb.AppendLine("REFERENCE 0,0");
            
            // Set print density (darkness) 0-15
            sb.AppendLine("DENSITY 8");
            
            // Set print speed (1-5)
            sb.AppendLine("SPEED 4");
            
            // Clear image buffer
            sb.AppendLine("CLS");
            
            // Calculate positions (in dots, 8 dots = 1mm)
            // Label layout optimized for 55x25mm: Company -> Name -> Price (center) -> Barcode (lower)
            int centerX = _labelWidthDots / 2;
            
            // 1. Company name at top (centered, smaller)
            string companyName = TruncateText(_businessName, 25);
            int companyY = 4; // 0.5mm from top
            // TEXT x,y,"font",rotation,x-scale,y-scale,"content"
            // Using font "1" (6x8 dots) for company name - smaller
            int companyTextWidth = companyName.Length * 6;
            int companyX = (_labelWidthDots - companyTextWidth) / 2;
            sb.AppendLine($"TEXT {Math.Max(8, companyX)},{companyY},\"1\",0,1,1,\"{EscapeText(companyName)}\"");
            
            // 2. Article name (centered, below company name)
            string articleName = TruncateText(article.Name, 30);
            int nameY = 16; // about 2mm from top
            int nameTextWidth = articleName.Length * 8;
            int nameX = (_labelWidthDots - nameTextWidth) / 2;
            sb.AppendLine($"TEXT {Math.Max(8, nameX)},{nameY},\"2\",0,1,1,\"{EscapeText(articleName)}\"");
            
            // 3. Price (LARGE and BOLD, EXACTLY centered in the label)
            string priceText = $"{article.SalesPrice:N2} EUR";
            int priceY = 50; // Center of label (~6mm from top for 25mm height)
            // Using font "4" (24x32 dots) with 2x scaling for LARGE BOLD price
            // Font 4 at 2x scale = 48 dots per character width
            int charWidth = 24; // Base width of font 4
            int scale = 2;
            int priceTextWidth = priceText.Length * charWidth * scale;
            // Center precisely
            int priceX = (_labelWidthDots - priceTextWidth) / 2;
            priceX = Math.Max(8, priceX); // Ensure minimum margin
            sb.AppendLine($"TEXT {priceX},{priceY},\"4\",0,{scale},{scale},\"{EscapeText(priceText)}\"");
            
            // 4. Barcode (centered, LOWER on the label - not covering price)
            // For alphanumeric barcodes like "NO.35", always use CODE128 which supports full ASCII
            string barcodeContent = article.Barcode;
            int barcodeHeight = 35; // 35 dots = ~4.5mm tall barcode
            int barcodeY = 115; // Lower - about 14mm from top (leaves space for price)
            
            // Determine barcode type and whether it can be encoded
            bool canEncode = CanEncodeBarcodeContent(barcodeContent);
            
            if (canEncode)
            {
                // Calculate barcode width for centering
                // CODE128: each character is approximately 11 modules, narrow bar = 2 dots
                int estimatedBarcodeWidth = (barcodeContent.Length + 4) * 11 * 2; // +4 for start/stop/checksum
                int barcodeX = (_labelWidthDots - estimatedBarcodeWidth) / 2;
                barcodeX = Math.Max(16, barcodeX);
                
                // Determine barcode type
                string barcodeType = GetBarcodeType(barcodeContent);
                
                // For EAN-13/EAN-8, use specific command
                if (barcodeType == "EAN13" && barcodeContent.Length == 13)
                {
                    sb.AppendLine($"BARCODE {barcodeX},{barcodeY},\"{barcodeType}\",{barcodeHeight},1,0,2,4,\"{barcodeContent}\"");
                }
                else if (barcodeType == "EAN8" && barcodeContent.Length == 8)
                {
                    sb.AppendLine($"BARCODE {barcodeX},{barcodeY},\"{barcodeType}\",{barcodeHeight},1,0,2,4,\"{barcodeContent}\"");
                }
                else
                {
                    // Use CODE128 for general barcodes (supports full ASCII including NO.35, etc.)
                    sb.AppendLine($"BARCODE {barcodeX},{barcodeY},\"128\",{barcodeHeight},1,0,2,2,\"{barcodeContent}\"");
                }
            }
            else
            {
                // Cannot encode as barcode, just print the code as text
                int textX = (_labelWidthDots - barcodeContent.Length * 12) / 2;
                sb.AppendLine($"TEXT {Math.Max(8, textX)},{barcodeY + 10},\"3\",0,1,1,\"{EscapeText(barcodeContent)}\"");
            }
            
            // Print command - print 'copies' labels
            sb.AppendLine($"PRINT 1,{copies}");
            
            // End of job
            sb.AppendLine("EOP");
            
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
        
        /// <summary>
        /// Check if barcode content can be encoded (only printable ASCII)
        /// </summary>
        private bool CanEncodeBarcodeContent(string content)
        {
            if (string.IsNullOrEmpty(content)) return false;
            // CODE128 supports ASCII 0-127, but for reliability use printable chars only
            return content.All(c => c >= 32 && c <= 126);
        }
        
        /// <summary>
        /// Alternative method using bitmap image for complex layouts.
        /// Falls back to this if TSPL direct commands don't work well.
        /// </summary>
        public void PrintBarcodeWithImage(Article article, int copies = 1, string? printerName = null)
        {
            if (article == null)
                throw new ArgumentNullException(nameof(article));
            
            if (string.IsNullOrEmpty(article.Barcode))
                throw new ArgumentException("Artikulli duhet të ketë barkodë", nameof(article));
            
            string targetPrinter = printerName ?? _printerName;
            
            // Generate label as bitmap
            using var labelBitmap = GenerateLabelBitmap(article);
            
            // Convert to 1-bit monochrome for thermal printer
            byte[] bitmapData = ConvertToMonochrome(labelBitmap);
            
            // Generate TSPL command with bitmap
            byte[] tsplCommand = GenerateTSPLWithBitmap(bitmapData, labelBitmap.Width, labelBitmap.Height, copies);
            
            // Send to printer
            RawPrinterHelper.SendBytesToPrinter(targetPrinter, tsplCommand);
        }
        
        private Bitmap GenerateLabelBitmap(Article article)
        {
            // Create bitmap at 203 DPI resolution (8 dots per mm)
            var bitmap = new Bitmap(_labelWidthDots, _labelHeightDots, PixelFormat.Format24bppRgb);
            bitmap.SetResolution(203, 203);
            
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.White);
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
            
            int centerX = _labelWidthDots / 2;
            
            // Fonts optimized for label layout
            using var companyFont = new Font("Arial", 5f, FontStyle.Regular); // Smaller company name
            using var nameFont = new Font("Arial", 6f, FontStyle.Regular);
            using var priceFont = new Font("Arial", 18f, FontStyle.Bold); // Large price in center
            using var barcodeTextFont = new Font("Arial", 5f, FontStyle.Regular);
            using var brush = new SolidBrush(Color.Black);
            
            var centerFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Near
            };
            
            // 1. Company name at very top (smaller)
            int currentY = 2;
            g.DrawString(_businessName, companyFont, brush, centerX, currentY, centerFormat);
            currentY += 12;
            
            // 2. Article name below company
            string articleName = TruncateText(article.Name, 32);
            g.DrawString(articleName, nameFont, brush, centerX, currentY, centerFormat);
            currentY += 14;
            
            // 3. Price EXACTLY centered in the label
            string priceText = $"{article.SalesPrice:N2} €";
            // Calculate exact center position
            SizeF priceSize = g.MeasureString(priceText, priceFont);
            int priceCenterY = (_labelHeightDots / 2) - ((int)priceSize.Height / 2) - 10;
            priceCenterY = Math.Max(currentY + 5, priceCenterY);
            g.DrawString(priceText, priceFont, brush, centerX, priceCenterY, centerFormat);
            
            // 4. Barcode at the BOTTOM of label (lower position)
            int barcodeY = _labelHeightDots - 55; // Near bottom, leaves room for barcode + text
            var barcodeImage = GenerateBarcodeImage(article.Barcode, 260, 35); // Slightly smaller barcode
            if (barcodeImage != null)
            {
                int barcodeX = (_labelWidthDots - barcodeImage.Width) / 2;
                g.DrawImage(barcodeImage, barcodeX, barcodeY, barcodeImage.Width, barcodeImage.Height);
                barcodeImage.Dispose();
            }
            
            // 5. Barcode number below the barcode
            int barcodeTextY = _labelHeightDots - 14;
            g.DrawString(article.Barcode, barcodeTextFont, brush, centerX, barcodeTextY, centerFormat);
            
            return bitmap;
        }
        
        private Bitmap? GenerateBarcodeImage(string content, int width, int height)
        {
            try
            {
                var barcodeWriter = new BarcodeWriter
                {
                    Format = BarcodeFormat.CODE_128,
                    Options = new EncodingOptions
                    {
                        Width = width,
                        Height = height,
                        Margin = 0,
                        PureBarcode = true
                    }
                };
                
                try
                {
                    return barcodeWriter.Write(content);
                }
                catch
                {
                    if (content.Length == 13 && content.All(char.IsDigit))
                    {
                        barcodeWriter.Format = BarcodeFormat.EAN_13;
                        return barcodeWriter.Write(content);
                    }
                    else if (content.Length == 8 && content.All(char.IsDigit))
                    {
                        barcodeWriter.Format = BarcodeFormat.EAN_8;
                        return barcodeWriter.Write(content);
                    }
                    throw;
                }
            }
            catch
            {
                return null;
            }
        }
        
        private byte[] ConvertToMonochrome(Bitmap source)
        {
            int width = source.Width;
            int height = source.Height;
            int bytesPerRow = (width + 7) / 8;
            byte[] result = new byte[bytesPerRow * height];
            
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var pixel = source.GetPixel(x, y);
                    int brightness = (pixel.R + pixel.G + pixel.B) / 3;
                    
                    // If dark (below threshold), set bit
                    if (brightness < 128)
                    {
                        int byteIndex = y * bytesPerRow + (x / 8);
                        int bitIndex = 7 - (x % 8);
                        result[byteIndex] |= (byte)(1 << bitIndex);
                    }
                }
            }
            
            return result;
        }
        
        private byte[] GenerateTSPLWithBitmap(byte[] bitmapData, int width, int height, int copies)
        {
            using var ms = new MemoryStream();
            using var sw = new StreamWriter(ms, Encoding.ASCII);
            
            sw.WriteLine($"SIZE {_labelWidthMm} mm, {_labelHeightMm} mm");
            sw.WriteLine($"GAP {_gapMm} mm, 0 mm");
            sw.WriteLine("DIRECTION 1,0");
            sw.WriteLine("DENSITY 8");
            sw.WriteLine("SPEED 4");
            sw.WriteLine("CLS");
            
            // BITMAP command: BITMAP x,y,width_bytes,height,mode,data
            int widthBytes = (width + 7) / 8;
            sw.Write($"BITMAP 0,0,{widthBytes},{height},0,");
            sw.Flush();
            
            ms.Write(bitmapData, 0, bitmapData.Length);
            
            sw.WriteLine();
            sw.WriteLine($"PRINT 1,{copies}");
            sw.Flush();
            
            return ms.ToArray();
        }
        
        private string GetBarcodeType(string content)
        {
            if (content.Length == 13 && content.All(char.IsDigit))
                return "EAN13";
            if (content.Length == 8 && content.All(char.IsDigit))
                return "EAN8";
            if (content.Length == 12 && content.All(char.IsDigit))
                return "UPCA";
            return "128"; // CODE128 for everything else
        }
        
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (text.Length <= maxLength) return text;
            return text.Substring(0, maxLength - 2) + "..";
        }
        
        private string EscapeText(string text)
        {
            // Escape special characters for TSPL
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "")
                .Replace("\n", " ");
        }
        
        /// <summary>
        /// Gets a list of available printers on the system.
        /// </summary>
        public static string[] GetAvailablePrinters()
        {
            return System.Drawing.Printing.PrinterSettings.InstalledPrinters
                .Cast<string>()
                .ToArray();
        }
    }
    
    /// <summary>
    /// Helper class to send raw data to printer using Windows API.
    /// </summary>
    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)]
            public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)]
            public string? pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)]
            public string? pDataType;
        }
        
        [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);
        
        [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool ClosePrinter(IntPtr hPrinter);
        
        [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFOA di);
        
        [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndDocPrinter(IntPtr hPrinter);
        
        [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool StartPagePrinter(IntPtr hPrinter);
        
        [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool EndPagePrinter(IntPtr hPrinter);
        
        [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);
        
        /// <summary>
        /// Sends raw bytes to a printer.
        /// </summary>
        public static bool SendBytesToPrinter(string printerName, byte[] bytes)
        {
            IntPtr hPrinter = IntPtr.Zero;
            bool success = false;
            
            try
            {
                // Open the printer
                if (!OpenPrinter(printerName.Normalize(), out hPrinter, IntPtr.Zero))
                {
                    throw new InvalidOperationException($"Nuk u hap dot printeri '{printerName}'. Sigurohu që printeri është i instaluar dhe i ndezur.");
                }
                
                // Start a document
                var di = new DOCINFOA
                {
                    pDocName = "Barcode Label",
                    pDataType = "RAW"
                };
                
                if (!StartDocPrinter(hPrinter, 1, ref di))
                {
                    throw new InvalidOperationException("Nuk u nis dot dokumenti i printerit.");
                }
                
                try
                {
                    // Start a page
                    if (!StartPagePrinter(hPrinter))
                    {
                        throw new InvalidOperationException("Nuk u nis dot faqja e printerit.");
                    }
                    
                    try
                    {
                        // Allocate unmanaged memory and copy bytes
                        IntPtr pBytes = Marshal.AllocCoTaskMem(bytes.Length);
                        try
                        {
                            Marshal.Copy(bytes, 0, pBytes, bytes.Length);
                            
                            // Write bytes to printer
                            if (!WritePrinter(hPrinter, pBytes, bytes.Length, out int written))
                            {
                                throw new InvalidOperationException("Nuk u shkrua dot te printeri.");
                            }
                            
                            success = (written == bytes.Length);
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(pBytes);
                        }
                    }
                    finally
                    {
                        EndPagePrinter(hPrinter);
                    }
                }
                finally
                {
                    EndDocPrinter(hPrinter);
                }
            }
            finally
            {
                if (hPrinter != IntPtr.Zero)
                {
                    ClosePrinter(hPrinter);
                }
            }
            
            return success;
        }
        
        /// <summary>
        /// Sends a string to a printer as raw data.
        /// </summary>
        public static bool SendStringToPrinter(string printerName, string data)
        {
            return SendBytesToPrinter(printerName, Encoding.ASCII.GetBytes(data));
        }
    }
}
