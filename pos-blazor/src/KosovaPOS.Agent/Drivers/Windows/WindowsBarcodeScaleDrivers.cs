using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using KosovaPOS.Agent.Contracts;

namespace KosovaPOS.Agent.Drivers.Windows;

/// <summary>
/// Barcode label printer, ported from the desktop <c>BarcodePrinterService</c>:
/// builds a TSPL label and sends it raw to the Windows printer via winspool.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsBarcodeDriver : IBarcodeDriver
{
    private readonly AgentConfig _cfg;
    private readonly ILogger<WindowsBarcodeDriver> _log;

    public WindowsBarcodeDriver(AgentConfig cfg, ILogger<WindowsBarcodeDriver> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public bool Available => true;

    public Task<AgentResult> PrintAsync(BarcodePrintRequest req, CancellationToken ct = default)
    {
        var printer = req.PrinterName ?? _cfg.BarcodePrinter;
        if (string.IsNullOrWhiteSpace(printer))
            return Task.FromResult(AgentResult.Fail("Asnjë printer barkodi nuk është konfiguruar (BARCODE_PRINTER)."));

        var copies = Math.Clamp(req.Copies, 1, 1000);
        var tspl = BuildTspl(req, copies);
        try
        {
            var ok = RawPrinterHelper.SendBytesToPrinter(printer, Encoding.GetEncoding(1252).GetBytes(tspl));
            _log.LogInformation("Barcode label x{Copies} for {Barcode} sent to {Printer}", copies, req.Barcode, printer);
            return Task.FromResult(ok ? AgentResult.Success() : AgentResult.Fail("Shkrimi te printeri dështoi."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AgentResult.Fail(ex.Message));
        }
    }

    private static string BuildTspl(BarcodePrintRequest req, int copies)
    {
        var name = Escape(req.ArticleName);
        var barcode = req.Barcode;
        var type = BarcodeType(barcode);
        var price = req.Price.ToString("0.00") + " €";
        var sb = new StringBuilder();
        sb.AppendLine("SIZE 40 mm, 30 mm");
        sb.AppendLine("GAP 2 mm, 0 mm");
        sb.AppendLine("DIRECTION 1,0");
        sb.AppendLine("REFERENCE 0,0");
        sb.AppendLine("DENSITY 8");
        sb.AppendLine("SPEED 4");
        sb.AppendLine("CLS");
        sb.AppendLine($"TEXT 10,10,\"2\",0,1,1,\"{name}\"");
        sb.AppendLine($"TEXT 10,45,\"4\",0,1,1,\"{Escape(price)}\"");
        sb.AppendLine($"BARCODE 10,90,\"{type}\",70,1,0,2,2,\"{barcode}\"");
        sb.AppendLine($"PRINT 1,{copies}");
        sb.AppendLine("EOP");
        return sb.ToString();
    }

    private static string BarcodeType(string content)
    {
        if (content.Length == 13 && content.All(char.IsDigit)) return "EAN13";
        if (content.Length == 8 && content.All(char.IsDigit)) return "EAN8";
        if (content.Length == 12 && content.All(char.IsDigit)) return "UPCA";
        return "128";
    }

    private static string Escape(string text) => text
        .Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\r", "").Replace("\n", " ");
}

/// <summary>
/// Serial scale reader, ported from the desktop <c>ScaleService</c>. Opens the
/// COM port, reads for a moment, and parses the first stable weight line
/// (Toledo/CAS/generic "  1.234 kg" style).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsScaleDriver : IScaleDriver
{
    private readonly AgentConfig _cfg;
    private readonly ILogger<WindowsScaleDriver> _log;

    public WindowsScaleDriver(AgentConfig cfg, ILogger<WindowsScaleDriver> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public bool Available => true;

    public async Task<ScaleReadResult> ReadAsync(CancellationToken ct = default)
    {
        SerialPort? port = null;
        try
        {
            port = new SerialPort(_cfg.ScaleComPort, _cfg.ScaleBaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 800,
                WriteTimeout = 500,
            };
            port.Open();

            var buffer = "";
            var deadline = DateTime.UtcNow.AddSeconds(2);
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                try { buffer += port.ReadExisting(); } catch (TimeoutException) { }

                var m = Regex.Match(buffer, @"[\d]+\.[\d]+");
                if (m.Success && (buffer.Contains('\r') || buffer.Contains('\n')) &&
                    decimal.TryParse(m.Value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var kg) && kg > 0)
                {
                    return new ScaleReadResult { Ok = true, Weight = kg, Unit = "kg", Stable = true };
                }
                if (buffer.Length > 256) buffer = "";
                await Task.Delay(100, ct);
            }
            return new ScaleReadResult { Ok = false, Error = "Peshorja nuk dërgoi lexim të vlefshëm." };
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Scale read failed on {Port}", _cfg.ScaleComPort);
            return new ScaleReadResult { Ok = false, Error = ex.Message };
        }
        finally
        {
            try { port?.Close(); port?.Dispose(); } catch { }
        }
    }
}

/// <summary>Raw-mode printing via the Win32 spooler (winspool.drv), from the desktop app.</summary>
[SupportedOSPlatform("windows")]
internal static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFOA di);

    [DllImport("winspool.drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    public static bool SendBytesToPrinter(string printerName, byte[] bytes)
    {
        var hPrinter = IntPtr.Zero;
        var success = false;
        try
        {
            if (!OpenPrinter(printerName.Normalize(), out hPrinter, IntPtr.Zero))
                throw new InvalidOperationException($"Nuk u hap dot printeri '{printerName}'.");

            var di = new DOCINFOA { pDocName = "KosovaPOS Label", pDataType = "RAW" };
            if (!StartDocPrinter(hPrinter, 1, ref di))
                throw new InvalidOperationException("Nuk u nis dot dokumenti i printerit.");
            try
            {
                if (!StartPagePrinter(hPrinter))
                    throw new InvalidOperationException("Nuk u nis dot faqja e printerit.");
                try
                {
                    var pBytes = Marshal.AllocCoTaskMem(bytes.Length);
                    try
                    {
                        Marshal.Copy(bytes, 0, pBytes, bytes.Length);
                        if (!WritePrinter(hPrinter, pBytes, bytes.Length, out var written))
                            throw new InvalidOperationException("Nuk u shkrua dot te printeri.");
                        success = written == bytes.Length;
                    }
                    finally { Marshal.FreeCoTaskMem(pBytes); }
                }
                finally { EndPagePrinter(hPrinter); }
            }
            finally { EndDocPrinter(hPrinter); }
        }
        finally
        {
            if (hPrinter != IntPtr.Zero) ClosePrinter(hPrinter);
        }
        return success;
    }
}
