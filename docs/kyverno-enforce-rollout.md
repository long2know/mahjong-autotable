# Kyverno `audit → enforce` rollout — operator runbook

> Phase K Wave 15 — Apone (DevOps).

This document captures the W15 pre-wire of the
`audit → enforce` Kyverno mode flip and the cutover-day
enablement procedure. It is the W15 hand-off artefact for the
`docs/prod-cutover.md §6.3` Gate-4 hardening calendar slot.

## 1. Where we are (W15 snapshot)

Three Kyverno ClusterPolicies are in play after this wave:

| Policy file                                                      | Wave | Action default                                  | Scope                              |
|------------------------------------------------------------------|------|-------------------------------------------------|------------------------------------|
| `infra/k8s/policies/kyverno-cosign-verify.yaml`                  | W3   | **Audit** + per-NS override (`prod=Enforce`)    | Cluster-wide image signing.        |
| `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`             | W4   | **Enforce**                                     | Prod-only image signing hard-pin.  |
| `infra/k8s/overlays/prod/kyverno-enforce-policies.yaml` (W15 NEW) | W15  | **Enforce** (pre-wired, **commented out**)      | Prod-only default-Enforce floor.   |

The W15 NEW manifest is committed to `infra/k8s/overlays/prod/`
but **not yet referenced** in `kustomization.yaml`. The
`kustomization.yaml` carries a COMMENTED-OUT `resources:` entry
that becomes a one-line uncomment on cutover day. The W14
post-cutover patch enablement pattern (see
`docs/prod-cutover.md §6.8`) is the precedent.

`kustomize build infra/k8s/overlays/prod/` against the W15
commit is **byte-identical** to the W14 baseline — confirmed
locally (`diff .work/apone-w14-safe/prod-build.yaml
.work/apone-w15-safe/prod-build.yaml` → empty).

## 2. Why the seed rule is `require-non-root`

The W15 manifest's single seed rule asserts the **non-root**
Pod security invariant. Three reasons that rule:

* **Independent of the W3/W4 signature chain.** Exercises the
  enforce-mode flip without coupling to the cosign verifier
  side. If the W15 flip surfaces an operational issue, the
  triage is "is the non-root pattern matching as expected?"
  not "did we break image admission?". Clean audit-trail
  separation.
* **The runtime already satisfies it.** The distroless base
  (`gcr.io/distroless/dotnet:8`) runs as UID 65532 by default.
  Adding the `securityContext.runAsNonRoot: true` policy is
  asserting an invariant the cluster already meets — failure
  would be diagnostic (someone added a privileged sidecar
  without flagging it), not a production outage.
* **Hudson `pod-security-violations-prod` panel is green.**
  Zero non-root violations in `mahjong-prod` for the last 30
  days. The enforce flip is operationally safe.

The policy file's seed rule is **the floor**, not the ceiling.
Future waves can append `disallow-host-network`,
`disallow-privileged`, `read-only-root-filesystem`, etc., as
peer rules under the same `validationFailureAction: Enforce`
without re-litigating the action mode.

## 3. Pre-flip readiness gate (Gate 4 contract)

Four pre-conditions MUST be green before the cutover-day
uncomment lands:

| #  | Pre-condition                                                                                                                          | Owner / source                                         |
|----|-----------------------------------------------------------------------------------------------------------------------------------------|--------------------------------------------------------|
| 1  | The W3 `verify-mahjong-images` ClusterPolicy audit window in `mahjong-prod` shows **zero deny events** for ≥ 30 consecutive days.        | Hudson — `kyverno-deny-events` dashboard panel.        |
| 2  | The Hudson `pod-security-violations-prod` panel shows **zero non-root violations** in `mahjong-prod` for ≥ 30 consecutive days.          | Hudson — same panel as above (separate row).           |
| 3  | A **staging rehearsal** ran: apply the W15 manifest to `mahjong-staging`, deploy the live build, confirm zero admission denials.         | Apone — `kubectl apply` in staging cluster.            |
| 4  | The `docs/prod-cutover.md §6.3` squad-review checkbox is signed off (Hudson + Bishop + Apone all ✅ on the readiness PR).                  | Squad — comments on the cutover-day PR.                |

If ANY row is red, the cutover-day PR does NOT merge. Carry to
the next month's hardening calendar slot per
`docs/prod-cutover.md §6.1`.

## 4. Cutover-day procedure (single-PR flip)

The cutover-day PR is a **single-line uncomment** in
`infra/k8s/overlays/prod/kustomization.yaml`:

```yaml
  # DO NOT uncomment until §3 pre-conditions are green.
- # - kyverno-enforce-policies.yaml  # ENABLE PER docs/kyverno-enforce-rollout.md §4
+ - kyverno-enforce-policies.yaml
```

