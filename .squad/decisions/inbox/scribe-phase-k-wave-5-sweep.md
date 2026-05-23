# Scribe — Phase K Wave 5 sweep memo

**Author:** Scribe (Archive) `<scribe@squad.mahjong>`
**Date:** 2026-06-21
**Branch:** `stlong/phase-k-wave-5-bringup`
**Model:** `claude-opus-4.7-xhigh` (Stephen's standing directive)
**Scope:** Fold the 4-file Phase K Wave 5 inbox
(`bishop-` / `hicks-` / `apone-` / `vasquez-phase-k-wave-5.md`)
into canonical `.squad/decisions.md` as a single
`## Phase K — Wave 5` section appended after the Phase K Wave 4
entry; append the Wave 5 closeout entry to
`.squad/agents/scribe/history.md`; capture the W2-W5 cross-lane
bundling trend + the W6 mitigation stack.

---

## Wave 5 gate

| Pass | Fail | Skip | Total | Δ vs Wave-4 baseline (1232) |
|------|------|------|-------|------------------------------|
| **1345** | **0** | **0** | **1345** | **+113** |

Closing invocation: `dotnet test src/backend/Mahjong.Autotable.slnx
--nologo` (1m 39s). **`MaxParallelThreads=2` workaround RETIRED**
this wave — Vasquez's `RegressionHostFixture` (`[CollectionDefinition("regression-host")]`
exposing a shared `WebApplicationFactory<Program>`) eliminates the
W4 cross-class disposal race. Default xUnit parallelism runs green
over multiple consecutive gate invocations.

**Zero-skip streak preserved → 19 consecutive green waves**
(J.1 → J.10 + K.1 + K.2 + K.3 + K.4 + K.5).

---

## Wave 5 commits (11 on branch, 12 with this sweep)

| SHA       | Author             | Subject                                                             |
|-----------|--------------------|---------------------------------------------------------------------|
| `b346157` | Apone (DevOps)     | ci(slsa): unify SLSA+SBOM under multi-subject in-toto predicate **[CROSS-LANE BUNDLED — see Procedural below]** |
| `8b3051f` | Hicks (Frontend)   | Phase K Wave 5 (Hicks) — memo + history log (implementation in `b346157`) |
| `d9209bc` | Apone (DevOps)     | Kyverno attestations block requiring SLSA v1                       |
| `133bb7d` | Apone (DevOps)     | staging `mahjong-jwt-keys` ExternalSecret                          |
| `797bb1a` | Apone (DevOps)     | retroactive `secrets-history-sweep` workflow                       |
| `8adbb05` | Apone (DevOps)     | HSTS preload-readiness probe + sticky-issue alerting               |
| `ec2f042` | Apone (DevOps)     | Terraform bootstrap (13 files — VPC + EKS + RDS + ECR + GH OIDC)   |
| `3625a8c` | Apone (DevOps)     | CHANGELOG 0.14.0 + memo + history                                  |
| `8756667` | Vasquez (QA)       | Wave 5 bring-up tests (21 files / 80+ facts / 9 hard-asserts)      |
| `eb339d7` | Bishop (Backend)   | 8 backend deliverables (auth envelope, JWKS, Prometheus labeled metrics, spectator distinction, TURN-TTL convergence) |
| `4b1c48f` | Bishop (Backend)   | memo + history                                                     |

All 11 commits carry the correct git author per the W4 author-hygiene
preamble. **4/4 author hygiene held this wave.**

---

## What was folded into `.squad/decisions.md`

A new `## Phase K — Wave 5 (production deepening + scene-shell <500 KB
win + auth envelope + JWKS reservation + labeled voice metrics +
SLSA/SBOM unified predicate + Terraform bootstrap + CollectionFixture)`
section appended after the Phase K Wave 4 entry. Subsections:

### Surfaces shipped by lane

**Bishop (7 deliverables, 2 commits):** `AuthTokenResponse`
sealed-record envelope with `Bearer`/`expiresInSeconds`; JWKS
endpoint 404 + `Cache-Control: no-store` reservation for the
Phase L RS256 flip; labeled VoiceHub Prometheus counters keyed
`(table, reason)` + `/metrics` exposition with HELP/TYPE preambles;
spectator-vs-not-seated split via snapshot-presence flag;
`Voice:TurnTtlSeconds` legacy-alias `IStartupFilter` migration
logger with `Volatile.Read`+`Interlocked.Exchange` at-most-once
latch; `docs/api-precedence.md` (NEW); `docs/jwt-rotation.md` §7
refresh (legacy singular `JwtSigningKey` kept one more wave).

