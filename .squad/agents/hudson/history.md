# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Hudson as Tester.
- Quality focus areas: rule correctness under edge conditions, API contract stability, and end-to-end gameplay regression.

## Work Log

### 2026-04-20: Changsha Test Catalog
**Mission:** Produce comprehensive test scenario catalog for Changsha Mahjong rules derived from source materials.

**Sources Analyzed:**
- MahjongPros.com (S1) — primary, detailed beginner's guide
- Baidu Baike (S2) — scoring specifics and technical rules
- Reddit r/Mahjong (S3) — inaccessible (verification wall)

**Deliverable:** `docs/rules/changsha-test-catalog.md`
- 80 total scenarios across 14 categories (tile set, deal, turn flow, melds, win patterns, scoring, bird catching, state machine, bots, API)
- 47 P0 (critical), 21 P1 (high), 12 P2 (medium) priority
- Every scenario structured: ID, description, setup, trigger, expected, source, priority

**Critical Findings:**
- **8 rule contradictions identified** between sources (see decisions inbox)
- **Top blockers for implementation:**
  1. Bird tile count: 1 vs. 2 tiles drawn (scoring impact)
  2. Scoring model: 1/6/7 point vs. 10/20/60/70 point system (fundamental)
  3. Multiple win resolution: single winner (proximity) vs. all win simultaneously
  4. Starting instant win continuation: hand ends vs. hand continues

**Skeptical Notes:**
- Baidu source references "Red Dragon" for dealer tiebreaker, but Changsha excludes all dragons from tile set—self-contradiction suggests adaptation error from another variant
- S1 emphasizes "258 Generals" pair requirement, S2 adds "random eye" exemptions for Big Wins—both valid but need clear hierarchy
- Kong replacement draw rules vary significantly between sources (dice optional vs. gated by ready state)

**Decision Record:** `.squad/decisions/inbox/hudson-changsha-test-catalog.md` created for Vasquez to resolve contradictions.

**Status:** Catalog complete. Awaiting spec resolution before test code generation. Ready to convert scenarios to xUnit/NUnit tests once rules finalized.

📌 Team update (2026-05-05T17-00-21Z): Test catalog decision merged to `.squad/decisions.md`. Vasquez completed Changsha canonical spec at `docs/rules/changsha-spec.md` addressing most rule areas; 11 open questions and 8 contradictions identified in catalog require product direction. Bishop completed backend audit at `docs/rules/changsha-backend-gap.md` (3/18 implemented, 10 missing). Hicks completed frontend plan at `docs/rules/changsha-frontend-plan.md` (Option B selected). Ready to convert 80 scenarios to code tests once high-priority contradictions resolved by Vasquez.
