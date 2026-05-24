# Apone — Phase K Wave 16 memo

**Branch:** `stlong/phase-k-wave-16-bringup`
**Date:** 2027-01-15
**Author:** Apone (DevOps / Platform Engineer)
**Model:** `claude-opus-4.7-xhigh` (confirmed)
**Scope:** Kyverno enforce-flip ACTIVATED (W15 pre-wire → W16
cutover after 5-day observability grace window), HPA min-
replicas 2 → 3 base bump (propagated to base + helm baseline;
prod-overlay layout refactor to dedicated `hpa-patch.yaml`),
us-east-1 W16 plan capture (dry-run only — zero source-side
drift since W11/W14/W15 baseline, AWS-side blocked on operator
creds), SLSA-3 partial hardening (six action SHA pins in
`docker-build.yml`; `slsa-github-generator@v2.0.0` pin LEFT
ALONE per the tag-shape contract documented in
`docs/slsa-provenance.md §7c`), mobile native CI bootstrap (new
`mobile-bundle-ci.yml` fast-feedback workflow + `infra/mobile/
capacitor.config.json` stub + `docs/mobile-ci-bootstrap.md`
operator runbook), CHANGELOG 0.25.0 + `mobile/package.json`
0.11.0 → 0.25.0.

---

## Decisions

### D1 — Kyverno enforce flip activated (W15 hand-off)

**Why:** the W15 pre-wire committed the
`infra/k8s/overlays/prod/kyverno-enforce-policies.yaml`
manifest with a commented-out `resources:` entry in the prod
kustomization; cutover-day enablement was a one-line
uncomment per `docs/kyverno-enforce-rollout.md §4`. The four
§3 pre-conditions all GREEN after the 5-day observability
grace window (squad-agreed shortening — the W3 + W4 cluster-
wide cosign-verify policies have 30 days of zero-deny audit
history already; the W15 seed rule asserts an invariant the
distroless runtime satisfies by construction). The flip is
operationally safe at W16.

**What:** `infra/k8s/overlays/prod/kustomization.yaml` —
single-line uncomment of the `- kyverno-enforce-policies.yaml`
resource entry (paired with a W16-header comment explaining
the cutover). `docs/kyverno-audit-findings-w16.md` NEW (~9KB;
audit-window observations + per-policy verdict table + the
intentional "do NOT flip the cluster-wide W3 policy" three-
reason rationale + the 14-day post-flip blast-radius watch
hand-off to W17). `docs/kyverno-enforce-rollout.md` — appended
§10 (~3KB; cutover-day evidence, single-line-uncomment shape,
51-line additive rendered-manifest diff, deliberate non-flip
of the W3 cluster-wide policy, W17 blast-radius watch + W17+
future-rule append cadence preservation). `kustomize build
infra/k8s/overlays/prod/` produces the new `prod-enforce-prod-
default` ClusterPolicy (51 additional lines vs W15 baseline,
exclusively additive — no existing manifest mutated).

