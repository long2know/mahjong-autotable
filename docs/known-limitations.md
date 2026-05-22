# Mahjong Autotable — Known Limitations (V1)

> Captured at Phase G GA (commit `730946c`). Updated as gaps close.

## Changelog

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

### 过胡 (Pass-Hu) decay precision is simplified

Per the canonical rule (spec §3.6), a seat that passes on a winning discard
is forbidden from declaring Hu **until their next own draw**. V1 implements
the decay correctly per-draw (`ChangshaStateMachine.cs:372-378`), but the
state machine simplification omits one edge case: if a player claims Pung/Kong
between the missed Hu and their next own draw, the Pung/Kong itself does
**not** reset the lockout — only an actual self-draw does. This is the
spec-correct behaviour per Baidu §过水 ("until your next draw"), but some
regional variants reset on any active turn.

**Impact:** subtle — affects ~1% of hands where a chain of claims happens
before the offending seat would otherwise draw. The current behaviour is
defensible per the Baidu canonical interpretation; this item exists to flag
the variant ambiguity to operators.

- Spec: [§3.6 Missed Win Rule (过胡 / 过水)](rules/changsha-spec.md#36-missed-win-rule-过胡--过水).
- Implementation: `Changsha/ChangshaStateMachine.cs` — `MissedWinSeats` set
  and its lifecycle.
- Deferred to: V2 (only if Stephen rules the regional variant takes precedence).

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

## Patterns deferred to V2

The V1 win detector covers four patterns: Standard (258 pair), Seven Pairs
(七对子), All Pungs (碰碰胡), Full Flush (清一色). The following Big-Win and
score-stacking behaviours are **deferred to V2** per spec §4.3:

### Thirteen Orphans (十三幺) — deferred + variant ambiguity

Classical 13-Orphans requires honor tiles (winds + dragons). Changsha plays
with a **108-tile deck and no honors**, so the classical hand is structurally
impossible. Some regional sources list a Changsha-adapted "9-Terminals"
analog (all rank-1-or-9 tiles); V2 may ship that under
`WinPattern.NineTerminals` instead.

- Spec: [§4.3 Patterns Deferred to V2](rules/changsha-spec.md#43-patterns-deferred-to-v2).
- Pinned by skipped tests:
  - `HuValidationBigWinsTests.cs:110` (`Hu_ThirteenOrphans_SpecGap_Skipped`)
  - `WinPatternTests.cs:114` (`ThirteenOrphans_DeferredToV2`)
- Design plan: `.squad/decisions/inbox/ripley-phase-h-design.md` §2.1.

---

### Robbing the (Added) Kong — 抢杠胡

When a seat upgrades an exposed Pung to a Kong (补杠) by adding their drawn
matching tile, any other seat that can win on that tile **should** be offered
a Hu opportunity in a brief claim window before the kong completes. V1 does
not open this window — added kongs proceed straight to the replacement draw.

This affects two test stubs (same mechanic, both sides of the table):
- The claimer's perspective: declare Hu on the kong-tile.
- The declarer's perspective: their added-kong opens a claim window.

Concealed kongs (暗杠) remain non-robbable in V2 — the spec is explicit.

- Spec: [§3.4.3 Added Kong / Extended Kong (补杠)](rules/changsha-spec.md#343-added-kong--extended-kong-补杠), [§4.3 Patterns Deferred to V2](rules/changsha-spec.md#43-patterns-deferred-to-v2).
- Pinned by skipped tests:
  - `WinPatternTests.cs:119` (`RobbingKong_Win_DeferredToV2`)
  - `EdgeCaseTests.cs:97` (`ExposedKong_CanBeRobbed_DeferredToV2`)
- Design plan: `.squad/decisions/inbox/ripley-phase-h-design.md` §2.2.

---

### Big-win pattern stacking + score multipliers

V1 returns a **single** `WinPattern` from the detector even when a hand
satisfies multiple Big-Win criteria simultaneously (e.g., 自摸 + 清一色 +
碰碰胡 — self-draw + Full Flush + All Pungs). The base BigWin payment applies
once; the additional patterns do not stack.

V2 will surface `WinDetectionResult.AllPatterns` and apply a multiplicative
stack factor to `ScoringService` payments — see the design memo for the
proposed multiplier table.

- Spec: [§5 Scoring (番 / Fan)](rules/changsha-spec.md#5-scoring-番--fan).
- Pinned by skipped tests:
  - `WinPatternTests.cs:124` (`StackedBigWinPatterns_DeferredToV2`)
  - `EdgeCaseTests.cs:99` (`StackedBigWinPatterns_DeferredToV2`)
  - `EdgeCaseTests.cs:100` (`MultipleBigWinPatterns_ScoresStack_DeferredToV2`)
- Design plan: `.squad/decisions/inbox/ripley-phase-h-design.md` §2.3.

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
