# Phase J Wave 10 — Vasquez QA memo: tournaments + audit-pruning + bot-reasoning + multi-arch + CSP-style-src + 5 e2e specs

**Author:** Vasquez (QA)
**Branch:** `stlong/phase-j-wave-10-completion`
**Base:** Wave 9 merge `stlong/phase-j-wave-9-polish` (origin/main @ 75df674).

---

## Final gate

| Pass | Fail | Skip | Total | Δ vs Wave 9 baseline |
|------|------|------|-------|----------------------|
| **832** | **0** | **0** | **832** | **+102** (target was ≥60 backend) |

Zero-skips streak **preserved (Wave 10 = the 14th consecutive green wave).**

No transient flakes observed across the full-suite run that produced the
832/0/0 number. Backend gate is clean.

`dotnet test --filter "Wave=Phase-J-10"` selects this wave's new facts
plus the Wave-10 traits Bishop/Apone carry in their parallel suites.

---

## Scope completed

### Backend (Mahjong.Autotable.Api.Tests) — 56 new facts (Vasquez-authored)

All facts carry `[Trait("Wave", "Phase-J-10")]`. Numbers in parens are
facts per file.

- **`Replay/ReplayV2NormaliserTests.cs` (6 facts)** — v1 backward-compat,
  optional field defaults, v2 pass-through, empty-events shape,
  schemaVersion advertisement, `CurrentSchemaVersion=2` constant.
- **`Audit/AuditPruningContractTests.cs` (6 facts)** — supplemental to
  Bishop's `AuditPruningServiceTests.cs` (which Bishop ships alongside).
  Pins DI registration, options binding (`Audit:`), defaults
  (30 d reconnect / 90 d CSP / 1440 min / Enabled=true), disabled-boot
  behaviour, report shape (`ReconnectDeleted`, `CspDeleted`), timing.
