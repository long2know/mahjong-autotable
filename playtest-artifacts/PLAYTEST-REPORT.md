# Changsha Mahjong Playtest Report — 2026-05-24

**Tester:** Vasquez (QA), Phase K Wave 23 emergency pivot
**Mission:** Concrete, screenshot-backed evidence of what works / what
doesn't when a real user tries to play Changsha mahjong end-to-end.

## Setup

| Item | Value |
|------|-------|
| Backend port | `8088` (port 8080 occupied by an unrelated python3 process) |
| Frontend serving path | Backend at `:8088` serves `/autotable/` (`UseStaticFiles` w/ PhysicalFileProvider, `Program.cs:1272-1291`) |
| Vite preview | `:4173` was running but **not used** — single-port backend serve is the production-shape deploy |
| Branch | `stlong/phase-k-wave-23-bringup` @ `e2b72da` |
| Backend startup | ✅ **SUCCESS** — `Now listening on: http://0.0.0.0:8088` (15 s cold-start, EF Core SQLite-bootstrap completes, ChangshaRulePresets seeded, Hydrated 0 games from persistence) |
| Backend `/health` | `200` |
| Backend `/autotable/` | `200` (serves built bundle) |
| Backend connection string | `Data Source=/tmp/playtest-mahjong.db` (Development) |
| Playwright | `@playwright/test 1.60.0`, chromium project, single test, **PASSED in 15.7 s** |
| Screenshots captured | **11** (`01`…`07b`, `10`) |

## Step-by-step findings

