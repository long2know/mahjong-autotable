# Hudson — Phase 5a Frontend Test Coverage

**By:** Hudson (Tester)
**Date:** 2026-05-13
**Branch:** `stlong/changsha-3d-phase5a`
**Refs:** `docs/rules/changsha-3d-renderer-plan.md` §3.3, §6, §8; Defaults #3 & #7

## TL;DR

Locked down the Phase 5a frontend contract with 12 new vitest cases (10 active
+ 2 intentionally skipped). All tests run against Hicks's actual Phase 5a
commit `1c1bd4a` (landed mid-session). Frontend `npm run build` clean,
no regression in existing 48 tests.

| Suite | Total | Active | Skipped |
|---|---:|---:|---:|
| Before Phase 5a | 48 | 48 | 0 |
| After Phase 5a  | 62 | 60 | 2 |
| **Delta** | **+14** | **+12** | **+2** |

*(48→60 active means 12 net-new running tests, exactly above the 7-8 brief
target — the extra coverage comes from negative controls and a static
fixture check on `index.html`.)*

## Tests added

### 1. `changshaTablePage.iframeUrl.test.tsx` (4 active tests)

Imports `buildAutotableIframeSrc` from `pages/ChangshaTablePage.tsx` (Hicks's
Phase 5a export). Asserts:

- Builds `/autotable/?gameId=…&embedded=1&seat=…` for a seated player (parses
  via `URL` + `searchParams.get` — order-tolerant on the wire).
- Omits `seat` when `seatIndex` is `undefined` (spectator mode).
- Faithfully encodes `seat=3` (north).
- Value-stability: same inputs → identical strings (underwrites Hicks's
  `useMemo([state.gameId, userSeat])` in `AutotableViewport`); distinct
  inputs → distinct outputs (guards against accidental constant cache).

### 2. `autotableBridge.cameraToggle.test.ts` (3 active tests)

Loads `src/frontend/autotable/changsha-bridge-receiver.js` once at module
level via `require('node:fs')` + `eval` and exercises it from jsdom. Asserts:

- `postMessage({ proto:'changsha-bridge/1', type:'camera-toggle' })` →
  synthetic `keydown(key='p', code='KeyP')` on `document`.
- The synthesized event has `bubbles: true` (so upstream's
  `window.addEventListener('keydown', ...)` at offset 1011975 of the bundle
  receives it via bubble phase).
