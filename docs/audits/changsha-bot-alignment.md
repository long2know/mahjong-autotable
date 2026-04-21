# Changsha-First Bot Alignment Audit

## Scope and method

This audit reviews the current scaffold against a **Changsha-first playable goal with bot opponents**, using the current backend/frontend/docker artifacts as evidence.  
No gameplay code changes are proposed here; this is architecture/rules alignment only.

## 1) Current-state architecture audit

### What exists now

- Backend is a .NET 10 minimal API with EF Core provider switch (Sqlite/PostgreSql/SqlServer), defaulting to SQLite.
- Persistence currently stores `TableSession` with:
  - `RuleSet` (default `"changsha"`)
  - `StateJson` (opaque JSON blob)
  - timestamps
- API surface is intentionally minimal:
  - `/api/health`
  - `/api/system/persistence`
- Frontend:
  - `src/frontend/autotable` is a placeholder baseline location.
  - `src/frontend/modern` is an optional React shell, not game logic.
- Docker packages backend + static frontend and mounts `/app/data` for SQLite file persistence.

### Audit conclusion

The current codebase is a **deployment-ready scaffold**, not a gameplay engine.  
It has correct seams for rule-set selection and persistence provider flexibility, but no deterministic game-state machine, no action validation pipeline, no bot runtime, and no anti-cheat/authority boundaries.

## 2) Changsha-specific gameplay requirements to support next

The next increment must convert Changsha rules from prose into explicit state transitions. At minimum:

1. **Authoritative hand lifecycle**
   - seat/wind assignment
   - dealer tracking across rounds
   - deterministic shuffle/deal and initial hand setup

2. **Wall/dead-wall/draw correctness**
   - explicit live wall pointer(s)
   - dead-wall replacement mechanics (if adopted variant requires it)
   - draw exhaustion and draw-end conditions

3. **Turn-state machine**
   - legal transitions: draw -> optional concealed actions -> discard -> response window -> next turn
   - priority resolution for simultaneous claims (win/pung/kong/chow precedence policy)
   - timeout/default-action behavior for online sessions

4. **Changsha win/scoring gates**
   - winning hand eligibility checks
   - fan/point constraints used by product scope
   - dealer/continuation and settlement transitions

5. **Kong and replacement edge handling**
   - exposed/concealed/add-a-tile kong distinctions
   - supplemental draw handling
   - robbing-kong behavior (if enabled by chosen Changsha profile)

6. **Rule-profile versioning**
   - codify a named `changsha-v1` profile so future Chinese variants compose by capability flags, not hard forks.

## 3) Gap analysis vs current scaffold

### Critical gaps (must-have before bot play)

- No domain model for tiles, melds, seats, wall, claims, or round state.
- No command/event model (everything is opaque `StateJson`).
- No authoritative action validator or illegal-action rejection semantics.
- No deterministic RNG strategy for replay/debug/audit.
- No API contract for joining table, starting round, submitting action, or receiving state deltas.
- No bot seat abstraction or action scheduling.
- No observability/audit trail for disputes (who acted, when, on what state hash).

### Moderate gaps (needed shortly after core loop)

- Reconnect/resume semantics and snapshot integrity.
- Rule-variant compatibility matrix and feature-flag guardrails.
- Performance boundaries for concurrent tables and bot think-time budgeting.

### Low/structural gaps

- Frontend baseline is placeholder; gameplay UI integration path exists but is not wired.
- No migration strategy yet from opaque state JSON to strongly-typed round/event storage.

## 4) Bot-play requirements and safety constraints

## Functional requirements

- Bot must use the **same legal action API** as humans (no privileged state mutation path).
- Bot decisions must be generated from server-authoritative visible state + own hidden hand only.
- Bot action submission should pass through identical validator and priority arbitration pipeline.

## Safety/integrity constraints

1. **Server authority**
   - client and bot are both untrusted; only server can mutate authoritative round state.

2. **Determinism and replay**
   - seedable RNG and append-only action/event log required for dispute resolution.

