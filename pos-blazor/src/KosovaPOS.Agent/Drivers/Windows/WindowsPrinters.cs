using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using KosovaPOS.Agent.Contracts;

namespace KosovaPOS.Agent.Drivers.Windows;

/// <summary>
/// The printers Windows knows about on this PC.
///
/// The POS asks for this list so the shop can PICK its receipt and label printers instead of
/// typing their Windows names by hand. A mistyped printer name is the worst kind of failure
/// here: it is silent, it looks exactly like a printer that is switched off, and the only
/// clue is a line in a log file nobody reads.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsPrinterEnumerator : IPrinterEnumerator
{
    private readonly AgentConfig _cfg;
    public WindowsPrinterEnumerator(AgentConfig cfg) => _cfg = cfg;

    public PrinterList List() => new()
    {
        Printers = WindowsPrinters.Installed(),
        Default = WindowsPrinters.Default(),
        ConfiguredReceipt = _cfg.ReceiptPrinter,
        ConfiguredBarcode = _cfg.BarcodePrinter,
    };
}

/// <summary>Win32 printer discovery (winspool), shared by the enumerator and the raw drivers.</summary>
[SupportedOSPlatform("windows")]
internal static class WindowsPrinters
{
    private const int PRINTER_ENUM_LOCAL = 2;
    private const int PRINTER_ENUM_CONNECTIONS = 4;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTER_INFO_4
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pPrinterName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pServerName;
        public uint Attributes;
    }

    [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumPrinters(int flags, string? name, int level, IntPtr pPrinterEnum,
                                            int cbBuf, out int pcbNeeded, out int pcReturned);

    [DllImport("winspool.drv", EntryPoint = "GetDefaultPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetDefaultPrinter(StringBuilder? buffer, ref int size);

    /// <summary>Every printer this PC can print to: local queues plus mapped network ones.</summary>
    public static List<string> Installed()
    {
        const int level = 4; // PRINTER_INFO_4: name only, and the only level that works for
                             // network connections without touching the remote spooler.
        const int flags = PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS;

        // Two-pass call: the first tells us how big the buffer must be.
        EnumPrinters(flags, null, level, IntPtr.Zero, 0, out var needed, out _);
        if (needed <= 0)
            return new List<string>();

        var buffer = Marshal.AllocHGlobal(needed);
        try
        {
            if (!EnumPrinters(flags, null, level, buffer, needed, out _, out var count))
                return new List<string>();

            var size = Marshal.SizeOf<PRINTER_INFO_4>();
            var names = new List<string>(count);
            for (var i = 0; i < count; i++)
            {
                var info = Marshal.PtrToStructure<PRINTER_INFO_4>(buffer + i * size);
                if (!string.IsNullOrWhiteSpace(info.pPrinterName))
                    names.Add(info.pPrinterName);
            }
            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    /// <summary>
    /// The Windows default printer, or null if the PC has none.
    ///
    /// ⚠ The agent runs as a service under LocalSystem, which has its own user profile and
    /// therefore its OWN default printer — usually none at all, even when the cashier's
    /// account has one. So this is a best-effort convenience, never something to rely on:
    /// the shop should still name its printers explicitly.
    /// </summary>
    public static string? Default()
    {
        var size = 0;
        GetDefaultPrinter(null, ref size);   // asks for the length; fails with INSUFFICIENT_BUFFER
        if (size <= 1 || Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
            return null;

        var sb = new StringBuilder(size);
        return GetDefaultPrinter(sb, ref size) && sb.Length > 0 ? sb.ToString() : null;
    }

    /// <summary>
    /// The printer a request should go to: the one the POS named, else the one the agent was
    /// installed with, else this PC's default. Returns null only when the PC has no printer
    /// to fall back to at all.
    /// </summary>
    public static string? Resolve(string? requested, string? configured) =>
        !string.IsNullOrWhiteSpace(requested)  ? requested
      : !string.IsNullOrWhiteSpace(configured) ? configured
      : Default();
}
