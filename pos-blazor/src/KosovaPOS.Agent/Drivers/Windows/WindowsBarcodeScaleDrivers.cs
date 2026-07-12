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
        var printer = WindowsPrinters.Resolve(req.PrinterName, _cfg.BarcodePrinter);
        if (string.IsNullOrWhiteSpace(printer))
            return Task.FromResult(AgentResult.Fail(
                "Asnjë printer etiketash nuk është zgjedhur, dhe ky kompjuter nuk ka printer të parazgjedhur. " +
                "Zgjidhe te Cilësimet → Pajisjet."));

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

    /// <summary>Printhead resolution of the HPRT (and every 203dpi label printer): 8 dots/mm.</summary>
    private const int DotsPerMm = 8;

    /// <summary>Glyph width of TSPL's built-in font "2", in dots — used to fit the article name.</summary>
    private const int NameCharDots = 12;

    private static string BuildTspl(BarcodePrintRequest req, int copies)
    {
        var wMm = Math.Clamp(req.LabelWidthMm, 20, 200);
        var hMm = Math.Clamp(req.LabelHeightMm, 10, 200);
        var wDots = wMm * DotsPerMm;
        var hDots = hMm * DotsPerMm;
        const int Margin = 8; // 1mm — the printhead cannot reach the very edge of the stock

        var usable = wDots - 2 * Margin;
        var price = Escape(req.Price.ToString("0.00") + " €");
        var name = Escape(Trunc(req.ArticleName, usable / NameCharDots));

        var sb = new StringBuilder();
        sb.AppendLine($"SIZE {wMm} mm, {hMm} mm");
        sb.AppendLine("GAP 2 mm, 0 mm");
        sb.AppendLine("DIRECTION 1,0");
        sb.AppendLine("REFERENCE 0,0");
        sb.AppendLine("DENSITY 8");
        sb.AppendLine("SPEED 4");
        sb.AppendLine("CLS");
        sb.AppendLine($"TEXT {Margin},4,\"2\",0,1,1,\"{name}\"");   // font 2 is 20 dots tall
        sb.AppendLine($"TEXT {Margin},28,\"4\",0,1,1,\"{price}\""); // font 4 is 32 dots tall

        // The bars start below the price and reach for the bottom of the label, leaving room
        // for the human-readable digits TSPL prints underneath them.
        const int BarTop = 68;
        const int DigitDots = 24;
        var barHeight = hDots - BarTop - DigitDots - Margin;

        // A barcode wider than the label is not a wide barcode — it is a clipped one, and a
        // clipped barcode still looks right while scanning nowhere. Shrink the module until
        // the symbol fits; if even the thinnest bar won't fit (or the label is too short for
        // bars at all), print the number as text, exactly as the browser path does.
        var narrow = FitNarrowDots(req.Barcode, usable);
        if (narrow is null || barHeight < 30)
        {
            sb.AppendLine($"TEXT {Margin},{Math.Min(BarTop, hDots - 24)},\"3\",0,1,1,\"{Escape(req.Barcode)}\"");
        }
        else
        {
            var type = BarcodeType(req.Barcode);
            var x = Margin + Math.Max(0, (usable - Modules(req.Barcode) * narrow.Value) / 2);
            sb.AppendLine($"BARCODE {x},{BarTop},\"{type}\",{barHeight},1,0,{narrow},{narrow * 2},\"{req.Barcode}\"");
        }

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

    /// <summary>Width of the symbol in modules (narrowest-bar units), for the type we'd pick.</summary>
    private static int Modules(string content) => BarcodeType(content) switch
    {
        "EAN13" or "UPCA" => 95,
        "EAN8"            => 67,
        // Code 128: start + one symbol per char + check + stop(13 modules), 11 modules each.
        _                 => 11 * (content.Length + 2) + 13,
    };

    /// <summary>
    /// The widest module (in dots) at which the symbol still fits <paramref name="usableDots"/>,
    /// capped at 3 — beyond that a scanner gains nothing. Null if it cannot fit at all, or the
    /// content is empty.
    /// </summary>
    private static int? FitNarrowDots(string content, int usableDots)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var modules = Modules(content);
        for (var narrow = 3; narrow >= 1; narrow--)
            if (modules * narrow <= usableDots) return narrow;

        return null;
    }

    private static string Trunc(string s, int max) =>
        string.IsNullOrEmpty(s) || max <= 0 ? "" : (s.Length <= max ? s : s[..max]);

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
