// Browser ↔ local hardware Agent bridge.
//
// The Blazor circuit runs on the server, but the *browser* runs on the cashier's
// PC — the same machine as the fiscal/receipt/barcode printers and scale. So the
// browser (not the server) fetches the agent at http://127.0.0.1:9099. Browsers
// treat http://127.0.0.1 as a secure context, so an HTTPS POS page may call it
// without mixed-content errors and without any inbound path to the NAT'd PC.

const AGENT_BASE = "http://127.0.0.1:9099";
const DEFAULT_TIMEOUT_MS = 8000;

async function call(path, { method = "GET", body = null, timeoutMs = DEFAULT_TIMEOUT_MS } = {}) {
    const ctrl = new AbortController();
    const timer = setTimeout(() => ctrl.abort(), timeoutMs);
    try {
        const res = await fetch(AGENT_BASE + path, {
            method,
            headers: body ? { "Content-Type": "application/json" } : undefined,
            body: body ? JSON.stringify(body) : undefined,
            signal: ctrl.signal,
            mode: "cors",
            cache: "no-store",
        });
        if (!res.ok) return { ok: false, error: `Agjenti u përgjigj me ${res.status}` };
        return await res.json();
    } catch (e) {
        // Agent not running / unreachable — the caller decides how to degrade.
        return { ok: false, offline: true, error: "Agjenti i pajisjeve nuk u gjet (nuk është i ndezur)." };
    } finally {
        clearTimeout(timer);
    }
}

export async function health() {
    return await call("/health", { timeoutMs: 2500 });
}

export async function printFiscal(req) {
    // Give the fiscal poll room beyond the server-side timeout.
    const t = (req?.timeoutSeconds ? req.timeoutSeconds * 1000 : 30000) + 5000;
    return await call("/fiscal/print", { method: "POST", body: req, timeoutMs: t });
}

// Clears the fiscal printer's article memory. Give it the same room as a fiscal print — it is
// the same round trip through F-Link and the same serial device.
export async function clearFiscalArticles() {
    return await call("/fiscal/clear-articles", { method: "POST", body: {}, timeoutMs: 35000 });
}

export async function printReceipt(req) {
    return await call("/receipt/print", { method: "POST", body: req });
}

export async function printBarcode(req) {
    return await call("/barcode/print", { method: "POST", body: req });
}

export async function printers() {
    return await call("/printers", { timeoutMs: 4000 });
}

export async function readScale() {
    return await call("/scale/read", { timeoutMs: 4000 });
}

// --- Browser fallback printing ------------------------------------------------
//
// When there is no agent on this PC, the *browser* has to draw the document — and a page can
// only print itself. That used to mean target="_blank": the receipt opened in a second tab,
// which the cashier then had to notice and close, and the till screen (with the sale still on
// it) was no longer the tab in front of them.
//
// A hidden iframe prints the same document from the page that is already open. The tab never
// changes, nothing is left behind to close, and with Chrome's --kiosk-printing the dialog does
// not even appear. Same-origin, so we can reach into it to call print().
export function printUrl(url) {
    return new Promise((resolve) => {
        const old = document.getElementById("pos-print-frame");
        if (old) old.remove();

        const frame = document.createElement("iframe");
        frame.id = "pos-print-frame";
        frame.setAttribute("aria-hidden", "true");
        frame.style.cssText = "position:fixed;right:0;bottom:0;width:0;height:0;border:0;visibility:hidden;";

        // Chrome discards the print job if the iframe goes away while the dialog is still up,
        // so it is removed on a timer rather than when print() returns (print() returns as soon
        // as the dialog opens, not when the user answers it).
        frame.onload = () => {
            try {
                frame.contentWindow.focus();
                frame.contentWindow.print();
                resolve({ ok: true });
            } catch (e) {
                resolve({ ok: false, error: String(e) });
            } finally {
                setTimeout(() => frame.remove(), 60000);
            }
        };

        frame.src = url;
        document.body.appendChild(frame);
    });
}
