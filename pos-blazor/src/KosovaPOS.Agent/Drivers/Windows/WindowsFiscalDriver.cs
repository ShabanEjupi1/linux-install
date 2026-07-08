using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using KosovaPOS.Agent.Contracts;

namespace KosovaPOS.Agent.Drivers.Windows;

/// <summary>
/// Real fiscal transport, ported from the desktop
/// <c>FiscalPrinterService.GenerateFiscalReceipt</c> + <c>SendToFiscalPrinterAndWait</c>.
///
/// F-Link (the Kosovo fiscal middleware) watches <c>FISCAL_TEMP_PATH</c> for a file
/// named <c>Fatura.inp</c>, sends it to the fiscal device over COM, then deletes it
/// (success) or writes a <c>.err</c> sidecar (failure). We drop the payload the
/// server built and poll for that outcome. No COM I/O happens in-process — F-Link
/// owns the port — so this needs only file access, but we still surface COM-port
/// diagnostics the way the desktop app did.
/// </summary>
public sealed class WindowsFiscalDriver : IFiscalDriver
{
    private readonly string _tempPath;
    private readonly string _errorsPath;
    private readonly string _comPort;
    private readonly ILogger<WindowsFiscalDriver> _log;

    public WindowsFiscalDriver(AgentConfig cfg, ILogger<WindowsFiscalDriver> log)
    {
        _log = log;
        _tempPath = NormalizePath(cfg.FiscalTempPath);
        _errorsPath = Path.Combine(_tempPath, "PrintErrors");
        _comPort = cfg.FiscalComPort;
        Directory.CreateDirectory(_tempPath);
        Directory.CreateDirectory(_errorsPath);
        Config = new FiscalConfig
        {
            TempPath = _tempPath,
            ComPort = _comPort,
            Model = cfg.FiscalModel,
            FiscalNumber = cfg.FiscalNumber,
        };
    }

    public bool Available => true;
    public FiscalConfig Config { get; }

    public async Task<FiscalPrintResult> PrintAsync(FiscalPrintRequest req, CancellationToken ct = default)
    {
        // F-Link requires the file be named exactly "Fatura.inp".
        var filePath = Path.Combine(_tempPath, "Fatura.inp");

        // A COM port must exist for F-Link to reach the device (advisory only).
        var ports = SerialPort.GetPortNames();
        if (ports.Length == 0)
            return Fail(req, filePath, "Asnjë port COM nuk u gjet në sistem. Lidhni printerin fiskal.");

        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
            await File.WriteAllTextAsync(filePath, req.Payload, new UTF8Encoding(false), ct);
            _log.LogInformation("Fiscal Fatura.inp written for receipt #{No}", req.ReceiptNumber);
        }
        catch (Exception ex)
        {
            return Fail(req, filePath, $"Nuk u shkrua dot skedari fiskal: {ex.Message}");
        }

        // Poll for F-Link to consume the file, or an error sidecar to appear.
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(req.TimeoutSeconds, 5, 120));
        var errFile = Path.ChangeExtension(filePath, ".err");
        while (sw.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(filePath))
            {
                if (File.Exists(errFile))
                {
                    var err = await SafeRead(errFile);
                    return Fail(req, filePath, $"F-Link raportoi gabim: {err}");
                }
                _log.LogInformation("Fiscal receipt #{No} accepted in {S:0.0}s", req.ReceiptNumber, sw.Elapsed.TotalSeconds);
                return new FiscalPrintResult { Ok = true, FilePath = filePath, WaitedSeconds = sw.Elapsed.TotalSeconds };
            }
            if (File.Exists(errFile))
            {
                var err = await SafeRead(errFile);
                TryDelete(filePath);
                return Fail(req, filePath, $"F-Link raportoi gabim: {err}");
            }
            await Task.Delay(250, ct);
        }

        MoveToErrorFolder(filePath, "timeout");
        return Fail(req, filePath,
            $"Printeri fiskal nuk u përgjigj për {sw.Elapsed.TotalSeconds:0} sekonda. " +
            "Kontrollo që F-Link është aktiv dhe printeri është i lidhur.");
    }

    private FiscalPrintResult Fail(FiscalPrintRequest req, string filePath, string error)
    {
        _log.LogWarning("Fiscal receipt #{No} failed: {Error}", req.ReceiptNumber, error);
        return new FiscalPrintResult { Ok = false, Error = error, FilePath = filePath };
    }

    private static async Task<string> SafeRead(string path)
    {
        try { return (await File.ReadAllTextAsync(path)).Trim(); } catch { return "(unreadable)"; }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* in use by F-Link */ }
    }

    private void MoveToErrorFolder(string filePath, string reason)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            var dest = Path.Combine(_errorsPath, $"{DateTime.Now:yyyyMMdd-HHmmss}-{reason}-{Path.GetFileName(filePath)}");
            File.Move(filePath, dest, overwrite: true);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Could not move failed fiscal file"); }
    }

    private static string NormalizePath(string p)
    {
        p = p.Trim();
        if (!p.EndsWith(Path.DirectorySeparatorChar) && !p.EndsWith('/') && !p.EndsWith('\\'))
            p += Path.DirectorySeparatorChar;
        return p;
    }
}
