# JWT signing-key rotation — rehearsal workflow

**Status:** Phase K Wave 11 — staging-only.
**Owner:** DevOps lane (Apone) for tooling, on-call SRE for
quarterly drills.
**Pairs with:** [`docs/jwt-ssm-runbook.md`](./jwt-ssm-runbook.md)
(the operator runbook).

---

## 1. Purpose

The W4 hand-off committed to a **90-day quarterly cadence** for
JWT signing-key rotation (`docs/jwt-ssm-runbook.md §3`). The
first such rotation lands at the end of September 2026. Doing
a fresh, hand-typed rotation directly on the prod chain is
high-risk: every active session is invalidated if the operator
flubs the JWKS publish order.

This workflow — [`.github/workflows/jwt-rotation-rehearsal.yml`](../.github/workflows/jwt-rotation-rehearsal.yml)
— is the **rehearsal harness**: a `workflow_dispatch`-only
pipeline that exercises the exact rotation sequence against
**staging** (hard-gated; the workflow refuses to run against
prod). The rehearsal SHOULD be run by the on-call SRE one week
before each quarterly rotation. The first rehearsal runs in
late Sep 2026 ahead of the first real Q3 rotation.

## 2. Prerequisites

* The `staging` env has a healthy ESO ClusterSecretStore named
  `aws-secrets-manager-staging` syncing the
  `mahjong-autotable-jwt` secret (W4 setup —
  `docs/jwt-ssm-runbook.md §2`).
* AWS IAM role `mahjong-staging-rotation-rehearsal` exists with
  trust policy allowing the squad's GitHub OIDC subject AND
  permissions:
  * `ssm:GetParameter`, `ssm:PutParameter`, `ssm:DeleteParameter`
    on `/mahjong/staging/auth/jwt/*`
  * `kms:Encrypt`, `kms:Decrypt`, `kms:GenerateDataKey` on
    `alias/mahjong-staging-secrets`
* Repo secret `KUBECONFIG_STAGING` set to a base64-encoded
  kubeconfig with namespace `mahjong-staging` access (≥ `patch`
  on Secret + `rollout restart` on Deployment).
* Repo secret `STAGING_BASE_URL` set to `https://staging.mahjong.example.com`
  (used by the JWKS validation step).

## 3. Workflow trigger

```
gh workflow run jwt-rotation-rehearsal.yml \
    -f target_env=staging \
    -f new_key_label=2026-09-rehearsal \
    -f archive_cleanup=false \
    --ref stlong/phase-k-wave-11-bringup
```

Inputs:

| Input              | Required | Default        | Description |
|--------------------|----------|----------------|-------------|
| `target_env`       | ✅       | `staging`      | Hard gate — fail-fast if not `staging`. |
| `new_key_label`    | ✅       | (no default)   | Human label for the new key (becomes the JWKS `kid`). Use `YYYY-MM-rehearsal` for clarity. |
| `archive_cleanup`  | ❌       | `false`        | If `true`, delete keys aged > 180 days from the archive bucket at the end of the run. Off by default — the operator reviews archive contents manually first. |

## 4. What the workflow does

The rehearsal mirrors the prod rotation sequence in
`docs/jwt-ssm-runbook.md §4`, step-by-step:

1. **Hard gate.** Refuse to run if `target_env != staging`.
   No `prod` opt-in path; prod is operator-only by design.
2. **Generate a new key pair.** RSA 4096-bit, `openssl genpkey`,
   in an ephemeral runner-local file (cleaned up at end).
3. **Promote keys in SSM:**
   * `/mahjong/staging/auth/jwt/active` → become `previous`
   * The new key becomes `active`
   * The current `previous` (if any) becomes `archive/<timestamp>`
4. **Force ESO sync.** Patch the ExternalSecret with an
   annotation tick to force an immediate refresh (else the
   15-min poll window stalls the test).
5. **Rolling restart** the `mahjong-autotable` Deployment in
   `mahjong-staging` so the new pods read the rotated Secret.
