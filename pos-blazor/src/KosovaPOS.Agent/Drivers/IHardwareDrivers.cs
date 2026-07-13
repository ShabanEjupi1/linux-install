using KosovaPOS.Agent.Contracts;

namespace KosovaPOS.Agent.Drivers;

/// <summary>
/// Drives the fiscal device. On Windows this drops the F-Link INP payload into
/// F-Link's watched folder and polls for consumption; elsewhere a mock stands in.
/// </summary>
public interface IFiscalDriver
{
    bool Available { get; }
    FiscalConfig Config { get; }
    Task<FiscalPrintResult> PrintAsync(FiscalPrintRequest req, CancellationToken ct = default);

    /// <summary>
    /// Clears the article (PLU) table out of the fiscal printer's own memory — the F-Link
    /// <c>O …;ALL</c> command. The device rejects a sale whose article name does not match the
    /// one it already holds under that PLU, so after a price or name change it starts refusing
    /// receipts, and the till stops. Clearing the table is what unblocks it: the next sale sends
    /// its articles inline and the printer accepts them.
    ///
    /// It is a separate method, not another payload, because F-Link reads this command from a
    /// different file (<c>ClearArticle.inp</c>, not <c>Fatura.inp</c>) — which is exactly the
    /// detail that made the shop keep a .bat file on the desktop to do it.
    /// </summary>
    Task<FiscalPrintResult> ClearArticlesAsync(int timeoutSeconds = 30, CancellationToken ct = default);
}

/// <summary>Prints a non-fiscal (courtesy) receipt on the local receipt printer.</summary>
public interface IReceiptDriver
{
    bool Available { get; }
    Task<AgentResult> PrintAsync(ReceiptPrintRequest req, CancellationToken ct = default);
}

/// <summary>Prints barcode labels on the local label printer.</summary>
public interface IBarcodeDriver
{
    bool Available { get; }
    Task<AgentResult> PrintAsync(BarcodePrintRequest req, CancellationToken ct = default);
}

/// <summary>
/// Prints A4 paper (the invoice, the waybill) on a named printer, with no print dialog.
/// The browser cannot choose a printer — kiosk-printing always goes to the Windows default,
/// which on a till is the thermal roll. Only a process on the PC can route the sheet.
/// </summary>
public interface IA4Driver
{
    bool Available { get; }
    Task<AgentResult> PrintAsync(A4PrintRequest req, CancellationToken ct = default);
}

/// <summary>Reads the current weight from a serial scale.</summary>
public interface IScaleDriver
{
    bool Available { get; }
    Task<ScaleReadResult> ReadAsync(CancellationToken ct = default);
}

/// <summary>Lists the printers installed on this PC, so the POS can offer them as a choice.</summary>
public interface IPrinterEnumerator
{
    PrinterList List();
}
