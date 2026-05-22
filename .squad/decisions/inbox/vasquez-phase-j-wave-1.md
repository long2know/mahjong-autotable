# Phase J Wave 1 — Vasquez memo (claim evaluator + hot-seat swap suites)

**Branch:** `stlong/phase-j-wave-1-hardening`
**Baseline:** 402 / 0 / 0 (Wave I.4 main = `d69b288`)
**Vasquez commit:** `ca9fe03` (`test(bot,autotable): Phase J Wave 1 — claim evaluator + hot-seat swap suites`)
**Final gate:** **409 passed / 0 failed / 0 skipped** (+7 over baseline; zero-skip streak preserved)

**Vasquez lane (test-only):**
- NEW `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Acceptance/ClaimEvaluatorTests.cs` — 4 facts
- NEW `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/HotSeatSwapTests.cs` — 3 facts

Strict test-only lane. **Zero production-code edits** in this commit.

## Production owners (Bishop, Hicks)

- **Bishop (commit `361d805`):** wired `HandEvaluator.MinShantenToHu` into
  `HardStrategy.DecideClaimPhase` as a strict-shanten-drop claim acceptance
  gate. Helpers `ShantenAfterPungClaim` / `ShantenAfterExposedKongClaim` /
  `ShantenAfterChowClaim` simulate the post-claim hand; `ClaimAcceptanceRank`
  encodes the tie-breaker Hu>Kong>Pung>Chow. Hu remains an unconditional
  fast-path. Chow simulation mirrors `RemoveChowTilesByLowestPattern` so the
  gate's decision matches the chow the runtime would actually play.
  Bishop deferred Task 2 (wall-exhaustion fast path) as a no-op because
  `ChangshaGameStateMachine.AdvanceToNextPlayer` already short-circuits to
  `WallExhausted` when `Wall.Count == 0`. See his memo §"What I deferred".
- **Hicks (commit `781798e`):** frontend-only — Move button + seat picker in
  the sidebar HUD, soft-reconnect via `?seat=` URL rewrite, and a one-line
  `world.seat` initial-value tweak for the spectator camera lock. No
  backend changes; my hot-seat swap tests verify the existing seat-take +
  runtime-binding pipe handles the disconnect/reconnect cycle Hicks's UI
  triggers.

## What shipped per file

### `ClaimEvaluatorTests.cs` — 4 facts pinning Bishop's gate

All four facts use reflection-defensive `ChangshaBotEngine.Resolve("hard")`
to stay compile-stable across future refactors, and exercise
`HardStrategy.OnOtherDiscard` directly via a manually-constructed
`ChangshaClaimWindow`.

1. **`Hard_RefusesPung_WhenItRaisesShanten`** — fixture is a 13-tile
   SevenPairs-candidate hand (5 pairs + 3 lones, shanten=1). The discard is
   a third copy of one pair-rank; accepting Pung breaks the SevenPairs path
   (because `ComputeSevenPairsShanten` disqualifies any hand with declared
   melds — verified by inspection of `HandEvaluator.cs:283`), and the
   standard path can only achieve shanten=2 from the remaining 4 pairs.
   Pre=1, post=2 → strict drop fails → Hard must `Pass`. Pinned by an
   explicit pre-shanten=1 sanity assertion before the action assertion.

2. **`Hard_AcceptsPung_WhenItDropsShanten`** — fixture is a chain of partial
   shapes (2 Wan chow partials, a gapped Tong partial, a Tong pair, a Tiao
   partial, a Tiao-7,7 pair) plus a Tiao-7 in the river → Pung Tiao-7. Pre
   shanten=3; post shanten=2 (locking the Tiao-7 pung leaves a partial-rich
   shape inside groupsNeeded=3). Strict drop → `Claim(Pung)`. Pre-shanten=3
   sanity-pinned.

3. **`Hard_AlwaysAcceptsHu_RegardlessOfShantenCheck`** — uses
   `AcceptanceFixture.ThirteenTileWaitingForWan1()` (the canonical Hu-ready
   hand) + a Wan-1 discard. Pre-shanten is 0 (clamped at zero by
   `MinShantenToHu`), so without Bishop's unconditional Hu fast-path the
   strict-drop check would refuse every non-Hu opportunity AND Hu itself
   (post=0 is NOT strictly less than pre=0). The pre-shanten=0 assertion is
   the regression alarm that fires loud if the clamp semantics change.

4. **`Hard_PrefersHigherPriorityTier_AmongShantenDroppingClaims`** —
   reframed from the directive's Kong-vs-Pung suggestion to **Pung-vs-Chow**
   because Kong-over-Pung is mathematically unreachable (proof: the shanten
   counter treats any 3-of-a-kind in concealed as a complete pung group,
   so claiming a Kong on a tile the bot already has 3 of cannot strictly
   drop shanten — verified by 7 candidate fixtures spanning compact-pung,
   chow-overlap, and disjoint-partial shapes; in every case Kong yields
   shanten ≥ pre while Pung yields shanten ≤ pre). The Pung-vs-Chow tie
   exercises the same `ClaimAcceptanceRank` tie-breaker mechanism (Pung=2,
   Chow=1) from a fixture where both options strictly drop shanten from 2
   to 1, so the only deciding factor is the rank. Bishop's Kong-over-Pung
   lift remains defensible as defence-in-depth code (e.g. against a future
   `MinShantenToHu` revision that doesn't treat 3-of-a-kind as complete) but
   is not exercisable through realistic adjudicator output today.

### `HotSeatSwapTests.cs` — 3 facts on the swap surface

All three tests use `WebApplicationFactory<Program>` + in-memory raw WS
clients (same scaffold as `SpectatorModeTests`), and assert on the runtime
snapshot via `IChangshaGameRuntime.TryGetSnapshot` + the connection
manager's `GetRuntimeGameIdBoundTo` test hook.

1. **`HotSeatSwap_PlayerToPlayer_PreservesGameState`** — ws#1 joins
   `?seat=0` + sends seats UPDATE to take seat 0. Wait for runtime binding;
   record runtimeGameId. Disconnect ws#1. ws#2 joins same `gameId`
   `?seat=1` + takes seat 1. Assertions: (a) `GetRuntimeGameIdBoundTo`
   returns the SAME runtimeGameId for both connections (binding survives),
   (b) `state.Seats[1].PlayerId == ws#2.PlayerId`, (c) `state.Seats[0]`
   still carries ws#1's playerId (current backend contract — orphan
   binding documented in the class-level docstring).