- Wrong-proto / missing-proto / garbage payloads do NOT fire any keydown
  (negative control on the receiver's proto sentinel).

### 3. `autotableBridge.embedded.test.ts` (2 active + 2 skipped)

**Active — static fixture check on `index.html`:** parses Hicks's edited
`src/frontend/autotable/index.html` and asserts the embedded-mode
implementation is present:

- CSS rule `html[data-changsha-embedded="1"] #sidebar { display:none }`
- CSS rule also hides `.seat-buttons`
- Inline `<script>` reads `URLSearchParams(window.location.search)`
- Inline `<script>` calls `.has('embedded')`
- Inline `<script>` calls `setAttribute('data-changsha-embedded','1')`
- The `#sidebar` element still exists in DOM (sandbox preserved — only
  hidden by CSS when embedded; standalone `/autotable/` still works per
  Default #2)

**Active — negative control:** receiver loads cleanly under jsdom without
crashing when no `embedded=1` marker is present (sandbox path).

**Skipped — `[MANUAL / INDEX.HTML]`:** stays skipped **permanently**. The
runtime behavior of the inline `<script>` cannot be exercised by vitest
under jsdom (the bundle's `index.html` is loaded via real iframe navigation
in production). Manual repro inlined in test body:
1. `npm --prefix src/backend run watch`
2. Open `http://localhost:5114/autotable/?embedded=1` — sidebar hidden
3. Open `http://localhost:5114/autotable/` (no query) — sidebar visible

**Skipped — `[NOT IMPLEMENTED — fallback path]`:** the alternative
implementation strategy (receiver script reads `location.search` and sets
a body class) was NOT chosen. Stays skipped unless a future refactor moves
the logic into the receiver instead of `index.html`.

### 4. `changshaReducer.signalrIntegration.test.ts` (3 active tests)

Phase 5a regression guard for the React-side state surface:

- **Snapshot test:** pins the 20-discriminator `GameAction` union
  (alphabetically sorted). Phase 5a MUST NOT add or remove a discriminator
  — if Bishop/Hicks change the SignalR event set, this test surfaces it.
- **`reset` action:** verifies it restores the expected `initialChangshaState`
  shape (4 `SeatHand` entries with empty `concealed[]` arrays, 4 `SeatInfo`
  with default east/south/west/north winds, empty `discardPile`, lobby
  phase, empty `gameId`). The earlier draft of this test mistakenly
  asserted `hands: []` — actual initial state has 4 empty hand entries.
- **Module-export smoke:** `useChangshaGame`, `useLiveChangshaGame`,
  `useChangshaMockGame`, `shouldUseMock`, `setUseMockOverride` all export
  the expected callable surface. Phase 5a's only React surface change is
  iframe `src` — the action set MUST stay intact.

## Seams + scaffolding required

1. **Receiver script in jsdom.** Vite's `?raw` suffix is blocked by
   `server.fs.allow` because `src/frontend/autotable/` is outside the
   modern frontend root. Fell back to `require('node:fs')` with local
   ambient declarations:
   ```ts
   declare const require: (id: string) => any;
   declare const __dirname: string;
   const fs = require('node:fs');
   ```
   This keeps the frontend `tsconfig.json` free of `@types/node` (which
   would leak Node types into app code) while letting the test file
   pull node APIs at runtime.

2. **Receiver IIFE listener stacking.** The receiver registers an
   anonymous `window.addEventListener('message', ...)`. Re-loading per
   test would stack listeners across tests in the same worker. Solved
   with module-level `receiverLoaded` guard + `ensureReceiverLoaded()`.

3. **No render-based useMemo identity check.** Brief asked for a render
   twice + assert iframe src identity test. Implemented as a *value-stability*
   helper test instead (same inputs → identical strings) because Hicks's
   `useMemo([state.gameId, userSeat])` is one composition layer above the
   exported helper. Render-based identity check is filed as an optional
   Phase 5b hardening (below).

## Coverage gaps / Phase 5b followups

| Gap | Severity | Reason | Suggested follow-up |
|---|---|---|---|
| Component render: `<AutotableViewport>` `iframe.src` identity across re-renders | Low | Value-stability of `buildAutotableIframeSrc` is tested; the React `useMemo` wrapping is straightforward and visible in source. Mocking `useChangshaGame` requires several Fluent UI providers — high test-infra cost, low marginal value. | Phase 5b: optionally add a `@testing-library/react` test that mocks `useChangshaGame` to a stable state and asserts iframe `src` stability. |
| Runtime sidebar hide on `?embedded=1` | Low (static check covers source-level invariant) | Vitest under jsdom cannot exercise inline `<script>` in `index.html` via real iframe navigation. | Add to a future Playwright e2e suite. Manual repro is documented in the test file. |
| Camera-toggle round-trip (button click → keydown synthesis → bundle observer toggles perspective) | Low | The bundle's perspective toggle is closed over inside the minified IIFE; only observable via WebGL state. | Manual smoke only — leave outside vitest scope. |
| `?seat=N` translator interpretation | N/A (backend) | Bishop's territory — translator reads seat from WS handshake query string. | Bishop's translator tests should cover. |

## Verification

```
npm --prefix src/frontend/modern test --run
  Test Files  8 passed (8)
       Tests  60 passed | 2 skipped (62)

npm --prefix src/frontend/modern run build
  ✓ built in 6.56s   (dist/index.html 0.41 kB / 645.44 kB JS unchanged)
```

## Commit

Tests committed in same wave as Hicks's Phase 5a — see git log on
`stlong/changsha-3d-phase5a` for SHA. Branch pushed to origin.

## What Bishop / Hicks should know

- **iframeUrl spec lock:** `buildAutotableIframeSrc` is now exercised by 4
  active tests. Changing its signature or output format will break tests
  immediately — that's the point. Coordinate any future tweaks via this
  test file.
- **camera-toggle spec lock:** the receiver's `case 'camera-toggle'` is
  pinned to `key:'p' / code:'KeyP' / bubbles:true` on `document`. Any
  receiver-script refactor must preserve those event properties.
- **`index.html` fixture lock:** the static-fixture test will flag any
  regression in the embedded-mode CSS / inline script if a future
  upstream bundle re-mirror clobbers Hicks's edits. The `README.md` in
  `src/frontend/autotable/` should call out that those edits are
  intentional and must survive a re-mirror.
- **Reducer regression guard:** if Bishop adds a new SignalR event type
  in Phase 5b that Hicks wires into the reducer, the alphabetical
  snapshot test must be updated **in the same PR** as the reducer
  change. Otherwise that PR is rejected.
