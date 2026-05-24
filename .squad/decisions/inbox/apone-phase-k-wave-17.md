# Apone — Phase K Wave 17 memo

**Branch:** `stlong/phase-k-wave-17-bringup`
**Date:** 2027-01-22
**Author:** Apone (DevOps / Platform Engineer)
**Model:** `claude-opus-4.7-xhigh` (confirmed)
**Scope:** Kyverno enforce 7-day post-flip observability
(HOLD — no rollback; zero denies across all three policies),
Mobile CI Android signing groundwork (env block + keystore-
decode step + SIGNED/UNSIGNED Gradle branches gated on
secret-presence), us-east-1 W17 plan capture (dry-run only —
zero source-side drift since W11/W14/W15/W16 baseline; gate
PARTIAL-GREEN/HOLD due to renderer Row 2 AMBER), HPA tuning
W17 7-day retrospective (off-peak over-provisioning trigger
acknowledged; cron-scheduled-min override DESIGNED, lands
W18 as a single-PR), SLSA-3 SHA pin expansion (W16's 6 pins
in `docker-build.yml` → +50 pins across 9 security-critical
workflows at W17), CHANGELOG 0.26.0 + `mobile/package.json`
0.25.0 → 0.26.0.

---

## Decisions

### D1 — Kyverno enforce 7-day post-flip observability (W16 hand-off)

**Why:** the W16 cutover-day flip activated the prod-only
`enforce-prod-default` ClusterPolicy after a 5-day grace
window. The W16 §10 + `.squad/decisions/inbox/apone-phase-k-
wave-16.md` "Hand-offs to W17" tasked Apone W17 with a
14-day blast-radius watch via Hudson `kyverno-deny-events`
+ `pod-admission-rate` panels. The W17 retro lands at the
7-day mark to keep the W17 PR rhythm; the second 7-day
window folds into W18's continuous observability slot.

**What:** New `docs/kyverno-enforce-w17-observability.md`
(~9.9 KB) — four-panel Hudson verdict table with the 7-day
window: 0 denies on `enforce-prod-default`, 0 denies on the
W3 audit-mode `kyverno-cosign-verify.yaml`, 0 denies on the
W4 `cosign-verify-prod` admission webhook, +3% headroom on
`pod-security-violations-prod`, +1 ms p99 on `admission-
webhook-latency-prod` (within noise — the W16 baseline was
4 ms p99; W17 reads 5 ms p99). Rollback decision: **HOLD;
no revert PR opened.** The 14-day window stays open for
W18+ continuous-observability watch but the cutover is
operationally green. Appended §11 to `docs/kyverno-enforce-
rollout.md` cross-referencing the new observability doc +
narrowing the §10 W17 watch slot to CLOSED-GREEN.

**Not:** flip the cluster-wide W3 `kyverno-cosign-verify.
yaml` policy. The W16 §4 + W15 §1 design intent (Audit-
default for "brand-new namespace fails SAFE") remains
intact through W17. The W17 retro does NOT modify any
existing Kyverno policy; it is observation-only.

### D2 — Mobile CI Android signing groundwork

**Why:** the W16 D5 inbox memo handed off "Mobile CI —
operator secret provisioning" as a W17 deliverable; the
`docs/mobile-ci-bootstrap.md §4` operator runbook listed the
four `ANDROID_*` secret names but the W2 `mobile-build.yml`
workflow had no consumption shape for them — Stephen could
upload the secrets and they would do nothing until a workflow
edit. W17 lands the workflow consumption shape so the
secret-upload flips the `mobile-build.yml` release branch
from UNSIGNED → SIGNED automatically.

**What:** Extended `.github/workflows/mobile-build.yml` with
a new job-level `env:` block exposing four `ANDROID_*`
secrets (`KEYSTORE_BASE64`, `KEYSTORE_PASSWORD`, `KEY_ALIAS`,
`KEY_PASSWORD`); a new "Decode Android keystore (when secret
present)" step that writes the base64 payload to
`${RUNNER_TEMP}/mahjong-autotable.keystore` and emits a
`keystore-present` boolean step output; and split the W2
`Gradle assembleRelease + bundleRelease` step into mutually-
exclusive SIGNED (gated on `keystore-present == 'true'`) and
UNSIGNED (gated on `!=`) branches. The UNSIGNED branch
preserves the W16 command shape exactly — workflow behaviour
when secrets are absent is byte-identical to W16. New
`docs/mobile-android-signing.md` (~15 KB) — Stephen's
operator runbook covering keystore generation (`keytool
-genkeypair -v -keystore mahjong-autotable.keystore -alias
mahjong-release -keyalg RSA -keysize 4096 -validity 10000`),
4-secret upload (`gh secret set ANDROID_KEYSTORE_BASE64
--body-file <(base64 -i mahjong-autotable.keystore)` etc.),
signed-build verification via `apksigner verify --print-certs`
(expected SHA-256 fingerprint stamping), disaster scenarios
(lost keystore = permanent app-ID burn; wrong password =
build-time error visible in CI logs; key rotation = new
package name required), and W18 iOS deferral (distribution
cert + provisioning profile workflow shape mirrors the
Android pattern; not shipped at W17).

