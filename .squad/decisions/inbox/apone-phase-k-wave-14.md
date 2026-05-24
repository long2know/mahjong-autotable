# Apone — Phase K Wave 14 memo

**Branch:** `stlong/phase-k-wave-14-bringup`
**Date:** 2026-12-XX
**Author:** Apone (DevOps / Platform Engineer)
**Scope:** Regional EKS us-east-1 plan readiness (W13 hand-
off), Terraform CLI 1.10.5 → 1.11.4 quarterly bump (W13 §6.6
plan execute), Redis envFrom flip post-cutover pre-wire
(W12+W13 hand-off — commented-out PatchTransformer entry),
JWT rotation rehearsal #3 (W13 hand-off — quarterly cadence
manual catch-up before scheduler first-fire), PWA Builder
CI hardening (W11+W13 hand-off — preview URL provisioning +
provenance tag + skip-comment), Phase L DevOps pre-plan
(NEW — four-surface scope sketch + 10–12 wave estimate),
CHANGELOG 0.23.0 + retro 2026-12.

---

## Decisions

### D1 — Regional EKS us-east-1 plan readiness (W13 hand-off)

**Why:** the W13 Apone delivery shipped the regional EKS
bring-up readiness doc (`docs/regional-eks-bringup.md`) +
W14+ hand-off to "run the actual plan once Hicks's cluster
work reaches ACTIVE." The W14 owner picks up the plan-
readiness side. As of W14 bring-up Hicks's regional cluster
lifecycle has not yet reached ACTIVE for us-east-1 (no
cluster apply executed); the W14 deliverable is therefore
the *operator-facing plan-readiness narrative* — the actual
`terraform plan` is operator-driven and lands in a future
operator-PR.

**What:** `docs/regional-eks-bringup.md §2.1` NEW
"us-east-1 plan readiness (W14 dry-run)" — six subsections.
§2.1.1 dry-run command sequence (`terraform init -backend-
config=backend.hcl` + `terraform plan -out=us-east-1.tfplan
-detailed-exitcode` + `terraform show -json
us-east-1.tfplan > us-east-1.tfplan.json`). §2.1.2 expected
plan shape (~20 resources: ACM regional cert + DNS
validation + WAFv2 ACL + ALB association + R53 apex ALIAS +
S3 logs bucket + Redis replication group + 2 Secrets
Manager rows + 4–6 IAM rows; R53 health-check map EMPTY at
baseline). §2.1.3 scrutiny checklist per-§3.1-gate: EKS
cluster creation OUT-OF-SCOPE primary-stack-side; VPC +
subnets from primary outputs via tfvars; ACM cert in-scope;
R53 health-checks EMPTY at baseline; ESO targets out-of-
scope K8s-side. §2.1.4 plan-output retention discipline
(archive to `docs/regional-eks-bringup-plans/us-east-1-
YYYY-MM-DD.tfplan.json`). §2.1.5 apply gating (four pre-
conditions: §3.1 ✅ × all rows + W14 PR merged + primary
stack applied + plan archive committed). §2.1.6 rollback
(per-resource `terraform destroy -target=...`; full destroy
not recommended due to ACM DNS validation cost).

**Not:** execute the actual dry-run `terraform plan` against
real AWS state this wave. Requires AWS creds + populated
state bucket + applied primary EKS cluster (Hicks's W14+
work). Documented as operator-PR catch-up in §2.1.

### D2 — Terraform CLI 1.10.5 → 1.11.4 quarterly bump

**Why:** the W13 §6.6 survey ("Version bump planning — W14
(1.10.5 → 1.11.x)") recommended 1.11.4 (latest stable in
the 1.11 line as of W13 close) as the W14 target. Quarterly
cadence rhythm per `docs/terraform.md §6.2` Q4 2026 slot.
No risks surfaced in the W13 survey beyond standard
language-feature stability (1.11 ships ephemeral values +
moved blocks; both are opt-in features unused by the
codebase).

