# Mahjong Claw — Changsha Completion Plan

## Objective
Reach full, production-stable Changsha Mahjong support in this repository by closing rules, API/state, UI, and QA gaps against the referenced Changsha rule set (tile set, xiao hu/da hu, kong events, bird catch, multi-win, dealer/round progression, and settlement).

## Current Baseline (Audit Summary)
- **Implemented core:** 108-tile Changsha set, 2/5/8 eye small-win gate, kong-aware hand shapes, Seven Pairs, All Pungs recognition, basic draw/discard/meld/win loop.
- **Partially implemented:** random-eye eligibility for some big-win shapes, dealer/current turn tracking.
- **Missing major mechanics:** xiao hu family, most da hu event-based variants, bird catch and payout math, claim arbitration, rob-kong/kong-on-cannon, missed-win lock, multi-winner resolution, full settlement ledger, dealer/round transition policy, gameplay e2e coverage.

## “100% Complete and Working” Definition
1. Full Changsha rule parity implemented in engine + runtime flow.
2. Server-authoritative turn/claim state machine with deterministic conflict resolution.
3. Complete settlement with per-seat deltas, dealer modifiers, bird effects, and replayable hand history.
4. Blazor client fully wired for real gameplay (no placeholder action flows).
5. Automated quality gates pass for domain, API integration, realtime ordering, and end-to-end multiplayer flows.

## Phase Roadmap

| Phase | Goal | Key Deliverables | Merge Gate |
|---|---|---|---|
| P0 | Planning + baseline lock | Audits, completion backlog, baseline test pass | Plan approved and merged |
| P1 | Runtime state-machine foundation | Claim windows, action legality model, transition invariants | Illegal transitions prevented by tests |
| P2 | Rules completion (Changsha) | Xiao hu/da hu evaluators, kong/last-tile/missed-win semantics | Rule parity matrix passing |
| P3 | API + persistence completion | Claim APIs, settlement ledger, richer state DTOs/events | Integration tests green |
| P4 | UI gameplay completion | Claim/meld/settlement UX wired to live APIs/events | Full playable hand flow in e2e |
| P5 | Stability + bot + race hardening | Concurrency/race handling, reconnect consistency, bot adaptation | Multi-client stability suite green |
| P6 | Release hardening | CI release gates, soak/perf checks, docs/runbook | Release candidate cut |

## Backlog (Execution IDs)

### P1 — State machine foundation
- **CHG-P1-001:** Introduce explicit action-state graph (draw/discard/claim/resolve/settle).
- **CHG-P1-002:** Add claim window + timeout model and persistence.
- **CHG-P1-003:** Implement discard claim priority resolution policy.
- **CHG-P1-004:** Add transition invariant/unit tests for legal/illegal action paths.

### P2 — Changsha rules completion
- **CHG-P2-001:** Add xiao hu detector set (Big Four Happiness, Banhu, Missing One Suit, Six Six Straight).
- **CHG-P2-002:** Add explicit da hu taxonomy + evaluator (Heavenly/Earthly, All 2/5/8, Pure Suit, Last Tile variants, Luxury Seven Pairs, Fully Reliant).
- **CHG-P2-003:** Model kong event variants (Win after Kong, Robbing Kong, Kong on Cannon).
- **CHG-P2-004:** Implement missed-win lock/unlock semantics.
- **CHG-P2-005:** Add multi-winner-on-discard rule resolution support.

### P3 — API/persistence/settlement
- **CHG-P3-001:** Add settlement ledger entities (hand result, seat deltas, reason codes).
- **CHG-P3-002:** Add bird catch resolution and payout multiplier pipeline.
- **CHG-P3-003:** Expand API contracts for claims, settlement, and hand progression.
- **CHG-P3-004:** Add API integration suite for action conflicts and settlement correctness.

### P4 — UI gameplay wiring
- **CHG-P4-001:** Replace disabled meld flow with live tile-selection/meld declaration UX.
- **CHG-P4-002:** Add claim decision UI (chi/pung/kong/win/pass) with priority/timeout feedback.
- **CHG-P4-003:** Add hand settlement view (winners, birds, deltas, dealer next).
- **CHG-P4-004:** Remove mock-only gameplay dependencies for active games.

### P5 — Stability and multiplayer correctness
- **CHG-P5-001:** Add realtime ordering/idempotency guardrails and tests.
- **CHG-P5-002:** Harden reconnect/resync behavior with state replay.
- **CHG-P5-003:** Expand bot logic to full-rule turn/claim compatibility.

### P6 — Release readiness
- **CHG-P6-001:** Enforce full CI gates for domain + API + e2e gameplay suites.
- **CHG-P6-002:** Add soak/perf checks around event throughput and hand completion.
- **CHG-P6-003:** Ship operator/runbook docs for migrations and rollback.

## QA Release Gates
1. Domain parity vectors for all xiao hu/da hu categories.
2. API integration coverage for every game action happy + negative path.
3. Realtime ordering/reconnect tests with deterministic sequencing checks.
4. End-to-end multiplayer gameplay tests covering claims, kong events, settlement.
5. Full solution build/test and e2e smoke all green on main.

## Branch Execution Strategy
Use short-lived branches per phase slice:
1. checkout `main`
2. create `feature/changsha-completion-{phase}-{slice}`
3. implement scoped backlog items + tests
4. commit, push, PR, merge
5. repeat for next phase until all backlog IDs close
