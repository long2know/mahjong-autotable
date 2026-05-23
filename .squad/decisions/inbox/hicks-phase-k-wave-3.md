# Hicks — Phase K Wave 3 memo

**Branch:** `stlong/phase-k-wave-3-bringup`
**Date:** 2026-06-07
**Author:** Hicks (Frontend Engineer)
**Scope:** Three.js / renderer chain split out of `game-bootstrap` into a dedicated `scene` chunk, service-worker pre-cache manifest, offline-friendly onboarding tour, per-game `voiceEnabled` flag wiring (mic disable + owner toggle + hub-error toast), Microsoft OAuth provider button, tournament seed POST refactored to auto-save on each drop with optimistic rollback.
**Build gate:** `parcel build` clean (~10 s); `tsc --noEmit --module esnext` introduces zero new errors beyond the Wave-2 baseline.

---

## Headline — game-bootstrap drops from 1.11 MB to 166 kB

Wave 2 hit the eager-bundle budget by deferring the renderer chain
behind `game-bootstrap.<hash>.js`, but it left three.js + the
renderer chain itself eagerly imported inside that chunk.  Result:
the post-lobby download was still 1.11 MB before tiles painted.

Wave 3 splits `game-bootstrap.ts` into two layers:

1. **`game-bootstrap.ts` (HUD shell)** — three.js-free entrypoint
   that wires the lobby-to-game DOM scaffolding, chat surface, and
   voice mic.  Marks `<body data-testid="game-shell-ready">` as soon
   as the shell mounts.
2. **`scene.ts` (NEW renderer chunk)** — owns three.js, AssetLoader,
   Game, MoveLog, lobby client attach.  Dynamic-imported by
   `game-bootstrap.ts` immediately after the shell paints; marks
   `<body data-testid="game-scene-ready">` after the first rAF.

### Post-Wave-3 chunk budget

| Asset | Wave 2 | Wave 3 | Δ |
|---|---|---|---|
| Eager JS (`autotable-src.<hash>.js`)     | 208.4 kB | **214.1 kB** | +5.7 kB (auth modal + toast helper) |
| Game shell (`game-bootstrap.<hash>.js`)  | **1.11 MB** | **166.0 kB** | **−85.0 %** |
| Renderer (`scene.<hash>.js`) (NEW)       | —        | 922 kB       | (three.js + Game + AssetLoader, was inside game-bootstrap) |
| Toast helper (`toast.<hash>.js`) (NEW)   | —        | 1.2 kB       | Shared toast region client for off-Client surfaces |
| **Total bytes on game URL** (shell + scene) | 1.11 MB | 1.09 MB | −2 % (paint sooner; HUD usable in 166 kB before scene streams) |

The total transfer is roughly the same, but the user-perceived
latency drops dramatically: the lobby paints with 214 kB, then on a
game URL the 166 kB shell mounts (HUD chrome usable), then the 922
kB scene chunk streams in concurrently with the tile-texture
network round-trips.

Old `game-bootstrap.<oldhash>.js` is auto-pruned by the new
post-build script — see "SW pre-cache" below.

---

## Per-game `voiceEnabled` flag

Wave 2 gated voice on the `?voice=1` URL flag and a magic toggle in
the settings drawer.  Bishop's backend now exposes a per-game flag
via `GET /api/games/{id}/settings` returning `{ voiceEnabled,
viewerIsOwner, ... }`, so Wave 3 wires the UI to that source of
truth.

### Wire contract

```text
GET  /api/games/{gameId}/settings
     → 200 { voiceEnabled: bool, viewerIsOwner: bool, ... }
POST /api/games/{gameId}/settings/voice
     body { enabled: bool }
     → 204 on success; 4xx on auth/owner mismatch
```

### Frontend surface

- **`voice.ts`** — on mount, probes `/settings`; if
  `voiceEnabled === false` the mic button renders disabled + carries
  a tooltip "Voice not enabled for this table".  If the player
  attempts `JoinVoice` and the hub responds with
  `"voice not enabled"` / `"spectators cannot join voice"`, the
  rejection routes through `toast.ts#showVoiceToast()` for a
  human-readable surface rather than a console error.  A
  `mahjong:voice-enabled` CustomEvent listener flips the mic live
  when the owner toggles it without a page reload.

