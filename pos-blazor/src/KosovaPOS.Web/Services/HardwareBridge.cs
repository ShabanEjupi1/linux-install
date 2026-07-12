using KosovaPOS.Agent.Contracts;
using KosovaPOS.Core.Printing;
using KosovaPOS.Core.Services;
using KosovaPOS.Core.Services.Hardware;
using KosovaPOS.Models;
using Microsoft.JSInterop;

namespace KosovaPOS.Web.Services;

/// <summary>
/// Server-side facade over the browser's <c>hardware.js</c> module. Pages call
/// this with domain objects (a <see cref="Receipt"/>, an <see cref="Article"/>);
/// it maps them to the agent contracts and invokes JS interop, which fetches the
/// local agent on the cashier PC. Every call degrades gracefully: if the agent is
/// offline the result carries <c>Offline = true</c> instead of throwing, so a sale
/// is never blocked by missing hardware.
///
/// Scoped to the circuit; JS interop only works once the component is interactive
/// (post-render), so callers should invoke from event handlers, not SSR.
/// </summary>
public sealed class HardwareBridge : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly SalesService _sales;
    private readonly BusinessProfileService _profile;
    private IJSObjectReference? _module;

    public HardwareBridge(IJSRuntime js, SalesService sales, BusinessProfileService profile)
    {
        _js = js;
        _sales = sales;
        _profile = profile;
    }

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/hardware.js");

    /// <summary>Agent health, or null if unreachable.</summary>
    public async Task<AgentHealth?> GetHealthAsync()
    {
        try
        {
            var m = await ModuleAsync();
            var h = await m.InvokeAsync<AgentHealth?>("health");
            // The offline path returns a plain {ok:false,...}; a real health has a Version.
            return string.IsNullOrEmpty(h?.Version) ? null : h;
        }
        catch (JSException) { return null; }
        catch (Exception)   { return null; }
    }

    /// <summary>Sends a fiscal receipt to F-Link via the agent.</summary>
    public async Task<FiscalPrintResult> PrintFiscalAsync(Receipt receipt, int timeoutSeconds = 30)
    {
        var req = HardwareMapper.ToFiscalRequest(receipt, timeoutSeconds);
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<FiscalPrintResult>("printFiscal", req);
        }
        catch (Exception ex)
        {
            return new FiscalPrintResult { Ok = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Clears the article (PLU) table out of the fiscal printer's memory — the F-Link
    /// <c>O …;ALL</c> command the shop has been running from a .bat file on the Windows desktop.
    ///
    /// The device holds its own copy of every article it has ever been sent, and refuses a sale
    /// whose name or tax group disagrees with the copy it holds. So the first sale after a price
    /// change can be rejected, and the till stops until the table is cleared. Nothing about the
    /// POS's own data changes: only the printer's memory is emptied, and the next sale re-sends
    /// its articles inline.
    /// </summary>
    public async Task<FiscalPrintResult> ClearFiscalArticlesAsync()
    {
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<FiscalPrintResult>("clearFiscalArticles");
        }
        catch (Exception ex)
        {
            return new FiscalPrintResult { Ok = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Prints the non-fiscal courtesy receipt for a saved sale, straight to the thermal
    /// printer as ESC/POS — no Windows print dialog. The document is laid out here, from
    /// what was actually persisted, so the paper matches the <c>/kupon/{n}</c> page and a
    /// reprint months later matches the original.
    /// </summary>
    public async Task<AgentResult> PrintReceiptAsync(long receiptNumber)
    {
        var invoice = await _sales.GetInvoiceAsync(receiptNumber);
        if (invoice is null) return AgentResult.Fail($"Fatura #{receiptNumber} nuk u gjet.");

        var shop = await _profile.GetSettingsAsync();
        var lines = ReceiptFormatter.Format(invoice, ReceiptHeaderFor(shop));
        var req = HardwareMapper.ToReceiptRequest(invoice.Number, lines, shop);

        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<AgentResult>("printReceipt", req);
        }
        catch (Exception ex) { return AgentResult.Fail(ex.Message); }
    }

    /// <summary>
    /// Prints a short test slip on the chosen receipt printer. The shop must be able to prove
    /// the printer works without ringing up a sale to find out — the first sale is the worst
    /// possible place to discover the printer name is wrong.
    /// </summary>
    public async Task<AgentResult> PrintTestReceiptAsync()
    {
        var shop = await _profile.GetSettingsAsync();
        var name = string.IsNullOrWhiteSpace(shop?.BusinessName) ? "KosovaPOS" : shop!.BusinessName!;

        const int w = ReceiptFormatter.DefaultWidth;
        var lines = new List<ReceiptTextLine>
        {
            new(Center(name.ToUpperInvariant(), w), ReceiptEmphasis.Title),
            new(new string('-', w)),
            new(Center("PROVË PRINTIMI", w), ReceiptEmphasis.Bold),
            new(Center($"{DateTime.Now:dd.MM.yyyy HH:mm}", w)),
            new(new string('-', w)),
            new(Center("Printeri punon. Ky nuk është kupon.", w)),
        };

        var req = HardwareMapper.ToReceiptRequest("TEST", lines, shop);
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<AgentResult>("printReceipt", req);
        }
        catch (Exception ex) { return AgentResult.Fail(ex.Message); }

        static string Center(string s, int width)
        {
            if (s.Length >= width) return s[..width];
            return new string(' ', (width - s.Length) / 2) + s;
        }
    }

    /// <summary>
    /// Prints one sample line per candidate code page, each line printed under the page it
    /// names. This is the only way to learn which page a printer's firmware really honours:
    /// the POS cannot ask it, and a printer that ignores the switch reports no error — it just
    /// prints δ where ë belongs. The shop reads the paper and picks the line that came out right.
    /// </summary>
    public async Task<AgentResult> PrintCodePageSampleAsync()
    {
        var shop = await _profile.GetSettingsAsync();
        const int w = ReceiptFormatter.DefaultWidth;

        var lines = new List<ReceiptTextLine>
        {
            new(Center("PROVË E KODIMIT", w), ReceiptEmphasis.Title),
            new(new string('-', w)),
            new("Gjej rreshtin ku shkronjat duken SAKTE:"),
            new("duhet te lexohet:  Ë ë Ç ç"),
            new("Zgjidh numrin e atij rreshti te Pajisjet."),
            new(new string('-', w)),
        };

        // The label ([1252] …) is plain ASCII on purpose: it must stay readable even on the
        // lines whose code page turns out to be the wrong one for this printer.
        foreach (var page in ReceiptCodePages.All)
        {
            lines.Add(new ReceiptTextLine(
                $"[{page.Value,-5}] Ë ë Ç ç — Kërçovë 1.50 €",
                ReceiptEmphasis.Normal,
                CodePage: page.Value));
        }

        lines.Add(new ReceiptTextLine(new string('-', w)));
        lines.Add(new ReceiptTextLine($"Tani perdoret: {shop?.ReceiptCodePage ?? ReceiptCodePages.Default}"));

        var req = HardwareMapper.ToReceiptRequest("KODIMI", lines, shop);
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<AgentResult>("printReceipt", req);
        }
        catch (Exception ex) { return AgentResult.Fail(ex.Message); }

        static string Center(string s, int width) =>
            s.Length >= width ? s[..width] : new string(' ', (width - s.Length) / 2) + s;
    }

    /// <summary>
    /// One sample label on the shop's real stock, so the label printer can be proved — and the
    /// price size and the label dimensions judged — without burning an article's barcode on it.
    /// </summary>
    public async Task<AgentResult> PrintTestLabelAsync()
    {
        var shop = await _profile.GetSettingsAsync();
        var sample = new Article
        {
            // A valid EAN-13 (check digit included), so the shop can scan the sample and see
            // that the bars it just printed are actually readable.
            Barcode = "5901234123457",
            Name = "PROVË — " + (string.IsNullOrWhiteSpace(shop?.BusinessName) ? "KosovaPOS" : shop!.BusinessName!),
            SalesPrice = 9.99m,
        };
        return await PrintBarcodeAsync(sample, 1);
    }

    /// <summary>The shop header both the browser page and the agent print. One definition, one receipt.</summary>
    public static ReceiptHeader ReceiptHeaderFor(BusinessSettings? shop) => new(
        Name: string.IsNullOrWhiteSpace(shop?.BusinessName) ? "KosovaPOS" : shop!.BusinessName!,
        Address: shop?.Address,
        Phone: shop?.Phone,
        FiscalNumber: shop?.FiscalNumber,
        VatNumber: shop?.VatNumber);

    /// <summary>
    /// Prints barcode labels for an article. The printer and the label stock default to the
    /// shop's settings — a caller that has to know the label size before it can print one is a
    /// caller that will get it wrong, and a label laid out for the wrong stock comes out cropped.
    /// </summary>
    public async Task<AgentResult> PrintBarcodeAsync(
        Article article, int copies = 1, string? printerName = null,
        int? labelWidthMm = null, int? labelHeightMm = null)
    {
        var shop = await _profile.GetSettingsAsync();

        // A zero here is not "no opinion", it is a label 0mm wide — and a settings row written
        // before these columns existed reads back as 0. Fall through to the stock the shop
        // actually has in the printer.
        static int Mm(int? asked, int? saved, int fallback) =>
            asked is > 0 ? asked.Value : saved is > 0 ? saved.Value : fallback;

        var req = HardwareMapper.ToBarcodeRequest(article, copies,
            string.IsNullOrWhiteSpace(printerName) ? shop?.BarcodePrinter : printerName,
            Mm(labelWidthMm, shop?.LabelWidthMm, 55),
            Mm(labelHeightMm, shop?.LabelHeightMm, 25));
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<AgentResult>("printBarcode", req);
        }
        catch (Exception ex) { return AgentResult.Fail(ex.Message); }
    }

    /// <summary>
    /// The printers installed on the cashier PC, so the shop can pick one rather than type its
    /// Windows name. Null when the agent is offline — the caller falls back to a free-text box.
    /// </summary>
    public async Task<PrinterList?> GetPrintersAsync()
    {
        try
        {
            var m = await ModuleAsync();
            var list = await m.InvokeAsync<PrinterList?>("printers");
            // The offline path returns {ok:false,...}, which deserialises to an empty list.
            return list is null || list.Printers.Count == 0 ? null : list;
        }
        catch (Exception) { return null; }
    }

    /// <summary>
    /// Prints a POS page (the A4 invoice, the 80mm receipt) through the browser, from the page
    /// the cashier is already on — a hidden iframe, not a second tab. Documents the agent
    /// cannot emit as raw bytes (the A4 invoice is one) have to be drawn by the browser, but
    /// that is no reason to take the till screen away.
    /// </summary>
    public async Task<AgentResult> PrintUrlAsync(string url)
    {
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<AgentResult>("printUrl", url);
        }
        catch (Exception ex) { return AgentResult.Fail(ex.Message); }
    }

    /// <summary>
    /// The thermal receipt, printed the best way this PC can manage: raw ESC/POS through the
    /// agent (no dialog, no navigation), and if there is no agent, the same document drawn by
    /// the browser in a hidden frame. Either way the cashier stays on the screen they were on.
    /// </summary>
    public async Task<AgentResult> PrintReceiptAnyWayAsync(long receiptNumber)
    {
        var sent = await PrintReceiptAsync(receiptNumber);
        return sent.Ok ? sent : await PrintUrlAsync($"/kupon/{receiptNumber}");
    }

    /// <summary>Reads the current weight from the scale.</summary>
    public async Task<ScaleReadResult> ReadScaleAsync()
    {
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<ScaleReadResult>("readScale");
        }
        catch (Exception ex) { return new ScaleReadResult { Ok = false, Error = ex.Message }; }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch (JSDisconnectedException) { }
        }
    }
}
