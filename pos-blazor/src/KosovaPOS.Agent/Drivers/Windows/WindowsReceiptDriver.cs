using System.Runtime.Versioning;
using System.Text;
using KosovaPOS.Agent.Contracts;

namespace KosovaPOS.Agent.Drivers.Windows;

/// <summary>
/// Non-fiscal (courtesy) receipt printer. The desktop <c>ReceiptPrinterService</c>
/// rendered via GDI (<c>System.Drawing.Printing</c>), which is Windows-only at
/// runtime and awkward for a headless agent. We instead emit ESC/POS raw text and
/// send it through the same winspool raw path used for barcodes — the standard,
/// driver-independent way to drive an 80mm thermal receipt printer.
///
/// The layout is NOT decided here: the server sends the receipt already laid out on
/// the 42-column grid by <c>ReceiptFormatter</c>, and this driver only stresses the
/// lines and emits them. That is what makes the paper and the <c>/kupon/{n}</c> page
/// the same document — printing raw ESC/POS means no Windows print dialog, so nothing
/// visible tells the cashier if the two have drifted apart.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsReceiptDriver : IReceiptDriver
{
    private static readonly byte[] Init = { 0x1B, 0x40 };           // ESC @
    private static readonly byte[] BoldOn = { 0x1B, 0x45, 0x01 };
    private static readonly byte[] BoldOff = { 0x1B, 0x45, 0x00 };
    private static readonly byte[] DoubleHeightOn = { 0x1D, 0x21, 0x01 };  // GS ! — height x2, width x1
    private static readonly byte[] DoubleHeightOff = { 0x1D, 0x21, 0x00 };
    private static readonly byte[] Cut = { 0x1D, 0x56, 0x42, 0x00 }; // GS V B 0

    /// <summary>
    /// The code pages an 80mm thermal printer can be switched to with ESC t, and the .NET
    /// encoding whose bytes that page expects. Sending the bytes without sending ESC t is
    /// what garbled ë and ç: the printer powers up on PC437 (where 0xEB is δ) no matter what
    /// the sender meant, so the page has to be selected on every job.
    /// </summary>
    public static readonly Dictionary<string, (byte Page, int CodePage)> CodePages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["1252"] = (16, 1252),  // WPC1252 — has ë ç Ë Ç; the one virtually every clone claims
        ["852"]  = (18, 852),   // PC852 Latin-2
        ["858"]  = (19, 858),
        ["850"]  = (2, 850),    // PC850 Multilingual
        ["1250"] = (45, 1250),  // WPC1250 Central European
        ["437"]  = (0, 437),    // PC437 — no Albanian letters; transliterated below
    };

    /// <summary>
    /// Last resort for a printer whose firmware ignores ESC t. A receipt that says
    /// "Faleminderit per blerjen" is ugly; one that prints δ where ë belongs is broken.
    /// Every mapping is one char to one char: ReceiptFormatter has already padded each line
    /// to the 42-column grid, and a substitution that changed the length would push the
    /// amount column out of alignment or wrap the line.
    /// </summary>
    private static readonly Dictionary<char, char> Ascii = new()
    {
        ['ë'] = 'e', ['Ë'] = 'E', ['ç'] = 'c', ['Ç'] = 'C',
        ['€'] = 'E', ['’'] = '\'', ['–'] = '-', ['—'] = '-',
    };

    private static string Transliterate(string s) =>
        string.Create(s.Length, s, static (dst, src) =>
        {
            for (var i = 0; i < src.Length; i++)
                dst[i] = Ascii.TryGetValue(src[i], out var c) ? c : src[i];
        });

    private readonly AgentConfig _cfg;
    private readonly ILogger<WindowsReceiptDriver> _log;

    public WindowsReceiptDriver(AgentConfig cfg, ILogger<WindowsReceiptDriver> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public bool Available => true;

    public Task<AgentResult> PrintAsync(ReceiptPrintRequest req, CancellationToken ct = default)
    {
        // The POS's choice wins; then the one the agent was installed with; then this PC's
        // default printer. Erroring out just because nobody named a printer was wrong — the
        // shop had a working printer and a saved sale, and got nothing but a red line.
        var printer = WindowsPrinters.Resolve(req.PrinterName, _cfg.ReceiptPrinter);
        if (string.IsNullOrWhiteSpace(printer))
            return Task.FromResult(AgentResult.Fail(
                "Asnjë printer faturash nuk është zgjedhur, dhe ky kompjuter nuk ka printer të parazgjedhur. " +
                "Zgjidhe te Cilësimet → Pajisjet."));

        if (req.Lines.Count == 0)
            return Task.FromResult(AgentResult.Fail("Kuponi erdhi bosh — asnjë rresht për të printuar."));

        try
        {
            // The POS's choice wins over the agent's install-time default, so a shop whose
            // printer ignores WPC1252 fixes its receipts from the Pajisjet screen instead of
            // waiting for someone to reinstall the agent with a different env var.
            var bytes = Build(req, string.IsNullOrWhiteSpace(req.CodePage) ? _cfg.ReceiptCodePage : req.CodePage);
            var ok = RawPrinterHelper.SendBytesToPrinter(printer, bytes);
            _log.LogInformation("Non-fiscal receipt #{No} sent to {Printer}", req.ReceiptNumber, printer);
            return Task.FromResult(ok ? AgentResult.Success() : AgentResult.Fail("Shkrimi te printeri dështoi."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AgentResult.Fail(ex.Message));
        }
    }

    /// <summary>
    /// Resolves a code page name to the ESC t selector and the .NET encoding that page expects.
    /// An unknown name — including "ascii" — falls back to PC437 with the accents transliterated,
    /// which is the one thing every ESC/POS printer ever made can render.
    /// </summary>
    private static (byte Page, int Cp, bool Transliterate) Resolve(string? name)
    {
        if (!string.IsNullOrWhiteSpace(name) && CodePages.TryGetValue(name, out var sel))
            return (sel.Page, sel.CodePage, sel.CodePage == 437);

        return (0, 437, true);
    }

    public static byte[] Build(ReceiptPrintRequest r, string codePage)
    {
        var job = Resolve(codePage);

        using var ms = new MemoryStream();
        void Raw(byte[] b) => ms.Write(b, 0, b.Length);

        Raw(Init);

        // The printer powers up on PC437 (where 0xEB is δ, not ë) no matter what the sender
        // meant, so the page is selected explicitly — and re-selected whenever a line asks for
        // a different one, which is what lets the encoding sample print every candidate page
        // on one slip.
        var current = (byte)(job.Page + 1); // ≠ job.Page, so the first line always emits ESC t
        void SelectPage(byte page)
        {
            if (page == current) return;
            Raw(new byte[] { 0x1B, 0x74, page }); // ESC t n
            current = page;
        }

        // Every line is emitted left-aligned and verbatim: ReceiptFormatter already padded it
        // to the column grid, and asking the printer to centre a pre-centred line would centre
        // the padding too. Double-height is height-only (GS ! 0x01), so the grid still holds.
        foreach (var line in r.Lines)
        {
            var (page, cp, translit) = line.CodePage is { Length: > 0 } ? Resolve(line.CodePage) : job;
            SelectPage(page);

            var enc = Encoding.GetEncoding(cp,
                // Anything the page cannot hold — a supplier's Ć, a stray ™ — becomes '?' rather
                // than throwing away the sale's receipt. Transliterate() catches the ones we know.
                EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);

            switch (line.Emphasis)
            {
                case 1: Raw(BoldOn); break;
                case 2: Raw(DoubleHeightOn); break;
            }

            var body = line.Text.TrimEnd();
            if (translit) body = Transliterate(body);

            var text = enc.GetBytes(body + "\n");
            ms.Write(text, 0, text.Length);

            switch (line.Emphasis)
            {
                case 1: Raw(BoldOff); break;
                case 2: Raw(DoubleHeightOff); break;
            }
        }

        // Feed the last line clear of the cutter, then cut.
        ms.WriteByte((byte)'\n');
        ms.WriteByte((byte)'\n');
        ms.WriteByte((byte)'\n');
        Raw(Cut);
        return ms.ToArray();
    }
}
