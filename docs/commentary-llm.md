# Commentary LLM

Phase K Wave 12 — Bishop.

## §1 — Overview

The W6–W11 commentary pipeline ships:

* `ICommentaryGenerator` — the abstract generator seam.
* `StubCommentaryGenerator` — deterministic placeholder.
* `OpenAiCommentaryGenerator` — Chat-Completions backed (OpenAI + Azure).
* `ICommentaryUsageMeter` — input + output token accounting, in-memory or EF-backed.
* `ICommentaryStore` — durable per-record history with retention sweep.

Wave 12 adds **cost budgeting** on top of the existing token meter.

## §2 — Configuration

| Key                                 | Default       | Notes                            |
| ----------------------------------- | ------------- | -------------------------------- |
| `Commentary:Provider`               | `Stub`        | `OpenAI` / `Azure` for real LLM  |
| `Commentary:Endpoint`               | OpenAI        | Provider HTTP base URI           |
| `Commentary:ApiKey`                 | (empty)       | Literal or `env:VAR_NAME`        |
| `Commentary:Model`                  | `gpt-4o-mini` | Model identifier                 |
| `Commentary:RateLimitPerGameSeconds`| 5             | Min interval between generations |
| `Commentary:MonthlyTokenCap`        | 0             | 0 = unlimited (W8 hard cap)      |
| `Commentary:UsageMeterImpl`         | `InMemory`    | `Ef` for production              |
| `Commentary:ThrowOnMonthlyCap`      | false         | true = 429 instead of fail-open  |
| `Commentary:StorageImpl`            | `InMemory`    | `Ef` for production              |
| `Commentary:RetentionDays`          | 30            | Record retention window          |
| `Commentary:CostBudget:MonthlyCapUsd`  | 0          | 0 = no USD cap                   |
| `Commentary:CostBudget:TokensPerDollar`| 200_000    | gpt-4o-mini canonical rate       |
| `Commentary:CostBudget:WarnThreshold`  | 0.8        | 80% warning trigger              |

## §3 — Token meter

The token meter records `inputTokens + outputTokens` per generation,
keyed by `gameId`. The `EfCommentaryUsageMeter` persists the counts
to the `CommentaryUsage` table via a per-(playerId, monthKey) row;
`InMemoryCommentaryUsageMeter` keeps the same counts in process
memory. Both satisfy the same `ICommentaryUsageMeter` contract:

* `RecordUsage(gameId, input, output)` — bumps the per-game + monthly counters.
* `MonthlyTokens(utcNow)` — total tokens across all games for the current calendar month.
* `ExceedsMonthlyCap(cap, utcNow)` — `true` when `MonthlyTokens(utcNow) ≥ cap` (0 = unlimited).

## §4 — Cost budgeting (Phase K Wave 12)

### Why a USD layer on top of token counts

Operators reason about LLM cost in dollars, not tokens. The W12
budget gate multiplies the monthly token count by
`Commentary:CostBudget:TokensPerDollar` to expose a USD ledger, then
classifies the result against `Commentary:CostBudget:MonthlyCapUsd`:

* `Healthy` — `usd < WarnThreshold × cap`. Generation proceeds.
* `Warning` — `WarnThreshold × cap ≤ usd < cap`. Generation proceeds but the controller emits a one-shot per-month warning log so operators can react.
* `Exhausted` — `usd ≥ cap`. The `CommentaryController` routes new requests to the deterministic stub generator until the next calendar month rolls over.

`tokensPerDollar` is the published gpt-4o-mini rate (~200_000 tokens
per USD as of 2026-06). Operators tuning a different model adjust the
knob — for gpt-4o (the full model) the canonical rate is closer to
50_000.

### Switch-to-stub mechanism

The controller resolves both the configured `ICommentaryGenerator`
and a concrete `StubCommentaryGenerator` singleton. On every
`POST /api/games/{gameId}/commentary/replay`, the controller calls
`SelectGenerator()`:

