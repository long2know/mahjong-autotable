# Phase J Wave 4 — Vasquez (Test Engineer): pattern-ordering endpoint coverage + game-completion lifecycle suite + frontend DOM contract

**By:** Vasquez (Senior Test Engineer), 2026-05-22

**Branch:** `stlong/phase-j-wave-4-completion`
**Primary commit:** `3c5ee33` — `test(phase-j-wave-4): pattern-ordering endpoint + game-completion lifecycle suite + DOM selectors contract`

---

## Wave goal

Pin three Wave-3/Wave-4 contracts that shipped (or are shipping) without
dedicated test coverage so future regressions fail loudly at CI rather
than via player bug reports:

1. **Bishop's Wave-3 pattern-ordering endpoint** (`GET /api/changsha/pattern-ordering`).
   No tests landed with the endpoint — a future blind spot where a new
   `WinPattern` enum value is added without a corresponding ordering
   entry would silently sink to `AlphabeticalFallbackOrder = 999` and
   render in the wrong slot in Hicks's result-modal chip strip.
2. **Bishop's Wave-4 reconciliation of `ChangshaPhase.GameComplete` vs
   `EndGame`.** The Wave-2 GameCompletion tests pin the symbols but not
   the lifecycle around them — specifically: SignalR `GameCompleted`
   exactly-once, the HydrateAsync skip filter for terminal phases, and
   the "before MaxHands stays playable" regression guard.
3. **Hicks's Wave-4 `data-testid` surface.** Three new testids
   (`mobile-move-log-toggle`, `reconnect-copy-link`, `toast-region`)
   join the Wave-2/-3 lobby + connection-banner surface; no
   integration tests exist yet, but the DOM contract MUST be
   documented in a single source of truth so the upcoming Playwright /
   Cypress suite has a stable surface to target.

## What shipped

### 1. `src/backend/tests/Mahjong.Autotable.Api.Tests/Api/PatternOrderingEndpointTests.cs` (NEW — 3 facts)

WebApplicationFactory<Program> over the real Minimal-API route handler
in `Program.cs`. Per-test SQLite isolation + `PersistSnapshots = false`
mirrors `HealthEndpointTests` verbatim.

| Test | Contract pinned |
|---|---|
| `PatternOrdering_ReturnsOk_WithFlatJsonMap` | 200 OK + flat `Dictionary<string,int>` shape; every key starts lowercase (camelCase wire convention); every value ≥ 0; total count == `ChangshaPatternOrdering.Order.Count`. |
| `PatternOrdering_AllWinPatternEnumValues_HaveAnOrderingEntry` | Reflects `Enum.GetValues<WinPattern>()` and asserts every defined value has a wire entry. Mirrors `Program.cs`'s `WinPatternWireName` switch locally so a future Bishop rename of a wire string fails the test for the right reason. |
| `PatternOrdering_HeavenlyHand_OutranksAllPungs` | Canonical tier ordering survives: HeavenlyHand (Big Win) < AllPungs (bonus-structural); SevenPairs (bonus-structural) < FullFlush (alphabetical tail). Asserts relative ranks only — Bishop's reserved-slot scheme means absolute integer values may shift between waves. |

### 2. `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/GameCompletionLifecycleTests.cs` (NEW — 4 facts)

Reflection-defensive against Bishop's `GameComplete` vs `EndGame`
reconciliation. `ResolveTerminalPhases()` discovers the terminal-phase
set via name-match heuristic (any enum value whose name contains
"Complete" or "EndGame") so Bishop's actual reconciliation choice
(collapse-via-alias `EndGame = GameComplete`, rename, keep both) keeps
the suite green regardless.

| Test | Contract pinned |
|---|---|
| `FourHandsCompleted_TransitionsToCanonicalTerminalPhase` | Default MaxHands=4 → state machine reaches a terminal phase after exactly 4 hands, IsGameComplete true. Bot-driven step-machine harness inlined (mirrors Wave-2 GameCompletionTests). |
| `BeforeMaxHands_StaysInPlayablePhase` | 3 of 4 hands → IsGameComplete false, phase outside terminal set. Guards against a regression from `>` to `>=` in the cap check (`HandNumber > MaxHands` is the canonical comparison per Wave-2 `RotateBanker`). |
| `GameCompletedEvent_Fires_OnceOnly` | SignalR subscribe to "GameCompleted" via `ChangshaHubTestHarness`, drive a 4-bot game (default MaxHands=4), assert exactly one event fires. 90s ceiling on first fire + 1s grace before count assertion. Payload shape check: `gameId, maxHands, finalScores, winner.seatIndex, phase` all present. |
| `HydrationFilter_SkipsTerminalPhase` | Per-terminal-phase (discovered via reflection), insert a synthesized snapshot into a fresh SQLite DB and assert HydrateAsync skips it. Active AwaitingDiscard control row must hydrate — without the positive case the test couldn't distinguish "filter skipped everything" from "filter worked correctly". |

### 3. `src/frontend/autotable-src/tests/selectors.md` (NEW directory + file — DOM contract)

19 distinct testids documented across four surfaces:

- **Lobby (13):** `lobby-toggle`, `lobby-players-section`,
  `lobby-players-strip`, `lobby-players-empty`,
  `lobby-player-chip-{0..3}`, `lobby-seat-preview`,
  `lobby-seat-preview-{0..3}`, `lobby-quick-match`,
  `lobby-variant-fieldset`, `lobby-bot-difficulty-fieldset`,
  `lobby-hand-count-fieldset`, `lobby-open-settings`, `lobby-apply`.
