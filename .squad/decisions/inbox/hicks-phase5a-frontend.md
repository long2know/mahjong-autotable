# Hicks — Phase 5a Frontend Wiring

**By:** Hicks (Frontend Dev)
**Date:** 2026-05-13
**Branch:** `stlong/changsha-3d-phase5a`
**Refs:** `docs/rules/changsha-3d-renderer-plan.md` §6, Defaults #3 & #7,
spike inbox at `.squad/decisions/inbox/copilot-directive-2026-05-13-3d-phase5a-defaults.md`

## TL;DR

Iframe is now wired to live Changsha game state. The upstream bundle
auto-connects to Bishop's `/autotable/ws` endpoint as soon as a game
exists. Camera-toggle HUD button works via postMessage → synthetic
keydown. Build clean (Vite, 645 KB / 186 KB gzip — unchanged baseline).
48/48 owned vitest tests green.

## Files changed (only files I own)

| File | LOC delta | Purpose |
|---|---:|---|
| `src/frontend/autotable/index.html` | +1 | `?embedded=1` → hide upstream sidebar + seat-buttons via `html[data-changsha-embedded="1"]` CSS attribute + inline `<script>` setter |
| `src/frontend/modern/src/pages/ChangshaTablePage.tsx` | +57 / −13 | Exported `buildAutotableIframeSrc(gameId, seatIndex?)`; `AutotableViewport` accepts `userSeat`, wraps `src` in `useMemo([state.gameId, userSeat])`, overlays `CameraToggleButton` at top-right (absolute, `zIndex: 10`); reattaches bridge on `iframeSrc` change (correct — iframe reloads → fresh WS) |
| `src/frontend/modern/src/changsha/autotableBridge.ts` | +1 | Extended `BridgeOutboundMessage` union with `{ type: 'camera-toggle' }` so `bridge.send({ type: 'camera-toggle' })` typechecks |
| `src/frontend/autotable/changsha-bridge-receiver.js` | +16 | New `case 'camera-toggle'` dispatches `KeyboardEvent('keydown', { key:'p', code:'KeyP', keyCode:80, which:80, bubbles:true })` on `document`; also re-emits CustomEvent for symmetry with other types |
| `src/frontend/modern/src/changsha/components/CameraToggleButton.tsx` | +43 (new) | Fluent UI 9 `Button` + `Tooltip` "🎥 Toggle View" |
| `src/frontend/modern/src/changsha/components/index.ts` | +1 | Re-export |

**Untouched (per task constraints):** `src/backend/**`, `__tests__/**`,
`autotable.9519e86d.js` and every bundled asset.

## Iframe URL format (canonical)

```
/autotable/?gameId={state.gameId}&embedded=1&seat={userSeat}
```

- Order is `URLSearchParams` insertion order: `gameId`, `embedded`,
  `seat`. Tests should parse via `new URL(src, base).searchParams.get(...)`
  rather than asserting raw string equality.
- `seat` parameter is **optional** in the exported helper
  (`buildAutotableIframeSrc(gameId, seatIndex?)`) — when `seatIndex`
  is `undefined` the parameter is omitted (spectator mode). Today
  `ChangshaTablePage` always passes `USER_SEAT = 0`, but the helper
  supports a no-seat caller for future use.
- `state.gameId` defaults to `''` when no game is bound; in that case
  the iframe is gated behind `showLobby`, so `state.gameId` is
  guaranteed non-empty by the time `AutotableViewport` mounts. (The
  helper still writes `gameId=` for an empty string; the iframe stays
  hidden behind the lobby so this doesn't manifest.)

## Camera-toggle message contract (for Hudson's tests)

**Wire envelope (parent → iframe):**
```js
window.postMessage(
  { proto: 'changsha-bridge/1', type: 'camera-toggle' },
  '*'
);
```

**Receiver behavior** (`changsha-bridge-receiver.js`):
```js
case 'camera-toggle':
  try {
    document.dispatchEvent(new KeyboardEvent('keydown', {
      key: 'p', code: 'KeyP', keyCode: 80, which: 80, bubbles: true
    }));
  } catch (e) { console.error('[changsha-bridge] camera-toggle dispatch failed', e); }
  dispatchAutotableEvent('camera-toggle', msg);
  break;
```

**Why this works against the bundle:** Verified at offset 1013613 in
`autotable.9519e86d.js`:
```js
case "p": this.settings.perspective.checked = !this.settings.perspective.checked,
          this.updateSettings(); break;
```
This handler is registered at offset 1011975 via
`window.addEventListener("keydown", this.onKeyDown.bind(this))`. Because
the listener is on `window`, a `document.dispatchEvent(...)` with
`bubbles: true` reaches it via the bubble phase. Match is on lowercase
`e.key`, so `key: 'p'` is the correct casing.

## WS URL resolution verification (Default #7)

Read upstream's `getUrl()` from the minified bundle at offset 1003085:
```js
getUrl() {
  let e = window.location.pathname;
  e = e.substring(1, e.lastIndexOf("/") + 1);  // "/autotable/" → "autotable/"
  let t = "https:" === window.location.protocol ? "wss:" : "ws:";
  let i = window.location.host;
  let n = e + "ws";                            // "autotable/ws"
  return `${t}//${i}/${n}`;                    // "wss://host/autotable/ws"
}
```

**Result:** with iframe loaded at `/autotable/?gameId=X&embedded=1&seat=0`,
the bundle connects to `ws(s)://{host}/autotable/ws`. Matches Default #7
exactly — Bishop's endpoint at `/autotable/ws` is the right path. No
coordination change needed.

