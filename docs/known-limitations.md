# Mahjong Autotable — Known Limitations (V1)

> Captured at Phase G GA (commit `730946c`). Updated as gaps close.

## Changelog

- **#117 (WP-B Scoring reconciliation)** — the live payment path is now
  **spec-pure §5.1 by default**. The Post-W23 fan-catalog folding + big-win
  stacking multiplier that had inflated payouts (a plain Standard-258 self-draw
  paid 2 instead of 1) are gated behind the opt-in `ChangshaScoringOptions.HouseRules`
  mode; the fan catalog + `AllPatterns` are still surfaced for display but are
  **query-only** wrt payments. See "Fan (番) catalog + big-win stacking" below and
  [spec §5.4](rules/changsha-spec.md#54-fan-catalog--big-win-stacking--query-only-non-scoring-in-v1).
- **Phase I Wave 4** — proper shanten counter + spectator seat **shipped**.
  `HandEvaluator.MinShantenToHu` is now a rigorous backtracking counter
  (Standard 4-groups+pair AND SevenPairs paths; returns the min). The
  "Bot shanten estimator is coarse" gap below is closed. `?seat=-1`
  spectator connections receive snapshots but never claim a seat, and
  pair with `?botCount=4` to trigger auto-deal for all-bots-watch mode.
- **Phase I Wave 3** — multi-game WS routing **shipped**.
  `AutotableWsEndpoint` honours `?gameId=X` and `JOIN.gameId` (was coerced to
  `changsha-default`). Per-gameId isolation is enforced for state, runtime
  binding, broadcasts, and lazy bot-spawn. Hydration filter also widens to
  skip `WallExhausted` (draw-terminal) rows.
- **Phase I Wave 2** — persistence-on-restart hydration **shipped**.
  `ChangshaGameRuntime.HydrateAsync` re-populates `_games` from the
  `ChangshaGames` table on process boot; non-terminal games survive a restart.

This page lists the V1 gaps a player or operator may notice during real play.
Each item links back to the canonical [Changsha rules spec](rules/changsha-spec.md)
section it diverges from and to the skipped backend test that pins the
behaviour. Items here are **known and accepted**, not bugs.

For the canonical rules baseline see [`docs/rules/changsha-spec.md`](rules/changsha-spec.md).

---

## Rules-engine limitations

### Chow tile-ID arbitration is coarse (first-valid wins)

When a player claims Chow on a discarded tile and their hand contains multiple
tile copies that could complete the run (e.g., two identical 4万 tiles when
the chow is 4万–5万–6万), V1 picks the **first matching tile in hand order**
rather than letting the claimer choose the exact tile-ID.

**Impact:** purely cosmetic in single-player; mildly observable in 3D view
where the wrong physical tile may be animated into the meld. Game state and
scoring are unaffected (logical tiles are identical).

- Spec: [§3.2 Claiming Discards](rules/changsha-spec.md#32-claiming-discards), [§3.3 Claim Priority](rules/changsha-spec.md#33-claim-priority).
- Implementation: `Changsha/ClaimAdjudicator.cs`.
- Deferred to: Phase I+ (no skipped test yet — tracked here as a
  documentation-only limitation).

---

### 过胡 (Pass-Hu) lockout is seat-level, not tile-specific

Per the canonical rule (spec §3.6), a seat that passes on a winning discard
is forbidden from declaring Hu **until their next own draw**. V1 implements
the decay correctly per-draw (`ChangshaStateMachine.cs` — `DrawTile` removes
the active seat from `MissedWinSeats`), and clears the lockout every new hand.

Two nuances are flagged (both are known + accepted, pending product direction):

1. **Seat-level vs. tile-specific (§3.6 self-contradiction).** `MissedWinSeats`
   is a `HashSet<int>` of *seats*, so a flagged seat is blocked from Hu on **any**
   tile until their next draw — including a *different* winning tile that §3.6's
   "only applies to the specific tile" clause would allow. See the surfaced open
   question in [spec §3.6](rules/changsha-spec.md#36-missed-win-rule-过胡--过水).
2. **Pung/Kong does not reset the lockout** — only an actual self-draw does
   (spec-correct per Baidu §过水 "until your next draw"; some regional variants
   reset on any active turn).

**Impact:** subtle — affects the minority of hands with a multi-tile wait or a
chain of claims before the offending seat would otherwise draw.

- Spec: [§3.6 Missed Win Rule (过胡 / 过水)](rules/changsha-spec.md#36-missed-win-rule-过胡--过水).
- Implementation: `Changsha/ChangshaStateMachine.cs` — `MissedWinSeats` set
  and its lifecycle.
- Pinned by: `MissedWinTests`, `MissedWinTileSpecificityCharacterizationTests`
  (blocks-different-tile + decay-on-draw characterization).
- Deferred to: V2 (only if Stephen rules tile-specific / regional variant takes precedence).

---

### Hard-tier bot falls back to Medium under time pressure

The Hard strategy uses an EV-aware depth-2 lookahead which can blow past
typical interactive timing budgets on dense hands. Phase H Wave 1 adds an
explicit `BotDecisionTimeoutMs` (default 2000ms — see Phase H design memo
§1.2). When a Hard-tier decision exceeds it, the runtime falls back to a
safe deterministic action (`ChangshaBotPolicy.SelectDiscardTile` for own-turn,
`Pass` for claim windows).

**Impact:** during contested hands, a Hard bot may transparently play like a
Medium bot for that decision. Game continues; no error surfaced to the user.

- Implementation (Wave 1): `Changsha/Runtime/ChangshaRuntimeOptions.cs`
  (`BotDecisionTimeoutMs`), `Changsha/Bot/ChangshaBotEngine.cs`
  (`InvokeWithTimeout`).
- Pinned by skipped test: `BotBehaviorTests.cs:141`
  (`Bot_TimeoutFallback_DeferredV2`) — un-skipped in Wave 1.

---

## Patterns & scoring — shipped since GA + V2 gaps

The V1 win detector covers Standard (258 pair), Seven Pairs (七对子), All Pungs
(碰碰胡), Full Flush (清一色), and Nine Terminals (九幺). Several items previously
listed here as "deferred" have since **shipped** — this section now records their
status accurately.

### Nine Terminals (九幺) — SHIPPED (Phase J Wave 4)

Classical 13-Orphans requires honor tiles (winds + dragons); Changsha's **108-tile
deck has no honors**, so the classical hand is structurally impossible. The
Changsha-adapted **九幺 / Nine Terminals** analog (every tile rank 1 or 9, all six
distinct terminals present — "loose" default) ships as `WinPattern.NineTerminals`,
a Big Win. See [spec §4.2.1](rules/changsha-spec.md#421-nine-terminals--strict-vs-loose-default-phase-j-wave-4).

- Pinned by: `WinPatternTests.NineTerminals_RankBoundsOnly` (binding semantic).
- Remaining V2 option: a strict 4-sets+pair variant behind a future game-options flag.

### Robbing the (Added) Kong — 抢杠胡 — SHIPPED (Phase H Wave 2)

When a seat upgrades an exposed Pung to a Kong (补杠), any other seat that can win
on that tile is offered a Hu-only claim window before the kong completes
(`WinMethod.RobbingKong`, `WinResult.IsRobbedKong`). Concealed kongs (暗杠) remain
non-robbable per spec §3.4.3.

- Spec: [§4.2.2 Special-Context Big Wins](rules/changsha-spec.md#422-special-context-big-wins-phase-h-wave-2--phase-i-wave-1).
- Pinned by: `WinPatternTests.RobbingKong_Win_DetectorAcceptsKongTileAsWinningTile`,
  `RobbingKongAcceptanceTests`, `EdgeCaseTests.ExposedKong_CanBeRobbed_*`.

### Fan (番) catalog + big-win stacking — DETECTED & SURFACED, NON-SCORING by default

The detector populates `WinDetectionResult.AllPatterns` when a hand satisfies
multiple Big-Win shapes, and the 14-entry fan catalog (`FanCalculator`) is evaluated
on every win. Both are **surfaced for display** (`ScoreResult.Fans` / `FanPoints`,
WS/SignalR `fans[]`) but are **query-only with respect to the authoritative payout**:
per #117 the live default (`ChangshaScoringOptions.SpecPure`) pays the spec §5.1
two-tier table verbatim — **no fan bonus folded in, no stacking multiplier applied**.

An opt-in `ChangshaScoringOptions.HouseRules` mode folds fan points into payments and
applies the `×Clamp(AllPatterns.Count,1,3)` stack for a possible future tournament
option; it is **not** the default.

- Spec: [§5.4 Fan Catalog & Big-Win Stacking — Query-Only](rules/changsha-spec.md#54-fan-catalog--big-win-stacking--query-only-non-scoring-in-v1).
- Pinned by: `Section51GoldenTests` (spec-pure §5.1 Examples 1-10 frozen),
  `ScoringOptionsCharacterizationTests` + `FanCatalogIntegrationTests` (house-rules
  magnitudes), `StackedBigWinScoringTests` (multiplier capability),
  `WinPatternTests.StackedBigWinPatterns_AllPungsPlusFullFlush_PopulatesAllPatterns`.
- **Open question (surfaced by #117, undecided):** whether canonical Changsha should
  score a fan catalog + stacking at all, or whether §5.1 is the complete model.

---

## UX / system limitations

### No soft variant hot-swap (page reload required)

Changing the variant picker in the Phase G sidebar lobby requires a full page
reload (`window.location.replace`) — the setup pipeline rebuilds tile
catalogues at boot, and mutating `gameType` mid-session would leave dangling
3D meshes in the scene graph. The lobby explicitly gates the variant select
behind a "Reload to change variant" warning.

`dealMode`, `botCount`, and `botDifficulty` **do** support hot-swap (they
take effect on the next deal without a reload).

- Spec/design: `.squad/decisions/inbox/ripley-phase-f-design.md` §1.4
  (Phase F risk #2); `.squad/decisions.md` Phase G entry.
- Implementation: `src/frontend/autotable-src/src/lobby.ts`.
- Deferred to: Phase I+ (requires clean disposal of `World.things` keyed by
  variant-specific tile IDs).

---

### Replay-integrity verifier not implemented

The append-only event log (`ChangshaGameState.EventLog`) is preserved in
state, but there is no verifier that replays events from sequence zero and
asserts byte-equality with the snapshot. This is a defence-in-depth gap, not
a functional one — events are deterministic by construction.

- Deferred to: v1.1 / Phase I+ (same MVP-narrowing cut as hydration).

---

## How to track new gaps

When a new skipped test is added, please:
1. Pin it here with the `File:Line` + Skip reason.
2. Link the spec section it relates to.
3. Reference the design memo (under `.squad/decisions/inbox/`) that captures
   the chosen V2 plan, if one exists.
