# Apone — Phase K Wave 14 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/apone/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 14 — DevOps bring-up

Branch: `stlong/phase-k-wave-14-bringup`
Bring-up-on commit (W13 close): `f0b8e4a` (PR #59 — gate
2789/0/0).

### Deliverables (seven)

1. **Regional EKS us-east-1 plan readiness docs.**
   `docs/regional-eks-bringup.md §2.1` NEW. Six subsections —
   §2.1.1 plan command (`terraform init -backend-config=
   backend.hcl` + `terraform plan -out=us-east-1.tfplan` +
   `terraform show -json`), §2.1.2 expected plan shape
   (~20 resources: ACM regional cert + DNS validation +
   WAFv2 ACL + ALB association + R53 apex ALIAS + S3 logs
   + Redis replication group + 2 Secrets Manager rows +
   4–6 IAM rows; R53 health-check map EMPTY at W14
   baseline), §2.1.3 scrutiny checklist (per-§3.1-gate
   mapping: EKS cluster creation OUT-OF-SCOPE primary-stack-
   side; VPC + subnets from primary stack outputs via
   tfvars; ACM cert in-scope; R53 health checks EMPTY at
   baseline; ESO targets out-of-scope K8s-side but Secrets
   Manager arns + IAM trust scoping in-scope), §2.1.4 plan-
   output retention (archive to `docs/regional-eks-bringup-
   plans/us-east-1-YYYY-MM-DD.tfplan.json`), §2.1.5 apply
   gating (four pre-conditions: §3.1 ✅ × all rows + W14 PR
   merged + primary stack applied + plan archive
   committed), §2.1.6 rollback (per-resource `terraform
   destroy -target=...`; full destroy not recommended). The
   dry-run NOT EXECUTED this wave — requires AWS creds +
   populated state bucket + applied primary stack; the W14
   deliverable is the operator-facing runbook.

2. **Terraform CLI 1.10.5 → 1.11.4 quarterly bump.**
   `.github/workflows/dr-rehearsal.yml` one-line bump
   (`terraform_version: "1.10.5"` → `"1.11.4"`). Sole
   consumer of `hashicorp/setup-terraform@v3` per the W11
   §6.6.4 survey + W13 §6.6 carry-over. `docs/terraform.md
   §7` NEW (seven subsections: pre-bump survey entry
   conditions; files changed; post-bump verification
   command sequence; §6.2 cadence-table row update
   narrative — W14 = 1.11.4 / current; provider compatibility
   confirmation — AWS `~> 5.50` → 5.100.0 stable across
   1.10.5 + 1.11.4; plan-output JSON shape confirmation —
   `format_version=1.2` stable; rollback path —
   `git revert` to W11 1.10.5 baseline). Local verification:
   `terraform fmt -recursive -check infra/terraform/` → exit
   0 on 1.11.4; `terraform init -backend=false -input=false`
   + `terraform validate` clean on all three env stacks.
   Module-standalone validate surfaces the W7+
   `configuration_aliases` provider warnings (expected —
   modules validated via parent envs).

3. **Redis envFrom flip post-cutover pre-wire.**
   `infra/k8s/overlays/prod/kustomization.yaml` gains a
   COMMENTED-OUT `patches:` entry immediately after the W12
   Redis envFrom mount block:

   ```yaml
   # - path: redis-envfrom-required-patch.yaml  # ENABLE AT PROD CUTOVER per docs/prod-cutover.md §6.8
   #   target:
   #     kind: Deployment
   #     name: mahjong-autotable
   ```

   `docs/prod-cutover.md §6.8` NEW (five subsections: pre-
   wired state; enablement procedure with one-shot pre-
   condition smoke covering pod readiness + ESO sync +
   14-day SecretSynced + staging rehearsal cross-reference;
   index-pin contract table mapping envFrom indices 0–4 to
   source and W14 baseline vs post-flip optional status; pre-
   flip invariant check confirming commented entry is a
   `kustomize build` no-op vs W13 baseline; rollback via
   single `git revert` of merge-commit). Stephen uncomments
   after §6.2 Gate 1 pre-conditions hold (14-day pod-ready +
   14-day ESO sync + 14-day SecretSynced + staging
   rehearsal).

4. **JWT rotation rehearsal #3 (manual W14 catch-up).**
   `workflow_dispatch` against staging with
   `new_key_label=2026-12-rehearsal` + `archive_cleanup=false`.
   Per-phase timing vs W12: total 3 min 51 s (W12 was 3 min
   48 s; +3 s within noise). §3.3 GREEN budget (< 4 min)
   holds. No phase regressed by more than +1 s. Documented
   in `docs/jwt-rotation-rehearsal.md §5` NEW (five
   subsections: run inputs; per-phase timing comparison;
   GA-readiness CONFIRMED with 2027-01-01 autonomous fire
   cleared to land; first prod rotation recommendation —
   end of January 2027 paired with the Q1 2027 scheduled
   rehearsal; runbook drift surface — zero drift detected).
   Existing §5–§10 renumbered §6–§11; one internal cross-
   reference updated (`see §8 Failure scenarios` → `see §9`).
   §3 history table + §4.3 quarterly table both updated
   with the W14 run row.

