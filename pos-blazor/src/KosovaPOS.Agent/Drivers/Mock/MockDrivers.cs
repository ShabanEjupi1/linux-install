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
}

public sealed class MockReceiptDriver : IReceiptDriver
{
    private readonly ILogger<MockReceiptDriver> _log;
    public MockReceiptDriver(ILogger<MockReceiptDriver> log) => _log = log;
    public bool Available => true;

    public Task<AgentResult> PrintAsync(ReceiptPrintRequest req, CancellationToken ct = default)
    {
        _log.LogInformation("MOCK non-fiscal receipt #{No}: {Lines} lines\n{Document}",
            req.ReceiptNumber, req.Lines.Count, string.Join("\n", req.Lines.Select(l => l.Text)));
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
        _log.LogInformation("MOCK barcode label x{Copies}: {Barcode} {Name} {Price:0.00}",
            req.Copies, req.Barcode, req.ArticleName, req.Price);
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
