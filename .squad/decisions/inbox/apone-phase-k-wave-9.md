# Apone — Phase K Wave 9 decision memo

> Author: Apone (DevOps)
> Date: 2026-07-23
> Branch: `stlong/phase-k-wave-9-bringup`

## Mission

Wave 9 production-hardens the W8 surfaces that opened the door
for prod traffic: the W8 staging-canary becomes a prod-canary
with sharper gates; the W8 mobile production-release picks up a
hotfix sibling; the W8 path-confusion guard generalises into a
cross-file invariant audit. The wave also lands two
infrastructure-of-the-squad items: YAML symbolic anchors in the
values overlays (so a hostname change touches one line, and so
doc-cross-references survive renumbering), and a rebase-inside-
flock pattern + lock-file relocation plan (closes the W8 retro's
non-fast-forward race + the `/tmp/squad-git-lock` ephemerality
problem).

Six DevOps-lane deliverables:

1. **Prod canary 3-template retarget** — refactor the W8
   single AnalysisTemplate (`success-rate`) into three
   independent gates (`success-rate` + `p99-latency` +
   `error-budget` burn rate). The Rollout's analysis step
   references all enabled templates; any one failing aborts.
2. **Mobile production-hotfix workflow** —
   `mobile-hotfix-v*.*.*` + `workflow_dispatch` workflow that
   bypasses External-Testing with explicit operator
   acknowledgement, two-reviewer env-gate, and three durable
   audit-trail markers.
3. **Cross-file invariant audit** — generalise the W7 signer-
   identity guard pattern into `scripts/check_invariants.py`;
   first new binding is JwtRsaKeys ↔ ESO Secret name + SSM
   path + env-var prefix across 7 surfaces.
4. **YAML symbolic anchors in values overlays** — `x-anchors:`
   block at the top of `values-{staging,prod}.yaml`,
   `*name` references throughout, doc cross-references switch
   from numeric (`§3.5`) to symbolic (`§canary-analysis`).
5. **Rebase-inside-flock + lock-file relocation plan** —
   `docs/agent-handoff-protocol.md` §3.6 (lock-file `/tmp/` →
   `.work/` cutover plan, W10 cutover wave) + §3.7 (`git
   fetch + rebase` inside the flock critical section).
6. **CHANGELOG 0.18.0 + this memo + history.md entry.**

## Decisions

### 1. Three independent canary gates, not one composite

**Decision.** The W8 single `success-rate` template becomes
THREE templates (`success-rate` + `p99-latency` +
`error-budget`). Argo Rollouts evaluates them in parallel; ANY
single failure aborts. No aggregation logic in the chart.

