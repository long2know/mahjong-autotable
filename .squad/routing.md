# Work Routing

How to decide who handles what.

## Routing Table

| Work Type | Route To | Examples |
|-----------|----------|----------|
| Architecture, scope, and cross-team decisions | Ripley | Feature decomposition, interface contracts, risk trade-offs |
| Backend, APIs, persistence, and integration | Bishop | .NET 10 services, EF Core models, SQLite migrations, provider abstraction |
| Frontend and interaction layer | Hicks | Table UI behavior, client integration, optional React/Fluent/Vite migration slices |
| Changsha and variant rule logic | Vasquez | Wall setup/draw rules, turn flow, variant compatibility boundaries |
| Code review and quality gate | Hudson | Review implementation quality, reject/approve with revision guidance |
| Testing and QA | Hudson | Rules validation cases, regression suites, gameplay scenario checks |
| Scope and priorities | Ripley | What to build next, sequencing, change impact |
| Session logging | Scribe | Automatic — never needs routing |
| Backlog and work monitoring | Ralph | Board checks, issue/PR pickup loops, idle/watch control |

## Issue Routing

| Label | Action | Who |
|-------|--------|-----|
| `squad` | Triage: analyze issue, assign `squad:{member}` label | Ripley |
| `squad:ripley` | Pick up lead/architecture/scope issues | Ripley |
| `squad:bishop` | Pick up backend/API/persistence issues | Bishop |
| `squad:hicks` | Pick up frontend/client behavior issues | Hicks |
| `squad:vasquez` | Pick up rules and variant logic issues | Vasquez |
| `squad:hudson` | Pick up testing and reviewer-gate issues | Hudson |
| `squad:scribe` | Pick up logging/decision-hygiene tasks | Scribe |
| `squad:ralph` | Pick up monitoring/triage automation tasks | Ralph |

### How Issue Assignment Works

1. When a GitHub issue gets the `squad` label, the **Lead** triages it — analyzing content, assigning the right `squad:{member}` label, and commenting with triage notes.
2. When a `squad:{member}` label is applied, that member picks up the issue in their next session.
3. Members can reassign by removing their label and adding another member's label.
4. The `squad` label is the "inbox" — untriaged issues waiting for Lead review.

## Rules

1. **Eager by default** — spawn all agents who could usefully start work, including anticipatory downstream work.
2. **Scribe always runs** after substantial work, always as `mode: "background"`. Never blocks.
3. **Quick facts → coordinator answers directly.** Don't spawn an agent for "what port does the server run on?"
4. **When two agents could handle it**, pick the one whose domain is the primary concern.
5. **"Team, ..." → fan-out.** Spawn all relevant agents in parallel as `mode: "background"`.
6. **Anticipate downstream work.** If a feature is being built, spawn the tester to write test cases from requirements simultaneously.
7. **Issue-labeled work** — when a `squad:{member}` label is applied to an issue, route to that member. The Lead handles all `squad` (base label) triage.
