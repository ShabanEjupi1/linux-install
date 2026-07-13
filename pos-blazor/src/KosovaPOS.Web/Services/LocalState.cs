using Microsoft.JSInterop;

namespace KosovaPOS.Web.Services;

/// <summary>
/// Server-side facade over <c>state.js</c> — the browser's localStorage, typed.
///
/// Anything a till must remember across a navigation (the cart it was in the middle of, the
/// printer this particular PC prints on) belongs to the MACHINE, not to the circuit and not to
/// the business row: the circuit dies on navigation, and two tills in one shop share a login.
///
/// Every call is best-effort. During prerender there is no JS at all, and a browser in private
/// mode can refuse to store — neither is a reason to fail whatever the caller was really doing.
/// </summary>
public sealed class LocalState : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private IJSObjectReference? _module;

    public LocalState(IJSRuntime js) => _js = js;

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", "./js/state.js");

    public async Task<T?> LoadAsync<T>(string key)
    {
        try
        {
            var m = await ModuleAsync();
            return await m.InvokeAsync<T?>("load", key);
        }
        catch (Exception) { return default; }
    }

    public async Task SaveAsync<T>(string key, T value)
    {
        try
        {
            var m = await ModuleAsync();
            await m.InvokeVoidAsync("save", key, value);
        }
        catch (Exception) { }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            var m = await ModuleAsync();
            await m.InvokeVoidAsync("remove", key);
        }
        catch (Exception) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null) return;
        try { await _module.DisposeAsync(); }
        catch (Exception) { /* circuit already gone */ }
        _module = null;
    }
}

/// <summary>The localStorage keys, in one place so a rename cannot orphan a shop's saved cart.</summary>
public static class LocalKeys
{
    /// <summary>The unfinished sale on this till. Cleared the moment the sale is saved.</summary>
    public const string SaleDraft = "pos.sale.draft.v1";

    /// <summary>
    /// How this till is set up — payment method, print toggles, the shelf it is filtered to.
    /// Deliberately NOT part of the sale draft: the draft is thrown away when a sale completes,
    /// and keeping these in it meant every sale reset the till to factory settings.
    /// </summary>
    public const string TillPrefs = "pos.till.prefs.v1";

    // There is deliberately no per-browser receipt-printer key. The receipt printer is the
    // shop's, chosen once on /pajisjet: a dropdown on the till is one more thing a cashier can
    // get wrong mid-queue, and a wrong printer name fails silently — it looks exactly like a
    // printer that is switched off.

    /// <summary>The label printer this PC prints barcodes on, overriding the shop default.</summary>
    public const string LabelPrinter = "pos.printer.label.v1";
}
