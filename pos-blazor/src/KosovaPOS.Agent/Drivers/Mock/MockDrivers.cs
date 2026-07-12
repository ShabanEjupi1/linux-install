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
        Printers = ["EPSON TM-T20 Receipt", "HPRT HT300 (labels)", "Microsoft Print to PDF"],
        Default = "Microsoft Print to PDF",
        ConfiguredReceipt = _cfg.ReceiptPrinter,
        ConfiguredBarcode = _cfg.BarcodePrinter,
    };
}