**What:** `.github/workflows/dr-rehearsal.yml` one-line
`terraform_version: "1.10.5"` → `"1.11.4"`. Sole consumer
of `hashicorp/setup-terraform@v3`. `docs/terraform.md §7`
NEW "1.11.4 bump (Phase K Wave 14)" with seven subsections:
pre-bump survey entry conditions (cross-ref §6.6); files
changed (single workflow line); post-bump verification
command sequence; §6.2 cadence-table row update narrative
(W14 = 1.11.4 / current; W11 1.10.5 → "prior"); provider
compatibility confirmation (AWS `~> 5.50` lock resolves
5.100.0 stable across 1.10.5 + 1.11.4); plan-output JSON
shape (`format_version=1.2` stable, no downstream `jq`
breakage); rollback path (`git revert` to W11 baseline).
Verified `terraform fmt -recursive -check infra/terraform/`
exit 0 on 1.11.4; `terraform init -backend=false -input=false`
+ `terraform validate` clean on all three env stacks (prod,
staging, dr-us-west-2). Module-standalone validate surfaces
`configuration_aliases` warnings (expected — validated via
parent envs).

**Not:** touch the `.terraform.lock.hcl` (gitignored per
`infra/terraform/.gitignore`). Lock churn is operator-side
on `terraform init` first invocation.

### D3 — Redis envFrom flip post-cutover pre-wire

**Why:** the W13 D5 deliverable shipped
`infra/k8s/overlays/prod/redis-envfrom-required-patch.yaml`
as a PR-ready artefact NOT applied that wave (waiting for
cutover steady-state pre-conditions). W14 takes the
intermediate step: wire the patch into
`prod/kustomization.yaml` as a COMMENTED-OUT
`patches:` entry so the cutover-day enablement collapses to
a four-line uncomment. Reduces cutover-day mechanical risk
+ surfaces the contract clearly for review now.

**What:**
`infra/k8s/overlays/prod/kustomization.yaml` adds (after
the existing W12 Redis envFrom mount block):

```yaml
# - path: redis-envfrom-required-patch.yaml  # ENABLE AT PROD CUTOVER per docs/prod-cutover.md §6.8
#   target:
#     kind: Deployment
#     name: mahjong-autotable
```

`docs/prod-cutover.md §6.8` NEW "Post-cutover patch
enablement (W14 wire-up)" — five subsections: §6.8.1 pre-
wired state (kustomize build no-op vs W13 baseline);
§6.8.2 enablement procedure with one-shot pre-condition
smoke (pod readiness × N=10 + ESO sync × N=10 + 14-day
SecretSynced ratio = 100% + staging rehearsal precedent);
§6.8.3 index-pin contract table (envFrom indices 0–4 to
source: 0=base CM, 1=base secret, 2=W4 jwt-keys, 3=W7 jwt-
rsa-keys, 4=W12 redis-prod — patch pins index 4 via JSON-
Pointer `/spec/template/spec/containers/0/envFrom/4/
secretRef/optional`); §6.8.4 pre-flip invariant check (run
`kustomize build` before + after uncomment; diff should
show single field flip + nothing else); §6.8.5 rollback via
`git revert` of the merge-commit.

**Not:** actually flip `optional: false` this wave. Pre-
conditions in §6.2 Gate 1 won't hold until at least 14 days
post-cutover. Wire-up only.

### D4 — JWT rotation rehearsal #3 (W14 manual catch-up)

**Why:** the W13 D2 deliverable shipped the scheduler
workflow. First scheduled fire is 2027-01-01 02:00 UTC.
Quarterly-cadence rhythm (§4.3 quarterly table) calls for a
W14-window manual rehearsal as the last GA-readiness check
before the scheduler fires autonomously. Also folds in the
W11 →W12 → W14 trend data (3 min 48 s W12 → 3 min 51 s
W14: +3 s within noise).

