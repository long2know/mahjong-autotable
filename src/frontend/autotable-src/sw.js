// Phase K Wave 2 — Service worker.
//
// Caching strategy:
//   • cache-first for static immutable assets (anything matching the
//     parcel content-hash filename pattern: `<name>.<8hex>.<ext>`).
//     Parcel re-builds mint new filenames, so we can hold these
//     indefinitely; the old hashed files coexist until the next
//     install-cycle's `activate` purges them.
//   • cache-first for fonts + tile textures.
//   • network-first with stale-while-revalidate fallback for the
//     `/api/games/public` lobby browse endpoint so users with a dead
//     connection still see the last-known game list (the lobby
//     overlays a `pwa-offline-banner` while this happens).
//   • network-only for everything else under `/api/` and `/hubs/`
//     (live data must always go to the wire).

const CACHE_VERSION = 'autotable-v2';
const STATIC_CACHE = `${CACHE_VERSION}-static`;
const LOBBY_CACHE = `${CACHE_VERSION}-lobby`;

const HASHED_ASSET_RE = /\.[0-9a-f]{8}\.(?:js|css|png|jpg|jpeg|svg|woff2?|otf|ttf|wav|mp4|glb)$/i;

self.addEventListener('install', (event) => {
  // No precache list — the first navigations will hydrate the cache
  // organically.  Skip waiting so a new SW activates promptly on
  // refresh; the page-level handler dispatches `mahjong:sw-update-ready`
  // for callers that want to surface a banner.
  event.waitUntil(self.skipWaiting());
});

self.addEventListener('activate', (event) => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys.map((key) => {
      if (key.startsWith('autotable-') && !key.startsWith(CACHE_VERSION)) {
        return caches.delete(key);
      }
      return Promise.resolve();
    }));
    await self.clients.claim();
  })());
});

self.addEventListener('fetch', (event) => {
  const req = event.request;
  if (req.method !== 'GET') return;
  const url = new URL(req.url);
  if (url.origin !== self.location.origin) return;

  // Skip the service worker itself + manifest (Chrome refetches the
  // manifest on every install — we never want a stale one).
  if (url.pathname.endsWith('/sw.js')) return;
  if (url.pathname.endsWith('/manifest.webmanifest')) return;

  // Lobby browse fallback — network-first, cache-on-success.
  if (url.pathname === '/api/games/public' || url.pathname.startsWith('/api/games/public?')) {
    event.respondWith(networkFirst(req, LOBBY_CACHE));
    return;
  }

  // Hashed parcel artefacts + img directory: cache-first, never expire.
  if (HASHED_ASSET_RE.test(url.pathname) || url.pathname.startsWith('/img/')) {
    event.respondWith(cacheFirst(req, STATIC_CACHE));
    return;
  }

  // Live API + hub traffic — network-only.  Don't even attempt cache,
  // so a stale `/api/auth/me` never lets an unauthenticated client
  // appear authenticated.
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/hubs/')) {
    return;
  }

  // HTML / everything else — network-first with a cached fallback so
  // the bundle still boots offline (returning a cached index.html lets
  // the SPA shell render its own offline banner).
  if (req.mode === 'navigate' || req.destination === 'document') {
    event.respondWith(networkFirst(req, STATIC_CACHE));
    return;
  }
});

async function cacheFirst(req, cacheName) {
  const cache = await caches.open(cacheName);
  const hit = await cache.match(req);
  if (hit !== undefined) {
    // Refresh in the background — best-effort, ignore failures.
    fetch(req).then((resp) => {
      if (resp.ok) cache.put(req, resp.clone());
    }).catch(() => { /* offline */ });
    return hit;
  }
  try {
    const resp = await fetch(req);
    if (resp.ok) await cache.put(req, resp.clone());
    return resp;
  } catch (err) {
    // Last-chance cache lookup with a fuzzier match (ignoring search).
    const fallback = await cache.match(req, { ignoreSearch: true });
    if (fallback !== undefined) return fallback;
    throw err;
  }
}

async function networkFirst(req, cacheName) {
  const cache = await caches.open(cacheName);
  try {
    const resp = await fetch(req);
    if (resp.ok) await cache.put(req, resp.clone());
    return resp;
  } catch (err) {
    const hit = await cache.match(req);
    if (hit !== undefined) return hit;
    // Synthesise a 503 for the lobby endpoint so the frontend can
    // surface "(offline)" rather than throwing.
    return new Response(JSON.stringify({ games: [], offline: true }), {
      status: 503,
      statusText: 'Offline',
      headers: { 'Content-Type': 'application/json' },
    });
  }
}
