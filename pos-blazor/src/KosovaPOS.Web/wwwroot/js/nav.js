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
    // Up on the click, not on the server's answer: the wait starts here, and so does the thing
    // that shows it. The destination is on the anchor, which is why the shape can be right.
    showSkeleton(new URL(anchor.href, document.baseURI).pathname);
}, { capture: true });

// Back/forward is a navigation too, and it has no anchor to light up.
window.addEventListener("popstate", () => {
    startProgress();
    showSkeleton(location.pathname);
});

function fadeIn(content) {
    if (REDUCED || !content) return;
    content.classList.remove("page-enter");
    void content.offsetWidth;
    content.classList.add("page-enter");
}

// ── The shell skeleton ───────────────────────────────────────────────────────────────────────
// Pages render with prerender:false (see RenderModes.cs), so the page slot is EMPTY until the
// circuit has connected AND the page's first render has come back over it. MainLayout draws a
// skeleton there; this shows and hides it.
//
// Hides — never removes. Blazor's enhanced navigation will not insert a new element into a slot
// an interactive component owns, so a skeleton taken out of the DOM after the first page would
// never come back for the second (measured: the page area just went blank instead). Left in place
// as an overlay, it is ours to toggle and Blazor has nothing to reinsert.
//
// The trigger is the page RENDERING, not the circuit connecting: the circuit comes up one round
// trip before the page does, and hiding the skeleton then would show an empty box.

const STUCK_AFTER_MS = 15000;   // a skeleton still shimmering this long after the click is a hang

let observer = null;
let stuckTimer = null;

const skeletonEl = () => document.querySelector(".shell-skeleton");
const contentEl = () => document.querySelector(".shell-content");

function isSkeleton(node) {
    return node instanceof Element && node.classList.contains("shell-skeleton");
}

function hasPage(content) {
    return Array.from(content.children).some(el => !isSkeleton(el));
}

// The till looks nothing like the list screens, and the shape has to be right AT THE CLICK — the
// server has not been asked where we are going yet, and by the time it answers, the wait we are
// filling is mostly over.
function shapeFor(pathname) {
    return /^\/sale(\/|$)/i.test(pathname) ? "as-till" : "as-page";
}

function showSkeleton(pathname) {
    const skeleton = skeletonEl();
    const content = contentEl();
    if (!skeleton || !content) return;

    skeleton.classList.remove("as-till", "as-page", "is-stuck");
    skeleton.classList.add(shapeFor(pathname));
    skeleton.classList.remove("is-idle");

    // The overlay is positioned against the top of the scrolling area, so a page left scrolled
    // down would show it above the fold and the old page below it. Navigation lands at the top
    // anyway.
    content.scrollTop = 0;

    watchForPage();
}

function hideSkeleton() {
    clearTimeout(stuckTimer);
    if (observer) { observer.disconnect(); observer = null; }

    const skeleton = skeletonEl();
    if (!skeleton || skeleton.classList.contains("is-idle")) return;

    skeleton.classList.add("is-idle");
    fadeIn(contentEl());
}

function watchForPage() {
    if (observer) { observer.disconnect(); observer = null; }
    clearTimeout(stuckTimer);

    const content = contentEl();
    if (!content) return;

    // A statically-rendered page (403, and anything else with no render mode) arrives with the
    // HTML and no mutation is ever coming. Hide now, before the browser paints a frame of it.
    if (hasPage(content)) {
        hideSkeleton();
        return;
    }

    observer = new MutationObserver(records => {
        const arrived = records.some(r => Array.from(r.addedNodes).some(n => n instanceof Element && !isSkeleton(n)));
        if (arrived) hideSkeleton();
    });
    observer.observe(content, { childList: true });

    stuckTimer = setTimeout(() => skeletonEl()?.classList.add("is-stuck"), STUCK_AFTER_MS);
}

document.addEventListener("click", e => {
    if (e.target instanceof Element && e.target.closest(".sk-retry")) location.reload();
});

// Enhanced navigation finished: the layout is in place, the page itself is still a round trip
// away. Blazor rewrote the skeleton's class from the server's copy, so re-assert the shape and
// keep it up until the page actually renders.
function pageArrived() {
    endProgress();
    document.querySelectorAll(".nav-link.pending").forEach(el => el.classList.remove("pending"));
    showSkeleton(location.pathname);
}

watchForPage();   // the first page load raises no enhancedload; its skeleton is already on screen

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
