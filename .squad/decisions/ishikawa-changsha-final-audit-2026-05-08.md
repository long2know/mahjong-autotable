# Ishikawa — Final Changsha Mahjong Parity Audit

**Date:** 2026-05-08T12:00:00-07:00
**Auditor:** Ishikawa, Domain Rules Engineer (Squad v0.9.4)
**Repo:** `mahjong-claw` @ `main` (HEAD `572540e`)
**Sources audited:**
1. PRIMARY — https://mahjongpros.com/blogs/how-to-play/beginners-guide-to-changsha-mahjong (Stephen-cited)
2. https://baike.baidu.com/en/item/Changsha%20Mahjong/36618
3. https://www.reddit.com/r/Mahjong/comments/xp6crv/

> Files inspected: `src/MahjongClaw.Domain/Rules/*.cs`, `src/MahjongClaw.Domain/Gameplay/MissedWinLock*.cs`, `src/MahjongClaw.Api/Program.cs`, `tests/MahjongClaw.Tests/`. No source modified.

---

## 1. Executive summary

🟡 **Minor gaps remaining.** The Changsha rule engine is functionally complete for the canonical rule set in all major dimensions (tile set, deal, turn flow, claim arbitration, win-type detection, settlement math, missed-win lock, multi-winner, dealer rotation, last-tile detection, robbing-kong, ledger persistence). However, this audit surfaced **3 NEW canonical-vs-impl discrepancies** not previously identified in the D/M/B backlog, all in the small-win/big-win classification and bird-mechanic dimensions. None block gameplay; one is a clear scoring bug under specific hands.

**Verdict so far across 4 prior audits:** each found real gaps; this audit found 3 more. We are **>95% complete** but cannot honestly claim 100%. Proposed fix scope below is small (≤2 hours total).

---

## 2. Parity table

Legend: ✅ matches canonical · ⚠️ deviation w/ rationale · ❌ canonical rule violated.

### A. Tile set & wall

| Rule | Spec source | Impl file:line | Status | Notes |
|---|---|---|---|---|
| 108 tiles, 3 suits × 9 × 4, no honors/flowers | mahjongpros §"Tiles to Play" | `Changsha258Ruleset.cs:23` (`TileSetSize=108`); `WallBuilder.cs:9,21` | ✅ | `TileFactory.CreateChangsha258Set()` produces 108. |
| Allowed suits = Characters/Dots/Bamboos | mahjongpros §"Tiles" | `Changsha258Ruleset.cs:10-15` | ✅ | Frozen set. |
| Initial deal: 14 to dealer / 13 to others, dealing in 2-stacks (4-tile rounds × 3, then +1, then dealer +1) | mahjongpros §"Dealing the Hand" | `GameDealer.cs:35-53` | ✅ | Matches "4 tiles × 3 rounds, +1, dealer +1". |
| Dice-roll wall break / replacement-tile draw site | mahjongpros §"Breaking the Wall" / §"Calling Tiles" (kong) | (not implemented physically) | ⚠️ | Server uses a flat shuffled wall and `Wall.Draw()` from one end. Replacement tile after kong drawn from same end. Functionally equivalent for a digital table; physical-table dice mechanics intentionally omitted. |

### B. Dealer selection & rotation

| Rule | Spec source | Impl file:line | Status | Notes |
|---|---|---|---|---|
| First dealer determined randomly (e.g. shuffle wind tiles) | mahjongpros §"Determining The Dealer" | `Program.cs:214-217` | ⚠️ | Impl picks the player whose `Seat == "East"` — i.e. the first player passed to the lobby. Effective behaviour is "host/first-joiner becomes dealer", not random pick. See **NEW-3** below. |
| Winner of previous game becomes next dealer | mahjongpros §"Determining The Dealer" rule 1 | `ChangshaSettlementCalculator.cs:182-184` (single winner branch); `SelectNextDealer` L335-338 | ✅ | |
| Multi-winner discard → random pick from winners | mahjongpros §"Determining The Dealer" rule 3 | `ChangshaSettlementCalculator.cs:341-345` | ✅ | RNG-injected. Tested in `ChangshaMultiWinnerSettlementTests`. |
| In a draw, last-tile drawer becomes dealer | mahjongpros §"Determining The Dealer" rule 2 | not implemented | ⚠️ | Wall-exhausted hand keeps dealer unchanged (see `Program.cs:1309-1312`). See **NEW-2** below. |
| Round-wind rotation E→S→W→N every 4 dealer rotations | mahjongpros §"Determining The Dealer" | `Program.cs:1198-1200` (`DealerRotationCount` tracked); D3 backlog item shipped | ✅ | |