5. **PWA Builder CI hardening.**
   `.github/workflows/pwa-builder.yml` three behaviour
   changes — (a) `Resolve preview URL` step emits
   `outputs.source` provenance tag in addition to URL, with
   always-populated `$GITHUB_STEP_SUMMARY` four-line state
   block; (b) success-path PR comment surfaces prominent
   preview-URL hyperlink + source field above scores table;
   (c) NEW skip-path PR comment posts under same
   `<!-- pwa-builder-report -->` marker when no preview URL
   provisioned (overwritten on subsequent push that DOES
   provision — no comment churn). Documented in
   `docs/frontend-pwa-audit.md §12` NEW (six subsections:
   background; W14 hardening details; preview URL
   provisioning paths — `secrets.PWA_PREVIEW_URL` /
   `workflow_dispatch input` / none; fork PR handling
   preserving W11 secrets-leak guard; schedule sweep
   cleanliness; hand-off to W15). actionlint clean.

6. **Phase L DevOps pre-plan.**
   `docs/phase-l-devops-readiness.md` NEW (seven sections).
   Four Phase L surfaces:
   * **§2.1 TURN cluster scaling** — 3 waves: vertical
     scale (4→8 vCPU + 8→16 GiB; 6→10–12 nodes) + Hudson
     load-test re-baseline, us-west-2 horizontal, EU+APSE
     horizontal. Hand-off dependencies on Hicks's frontend
     STUN/TURN URI list + Bishop's region-tagged TURN
     credential acceptance.
   * **§2.2 Mobile native app CI** — 2 waves: Apple +
     Google enrolment + credential provisioning + production-
     rails activation; quarterly rehearsal workflow + cadence.
     Decision needed: shared SemVer vs mobile-only counter
     for release numbering.
   * **§2.3 Multi-region active-active** — 4–5 waves: design
     memo (Aurora vs session-affinity — Apone recommends
     session-affinity since Aurora Global is technically
     active-passive with cross-region replication lag that
     would surface as game-state drift); session-affinity
     routing layer; per-region Redis + Bishop runtime
     region awareness; frontend region-stable endpoint +
     active-active cutover; optional EU+APSE activation
     following Phase K W15+ regional cluster work.
   * **§2.4 Container scanning shift-left** — 1 wave: Trivy
     PR trigger + CRITICAL/HIGH severity gate + `.trivy.ignore`
     allow-list convention.
   Cross-surface dependency graph + preliminary 10–12 wave
   sequencing recommendation; Phase K → L hand-off artefact
   list; Phase K close-out items list (W15 Kyverno enforce,
   W15 HPA bump, W15+ EU/APSE cluster, W16 CSP enforce, W17
   TF Q1 bump, W17 first scheduled JWT rehearsal) — NOT
   Phase L scope.

7. **CHANGELOG + retro + wave-scoped artefacts.**
   * `CHANGELOG.md [0.23.0]` — theme paragraph + Added
     (six items: regional §2.1, terraform §7, prod-cutover
     §6.8, jwt-rotation-rehearsal §5, frontend-pwa-audit
     §12, phase-l-devops-readiness) + Changed (three items:
     dr-rehearsal.yml, pwa-builder.yml, jwt-rotation-
     rehearsal.md renumber) + Build invariants verified
     (terraform fmt + per-env validate + actionlint +
     kustomize build clean; backend gate 2789/0/0 carry).
   * `docs/retro-2026-12.md` NEW (six sections).
   * `Phase_K_W14/Apone/charter.md` + `history.md` (this
     file).
   * `.squad/decisions/inbox/apone-phase-k-wave-14.md` NEW.
   * `.squad/agents/apone/history.md` W14 entry appended.

### Build invariants verified

| Surface                                          | Tool                                          | Result                                                                                  |
|--------------------------------------------------|-----------------------------------------------|------------------------------------------------------------------------------------------|
| Workflow files (2 modified)                      | `actionlint`                                  | Clean on `pwa-builder.yml` + `dr-rehearsal.yml`. Pre-existing `lane-discipline-nightly.yml:87` heredoc parse error unchanged. |
| Terraform fmt                                    | `terraform fmt -recursive -check`             | Clean on 1.11.4 across `infra/terraform/`. Exit 0.                                       |
| Terraform validate (3 envs)                      | `terraform init -backend=false` + `validate`  | `Success! The configuration is valid.` × 3 (prod, staging, dr-us-west-2).                |
| Kustomize prod                                   | `kustomize build infra/k8s/overlays/prod/`    | Clean. Diff vs W13 baseline empty (commented-out patches entry parses as no-op).         |
| Kustomize staging                                | `kustomize build infra/k8s/overlays/staging/` | Clean. No W14-side staging overlay changes.                                              |
| Backend gate                                     | (inherited)                                   | 2789/0/0 from W13 merge preserved (no Apone-lane `src/**` changes).                       |

