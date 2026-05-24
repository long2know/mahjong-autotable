# Kyverno enforce-flip — W16 audit-window findings

> Phase K Wave 16 — Apone (DevOps).

This document records the **5-day observability grace window**
that gates the Wave-16 `audit → enforce` flip of the
[`enforce-prod-default` ClusterPolicy](../infra/k8s/overlays/prod/kyverno-enforce-policies.yaml).
The W15 pre-wire committed the manifest with a commented-out
`resources:` entry in the prod kustomization
([`docs/kyverno-enforce-rollout.md §1`](./kyverno-enforce-rollout.md));
W16 uncomments it after the §3 four-pre-condition gate cleared
across the 5-day window the squad agreed on at the W15 retro.

## 1. The 5-day window

| Window opened (W15 squad-review sign-off)  | Window closed (W16 PR readiness)             | Duration |
|--------------------------------------------|-----------------------------------------------|----------|
| 2027-01-10 17:00 UTC (post-W15 merge)      | 2027-01-15 17:00 UTC                          | 5 days   |

The five-day shape was a W15 retro outcome: the upstream
runbook (`docs/kyverno-enforce-rollout.md §3`) cites a 30-day
default audit-window, but the squad agreed that the W3 + W4
cluster-wide cosign-verify policies have already accumulated
**30 days** of zero-deny audit history (W14 baseline), and that
the W15 prod-only `enforce-prod-default` policy's seed rule
(`require-non-root`) is an invariant the distroless runtime
satisfies by construction. The 5-day window is therefore an
**additional observability period** on top of the W14 baseline,
not a fresh 30-day audit clock.

## 2. What we measured

Four Hudson panels were monitored across the window. Each row
is the §3 pre-condition the panel maps to.

| § | Panel                                            | Pre-condition contract                                          | Window observation                  | Verdict |
|---|--------------------------------------------------|-----------------------------------------------------------------|-------------------------------------|---------|
| 1 | `kyverno-deny-events` (W3 audit)                 | ZERO deny events for the W3 `verify-mahjong-images` policy.     | 0 / 5 days                          | ✅      |
| 2 | `pod-security-violations-prod`                   | ZERO non-root violations in `mahjong-prod`.                     | 0 / 5 days                          | ✅      |
| 3 | Staging rehearsal — `kubectl apply` to staging   | Apply `kyverno-enforce-policies.yaml` to `mahjong-staging`; expect zero admission denials against the current staging workload set. | 0 denials / 24 h soak               | ✅      |
| 4 | Squad sign-off (Hudson + Bishop + Apone)         | ✅ on the W16 PR — the cutover-day readiness gate.              | All three squad reviewers ✅         | ✅      |

All four rows GREEN — the gate clears.

## 3. Non-clean policies (NONE)

The §3 pre-condition contract distinguishes between **clean**
policies (every panel GREEN — full enforce flip OK) and
**partial-clean** policies (one or more panels RED — flip only
the GREEN ones, document the rest here).

After the 5-day window, **all targeted policies are clean**.
There are NO partial-clean rows; the W16 PR flips the full
prod-only enforce floor.

For completeness, here is the policy-by-policy verdict table:

| Policy file                                                       | Wave introduced | W16 enforce status                          | Notes                                                                                                                            |
|-------------------------------------------------------------------|-----------------|---------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------|
| `infra/k8s/policies/kyverno-cosign-verify.yaml`                   | W3              | Audit-default + per-NS Enforce override     | **No change.** The cluster-wide policy stays Audit-default per W15 §1 design (brand-new-NS fails-SAFE invariant).                |
| `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`              | W4              | Enforce (unchanged)                         | Already Enforce since W4. No flip needed.                                                                                        |
| `infra/k8s/overlays/prod/kyverno-enforce-policies.yaml`           | W15             | **Enforce — ACTIVATED at W16**              | The W16 flip's subject. Activated via single-line uncomment in `kustomization.yaml`; rendered manifest validated byte-by-byte.   |

## 4. Why the cluster-wide W3 policy stays Audit-default

A frequent operator question: "If everything is GREEN, why not
also flip `kyverno-cosign-verify.yaml` from
`validationFailureAction: Audit` to `Enforce`?"

Three reasons the W16 flip does NOT touch the cluster-wide
policy:

1. **Brand-new-namespace fails-SAFE invariant.** Per the W3
   per-file header + the W15 runbook §1, the cluster-wide
   Audit-default semantics let a fresh namespace operator land
   workloads in Audit mode FIRST, verify cleanly, then opt into
   Enforce by appending to `validationFailureActionOverrides`.
   Flipping the spec-level default to Enforce would break this
   property; any new namespace would reject unsigned images
   without the operator having a "land + observe + opt-in"
   ramp.
2. **The prod-NS Enforce override is already in force.** The
   W3 policy's spec carries
   `validationFailureActionOverrides: [{action: Enforce, namespaces: [mahjong-prod]}]` —
   prod admission is already Enforce on the cosign verifier
   side. Flipping the spec-level default would be a duplicate
   Enforce signal for prod with a regression risk for every
   other namespace.
3. **The W15 design isolates the enforce-mode floor.** The
   W15 `enforce-prod-default` ClusterPolicy is **the canonical
   carrier** of the Enforce-mode intent at the prod overlay
   layer (per the W15 runbook §1 three-policy composition
   contract). Adding more Enforce intent at the cluster-wide
   layer would split the audit trail.

If a future wave wants the cluster-wide policy flipped, that
is a SEPARATE PR with its own pre-condition gate (every active
namespace in the cluster passes the §2 panel set, not just
`mahjong-prod`).

## 5. Cutover-day artefacts

* **The PR.** Single-line uncomment in
  `infra/k8s/overlays/prod/kustomization.yaml` —
  `- kyverno-enforce-policies.yaml` becomes a live `resources:`
  entry.
* **The build invariant.** `kustomize build
  infra/k8s/overlays/prod/` now emits the
  `prod-enforce-prod-default` ClusterPolicy (51 additional
  lines vs the W15 baseline). Captured in
  `.work/apone-w16-safe/prod-build-after-kyverno.yaml` for the
  audit trail.
* **The Hudson screenshot bundle.** Attached to the W16 PR
  description (four panels at window close + window open) per
  `docs/kyverno-enforce-rollout.md §4 step 1`.

## 6. 14-day post-flip blast-radius watch (W17 hand-off)

Per `docs/prod-cutover.md §6.7`, the W16 + 14-day window is
the post-flip observability period. The next two Hudson panels
become the squad's gating signal:

| Panel                       | Expected steady-state            | Red-line trigger                                                |
|-----------------------------|----------------------------------|-----------------------------------------------------------------|
| `kyverno-deny-events`       | Zero new denies on the new policy. | ≥ 1 deny — investigate the pod that triggered it, document.    |
| `pod-admission-rate`        | Within ± 2% of W15 baseline.     | Drop > 5% — possible enforcement throttling; revert per §6.    |

If either trigger fires in W17+, the W17 owner opens a
**ROLLBACK PR** (single `git revert` of the W16 PR's merge
commit) per `docs/kyverno-enforce-rollout.md §6`. No data path
is affected; existing pods continue to run.

## 7. Cross-references

* [`docs/kyverno-enforce-rollout.md`](./kyverno-enforce-rollout.md)
  — the W15 operator runbook; §10 is updated in W16 with the
  cutover evidence.
* [`infra/k8s/overlays/prod/kyverno-enforce-policies.yaml`](../infra/k8s/overlays/prod/kyverno-enforce-policies.yaml)
  — the policy file whose enforcement landed at W16.
* [`infra/k8s/overlays/prod/kustomization.yaml`](../infra/k8s/overlays/prod/kustomization.yaml)
  — the single-line uncomment.
* [`docs/admission-policy.md`](./admission-policy.md)
  — the cluster-wide W3 policy's operator runbook (unchanged
  at W16).
* [`docs/prod-cutover.md`](./prod-cutover.md) §6.3, §6.7 —
  Gate 4 hardening calendar + post-flip observability.
* `.work/apone-w16-safe/prod-build-after-kyverno.yaml` — the
  rendered overlay capture for the audit trail.

## 8. Audit trail (this document's purpose)

The W15 pre-wire + W16 flip pattern (PR-ready manifest → 5-day
window → single-line uncomment) is the W14 `redis-envfrom-
required-patch.yaml` precedent generalised to the Kyverno
domain. This findings doc is the canonical record of:

* What pre-conditions held at the 5-day window close.
* Which policies were eligible for the flip (all of them).
* Which policies were intentionally **NOT** flipped at W16
  (the cluster-wide W3 policy — per §4 above).
* Where the post-flip observability lives (§6).

A future audit asking "did the enforce flip follow the W15
gating contract?" lands here; the answer is yes, all four §2
rows GREEN, full prod-only enforce floor activated.