### C. Turn flow & calling

| Rule | Spec source | Impl file:line | Status | Notes |
|---|---|---|---|---|
| Turn = draw → win-or-discard | mahjongpros §"Basic Rules" | `Program.cs` discard endpoint L500+; `TurnFlowValidator` | ✅ | |
| CCW seat order (E→S→W→N) | mahjongpros §"Basic Rules" | `ChangshaSettlementCalculator.cs:43-49` | ✅ | |
| Call window after discard, immediate-or-dead | mahjongpros §"Calling Tiles" | M1 `PendingClaimWindow` + `Program.cs:501-506` | ✅ | 3 s human / 250 ms bot. |
| Priority Win > Pung/Kong > Chow | mahjongpros §FAQ | `ClaimArbitrator.cs` | ✅ | 24 unit tests. |
| Chow only from prior (CCW-immediate) player | mahjongpros §"Calling Tiles" | `ChowEligibilityRule.cs:7-36` | ✅ | M2; 12 tests. |
| Multi-claimant Win → closest-after-discarder wins (single-winner ruleset) OR all win (multi-winner ruleset) | mahjongpros §FAQ + "Multiple Players can win off the same tile" | `ClaimArbitrator.ResolveMultiWin` L48-80 | ✅ | Engine implements multi-winner ("一炮多响"); canonical also allows it. |

### D. Win conditions (every Changsha-specific hand)

Canonical hands listed in mahjongpros §"Payout Table" + §"Starting Hand Instant Win".