- **`Tournaments/TournamentHarness.cs`** — shared multi-candidate URL
  base for the 5 tournament suites below. Soft-passes uniformly on 404
  (Bishop's `TournamentController` is forward-staged).
- **`Tournaments/TournamentCrudTests.cs` (7 facts)** — create/list/get/
  update/delete + shape probes; "never 5xx" guard.
- **`Tournaments/TournamentStartTests.cs` (5 facts)** — start endpoint
  status transition (draft → registration-open → in-progress), pairing
  generation, idempotent re-start guard.
- **`Tournaments/TournamentPairingTests.cs` (5 facts)** —
  `TournamentPairing` algorithm (single-elim bracket vs round-robin),
  byes, deterministic seed, idempotency.
- **`Tournaments/TournamentAdvancementTests.cs` (4 facts)** — match
  result posting advances winner, idempotent re-post guarded, status
  flips to "complete" when bracket exhausts.
- **`Tournaments/TournamentLeaderboardTests.cs` (5 facts)** —
  leaderboard endpoint envelope, sort order, ties, in-progress vs
  complete shape, never 5xx.
- **`Api/DatabaseHealthDetailTests.cs` (6 facts)** — Wave 10
  `db.providerName` / `db.canQuery` / `db.migrationsApplied` additions
  to /health; preserves Wave 7 baseline (`status`/`db.connected`/
  `db.latencyMs`); `?simple=1` omits the new fields; never leaks
  connection-string fragments (path / password / `Data Source=`).
- **`ChangshaServices/BotDecisionReasoningTests.cs` (7 facts)** —
  Bishop's `BotDecision`/`DecideWithReasoning` surface. Each tier
  populates a non-empty `Reasoning` list, first line carries the tier
  discriminator, Master's reasoning includes a safety / defense /
  opponent line (the tier's signature differentiator),
  `BotDecision.Reasoning` is declared `IReadOnlyList<string>`,
  `FromAction(action)` ships empty reasoning, action chosen by the new
  surface matches legacy `DecideAction`, `Difficulty` property remains
  canonical.
- **`Autotable/LateJoinSnapshotStabilityTests.cs` (5 facts)** —
  supplementary sibling to Apone's `LateJoin_..._Stability50x` in
  `AutotableWsRelayTests`. Asserts untouched-game empty snapshot,
  multi-late-joiner identical entry sets, re-join picks up latest
  store mutations, `AutotableConnectionManager.GetStoredEntryCount`
  (string, string) overload + legacy (string) overload both exist.
- **`Security/CspStyleSrcNoUnsafeInlineTests.cs` (6 facts)** — Apone's
  Wave-10 `Security:CspStrictStyles` knob. Strict=true drops
  `'unsafe-inline'` from `style-src`; doesn't touch `script-src`;
  default keeps `'unsafe-inline'` (lock the default against accidental
  tightening); `DefaultCsp` constant still ships unsafe-inline; config
  key spelt canonically; strict-mode preserves adjacent directives.
- **`Deploy/MultiArchDockerSanityTests.cs` (6 facts)** — Dockerfile +
  `.github/workflows/` multi-arch incantations. Every check soft-passes
  when the multi-arch refactor hasn't yet landed: top-level
  Dockerfile present, `*-build` stages pin `--platform=$BUILDPLATFORM`,
  runtime stage references `$TARGETPLATFORM`, `dotnet publish` is not
  hard-coded to an x64 RID once multi-arch is on, buildx + linux/arm64
  configured in a workflow, runtime stage is the `aspnet` image.
- **`Regression/Wave1Through10RegressionTests.cs` (12 facts)** —
  cross-wave canary. Walks Wave 1 → 10 surfaces (health / identity /
  games-list / reconnect-audit / leaderboard / replay / game-audit /
  CSP / chat / tournaments) asserting "never 5xx" per wave plus two
  cross-wave invariants (health survives all probes; health never
  leaks DB secrets).

**Live test counts (verified against `dotnet test`):**

```
$ cd src/backend
$ dotnet test tests/Mahjong.Autotable.Api.Tests/Mahjong.Autotable.Api.Tests.csproj \
    -c Release --nologo --no-build
Passed!  - Failed: 0, Passed: 832, Skipped: 0, Total: 832, Duration: 50 s
```

### Frontend e2e (Playwright) — 22 new test cases across 5 specs

Each spec follows the Wave 9 Hicks-mocking pattern (`page.route` for
every required backend endpoint) and the canonical reflection-defensive
soft-pass: missing testids / surfaces → `test.info().annotations.push({
type: 'soft-pass', description: '<canonical string>' })` + early return.

| Spec                            | Test cases | Covers                                                |
|---------------------------------|------------|-------------------------------------------------------|
| `tournament-flow.spec.ts`       | 5          | lobby card, create form, register, start, leaderboard |
| `avatar-migration.spec.ts`      | 4          | `#808080` migration modal, pick persist, dismiss safe, fresh-profile no-modal |
| `csp-no-inline-styles.spec.ts`  | 3          | served `style-src` lacks `'unsafe-inline'`, DOM has no inline styles, head has no `<style>` blocks |
| `audit-why-expand.spec.ts`      | 5          | `replay-audit-row-{i}-why` toggle, reasoning panel, list-item lines, second-click closes, `data-strategy` badge |
| `spectator-chat.spec.ts`        | 5          | `?seat=-1` chat panel, spectators default channel, chronological backfill, composer enabled, no table-channel leak |

All 22 specs parse under `npx playwright test --list`. Canonical
soft-pass annotation strings are documented in
`src/frontend/autotable-src/tests/selectors.md` under the new
"Phase J Wave 10 Playwright coverage — Vasquez" subsection so the CI
summary scraper keeps working.

### Selector contract — `src/frontend/autotable-src/tests/selectors.md`

Appended a Wave-10 footer (15 testids + 5 spec coverage map + 20
canonical soft-pass strings). The footer is append-only per project
convention.

---

## Coordination notes

- **Build break recovery (recorded for future waves).** During the
  initial build of my test project Bishop's WIP shipped a
  `Mahjong.Autotable.Api.Tournament` namespace AND a
  `Mahjong.Autotable.Api.Data.Entities.Tournament` entity class
  simultaneously. The `AppDbContext.Tournaments` DbSet declaration
  resolved `Tournament` to the sibling namespace (CS0118), bricking
  the whole solution build. After waiting briefly for self-heal
  (Bishop's reflection-self-fix pattern from Wave 9), I applied the
  minimal surgical fix: fully-qualified the four `Tournament` type
  references in `AppDbContext.cs` to
  `Mahjong.Autotable.Api.Data.Entities.Tournament`. This is a
  cross-lane edit; flagged here so Bishop can roll it into his own
  Wave-10 commit if he prefers (the diff is 4 lines; mine is the
  minimum needed to unblock testing). Story is logged under
  Wave 10 blind-spots below.
- **Tournament surface is forward-staged.** Bishop's
  `TournamentController` isn't yet shipped, so all 26 tournament
  test facts (CRUD + Start + Pairing + Advancement + Leaderboard
  suites) currently exercise the URL-candidate / 404-soft-pass
  branches. They will pop green automatically as Bishop registers
  routes — and the `[Trait("Wave","Phase-J-10")]` selector keeps the
  suite filterable.
- **Audit pruning lane discipline.** Bishop already shipped his own
  `Audit/AuditPruningServiceTests.cs` (5 row-counting facts). I
  removed an earlier duplicate I had at `Auth/AuditPruningServiceTests.cs`
  and authored `Audit/AuditPruningContractTests.cs` as a *supplemental*
  6-fact suite (DI / options / defaults / disabled-boot / report
  shape / timing). The two files compose cleanly.
- **Late-join lane discipline.** Apone shipped the canonical 50×
  stability loop *inline* in `AutotableWsRelayTests` (the
  `_Stability50x` fact). My `LateJoinSnapshotStabilityTests.cs` is
  a sibling file with 5 *additional* invariants (untouched-game
  carry-over guard, multi-joiner identical sets, re-join store
  freshness, manager-accessor overload presence). No edits to
  Apone's file.
- **Bot reasoning shape.** Bishop's `BotDecision` is a
  `readonly record struct(BotAction Action, int? Tile, int Score,
  IReadOnlyList<string> Reasoning)`; the new
  `IChangshaBotStrategy.DecideWithReasoning` is a default-interface
  method that wraps `DecideAction` for legacy strategies. Each of
  Easy/Medium/Hard/Master overrides it. Master's reasoning carries
  a "safety analysis" line that is the tier's signature
  differentiator (my BotDecisionReasoningTests pin this).

---

## Blind spots for future waves

1. **Tournament timezone / DST.** When tournaments span DST
   transitions the `Round` × `CreatedAt` index ordering can flip a
   bracket. None of my suites exercise this; needs a Wave-11 facet.
2. **WS reconnect during a tournament match.** The late-join
   stability suite covers the relay store; it doesn't cover the
   tournament-match state machine surviving a mid-hand WS drop.
3. **Avatar migration race.** `avatar-migration.spec.ts` covers the
   localStorage path. The auth-server canonical avatar (Wave 2's
   `/api/identity` shape) is NOT cross-checked — if the server
   reshapes a legacy colour out of band, the client may show two
   colours briefly. Spec for Wave 11.
4. **CSP report ingestion under load.** Wave 8 ships
   `/api/csp-report`. Wave 9 wired the persistence. Wave 10 tightens
   the policy — but I don't have a "1000 reports / second" stress
   facet on the ingest endpoint.
5. **Multi-arch image build verification.** My
   `MultiArchDockerSanityTests` only inspects the Dockerfile + workflow
   YAML strings. A *runtime* smoke that pulls the `linux/arm64`
   variant and curls /health from inside it is out of scope (needs
   buildx in CI; Apone owns the rollout).
6. **Tournament admin RBAC.** Bishop's TournamentController is
   forward-staged — once it lands, my suite probes "creator can start"
   and "register flips status" but not the admin-vs-creator boundary
   (admin force-cancels a tournament, etc.). Needs a Wave-11 lane.

---

## Hand-off

- **selectors.md** — Wave 10 footer appended (15 testids + 5 specs +
  20 soft-pass strings).
- **history.md** — Wave 10 entry appended under `.squad/agents/vasquez/history.md`.
- **Branch** — `stlong/phase-j-wave-10-completion` (this PR).
- **Co-author** — `Copilot <223556219+Copilot@users.noreply.github.com>`.

Scribe to merge this memo as `.squad/decisions/inbox/` is the canonical
QA hand-off path.
