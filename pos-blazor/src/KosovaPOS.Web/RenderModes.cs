namespace KosovaPOS.Web;

/// <summary>
/// How the pages of this app render.
///
/// Every screen used to be plain <c>InteractiveServer</c>, which prerenders: the page is built
/// once on the HTTP request and then built AGAIN when the WebSocket circuit connects. That meant
/// every screen ran its <c>OnInitializedAsync</c> — and so its database queries — twice per visit,
/// to produce a first copy that the user cannot click. Between those two builds sat the worst part
/// of the old feel: a page that looked finished, listed real articles, and ignored every tap,
/// because the circuit behind it had not connected yet. A till that ignores taps is a till the
/// cashier taps harder.
///
/// So: no prerender. The page is built once, when it can actually be used. What the user sees in
/// the meantime is not a blank screen — <c>MainLayout</c> is still server-rendered, so the nav,
/// the appbar and a skeleton of the page are already on screen while the circuit connects.
/// A frame of honest skeleton beats a frame of dead page.
/// </summary>
public static class RenderModes
{
    public static readonly Microsoft.AspNetCore.Components.Web.InteractiveServerRenderMode Interactive = new(prerender: false);
}