Five-step operator runbook:

1. **Verify pre-conditions.** Capture the four §3 panels as
   screenshots; attach to the cutover-day PR description.
   Stephen reviews + signs off.
2. **Smoke-test in staging.** `kubectl apply -f infra/k8s/
   overlays/prod/kyverno-enforce-policies.yaml -n
   mahjong-staging` (staging cluster, manual). Watch for
   `PolicyReport` events; expect zero denials against the
   current staging workload. `kubectl delete` after.
3. **Merge the uncomment PR.** Cutover-day window is the
   monthly Gate-4 slot per `docs/prod-cutover.md §6.1`.
4. **Apply to prod cluster.** Standard Argo-CD sync OR
   operator-driven `kubectl apply -k infra/k8s/overlays/
   prod/`. Confirm `kubectl get clusterpolicy
   enforce-prod-default
   -o jsonpath='{.spec.validationFailureAction}'` returns
   `Enforce`.
5. **14-day blast-radius watch.** Hudson's `kyverno-deny-
   events` + `pod-admission-rate` panels per `docs/prod-
   cutover.md §6.7`. If either turns red, revert (see §6).

## 5. Invariant check (commented entry is a no-op)

The W15 wire-up edits ONE line of
`infra/k8s/overlays/prod/kustomization.yaml` — a commented
`resources:` entry. The build invariant:

```bash
kustomize build infra/k8s/overlays/prod/ \
  > .work/apone-w15-safe/prod-build.yaml
diff .work/apone-w14-safe/prod-build.yaml \
     .work/apone-w15-safe/prod-build.yaml
# Expected: empty diff.
```

The W15 PR description MUST cite this invariant. If the diff
is non-empty, the wire-up is wrong (someone uncommented the
entry inadvertently or the YAML parser is misreading the
comment) — STOP and review.

## 6. Rollback (post-uncomment)

Single `git revert <merge-commit>` + `kubectl apply -k`. The
W3 + W4 policies remain in place; admission behaviour reverts
to the W14 baseline (W3 audit-default + prod-only Enforce via
override; W4 hard-pin Enforce on the signature chain). The
W15 default-Enforce floor disappears; admission decisions
become stateless again.

No data path is affected. Existing pods continue to run; only
NEW admission decisions follow the reverted policy set.

## 7. W16+ follow-on candidates

Once the W15 enforce flip is in steady-state (W15 + 14 days
green panels), the squad can append additional Enforce-mode
rules to `kyverno-enforce-policies.yaml` without re-running
the §3 pre-flight (the action mode is already proven; only
rule-specific pre-flights apply). Candidates ranked by
operational signal:

| Rule candidate                       | Hudson panel pre-flight                          | Wave target |
|--------------------------------------|--------------------------------------------------|-------------|
| `disallow-host-network`              | `pod-network-violations-prod` (W12 panel, green) | W16         |
| `read-only-root-filesystem`          | `fs-write-violations-prod` (W12 panel, green)    | W16         |
| `disallow-privileged-containers`     | `privileged-pods-prod` (W12 panel, green)        | W17         |
| `require-resource-requests-limits`   | `pod-no-requests-prod` (W12 panel — currently AUDIT-only, conversion required) | W17+       |

The last row is a multi-wave conversion — pods without
explicit resource requests would be rejected today. Audit-only
panel ramp-up is the W16 deliverable; enforce flip is W17+.

## 8. Why this lives at the policy file, not in
   `docs/admission-policy.md`

The W3 `docs/admission-policy.md` covers the **upstream
ClusterPolicy** (cluster-wide cosign verify). This document
covers the **per-overlay supplemental policy** at the prod
overlay layer. The split mirrors the file-system split
(`infra/k8s/policies/` for cluster-wide; `infra/k8s/overlays/
prod/` for prod-only) — operators looking for "what does prod
admission look like?" land here; operators looking for
"how does the cluster default admission work?" land in
`docs/admission-policy.md`.

## 9. Cross-references

* `docs/prod-cutover.md §6.3` — Gate 4 hardening calendar slot.
* `docs/prod-cutover.md §6.7` — per-gate observability panel mapping.
* `docs/prod-cutover.md §6.8` — W14 precedent (post-cutover patch enablement pattern).
* `infra/k8s/policies/kyverno-cosign-verify.yaml` — W3 cluster-wide policy.
* `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml` — W4 prod hard-pin.
* `infra/k8s/overlays/prod/kyverno-enforce-policies.yaml` — W15 pre-wire (this rollout's subject).
* `docs/admission-policy.md` — operator runbook for the W3 cluster-wide policy.
* `Phase_K_W15/Apone/history.md` — W15 wave history with the kustomization-edit + kustomize-build invariant evidence.