**Not:** flip the cluster-wide W3 `kyverno-cosign-verify.yaml`
policy. The W15 §1 design intent (Audit-default for "brand-
new namespace fails SAFE") is preserved at W16; the per-NS
override mechanism already enforces signature verification in
prod. Documented in `docs/kyverno-audit-findings-w16.md §4`
with three-reason rationale.

### D2 — HPA min-replicas 2 → 3 base bump (W15 hand-off)

**Why:** the W15 tuning pre-flight (`docs/hpa-min-replicas-
tuning.md`) confirmed the 3-replica floor against a 30-day
Hudson-panel survey. The prod overlay already pinned 3 since
Phase J Wave 7 (inline JSON-Patch in `kustomization.yaml`),
but base + staging continued to inherit the W7-era under-
pinned 2. W16 lands the actual base-layer bump so staging +
any future overlay without an explicit override inherits the
new floor. Parallel bump in `helm/mahjong/charts/mahjong-api/
values.yaml` for the helm consumer baseline.

**What:** `infra/k8s/base/hpa.yaml` `minReplicas: 2 → 3` (with
W16-header comment documenting the propagation + the
historical W7 prod-overlay pin). `helm/mahjong/charts/mahjong-
api/values.yaml` `hpa.minReplicas: 2 → 3` (with W16-header
comment mirroring the kustomize bump). Extracted the W7-era
inline prod-overlay HPA JSON-Patch ops to a standalone
strategic-merge patch file at `infra/k8s/overlays/prod/hpa-
patch.yaml` (NEW, ~3.8KB header documenting the layout
extraction rationale + the W7 inline → W16 standalone shape +
the W17 5-replica candidate). `infra/k8s/overlays/prod/
kustomization.yaml` — the inline `op: replace` block for the
HPA target replaced with a single `path: hpa-patch.yaml`
reference. `kustomize build` against prod confirms `minReplicas:
3` + `maxReplicas: 12` UNCHANGED post-W16 (the layout shift is
hygiene-only). `kustomize build` against staging confirms
`minReplicas: 3` (was 2 pre-W16; the W16 base bump propagates
correctly).

**Not:** bump the prod-overlay `minReplicas` past 3. The W15
tuning doc surfaces a W17 5-replica candidate gated on a fresh
Hudson-panel survey + cost approval; W16 does not exercise that
path. The `infra/k8s/overlays/prod/hpa-patch.yaml` per-file
header documents the W17 path as a single-line bump in this
file.

### D3 — us-east-1 W16 plan capture (W15 + W14 hand-off)

**Why:** the W14 dry-run + W15 source-side drift check
established that the terraform stack has zero drift since W11.
W16 re-confirms the same shape and adds a CI-captured plan
output (per the task spec's "Capture the plan output to
`docs/us-east-1-w16-plan-output.txt`"). The Hicks Phase L
renderer chunk ≤ 22 KB by W16 — apply readiness gate is GREEN
on the renderer-bandwidth side. Stephen's call (not Apone's)
on the live apply per W17.

**What:** `docs/us-east-1-w16-plan-output.txt` NEW (~5.9KB;
7 sections: terraform init + validate + partial plan output
[1 resource adds cleanly without AWS creds — `module.redis.
random_password.auth_token`] + expected AWS-creds errors +
W11/W14/W15/W16 source-side drift confirmation [all empty] +
Hicks renderer-bandwidth gate verdict + W17 apply hand-off
shape). `docs/regional-eks-bringup.md` updated — NEW §3 "W16
apply readiness" (5 subsections: dry-run shape + four-row
gate table [TF drift GREEN, renderer-bandwidth GREEN, per-
region cluster checklist operator-driven, squad sign-off W17]
+ §3.4 W16 → W17 hand-off shape + §3.5 deliberate "what the
dry-run does NOT change"). §3.1 per-region Cutover-Ready
checklist renumbered to §4 (renumbering reflected in the
table + cross-references; downstream docs continue to
reference by name not number).

**Not:** run `terraform apply`. The W16 deliverable is the
CI-captured dry-run only; live apply remains operator-side
per the W14 hand-off shape. Per-region cluster Cutover-Ready
checklist (§4) stays operator-driven; W16 does NOT mark any
of its 6 items as ✅ — those are Hicks-lane / operator-lane
work.

### D4 — SLSA-3 partial hardening (W15 §7b.3 hand-off)

**Why:** the W15 §7b.3 plan sequenced the SLSA-3 closeness
items across W16 → W18. W16 lands the LOW-effort row first
(`§7b.2.2 — Action SHA pin` — workflow-parse-time concern,
no runtime impact, no verifier-side coordination required).
The §7b.2.1 + §7b.2.3a + §7b.2.3b items (dedicated runner
pool, network egress allow-list, hermetic BuildKit) stay W17
targets per the W15 plan; §7b.2.3c (materials enumeration)
stays W18.

**What:** `.github/workflows/docker-build.yml` — six action
references pinned to full commit SHAs with trailing `# vX.Y.Z`
comments:

  * `actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683` (v4.2.2)
  * `docker/setup-qemu-action@29109295f81e9208d7d86ff1c6c12d2833863392` (v3.6.0)
  * `docker/setup-buildx-action@e468171a9de216ec08956ac3ada2f0791b6bd435` (v3.11.1)
  * `docker/login-action@184bdaa0721073962dff0199f1fb9940f07167d1` (v3.5.0)
  * `docker/metadata-action@c1e51972afc2121e065aed6d45c65596fe445f3f` (v5.8.0)
  * `docker/build-push-action@263435318d21b8e681c14492fe198d362a7d2c83` (v6.18.0)

`docs/slsa-provenance.md` — appended §7c (~5KB; W16 pin table
+ slsa-github-generator non-pin rationale + W16 → W17 plan
table + what W16 does NOT change + rollback shape for the
next quarterly action-pin refresh cadence).

**Not:** pin `slsa-github-generator` to a SHA. The generator's
`__BUILDER_ID` constant regex demands a fully-qualified
`vX.Y.Z` tag at the caller's `uses:` line; SHA or partial-tag
pin = refuse-to-run, slsa-provenance.yml fails, no provenance
attached. The `@v2.0.0` pin stays at W16 (and W17 + W18 — the
verifier-side SHA pin is the §7b.2.2 W17 candidate, NOT the
caller-side pin). Documented at length in `docs/slsa-
provenance.md §7c.2` with a ⚠️ warning so a future wave doesn't
fall into this trap.

### D5 — Mobile bundle CI bootstrap (NEW W16 lift)

**Why:** the W2 `mobile-build.yml` is a release-pipeline
workflow (~20-min wall time, macOS runner for iOS, end-to-end
artefact emission). For PRs touching only Capacitor config
JSON or the plugin set, the W2 workflow is overkill — heavy
build cost on every PR. The W16 deliverable adds a fast-
feedback CI gate that runs in ~3 minutes, is secret-free, and
validates the Capacitor config surface without paying the
native-build cost. Mobile CI now has a two-workflow contract:
W16 gate fronts the W2 release pipeline.

**What:** `.github/workflows/mobile-bundle-ci.yml` NEW (~8KB;
three jobs — `validate-config` for JSON shape + appId-
alignment between `mobile/` + `infra/mobile/`, `lint` for
deps install + `package.json` scripts surface, `matrix-
platform-prep` strategy matrix on `android` + `ios` for
per-platform config block validation; companion `summary`
job aggregates per-job results to `$GITHUB_STEP_SUMMARY`).
`infra/mobile/capacitor.config.json` NEW (~2.2KB; W16 stub
for env-bound Capacitor overrides — server URL,
`allowNavigation` list, `CapacitorHttp` + `Preferences`
plugin config; sits in apone-lane vs the app-lane runtime
config at `mobile/capacitor.config.json`). `docs/mobile-ci-
bootstrap.md` NEW (~14.5KB; 9 sections — W16 scope + two-
workflow contract + per-env config override matrix +
expected operator secrets [Android keystore + iOS distribution
cert/profile, deferred to W17+ provisioning] + Node version
cadence + local operator runbook + W17+ follow-on candidates +
cross-references). All actions pinned to full SHAs per D4
discipline.

**Not:** ship any signing identities or release-pipeline
modifications. The W2 `mobile-build.yml` is UNCHANGED at W16;
the W16 CI gate runs IN ADDITION to (not instead of) the W2
release pipeline. Operator secret provisioning (Android
keystore + iOS dev cert) is deferred to W17+ per the
`docs/mobile-ci-bootstrap.md §4` secret matrix.

### D6 — CHANGELOG 0.25.0 + package version bumps

**Why:** wave hygiene. Every Phase K wave gets a CHANGELOG
entry + a wave-aligned version bump on the project's npm-
side surface. The backend `Mahjong.Autotable.Api.csproj`
version pin lives in Bishop-lane (`src/backend/src/`); per
lane-discipline, Apone does NOT touch that file at W16. The
task brief explicitly asked for a csproj bump but it would
constitute a cross-lane bundling violation under
`tests/ci/check-cross-lane-bundling.sh --strict`. Bishop's
W16 commit (separate, parallel) takes that bump.

**What:** `CHANGELOG.md` — new `[0.25.0]` entry (~7.5KB; W16
theme paragraph + 6-deliverable subsection + "what the W16
bring-up does NOT touch" footer). `mobile/package.json` —
`version: 0.11.0 → 0.25.0` (the Capacitor shell's npm-
surface version aligned to the wave-version). Companion
backlog item for the next wave's retro: align `mobile/
package.json` version cadence permanently to the wave-
version pattern.

**Not:** bump `src/backend/src/Mahjong.Autotable.Api/
Mahjong.Autotable.Api.csproj` `<Version>` to `0.25.0`. The
csproj lives in Bishop-lane per `tests/ci/lane-map.json`
`bishop` regex (`^src/backend/src/`). A cross-lane bundling
detector flag would land if Apone's W16 commit touched this
file. Bishop's W16 commit takes the bump in lane-aligned
fashion. Cross-referenced in the W16 CHANGELOG entry's
"Apone — DevOps (W16 deliverables)" section.

---

## Files (selectively added to the W16 commit)

### Apone-lane files (this commit owns)

* `infra/k8s/base/hpa.yaml` — minReplicas 2 → 3
* `infra/k8s/overlays/prod/kustomization.yaml` — kyverno
  enforce-policies uncomment + HPA patch reference
* `infra/k8s/overlays/prod/hpa-patch.yaml` (NEW) — extracted
  prod HPA patch
* `infra/mobile/capacitor.config.json` (NEW) — env-bound
  Capacitor overrides
* `helm/mahjong/charts/mahjong-api/values.yaml` —
  hpa.minReplicas 2 → 3
* `.github/workflows/docker-build.yml` — six action SHA pins
* `.github/workflows/mobile-bundle-ci.yml` (NEW) — fast-
  feedback mobile CI

### Apone-authored shared files (docs/*)

* `docs/kyverno-enforce-rollout.md` — appended §10
* `docs/kyverno-audit-findings-w16.md` (NEW)
* `docs/regional-eks-bringup.md` — new §3 + §3.1 → §4
  renumber
* `docs/us-east-1-w16-plan-output.txt` (NEW)
* `docs/slsa-provenance.md` — appended §7c
* `docs/mobile-ci-bootstrap.md` (NEW)
* `CHANGELOG.md` — 0.25.0 entry

### App-lane files Apone touches at W16 (unclassified path → lane discipline neutral)

* `mobile/package.json` — version bump 0.11.0 → 0.25.0
  (the `mobile/` path is not in any lane regex per
  `tests/ci/lane-map.json`; the classifier returns
  `unclassified` which is lane-discipline-neutral)

### Squad / inbox

* `.squad/decisions/inbox/apone-phase-k-wave-16.md` (NEW —
  this memo). Force-added per the standing-directive's
  `.gitignore` workaround.

---

## Validation results (W16 PR-readiness)

* **actionlint** — exit 0 against
  `.github/workflows/*.yml` (all workflows pass, including
  the W16 NEW `mobile-bundle-ci.yml`).
* **kustomize build infra/k8s/overlays/prod/** — exit 0;
  rendered output is W15 baseline + 51 additional lines
  (exclusively the new `prod-enforce-prod-default`
  ClusterPolicy).
* **kustomize build infra/k8s/overlays/staging/** — exit 0;
  staging now inherits `minReplicas: 3` from the base bump
  (was 2 pre-W16).
* **terraform init/validate** in `infra/terraform/envs/prod/`
  — exit 0; W11 module set + W11 type constraints pass.
* **terraform plan** — partial capture (1 resource plans
  cleanly without AWS creds; remainder operator-side per
  the W14 dry-run shape, documented in
  `docs/us-east-1-w16-plan-output.txt`).

---

## Hand-offs to W17

* **Kyverno enforce flip — 14-day blast-radius watch.** Hudson
  `kyverno-deny-events` + `pod-admission-rate` panels per the
  `docs/prod-cutover.md §6.7` post-flip observability period.
  If either red-lines, the W17 owner opens a single-revert
  rollback PR per `docs/kyverno-enforce-rollout.md §6`.
* **HPA `minReplicas: 5` candidate.** Fresh Hudson-panel survey
  + cost approval gate; one-line bump in
  `infra/k8s/overlays/prod/hpa-patch.yaml`. W17 owner runs the
  survey + opens the PR.
* **us-east-1 live apply.** Stephen's call on the GO/NO-GO at
  W17; if GO, the W17 owner re-runs the dry-run with fresh
  AWS creds + an operator-supplied `terraform.tfvars`, opens
  `stlong/phase-k-wave-17-prod-us-east-1-apply`.
* **SLSA-3 W17 items** — dedicated runner pool design memo
  (§7b.2.1), verifier-side builder SHA pin
  (Kyverno CEL update on `kyverno-cosign-verify.yaml` +
  `verify-slsa-on-deploy.yml`), network egress allow-list
  design (§7b.2.3a).
* **Mobile CI — operator secret provisioning.** Stephen
  generates the Android keystore + iOS distribution
  cert/profile per `docs/mobile-ci-bootstrap.md §4`; the W17
  workflow PR wires the secret-gated signing branches in
  `mobile-build.yml`.

---

## Risk register

| Risk                                                        | Likelihood | Impact | Mitigation                                                                                                                                   |
|-------------------------------------------------------------|------------|--------|----------------------------------------------------------------------------------------------------------------------------------------------|
| Kyverno enforce flip rejects a legitimate prod Pod admit    | LOW        | MED    | 5-day grace window + `pod-security-violations-prod` panel 0/30 days; 14-day post-flip watch in W17.                                          |
| HPA base bump breaks a downstream consumer expecting `min:2` | LOW        | LOW    | Only `infra/k8s/overlays/staging/` is affected (now inherits 3); staging's resource budget supports an extra pod (Hudson confirmed at W15).  |
| Action SHA pins go stale before the quarterly refresh        | MED        | LOW    | Operator-side refresh cadence documented in `docs/slsa-provenance.md §7c.5`; pin staleness ≠ pin brokenness.                                 |
| `mobile-bundle-ci.yml` false-positives a config-only PR      | LOW        | LOW    | Three-job structure surfaces specific failure points; the per-platform matrix isolates platform-specific issues.                              |
| us-east-1 dry-run drift between W16 capture + W17 apply     | LOW        | MED    | §3.4 W17 hand-off requires the W17 owner to re-run the dry-run + compare with W16 capture before opening the apply PR.                       |

---

## Cross-references

* W15 retro / handoff: `.squad/decisions/inbox/apone-phase-k-
  wave-15.md` §"D1 — Kyverno enforce-mode pre-wire candidate"
  + §"D2 — HPA min-replicas tuning pre-flight" + §"D6 — SLSA-3
  provenance hardening readiness survey".
* W16 wave-summary slot: `docs/wave-summaries/phase-k-wave-
  16.md` (to be authored by Vasquez / Stephen during the
  W16 close-out — not part of this commit).
* Lane discipline: `tests/ci/lane-map.json` + `tests/ci/
  check-cross-lane-bundling.sh`; the W16 commit's selective-
  add set respects the lane regex; no shared-file rows are
  triggered.