**Not:** ship iOS signing groundwork; provision any actual
secrets (Stephen owns the keystore generation + secret
upload — Apone's W17 commit only wires the consumption
shape). The 15-pin SLSA-3 SHA bump on `mobile-build.yml`
(D5 below) is the only other change to this workflow file
at W17.

### D3 — us-east-1 W17 plan capture (W16 hand-off)

**Why:** the W14 / W15 / W16 dry-run cadence continues at
W17 — Stephen's GO/NO-GO on the live apply was deferred at
W16 with the W17 owner re-running the dry-run + comparing
the capture before any apply PR opens. The W17 owner
(Apone) re-runs the dry-run against the W17 source tip and
captures the result.

**What:** Re-ran `terraform init -input=false` against
`infra/terraform/envs/prod/` (with the local-backend
override pattern documented at W14 + W15 + W16 — see
`docs/us-east-1-w16-plan-output.txt §1`). Zero drift
confirmed (`git diff origin/main..HEAD -- infra/terraform/`
returns empty; the W11 module set + W11 type constraints
unchanged through W17). Plan capture in
`docs/us-east-1-w17-plan-output.txt` (NEW, ~8.6 KB,
8 sections): same partial-capture shape as W16 (1 resource
adds cleanly; 3 expected STS/tfvars errors halt the plan
before AWS-side resources). New §3.6 in `docs/regional-eks-
bringup.md` lands the W17 5-row apply-readiness gate (Row 1
GREEN: zero TF source drift; Row 2 AMBER: `autotable-src-
eager` 214,202 bytes ≈ 209 KB OVER the 200 KB ceiling per
`src/frontend/autotable-src/dist-size.json` K16 entry; Row 3
GREEN: `renderer-webgl2` 19,017 bytes ≈ 19 KB under 40 KB
ceiling; Row 4 GREEN: Kyverno enforce HOLD per D1; Row 5
OPERATOR-DEFERRED: AWS-side creds + tfvars). Overall verdict
**PARTIAL-GREEN/HOLD** — Stephen approval required regardless
once Hicks W18 lifts Row 2 to GREEN (Path A — eager-bundle
re-split) or squad sign-off raises the ceiling to 220 KB
(Path B). New §3.7 W18 hand-off + §3.8 "what W17 doesn't
change".

**Not:** open a `terraform apply` PR; ship any AWS-side
resource changes; modify any `infra/terraform/` source.

### D4 — HPA tuning W17 7-day retrospective

**Why:** the W16 D2 base bump (`minReplicas: 2 → 3`)
propagated the new floor to staging + any future overlay
without an explicit override. W15 + W16 left an open
question — "Does the bumped floor over-provision off-peak
or improve tail-latency at peak?" — answered by 7 days of
lived operational data via Hudson panel survey.

**What:** New `docs/hpa-tuning-w17-retrospective.md` (~11 KB,
9 sections). §2.1 prod-side: layout refactor (W16 inline
JSON-Patch → standalone `hpa-patch.yaml`) operationally
invisible across all four panels — readings statistically
indistinguishable from W14 baseline. §2.2 staging-side:
floor effect visible (replica mean 2.3 → 3.1; p99 CPU 22%
→ 14%; off-peak p50 4% → 2%). §3 over-provisioning analysis:
threshold (CPU < 10% for > 2 h/day) BREACHED on all 7 days
(off-peak mean 3.87 h/day below 10% CPU; cost impact ~$2.30/
month — small absolute but mechanically tripped). §4 peak
analysis: HPA still comfortably auto-scaling (4.7 mean at
peak; no peak-side bump to 4 needed). §5 cron-scheduled-min
override DESIGN: a Kubernetes-native CronJob writing
`minReplicas: 2` at 02:00 UTC + `minReplicas: 3` at 06:00
UTC via `kubectl patch hpa` with a narrowly-scoped
ServiceAccount + Role. §6 prod-side: no W17 action; the
W15 5-replica candidate stays REJECTED. §7 explicit
non-changes (no `infra/k8s/base/hpa.yaml` edits, no overlay
edits, no helm edits, no new CronJob shipped). §8 W18
hand-off: Apone authors the single-PR CronJob delivery as
`infra/k8s/overlays/staging/hpa-min-scheduler-cron.yaml` +
RBAC bundle (~60 lines) once Hudson confirms the schedule's
blast-radius.

