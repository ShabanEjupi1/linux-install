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

    /// <summary>The shop header both the browser page and the agent print. One definition, one receipt.</summary>
    public static ReceiptHeader ReceiptHeaderFor(BusinessSettings? shop) => new(
        Name: string.IsNullOrWhiteSpace(shop?.BusinessName) ? "KosovaPOS" : shop!.BusinessName!,
        Address: shop?.Address,
        Phone: shop?.Phone,
        FiscalNumber: shop?.FiscalNumber,
        VatNumber: shop?.VatNumber);

    /// <summary>Prints barcode labels for an article, optionally on a named printer (the HPRT).</summary>
    public async Task<AgentResult> PrintBarcodeAsync(
        Article article, int copies = 1, string? printerName = null,
        int labelWidthMm = 55, int labelHeightMm = 25)
    {
        var req = HardwareMapper.ToBarcodeRequest(article, copies,
            string.IsNullOrWhiteSpace(printerName) ? null : printerName,
            labelWidthMm, labelHeightMm);
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