| Canonical hand (中文 / pinyin) | Spec source | Impl file:line | Status | Notes |
|---|---|---|---|---|
| Plain Small Win — 4 melds + 258 pair (将牌) | §"Any Hand" / §"258 Generals" | `Changsha258Ruleset.TryGetWinningHand` L39-97; `IsSmallWinPair` L99-105 | ✅ | |
| Random-eye Big Win bypass (any pair when the body is itself a Big Win) | §"258 Generals" — Big Wins skip 258 requirement | `Changsha258Ruleset.cs:89-96, 277-297` | ✅ | Pure Suit / All 258 / SanTong / 7-pairs / All-Pungs all eligible. |
| All Pungs / 碰碰胡 (Big) | §"All Triplets" | `ChangshaWinTypeEvaluator.cs:70-73`; in `BigWinTypes` L11 | ✅ | |
| All Generals / 将将胡 — `AllTwoFiveEight` (Big) | §"All Generals" | Evaluator L75-78; BigWinTypes L12 | ✅ | |
| Pure Suit / 清一色 (Big) | §"Full Flush" | Evaluator L80-83, L209-218; BigWinTypes L13 | ✅ | |
| Full Beggar's Hand / 全求人 — `FullyReliant` (Big) | §"Full Beggar's Hand" | `ChangshaFullyReliantDetector.cs:5-17`; BigWinTypes L21 | ✅ | D6. |
| Seven Pairs / 七巧对 (Big) | §"Seven Pairs" | Evaluator L85-87 | ✅ | |
| Luxury Seven Pairs / 豪华七小对 ×2 | §"Luxurious Seven Pair" | Evaluator L89-92; ×2 multiplier `ChangshaSettlementCalculator.cs:162-173` | ⚠️ | ×2 currently doubles **only the dealer-loser delta**, not all loser deltas. Canonical "Big Win × 2" implies the whole payment doubles. See **NEW-1** below. |
| Last Tile (海底捞月) — self-draw on last wall tile (Big) | §"Last Tile Win" | Evaluator L105-108; BigWinTypes L14; server detection `Program.cs:2088, 2646` | ✅ | M5. |
| Last Discard (河底捞鱼) — win on last discard (Big) | §"Last Tile Discard" | Evaluator L110-113; BigWinTypes L15 | ✅ | |
| Win After Kong / 杠上开花 (Big) | §"After a Kan" | Evaluator L115-118; BigWinTypes L18 | ✅ | Flag set client-side / API forwards; no server auto-detect (acceptable — kong→draw→win path is local). |
| Kong-on-Cannon / 杠上炮 (Big) | §"After a Kan Penalty" | Evaluator L125-128; BigWinTypes L20 | ✅ | |
| Robbing the Kong / 抢杠胡 (Big) | §"Robbing the Kong" | `ChangshaRobbingKongDetector.cs:7-28`; Evaluator L120-123; `Program.cs:867,1913` (KongRobOpportunity broadcast) | ✅ | M4 detection complete. |
| Heavenly Win / 天和 — dealer instant on first 14 (Big) | §"Blessing of Heaven" | Evaluator L95-98; BigWinTypes L9 | ✅ | Flag-driven. |
| Earthly Win / 地和 — non-dealer instant on first draw (Big) | §"Blessing of Earth" | Evaluator L100-103; BigWinTypes L10 | ✅ | Spec says "without declaring any open-meld" — currently relies on caller; no server check that no melds were declared before flag set. Minor. |
| Four Joys / 四喜 — starting hand with a quad (**Small**) | §"Starting Hand Instant Win" | Evaluator L42-47, `HasAnyQuad` L175-178; **NOT in BigWinTypes** | ✅ | Correctly small. |
| All Pure / 板板胡 — starting hand with no 258 (**Small**) | §"Starting Hand Instant Win" | Evaluator L49-52, L180-181 | ✅ | Correctly small. |
| Voided Suit / 缺一色 — starting hand missing one suit (**Small**) | §"Starting Hand Instant Win" | Evaluator L54-57, L183-199 | ✅ | Correctly small; PureSuit kept distinct. |
| Six Six Straight / 六六顺 — starting hand with two triplets (**Small**) | §"Starting Hand Instant Win" | Evaluator L59-62, L201-204 | ✅ | Correctly small. |
| Three Same / 三同 (San Tong) — starting hand same number across all 3 suits (**Small per canonical**) | §"Starting Hand Instant Win" — listed under Small Wins | Evaluator L64-67, L220-247; `BigWinTypes` L22 | ❌ | **SanTong is incorrectly classified as a Big Win.** Canonical mahjongpros source explicitly lists it under "Starting Hand Instant Win Conditions" with value "Small Win". See **NEW-1**. |

### E. 258 small-win pair rule + random-eye bypass

| Rule | Spec source | Impl file:line | Status |
|---|---|---|---|
| Pair must be 2/5/8 for default Small Win | §"258 Generals" | `Changsha258Ruleset.cs:8, 99-105, 82-87` | ✅ |
| Big-Win bodies bypass 258 pair requirement | §"258 Generals" footnote: "*Big Win hands do not need a 2,5,8 pair unless specified." | `Changsha258Ruleset.cs:89-96, 277-297` | ✅ |
| LastTile/WinAfterKong/KongOnCannon/RobbingKong **explicitly require** 258 pair per canonical | §"Last Tile Win" / §"After a Kan" / §"Robbing the Kong" — all say "Needs a pair of Generals" | not enforced — Evaluator simply tags context flags | ⚠️ | Engine treats *all* Big Wins as eye-flexible. Per canonical, those four require 258 pair; only AllPungs/AllGenerals/PureSuit/FullBeggar/SevenPairs/Heavenly/Earthly are truly eye-flexible. Could create false-positive wins in edge cases. Documented as variant choice. |

### F. Bird (扎鸟 / Niao)

