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
    /// <summary>Where the agent writes its rolling logs — surfaced so support can find them.</summary>
    public string LogDirectory { get; set; } = "";
    public DateTimeOffset ServerTime { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// The printers installed on the cashier PC. The POS asks for these so the shop can PICK a
/// printer from a list instead of typing a Windows printer name exactly right — getting that
/// name wrong is silent, and looks identical to a printer that is switched off.
/// </summary>
public sealed class PrinterList
{
    public List<string> Printers { get; set; } = new();
    /// <summary>The Windows default printer — what the agent uses when nothing is configured.</summary>
    public string? Default { get; set; }
    /// <summary>What the agent is configured to use today (RECEIPT_PRINTER / BARCODE_PRINTER).</summary>
    public string? ConfiguredReceipt { get; set; }
    public string? ConfiguredBarcode { get; set; }
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

/// <summary>
/// A receipt that has ALREADY been laid out, by <c>ReceiptFormatter</c>, into fixed-width
/// lines on the 42-column grid of an 80mm roll. The agent only stresses and emits them.
///
/// It deliberately does NOT carry the sale (items, totals, shop header): when it did, the
/// agent laid the receipt out a second time and the two layouts drifted — the paper the
/// cashier hands over and the page at <c>/kupon/{n}</c> were not the same document. Send
/// the document, not the data.
/// </summary>
public sealed class ReceiptPrintRequest
{
    /// <summary>For the agent's log only — the layout is already fixed in <see cref="Lines"/>.</summary>
    public string ReceiptNumber { get; set; } = "";
    public List<ReceiptDocumentLine> Lines { get; set; } = new();
    public string? PrinterName { get; set; }

    /// <summary>
    /// The ESC/POS code page this printer is to be switched to: "1252", "852", "858", "850",
    /// "1250", "437", or "ascii" to strip the accents. Null = whatever the agent was installed
    /// with.
    ///
    /// It travels with the job rather than living in the agent's environment because there is
    /// no way to know from here which page a given printer's firmware actually honours — the
    /// shop finds out by printing the sample at <c>/pajisjet</c> and picking the line that
    /// rendered ë correctly. That has to be changeable from the POS, without reinstalling.
    /// </summary>
    public string? CodePage { get; set; }
}

/// <param name="Emphasis">0 normal · 1 bold · 2 double-height. Matches <c>ReceiptEmphasis</c>.</param>
public sealed class ReceiptDocumentLine
{
    public string Text { get; set; } = "";
    public int Emphasis { get; set; }

    /// <summary>
    /// Prints this one line under a different code page than the rest of the job. Only the
    /// encoding sample uses it — one line per candidate page, so the shop can hold the paper
    /// up and see which one prints "ë ç" instead of "Î´Ã§".
    /// </summary>
    public string? CodePage { get; set; }
}

// --- Barcode label ----------------------------------------------------------

public sealed class BarcodePrintRequest
{
    public string Barcode { get; set; } = "";
    public string ArticleName { get; set; } = "";
    public decimal Price { get; set; }
    public int Copies { get; set; } = 1;
    public string? PrinterName { get; set; }

    /// <summary>
    /// The label stock, in mm. The TSPL document is laid out against these — a label
    /// built for 40×30 stock and printed on the shop's 55×25 comes out cropped, so the
    /// size travels with the request rather than being assumed by the driver.
    /// </summary>
    public int LabelWidthMm { get; set; } = 55;
    public int LabelHeightMm { get; set; } = 25;
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
