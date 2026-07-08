using KosovaPOS.Agent.Contracts;
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
    private IJSObjectReference? _module;

    public HardwareBridge(IJSRuntime js) => _js = js;

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

    /// <summary>Prints a non-fiscal courtesy receipt.</summary>
    public async Task<AgentResult> PrintReceiptAsync(Receipt receipt, BusinessSettings? shop = null)
    {
        var req = HardwareMapper.ToReceiptRequest(receipt, shop);
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<AgentResult>("printReceipt", req);
        }
        catch (Exception ex) { return AgentResult.Fail(ex.Message); }
    }

    /// <summary>Prints barcode labels for an article.</summary>
    public async Task<AgentResult> PrintBarcodeAsync(Article article, int copies = 1)
    {
        var req = HardwareMapper.ToBarcodeRequest(article, copies);
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<AgentResult>("printBarcode", req);
        }
        catch (Exception ex) { return AgentResult.Fail(ex.Message); }
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
