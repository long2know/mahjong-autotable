# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings

- Team initialized with Ripley as Lead.
- Immediate focus is aligning Changsha rule flow with autotable interaction and backend contracts.
- Established a two-track frontend structure: `src/frontend/autotable` (baseline) plus optional `src/frontend/modern` (React + Fluent UI 9 + TS + Vite).
- Created backend foundation at `src/backend/src/Mahjong.Autotable.Api` with EF Core provider switching (`Sqlite`, `PostgreSql`, `SqlServer`) via `Persistence:Provider`.
- Standardized local startup with VS Code configs in `.vscode/launch.json` and `.vscode/tasks.json`, including one-key full-stack F5 compound.
- Added single-image Docker targets in `infra/docker/Dockerfile`: `runtime-autotable` and `runtime-modern` for incremental UI rollout.

## Pivot Plan Synthesis (2026-05-13)

Synthesised Bishop's backend salvage inventory, Hicks's TS modification map, and Vasquez's rules diff manifest into a single phased pivot plan at `.squad/decisions/inbox/ripley-pivot-plan.md`. Stephen's binding directive (2026-05-13T23:06Z) ordered vendoring pwmarcz/autotable in-tree, implementing Changsha rules directly in its TS, having the .NET backend speak autotable's native `NEW`/`JOIN`/`JOINED`/`UPDATE` WS protocol, and deleting Strategy C, the React/Fluent UI 9 SPA, the SignalR `/hubs/changsha` surface, and the iframe-bridge.