- **`settings-drawer.ts`** — adds `voice-enable-toggle` to the
  Network panel, only rendered when `viewerIsOwner === true`.
  Optimistic flip → POST → rollback + toast on failure.  Success
  dispatches `mahjong:voice-enabled` so the in-flight voice module
  flips state without a reload.

- **`toast.ts` (NEW)** — extracted the toast region client from
  `ClientUi` so off-Client surfaces (voice, tournaments) can surface
  toasts without holding a `Client` reference.  Looks up
  `#toast-region` lazily and falls back to `console.warn` if the
  region is missing.

### Open question — VoiceHub method ack contract

Bishop's `JoinVoice` hub method currently returns a `Task` (no
return body); the frontend infers rejection from the thrown
`HubException` message.  If Bishop migrates to a typed result
(`{ ok: bool, reason: string }`), we'll update the `showVoiceToast`
reason map in `toast.ts` accordingly.  Flagging for Wave-4
coordination.

---

## Microsoft OAuth provider

Added a third OAuth provider button in the sign-in modal alongside
Google + GitHub.

### Design

- **Brand icon:** inline 4-tile SVG using Microsoft's brand colours
  (`#f25022 / #7fba00 / #00a4ef / #ffb900`) — no CDN dependency,
  no external image fetch, fully theme-able via CSS variables.
- **Flow:** unlike Google's POST-then-redirect handshake, Microsoft
  uses a direct `window.location.href = '/api/auth/login?provider=microsoft&returnUrl=…'`
  because Bishop's Entra integration round-trips state via a cookie
  set on the GET redirect (rather than via a JSON body).
- **Provider badge:** the `auth-header-chip` carries `🟦 Microsoft`
  next to the user's display name for users who signed in via
  Microsoft.

### Modal scaffold note

Wave 2 referenced `signin-modal` testids in the e2e suite but the
markup was never actually mounted in `index.html` (the e2e
soft-passed when the modal count was 0).  Wave 3's
`ensureAuthMarkup()` injects the full sign-in modal + lobby header
chip + magic-link landing during `auth.ts` module init — so the
existing soft-pass tests should now hard-assert.

---

## Tournament seeding — auto-POST + optimistic rollback

Wave 2 required the admin to drag-reorder seeds and then click
"Save" to commit.  Wave 3 auto-POSTs on every successful drop so
the canonical ordering matches the UI without explicit save.

### Wire contract change

Wave 2 sent `{ seeds: string[] }` (array of `playerId`).  Wave 3
sends the richer per-Bishop-Wave-3-spec payload:

```json
POST /api/tournaments/{id}/seed
{
  "seeds": [
    { "playerId": "...", "seedNumber": 1 },
    { "playerId": "...", "seedNumber": 2 },
    ...
  ]
}
```

`seedNumber` is 1-based.  The new shape lets Bishop attribute each
seed without inferring from array position (and lays the groundwork
for sparse / partial seeding in a future wave).

### Rollback behaviour

`persistSeeds()` captures `lastSavedSeeds` before each POST; on
non-2xx, the working seed array is reverted, the list re-renders,
and `toast.ts#showToast()` surfaces "Seed order could not be saved
— restored previous order."  The manual "Save" button is retained
as a keyboard-only fallback for users without HTML5 drag-drop.

---

## Offline-friendly onboarding tour

Wave 2's tour blocked on `GET /api/players/me/onboarding-status`
before deciding whether to show the tour.  An offline first-time
user therefore stared at a blank lobby until the request 503'd.

Wave 3 makes the probe non-blocking:

- `installOnboardingTour()` races the probe against a 300 ms timer.
  If the timer wins, the tour starts immediately with the LS flag
  as the authoritative source.
- The probe still runs to completion in the background; if it
  succeeds after the timer fires it logs to console for telemetry
  but doesn't preempt the tour.
- `persistServerCompletion()` is now fire-and-forget (synchronous
  return).  POST failure flips `offlineFallback = true` so future
  re-mounts don't retry.