6. **JWKS validation.** Poll `${STAGING_BASE_URL}/.well-known/jwks.json`
   until:
   * The old `kid` (previous) is STILL present (so old
     tokens still verify — zero session disruption).
   * The new `kid` (active) is present.
   * The total `keys` length is ≥ 3 (active + previous +
     ≥ 1 archived).
   Time out at 5 minutes; fail the workflow if any assertion
   fails.
7. **Optional archive cleanup** (only if input `archive_cleanup=true`).
8. **Artefact emit.** Generate `docs/jwt-rotation-rehearsal-YYYY-MM-DD.md`
   with the timing, JWKS state before/after, and any
   anomalies. Upload as a workflow artefact for the post-
   rehearsal review.

## 5. Dry-run guidance

The workflow does NOT have a dry-run flag — rotation is the
test. To validate the workflow itself without rotating keys,
the operator can:

* Run with `new_key_label=dryrun-YYYY-MM-DD` and IMMEDIATELY
  re-run with the previous label to roll the rotation back.
  The W4 cadence (3 keys retained at all times) means up to
  4 rehearsal rotations in a quarter is safe before the
  archive grows beyond the policy ceiling.
* The first rehearsal (late Sep 2026) should be done on a
  Tuesday-Thursday window during business hours so the
  on-call SRE is reachable if JWKS validation fails.

## 6. Failure scenarios + recovery

| Symptom (workflow output)                                                                  | Likely cause                                                                       | Recovery                                                                                                                                              |
|--------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------|
| Step 1 fails: `target_env=prod refused`                                                    | Operator typo / wrong workflow used                                                | Re-run with `target_env=staging`.                                                                                                                     |
| Step 3 fails: `AccessDenied` on `ssm:PutParameter`                                          | The OIDC role's trust policy is mis-scoped                                         | Verify `infra/terraform/modules/github-oidc/` allows the rehearsal repo + branch. Roll the IAM policy via TF apply; re-run.                            |
| Step 4 hangs > 2 min                                                                       | ExternalSecret is stuck (CSO unhealthy)                                            | `kubectl -n external-secrets get clusterexternalsecretstore aws-secrets-manager-staging`. Restart the operator pod; re-run from step 4.                |
| Step 5: `Deployment.spec.template.metadata.annotations` patch rejected                     | KUBECONFIG_STAGING missing the `patch` verb                                        | Re-issue the kubeconfig with the correct role; replace the repo secret; re-run.                                                                       |
| Step 6: JWKS missing new kid after 5 min                                                   | App is still using the cached old JWKS, or the pod restart picked up only one pod  | `kubectl -n mahjong-staging rollout status deploy/mahjong-autotable`. If `Available`, hit `/.well-known/jwks.json` directly from a pod (bypass CDN).   |
| Step 6: JWKS missing old kid (active live)                                                 | The rotation deleted the previous key — this is a regression                       | **STOP.** Roll back: copy `archive/<latest>` → `previous` in SSM, force ESO sync, restart pods. Open a P2 incident — this would break prod sessions.   |

## 7. Post-rehearsal review checklist

Within 24 hours of the rehearsal run, the on-call SRE files
a 2-paragraph note in the next monthly retro (e.g.
`docs/retro-2026-10.md` §3) covering:

* Workflow elapsed time (target: < 4 min). Note any timings
  > 6 min as an action item.
* JWKS state diff (the artefact captures the before/after).
* Any unplanned manual steps required (those become workflow
  bugs to fix before the next quarterly drill).
* Recommendation: ship the matching prod rotation as a
  separate operator-driven PR (NOT this workflow — prod is
  intentionally operator-only).

## 8. Cross-references

* [`.github/workflows/jwt-rotation-rehearsal.yml`](../.github/workflows/jwt-rotation-rehearsal.yml)
  — the workflow itself.
* [`docs/jwt-ssm-runbook.md`](./jwt-ssm-runbook.md) — the
  operator runbook for actual prod rotations (§3 cadence, §4
  rotation steps).
* [`docs/secret-management.md`](./secret-management.md) — KMS
  conventions + rotation policy.
* [`docs/oauth-production-setup.md`](./oauth-production-setup.md)
  §4 — the OIDC chain whose JWKS is being validated.
* [`infra/terraform/modules/github-oidc/`](../infra/terraform/modules/github-oidc/)
  — the IAM module that grants the rehearsal role.
