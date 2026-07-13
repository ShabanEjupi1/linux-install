using System.Text;
using KosovaPOS.Agent.Contracts;

namespace KosovaPOS.Agent.Drivers.Mock;

/// <summary>
/// Cross-platform stand-ins used when there is no real hardware (dev, Linux,
/// or <c>AGENT_MOCK=true</c>). They exercise the exact same request/response
/// contracts as the Windows drivers, and write artefacts to a temp folder so a
/// developer can inspect what would have been sent to the device.
/// </summary>
public sealed class MockFiscalDriver : IFiscalDriver
{
    private readonly string _dir;
    private readonly ILogger<MockFiscalDriver> _log;

    public MockFiscalDriver(AgentConfig cfg, ILogger<MockFiscalDriver> log)
    {
        _log = log;
        _dir = Path.Combine(Path.GetTempPath(), "kosovapos-agent", "fiscal");
        Directory.CreateDirectory(_dir);
        Config = new FiscalConfig
        {
            TempPath = _dir,
            ComPort = cfg.FiscalComPort,
            Model = cfg.FiscalModel + " (mock)",
            FiscalNumber = cfg.FiscalNumber,
        };
    }

    public bool Available => true;
    public FiscalConfig Config { get; }

    public async Task<FiscalPrintResult> PrintAsync(FiscalPrintRequest req, CancellationToken ct = default)
    {
        var stamp = req.ReceiptNumber ?? DateTime.Now.ToString("HHmmss");
        var path = Path.Combine(_dir, $"Fatura-{stamp}.inp");
        await File.WriteAllTextAsync(path, req.Payload, ct);
        _log.LogInformation("MOCK fiscal receipt #{No} written to {Path} ({Bytes} bytes)",
            req.ReceiptNumber, path, req.Payload.Length);
        return new FiscalPrintResult { Ok = true, FilePath = path, WaitedSeconds = 0 };
    }

    public async Task<FiscalPrintResult> ClearArticlesAsync(int timeoutSeconds = 30, CancellationToken ct = default)
    {
        var path = Path.Combine(_dir, "ClearArticle.inp");
        await File.WriteAllTextAsync(path, "O,1,______,_,__;ALL\n", ct);
        _log.LogInformation("MOCK fiscal clear-articles written to {Path}", path);
        return new FiscalPrintResult { Ok = true, FilePath = path, WaitedSeconds = 0 };
    }
}

public sealed class MockReceiptDriver : IReceiptDriver
{
    private readonly ILogger<MockReceiptDriver> _log;
    public MockReceiptDriver(ILogger<MockReceiptDriver> log) => _log = log;
    public bool Available => true;

    public Task<AgentResult> PrintAsync(ReceiptPrintRequest req, CancellationToken ct = default)
    {
        // Printer and code page are logged because they are the two things that are wrong when a
        // shop says "it prints, but…": the wrong printer looks like a printer that is switched
        // off, and the wrong code page looks like a broken font. Neither is visible in the text.
        _log.LogInformation("MOCK non-fiscal receipt #{No} → printer '{Printer}', code page {Page}: {Lines} lines\n{Document}",
            req.ReceiptNumber, req.PrinterName ?? "(default)", req.CodePage ?? "(agent default)",
            req.Lines.Count,
            string.Join("\n", req.Lines.Select(l =>
                l.CodePage is null ? l.Text : $"{l.Text}   «cp {l.CodePage}»")));
        return Task.FromResult(AgentResult.Success());
    }
}

public sealed class MockBarcodeDriver : IBarcodeDriver
{
    private readonly ILogger<MockBarcodeDriver> _log;
    public MockBarcodeDriver(ILogger<MockBarcodeDriver> log) => _log = log;
    public bool Available => true;

