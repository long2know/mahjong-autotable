# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture, sequencing, reviewer gates | Kusanagi | Scope trade-offs, design reviews, issue triage |
| Mahjong rules logic and scoring behavior | Ishikawa | Hand validation, ruleset behavior, domain invariants |
| API contracts, DTOs, SignalR events | Batou | Endpoints, payloads, realtime server flow |
| Blazor UX and client integration | Togusa | Lobby/game UI flow, client API wiring, state rendering |
| Test plans and automation | Saito | Unit/integration/e2e coverage, regression suites |
| Build/CI/reliability and release readiness | Borma | Pipelines, run scripts, environment hardening |
| Session logging and decision merge | Scribe | Logs, decision inbox merge, cross-agent propagation |
| Continuous backlog/issue monitoring | Ralph | Work queue scan, assignment nudges, status rounds |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Kusanagi |
| `squad:kusanagi` | Pick up architecture/triage/review work | Kusanagi |
| `squad:ishikawa` | Pick up rules-engine and domain work | Ishikawa |
| `squad:batou` | Pick up API/DTO/SignalR work | Batou |
| `squad:togusa` | Pick up Blazor client work | Togusa |
| `squad:saito` | Pick up testing/QA automation work | Saito |
| `squad:borma` | Pick up DevOps/reliability work | Borma |

## Rules

1. **Behavior changes require tests.** Any rules, flow, or functional behavior update must include or update test coverage in the same change.
2. **API/DTO changes require client + docs sync.** Batou and Togusa must align request/response contracts; docs must be updated before closure.
3. **Non-obvious decisions go to decisions.md.** Team members write to `.squad/decisions/inbox/`; Scribe merges into canonical decisions.
4. **Eager by default.** Spawn all agents who can start independently, including anticipatory test/docs work.
