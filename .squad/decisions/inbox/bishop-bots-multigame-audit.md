# Bishop — W25 Bot Autonomy + Multi-Game Audit

**Date:** 2026-06-03
**Branch:** `test/bishop-bots-multigame`
**Stephen's directive:** "Verify bots are operating autonomously, each game has its own state, late joiners get current state, bots actively claim when opportunity arises, and bot difficulty (URL `?botDifficulty=`) actually changes their strategy."

## TL;DR

Three concrete defects shipped:

1. **`?botDifficulty=` URL param was a black hole** — captured into
   `AutotableConnection.BotDifficulty` but never forwarded to the
   runtime. Every game played at the runtime-wide `Medium` default
   regardless of URL value, including spectator all-bots-watch URLs.

2. **Bot strategy was process-scoped instead of per-game** —
   `ChangshaGameRuntime._strategy` was a single field shared across
   every game on the host. Setting one game to `Hard` (had the
   plumbing been working) would have flipped every other game on the
   same process.

3. **Replay audit envelope mislabeled bot difficulty** —
   `ResolveReplayEventSource` formatted `"bot:{_strategy.Difficulty}"`
   off the process-scoped strategy, so audit replay always showed
   `bot:medium` no matter what the live game was running.

Plus one flaky test fix and 8 new xUnit cases + a Playwright spec
that exercises the full WS → runtime plumbing.

## Pre-W25 state

`AutotableWsEndpoint.cs:265-267` reads `?botDifficulty=` and stashes
it on `AutotableConnection.BotDifficulty`. That field has exactly two
consumers:

- The connection log line (`AutotableWsEndpoint.cs:285`).
- *Nothing else.*

`ChangshaGameRuntime` is constructed with one
`IChangshaBotStrategy _strategy = ChangshaBotEngine.Default` (Medium).
The dispatch sites at `BotClaimAsync` (~line 1263) and
`RunBotTurnAsync` (~line 1505) both call `_strategy.DecideWithReasoning(state, seatIndex)`.

Net effect: pre-W25, the runtime ALWAYS dispatched on Medium
regardless of URL difficulty, and there was no per-game override
surface at all.

## Fix

### Per-game strategy override on `ChangshaGameInstance`

```csharp
private IChangshaBotStrategy? _botStrategy;
public IChangshaBotStrategy? BotStrategy
{
    get => Volatile.Read(ref _botStrategy);
    set => Volatile.Write(ref _botStrategy, value);
}
```

Volatile because writes happen at game-create / strategy-rebind and
reads happen on every bot tick — no need for a lock since strategies
are stateless singletons. Null means "fall back to the runtime
default" so existing test harnesses that never set per-game strategy
keep their pre-W25 behaviour.

### `IChangshaGameRuntime` surface additions

```csharp
Task<bool> SetBotStrategyAsync(string gameId, string difficulty, CancellationToken ct = default);
string? GetActiveBotDifficulty(string gameId);
```

