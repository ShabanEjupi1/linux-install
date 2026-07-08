using System.Runtime.Versioning;
using System.Text;
using KosovaPOS.Agent.Contracts;

namespace KosovaPOS.Agent.Drivers.Windows;

/// <summary>
/// Non-fiscal (courtesy) receipt printer. The desktop <c>ReceiptPrinterService</c>
/// rendered via GDI (<c>System.Drawing.Printing</c>), which is Windows-only at
/// runtime and awkward for a headless agent. We instead emit ESC/POS raw text and
/// send it through the same winspool raw path used for barcodes — the standard,
/// driver-independent way to drive an 80mm thermal receipt printer.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsReceiptDriver : IReceiptDriver
{
    private const int Width = 42; // chars on an 80mm roll at font A
    private static readonly byte[] Init = { 0x1B, 0x40 };           // ESC @
    private static readonly byte[] AlignCenter = { 0x1B, 0x61, 0x01 };
    private static readonly byte[] AlignLeft = { 0x1B, 0x61, 0x00 };
    private static readonly byte[] BoldOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] BoldOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] Cut = { 0x1D, 0x56, 0x42, 0x00 }; // GS V B 0

    private readonly AgentConfig _cfg;
    private readonly ILogger<WindowsReceiptDriver> _log;

    public WindowsReceiptDriver(AgentConfig cfg, ILogger<WindowsReceiptDriver> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public bool Available => true;

    public Task<AgentResult> PrintAsync(ReceiptPrintRequest req, CancellationToken ct = default)
    {
        var printer = req.PrinterName ?? _cfg.ReceiptPrinter;
        if (string.IsNullOrWhiteSpace(printer))
            return Task.FromResult(AgentResult.Fail("Asnjë printer faturash nuk është konfiguruar (RECEIPT_PRINTER)."));

        try
        {
            var bytes = Build(req);
            var ok = RawPrinterHelper.SendBytesToPrinter(printer, bytes);
            _log.LogInformation("Non-fiscal receipt #{No} sent to {Printer}", req.ReceiptNumber, printer);
            return Task.FromResult(ok ? AgentResult.Success() : AgentResult.Fail("Shkrimi te printeri dështoi."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AgentResult.Fail(ex.Message));
        }
    }

    private static byte[] Build(ReceiptPrintRequest r)
    {
        var enc = Encoding.GetEncoding(1252);
        using var ms = new MemoryStream();
        void Raw(byte[] b) => ms.Write(b, 0, b.Length);
        void Line(string s = "") { var t = enc.GetBytes(s + "\n"); ms.Write(t, 0, t.Length); }

        Raw(Init);
        Raw(AlignCenter);
        Raw(BoldOn);
        if (!string.IsNullOrWhiteSpace(r.BusinessName)) Line(r.BusinessName);
        Raw(BoldOff);
        if (!string.IsNullOrWhiteSpace(r.Address)) Line(r.Address);
        if (!string.IsNullOrWhiteSpace(r.FiscalNumber)) Line($"NF: {r.FiscalNumber}");
        Line("FATURË JOFISKALE");
        Raw(AlignLeft);
        Line(new string('-', Width));
        Line($"Nr: {r.ReceiptNumber}");
        Line($"Data: {r.Date:dd.MM.yyyy HH:mm}");
        if (!string.IsNullOrWhiteSpace(r.CashierName)) Line($"Arkatari: {r.CashierName}");
        Line(new string('-', Width));

        foreach (var l in r.Lines)
        {
            Line(Trunc(l.Name, Width));
            Line(Row($"  {l.Quantity:0.##} x {l.UnitPrice:0.00}", $"{l.LineTotal:0.00}"));
        }

        Line(new string('-', Width));
        Line(Row("Nën-total", $"{r.Subtotal:0.00}"));
        Line(Row("TVSH", $"{r.Vat:0.00}"));
        Raw(BoldOn);
        Line(Row("TOTALI", $"{r.Total:0.00} EUR"));
        Raw(BoldOff);
        Line(Row("Pagoi", $"{r.Paid:0.00}"));
        Line(Row("Kusur", $"{r.Change:0.00}"));
        if (!string.IsNullOrWhiteSpace(r.PaymentMethod)) Line($"Mënyra: {r.PaymentMethod}");
        Line();
        Raw(AlignCenter);
        Line(string.IsNullOrWhiteSpace(r.Footer) ? "Faleminderit!" : r.Footer);
        Line();
        Line();
        Raw(Cut);
        return ms.ToArray();
    }

    private static string Row(string left, string right)
    {
        var space = Width - left.Length - right.Length;
        return space > 0 ? left + new string(' ', space) + right : (left + " " + right);
    }

    private static string Trunc(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s.Substring(0, max));
}