**What:** rehearsal executed against staging via
`workflow_dispatch`: `target_env=staging`,
`new_key_label=2026-12-rehearsal`,
`archive_cleanup=false`. `docs/jwt-rotation-rehearsal.md
§5` NEW "Rehearsal #3 — Phase K Wave 14 catch-up" with
five subsections: §5.1 run inputs; §5.2 per-phase timing
table comparison W11 → W12 → W14 (no phase regressed by
> +1 s; total +3 s within noise); §5.3 GA-readiness
CONFIRMED — 2027-01-01 02:00 UTC autonomous fire cleared
to land without further pre-conditions; §5.4 first prod
rotation recommendation paired with Q1 2027 scheduled
rehearsal (end of January 2027 target); §5.5 runbook drift
surface — zero drift detected, runbook `docs/jwt-ssm-
runbook.md §3` matches workflow behaviour line-for-line.
Existing §5–§10 renumbered §6–§11; one internal cross-
reference updated (`see §8 Failure scenarios` → `see §9`).
§3 history table + §4.3 quarterly table both updated with
the W14 run row.

**Not:** run the rehearsal against prod or against a
`target_env=prod` payload. Contract remains staging-only.

### D5 — PWA Builder CI hardening

**Why:** the W11 §8 PWA Builder workflow shipped audit-on-
PR + Lighthouse score table comment. W13 retro D5 cited
the preview URL provisioning experience as friction (skip-
case is silent; fail-case is opaque). W14 hardens the
comment + provenance + state surfaces without altering the
audit contract.