### Cross-lane integration points

* **Hicks W14+ regional cluster lifecycle** — the W14
  regional-eks-bringup §2.1 plan-readiness narrative
  consumes Hicks's cluster work reaching ACTIVE state for
  us-east-1 + us-west-2.
* **Hudson** — out of scope this wave; the W14 retros +
  prod-cutover.md §6.8.2 operator runbook cite the existing
  W12 panel set. Hudson re-validation is W15+ backlog.
* **Bishop** — no direct interface this wave; the W14 JWT
  rehearsal #3 exercises Bishop's W12 JWKS-cache pre-warm
  (already in steady-state).
* **Vasquez** — no direct interface this wave.

### Lane discipline

Stayed strictly within the DevOps lane. Touched files:

* `.github/workflows/dr-rehearsal.yml` (TF version bump)
* `.github/workflows/pwa-builder.yml` (hardening)
* `infra/k8s/overlays/prod/kustomization.yaml` (commented-
  out pre-wire)
* `docs/regional-eks-bringup.md` (NEW §2.1)
* `docs/terraform.md` (NEW §7)
* `docs/prod-cutover.md` (NEW §6.8)
* `docs/jwt-rotation-rehearsal.md` (NEW §5 + renumber +
  table rows)
* `docs/frontend-pwa-audit.md` (NEW §12 — Apone-authored
  workflow runtime detail in Hicks's doc per W10 precedent)
* `docs/phase-l-devops-readiness.md` (NEW)
* `docs/retro-2026-12.md` (NEW)
* `CHANGELOG.md` (`[0.23.0]` entry)
* `Phase_K_W14/Apone/{charter,history}.md` (NEW)
* `.squad/decisions/inbox/apone-phase-k-wave-14.md` (NEW)
* `.squad/agents/apone/history.md` (W14 entry appended)

NO touches to `src/backend/**`, `src/frontend/**` (except
the Apone-authored `docs/frontend-pwa-audit.md §12`),
`tests/**`, or `.vscode/`. The `.github/workflows/` +
`infra/k8s/overlays/` + `docs/` + `Phase_K_W14/Apone/` +
`.squad/` paths are inside the W14 charter lane.

### Pre-W15 hand-off notes

* **Kyverno `audit → enforce` flip pre-wire** — candidate
  for the W15 owner per `docs/prod-cutover.md §6.3` Gate 4.
  W14 pattern (pre-wire commented-out, cutover-day
  uncomment) is the candidate approach.
* **HPA min-replicas 3 → 5 bump pre-flight** — per §6.4
  Gate 5; 30-day Hudson panel review pre-condition. W14 +
  14 days is the earliest target; W15 may be too early.
* **lane-discipline-nightly.yml:87 heredoc parse error** —
  W5-era; on the W15+ backlog. Fix is a heredoc indent change.
* **Hudson dashboard re-validation** — if Hudson back in
  scope by W15, validate the W12 panels still render.
* **us-east-1 actual `terraform apply`** — IF Hicks's
  regional cluster lifecycle reaches ACTIVE for us-east-1 +
  us-west-2 by W15. The §2.1.5 apply-gating contract is the
  entry criterion.
* **W17+: first scheduled JWT rotation rehearsal fire** at
  2027-01-01 02:00 UTC. Append auto-generated report to
  `docs/`; update §4.3 row 4.
* **W17: Q1 2027 Terraform CLI quarterly bump** (1.11.x →
  1.12.x). Re-run §6.6 survey shape against 1.12 release
  page on bring-up day.
* **End of January 2027: first real prod JWT rotation**
  recommended; operator-only via `docs/jwt-ssm-runbook.md §3`.

### Patterns locked in W14

* **Pre-wire-then-toggle.** Multi-line mechanical changes
  should be split into TWO waves: patch file lands wave N,
  pre-wire (commented-out) lands wave N+1, cutover-day flip
  is a comment-prefix toggle. Pre-condition verification
  stays the same; mechanical work shrinks to a one-glance
  diff. Pattern applies to flip-an-existing-field-value
  changes (envFrom optional flag, Kyverno audit/enforce,
  CSP report-only); does NOT apply to number-bumps (HPA
  min-replicas).
* **Survey-then-execute cadence rhythm.** Quarterly cadence
  items (TF CLI bump, JWT rotation) follow the two-wave
  shape: wave N surveys (risk classification + provisional
  recommendation), wave N+1 executes (actual change). The
  survey output is the audit trail; the execute wave's
  diff is the change.
* **Renumber discipline.** After a section renumber, ALWAYS
  grep for old section numbers (`grep -nE "§[N-M]"`) to
  catch internal cross-references. Cite the grep output in
  the wave commit message as evidence.
* **Phase pre-plan as cross-lane negotiation input.**
  Surface next-phase scope BEFORE current-phase close-out
  so other lanes can produce their own pre-plans and the
  charter memo writes itself.
