using System.Reflection;
using KosovaPOS.Agent;
using KosovaPOS.Agent.Contracts;
using KosovaPOS.Agent.Drivers;
using KosovaPOS.Agent.Drivers.Mock;
using KosovaPOS.Agent.Drivers.Windows;

// ---------------------------------------------------------------------------
// KosovaPOS local hardware Agent.
//
// Runs on the cashier's PC next to the fiscal/receipt/barcode printers + scale.
// The Blazor browser (same machine) fetches http://127.0.0.1:9099 — so the POS
// server never needs an inbound path to the NAT'd cashier PC. On Windows it uses
// the real drivers; elsewhere (or with AGENT_MOCK=true) it uses mocks so the whole
// pipeline is testable off a Windows box.
// ---------------------------------------------------------------------------

var cfg = AgentConfig.FromEnvironment();
var useReal = OperatingSystem.IsWindows() && !cfg.ForceMock;

var builder = WebApplication.CreateBuilder(args);

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

app.Logger.LogInformation("KosovaPOS Agent {Version} on :{Port} — {Mode} drivers",
    version, cfg.Port, useReal ? "REAL hardware" : "MOCK");

app.Run();
