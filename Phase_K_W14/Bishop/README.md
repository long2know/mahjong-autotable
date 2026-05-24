# Phase K Wave 14 — Bishop (Backend) — artefacts

This directory carries Bishop-lane Phase K Wave 14 artefacts.

## Test summary

Bishop-lane test surface added in W14:

| Test file | Count |
| --- | --- |
| `SpectatorAuditQueryEndpointTests.cs` | 16 |
| `CommentaryCostSummaryEndpointTests.cs` | 10 |
| `BracketQueryEndpointTests.cs` | 13 |
| `ReplayListingEndpointTests.cs` | 19 |
| `JwksOverlapEnforcementTests.cs` | 10 |
| `SignalRSequenceMetricsTests.cs` | 21 |
| **Total Bishop W14 tests** | **89** |

All 89 tests pass via:

```
dotnet test src/backend/Mahjong.Autotable.slnx --nologo \
    --filter "Wave=Phase-K-14&Lane=Bishop"
```

Full backend gate: **3027/0/0 → 3029 total** (the 2
failures are Vasquez-lane pre-existing — `frontend-pwa-audit.md`
content assertions outside Bishop's lane).

## Files modified / added (Bishop lane)

### New backend files

* `src/backend/src/Mahjong.Autotable.Api/Commentary/CommentaryCostController.cs`
  — admin-gated `GET /api/commentary/cost/summary`.
* `src/backend/src/Mahjong.Autotable.Api/Observability/SignalRSequenceMetrics.cs`
  — three-metric Prometheus collector singleton.

### Modified backend files

* `Spectator/SpectatorHandoffAudit.cs` — `QueryAsync` API +
  `PageSize` options.
* `Spectator/SpectatorHandoffController.cs` — admin-gated
  audit query endpoint.
* `Tournament/EfBracketStore.cs` — `BracketQueryOptions` class.
* `Tournament/TournamentController.cs` — paginated bracket
  query endpoint.
* `Replays/ReplayStore.cs` — `ListAsync` API + `PageSize`
  options.
* `Replays/ReplayController.cs` — paginated listing endpoint.
* `Auth/JwtValidationService.cs` — `ErrorRollbackRejected`
  constant + overlap-window enforcement.
* `Observability/SignalRSequenceRetentionSweep.cs` — optional
  metrics injection.
* `Observability/MetricsEndpoint.cs` — SignalR metrics rendering
  with fallback.
* `Program.cs` — JWT validator factory registration, bracket
  query options, SignalR metrics singleton.

### Docs

* `docs/spectator-handoff.md` — §4 Audit query API.
* `docs/commentary-llm.md` — §6 Cost dashboard endpoint.
* `docs/bracket-shape.md` — §5 Bracket query API.
* `docs/replay-by-id.md` — §3 Replay listing API.
* `docs/jwt-rotation.md` — §14 Overlap-window enforcement.
* `docs/realtime-resilience.md` — §8 Metrics.
* `docs/phase-l-bringup.md` — new pre-work surface doc.

### Memo + history

* `.squad/decisions/inbox/bishop-phase-k-wave-14.md` — full
  design rationale.
* `.squad/agents/bishop/history.md` — Phase K Wave 14
  appended section.