3. **Timing fairness**
   - enforce min/max think windows and default actions; prevent bot starvation/instant unfair loops.

4. **State privacy**
   - strict view projection per seat; no full-state leakage to clients/bots.

5. **Failure containment**
   - bot crash/timeout degrades to safe fallback (auto-pass/auto-discard policy), not table deadlock.

6. **Anti-collusion controls (initial)**
   - no out-of-band communication channels between bot instances.
   - central action logging for anomaly inspection.

## 5) Recommended phased implementation plan (iterative slices)

### Phase 0 — Rule contract freeze (analysis/spec only)

- Produce `changsha-v1` rule contract:
  - state schema
  - legal actions by turn phase
  - precedence table for contested claims
  - end-of-hand and settlement transitions
- Explicitly mark ambiguous Changsha clauses needing product decision.

**Exit:** approved deterministic transition table.

### Phase 1 — Authoritative round engine (human-only, no UI dependency)

- Implement strongly-typed round aggregate + state machine.
- Add command validation and event emission.
- Persist event log + snapshots (can still include JSON snapshot, but typed in memory).

**Exit:** CLI/integration tests can play full hands deterministically with scripted actions.

### Phase 2 — Multiplayer API contract

- Add endpoints/ws contracts for:
  - create/join/start table
  - submit action
  - receive projected state updates
- Add seat-scoped view projections and reconnect support.

**Exit:** two human test clients can complete a hand with server arbitration.

### Phase 3 — Bot seat adapter (rules-safe baseline bot)

- Add bot runtime that consumes projected seat state and submits legal actions through same API.
- Start with deterministic heuristic policy (not ML), instrumented for replay.

**Exit:** 1 human + 3 bots can complete stable full rounds without deadlock/illegal state.

### Phase 4 — Hardening for production-like play

- Add timeout policies, bot failover fallback actions, and per-table health watchdog.
- Add invariant monitors (tile conservation, turn ownership, wall bounds).
- Add metrics: action latency, invalid action rate, bot timeout count.

**Exit:** sustained multi-table simulation with invariant violations at zero.

### Phase 5 — Variant expansion seam

- Introduce capability-flagged rule modules (expanded Chinese variants) against the same engine interfaces.
- Keep Changsha as locked reference profile for regression.

**Exit:** at least one additional Chinese variant enabled without branching/forking core engine.

## Team-level decisions needed soon

1. Confirm the exact Changsha ruleset profile to encode as `changsha-v1` (some clauses differ by room/city customizations).
2. Confirm contested-claim precedence policy and tie-break strategy.
3. Confirm timeout/default-action policy for both humans and bots.
4. Confirm whether “bot transparency” (explicit bot labeling at table) is required for product.

Without these decisions, gameplay-critical paths remain ambiguous.

## Phase 0 contract alignment update

`docs/specs/changsha-v1-contract.md` now defines the Phase 0 executable contract for the first gameplay slice:
- deterministic dealer/opening and draw-discard loop boundaries
- required backend state fields for authority, replay, and invariants
- shared legal-action validator rules for human and bot seats
- explicit end/error semantics and deferred Changsha scope list

Phase 0 completion now maps to implementing this contract exactly (state + validator + replay guarantees) before moving into Phase 1 engine coding.

## Phase 0 implementation progress (current iteration)

Implemented in backend this cycle:
- action-sequence/state-version progression on authoritative actions
- canonical state hashing (`integrity.stateHash`) for deterministic integrity snapshots
- explicit error-code payloads for action rejection, including optimistic concurrency (`CONCURRENCY_CONFLICT`)
- state normalization for persisted snapshots to keep legacy payloads compatible with new integrity fields
- durable append-only event persistence in `TableSessionEvents` with ordered sequence retrieval
- replay verification endpoint (`POST /api/tables/{id}/replay/verify`) that compares expected vs replayed hash

Still pending for full Phase 0 completion:
- hard replay governance (API-level pass/fail policy and automated integrity enforcement hooks)
