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

export async function printReceipt(req) {
    return await call("/receipt/print", { method: "POST", body: req });
}

export async function printBarcode(req) {
    return await call("/barcode/print", { method: "POST", body: req });
}

export async function readScale() {
    return await call("/scale/read", { timeoutMs: 4000 });
}
