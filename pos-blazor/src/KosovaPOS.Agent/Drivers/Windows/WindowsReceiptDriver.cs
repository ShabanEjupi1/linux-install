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
///
/// The layout is NOT decided here: the server sends the receipt already laid out on
/// the 42-column grid by <c>ReceiptFormatter</c>, and this driver only stresses the
/// lines and emits them. That is what makes the paper and the <c>/kupon/{n}</c> page
/// the same document — printing raw ESC/POS means no Windows print dialog, so nothing
/// visible tells the cashier if the two have drifted apart.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsReceiptDriver : IReceiptDriver
{
    private static readonly byte[] Init = { 0x1B, 0x40 };           // ESC @
    private static readonly byte[] BoldOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] BoldOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] DoubleHeightOn = { 0x1D, 0x21, 0x01 };  // GS ! — height x2, width x1
    private static readonly byte[] DoubleHeightOff = { 0x1D, 0x21, 0x00 };
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

        if (req.Lines.Count == 0)
            return Task.FromResult(AgentResult.Fail("Kuponi erdhi bosh — asnjë rresht për të printuar."));

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
        // 1252 covers the Albanian letters the shop actually prints (ë 0xEB, ç 0xE7), which
        // the printer's default code page maps back to the right glyphs.
        var enc = Encoding.GetEncoding(1252);
        using var ms = new MemoryStream();
        void Raw(byte[] b) => ms.Write(b, 0, b.Length);

        Raw(Init);

        // Every line is emitted left-aligned and verbatim: ReceiptFormatter already padded it
        // to the column grid, and asking the printer to centre a pre-centred line would centre
        // the padding too. Double-height is height-only (GS ! 0x01), so the grid still holds.
        foreach (var line in r.Lines)
        {
            switch (line.Emphasis)
            {
                case 1: Raw(BoldOn); break;
                case 2: Raw(DoubleHeightOn); break;
            }

            var text = enc.GetBytes(line.Text.TrimEnd() + "\n");
            ms.Write(text, 0, text.Length);

            switch (line.Emphasis)
            {
                case 1: Raw(BoldOff); break;
                case 2: Raw(DoubleHeightOff); break;
            }
        }

        // Feed the last line clear of the cutter, then cut.
        ms.WriteByte((byte)'\n');
        ms.WriteByte((byte)'\n');
        ms.WriteByte((byte)'\n');
        Raw(Cut);
        return ms.ToArray();
    }
}
