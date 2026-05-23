# Hicks — Phase K Wave 2 memo

**Branch:** `stlong/phase-k-wave-2-bringup`
**Date:** 2026-05-31
**Author:** Hicks (Frontend Engineer)
**Scope:** Lobby bundle split (renderer chain + Client + three / SignalR fan-out lazy-loaded), WebRTC voice chat UI, server-authoritative onboarding-tour status, tournament drag-drop seeding (admin), replay finals deep-link, PWA manifest + service worker + offline lobby cache.
**Build gate:** `parcel build` clean (~11 s); `tsc --noEmit -p .` introduces zero new errors beyond the pre-existing TS1323 dynamic-import warnings (same shape as `sentry.ts:97`).

---

## Headline — bundle budget delivered

The Wave 1 memo flagged the eager bundle at **1.318 MB** as the
single biggest user-experience risk; cold-load on a phone over LTE
was roughly 3 s before the lobby paints.  Wave 2 pulls the renderer
chain (`Game`, `World`, `Client`, `MoveLog`, `AssetLoader` + the
top-level three.js imports + the chat module) out of the eager
entry and into a `game-bootstrap.<hash>.js` dynamic chunk that
only loads after the user has crossed the lobby boundary (i.e.,
the URL has a non-empty query string, which is what the lobby +
Quick Match flow stamps before the page navigates).

### Eager-load comparison

| Asset | Wave 1 | Wave 2 | Δ |
|---|---|---|---|
| Eager JS (`autotable-src.<hash>.js`)        | **1.318 MB** | **208.44 kB** | **−84.2 %** |
| Eager CSS (3 chunks, unchanged)             | ~216 kB      | ~216 kB       |   0 % |
| **Eager total** (JS + CSS + manifest+icons) | **~1.55 MB** | **~430 kB**   | **−72 %** |

### Lazy chunks ready when needed

| Chunk | Size | Trigger |
|---|---|---|
| `game-bootstrap.<hash>.js`  | 1.11 MB | First non-empty `?…` on the URL (Quick Match / Apply / `?gameId=`) |
| `esm.<hash>.js` (Sentry)    | 395 kB  | Only when `<meta name="sentry-dsn">` has a DSN |
| `tournaments.<hash>.js`     | 23.8 kB | Tournaments tab hover/focus/click |
| `history.<hash>.js`         | 12.3 kB | Profile-page open |
| `tour.<hash>.js`            | 9.5 kB  | First visit (skipped server-side when completed) |
| `chat.<hash>.js`            | 12.2 kB | After `?gameId=` lands on the URL |
| `audit.<hash>.js`           | 7.4 kB  | Admin probe + replay-tab activation |
| `voice.<hash>.js`           | 5.6 kB  | `?voice=1` on a game URL |

Cold lobby visitor now downloads `~430 kB` before paint — comfortably
under the 500 kB budget Hudson asked for in the Wave-1 review.

### How the split was achieved

1. `utils.ts` previously imported `Vector3` / `Quaternion` at module
   top level for `SEAT_ROTATIONS`, which pulled three.js into every
   module that imported even a single DOM helper.  Wave 2 splits
   `utils.ts` into:
   - `dom-utils.ts` — pure DOM helpers (`setElHidden` / `showEl` /
     `hideEl`).  Zero non-DOM deps.
   - `utils.ts` — unchanged three.js-bound geometry helpers, plus
     a back-compat re-export shim of the DOM helpers.
   The lobby-chain modules (audit / chat / history / identity /
   leaderboard / lobby / profile-page / profile / settings-drawer /
   tournaments / client-ui / game-ui) were migrated to import the
   DOM helpers from `./dom-utils` directly.
2. `index.ts` was rewritten as a lobby-only entry: it imports
   lobby + reconnect + sentry + i18n + identity + pwa.  The
   renderer / Client / three / chat / voice surface lives in a new
   `game-bootstrap.ts` module that's dynamically imported only
   when `window.location.search !== ''` — i.e., the user has Quick-
   Matched or followed a `?gameId=` link.  Quick Match calls
   `window.location.replace()` so the full reload triggers the
   bootstrap on the next navigation.
