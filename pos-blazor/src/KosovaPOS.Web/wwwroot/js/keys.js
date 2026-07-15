// Keyboard shortcuts for the till.
//
// A cashier at a queue works with one hand on a scanner and the other on the keyboard; reaching
// for the mouse to press "PËRFUNDO" is the slowest thing they do all day. So the busy screens
// answer to keys — F12 to finish a sale, Escape to clear a half-typed search — and they must
// answer WHEREVER focus happens to be, which a per-input @onkeydown cannot: the moment focus is
// on a payment button or a ± stepper, an input handler is deaf. This listens on the window.
//
// It also owns the one thing Blazor cannot do from C#: stopping the browser's own default for a
// key. F12 opens the developer tools unless something calls preventDefault synchronously, in the
// same tick as the keydown — a round trip to the server is far too late. So the prevent list is
// handled here, in JS, and only the keys the page actually claims are ever sent across the wire
// (typing into the search box must not chatter one interop call per character).

let handler = null;
let dotnet = null;
let hot = new Set();
let prevent = new Set();

// "Ctrl+Alt+Shift+F12" — modifiers first, in a fixed order, so the C# side matches on a stable
// string. A bare key ("F12", "Escape", "Enter") carries no modifier prefix.
function signature(e) {
    let s = "";
    if (e.ctrlKey) s += "Ctrl+";
    if (e.altKey) s += "Alt+";
    if (e.shiftKey) s += "Shift+";
    return s + e.key;
}

/// Registers the keys a page cares about. `hotKeys` are sent to .NET's [JSInvokable] OnHotKey;
/// `preventKeys` (a subset) also have their browser default suppressed. Re-registering replaces
/// the previous page's set — a single listener, swapped, never stacked.
export function register(dotNetRef, hotKeys, preventKeys) {
    unregister();
    dotnet = dotNetRef;
    hot = new Set(hotKeys || []);
    prevent = new Set(preventKeys || []);

    handler = (e) => {
        const sig = signature(e);
        if (!hot.has(sig)) return;
        if (prevent.has(sig)) e.preventDefault();
        // Fire-and-forget: the circuit is where the sale lives, and a dropped keystroke is not
        // worth awaiting under the cashier's fingers.
        dotnet.invokeMethodAsync("OnHotKey", sig);
    };

    // Capture phase: a shortcut is the page's before it is any widget's, and F12 must be caught
    // before the browser acts on it.
    window.addEventListener("keydown", handler, true);
}

export function unregister() {
    if (handler) window.removeEventListener("keydown", handler, true);
    handler = null;
    dotnet = null;
    hot = new Set();
    prevent = new Set();
}

/// Focuses an element by id — for the boxes that appear in a modal and so have no @ref until the
/// render that shows them.
export function focus(id) {
    const el = document.getElementById(id);
    if (el) { el.focus(); if (el.select) el.select(); }
}