- `resetTour()` clears `offlineFallback` so a manual replay works.

---

## SW pre-cache manifest

`scripts/generate-sw-manifest.js` (NEW, chained from
`npm run build:post` after parcel build):

1. **Copies `sw.js` into the dist.**  Parcel doesn't bundle `sw.js`
   (it's referenced via a string literal in `pwa.ts`), so without
   the copy the deploy carries the stale Wave-2 service worker.
2. **Prunes stale hashed chunks.**  Parcel's `--no-cache` clears
   its own cache but doesn't delete superseded outputs from
   `--dist-dir`, so old `game-bootstrap.<oldhash>.js` would
   accumulate across waves.  The script walks `index.html` →
   JS-of-JS to find the live chunk set and deletes every other
   hashed sibling.  Wave-3 build pruned 6 stale Wave-2 chunks.
3. **Emits `manifest-precache.json`** with the eager lobby chain
   (autotable-src + shell + icons + index.html) so the SW
   `install` handler can pre-warm the static cache on first visit.

### `sw.js` install behaviour

```js
self.addEventListener('install', (event) => {
  event.waitUntil((async () => {
    const resp = await fetch('manifest-precache.json', { cache: 'no-store' });
    const manifest = await resp.json();
    const cache = await caches.open(STATIC_CACHE);
    // HEAD-probe filter so one missing entry doesn't fail the whole install.
    const reachable = await filterReachable(manifest.assets);
    await cache.addAll(reachable);
    await self.skipWaiting();
  })());
});
```

Cache version bumped to `autotable-v3`; `activate` purges any
cache prefixed `autotable-` not matching the new version, so the
upgrade evicts Wave-2 precache entries on first navigation.

### What's deliberately NOT pre-cached

- Scene chunk (922 kB) — would balloon the install payload to
  ~1.4 MB; user-perceived latency is better with lazy stream.
- Large media (mp4 / glb / tile textures) — already cache-first
  at runtime, no reason to push the install cost.

---

## Bundle size summary (post-Wave-3)

```
autotable-src.<hash>.js       214 K  (eager,  target <500 K)  ✓
game-bootstrap.<hash>.js      166 K  (shell,  target <300 K)  ✓
scene.<hash>.js               922 K  (lazy renderer chunk)    ✓
toast.<hash>.js               1.2 K  (lazy helper)            ✓
chat.<hash>.js                 16 K
voice.<hash>.js               6.7 K
tour.<hash>.js                9.4 K
tournaments.<hash>.js          25 K
history.<hash>.js              13 K
audit.<hash>.js               7.2 K
esm.<hash>.js (Sentry vendor) 386 K  (only when sentry-dsn meta is set)
manifest-precache.json        449 B  (NEW, lists 11 install-cycle assets)
sw.js                         6.2 K  (re-copied from autotable-src/ on every build)
```

---

## Wave-4 handoff

1. **VoiceHub typed result.** If Bishop migrates `JoinVoice` /
   `LeaveVoice` to return `{ ok, reason }` instead of throwing
   `HubException`, update `toast.ts#showVoiceToast` reason map.
2. **Owner detection.** Currently relies on `viewerIsOwner` from
   `/settings` — works but is one extra round-trip.  Consider
   stamping ownership in the SignalR `GameJoined` payload.
3. **Tournament seed sparse-mode.** Wave-3 wire shape allows
   partial seedings (`seeds: [{playerId, seedNumber: 1}, ..., {playerId, seedNumber: 5}]`
   with gaps) — Bishop's spec confirms but the UI doesn't yet
   surface a way to leave a gap.  Wave-4 admin UI work.
4. **Microsoft branding.** Verify the inline 4-tile SVG passes
   Microsoft's brand-asset usage guidelines.  If pushback,
   trivial swap to the official Microsoft-hosted SVG (CDN).
5. **Pre-cache scope expansion.** Once the scene chunk is below
   500 kB (Wave 5 three.js tree-shake?), consider adding it to the
   pre-cache manifest so returning-user game-URL load is fully
   warm.
