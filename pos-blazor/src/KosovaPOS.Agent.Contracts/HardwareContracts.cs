namespace KosovaPOS.Agent.Contracts;

// ---------------------------------------------------------------------------
// Wire contracts shared by the Blazor server (which builds payloads) and the
// KosovaPOS.Agent that runs on the cashier's PC next to the hardware.
// The browser fetches http://127.0.0.1:9099 on the same machine, so the agent
// never needs a public/inbound network path (it sits behind NAT with the PC).
// Keep this assembly dependency-free (POCOs only) so the agent stays tiny.
// ---------------------------------------------------------------------------

/// <summary>What the agent can do on this machine. Returned by <c>GET /health</c>.</summary>
public sealed class AgentHealth
{
    public string Version { get; set; } = "";
    public string Platform { get; set; } = "";
    /// <summary>True when the agent is talking to real hardware; false = dev/mock drivers.</summary>
    public bool RealHardware { get; set; }
    public AgentCapabilities Capabilities { get; set; } = new();
    /// <summary>Fiscal transport config the agent is using (folder / COM port / model).</summary>
    public FiscalConfig Fiscal { get; set; } = new();
    public DateTimeOffset ServerTime { get; set; } = DateTimeOffset.Now;
}

public sealed class AgentCapabilities
{
    public bool Fiscal { get; set; }
    public bool Receipt { get; set; }
    public bool Barcode { get; set; }
    public bool Scale { get; set; }
}

public sealed class FiscalConfig
{
    public string TempPath { get; set; } = "";
    public string ComPort { get; set; } = "";
    public string Model { get; set; } = "";
    public string FiscalNumber { get; set; } = "";
}

// --- Fiscal -----------------------------------------------------------------

/// <summary>
/// A ready-to-drop F-Link INP payload. The server builds the exact text
/// (<c>FiscalReceiptBuilder</c> in Core) so all fiscal-format logic stays
/// server-side and testable; the agent only writes it to F-Link's watched
/// folder and polls for the result.
/// </summary>
public sealed class FiscalPrintRequest
{
    /// <summary>The full F-Link INP file body (the <c>S,1,…;…</c> lines).</summary>
    public string Payload { get; set; } = "";
    /// <summary>Receipt number, for logging/audit correlation only.</summary>
    public string? ReceiptNumber { get; set; }
    /// <summary>How long to wait for F-Link to consume the file before failing.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}

public sealed class FiscalPrintResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
    /// <summary>Path the payload was written to (for troubleshooting).</summary>
    public string? FilePath { get; set; }
    public double WaitedSeconds { get; set; }
}

// --- Non-fiscal receipt -----------------------------------------------------

/// <summary>Structured receipt the agent renders to the local receipt printer.</summary>
public sealed class ReceiptPrintRequest
{
    public string BusinessName { get; set; } = "";
    public string? Address { get; set; }
    public string? FiscalNumber { get; set; }
    public string ReceiptNumber { get; set; } = "";
    public DateTimeOffset Date { get; set; } = DateTimeOffset.Now;
    public string CashierName { get; set; } = "";
    public string PaymentMethod { get; set; } = "";
    public List<ReceiptLineDto> Lines { get; set; } = new();
    public decimal Subtotal { get; set; }
    public decimal Vat { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Change { get; set; }
    public string? Footer { get; set; }
    public string? PrinterName { get; set; }
}

public sealed class ReceiptLineDto
{
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
}

// --- Barcode label ----------------------------------------------------------

public sealed class BarcodePrintRequest
{
    public string Barcode { get; set; } = "";
    public string ArticleName { get; set; } = "";
    public decimal Price { get; set; }
    public int Copies { get; set; } = 1;
    public string? PrinterName { get; set; }
}

// --- Scale ------------------------------------------------------------------

public sealed class ScaleReadResult
{
    public bool Ok { get; set; }
    public decimal Weight { get; set; }
    public string Unit { get; set; } = "kg";
    public bool Stable { get; set; }
    public string? Error { get; set; }
}

/// <summary>Uniform ok/error envelope for the simple print endpoints.</summary>
public sealed class AgentResult
{
    public bool Ok { get; set; }
    public string? Error { get; set; }

    public static AgentResult Success() => new() { Ok = true };
    public static AgentResult Fail(string error) => new() { Ok = false, Error = error };
}