- **Mobile drawers (1, Wave 4):** `mobile-move-log-toggle`.
- **Reconnect / disconnect banner (5):** `connection-banner`,
  `connection-banner-retry`, `reconnect-copy-link` (Wave 4),
  `connection-banner-lobby`, `toast-region` (Wave 4).
- **Reserved (future):** in-game HUD (`hud-*`), result-modal
  (`result-modal-pattern-chip-{wireName}` — chip strip MUST consume
  `/api/changsha/pattern-ordering` wire names so the integration test
  asserts ordering end-to-end), game-over modal (`game-over-*`).

Every entry carries a file:line citation. Stability Contract section
spells out the identity / cardinality / lifetime / naming guarantees
Hicks's surface owes the integration suite.

## Gate

```
Failed: 0, Passed: 431, Skipped: 0
```

- Wave 3 baseline: 424. Wave 4: +7 = 431.
- Wave-4 filter (`Wave=Phase-J-4`): 7 / 7 green.
- Zero-skip streak preserved (8 consecutive waves: I.1 → I.2 → I.3 → I.4 → J.1 → J.2 → J.3 → J.4).

## Bishop's chosen canonical terminal-phase name

**`GameComplete`** — Bishop's reconciliation collapses `EndGame` into
`GameComplete` via the C# enum-alias trick (`EndGame = GameComplete`,
same underlying int). Both names still compile, satisfy `==` equality,
and round-trip through System.Text.Json by either name (since `Enum`
serialization defaults to the canonical name for the int value).
`state.Phase.ToString()` always returns `"GameComplete"` post-merger,
so the SignalR `GameCompleted` payload's `phase` field is a single
canonical wire string. Wave-2 tests that reference `ChangshaPhase.EndGame`
continue to compile (alias retained for backwards source-compat).

## Hicks's testid surface

**19 distinct ids** in the Wave-4 contract (cardinality of unique
`data-testid` patterns; runtime element count is higher because four
of these are templated `-{0..3}` indices). Of those 19:

- **13** were committed in Wave 2/3 (lobby surface + connection
  banner basics).
- **3** are NEW in Wave 4: `mobile-move-log-toggle`,
  `reconnect-copy-link`, `toast-region`.
- **3** are dynamically injected from TS rather than hard-coded in
  `index.html` (`lobby-seat-preview-{i}`, `lobby-players-empty`,
  `lobby-player-chip-{chipIndex}`).

## Blind spots flagged for Bishop / Hicks / Apone

### Bishop

- **Alias-merged enum has wire-shape implications on rehydrate.**
  Legacy snapshots persisted with `"EndGame"` in JSON round-trip back
  to `GameComplete` semantically (same int), but `ToString()`
  re-serializes them as `"GameComplete"` going forward. Pinning this
  round-trip is a follow-on test slot — not blocking for Wave 4
  because no production deployments carry pre-Wave-4 snapshots, but
  worth scheduling for Wave 5 / J-extended if the deploy lane lands
  before the alias is mature.
- **`AlphabeticalFallbackOrder = 999` is a silent fallback.** A
  future `WinPattern` value added without an ordering entry sorts
  to the tail without throwing. Bishop's defensive choice is correct
  (no crash), but my Test #2 reflects this gap loudly at CI so the
  miss is caught at PR time rather than via player UX bug reports.

### Hicks

- **`reconnect.ts` is untracked.** 260 LOC new file exports
  `SESSION_KEY_PREFIX`, `TOKEN_TTL_MS`, `SessionToken` — the
  reconnect-copy-link button (`data-testid="reconnect-copy-link"`)
  depends on this module being wired in from `client.ts` or
  `index.ts` at runtime. If Hicks's commit lands without wiring
  the import, the copy-link button is inert and the contract test
  catches `data-testid` presence but not behaviour. Smoke-test
  wire-check recommended before merge.
- **No testids on game-over modal yet.** Test #3 of the lifecycle
  suite pins the `GameCompleted` SignalR event fires once; the
  end-of-game UI Hicks surfaces from that event has no contract
  entry yet. `selectors.md` reserves the `game-over-*` prefix but
  doesn't yet have file:line citations. Schedule for Hicks's next
  wave.

### Apone

- **`squad-*.yml` workflow files are untracked.** `.github/workflows/`
  has 7 untracked `squad-*.yml` files (`squad-ci.yml`,
  `squad-docs.yml`, `squad-insider-release.yml`,
  `squad-label-enforce.yml`, `squad-preview.yml`, `squad-promote.yml`,
  `squad-release.yml`). Apone's committed Wave-4 work is `docker-build.yml`
  + `docker-smoke.yml`. These untracked files may be future-wave
  scaffolding or scratch; clarify in Apone's next memo whether they're
  meant to land or get moved to `scratch/`.
- **The Wave-3 `BUILD_SHA=""` empty-string issue I flagged last wave
  still appears unaddressed in committed code.** Bishop's `??` operator
  only handles null; Apone's `ENV BUILD_SHA=""` sets a literal empty
  string. Verified via my Wave-3 smoke that live `/health` returns
  `buildSha=""` rather than `"dev"`. Either Bishop widens the fallback
  to `IsNullOrEmpty` or Apone changes the Dockerfile default to
  `BUILD_SHA=dev`. Pinning a unit test for the empty-string case once
  one of them ships the fix.