2. **`HotSeatSwap_PlayerToSpectator_DoesNotClaimSeat`** — ws#1 takes seat
   0, disconnects, ws#2 joins as spectator (`?seat=-1`, no seat-take).
   Assertions: (a) runtime binding survives, (b) the spectator's playerId
   appears in NO seat (neither in the snapshot's `seats` entries nor in
   `state.Seats[i].PlayerId` for any i), (c) Alice's prior seat-0 binding
   is preserved. **Caveat:** the directive's "frees seat" wording is
   reframed — the autotable WS path does NOT call
   `ChangshaGameRuntime.HandleDisconnectAsync` (only the SignalR Hub
   does), so seats are not released on autotable disconnect. The
   bundle UI works around this by disabling the current seat in Hicks's
   picker. If a future wave promotes seat-release to the autotable
   disconnect path, this fact's seat-0-preserved assertion will flip.

3. **`HotSeatSwap_SpectatorToPlayer_BindsSeat`** — ws#1 joins as spectator
   (no seat-take, may or may not trigger a runtime binding depending on
   future eager-bind tweaks), disconnects, ws#2 joins `?seat=2` + takes
   seat 2. Assertions: (a) `state.Seats[2].PlayerId == ws#2.PlayerId`, (b)
   the spectator's prior playerId is in no seat. Test is written to be
   neutral re: whether the spectator's JOIN eagerly bound a runtime — both
   outcomes are consistent with the final seat-2 state.

## Surprises

- **The Kong-over-Pung tie-breaker is theoretically dead code today.**
  Bishop's `ClaimAcceptanceRank` lifts Kong (rank 3) above Pung (rank 2),
  but I cannot construct a fixture where both Kong and Pung **both strictly
  drop shanten** because the shanten counter already counts any concealed
  3-of-a-kind as a complete group. Kong from discard requires 3 in hand →
  that "concealed pung group" is "moved" to a declared meld with zero net
  structural gain. Pung from the same 3 concealed copies removes 2 (leaving
  a dangler) → typically the same or worse shanten. The lift remains
  defensible as defence-in-depth (and Bishop's explicit ordering matches
  the runtime's CCW seat-distance preference rationale per his memo
  §"Acceptance rank"), but the Phase J Wave 1 acceptance gate cannot
  exercise it through realistic adjudicator output. Pinned the same
  tie-breaker mechanism via Pung-vs-Chow instead.

- **`HandEvaluator.MinShantenToHu` clamps at zero.** A "winning" hand (e.g.
  the canonical `ThirteenTileWaitingForWan1` shape) reports shanten=0. So
  fixtures that rely on observing "shanten raises after a claim" need to
  start at shanten ≥ 1 — I used SevenPairs-leaning 5-pair shapes for the
  "raises" path because pung breaks SevenPairs deterministically (any
  declared meld disqualifies it, per `HandEvaluator.cs:283`).

- **Autotable disconnect does NOT release runtime seats.** This is the
  largest backend gap the hot-seat swap surface bumps into, and the reason
  my Test 2 is reframed to "DoesNotClaimSeat" instead of "FreesSeat". The
  bundle UI works around it by disabling the current seat in the picker,
  but a backend wave to wire `_runtime.HandleDisconnectAsync` from the
  autotable disconnect path would close the loop cleanly. Recommend a
  Phase J Wave 2 brief if this comes up again.

## Nothing skipped

All 7 new facts run unconditionally (no `Skip = `). Bishop's gate is shipped
and stable, Hicks's UI changes are frontend-only and don't gate any of my
test fixtures.

## Blind spots / future work

- **Bishop's chow simulation** (`ShantenAfterChowClaim`) is exercised
  indirectly through Fact 4 (Pung-vs-Chow tie-breaker) — the chow branch
  succeeds, drops shanten, and is correctly tie-broken by Pung. A targeted
  fact pinning chow's lowest-rank-first pattern selection (e.g. construct a
  fixture where the lowest-rank chow pattern is structurally worse than a
  higher-rank one, prove the gate uses the lowest pattern and refuses)
  would close a small gap but is not required by the wave brief.
- **Wall-exhaustion fast path** (Bishop's Task 2) was deferred as no-op per
  his memo §"What I deferred"; no test added. If a future wave reopens
  this, the existing `WallExhaustionTests.cs` family already covers the
  current state-machine short-circuit; the new wave would need a test
  pinning the runtime-driver path explicitly.
- **Seat release on autotable disconnect** is the largest test/backend
  contract gap touched by this wave. Documented in Test 2's class-level
  docstring and in "Surprises" above.

**Memo:** this file (`.squad/decisions/inbox/vasquez-phase-j-wave-1.md`).