    public Task<AgentResult> PrintAsync(BarcodePrintRequest req, CancellationToken ct = default)
    {
        // The TSPL the real driver would emit, so the layout (price size, whether the bars kept
        // their human-readable digits) can be read off a Linux box instead of a shop's printer.
        var tspl = Windows.WindowsBarcodeDriver.BuildTspl(req, Math.Clamp(req.Copies, 1, 1000));
        _log.LogInformation("MOCK barcode label x{Copies} → printer '{Printer}', stock {W}×{H}mm: {Barcode} {Name} {Price:0.00}\n{Tspl}",
            req.Copies, req.PrinterName ?? "(default)", req.LabelWidthMm, req.LabelHeightMm,
            req.Barcode, req.ArticleName, req.Price, tspl);
        return Task.FromResult(AgentResult.Success());
    }
}

/// <summary>
/// Writes the A4 document out as plain text instead of drawing it on paper, so the invoice and
/// the waybill can be inspected — columns, totals, buyer block — off a Windows box.
/// </summary>
public sealed class MockA4Driver : IA4Driver
{
    private readonly ILogger<MockA4Driver> _log;
    private readonly string _dir;

    public MockA4Driver(ILogger<MockA4Driver> log)
    {
        _log = log;
        _dir = Path.Combine(Path.GetTempPath(), "kosovapos-agent", "a4");
        Directory.CreateDirectory(_dir);
    }

    public bool Available => true;

    public async Task<AgentResult> PrintAsync(A4PrintRequest req, CancellationToken ct = default)
    {
        var d = req.Document;
        var text = new StringBuilder();
        text.AppendLine(d.DocumentTitle);
        foreach (var l in d.SellerLines) text.AppendLine("  " + l);
        foreach (var m in d.Meta) text.AppendLine($"  {m.Key} {m.Value}");
        if (d.PartyTitle is not null)
        {
            text.AppendLine($"[{d.PartyTitle}]");
            foreach (var p in d.Party) text.AppendLine($"  {p.Key} {p.Value}");
        }
        text.AppendLine(string.Join(" | ", d.Columns.Select(c => c.Header)));
        foreach (var r in d.Rows) text.AppendLine(string.Join(" | ", r.Cells));
        if (d.SummaryTitle is not null)
        {
            text.AppendLine($"[{d.SummaryTitle}]");
            foreach (var r in d.SummaryRows) text.AppendLine("  " + string.Join(" | ", r.Cells));
        }
        foreach (var t in d.Totals) text.AppendLine($"  {t.Key} {t.Value}");
        foreach (var n in d.Notes) text.AppendLine(n);

        var safe = string.Concat((req.Title.Length == 0 ? "a4" : req.Title)
            .Select(c => char.IsLetterOrDigit(c) ? c : '-'));
        var path = Path.Combine(_dir, $"{safe}.txt");
        await File.WriteAllTextAsync(path, text.ToString(), ct);

        _log.LogInformation("MOCK A4 '{Title}' → printer '{Printer}' x{Copies}, written to {Path}\n{Doc}",
            req.Title, req.PrinterName ?? "(default)", req.Copies, path, text.ToString());
        return AgentResult.Success();
    }
}

public sealed class MockScaleDriver : IScaleDriver
{
    private static readonly Random Rng = new();
    public bool Available => true;

    public Task<ScaleReadResult> ReadAsync(CancellationToken ct = default) =>
        Task.FromResult(new ScaleReadResult
        {
            Ok = true,
            Weight = Math.Round((decimal)(Rng.NextDouble() * 2 + 0.1), 3),
            Unit = "kg",
            Stable = true,
        });
}

/// <summary>Stand-in printer list, so the POS's printer pickers can be exercised off Windows.</summary>
public sealed class MockPrinterEnumerator : IPrinterEnumerator
{
    private readonly AgentConfig _cfg;
    public MockPrinterEnumerator(AgentConfig cfg) => _cfg = cfg;

    public PrinterList List() => new()
    {
        Printers = ["EPSON TM-T20 Receipt", "HPRT HT300 (labels)", "HP LaserJet A4", "Microsoft Print to PDF"],
        Default = "Microsoft Print to PDF",
        ConfiguredReceipt = _cfg.ReceiptPrinter,
        ConfiguredBarcode = _cfg.BarcodePrinter,
        ConfiguredInvoice = _cfg.InvoicePrinter,
    };
}
