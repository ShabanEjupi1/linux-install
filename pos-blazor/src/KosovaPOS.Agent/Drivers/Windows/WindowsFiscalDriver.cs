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

    /// <summary>F-Link requires the receipt file be named exactly this.</summary>
    private const string ReceiptFile = "Fatura.inp";

    /// <summary>
    /// And it reads the clear-articles command from a DIFFERENT file. Writing the O command into
    /// Fatura.inp does nothing at all — which is why the shop has been double-clicking a .bat on
    /// the desktop instead.
    /// </summary>
    private const string ClearArticleFile = "ClearArticle.inp";

    /// <summary>The whole of the clear-articles command. Same bytes as the shop's Clear Article.bat.</summary>
    private const string ClearArticlePayload = "O,1,______,_,__;ALL\n";

    public Task<FiscalPrintResult> PrintAsync(FiscalPrintRequest req, CancellationToken ct = default) =>
        SendAsync(ReceiptFile, req.Payload, $"receipt #{req.ReceiptNumber}", req.TimeoutSeconds, ct);

    public Task<FiscalPrintResult> ClearArticlesAsync(int timeoutSeconds = 30, CancellationToken ct = default) =>
        SendAsync(ClearArticleFile, ClearArticlePayload, "clear-articles", timeoutSeconds, ct);

    /// <summary>
    /// Drops an INP file into F-Link's watched folder and waits for it to be consumed. F-Link
    /// deletes the file on success and writes a <c>.err</c> sidecar on failure; a file that is
    /// still sitting there when the timeout expires is moved aside, because a leftover INP is
    /// itself a cause of the next command failing.
    /// </summary>
    private async Task<FiscalPrintResult> SendAsync(
        string fileName, string payload, string label, int timeoutSeconds, CancellationToken ct)
    {
        var filePath = Path.Combine(_tempPath, fileName);

        // A COM port must exist for F-Link to reach the device (advisory only).
        var ports = SerialPort.GetPortNames();
        if (ports.Length == 0)
            return Fail(label, filePath, "Asnjë port COM nuk u gjet në sistem. Lidhni printerin fiskal.");

        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
            await File.WriteAllTextAsync(filePath, payload, new UTF8Encoding(false), ct);
            _log.LogInformation("Fiscal {File} written for {Label}", fileName, label);
        }
        catch (Exception ex)
        {
            return Fail(label, filePath, $"Nuk u shkrua dot skedari fiskal: {ex.Message}");
        }

        // Poll for F-Link to consume the file, or an error sidecar to appear.
        var sw = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 120));
        var errFile = Path.ChangeExtension(filePath, ".err");
        while (sw.Elapsed < timeout)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(filePath))
            {
                if (File.Exists(errFile))
                {
                    var err = await SafeRead(errFile);
                    return Fail(label, filePath, $"F-Link raportoi gabim: {err}");
                }
                _log.LogInformation("Fiscal {Label} accepted in {S:0.0}s", label, sw.Elapsed.TotalSeconds);
                return new FiscalPrintResult { Ok = true, FilePath = filePath, WaitedSeconds = sw.Elapsed.TotalSeconds };
            }
            if (File.Exists(errFile))
            {
                var err = await SafeRead(errFile);
                TryDelete(filePath);
                return Fail(label, filePath, $"F-Link raportoi gabim: {err}");
            }
            await Task.Delay(250, ct);
        }

        MoveToErrorFolder(filePath, "timeout");
        return Fail(label, filePath,
            $"Printeri fiskal nuk u përgjigj për {sw.Elapsed.TotalSeconds:0} sekonda. " +
            "Kontrollo që F-Link është aktiv dhe printeri është i lidhur.");
    }

    private FiscalPrintResult Fail(string label, string filePath, string error)
    {
        _log.LogWarning("Fiscal {Label} failed: {Error}", label, error);
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
