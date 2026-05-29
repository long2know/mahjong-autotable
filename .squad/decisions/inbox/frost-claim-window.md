# Frost — Claim window doesn't open for local seat (Vasquez D1 root-cause)

**Date:** 2026-05-29
**Branch:** `investigate/claim-window-local-seat`
**Probe:** `playtest-artifacts/frost-claim-window-verify.spec.mjs`
**Hand-off:** Vasquez (update D scenario premise), Hicks (overlay author),
Bishop (translator owner — change is in the protocol layer Bishop owns).
**Baseline SHA:** main @ 2026-05-29 morning (post-Bishop Int64-boxing fix).

## TL;DR

Vasquez's audit gate D1 ("claim window opens for local seat in a
4-Hard-bot match in 90 s") is **FAIL** for **three layered reasons**.
Two are real bugs; one is an audit premise issue:

1. **BUG #1 — translator emits `deadline: 0` always.** The backend
   sets `deadlineUnixMs: 0` on every `ClaimWindowEntry` regardless of
   the runtime's `ClaimWindowTimeoutMs`. Both the new Ferro overlay
   (`ui/claim-window-overlay.ts`) and the side-panel claim renderer
   (`game-ui.ts.tickClaimCountdown`) compute
   `remaining = deadline - Date.now()`, which with `deadline=0` is a
   huge negative — so the overlay hides instantly and the side panel
   auto-passes instantly. The local player never sees the window even
   when the rule engine correctly emits a claim opportunity for them.
2. **BUG #2 — wire-key type mismatch on the `claim` collection.** The
   backend's `ChangshaCollectionEncoder.EncodeClaimWindow(int seat …)`
   writes the seat as a **number** into `CollectionEntry.Key`, which
   the protocol's `CollectionEntryJsonConverter.Write` serialises as a
   bare JSON number. The frontend `Collection<K,V>` stores entries in
   a JS `Map`, where `Map.get(0) !== Map.get("0")` — and `game-ui.ts`
   `sendClaim()` writes the outbound action via
   `client.claim.set(String(selfSeat), …)`, so every overlay /
   side-panel filter compares `key !== String(selfSeat)` (a strict
   inequality between `Number` and `String`). The filter always
   misses, the listener's fallback `client.claim.get(String(selfSeat))`
   also misses (the map is keyed by `Number`), and `activeClaim` stays
   `null` forever — even with a perfectly-shaped claim entry actually
   sitting in the map under the numeric key.