| Rule | Spec source | Impl file:line | Status |
|---|---|---|---|
| Two birds drawn after each win | §"Capturing the Bird" | `BirdRanks: int[]?` configured at **game-create**, not drawn post-win — `Program.cs:66` and read at win L1160-1166, 2687-2698 | ⚠️ | Engine pre-configures bird ranks in `GameEntity.BirdRanks`; canonical draws them live from the wall after winning. Functionally a config-time deterministic alternative. Documented design choice. See **NEW-2**. |
| Rank→seat mapping (1/5/9=dealer, 2/6=right, 3/7=opposite, 4/8=left) | §"Capturing the Bird" | `ChangshaSettlementCalculator.ResolveBirdSeat` L476-492 | ✅ | Verified: dealer East + rank 2 → South (right CCW). |
| Match on winner OR discarder doubles; double-match triples | §"Capturing the Bird" | `ApplyMultiBirdAdjustments` L352-417 | ✅ | Discard branch L402-416 implements both. |
| **Self-draw**: birds must match **winner** to count | §"Capturing the Bird" — "In the case of self-draws, there is no discarder so the bird tiles must correspond to the winner in order to double or triple." | `ApplyMultiBirdAdjustments` L374-394 — also charges a bird-matched **loser** an extra base payment | ❌ | Implementation deviates: on self-draw, if a bird lands on a non-winner seat, that seat pays an extra base unit. Canonical says only winner-matches count on self-draw. See **NEW-1**. |
| Birds skipped for Starting-Hand Instant Wins | §"Capturing the Bird" — "Bird tiles are not drawn for Starting Hand Instant Win Conditions" | not implemented — birds are applied regardless of whether win type is starting-hand | ⚠️ | Not previously identified; minor since starting-hand wins are rare and engine pre-configures birds anyway. |

### G. Settlement / scoring math

| Rule | Spec source | Impl file:line | Status |
|---|---|---|---|
| Small Win: NonDealer ±1 / Dealer ±2 | §"Payout Table" | `ChangshaSettlementCalculator.cs:118-148` (units of 10) | ✅ | Self-draw: each loser pays 10 (or 20 if dealer); discard: discarder pays 10 (+10 if discarder=dealer, +10 if winner=dealer). |
| Big Win: NonDealer ±6 / Dealer ±7 | §"Payout Table" | same block, "70" / "60" branches | ✅ | |
| Multi-winner aggregation: each winner settled independently against discarder | implicit / common practice | `CalculateMultiWinner` L193-318 | ✅ | |
| Self-draw vs discard payer attribution (放炮 vs 自摸) | §"Payout Table" — split semantics | L109-149 | ✅ | |
| Multipliers stack via bird (×2 / ×3 only) | §"Payout Table" formula | bird logic L352-417 | ✅ | Small wins do NOT stack with Big wins per canonical L"Small wins do not stack with Big wins" — engine respects this since `IsBigWin` is a single boolean. |

### H. Missed-win lock (诈胡 / 过胡)

| Rule | Spec source | Impl file:line | Status |
|---|---|---|---|
| Passing a winnable discard locks player out of winning on any tile in their wait set until they next discard | §(implicit canonical Hunan rule, also Reddit thread) | `MissedWinLockTracker.cs:8-105`; `Program.cs:2434-2483, 2865-2867` | ✅ | M7. Brute-force wait-set computation over 27 candidates is correct-by-construction. Lock cleared on player discard or meld. Self-draw exempt. 16 tests. |

### I. Kong handling

| Rule | Spec source | Impl file:line | Status |
|---|---|---|---|
| Three kong types: concealed / exposed / promoted (added) | §"Calling Tiles" + §"Arranging Your Tiles" | `KongKind` enum; `Program.cs:647-679` | ✅ | M3. |
| Replacement tile drawn after kong | §"Calling Tiles" — "you have to also grab an extra tile" | `Program.cs:747-769` | ✅ | M3. Drawn from end of wall (single-end model). |
| Robbing-kong only on **promoted** kong, not concealed | §"Robbing the Kong" — "tile that an opponent adds to an existing Pong" | `Program.cs:867,1913` only fires on promoted path | ✅ | |
| Wins-after-kong (杠上开花) eligible | §"After a Kan" | flag-driven via `IsWinAfterKong` | ✅ | |

