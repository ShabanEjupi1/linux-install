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
    builder.Services.AddSingleton<IA4Driver, WindowsA4Driver>();
    builder.Services.AddSingleton<IScaleDriver, WindowsScaleDriver>();
    builder.Services.AddSingleton<IPrinterEnumerator, WindowsPrinterEnumerator>();
}
else
{
    builder.Services.AddSingleton<IFiscalDriver, MockFiscalDriver>();
    builder.Services.AddSingleton<IReceiptDriver, MockReceiptDriver>();
    builder.Services.AddSingleton<IBarcodeDriver, MockBarcodeDriver>();
    builder.Services.AddSingleton<IA4Driver, MockA4Driver>();
    builder.Services.AddSingleton<IScaleDriver, MockScaleDriver>();
    builder.Services.AddSingleton<IPrinterEnumerator, MockPrinterEnumerator>();
}

var app = builder.Build();
app.UseCors(CorsPolicy);

var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

app.MapGet("/health", (IFiscalDriver fiscal, IReceiptDriver receipt, IBarcodeDriver barcode,
                       IA4Driver a4, IScaleDriver scale) =>
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
            A4 = a4.Available,
            Scale = scale.Available,
        },
    }));

app.MapPost("/fiscal/print", async (FiscalPrintRequest req, IFiscalDriver fiscal, CancellationToken ct) =>
    Results.Ok(await fiscal.PrintAsync(req, ct)));

// Clears the article table out of the fiscal printer's memory. The device refuses a sale whose
// article name disagrees with the one it already holds, so after a price change it starts
// rejecting receipts and the till stops dead — and the only fix the shop had was a .bat file on
// the Windows desktop. It is a POST with no body: there is exactly one thing this command can do.
app.MapPost("/fiscal/clear-articles", async (IFiscalDriver fiscal, CancellationToken ct) =>
    Results.Ok(await fiscal.ClearArticlesAsync(30, ct)));

app.MapPost("/receipt/print", async (ReceiptPrintRequest req, IReceiptDriver receipt, CancellationToken ct) =>
    Results.Ok(await receipt.PrintAsync(req, ct)));

app.MapPost("/barcode/print", async (BarcodePrintRequest req, IBarcodeDriver barcode, CancellationToken ct) =>
    Results.Ok(await barcode.PrintAsync(req, ct)));

// The A4 paper: the invoice and the waybill, drawn with GDI onto the printer the shop chose.
// This endpoint is the whole reason the agent can do something the browser cannot — pick which
// printer a document goes to. Give it room: a laser warming up from sleep is not quick.
app.MapPost("/a4/print", async (A4PrintRequest req, IA4Driver a4, CancellationToken ct) =>
    Results.Ok(await a4.PrintAsync(req, ct)));

// The printers on THIS PC, so the POS can offer them as a list instead of asking the shop to
// type a Windows printer name exactly right.
app.MapGet("/printers", (IPrinterEnumerator printers) => Results.Ok(printers.List()));

app.MapGet("/scale/read", async (IScaleDriver scale, CancellationToken ct) =>
    Results.Ok(await scale.ReadAsync(ct)));

app.Logger.LogInformation("KosovaPOS Agent {Version} on :{Port} — {Mode} drivers, logs in {LogDir}",
    version, cfg.Port, useReal ? "REAL hardware" : "MOCK", cfg.LogDirectory);

app.Run();