3. **AUDIT PREMISE — Vasquez's D scenario uses `botCount=4`.** A
   `botCount=4` URL fills every seat with a bot at WS-upgrade time;
   the viewer connects as a spectator (`seat=(null)`). Spectators
   are by-design never claim-eligible (the rule engine emits
   opportunities for the three non-discarder seats, none of which are
   the viewer's). D1 can't pass without giving the viewer a real
   seat (`botCount=3`).

## How I traced it

End-to-end driven by
`playtest-artifacts/frost-claim-window-verify.spec.mjs`, which connects
with `dealMode=auto&botCount=3&botDifficulty=Hard`, takes seat 0, runs
an `autoDiscardHumanDraw()` helper to keep the human's turn from
stalling, and polls every 400 ms for:

* `client.claim.get(String(0))` AND `client.claim.get(0)` (proves the
  entry shape AND tells us which key type the map is using),
* every key in `client.claim.entries()` (proves what the wire actually
  delivered), and
* the `.ferro-claim-overlay-visible` class on the DOM (proves the
  overlay's listener responded).

Console capture confirmed every angle:

```
"mine": { "available": ["Pung"], "deadline": <epoch-ms>, "source": 1, "tile": 50 },
"overlay": { "exists": true, "hidden": true, "hasVisibleClass": false,
             "worldSeat": 0, "clientSeat": 0,
             "claimKeys": [["0", ["available","deadline","source","tile"]]] }
```

— a claim entry **was** in the map (probe-side `.get(0)` returns it,
hence the `claimKeys` snapshot stringifying its key to "0"), but the
overlay's listener never set `activeClaim` because its `String(seat)`
filter against the numeric wire key always failed.

## What's fixed (this branch)

### Backend — `OpenedAtUnixMs` + real deadline
* `Changsha/ChangshaDomain.cs` — added `OpenedAtUnixMs` (long) to
  `ChangshaClaimWindow`.
* `Changsha/ChangshaStateMachine.cs` — sets
  `OpenedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`
  whenever a claim window opens (`Discard` + `DeclareAddedKong`).
* `Autotable/ChangshaToAutotableTranslator.cs` — `Translate` now
  accepts `long claimWindowTimeoutMs = 0`. When **both**
  `OpenedAtUnixMs > 0` and `claimWindowTimeoutMs > 0`, emits
  `deadlineUnixMs = OpenedAtUnixMs + claimWindowTimeoutMs`. Otherwise
  emits 0 (back-compat for tests / rehydrated state).
* `Autotable/AutotableWsEndpoint.cs` — accepts
  `IOptions<ChangshaRuntimeOptions>` in the ctor, snapshots
  `_claimWindowTimeoutMs` once, and passes it to both `Translate`
  call sites.

### Backend — wire-key consistency
* `Autotable/AutotableProtocol.cs` — `EncodeClaimWindow` /
  `EncodeClaimWindowClosed` now stringify the seat
  (`seat.ToString(CultureInfo.InvariantCulture)`) so the JSON key is
  always a string. Matches the frontend's
  `client.claim.set(String(seat), …)` storage convention and lets
  server snapshots merge into a single Map entry instead of splitting
  by type and silently shadowing it.

### Frontend defensive guards
* `ui/claim-window-overlay.ts` — `refresh()` and `tick()` now treat
  `deadline <= 0` as **"no client countdown"** instead of "expired
  now". When deadline is 0/negative the overlay still shows (badges +
  Pass), the timer reads "—", and the progress bar holds full so a
  rehydrated-state or test-injected entry doesn't auto-hide.
* `game-ui.ts` `tickClaimCountdown()` — same treatment: shows "—",
  stops the ticker, no auto-Pass on `deadline <= 0`.

### Tests
* `Autotable/AutotableTranslatorTests.cs` — added 4 new tests:
  * `ClaimEntry_EmitsAbsoluteDeadline_WhenTimeoutPassed`
  * `ClaimEntry_EmitsZeroDeadline_WhenTimeoutNotPassed`
  * `ClaimEntry_EmitsZeroDeadline_WhenOpenedAtZero_EvenWithTimeout`
  * `ClaimEntry_EmitsOnePerEligibleSeat_KeyedBySeatIndex` — now
    asserts the keys are **stringified** ("1","2","3") to pin the
    type contract.

## Regression

* `dotnet test … --filter "~AutotableTranslator|~ClaimAdjudicator|
  ~VariantSwitchAcceptance"` — 50/50 PASS.
* Frontend rebuild via `npm run build` (Vite) clean.
* End-to-end probe `frost-claim-window-verify.spec.mjs` now reports
  **PASS** — observed entry `available=["Chow"], deadline>0, source=3,
  tile=77`, overlay class `ferro-claim-overlay ferro-claim-overlay-visible`,
  `display: grid`, `bbox > 0`.

## Hand-off to Vasquez

D scenario (`playtest-full-game-integration.spec.mjs:888`) needs:
1. Change `botCount=4` → `botCount=3` so the viewer can take seat 0.
2. Add an explicit `await page.locator('.take-seat').first().click()`
   step so the seat actually binds to the human.
3. (Optional) Drive a periodic auto-discard from the human seat so the
   round doesn't stall when the player has 14 in hand — see
   `autoDiscardHumanDraw()` in the new verify spec for the helper.

Once those are in, D1 will pass with the backend + frontend fixes from
this branch.

## Hand-off to Hicks

If you ever wire a **new** Collection consumer that filters by seat,
remember: the on-wire key for `claim` is now a stringified seat
("0".."3"), and `Map.get` is type-strict. The Collection's own setters
also store via string. Always compare with `String(key) === selfKey`
(or coerce both to the same type) — the strict-equality trap is what
made BUG #2 invisible for so long.