**Hicks (4 deliverables, work shipped in `b346157`):**
**scene-shell 886 kB → 2.33 kB (−99.7%) via lazy `three-renderer`
peel** — Wave-2 `<500 kB` target finally closed with 99.5%
headroom (deferred W2 → W3 → W4); retire W3 `game-scene-ready`
back-compat marker; keyboard-accessible sparse-seed reorder
(Arrow/Enter on focusable handle + inline modal dialog + aria-live
announcer); typed `VoiceReason` discriminated union with
`never`-narrowing exhaustiveness guard + dual-entry-point string
boundary wrapper.

**Apone (7 deliverables, 6 commits):** SLSA + SBOM unified under
one multi-subject in-toto predicate via
`generator_generic_slsa3.yml@v2.0.0`; Kyverno `attestations:`
content-pin AND signer-pin belt-and-suspenders;
`mahjong-jwt-keys-staging` ExternalSecret (Wave-N+1 mirroring rule
formalised); `workflow_dispatch`-only retroactive
`secrets-history-sweep.yml`; HSTS preload-readiness cron probe +
sticky-issue alerting pattern; Terraform bootstrap module
(VPC + EKS + RDS + ECR + GH OIDC, 13 files); CHANGELOG `[0.14.0]`.

**Vasquez (7 deliverables, 1 commit):** 9 W4 contract-gap
soft-passes flipped to hard-asserts via
`ContractGapHardAssertW5Tests`; 5 new W5 contract files
(`BishopW5SurfaceTests` / `AponeW5InfraContractTests` /
`HicksW5FrontendContractTests` / `TestShimSanityTests` /
`W5SurfaceSmokeFactsTests`) carrying 80+ facts; regression-class
rename `KW4 → KW5` + 7 W5 smokes appended; **Hudson hand-off
actioned — `RegressionHostFixture` `[CollectionDefinition]` ships
+ `MaxParallelThreads=2` workaround RETIRED**;
`TESTING_SHIM`-gated `TestHttpClientExtensions.WithDirectSession`
(3 overloads — cookie-only / DB-aware / role-stamped, FK-aware);
`docs/agent-handoff-protocol.md` (NEW) formalising stash-checkpoint
discipline; 5 new Playwright specs.

### Bundle-size metrics

| Chunk                          | Wave 4   | Wave 5      | Δ                     |
|--------------------------------|----------|-------------|-----------------------|
| `autotable-src.<hash>.js` (eager) | 218.7 kB | 218.7 kB | unchanged             |
| `game-bootstrap.<hash>.js`     | 169.9 kB | 170.0 kB    | +0.1 kB (`preloadGameBootstrap` warms `three-renderer`) |
| `scene-shell.<hash>.js`        | 886.4 kB | **2.33 kB** | **−884 kB (−99.7 %)** |
| `three-renderer.<hash>.js` (NEW, x2 sub-chunks) | —        | 144.9 kB + 724.7 kB | net renderer transfer ≈ 870 kB (lazy on first game URL) |
| `scene-effects.<hash>.js`      | 59.7 kB  | 59.7 kB     | unchanged             |
| `game-state.<hash>.js`         | 1.9 kB   | 1.9 kB      | unchanged             |

**`scene-shell` <500 kB Wave-2 target met with 99.5% headroom**
after three waves of deferral.

### Gate progression

| Wave  | Test gate (pass/fail/skip) | Δ      | Zero-skip streak |
|-------|----------------------------|--------|------------------|
| K.1   | 1062 / 0 / 0               | —      | 15               |
| K.2   | 1062 / 0 / 0 (TBD recheck) | —      | 16               |
| K.3   | 1152 / 0 / 0               | +90    | 17               |
| K.4   | 1232 / 0 / 0               | +80    | 18               |
| **K.5** | **1345 / 0 / 0**         | **+113** | **19**         |

### Procedural Notes — Cross-lane bundling W2-W5 trend

Cross-lane bundling has now occurred in EVERY Phase K wave:

- **W2:** Bishop's commits absorbed Vasquez + Apone WIP.
- **W3:** Bishop's six backend commits git-authored as Vasquez
  (identity-clobber).
- **W4:** Bishop's `2265de8` swept all 7 Vasquez backend test files
  + regression rename (content-bundling).
- **W5:** Apone's `b346157` swept all of Hicks's frontend
  implementation (content-bundling, OPPOSITE lane direction from
  W4).

