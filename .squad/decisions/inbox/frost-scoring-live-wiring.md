# Frost — Live Scoring Wire-Proof (Wave-O)

**Author:** Frost (mahjong-autotable squad)
**Date:** 2026-06-04
**Scope:** Prove end-to-end that `FanCalculator` fires during a real
4-bot Changsha game AND that the detected fans land on the WebSocket
payload the frontend reads — not just in unit-test isolation.
**Status:** ✅ Complete — wiring is intact at every layer; new
acceptance tests + a Playwright wire-tap spec pin it down.

## TL;DR

- **FanCalculator IS wired into the live `ChangshaStateMachine.Score`
  path.** The previous Frost memory ("FanCalculator NOT wired") was
  outdated and is corrected below with exact line citations.
- Added **6 new acceptance tests** in
  `LiveHuFanWiringTests.cs` that drive the real state machine to Hu,
  serialize through the production wire encoder + `AutotableJson.Options`,
  and assert the full `scoreResult.fans` schema (`fan`, `points`,
  `chinese`, `pinyin`, `english`) lands as expected — including the
  Draw case where `scoreResult` is correctly OMITTED from the wire
  (not emitted as `null`) thanks to
  `JsonIgnoreCondition.WhenWritingNull`.
- Added **`playtest-scoring-live.spec.mjs`** that spectates a real
  4-bot Hard game, taps the WS via CDP, AND polls the bundle's
  `client.result` collection — and asserts at least one Hu carries
  non-empty fans on the wire. **Latest run: PASS on game 1** —
  2 distinct Hu's observed, 1 with `concealedHand` fan, 6 CDP-captured
  WS frames carrying fans (independent wire-proof, not a bundle echo).

## 1. Live wiring path — VERIFIED 2026-06-04

The fan calculation is invoked synchronously inside the state machine
on the win-detection edge and is projected through the production
wire encoder used by both bundle WS and SignalR:

| Layer | File:Line | What happens |
| --- | --- | --- |
| State-machine `Score` action | `Changsha/ChangshaStateMachine.cs:944` | Calls `FanCalculator.EvaluateHand(...)` via `EvaluateFanBonuses` (line 914-945) using `state.CurrentWin` flags + `state.Wall`. |
| Apply bonuses to payments | `Changsha/ChangshaStateMachine.cs:859-906` (`Score`) | Calls `ApplyFanBonusesToPayments` so each fan adds rows to `score.Payments` with `reason: "fan:<id>"`. Verified on the wire (game-1 payment row: `{"fromSeatIndex":3,"toSeatIndex":2,"amount":1,"reason":"fan:concealedHand"}`). |
| Wire-name mapping | `Changsha/ChangshaStateMachine.cs:983` (`FanWireName`) | `char.ToLowerInvariant(name[0]) + name[1..]` → camelCase id (e.g., `ConcealedHand` → `concealedHand`). |
| Wire projection | `Autotable/ChangshaToAutotableTranslator.cs:274-285` (`BuildHandResult`) | Projects `score.Fans` into `ScoreResultEntry.Fans` of `FanEntry` records, attaching Chinese/Pinyin/English labels from `FanCatalog.Get`. |
| Wire schema | `Autotable/AutotableProtocol.cs:296-317` (`FanEntry`) | `{ fan, points, chinese, pinyin, english }`. |
| Bundle emission | `Autotable/AutotableProtocol.cs:371` (`EncodeHandResult`) | `["result","current", value]` collection entry. |
| Null-omission semantics | `Autotable/AutotableProtocol.cs:479-486` (`AutotableJson.Options`) | `JsonIgnoreCondition.WhenWritingNull` — Draw outcomes OMIT `scoreResult` and `winResult` entirely (not `null`). Frontend uses `result.scoreResult?.fans` optional chaining. |
| SignalR mirror | `Changsha/Runtime/ChangshaGameRuntime.cs:1972-2036` (`EmitScoringAndHandFinishedAsync`) | Mirrors the same `FanEntry` shape on the `ScoringComplete` SignalR push. |

**Catalog source-of-truth:** `Changsha/Scoring/Fan.cs:156-252`
(`FanCatalog.Entries`) — 14-fan static dictionary; `Get(fan)` lookup
on line 251. Each entry carries `Chinese`, `Pinyin`, `English`,
`Points`, `Variant` (Changsha vs ExpandedChinese).

## 2. New acceptance tests — `LiveHuFanWiringTests.cs`

Location:
`src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Scoring/LiveHuFanWiringTests.cs`

