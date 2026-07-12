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