**Why.** A composite metric (e.g. "weighted score of success-
rate AND latency AND budget") obscures WHICH dimension broke.
The post-mortem question "did the canary regress on latency or
on errors?" requires looking at the underlying templates
anyway — the composite is pure overhead. Three independent
gates also let an operator disable one (e.g. no latency
histogram instrumentation yet) without removing the gate
entirely.

**Defaults (in `helm/mahjong/values-prod.yaml`).**

- `successRate.threshold: 0.99` (99% non-5xx over 5m rolling)
- `p99Latency.threshold: 500` (ms; `histogram_quantile(0.99, ...)
  * 1000`)
- `errorBudget.threshold: 14.4` (Google SRE 2%/1h fast-burn
  alert threshold against `sloErrorRate: 0.01`)
- All three: count=10 × 30s interval = 5m window, failureLimit=1

The 14.4 number is canonical SRE Workbook (2% of monthly budget
burned in 1h, at SLO=99%). Crossing it inside 5m means
continuing the rollout would exhaust the budget faster than
on-call can respond → abort.

### 2. Hotfix workflow uses a SEPARATE 2-reviewer environment

**Decision.** `mobile-production-hotfix.yml` is env-gated on
`release-channel-production-hotfix` (NEW environment, two
required reviewers). NOT on the routine `release-channel-production`
(one reviewer).

**Why.** Skipping the External-Testing 7-day soak window
demands a second pair of eyes on the **decision to skip**, not
just on the output. The output gate is the same in both
workflows (build → submit). The decision gate is what matters.

**Audit-trail guarantees** (THREE durable markers per run):

1. `::warning::HOTFIX PATH — External-Testing skipped. Reason:
   <reason>. Reviewers: <list>` log line in the Actions UI.
2. `step-summary` banner with the hotfix reason rendered at
   the top of the run page (markdown).
3. Slack notification on `#mobile-releases` with the hotfix
   reason embedded.

No single marker is sufficient — audit reviewers should not
need to dig through the Actions log to reconstruct WHY a cut
bypassed soak. The `hotfix_reason` input is non-empty validated
on `workflow_dispatch`; the tag-push path reads it from `git
tag -a mobile-hotfix-v<x.y.z> -m "<reason>"`.

**Default rollout posture: 100% Android (not staged).** A
hotfix worth skipping soak is worth fully replacing the broken
build immediately. Staged rollout is the wrong default for an
RCE or revenue-blocking bug. The operator can override via the
`android_rollout_fraction` input.

### 3. `scripts/check_invariants.py` is the extension point for future cross-file invariants

**Decision.** New script that generalises the W7 signer-
identity guard pattern. Single `INVARIANTS` tuple at module
level; new bindings just declare an `Invariant` constant and
append. Wraps the W7 script via **subprocess** (NOT `import`)
so a stack-trace in one doesn't pollute the other and so each
script remains independently runnable / debuggable.

**Why a separate script (not `check_signer_identity.py`).**
The W7 script owns its canonical-regex storage + six-file
extractor set + path-confusion guards. Bolting more bindings
on conflates concerns and breaks the W7 history doc reference.

**Why subprocess (not import).** Re-running via
`subprocess.run([sys.executable, "scripts/check_signer_identity.py"])`
keeps a hard process boundary — a Python exception in one
doesn't pollute the other's traceback. Also lets us point
pre-commit at one of the two with `--skip-signer-identity` when
needed.

**W9 ships one binding: JwtRsaKeys.** The RS256 fallback
analogue of the W5 HS256 drift incident. Audits 7 surfaces with
exact-value + min-count assertions (see history.md W9 §1.3).

### 4. YAML anchors live under `x-anchors:` top-level key

**Decision.** Per-env scalars (hostname, TLS secret, env name,
CORS origin, Prometheus endpoint) declared once under a top-
level `x-anchors:` block; consumers reference via `*name`.

**Why the `x-anchors:` namespace specifically.** Helm ignores
unknown top-level keys; `x-*` is the de-facto OpenAPI /
docker-compose / GitHub Actions convention for "extension /
ignored / for-humans-only". The chart-of-charts merge accepts
it without rendering — verified via `helm template` + PyYAML
`safe_load_all` round-trip.

**Doc cross-references switch numeric → symbolic.** Values-
file docstrings reference `§canary-analysis`, `§parity-matrix`,
`§yaml-anchor-pattern`, `§subchart-toggles` — `docs/helm-charts.md`
adds matching `<a name="...">` HTML anchors. Section renumbering
(which the W8 → W9 transition just did, adding three new
sections) no longer breaks the references.

**When NOT to use anchors.** Subchart values that need per-
overlay distinct typing (string vs list); single-occurrence
values where the anchor declaration is pure overhead; inside
subchart values files (umbrella merge semantics interact poorly
— keep anchors at the overlay level).

### 5. Lock-file `/tmp/squad-git-lock` → `.work/squad-git-lock` is W10, not W9

**Decision.** W9 KEEPS `/tmp/squad-git-lock` (per the task
brief). W10+ canonical is `.work/squad-git-lock`. The migration
is one-wave: every agent prompt template flips the path in the
W10 onboarding.

**Why W10, not W9.** Mid-wave migration would DEFEAT the
mutex: two agents holding two different lock files would race.
The path is uniform per wave by design.

**Why migrate at all.** Three problems with `/tmp/`:

1. **Ephemeral.** `/tmp/` is wiped on reboot and (on some
   runtimes) inactivity. Second agent that comes online
   between the wipe and the next squad session creates a
   brand-new lock instead of attaching — losing serialisation
   exactly when it matters.
2. **World-writable.** Non-squad processes may hold unrelated
   flocks against `/tmp/squad-git-lock` (watchdog scripts that
   grab every `*.lock` in `/tmp/`).
3. **Hard-prohibition.** Several agent runtimes block writes
   under `/tmp/` (Scribe / Vasquez noted in W8 retros) — so
   the lock file silently never gets created and the flock is
   a no-op.

`.work/` is gitignored except for `.work/.gitkeep`. The
`.gitkeep` materialises the directory on a fresh clone so
`flock 9>.work/squad-git-lock` doesn't fail on missing parent.

### 6. `git fetch + rebase` happens INSIDE the flock

**Decision.** The canonical W9+ commit pattern adds `git fetch
origin <branch>` + `git rebase origin/<branch>` between the
local commit and the push, INSIDE the flock critical section.

**Why inside, not outside.** Outside (e.g. `git pull --rebase`
before acquiring the lock), there'd be a window where the lock
is acquired but the local branch is stale — another agent
could fetch + rebase in parallel, and both agents would
converge to push the same stale tip.

**Why the rebase matters at all.** The flock serialised the
local critical section but NOT the network's view of the
branch tip. Non-squad push (Stephen amending a PR off-flock) or
a pre-flock push that landed between our last fetch and our
local commit would cause non-fast-forward rejection.

**Conflict semantics.** `git rebase --abort` + bail-out without
pushing. The lane-discipline gate hard-rejects cross-lane
commits, so two agents touching the same file is a process
bug. The abort path primarily exists for the rare cross-lane-
shared-file edits (the W8 `selectors_md_shared` allowlist).
Operator-level intervention is the correct escalation when
this fires.

## Carry-forward into Wave 10

- **Lock-file location cutover.** Every agent flips
  `/tmp/squad-git-lock` → `.work/squad-git-lock` in its prompt
  template; the `.work/.gitkeep` + `.gitignore` are in place.
- **Remove legacy `canary.analysis` block.** `helm/mahjong/values.yaml`
  still has the W8 single-template block alongside the new
  `canary.analyses.*` blocks. Safe to remove after a wave of
  soak (W10 if no field reports of breakage).
- **First live prod canary cut.** Operator flips
  `canary.enabled=true` + `api.enabled=false` in a future
  prod release (earliest realistic: W11). The three gates +
  the prod thresholds + the prod Prometheus endpoint are all
  pre-staged.
- **Argo CD adoption.** With Argo Rollouts already in the
  cluster, adding Argo CD is a small step. W10 candidate.
- **Extend `scripts/check_invariants.py`.** Candidate next
  bindings: OAuth `ClientId` ↔ ConfigMap + Helm + frontend
  env; cosign signer-identity → KMS key ARN (if Phase L moves
  to keyed cosign).
- **Apply YAML-anchor pattern to subchart values** —
  `helm/mahjong/charts/mahjong-api/values.yaml` if subchart
  values grow per-env duplication (W10+).
- **Codify `.work/<agent>-w<N>-safe/` backup discipline.**
  W9 lost two edit batches to concurrent-agent stash-and-
  reset events before the per-batch backup discipline was in
  force. The W10 agents-onboarding doc should make this a
  first-class step.
