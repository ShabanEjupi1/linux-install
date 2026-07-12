// Per-machine UI state that has to outlive the page.
//
// A Blazor Server component's fields live in the circuit, and the circuit's component
// instance is destroyed the moment the cashier navigates away — so a cart that was half
// scanned, or a printer that was chosen from a dropdown, is gone by the time they come
// back. The server also cannot hold it: it does not know WHICH till it is talking to, and
// two tills in the same shop share one login. The browser does. So it lives here.
//
// localStorage is per-origin, and every business is its own subdomain, so one shop's
// unfinished sale can never surface on another's till.

export function load(key) {
    try {
        const raw = localStorage.getItem(key);
        return raw ? JSON.parse(raw) : null;
    } catch {
        // Private-mode, a full disk, or a value written by an older build that no longer
        // parses. None of them are worth taking the till down for.
        return null;
    }
}

export function save(key, value) {
    try {
        localStorage.setItem(key, JSON.stringify(value));
    } catch {
        // Ignore: failing to REMEMBER a sale must never fail the sale.
    }
}

export function remove(key) {
    try {
        localStorage.removeItem(key);
    } catch { }
}