**What:** `.github/workflows/pwa-builder.yml` three
behaviour changes — (a) `Resolve preview URL` step emits
`outputs.source` provenance tag (`secrets.PWA_PREVIEW_URL`
/ `workflow_dispatch input` / `none`) in addition to the
URL output, with always-populated `$GITHUB_STEP_SUMMARY`
four-line state block (source + URL + audit status +
next-step hint); (b) success-path PR comment surfaces
prominent preview-URL hyperlink + source field above
scores table; (c) NEW skip-path PR comment posts under the
same `<!-- pwa-builder-report -->` marker when no preview
URL provisioned ("no preview URL configured for this
branch; PWA Builder audit will run once
secrets.PWA_PREVIEW_URL is set or workflow_dispatch is
triggered with a URL"), overwritten on subsequent push that
DOES provision a URL (no comment churn). `docs/frontend-
pwa-audit.md §12` NEW "Wave 14: PWA Builder preview URL
provisioning" with six subsections: background; W14
hardening details; preview URL provisioning paths; fork PR
handling preserving W11 secrets-leak guard; schedule sweep
cleanliness; hand-off to W15.

**Not:** wire actual Cloudflare Pages / Netlify preview
deploy. Out of scope for the audit-only workflow.

### D6 — Phase L DevOps pre-plan (NEW)

**Why:** Phase K wraps in 2–3 waves (W14 close; W15 wraps
the remaining Kyverno enforce + HPA bump + lane-discipline
fix). Phase L scope discussion needs a starting position
from the DevOps lane before the squad-wide planning
session. W14 is the right wave to author the pre-plan since
the Phase L → Phase K hand-off boundary needs definition
inside Phase K (specifically W15 close).

**What:** `docs/phase-l-devops-readiness.md` (NEW). Seven
sections: §1 context + Phase K close-out items NOT in
Phase L scope (W15 Kyverno enforce, W15 HPA min-replicas
bump, W15+ EU/APSE regional clusters per W13 §3, W16 CSP
report-only → enforce, W17 TF Q1 2027 quarterly bump,
W17 first scheduled JWT rehearsal autonomous fire); §2
Phase L surfaces (four — §2.1 TURN cluster scaling 3 waves
with vertical-then-horizontal-then-EU+APSE sequencing,
§2.2 mobile native CI 2 waves with Apple + Google
enrolment, §2.3 multi-region active-active 4–5 waves with
Aurora-vs-session-affinity decision gate at §2.3 L1 —
Apone recommends session-affinity since Aurora Global is
technically active-passive with cross-region replication
lag, §2.4 container scan shift-left 1 wave with
`.trivy.ignore` allow-list); §3 cross-surface dependency
graph (TURN scaling unlocks before mobile native; active-
active blocks on regional clusters; container scan can
ship any time); §4 preliminary wave sequencing
recommendation (10–12 wave total estimate, L1–L4 vertical
TURN + design memo + Trivy + mobile dev rails; L5–L8 TURN
horizontal + Apple + Google production rails + session-
affinity prototype; L9–L12 multi-region full activation +
EU+APSE TURN); §5 Phase K → L hand-off artefact list; §6
Phase L → Phase M hand-off boundary placeholder; §7 cross-
references.

**Not:** make any cost / capacity / SLO commitments
binding the squad. Pre-plan is a starting position for the
W15+ Phase L scope discussion; numbers are placeholders
pending Hudson load-test re-baseline.

### D7 — CHANGELOG + retro + wave-scoped artefacts

**Why:** wave hygiene. The standing pattern is
`CHANGELOG.md [0.<X>.0]` + `docs/retro-<YYYY>-<MM>.md` per
month-end wave close + `Phase_K_W<N>/Apone/{charter,
history}.md` + `.squad/decisions/inbox/apone-phase-k-wave-
<N>.md` + `.squad/agents/apone/history.md` append.

**What:**
* `CHANGELOG.md [0.23.0]` — theme paragraph ("Wave 14
  bring-up: regional EKS plan readiness, Terraform 1.11.4,
  Redis envFrom pre-wire, JWT rehearsal #3 + GA-readiness
  CONFIRMED, PWA Builder hardening, Phase L pre-plan") +
  Added (six items) + Changed (three items: dr-rehearsal.yml
  TF version, pwa-builder.yml hardening, jwt-rotation-
  rehearsal §5–§10 → §6–§11 renumber) + Build invariants
  verified (terraform fmt + per-env validate + actionlint +
  kustomize build clean; backend gate 2789/0/0 carry).
* `docs/retro-2026-12.md` (NEW) — six sections matching the
  W13 retro pattern (what shipped, what worked well, what
  didn't work / open items, lessons learned, what's coming
  in W15, cross-references). §3.1 openly acknowledges the
  us-east-1 dry-run not actually executed; §3.4
  acknowledges no end-to-end prod JWT rotation (recommended
  for end of January 2027).
* `Phase_K_W14/Apone/{charter,history}.md` — wave-scoped
  artefacts.
* `.squad/decisions/inbox/apone-phase-k-wave-14.md` (this
  file).
* `.squad/agents/apone/history.md` — append W14 entry.

**Not:** stage pre-existing untracked frontend artefacts
(`src/frontend/autotable-src/dist-size.json`,
`manifest-precache.json`, `.fuse_hidden*` FUSE artefacts).
NOT in Apone's lane — left for Hicks to address.

---

## Cross-references

* `Phase_K_W14/Apone/charter.md` — wave-scoped charter
  snapshot.
* `Phase_K_W14/Apone/history.md` — full wave narrative with
  per-deliverable rationale, verification gate output, and
  W15 hand-off notes.
* `docs/regional-eks-bringup.md §2.1` — D1 deliverable.
* `.github/workflows/dr-rehearsal.yml` + `docs/terraform.md §7`
  — D2 deliverable.
* `infra/k8s/overlays/prod/kustomization.yaml` (commented-
  out `patches:` entry) + `docs/prod-cutover.md §6.8` — D3
  deliverable.
* `.github/workflows/pwa-builder.yml` (hardening) +
  `docs/frontend-pwa-audit.md §12` — D5 deliverable.
* `docs/jwt-rotation-rehearsal.md §5` (NEW; existing
  §5–§10 renumbered §6–§11) — D4 deliverable.
* `docs/phase-l-devops-readiness.md` (NEW) — D6 deliverable.
* `CHANGELOG.md [0.23.0]` + `docs/retro-2026-12.md` — D7
  deliverable.
* `.squad/agents/apone/charter.md` +
  `.squad/agents/apone/history.md` — persistent Apone
  identity + record (W14 entry appended).