**Wave 5 specifics — Apone's `b346157`.** The commit landed
git-authored as `Apone (DevOps) <apone@squad.mahjong>` (author
hygiene preamble held — 11/11 W5 commits correctly authored), but
its file list contains all of Hicks's frontend implementation:
`src/three-renderer.ts` (NEW, 78 lines), `src/scene-shell.ts`
(rewritten, 106 changed), `src/voice.ts` (126 changed),
`src/tournaments.ts` (289 changed), `src/game-bootstrap.ts` (27
changed), `scripts/generate-sw-manifest.js`, `tests/selectors.md`
W5 footer (131 added), plus 11 built artefacts under
`src/frontend/autotable/` (new hashes for `scene-shell`,
`three-renderer` x2, `tournaments`, `voice`, `game-bootstrap`,
`toast`; pruned stale W4 hashes; updated `manifest-precache.json`
+ `index.html`).

**Root cause.** During Apone's commit-tree recovery from a
concurrent agent's `.git/config` race (the `user.{name,email}`
between `git config` SET and `git commit` was rewritten by a
neighbouring agent run between the two steps), Apone's
`git commit-tree` recovery ran against a working tree that already
had Hicks's untracked frontend files staged via an earlier
`git add`. The recovery commit absorbed them. Hicks then re-bundled
differently — committing only the memo + history — once the
working tree was unwedged.

**The W4 author-hygiene preamble fixed IDENTITY at commit-time but
NOT cross-lane CONTENT bundling. Vasquez's W5 stash-checkpoint
discipline (`docs/agent-handoff-protocol.md`) fixes own-work
PRESERVATION but does not stop a concurrent agent's `git add` from
absorbing your untracked files.**

**Production impact.** Zero. Squash-merge collapses per-commit
authors; the PR-level `Co-authored-by: Copilot` trailer is the
canonical attribution surface, and the trailer is preserved on
`b346157` and `8b3051f`. The work content is correctly each
agent's per the inbox memos + histories. Functionally complete.

**W6 mitigation — TWO new disciplines stacking with W4 + W5:**

1. **Per-invocation `git -c user.name=X -c user.email=Y commit ...`**
   instead of `git config user.name X` + later `git commit`. The
   `-c` form is race-safe: identity is bound to the exact `commit`
   invocation and cannot drift between SET and COMMIT.
   **RETIRES the W4 start-of-prompt `git config user.name X`
   step** (which works at the per-invocation level but is
   vulnerable to interleaved agent runs rewriting `.git/config`
   between commits).
2. **`flock /tmp/squad-git-lock git add … && git commit …`
   coordinator-side mutex.** Agents serialise the git-write
   critical section (≤30 s typical) so a concurrent agent's
   `git add` cannot absorb your untracked files between your
   `git add` and your `git commit`. The mutex is held only for
   the `add → status verify → commit` critical section so it
   doesn't serialise the agents' overall work.

**Stack of disciplines after W6:**

| Layer                          | Discipline                                                 | Wave introduced |
|--------------------------------|------------------------------------------------------------|-----------------|
| Identity                       | per-invocation `git -c user.name=… -c user.email=… commit` | W6              |
| Own-work preservation          | `git stash --include-untracked` per logical chunk          | W5 (`docs/agent-handoff-protocol.md`) |
| Cross-agent isolation          | `flock /tmp/squad-git-lock git add … && git commit …` mutex | W6             |

### Vasquez's `docs/agent-handoff-protocol.md` (NEW W5)

Formalises the stash-checkpoint pattern:

1. `git config user.name "<Name>"` + `…email "<addr>@squad.mahjong"`
   locked in BEFORE any work begins (W4 preamble — superseded in
   W6 by the per-invocation `-c` form).
2. `git stash --include-untracked` checkpoint after each logical
   chunk so the work survives a neighbouring agent's `git reset`.
3. Explicit `git add <path>` per file — NEVER `git add -A` — so
   another agent's WIP cannot land in your commit.
4. Per-commit `git log -1 --format='%an <%ae>'` MUST print the
   correct author.

**Scribe recommendation: adopt verbatim in Wave 6 prompts.** Pairs
with (does NOT replace) the W6 race-safe identity binding +
`flock` mutex.

### Hudson hand-off status — actioned (by Vasquez)

Apone's W5 hand-off asked Hudson to ship the `CollectionFixture`
to retire the W4 `MaxParallelThreads=2` workaround. Hudson did NOT
action this in W5 (other Wave-5 priorities — captured in
`docs/test-harness-handoff.md` § "Phase K Wave 5 — addendum"
written by Vasquez). **Vasquez implemented the fixture as part of
the W5 bring-up:**

