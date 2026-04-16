# Squad Decisions

## Active Decisions

### 2026-04-16: Behavior-changing work requires tests
**By:** Stephen Long (via Squad setup)
**What:** Any gameplay/rules/API behavior change must include test additions or updates in the same workstream.
**Why:** Prevent regressions and keep confidence high as rules and realtime flows evolve.

### 2026-04-16: API/DTO changes require client and docs synchronization
**By:** Stephen Long (via Squad setup)
**What:** Any API contract or DTO update must include corresponding client update and docs update before completion.
**Why:** Keep server/client compatibility intact and avoid stale integration docs.

### 2026-04-16: Non-obvious decisions must be captured in Squad memory
**By:** Stephen Long (via Squad setup)
**What:** Team members record non-obvious technical/process decisions in `.squad/decisions/inbox/*.md` for Scribe merge.
**Why:** Preserve rationale for future sessions and reduce repeated re-discovery.

## Governance

- Keep decisions concise and implementation-facing.
- Prefer append-only entries; avoid rewriting prior decision intent.
- Route unresolved trade-offs to Kusanagi for lead review.