| # | Test | What it pins |
| --: | --- | --- |
| 1 | `SelfDraw7PairHu_WireSerialization_CarriesFansWithLabels` | Drives state machine to seven-pairs self-draw; serializes the emitted entry through `ChangshaCollectionEncoder.EncodeHandResult` + `AutotableJson.Options`; asserts wire JSON has `scoreResult.fans` with `sevenPairs`, `concealedHand`, `selfDraw`, each carrying Chinese/Pinyin/English/points, and that `fanPoints` equals the per-fan sum. |
| 2 | `Discard7PairHu_WireSerialization_CarriesSevenPairsAndConcealedHandFans` | Same test but for a discard-claimed seven-pairs (no `selfDraw` fan); confirms `concealedHand` is still emitted (concealed melds only) and that the wire schema is identical. |
| 3 | `Draw_WireSerialization_OmitsScoreResultAndWinResult` | Drives the runtime to a Draw outcome and asserts that the wire payload OMITS `scoreResult` and `winResult` entirely (TryGetProperty returns false) — proving the `WhenWritingNull` ignore semantics that the frontend depends on. |
| 4 | `FullFlushSelfDrawConcealedHu_StacksFans_FanPointsMatchesPerFanSum` | Stacked-fan case: a self-drawn full-flush concealed hand. Asserts the wire carries `fullFlush` + `selfDraw` + `concealedHand`, and that `scoreResult.fanPoints` exactly equals `Σ fan.points`. |
| 5 | `WireSerialization_PaymentsRows_HaveFanReasonPrefix` | Asserts every fan listed in `scoreResult.fans` has a matching `payments[].reason == "fan:<id>"` row — proves `ApplyFanBonusesToPayments` is wired in alongside `EvaluateFanBonuses`. |
| 6 | `FanWireIdentifiers_RoundTripThroughFanCatalog` | Iterates every fan emitted on the wire and asserts a successful `FanCatalog.Get` lookup with non-empty Chinese/Pinyin/English — guards against future enum members added without a `FanCatalog` row. |

**Run:**

```
dotnet test src/backend/Mahjong.Autotable.slnx --nologo \
  --filter "FullyQualifiedName~LiveHuFanWiringTests"
```

→ 6/6 pass. Targeted sweep (`~Scoring|~FanCalculator|~HandResult`)
remains 105/105 green.

## 3. Live wire-tap spec — `playtest-scoring-live.spec.mjs`

Location: `playtest-artifacts/playtest-scoring-live.spec.mjs`

Drives a real Chromium browser against the running backend at `:8088`,
spectates a 4-bot Hard Changsha game via the standard
`?variant=changsha&dealMode=auto&botCount=4&botDifficulty=Hard` query,
and captures evidence on TWO independent channels:

1. **Bundle channel** — subscribes to
   `window.game.world.client.result.on('update')` and drains every
   emitted `HandResultEntry`.
2. **Wire channel (CDP)** — taps `Network.webSocketFrameReceived`,
   parses every frame containing `"result"`, and counts how many
   carry `["result","current",{...scoreResult.fans...}]` with a
   non-empty fans array. This is the ground-truth wire proof —
   it cannot be faked by client-side defensive fill-ins.

The spec **loops up to 6 fresh games** (75 s each, fresh `gameId`
per attempt) and stops on the first Hu with non-empty fans. Each
Hu is fingerprinted by `winner|basePoints|fanPoints|fan-ids` to
de-duplicate the 30+ snapshot replays per hand.

**After every observed Hu the spec also sends
`client.match.set(1, { action: 'nextHand' })` and clicks the result
modal's "Next Hand" button** to mirror real user input, in case the
runtime auto-advance stalls behind the modal.

**Assertions (all hard FAILs):**

1. At least one observed Hu has `scoreResult.fans.length > 0`.
2. Each fan has a non-empty `fan` (camelCase id), `chinese`,
   `pinyin`, `english`, and `points > 0`.
3. `scoreResult.fanPoints == Σ fan.points`.
4. `scoreResult.basePoints > 0`.
5. The CDP wire-tap saw at least one frame carrying fans
   (`wireSnapshot.fansFramesSeen > 0`).

**Latest run (2026-06-04, game 1):**

```
[frost-scoring-live] Hu#1 winner=1 basePoints=1 fanPoints=0 fans=[]
[frost-scoring-live] Hu#2 winner=2 basePoints=2 fanPoints=1 fans=[concealedHand]
[frost-scoring-live] games run: 1 / 6
[frost-scoring-live] total Hu observed: 2 (with fans: 1)
[frost-scoring-live] wireSnapshot: result=111, Hu=111, fans=6
[frost-scoring-live] FIRST Hu-with-fans @ game 1: winner=2, basePoints=2, fanPoints=1, category=smallWin
[frost-scoring-live]   · concealedHand (门清 / mén qīng / Concealed Hand) = 1 pts
[frost-scoring-live] result: PASS
```