- `src/backend/tests/Mahjong.Autotable.Api.Tests/Regression/RegressionHostFixture.cs`
  exposes a shared `WebApplicationFactory<Program>` via
  `[CollectionDefinition("regression-host")]`.
- `Wave1ThroughKW5RegressionTests` adopts the fixture via
  `[Collection("regression-host")]` + constructor injection,
  removing the old `IAsyncLifetime` boot per-class.
- W4 disposal race (`ObjectDisposedException` on shared sqlite
  connection when two collections raced teardown) eliminated.
- **`xunit.runner.json` not needed; default parallelism restored.**
- Closing W5 invocation is the plain
  `dotnet test src/backend/Mahjong.Autotable.slnx --nologo` —
  **`MaxParallelThreads=2` workaround RETIRED.**

Hudson's W6 recommended next step (from `docs/test-harness-handoff.md`
addendum): if the regression class grows past ~80 facts, split
into `Wave1ThroughKW5RegressionTests` + sibling
`Wave1ThroughKW5RegressionEnvelopeTests` — both sharing the same
`regression-host` collection.

---

## Patterns locked this wave (15 forward-applicable)

Captured in `.squad/decisions.md` § "Patterns locked this wave"
under the Wave 5 section. Highlights:

1. Lazy renderer peel — monolithic chunks blocking first-paint
   can be peeled into a thin coordinator + lazy renderer if the
   static-import boundary is clean.
2. `sealed record` + per-property `[JsonPropertyName]` + RFC
   literal compile-time constants for externally-consumed JSON
   envelopes.
3. Cache-bypass URL slot reservation (404 + `no-store`) ahead of
   the eventual flip.
4. Prometheus labeled counters with null/empty/whitespace
   normalisation + always-present HELP/TYPE preambles.
5. Snapshot-presence as cheap discriminator between failure modes
   sharing a wire constant.
6. Legacy-alias `IStartupFilter` migration logger with
   `Interlocked` at-most-once latch, shipped ONE wave before
   removal.
7. Generic SLSA generator for multi-subject in-toto predicates.
8. Kyverno `attestations:` content-pin + signer-pin
   belt-and-suspenders.
9. `workflow_dispatch`-only for full-history scans.
10. Sticky-issue alerting for cron probes.
11. Wave-N+1 mirroring rule for prod-only data planes.
12. Terraform manages infra, helm manages workloads — never both
    in one tfstate.
13. `RegressionHostFixture` via `[CollectionDefinition]` as the
    canonical xUnit pattern for shared `WebApplicationFactory`.
14. `TESTING_SHIM`-gated test helpers via
    `<DefineConstants>$(DefineConstants);TESTING_SHIM</DefineConstants>`
    on the test project ONLY.
15. Typed discriminated union with `never`-narrowing
    exhaustiveness guard + dual-entry-point string boundary
    wrapper.

---

## Files staged (selective adds only — NEVER `git add -A`)

```
.squad/decisions.md
.squad/agents/scribe/history.md
.squad/decisions/inbox/scribe-phase-k-wave-5-sweep.md (force-add — this memo)
```

Pre-session untracked files left in place — owned by other
sessions / agents:

- `.copilot/skills/error-recovery/`
- `.github/workflows/squad-*.yml`
- `.tool-actionlint/`
- `.work/`

No code / infra / test changes — other agents own those lanes.

---

## Commit

`git -c user.name='Scribe (Archive)' -c user.email='scribe@squad.mahjong' commit ...`
(race-safe per-invocation form, dogfooding the W6 mitigation
this Scribe sweep). Per-commit verification:
`git log -1 --format='%an <%ae>'` MUST print
`Scribe (Archive) <scribe@squad.mahjong>`.

---

## Phase K Wave 5 — DONE.

Branch ready for PR against `main`. Standing directives reaffirmed
(opus-only, no-pauses), the W4 author-hygiene preamble confirmed
working at the git-author level (11/11 commits this wave), the W5
cross-lane bundling failure mode documented + W6 mitigations
(per-invocation `git -c user.* commit` + `flock` mutex) locked
in. Stash-checkpoint discipline formalised in
`docs/agent-handoff-protocol.md`. Hudson hand-off actioned —
`RegressionHostFixture` ships, `MaxParallelThreads=2` retired.
Zero-skip streak at **19 consecutive waves** (J.1 → J.10 + K.1 +
K.2 + K.3 + K.4 + K.5). Test gate **1345 / 0 / 0** at close
(1232 → 1345 / +113). Bundle-size headline: **scene-shell 886 kB
→ 2.33 kB (−99.7%)** — W2 `<500 kB` target finally closed with
99.5% headroom.