3. The chat module is reached through `game-bootstrap` (was eager
   in Wave 1 because `index.ts` imported `installChatUI` directly).

`lobby.ts` only imports `Client` as a `type`, so the type-only edge
doesn't pull `client.ts` into the eager graph.  Verified by
`grep "HubConnectionBuilder" autotable-src.<hash>.js` — SignalR is
in the eager bundle (matchmaking + profile broadcasts depend on it)
but `Game`/`World`/`Client`/three are not.

---

## Task-by-task summary

### Task 1 — Voice chat

**New file:** `src/frontend/autotable-src/src/voice.ts` (~330 lines).

WebRTC mesh up to 4 peers; one `RTCPeerConnection` per peer using
the polite-peer offer/answer pattern.  ICE servers come from
`GET /api/turn` with a public STUN fallback (`stun.l.google.com`)
when the endpoint 404s — so the module is safe to merge ahead of
Bishop's TURN-server provisioning.

Public surface:
```ts
mountVoicePanel({ gameId, playerId, displayName }): { unmount(): void }
```

Mounted from `game-bootstrap.ts` when `?voice=1` lands on the URL.
The voice panel is a fixed-position `<aside>` (bottom-right of the
game viewport) with:
- `voice-mic-toggle` — primary mic button (`aria-pressed`,
  "🎙️ Mute" / "🔴 Live").  Disabled + `voice-mic-denied` class on
  getUserMedia rejection.
- `voice-peer-{connectionId}` — status pill ("Connecting" /
  "Connected" / "Failed"); class `voice-peer-status-{state}`.
- `voice-volume-{connectionId}` — `<input type="range">` (0–1,
  step 0.05) bound to the peer's `<audio>.volume`.

**Wire contract for Bishop:**
- `GET /api/turn` →
  ```json
  { "iceServers": [{ "urls": ["turn:…"], "username": "...", "credential": "..." }] }
  ```
- `VoiceHub` (SignalR, `/hubs/voice`):
  - Server → client: `PeerJoined(connectionId, displayName)`,
    `PeerLeft(connectionId)`, `Offer(fromConnId, sdp)`,
    `Answer(fromConnId, sdp)`,
    `IceCandidate(fromConnId, candidate)`.
  - Client → server: `SendOffer(toConnId, sdp)`,
    `SendAnswer(toConnId, sdp)`,
    `SendIceCandidate(toConnId, candidate)`.
- Until Bishop publishes a `voiceEnabled` flag on the game state
  the UI is gated by `?voice=1` so we don't show the panel on
  non-opted-in tables.  Suggest Wave 3 swap the URL gate for the
  authoritative flag.

### Task 2 — Server-authoritative onboarding tour

**Updated file:** `src/frontend/autotable-src/src/tour.ts`.

Wave 1 read the LS flag only — Vasquez correctly noted that a
returning user on a second device or in incognito would re-see the
tour.  Wave 2 probes the server first and falls back to LS.

Flow:
1. `installOnboardingTour()` fires `probeServerOnboardingStatus()`
   which `GET /api/players/me/onboarding-status`.
   - 200 `{ completed: true, completedAtUtc?: string }` → mirror to
     LS so future cold-loads skip the tour even offline; bail out.
   - 200 `{ completed: false }` → continue to the LS check; if LS
     also says incomplete, show the tour.
   - 404 / network error → silently fall through to the Wave-1
     LS-only path.
2. On `endTour(true)` we POST the same endpoint with
   `{ completed: true, completedAtUtc: "<iso>" }`.  POST failure is
   silently ignored (LS is the authoritative offline fallback) so
   the module is safe to merge ahead of Bishop's backend.

**Wire contract for Bishop:**
- `GET /api/players/me/onboarding-status` →
  `200 { completed: boolean, completedAtUtc?: string }` or `404`.
- `POST /api/players/me/onboarding-status` body
  `{ completed: true, completedAtUtc: "<iso>" }` →
  `204 No Content` or `404`.

### Task 3 — Tournament drag-drop seeding (admin)

**Updated file:** `src/frontend/autotable-src/src/tournaments.ts`.

