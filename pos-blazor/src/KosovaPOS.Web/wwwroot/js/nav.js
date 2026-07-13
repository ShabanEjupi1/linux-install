// Navigation feel.
//
// Blazor's enhanced navigation fetches the next page over the wire and swaps the DOM when it
// arrives. Between the click and the swap — a server round trip, plus whatever the page's
// OnInitializedAsync asks the database for — NOTHING happens on screen: the old page just sits
// there, and then is replaced all at once. That gap is what makes a fast app feel cheap. The user
// clicks again, because nothing told them the first click landed.
//
// Three things fix it, and none of them make the page arrive any sooner:
//   1. the click is acknowledged instantly (the nav item lights up, before the server answers),
//   2. a progress bar shows the wait is a wait and not a hang,
//   3. the new page fades in rather than being slammed into place.

const BAR_ID = "route-progress";
const SHOW_AFTER_MS = 130;   // below this a bar would flash and read as a glitch, not as progress
const REDUCED = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

let showTimer = null;
let bar = null;
let navigating = false;

function ensureBar() {
    if (bar && bar.isConnected) return bar;
    bar = document.createElement("div");
    bar.id = BAR_ID;
    bar.innerHTML = '<div class="route-progress-fill"></div>';
    document.body.appendChild(bar);
    return bar;
}

function startProgress() {
    if (navigating) return;
    navigating = true;

    clearTimeout(showTimer);
    showTimer = setTimeout(() => {
        const el = ensureBar();
        el.classList.remove("done");
        // Reflow between the reset and the run, or the browser coalesces them and the bar
        // jumps straight to its resting width with no motion at all.
        void el.offsetWidth;
        el.classList.add("running");
    }, SHOW_AFTER_MS);
}

function endProgress() {
    clearTimeout(showTimer);
    if (!navigating) return;
    navigating = false;

    if (!bar || !bar.isConnected) return;
    if (!bar.classList.contains("running")) return;   // it never became visible; nothing to hide

    bar.classList.add("done");
    setTimeout(() => {
        if (!bar) return;
        bar.classList.remove("running", "done");
    }, 260);
}

// The clicked link goes active immediately. NavLink only updates its own class once the new page
// has been rendered — which is precisely the interval we are trying to fill.
function markPending(anchor) {
    document.querySelectorAll(".nav-link.pending").forEach(el => el.classList.remove("pending"));
    if (anchor && anchor.classList.contains("nav-link")) anchor.classList.add("pending");
}

// Only navigations the SPA will handle. A download, a new tab, or an off-site link leaves this
// page alive — and a progress bar that never finishes is worse than none.
function isEnhanced(anchor, event) {
    if (!anchor || event.defaultPrevented) return false;
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return false;
    if (anchor.target && anchor.target !== "_self") return false;
    if (anchor.hasAttribute("download")) return false;

    const href = anchor.getAttribute("href");
    if (!href || href.startsWith("#")) return false;

    const url = new URL(anchor.href, document.baseURI);
    if (url.origin !== location.origin) return false;
    if (url.pathname === location.pathname && url.search === location.search) return false;

    // /shkarko/… is a file download: the page stays where it is.
    if (url.pathname.startsWith("/shkarko/")) return false;

    return true;
}

document.addEventListener("click", e => {
    const anchor = e.target instanceof Element ? e.target.closest("a[href]") : null;
    if (!isEnhanced(anchor, e)) return;
    markPending(anchor);
    startProgress();
}, { capture: true });

// Back/forward is a navigation too, and it has no anchor to light up.
window.addEventListener("popstate", () => startProgress());

// Enhanced navigation finished and the new DOM is in place. Fade the content in from here rather
// than from a CSS rule on load: enhanced nav DIFFS the DOM, so a page whose shell survives the
// swap would never re-run a mount animation.
function pageArrived() {
    endProgress();
    document.querySelectorAll(".nav-link.pending").forEach(el => el.classList.remove("pending"));

    if (REDUCED) return;

    const content = document.querySelector(".shell-content");
    if (!content) return;

    content.classList.remove("page-enter");
    void content.offsetWidth;
    content.classList.add("page-enter");
}

if (window.Blazor) {
    Blazor.addEventListener("enhancedload", pageArrived);
} else {
    // blazor.web.js has not run yet — it is loaded after this file.
    document.addEventListener("DOMContentLoaded", () => {
        if (window.Blazor) Blazor.addEventListener("enhancedload", pageArrived);
    });
}

// A full page load (F5, or the first hit) never raises enhancedload.
window.addEventListener("load", () => endProgress());