`SetBotStrategyAsync` resolves the difficulty through the existing
`ChangshaBotEngine.Resolve` (case-insensitive; unknown → Medium —
deliberate UX so a typo in the URL doesn't crash the table) and
binds it on the instance. Idempotent: rebinding to the same
difficulty is a no-op; rebinding to a different difficulty hot-swaps.

`GetActiveBotDifficulty` is a diagnostic accessor used by the new
xUnit tests to assert without exposing strategy instances.

### Dispatch + replay-source updates

Both bot dispatch sites now read `var strategy = instance.BotStrategy ?? _strategy;`
and dispatch on `strategy.DecideWithReasoning`. `ResolveReplayEventSource`
takes the instance too so the audit envelope reports the per-game
difficulty correctly.

### WS endpoint plumbing

`EnsureRuntimeBoundAsync` now accepts an optional `botDifficulty`
parameter; both the spectator auto-deal path
(`TryAutoDealForSpectatorAsync`) and the human seat-take path
(`TryHandleSeatTakeAsync`) pass `connection.BotDifficulty` through.
Idempotent re-binding still propagates the difficulty, so a reconnect
with a different `?botDifficulty=` will rebind the strategy.

## Late-join test flake fix

`LateJoin_ToExistingGameId_ReceivesAccumulatedSnapshot_ForThatGameOnly`
was failing 1/1 locally. Root cause was NOT a real cross-game leak —
the assertion was self-inflicted:

- Bob pushed `["things", 42L, {...}]` into MULTI-B.
- Charlie joined MULTI-A and the assertion checked that no `things[42]`
  appears in his snapshot.
- BUT `ChangshaToAutotableTranslator.ShouldSynthesizeWall` returns
  true during `Seating | RollingDice` when state.Wall is empty, and
  emits a synthetic 108-tile face-down wall keyed `things[0..107]`.
  Key `42` collided directly with the synthetic wall.

Fix: use a key outside the `AutotableSlotMap.TotalTiles = 108` range
(`999_999_042L`) and add a unique value marker. Defence-in-depth: the
assertion now also checks the marker value, so even a future
translator change reintroducing wide-range synthetic ids can't pass.

## Tests added

### `UrlBotDifficultyPlumbingTests` — 8 cases, all green

WS-integration tests that connect with `?botDifficulty=...` and
assert `runtime.GetActiveBotDifficulty(runtimeGameId)` matches:

- `UrlBotDifficulty_IsForwardedTo_RuntimeStrategy` — Theory over
  Easy / Medium / Hard / Master (4 cases).
- `UrlBotDifficulty_IsCaseInsensitive` — `EASY`, `HARD`, `MaStEr`
  (3 cases).
- `UrlBotDifficulty_UnknownValue_FallsBackToMedium` —
  `?botDifficulty=GalaxyBrain` lands on Medium (the documented
  Resolve fallback).
- `UrlBotDifficulty_TwoGames_AreIsolated` — two parallel spectator
  games with different difficulties don't cross-bleed; explicit
  pin against the pre-W25 process-scoped-strategy regression.

Uses the spectator (`?seat=-1&botCount=4`) URL path because it
deterministically auto-binds the runtime without a seat-take
round-trip.

### `playtest-bishop-bots.spec.mjs` — Playwright, 3 sections, all green

- **Section B** (4-bot self-play): one all-bots-watch context,
  3-minute observation window, asserts peak discards ≥ 8 across
  samples AND no page errors. Discards reset between hands when
  a Hu fires, so the assertion is on peak not final.
- **Section C** (multi-game isolation): two contexts with distinct
  gameIds, sample (id, slotName) tuple sets across the window,
  assert no full-overlap cross-bleed.
- **Section D** (late-join state delivery): early context plays for
  the warmup window, late context joins the same gameId, asserts
  late side sees ≥ half the early side's current discards.

Smoke-run against `http://127.0.0.1:8088` with
`BISHOP_B_OBSERVE_MS=45000`: all three sections PASS.

## Results

- `dotnet test --filter "UrlBotDifficultyPlumbing|MultiGameRouting|LateJoin|BotStrategy"`
  → **44/44 PASS** in 10 s.
- Broader sweep `Autotable|Changsha.Bots|Changsha.Runtime|Changsha.Acceptance|Changsha.Replay`
  → **5324/5327 PASS** (1 unrelated Vasquez W9 workflow YAML failure;
  2 intentionally-skipped 100-hand bot simulations).
- Playwright `playtest-bishop-bots.spec.mjs` against the dev
  backend → **3/3 sections PASS**.

## Takeaways for the squad

1. **URL params need a plumb-AND-pin test pair.** Capturing a query
   param into `AutotableConnection.*` is *not* plumbing it — the
   integration path to the runtime is the load-bearing part. Any
   new query param should ship with at least one WS-integration
   test that asserts the downstream observable effect.

2. **Process-scoped engine fields are multi-game-poison.** Treat
   any `_field` on a singleton runtime as a shared-state hazard —
   if there's a "per-game" intuition behind it, hoist it onto the
   per-game instance with a `?? _runtimeDefault` fallback (same
   pattern this PR uses for `BotStrategy`). Audit candidates next
   sprint: `ChangshaRuntimeOptions` knobs (timeouts), the JSON
   serializer options, the dice service.

3. **Tile-id space (0..107) is a forbidden zone for synthetic
   collection keys in tests.** The translator's face-down-wall
   synthesis re-uses that range for client-side rendering when
   the runtime hasn't dealt yet. Any `["things", N, value]` push
   in a test that lands during Seating/RollingDice phase WILL
   collide. Use `>= 1_000_000_000L` for test-side synthetic keys.

4. **`?botDifficulty=` is now observable via
   `IChangshaGameRuntime.GetActiveBotDifficulty(gameId)`** — handy
   for admin tooling and for any future "swap difficulty between
   hands" hot-flip feature. The setter is also async-safe to call
   from a hot path because it's just a volatile field write under
   the hood.
