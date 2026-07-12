// Minimal service worker: a pass-through, on purpose.
//
// Chrome/Edge only offer "Install app" when a page has a manifest AND a service
// worker with a fetch handler. This provides exactly that and nothing more — it
// caches NOTHING.
//
// Caching would actively break this app: KosovaPOS is Blazor *Server*, so the UI
// is driven over a live SignalR circuit and the served HTML is a shell tied to the
// current build. A cached shell would reconnect against a newer server and fail, or
// show a stale till. Offline selling is not something a cache can fake — the sale
// has to reach Postgres — so we don't pretend to support it.
self.addEventListener('install', event => {
  // Take over immediately so an updated app isn't blocked by the old worker.
  self.skipWaiting();
});

self.addEventListener('activate', event => {
  event.waitUntil(self.clients.claim());
});

self.addEventListener('fetch', event => {
  // Straight to the network. Present only to satisfy installability.
  event.respondWith(fetch(event.request));
});