```csharp
private ICommentaryGenerator SelectGenerator()
{
    if (_budget is null || _stubGenerator is null) return _generator;
    var evaluation = _budget.Evaluate(DateTime.UtcNow);
    if (evaluation.State == BudgetState.Exhausted
        && !ReferenceEquals(_generator, _stubGenerator))
    {
        return _stubGenerator;
    }
    return _generator;
}
```

The switch is per-request, idempotent, and pure — there is no shared
mutable state between requests. The `CommentaryCostBudget.Evaluate`
side-channel does a one-shot per-month log when the state transitions
to `Warning` / `Exhausted` so the audit trail is preserved.

### Reset

The token meter resets on the first day of every calendar month (the
`MonthlyTokens(utcNow)` query is bound to `year * 100 + month`). The
budget gate inherits that reset automatically — no operator action
needed.

### Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W12/Bishop/CommentaryCostBudgetFacts.cs`:

* `CommentaryOptions.CostBudgetOptions` defaults: `MonthlyCapUsd = 0`, `TokensPerDollar = 200_000`, `WarnThreshold = 0.8`.
* `Evaluate` returns `Healthy` at zero usage (with cap configured).
* `Evaluate` returns `Warning` at 80% of cap.
* `Evaluate` returns `Exhausted` at full cap.
* `Evaluate` returns `Healthy` when `MonthlyCapUsd = 0` regardless of usage.
* `MonthlyUsd = MonthlyTokens / TokensPerDollar`.
* `BudgetState` enum carries `Healthy`, `Warning`, `Exhausted`.

## §5 — Realtime warnings (Phase K Wave 13)

### §5.1 — SignalR admin channel

W13 lands a SignalR side-channel so operator dashboards can surface
budget transitions in real time rather than tailing the log stream
for the one-shot per-month warning.

* **Hub**: `CommentaryCostAdminHub` — mapped at
  `/hubs/admin/commentary-cost`.
* **Group**: `commentary:cost:admin` — every admin client joins the
  same broadcast group via `JoinAdminChannel()`.
* **Auth**: admin-gated upstream by the cookie resolver on the
  negotiate request (matches the gating on every other
  `/hubs/admin/*` route).

### §5.2 — Envelopes

| Event                       | Trigger                                            |
| --------------------------- | -------------------------------------------------- |
| `CommentaryCostWarning`     | First `Evaluate()` flip to `Warning` per month     |
| `CommentaryCostCapReached`  | First `Evaluate()` flip to `Exhausted` per month   |

Both envelopes carry `{ monthlyUsd, capUsd, warnThresholdUsd, model, monthKey }`
so the dashboard can render the absolute + relative usage without
re-querying the meter.

### §5.3 — Wiring

`CommentaryCostBudget.Evaluate` invokes the broadcaster from inside
the existing `Interlocked.CompareExchange` one-shot gates that fence
the warning + cap log lines. The broadcast is **fire-and-forget**
behind a `FireBroadcast` helper that observes the returned task's
exception (so the unobserved-task finalizer never sees it) — the
hot evaluation path never awaits the SignalR roundtrip.

The broadcaster is registered as a singleton in `Program.cs` so the
hub context is reused across requests. A missing broadcaster (e.g.
when SignalR is disabled in a test fixture) is a hard no-op — the
`Evaluate` call still logs and switches to the stub.

### §5.4 — Contract pins

Hard-asserted in
`tests/Mahjong.Autotable.Api.Tests/Phase_K_W13/Bishop/CommentaryCostBroadcastTests.cs`:

* The hub exposes `JoinAdminChannel` / `LeaveAdminChannel` methods.
* `CommentaryCostAdminHub.AdminGroup == "commentary:cost:admin"`.
* `WarningEvent == "CommentaryCostWarning"`,
  `CapReachedEvent == "CommentaryCostCapReached"`.
* The broadcaster invokes the hub group exactly once per evaluation
  transition (matches the one-shot log gate).
* The broadcaster swallows exceptions so a degraded hub never
  breaks the cost evaluation path.
* `Evaluate()` wires through to the broadcaster on the first
  `Warning` / `Exhausted` flip within a calendar month.
* Subsequent flips inside the same month are suppressed at the
  source (re-uses the W12 log-suppression gate).