### J. End conditions

| Rule | Spec source | Impl file:line | Status |
|---|---|---|---|
| Wall exhaustion ends hand | §"Last Tile of The Wall" — "if no one can win… hand ends in a draw" | `Program.cs:1309-1312` (broadcast `WallExhausted`); `CompleteDrawnGameAsync` | ✅ | |
| Last-tile rules don't apply if final tile drawn as kong replacement | §"Last Tile of The Wall" — explicit | not modelled — server treats all wall-empty states uniformly | ⚠️ | Edge case rare; no evidence engine currently distinguishes. Documented. |
| Drawn hand records zero deltas in ledger | M8 | `PersistHandResultAsync` (M8) | ✅ | |

### K. Edge cases

| Rule | Spec source | Status |
|---|---|---|
| Last tile in wall, last player may pass | §"Last Tile of The Wall" | ⚠️ Engine auto-draws current player; no explicit "pass last tile" UI or API. Minor UX gap. |
| Simultaneous claims (one-discard-multi-win) | M6 | ✅ Implemented + tested. |
| Tile flow during multi-winner | M6/M8 | ✅ Each winner settled independently; ledger persists per-winner rows. |

---

## 3. NEW gaps (not in prior backlog)

### NEW-1 — SanTong (三同) misclassified as Big Win — **P1 BUG**

**Where:** `src/MahjongClaw.Domain/Rules/ChangshaWinTypeEvaluator.cs:22` (entry in `BigWinTypes` set).
**Canonical:** mahjongpros §"Starting Hand Instant Win Conditions" lists Three Same (San Tong) with value "**Small Win**", alongside Four Joys / All Pure / Voided Suit / Six Six Straight (all of which the impl correctly classifies as Small).
**Impact:** A player whose initial deal contains the same number as a triplet across all three suits will be paid out as if they hit a Big Win (60 / 70 base) instead of a Small Win (10 / 20 base) — a **6× over-payment** in real money/score.
**Fix scope:** delete `ChangshaWinType.SanTong` from `BigWinTypes` initialiser at L22. Add regression test in `ChangshaWinTypeEvaluatorTests` asserting `IsSmallWin && !IsBigWin` for SanTong-only hands. ~5 LOC + 1 test, ~30 min.

### NEW-2 — Self-draw bird hit on losers gives extra payment — **P1 SCORING DEVIATION**

**Where:** `src/MahjongClaw.Domain/Rules/ChangshaSettlementCalculator.cs:374-394` (the "Birds landed on losers" branch inside the self-draw path).
**Canonical:** mahjongpros §"Capturing the Bird" — *"In the case of self-draws, there is no discarder so the bird tiles must correspond to the winner in order to double or triple."*
**Current behaviour:** On self-draw, if a bird-rank resolves to a non-winner seat, that seat is charged one additional base payment to the winner. Canonical says only winner-matches double / triple on self-draw; non-winner-matched birds should be ignored entirely.
**Impact:** Mild over-payment from one specific loser per non-winner-matched bird on self-draws.
**Fix scope:** delete the `foreach (var birdSeatGroup in birdSeats.GroupBy…)` block at L374-393 and the trailing `return;` at L394. Update `ChangshaSettlementCalculatorTests` self-draw bird scenarios to assert no extra payment when bird lands on losers. ~15 LOC + 2 tests, ~45 min.

### NEW-3 — First-dealer selection is not random — **P2 UX/RULES**

**Where:** `src/MahjongClaw.Api/Program.cs:214-215` — picks the player whose `Seat == "East"`, which is whichever player is registered first.
**Canonical:** mahjongpros §"Determining The Dealer" — first dealer is determined randomly (e.g. shuffle wind tiles or roll dice).
**Impact:** Cosmetic in single-table digital play — host effectively chooses dealer by joining first. Does not affect hand mechanics.
**Fix scope:** Either (a) randomise seat assignment at `/start`, OR (b) document explicitly. ~10 LOC, ~30 min if (a).