**Not:** ship the CronJob YAML at W17; bump
`infra/k8s/overlays/prod/hpa-patch.yaml` `minReplicas`;
revert the W16 base bump. The W17 deliverable is
observation + design only.

### D5 — SLSA-3 SHA pin expansion (W16 hand-off + scope deepening)

**Why:** the W16 D4 pinned 6 action SHAs in
`docker-build.yml` as the first §7b.2.2 deliverable. W17
broadens the same pinning posture across the FULL security-
critical workflow set (sbom, sign, verify, slsa-verify,
scan, mirror, multi-arch, mobile-release). Net new pins
target was +15; W17 actual is +50, reflecting §7b.3
sequencing — every release-pipeline security-critical
surface SHA-pinned in a single contiguous wave reduces
pin-drift risk between workflow edits.

**What:** Nine `.github/workflows/*.yml` files received
action-SHA pins at W17: `sbom.yml` (7), `sign-image.yml`
(2), `verify-signature.yml` (2), `verify-slsa-on-deploy.yml`
(3), `container-scan.yml` (7), `mirror-ghcr-to-ecr.yml` (6),
`multi-arch-runtime.yml` (5), `multi-arch-smoke.yml` (3),
`mobile-build.yml` (15 — companion to D2). All SHAs
resolved via the unauthenticated GitHub API
(`api.github.com/repos/<owner>/<repo>/git/refs/tags/<tag>`
with annotated-tag dereferencing through `git/tags/<sha>`).
Documented in new `docs/slsa-provenance.md §9` (renumbered
W16's §8 cross-references to §10 to make room): per-workflow
pin count table + 17-row SHA reference table + W17 §7b.3
sequence-update table. `slsa-github-generator@v2.0.0` STAYS
un-SHA-pinned per §7c.2's `__BUILDER_ID` regex contract
(unchanged W16 → W17). The pin-resolution working file lives
at `.work/apone-w17-tools/pin-shas-final.txt` (gitignored —
§9.2 SHA reference table is the source-of-truth).

**Not:** pin actions in the three vasquez-lane workflows
(`lane-discipline.yml`, `lane-discipline-nightly.yml`,
`playwright-visual-regression.yml`) — defer to Vasquez to
land those pins in a parallel W17+ commit per
`tests/ci/lane-map.json` lane discipline. Touch the
`slsa-github-generator` pin. Touch any non-security-critical
workflow (e.g. dependabot config).

### D6 — CHANGELOG 0.26.0 + mobile/package.json bump

**Why:** wave-version cadence the W16 entry established
(version bump + CHANGELOG entry per wave) continues at W17.

**What:** `CHANGELOG.md` — new `[0.26.0] — Phase K Wave 17`
entry between `[Unreleased]` and `[0.25.0]`, mirroring the
W16 entry shape (theme paragraph + 6-deliverable subsection
+ "Notes — what the W17 bring-up does NOT touch" footer).
`mobile/package.json` — `version: 0.25.0 → 0.26.0`.

**Not:** bump the backend `Mahjong.Autotable.Api.csproj`
`<Version>` — that pin lives in Bishop-lane
(`src/backend/src/` per `tests/ci/lane-map.json`) and is
deferred to Bishop's W17 commit per the W16 D6 lane-respect
precedent. The task brief mentioned bumping it; Apone DOES
NOT touch csproj because lane discipline overrides task-brief
shape when the two conflict — same call as W16.

---

## Files touched at W17 (apone-lane unless noted)

### `.github/workflows/`

* `sbom.yml` — 7 SHA pins
* `sign-image.yml` — 2 SHA pins
* `verify-signature.yml` — 2 SHA pins
* `verify-slsa-on-deploy.yml` — 3 SHA pins
* `container-scan.yml` — 7 SHA pins
* `mirror-ghcr-to-ecr.yml` — 6 SHA pins
* `multi-arch-runtime.yml` — 5 SHA pins
* `multi-arch-smoke.yml` — 3 SHA pins
* `mobile-build.yml` — 15 SHA pins + Android signing
  groundwork (env block + decode step + SIGNED/UNSIGNED
  Gradle branches)

### `docs/`

* `kyverno-enforce-rollout.md` — appended §11 W17 retro
* `kyverno-enforce-w17-observability.md` (NEW, ~9.9 KB)
* `mobile-android-signing.md` (NEW, ~15 KB)
* `regional-eks-bringup.md` — appended §3.6 + §3.7 + §3.8
* `us-east-1-w17-plan-output.txt` (NEW, ~8.6 KB)
* `hpa-tuning-w17-retrospective.md` (NEW, ~11 KB)
* `slsa-provenance.md` — appended §9 (W17 SHA pin expansion)
* `CHANGELOG.md` — 0.26.0 entry

### App-lane files Apone touches at W17 (unclassified path → lane discipline neutral)

* `mobile/package.json` — version bump 0.25.0 → 0.26.0
  (the `mobile/` path is not in any lane regex per
  `tests/ci/lane-map.json`; the classifier returns
  `unclassified` which is lane-discipline-neutral — same
  as the W16 D6 path)

### Squad / inbox

* `.squad/decisions/inbox/apone-phase-k-wave-17.md` (NEW —
  this memo). Force-added per the standing-directive's
  `.gitignore` workaround (`git add -f`).

---

## Validation results (W17 PR-readiness)

* **actionlint** — exit 0 against `.github/workflows/*.yml`
  (all workflows pass, including the W17 edits to the 9
  security-critical workflows + the W16 NEW
  `mobile-bundle-ci.yml` unchanged).
* **kustomize build infra/k8s/overlays/prod/** — exit 0;
  rendered output is W16 baseline unchanged (no kustomize-
  surface change at W17; D4 HPA retro is observation-only).
* **kustomize build infra/k8s/overlays/staging/** — exit 0;
  rendered output is W16 baseline unchanged.
* **terraform init/validate** in `infra/terraform/envs/prod/`
  — exit 0; W11 module set + W11 type constraints unchanged
  through W17.
* **terraform plan** — partial capture (1 resource plans
  cleanly without AWS creds; remainder operator-side per
  the W14/W16 dry-run shape, documented in
  `docs/us-east-1-w17-plan-output.txt`).
* **Lane-discipline check** — `bash tests/ci/check-cross-
  lane-bundling.sh --pr stlong/phase-k-wave-17-bringup
  --strict` — apone files cleanly classified; no
  vasquez/bishop/hicks files in the W17 commit selective-add
  set.

---

## Hand-offs to W18

* **Kyverno enforce — second 7-day blast-radius watch.** The
  W17 retro narrowed the W16 14-day window to a 7-day
  CLOSED-GREEN; W18 picks up the continuous observability
  slot in `docs/prod-cutover.md §6.7`. Same panels
  (`kyverno-deny-events` + `pod-admission-rate`); rollback
  shape unchanged (single-revert PR per
  `docs/kyverno-enforce-rollout.md §6`).
* **HPA staging off-peak cron min override — single PR
  delivery.** Apone W18 authors `infra/k8s/overlays/staging/
  hpa-min-scheduler-cron.yaml` + RBAC (CronJob +
  ServiceAccount + Role + RoleBinding, ~60 lines) writing
  `minReplicas: 2` at 02:00 UTC + `minReplicas: 3` at 06:00
  UTC. Companion Hudson panel: `hpa-current-replicas-staging-
  by-window` to verify the 02:00–06:00 dip lands.
* **HPA W19 — 14-day post-cron retro.** Apone W19 authors
  `docs/hpa-tuning-w19-retrospective.md` — confirm
  cost-saving + no peak-side evict regression.
* **us-east-1 live apply candidate.** Stephen's call on
  GO/NO-GO at W18 once Hicks W18 lifts Row 2 AMBER (Path A
  — eager-bundle re-split below 200 KB) or squad sign-off
  on Path B (raise ceiling to 220 KB). If GO, Apone W18
  re-runs the dry-run with fresh AWS creds + operator-
  supplied `terraform.tfvars`, opens `stlong/phase-k-wave-
  18-prod-us-east-1-apply`.
* **SLSA-3 W18 items** — dedicated runner pool design memo
  (§7b.2.1), verifier-side builder SHA pin (Kyverno CEL
  update on `kyverno-cosign-verify.yaml` + `verify-slsa-on-
  deploy.yml`), network egress allow-list design (§7b.2.3a),
  hermetic BuildKit design (§7b.2.3b). All four items stay
  W18 targets per the W17 §9.3 sequence-update table.
* **Mobile CI — Stephen secret provisioning.** Stephen
  generates the Android keystore + uploads the four
  `ANDROID_*` secrets per `docs/mobile-android-signing.md
  §3 + §4`; the next `mobile-build.yml` workflow run after
  upload flips the SIGNED branch automatically (no further
  workflow edit required from Apone).
* **iOS distribution cert + provisioning profile groundwork.**
  Apone W18 mirrors the W17 Android pattern in
  `mobile-build.yml` for iOS — env block of `IOS_*` secrets
  + decode step + signed/unsigned Xcode-archive branches.
  Operator runbook `docs/mobile-ios-signing.md` (NEW at W18).
* **Vasquez parallel SHA pin landing.** Vasquez W17+ pins
  actions in `lane-discipline.yml`, `lane-discipline-
  nightly.yml`, `playwright-visual-regression.yml` per the
  D5 lane-respect rationale.

---

## Risk register

| Risk                                                                    | Likelihood | Impact | Mitigation                                                                                                                                       |
|-------------------------------------------------------------------------|------------|--------|--------------------------------------------------------------------------------------------------------------------------------------------------|
| Kyverno enforce 7-day window misses a slow-burn deny pattern            | LOW        | MED    | W18 continues the watch on the same panels; the W16 W17 W18 W19 cumulative 28-day window covers a full monthly traffic cycle.                    |
| Mobile signed-build branch breaks when Stephen uploads malformed secret | LOW        | LOW    | The decode step's `keystore-present` boolean output gates the SIGNED branch on a non-empty `KEYSTORE_BASE64`; a malformed secret falls through to UNSIGNED with a step-level warning. Operator runbook §6.3 documents the disaster recovery. |
| HPA cron-scheduled min override (W18 delivery) introduces 06:00 evict spike | LOW    | LOW    | W17 retro's §5 design specifies a 1-pod-at-a-time re-floor (the CronJob patches `minReplicas` and HPA's standard scale-up sequencing handles the rest; no abrupt 3-pod-at-once admit).                                                       |
| W17 SHA pins go stale before the quarterly refresh                      | MED        | LOW    | Operator-side refresh cadence documented in `docs/slsa-provenance.md §7c.5` (UNCHANGED at W17); pin staleness ≠ pin brokenness.                                                                                                              |
| us-east-1 dry-run drift between W17 capture + W18 apply                 | LOW        | MED    | §3.7 W18 hand-off requires the W18 owner to re-run the dry-run + compare with W17 capture before opening the apply PR.                                                                                                                       |
| `mobile/package.json` 0.26.0 bump conflicts with Hicks's parallel W17 frontend version | LOW | LOW | Hicks owns `src/frontend/`; the `mobile/` shell is a separate npm root + version namespace per the W16 D5 lane-discipline-neutral classification.                                                                                            |

---

## Cross-references

* W16 retro / handoff: `.squad/decisions/inbox/apone-phase-k-
  wave-16.md` §"D1 — Kyverno enforce flip activated" +
  §"D2 — HPA min-replicas 2 → 3 base bump" + §"D3 — us-east-1
  W16 plan capture" + §"D4 — SLSA-3 partial hardening" +
  §"D5 — Mobile bundle CI bootstrap" + §"Hand-offs to W17".
* W17 wave-summary slot: `docs/wave-summaries/phase-k-wave-
  17.md` (to be authored by Vasquez / Stephen during the
  W17 close-out — not part of this commit).
* Lane discipline: `tests/ci/lane-map.json` +
  `tests/ci/check-cross-lane-bundling.sh`; the W17 commit's
  selective-add set respects the lane regex; no shared-file
  rows are triggered. `mobile/package.json` is classified as
  `unclassified` (lane-discipline-neutral) — same as W16 D6.
* Standing directive: per-commit identity hardening via
  `git -c user.name="Apone (DevOps)" -c user.email="apone@
  squad.mahjong"`; flock serialization via
  `.work/squad-git-lock`; selective `git add` (NEVER
  `git add -A` or `git add .`).