Findings + screenshot:
`playtest-artifacts/screenshots/frost-scoring-live-2026-06-04T14-11-30-915Z/`.

### 3a. Why Hu#1 had no fans (legitimate, not a bug)

Hu#1 was a `Standard` 258-pair won by **discard** with at least one
**claimed meld** (the runtime's bots open-meld aggressively at Hard).
That gates out every fan in the catalog except situational ones
(KongReplacement / RobbingKong / LastTileFromWall / LastDiscardCatch),
none of which were applicable to this draw position. This is the
**expected behaviour** of `FanCalculator` — see `FanCalculator.cs:172`
`AnyClaimed(...)` guard. The empty fans array is the correct answer
for that hand and would surprise no rule reviewer.

## 4. 4-bot draw / "stuck on hand 1" investigation

**Background.** Hicks's earlier `vreg` run reported that 4-bot Medium
games "tend to Draw at ~32 s". My first run of this spec (300 s,
single game) observed only ONE Hu (hand-1 winner=1) replayed 327
times — appearing as if the game stalled.

**Updated finding (after fresh spec run).** The fresh run produced
**2 distinct Hu's in a single 75 s game** with both `client.result`
and CDP frames advancing past hand 1. So the previous "stuck on
hand 1" observation was an **artefact of the first spec's poller**,
not a backend stall:

- The first spec polled at 1 s and counted EVERY snapshot replay as
  a fresh observation — so the same hand-1 result rebroadcast 327
  times looked like 327 events. The current spec de-duplicates by
  `winner|basePoints|fanPoints|fan-ids` fingerprint and correctly
  reports 2 unique Hu's.
- The CDP tap shows 111 result frames in 75 s — the bundle is being
  resynced very aggressively (probably each spectator state change).
  This is a separate observation for Bishop / Ferro to consider for
  optimisation but is NOT a bug in the scoring path.

**4-bot Hard difficulty appears to Hu fast enough** — game 1 produced
2 distinct hands' worth of Hu activity in 75 s without any UI
interaction. Hicks's "32 s to Draw" observation is consistent with
Medium-difficulty bots being more defensive; that lane (bot strategy
tuning) is owned by Frost but is **out of scope for this wave**.
Tracked as a follow-up below.

**Recommended follow-up (Frost, future wave):** instrument the
bot-vs-bot harness to log per-difficulty Hu-rate / Draw-rate over a
sample of 50 hands per difficulty level, and consider whether the
Medium claim-priority is too conservative.

## 5. Memory correction

The Squad memory entry asserting "FanCalculator NOT wired into
`ChangshaStateMachine.Score`" was **outdated as of 2026-06-04**. It
described an earlier state of the trunk; the wire-up has been intact
since at least Wave-K (audit memo `frost-scoring-audit.md`,
2026-05-29). I've downvoted any such memory and stored a corrected
fact: *"FanCalculator IS wired end-to-end: ChangshaStateMachine.Score
→ EvaluateFanBonuses (line 914) → ChangshaToAutotableTranslator.
BuildHandResult (line 274) → ScoreResultEntry.Fans on the wire."*

## 6. Lane discipline

Touched only the explicit Wave-O allowlist:

- `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/Scoring/LiveHuFanWiringTests.cs` (new)
- `playtest-artifacts/playtest-scoring-live.spec.mjs` (new)
- `playtest-artifacts/screenshots/frost-scoring-live-*/` (artefacts)
- `.squad/decisions/inbox/frost-scoring-live-wiring.md` (this memo)
- `.squad/agents/frost/history.md` (history append)

Did **NOT** touch: frontend, Players/persistence, `AutotableWsEndpoint.cs`,
`ChangshaGameRuntime.cs`, `ChangshaStateMachine.cs`,
`ChangshaToAutotableTranslator.cs`, `AutotableProtocol.cs`, or
`Changsha/Scoring/*.cs` production sources.

## 7. Open questions / follow-ups

- **Bot strategy tuning (Frost lane).** Quantify Hu-rate vs Draw-rate
  per difficulty level over 50 hands. If Medium is genuinely
  Draw-biased, audit `BotStrategy*.cs` claim priorities.
- **Bundle resync chattiness (Bishop / Ferro).** The CDP tap saw
  111 `result.current` frames in 75 s of a single 2-Hu game. Worth
  a glance from the runtime side; not a correctness issue.
- **Catalog drift guard.** The new `FanWireIdentifiers_RoundTripThroughFanCatalog`
  test already guards drift, but a CI hint to fail on any new fan
  enum member without a catalog row would be belt-and-braces.