| # | Action | Result (observed) | Screenshot | Verdict |
|---|--------|-------------------|------------|---------|
| 1 | Load `/autotable/` | Page loads, `Loading...` `#loading` element still visible, lobby pickers behind it, tour overlay open, onboarding modal open. URL: `http://127.0.0.1:8088/autotable/`. | `01-home-loaded.png` | ✅ |
| 1b | Dismiss tour + onboarding | `#tour-skip` succeeded; `#onboarding-skip` blocked by **a stack of overlays** in the first attempt (see Fix #1). With init-script CSS defang, both close. | `01b-after-tour-dismissed.png` | ⚠️ partial |
| 2 | Find lobby | `#lobby-quick-match` present + visible (count=1). `#loading` element **never hides** — still shows "Loading..." behind the lobby (stale UI state). | `02-lobby-state.png` | ⚠️ |
| 3 | Pick Changsha rule preset | `#lobby-rule-preset-select` **NOT FOUND** — 0 occurrences in DOM. The DB-seeded `ChangshaRulePreset` rows never surface in the lobby. | `03-after-preset-select.png` | ❌ |
| 4 | Click Quick Match | Click succeeded. Lobby closes. Scene begins to load. **404 on `GET /autotable/api/changsha/pattern-ordering`** (front-end uses relative URL, served from `/autotable/` so the request resolves under `/autotable/api/...` but the backend exposes it at `/api/...`). | `04-after-quick-match.png` | ⚠️ |
| 5 | Click Take Seat | `.take-seat` elements: total=4, **visible=0**. After Quick Match auto-fills bots, the user is auto-seated and the seat buttons are hidden — so the manual "Take Seat" path is unreachable from Quick Match. No way to *un*-do the auto-seat to take a different seat without leave-seat dance. | `05-after-take-seat.png` | ❌ for "I want to pick my seat" flow; ✅ for "I just want to play" |
| 6 | Game canvas appears | `<canvas>` count: **1** (the three.js scene root). | `06-game-scene.png` | ✅ canvas mounted |
| 6.5 | Tile textures load? | **NO.** Three.js `GLTFLoader` calls `URL.createObjectURL(blob)` to extract embedded textures from `.glb` then fetches them via `fetch('blob:...')`. The backend's CSP has `connect-src 'self' ws: wss:` — **`blob:` is missing**, so every texture fetch is blocked. End-state: pageerror `TypeError: Cannot set properties of undefined (setting 'colorSpace') at processMaterial` because the texture object is undefined. **Tile meshes have no textures.** | `06-game-scene.png` | ❌ **critical** |
| 7 | Click Deal | `#deal` visible+enabled, click succeeded. `Setup` panel opens. | `07-after-deal.png` | ✅ click landed |
| 7b | Click Connect (legacy SignalR) | `#connect` (class `server-disconnected`) clicked. State **stays `server-disconnected`** — SignalR connection to `/autotable/hubs/...` does not establish from inside the bundle (related: lack of an authenticated session; the player has not signed in). | `07b-after-connect.png` | ❌ |
| 8 | Visible buttons at game-time | 16 visible buttons. Notable: `Deal`, `Setup`, `Dealer`, `Honba: 0` *(riichi-only, leaking into Changsha)*, `碰 Pung`, `吃 Chow`, `杠 Kong`, `胡 Hu`, `跳过 Pass`, `Connect` *(still disconnected)*, `Nick`, `Leave seat`, `More`, `☰ Lobby`, `⚙ Settings`. | — | ⚠️ Riichi `Honba` button visible in Changsha mode |
| 9 | Tile / hand DOM surfaces | `[data-testid*="tile"]`: **0**. `[data-testid*="hand"]`: **0**. `[data-testid*="seat"]`: 4. **Tiles are rendered solely in WebGL** — there is no DOM projection of the player's hand, no `[data-testid="tile-*"]` for screen readers, no a11y projection. Combined with the texture-load failure (#6.5) this means **the player cannot see their tiles at all.** | — | ❌ |
| 10 | Final state | Same as step 7b — game canvas is visible, scene loaded, Deal succeeded, but Connect failed and tiles have no textures. | `10-final-state.png` | ❌ NOT PLAYABLE |

## Browser console errors (verbatim, deduplicated)

```
404 GET /autotable/api/changsha/pattern-ordering
    (front-end fetched relative 'api/changsha/pattern-ordering' from
    document base /autotable/; backend exposes at /api/changsha/pattern-ordering)

Connecting to 'blob:http://127.0.0.1:8088/<uuid>' violates the following
Content Security Policy directive: "connect-src 'self' ws: wss:".
The action has been blocked.
    (×4 — one per tile-texture in the GLB)

Fetch API cannot load blob:http://127.0.0.1:8088/<uuid>. Refused to
connect because it violates the document's Content Security Policy.

THREE.GLTFLoader: Couldn't load texture blob:http://127.0.0.1:8088/<uuid>
    (×4)

THREE.Material: 'color' is not a property of THREE.MeshStandardMaterial.
THREE.Material: 'metalness' is not a property of THREE.MeshStandardMaterial.
THREE.Material: 'roughness' is not a property of THREE.MeshStandardMaterial.
THREE.Material: 'emissive' is not a property of THREE.MeshStandardMaterial.
    (repeated — three.js version-mismatch between the GLB exporter and the
    runtime; warnings only, not blockers)

TypeError: Cannot set properties of undefined (setting 'colorSpace')
    at Ae.processMaterial (three-renderer.9c8e77c2.js:2:37775)
    at Ae.processMesh (three-renderer.9c8e77c2.js:2:37682)
    at gltf-loader.01115b14.js:1:2340
    (caused by the CSP blob: block above — the texture future resolves to
    undefined, and processMaterial tries to set `.colorSpace` on it)
```

## Network errors / failed requests

```
404 GET http://127.0.0.1:8088/autotable/api/changsha/pattern-ordering
FAILED GET http://127.0.0.1:8088/autotable/api/changsha/pattern-ordering -- net::ERR_ABORTED
```

No 5xx responses from the backend during the run. The backend itself
is healthy.

## TOP 5 FIX LIST (priority order)

### 1. **CSP `connect-src` is missing `blob:` — tiles render with no textures**
**File:** `src/backend/src/Mahjong.Autotable.Api/Observability/SecurityHeadersMiddleware.cs:172` (and the `StrictCsp` twin at `:191`)
**Current:**
```
connect-src 'self' ws: wss:;
```
**Required:**
```
connect-src 'self' ws: wss: blob:;
```
**Why:** Three.js' `GLTFLoader` (`node_modules/three/examples/jsm/loaders/GLTFLoader.js:3390`) extracts embedded textures from `.glb` via `URL.createObjectURL(blob)` and then `fetch()`es them. With `blob:` absent from `connect-src`, every texture load is blocked. The downstream symptom is the visible `TypeError: Cannot set properties of undefined (setting 'colorSpace') at processMaterial` — that's processing a texture that came back undefined because the fetch was blocked. **End state: no mahjong tile is rendered correctly.** This is the single largest playability blocker; everything else is paper-over.

### 2. **Front-end `pattern-ordering` URL is relative — resolves to `/autotable/api/...` and 404s**
**File:** `src/frontend/autotable-src/src/pattern-utils.ts:79`
**Current:**
```ts
const res = await fetch('api/changsha/pattern-ordering', { credentials: 'same-origin' });
```
**Required:**
```ts
const res = await fetch('/api/changsha/pattern-ordering', { credentials: 'same-origin' });
```
**Why:** The bundle is served from `/autotable/` and `<base href>` is unset, so `fetch('api/...')` resolves to `/autotable/api/changsha/pattern-ordering` — the backend has the route registered at `/api/changsha/pattern-ordering` (`Program.cs:1491`), so it 404s. Same pattern likely exists for any other `fetch('api/...')` in the bundle (`grep` shows this is the only one in `pattern-utils.ts`, but **every fetch in the bundle should be audited for the same bug**). The fallback path silently swallows the error, so pattern display ordering is wrong but the game limps along — until other endpoints exhibit the same shape.

### 3. **Three full-page overlays with `aria-hidden="true"` intercept all pointer events**
**Files:**
- `src/frontend/autotable-src/index.html` (or component templates) — `#tour-overlay`, `#magic-link-landing`, `#signin-modal-backdrop`
- Whichever module mounts these (probably `tour.ts`, `magic-link.ts`, and `signin-modal.ts`)

**Symptom:** Before injecting a `pointer-events: none` CSS shim, the very first click on `#onboarding-skip` was intercepted by `#tour-overlay`; then by `#magic-link-landing` (which is in DOM with `aria-hidden="true"` but `display: block` and `pointer-events: auto`); then by `#signin-modal-backdrop` (likewise aria-hidden in DOM with the modal supposedly closed). With all three defanged via init-script CSS the lobby is finally clickable.

**Required:** Any element with `aria-hidden="true"` that is currently in the DOM must EITHER be removed from the DOM, OR have `pointer-events: none` and `display: none`. A `aria-hidden="true"` element that intercepts clicks is a critical a11y violation and a real playability bug.

### 4. **Lobby `#lobby-rule-preset-select` never renders despite seeded DB rows**
**Files:**
- `src/frontend/autotable-src/src/rule-presets.ts:302` (the consumer that calls `getElementById('lobby-rule-preset-select')`)
- `src/frontend/autotable-src/index.html` (the picker `<select>` itself — needs to be present in the lobby panel)
- `src/backend/src/Mahjong.Autotable.Api/Changsha/...` — the GET endpoint that streams rule-presets to the lobby on boot

**Symptom:** Despite the backend seeding `ChangshaRulePresets` on startup (visible in the log: an `INSERT INTO "ChangshaRulePresets" (…)` runs in EF Core), the front-end exposes **0** `lobby-rule-preset-select` elements in DOM. Users have no way to actually pick a Changsha rule preset from the lobby. Quick Match works (it ignores the picker) but the picker UX is completely absent.

### 5. **SignalR `#connect` button stays `server-disconnected` after click**
**File:** `src/frontend/autotable-src/src/client-ui.ts` (or wherever `#connect`'s click handler is wired)

**Symptom:** Clicking `#connect` (class `btn btn-warning btn-sm w-100 server-disconnected mr-2`) does not transition the class to `server-connected`. The hub URL is `/autotable/hubs/changsha` — same prefix problem as Fix #2 may apply (relative vs absolute URL), or auth is required and the unauthenticated player gets a 401 from the hub negotiate.
- **Investigate:** open chromium DevTools → network → filter `hubs/`. If a 401 is returned, the fix is either (a) make the hub endpoint anonymous-friendly so Quick Match's bot-game can run without sign-in, OR (b) gate Quick Match behind a sign-in step. Either way, **the current state is "Connect button exists but does nothing"**, which is unplayable.

## Changsha rule coverage assessment

This is **out-of-scope for a UI playtest** — the rules engine cannot be exercised because:
1. Tile textures don't render (Fix #1)
2. SignalR hub doesn't connect (Fix #5)

So we cannot put hand→discard→claim→Hu through its paces from the UI. Per `https://mahjongpros.com/blogs/how-to-play/beginners-guide-to-changsha-mahjong`:

| Rule | UI exposure | Code location (from grep) |
|------|-------------|----------------------------|
| Dice roll determines wall break point | ❓ not exercised — scene loads but cannot interact | `Changsha/ChangshaStateMachine.cs` (modified in working tree but on wave-23 branch this is the registered state machine) |
| Players pick own tiles in groups of 4 | ❓ not exercised — Manual deal mode IS in the `#deal-mode` dropdown ("Manual (click to pick)") | `index.html:120-126` |
| 12 + final = 13 tile setup | ❓ not exercised | `Changsha/Runtime/ChangshaGameRuntime.cs` (per Bishop W23 commits) |
| Discard-claim cycle (Pong/Kong/Hu/Chow) | ✅ DOM surfaces exist: `#claim-pung`, `#claim-chow`, `#claim-kong`, `#claim-hu`, `#claim-pass` (all disabled until a claim window opens) | `index.html:178-204` |
| Hu winning hand validation | ❓ not exercised — `胡 Hu` button is visible but disabled | — |
| Scoring with Changsha-specific patterns | ❓ not exercised; the pattern-ordering API is unreachable due to Fix #2 | `Changsha/Patterns/ChangshaPatternOrdering.cs` |

## Bot / single-player support

- **Single human + 3 bots:** ✅ The `Quick Match` button's tooltip is literally "Skip the pickers and start a 3-Medium-bot game now", and clicking it did transition the scene (lobby closed, three.js canvas activated). The bot count picker (`#bot-count`) and difficulty picker (`#bot-difficulty`) are in the Setup panel and default to 3-Medium for Changsha.
- **All-bot demo:** ❓ Not visible — no `All bots` button found. The `#bot-count` select tops out at "4 bots" so technically a user could pick 4 bots and then never `Take seat`, but the `Deal` button currently fires on the local seat-0 player.

## Verdict

**❌ NOT PLAYABLE end-to-end.**

The scene shell, lobby, Quick Match transition, and DOM-level claim/Deal controls all wire up. But because:
1. **CSP blocks the texture blobs** (Fix #1) → tiles are invisible / `colorSpace` undefined crash
2. **SignalR `#connect` doesn't actually connect** (Fix #5) → no state synchronisation
3. **No DOM tile/hand projection** (`tileTestids: 0, handTestids: 0`) → even if textures loaded, screen readers / fallback users see nothing

…a user **cannot play a hand of Changsha** today via this build. Fix #1 (one-line CSP change) is by far the highest-leverage win — it unblocks tile rendering, after which Fix #5 (SignalR connect) becomes the next blocker for actual gameplay state to flow.

## Artifacts

```
playtest-artifacts/
├── 01-home-loaded.png             — initial page load with tour + onboarding
├── 01b-after-tour-dismissed.png   — after #tour-skip + #onboarding-skip
├── 02-lobby-state.png             — lobby pickers + Quick Match visible
├── 03-after-preset-select.png     — no rule-preset-select to interact with
├── 04-after-quick-match.png       — lobby closed, scene loading
├── 05-after-take-seat.png         — take-seat hidden (Quick Match auto-seats)
├── 06-game-scene.png              — three.js canvas active, tiles in scene but textureless
├── 07-after-deal.png              — #deal clicked; Setup expanded
├── 07b-after-connect.png          — #connect clicked; still server-disconnected
├── 10-final-state.png             — final pose
├── findings.json                  — machine-readable manifest
└── playwright-output.log          — full Playwright run log + browser console + network
```