Admin probe (reuses the pattern from `audit.ts:60-109`) hits
`GET /api/auth/me` and looks for `role: 'admin'` or
`roles: ['admin', …]`.  When the probe succeeds and the tournament
is in `open` / `registration-open` status with a single-elim
format, a `tournament-seeding-panel` surface is mounted above the
bracket SVG.

The panel is an `<ol>` of `tournament-seed-row-{N}` `<li>` items,
each a draggable HTML5 drag-drop target.  `dragstart` / `dragover` /
`drop` / `dragend` reorder the list in place (with `aria-grabbed`
mirroring the live drag); a Save button posts the canonical order
to `POST /api/tournaments/{id}/seed` with body
`{ seeds: [playerId, …] }`.

On success the tournament detail is re-opened so the bracket
reflects the server's canonical layout.  On failure the
`tournament-seeding-status` pill surfaces beneath the Save button
for 4 s and the panel stays open.

**Wire contract for Bishop:**
- `GET /api/auth/me` → `{ role: 'admin' | … , roles?: string[] }`.
  Already exists; reused unchanged.
- `POST /api/tournaments/{id}/seed` body
  `{ seeds: [playerId, …] }` → `204 No Content`.  Rejecting a
  seeding op (tournament already started, non-admin, etc.) should
  return `409` or `403` with `{ message: "..." }` — Hicks surfaces
  `message` in the status pill.

### Task 4 — Replay finals deep-link

**Updated files:** `src/frontend/autotable-src/src/replay-launcher.ts`,
`src/frontend/autotable-src/src/replay.ts`.

`openReplayForGame(gameId, options?: { finals?: boolean })`.  When
`finals: true`:
1. Stamps `?finals=true` on the URL via `history.replaceState` so
   sharing the link reopens the replay at the finals view.
2. `replay.ts:openServer` sees `wantFinals` and sets
   `selectedHandIdx = hands.length - 1`, then scrolls the final
   move into view on first paint.

Cold-link visitors (no in-app navigation) are also covered: a new
`readFinalsFlagFromUrl()` helper checks the URL on every
`openServer()` call so a shared `?finals=true` link works without
the launcher option.

All tournament replay entry points (SVG bracket finals pin,
detail-strip Watch-replay button, round-robin / Swiss row ▶
buttons) now pass `{ finals: true }`.

### Task 5 — PWA manifest + service worker + offline lobby

**New files:** `src/frontend/autotable-src/manifest.webmanifest`,
`src/frontend/autotable-src/sw.js`,
`src/frontend/autotable-src/src/pwa.ts`.
**Updated file:** `src/frontend/autotable-src/index.html`.

Manifest:
- `display: standalone`, `theme_color: #1e2a36`, three icons
  pointing at `img/icon-{16,32,96}.auto.png`.
- Linked from `index.html` alongside `theme-color` +
  `apple-mobile-web-app-*` metas + an apple-touch-icon shim.

Service worker (`CACHE_VERSION = 'autotable-v2'`):
- Cache-first for parcel content-hash assets (`.<8hex>.{js,css,…}`)
  and `/img/*`.  Old hashed files survive until the next install
  cycle's `activate` step purges them.
- Network-first with cache fallback for `/api/games/public` so a
  returning user with a dead connection still sees the last-known
  public lobby (and the offline banner appears).
- Network-only for everything else under `/api/*` + `/hubs/*` so
  auth + matchmaking + voice never serve stale data.
- Network-first with a cached `index.html` fallback so the SPA
  shell boots offline.

`pwa.ts`:
- `registerServiceWorker()` exported, called from the rewritten
  `index.ts`.
- Mounts a `pwa-offline-banner` `<div role="status">` that toggles
  with `navigator.onLine` + the `online`/`offline` events.  Also
  re-broadcasts as `mahjong:offline` / `mahjong:online`
  CustomEvents so history.ts + matchmaking.ts can adapt later.
- Captures `beforeinstallprompt` into a module-level variable and
  surfaces a `pwa-install-prompt` button on Chrome/Edge.  Clicking
  it invokes the deferred native prompt; the button removes itself
  after the choice.