**Additionally** — the bundle reads `gameId` from the URL via
`getUrlState() = new URLSearchParams(window.location.search).get("gameId")`
at offset 1002792. The bundle's `start()` (offset 1002975) calls
`this.client.join(this.url, gameId)` if and only if `getUrlState()` is
non-null — otherwise the WS stays disconnected. So **`?gameId=X` is
mandatory for the bundle to auto-connect**. Without it the bundle would
sit idle requiring a manual click. This is a load-bearing detail for
Bishop's Strategy C — without `gameId` in the URL, the bundle never
sends `JOIN gameId` and the translator never sees any client.

## Sidebar hide approach

```html
<style id=changsha-embedded-mode>
  html[data-changsha-embedded="1"] #sidebar,
  html[data-changsha-embedded="1"] .seat-buttons { display: none !important; }
</style>
<script>
  (function(){
    try {
      if (new URLSearchParams(window.location.search).has('embedded')) {
        document.documentElement.setAttribute('data-changsha-embedded','1');
      }
    } catch(e) {}
  })();
</script>
```

The script runs synchronously before `<body>` parses. CSS resolves
against `html[data-changsha-embedded="1"]` so the sidebar and
`.seat-buttons` row never become visible — no flash. The bundle still
constructs them in DOM and binds handlers (we don't strip them, just
hide visually) so any internal bundle logic referring to those nodes
keeps working.

**Standalone `/autotable/` sandbox preserved (Default #2):** when the
URL has no `embedded` parameter, the data attribute is not set, the
CSS rules do not match, and the upstream UI renders exactly as before.

## What Bishop should know

- The bundle's WS URL will be `wss?://{host}/autotable/ws` (path is
  taken from the iframe location's pathname segment). Confirmed
  Default #7 path is correct.
- The bundle's JOIN message carries the `gameId` from the URL —
  Bishop's WS handshake must accept arbitrary client-chosen gameIds
  (UUIDs from `CreateGame`) and route them to the right
  `ChangshaGameState` instance.
- The `?seat=N` parameter is **not** read by the upstream bundle.
  It's metadata I'm placing in the URL for Bishop's translator to
  read off the WS handshake (e.g. via a query-string parse on
  `httpContext.Request.QueryString`) so the translator knows which
  seat the React-side user owns. If Bishop's design needs it on a
  different transport (e.g. inside the first `UPDATE` after JOIN),
  let me know and I'll switch.
- Upstream's `Client` auto-reconnects 15× at 2 s intervals when the
  WS drops. Bishop's endpoint should respond to `JOIN unknown-id` with
  an empty `UPDATE` rather than refusing the socket, to avoid flapping
  (spike risk doc, surprise #6).

## What Hudson should know (test contract)

Three tests Hudson wrote on his WIP branch land cleanly against my
implementation once two trivial issues are fixed in his test files
(I MUST NOT touch `__tests__/**`, so flagging here):

1. `autotableBridge.cameraToggle.test.ts` — `RECEIVER_PATH` is wrong:
   `../../../../../autotable/changsha-bridge-receiver.js` has one too
   many `..`. From `__tests__/` the correct path is `../../../../autotable/changsha-bridge-receiver.js`
   (four `..`'s, not five). Once fixed, the two `.skip()`'d tests will
   pass against my receiver — verified the behavior matches his spec
   exactly (key='p', code='KeyP', bubbles=true).

2. `autotableBridge.embedded.test.ts` and the `cameraToggle` file both
   import `node:fs` / `node:path` / `__dirname`. The repo's `tsconfig`
   doesn't include `@types/node`, so `tsc -b` errors on these files.
   Vitest itself runs them fine at runtime (esbuild transformer +
   jsdom env), so adding `@types/node` to devDeps OR declaring
   `/// <reference types="node" />` at the top of the test files will
   unblock `npm run build` end-to-end. Pre-existing on his branch
   before my changes — not introduced by Phase 5a wiring.

3. `changshaTablePage.iframeUrl.test.tsx` — the local
   `buildAutotableIframeSrc` helper in the test file matches the
   exported helper I shipped from `ChangshaTablePage.tsx` byte-for-byte.
   He can drop the local copy and import from
   `'../../pages/ChangshaTablePage'` per his own un-skip note. The
   render-based `.skip()`'d memoization test should now work — the
   `useMemo([state.gameId, userSeat])` is in place.

## Build / test results

- `npm --prefix src/frontend/modern run build` (with Hudson's WIP test
  files moved aside, since they have pre-existing tsc errors unrelated
  to Phase 5a): **clean** — `tsc -b` passes, `vite build` produces
  `dist/assets/index-g8xrTc1k.js 645.44 kB / 185.85 kB gzip`. Identical
  bundle baseline modulo the new component.
- `npm test` over the 4 owned test files: **48/48 green**
  (autotableBridge, changshaReducer, signalrClient, useChangshaMockGame).
- Hudson's `cameraToggle` test file: 2 of 3 tests `.skip()`'d pending
  un-skip after his path fix; the 3rd ("ignores wrong proto") fails on
  a path bug in his test file, not on my receiver behavior.

## Status

Ready to merge. Bishop and Hudson can land in any order — disjoint
file sets.
