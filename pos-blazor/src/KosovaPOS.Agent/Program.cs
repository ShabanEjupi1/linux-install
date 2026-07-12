using System.Reflection;
using System.Text;
using KosovaPOS.Agent;
using KosovaPOS.Agent.Contracts;
using KosovaPOS.Agent.Drivers;
using KosovaPOS.Agent.Drivers.Mock;
using KosovaPOS.Agent.Drivers.Windows;
using KosovaPOS.Agent.Logging;
using Microsoft.Extensions.Hosting.WindowsServices;

// ---------------------------------------------------------------------------
// KosovaPOS local hardware Agent.
//
// Runs on the cashier's PC next to the fiscal/receipt/barcode printers + scale.
// The Blazor browser (same machine) fetches http://127.0.0.1:9099 — so the POS
// server never needs an inbound path to the NAT'd cashier PC. On Windows it uses
// the real drivers; elsewhere (or with AGENT_MOCK=true) it uses mocks so the whole
// pipeline is testable off a Windows box.
// ---------------------------------------------------------------------------

// Both raw-print drivers encode their bytes as code page 1252 — the one the thermal and
// label printers expect, and the only one that carries ë and ç. .NET Core ships ONLY
// UTF-8/ASCII/Latin1: without this line Encoding.GetEncoding(1252) throws
// NotSupportedException, and every receipt and every label fails to print.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var cfg = AgentConfig.FromEnvironment();
var useReal = OperatingSystem.IsWindows() && !cfg.ForceMock;

// Under the SCM the working directory is C:\Windows\System32, so the content root
// has to be pinned to the exe's folder. AddWindowsService (the IServiceCollection
// overload) does NOT do this — only the IHostBuilder UseWindowsService does, and
// that isn't usable from WebApplicationBuilder.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
});

// Lets the SCM start/stop us (without this, `sc start` reports a timeout and marks
// the service failed). No-op off Windows, so `dotnet run` on Linux is unaffected.
builder.Services.AddWindowsService(o => o.ServiceName = "KosovaPOSAgent");

// A service has no stdout — without a file sink, a failed fiscal print is invisible.
// Keep the file readable by support: our own driver lines plus start/stop, and only
// warnings-and-worse from the framework (per-request diagnostics would bury them).
builder.Logging.AddProvider(new FileLoggerProvider(cfg.LogDirectory));
builder.Logging.AddFilter<FileLoggerProvider>("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter<FileLoggerProvider>("Microsoft.Hosting.Lifetime", LogLevel.Information);

// Bind to loopback only — the agent must never be reachable off the machine.
builder.WebHost.ConfigureKestrel(k => k.ListenLocalhost(cfg.Port));

builder.Services.AddSingleton(cfg);

const string CorsPolicy = "pos";
builder.Services.AddCors(o => o.AddPolicy(CorsPolicy, p =>
{
    if (cfg.AllowedOrigins == "*")
        p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod();
    else
        p.WithOrigins(cfg.AllowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
         .AllowAnyHeader().AllowAnyMethod();
}));

if (useReal)
{
    builder.Services.AddSingleton<IFiscalDriver, WindowsFiscalDriver>();
    builder.Services.AddSingleton<IReceiptDriver, WindowsReceiptDriver>();
    builder.Services.AddSingleton<IBarcodeDriver, WindowsBarcodeDriver>();
    builder.Services.AddSingleton<IScaleDriver, WindowsScaleDriver>();
}
else
{
    builder.Services.AddSingleton<IFiscalDriver, MockFiscalDriver>();
    builder.Services.AddSingleton<IReceiptDriver, MockReceiptDriver>();
    builder.Services.AddSingleton<IBarcodeDriver, MockBarcodeDriver>();
    builder.Services.AddSingleton<IScaleDriver, MockScaleDriver>();
}

var app = builder.Build();
app.UseCors(CorsPolicy);

var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

app.MapGet("/health", (IFiscalDriver fiscal, IReceiptDriver receipt, IBarcodeDriver barcode, IScaleDriver scale) =>
    Results.Ok(new AgentHealth
    {
        Version = version,
        Platform = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
        RealHardware = useReal,
        Fiscal = fiscal.Config,
        LogDirectory = cfg.LogDirectory,
        Capabilities = new AgentCapabilities
        {
            Fiscal = fiscal.Available,
            Receipt = receipt.Available,
            Barcode = barcode.Available,
            Scale = scale.Available,
        },
    }));

app.MapPost("/fiscal/print", async (FiscalPrintRequest req, IFiscalDriver fiscal, CancellationToken ct) =>
    Results.Ok(await fiscal.PrintAsync(req, ct)));

app.MapPost("/receipt/print", async (ReceiptPrintRequest req, IReceiptDriver receipt, CancellationToken ct) =>
    Results.Ok(await receipt.PrintAsync(req, ct)));

app.MapPost("/barcode/print", async (BarcodePrintRequest req, IBarcodeDriver barcode, CancellationToken ct) =>
    Results.Ok(await barcode.PrintAsync(req, ct)));

app.MapGet("/scale/read", async (IScaleDriver scale, CancellationToken ct) =>
    Results.Ok(await scale.ReadAsync(ct)));

app.Logger.LogInformation("KosovaPOS Agent {Version} on :{Port} — {Mode} drivers, logs in {LogDir}",
    version, cfg.Port, useReal ? "REAL hardware" : "MOCK", cfg.LogDirectory);

app.Run();