Parcel doesn't process `sw.js` because nothing in the dependency
graph references it.  Workflow change: `cp sw.js manifest.webmanifest`
to the dist root after each `parcel build`.  Coordinate with
Hudson on the CI release script so the copy step is automated.

---

## Test gates

Vasquez — Wave 2 Playwright soft-passes (annotations only, no
hard failures yet):

- `voice-mic-toggle hidden until ?voice=1 is on the URL`
- `voice-peer-* requires VoiceHub (Bishop's /hubs/voice)`
- `tournament-seeding-panel hidden when admin probe returns false`
- `tournament-seed-row-* drag-drop reorders + Save POSTs /seed`
- `pwa-install-prompt only fires on Chrome/Edge after beforeinstallprompt`
- `pwa-offline-banner toggles with navigator.onLine`
- `tour completes once when /api/players/me/onboarding-status is 200 { completed:true }`
- `replay deep-link ?finals=true auto-scrolls to last hand`

See `tests/selectors.md` § "Phase K Wave 2 testids" for the
full surface inventory.

---

## Wave-3 follow-ups

Tickets I'd like to file (Hudson to triage):

1. **Pre-cache critical assets** — `sw.js install` currently only
   cache-first's hashed assets after the browser has already
   fetched them once.  We could pre-cache the lobby bundle +
   manifest icons + CSS during the SW install for an even faster
   second-visit boot.  Needs a parcel post-build script to emit a
   `manifest.json` of hashed-asset URLs the SW can read.
2. **Drop `?voice=1` for a server flag** — once Bishop publishes a
   `voiceEnabled` boolean on the game-state broadcast, swap the
   URL-flag voice gate for the authoritative flag and add an
   in-table opt-in UI.
3. **TURN provisioning** — without a TURN server, voice will only
   work on benign NATs.  Bishop / Hudson need to provision a
   `coturn` instance and wire `/api/turn` to return real ICE
   creds (the JS expects ICE creds with at most a 1-hour TTL).
4. **Lazy-load three.js inside `game-bootstrap`** — `game-bootstrap.<hash>.js`
   is still 1.11 MB; three.js is the biggest single contributor.
   Worth investigating whether the renderer can be split into a
   "shell" (DOM + Client + matchmaking handshake) and a "scene"
   (three.js + GLB loaders) so the first frame ships sooner.
5. **Offline-friendly tour** — `tour.ts` falls back to LS when the
   onboarding probe 404s, but the tour HTML strings are inlined
   into the lazy `tour.<hash>.js` chunk.  When the SW cache miss
   happens (incognito offline), the tour won't render.  Suggest a
   cached-fallback path on the SW.
6. **Replay timeline ARIA polish** — the auto-scroll-to-finals
   uses `scrollIntoView({ behavior: 'smooth' })`.  Screen readers
   announce nothing; suggest a `aria-live="polite"` "Showing
   finals" status pill on first finals render.

---

## Notes for the agent who picks up Wave 3

- **Author hygiene:** the branch was committed under
  `Hicks (Frontend) <hicks@squad.mahjong>` with a `Co-authored-by:
  Copilot` trailer; please keep the convention.
- **Never `git add -A`** in this repo — there are agent-private
  workflows / actionlint binaries / skill caches under
  `.copilot/`, `.github/workflows/squad-*.yml`, `.tool-actionlint/`,
  `.work/` that should NOT land in commits.  Use the explicit
  staging list documented at the bottom of `history.md`.
- **Build commands:**
  ```sh
  cd src/frontend/autotable-src
  npx tsc --noEmit --strict --target es6 --moduleResolution bundler \
    --esModuleInterop --lib DOM,DOM.Iterable,es6,es2017 src/index.ts
  npx parcel build index.html --dist-dir ../autotable \
    --public-url . --no-source-maps --no-cache
  cp sw.js manifest.webmanifest ../autotable/  # parcel doesn't see them
  ```
- **Bundle-budget sanity check after any future eager import:**
  ```sh
  ls -lS ../autotable/autotable-src.*.js | head -1
  # Wave-2 budget: eager bundle must stay below 500 kB.
  ```