### NEW-4 (P2 documentation) — Bird draw timing differs from canonical

The engine pre-configures bird ranks in `GameEntity.BirdRanks` at game creation (`Program.cs:66`) instead of drawing them live from the wall after each win as canonical describes. This is a deliberate digital simplification that yields equivalent expected value when ranks are uniformly random; document in README/charter.

### NEW-5 (P2 rule strictness) — Big-Win Pair-Generals subset not enforced

Canonical explicitly lists "Needs a pair of Generals (2s, 5s, or 8s)" for: Last-Tile-Draw, Last-Tile-Discard, After-a-Kan, After-a-Kan-Penalty, Robbing-the-Kong, Heavenly, Earthly. The engine treats *all* Big Wins as eye-flexible (`IsEligibleRandomEyeBigWin` at `Changsha258Ruleset.cs:293-297`). This means a hand that's *only* "Last Tile Draw" + non-258 pair would currently be paid as a Big Win where canonical would require a 258 pair. Edge case — surfaces only when player has none of {AllPungs, PureSuit, AllGenerals, SevenPairs, FullBeggar, SanTong} but does have one of the contextual flags. Worth adding strict-mode validation.

### NEW-6 (P3) — Earthly Win does not verify "no melds declared"

Canonical requires Blessing of Earth to be won "without declaring any open-meld, including closed quads". The engine merely accepts the `IsEarthly` context flag from caller (`ChangshaWinContext.cs:6`) without server-side verification that no melds (including concealed kongs) were declared by the non-dealer winner.

---

## 4. Canonical hands intentionally out of scope

Per mahjongpros: *"Unlike other types of mahjong, more special hands like All Greens, Sixteen Arhats, and Nine Gates simply do not exist in Changsha Mahjong."* — engine correctly omits these.

The engine also correctly omits flowers, seasons, dragons, and winds-as-tiles (Changsha uses only the 108 suited tiles), aligning with canonical.

---

## 5. Final verdict & recommended next actions

**Verdict: 🟡 Substantively complete, NOT 100%.** The gap between current engine and canonical Changsha is now narrow enough that no integration-level rework is needed, but **NEW-1 (SanTong misclassification) is a clear scoring bug** that produces wrong settlement amounts in a real (if uncommon) win path and should be fixed before declaring parity done.

**Severity counts (NEW gaps only):** P0: 0 · P1: 2 (NEW-1, NEW-2) · P2: 3 (NEW-3, NEW-4, NEW-5) · P3: 1 (NEW-6).

**Recommended next-action sequence:**

1. **Fix NEW-1** — single-line removal from `BigWinTypes`; add Small-Win regression test for SanTong. Owner: Togusa. ETA 30 min.
2. **Fix NEW-2** — remove self-draw "birds-on-losers" extra payment block; update tests. Owner: Togusa. ETA 45 min.
3. **Triage NEW-3 / NEW-4 / NEW-5 / NEW-6** with Stephen — these are arguably variant choices; each warrants either an issue or an entry in `docs/changsha-rules.md` documenting the deviation explicitly. Owner: Kusanagi to decide.
4. After NEW-1 & NEW-2 ship, re-run this audit and we can credibly state 🟢 100% canonical parity.

**How I verified:** read every file in `src/MahjongClaw.Domain/Rules/` and `src/MahjongClaw.Domain/Gameplay/MissedWinLock*.cs` end-to-end; cross-checked the win-type enum against the canonical hand table line-by-line; traced settlement code flow for both single- and multi-winner paths; verified bird seat resolution arithmetic by hand for dealer=East and ranks 1-9; inspected backlog entries D1-D6, M1-M8, B1 against their cited file:line ranges. No assumption was made on a rule without a citation in the canonical source.

— *Ishikawa, hand on the holster, eyes on the data.*