### Approach
- **Five-phase shape (A–E)** that ships *something playable* after each phase: A = vendor + cruft delete (~5,000 LOC out, stock Riichi bundle still works); B = tile/asset Changsha-shaping (sandbox playable, no rules); C = backend WS-native + per-viewer privacy filter + inbound-UPDATE validation (4-bot Changsha hand end-to-end); D = rules graft via custom collections (`changsha.claim`/`scoring`/`banker`/`lifecycle`) + claim/Hu overlays + 过胡 + chow disambiguation (human-vs-bots playable, Changsha v1 lock); E = lobby-in-sidebar + Docker single-image + docs sweep.
- **15 consolidated decisions** for Stephen (combining Bishop's 8, Vasquez's 9, and Hicks's 10 — collapsed by topic). Every decision carries an opinionated default + a switching-cost grade. The two non-negotiables I framed firmly: per-viewer `things` filter (decision 6 — without it, hands leak via DevTools) and custom collections for the 6 non-carrier events (decision 7).

### Opinionated picks
- **Vendor as in-tree fork** at `src/frontend/autotable-src/`, not a submodule. Upstream rejects non-Riichi PRs anyway; submodule footgun for CI / Copilot agents is real.
- **Keep upstream Parcel 2.15**; don't migrate to Vite. The "consolidate to one bundler" pressure disappears with the React app's deletion in Phase A.
- **Path 1 lobby** — autotable's existing sidebar IS the lobby. No React survives in v1.
- **Collapse `GameType` to `CHANGSHA` only** (Vasquez Q2). Carrying legacy variants as dead enums invites drift.
- **Claim labels: Chinese primary + pinyin sublabel** (`碰 Pung` etc.).
- **Drop deal-acks** — autotable has no ack concept; turn 1 starts on a fixed server timeout after the last deal UPDATE.
- **Hard-delete the entire legacy `Tables/*` + `/api/tables/*` REST surface in Phase A** (Bishop D5 default), not later. No surviving frontend consumes them; their tests go red whenever the controllers leave, so doing it in Phase A consolidates the cruft sweep.
- **Defer persistence-on-restart hydration and replay-integrity verifier** to v1.1 — both are pre-existing gaps unrelated to the pivot.

### Key tradeoffs and risks captured
- **Top-3 critical risks:** (1) hand-tile privacy at protocol layer (Hicks R1 — server-side filter is mandatory); (2) six runtime events have no native carrier (Bishop's #1 — custom collections); (3) inbound-UPDATE validation engine doesn't exist yet (Bishop's #4 — adds ~80 LOC `ChangshaStateMachine.ValidateInboundMove` as Phase C prerequisite).
- **Drag-to-claim vs button-driven claims**: I defaulted to buttons + custom `changsha.claim` collection (decision 7) because Hicks R2 flagged drag-only as having irreducible race / ambiguity. Listed drag-only as cut-3 in the MVP-narrowing section for Stephen's awareness.
- **MVP-faster cuts I'd accept:** (1) defer multiplayer ("1 human + 3 bots, single backend instance"); (2) drop the explicit claim window and ship drag-to-meld with a first-valid-drag arbiter; (3) defer Vasquez's two conformance gaps (chow tile-IDs, 过胡) to v1.1. All three are reversible without architectural debt.

### Net delta projected
~6,000 LOC source + ~1,300 LOC tests deleted (Phases A + C + E). ~1,500 LOC TS added (Phases B + D). ~600 LOC backend repointed (Phase C). Pure-rules Bucket A (2,500 LOC src + 2,800 LOC tests) survives untouched. Plan is in the inbox awaiting Stephen's batch-answer.

### F5 dev-loop amendment (2026-05-13T23:15Z follow-up)

Stephen bolted a binding constraint onto the pivot mid-flight (`copilot-directive-2026-05-13T2315Z-f5-dev.md`): *"Also - keep in mind I still want to be able to launch the app with vscode.."* The original plan killed `src/frontend/modern/` (and its Vite dev server) in Phase A without spelling out how the F5 dev loop survives. I amended the plan in-place (no new inbox file) with four surgical additions: (1) a **Local Dev (F5) story** subsection inside Phase A specifying the new compound launch (`F5 Full Stack (Backend + Autotable)` boots `.NET Backend` + a new `Autotable (Parcel watch)` node-terminal config in parallel), the new `autotable: watch` task wrapping upstream's `make parcel` (or `parcel watch --dist-dir build/`), the **byte-identical preservation** of PR #27 + #28's PATH augmentation, the natural retirement of `src/frontend/modern/vite.config.ts`'s proxy when `modern/` dies, and a recommendation to default to pure `parcel watch` (no HTTP server, no port-1234 dependency — backend serves `build/` verbatim) with the `make parcel` + HMR-shim alternative scoped as v1.1 polish; (2) a new **Q16 (F5 dev shape)** at the end of §4 — defaulted to compound launch — inserted at the END rather than between Q1 and Q2 to avoid breaking the ~6 ordinal cross-references ("decision 1", "decision 6", "decision 14", etc.) that pepper §5/§6/§7; (3) a `.vscode/launch.json` + `.vscode/tasks.json` bullet in Phase A's Deliverables block explicitly calling out PATH-augmentation preservation; (4) an F5-regression sentence appended to Phase A's Exit criteria — fresh checkout + one-time `dotnet restore` + one-time `npm install` = working `/autotable/` page on F5 with zero further manual steps. Verified by re-reading the relevant sections after edit; no other section of the plan touched.

## Architectural Pivot — Phase A SHIPPED (2026-05-13)

**Branch:** stlong/autotable-vendored-pivot (merged to main @ 55d8dfb)
**Timestamp:** 2026-05-13T23:05Z
**Contribution:** Synthesized 5-phase pivot plan from parallel inventories (Bishop/Hicks/Vasquez), articulated 16 numbered architectural defaults (vendoring, bundler, lobby, claims, privacy, etc.), drafted 3 MVP fast-cuts (single-game-per-instance, drag-to-meld, defer Vasquez gaps), incorporated F5 dev-loop constraint amendment (compound launch + Parcel watch task). Plan awaited Stephen's batch-decision; after acceptance, Phase A executed in parallel by Bishop + Hicks.
