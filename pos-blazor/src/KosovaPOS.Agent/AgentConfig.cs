namespace KosovaPOS.Agent;

/// <summary>
/// Agent configuration, read from environment variables (same names the desktop
/// app used, so an existing cashier PC keeps working unchanged).
/// </summary>
public sealed class AgentConfig
{
    /// <summary>Loopback port the browser fetches. Default 9099.</summary>
    public int Port { get; init; } = 9099;

    /// <summary>Force the mock drivers even on Windows (for testing).</summary>
    public bool ForceMock { get; init; }

    // Fiscal (F-Link) transport
    public string FiscalTempPath { get; init; } = "C:\\TEMP\\";
    public string FiscalComPort { get; init; } = "COM1";
    public string FiscalModel { get; init; } = "FP700+";
    public string FiscalNumber { get; init; } = "003910";

    // Receipt / barcode / A4 printers (Windows printer names; null = default printer)
    public string? ReceiptPrinter { get; init; }
    public string? BarcodePrinter { get; init; }

    /// <summary>
    /// Where the A4 paper goes — the invoice and the waybill. Separate from the receipt printer
    /// because it is a different sheet of paper: an invoice sent to the 80mm roll prints as a
    /// metre of curling till receipt, and that is exactly what happens when the browser prints
    /// it, because the browser can only ever use the Windows default printer.
    /// </summary>
    public string? InvoicePrinter { get; init; }

    /// <summary>
    /// The ESC/POS code page the receipt printer is switched to before each job: 1252, 852,
    /// 858, 850, 437, or "ascii" to strip the accents (ë→e). 1252 is the one that carries the
    /// Albanian letters and that almost every 80mm printer supports; a printer whose firmware
    /// ignores the switch can be dropped to "ascii" here without a rebuild.
    /// </summary>
    public string ReceiptCodePage { get; init; } = "1252";

    // Scale (serial)
    public string ScaleComPort { get; init; } = "COM3";
    public int ScaleBaudRate { get; init; } = 9600;

    /// <summary>Origins allowed to call the agent (the Blazor app URL). "*" in dev.</summary>
    public string AllowedOrigins { get; init; } = "*";

    /// <summary>Where the rolling log files go. A service has no console.</summary>
    public string LogDirectory { get; init; } = DefaultLogDirectory();

    private static string DefaultLogDirectory() =>
        OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                           "KosovaPOS", "Agent", "logs")
            : Path.Combine(Path.GetTempPath(), "kosovapos-agent-logs");

    public static AgentConfig FromEnvironment()
    {
        static string? Env(string k) => Environment.GetEnvironmentVariable(k);
        static int IntEnv(string k, int fallback) =>
            int.TryParse(Env(k), out var v) ? v : fallback;
        static bool BoolEnv(string k) =>
            bool.TryParse(Env(k), out var v) && v;

        return new AgentConfig
        {
            Port           = IntEnv("AGENT_PORT", 9099),
            ForceMock      = BoolEnv("AGENT_MOCK"),
            FiscalTempPath = Env("FISCAL_TEMP_PATH") ?? "C:\\TEMP\\",
            FiscalComPort  = Env("FISCAL_PRINTER_PORT") ?? "COM1",
            FiscalModel    = Env("FISCAL_PRINTER_MODEL") ?? "FP700+",
            FiscalNumber   = Env("FISCAL_NUMBER") ?? "003910",
            ReceiptPrinter = Env("RECEIPT_PRINTER"),
            BarcodePrinter = Env("BARCODE_PRINTER"),
            InvoicePrinter = Env("INVOICE_PRINTER"),
            ReceiptCodePage = Env("RECEIPT_CODEPAGE") ?? "1252",
            ScaleComPort   = Env("SCALE_PORT") ?? "COM3",
            ScaleBaudRate  = IntEnv("SCALE_BAUD", 9600),
            AllowedOrigins = Env("AGENT_ALLOWED_ORIGINS") ?? "*",
            LogDirectory   = Env("AGENT_LOG_DIR") ?? DefaultLogDirectory(),
        };
    }
}
