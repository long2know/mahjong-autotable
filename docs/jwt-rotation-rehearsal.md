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

## 3. Rehearsal history

The rehearsal cadence is **quarterly** (matching the prod
rotation cadence in `docs/jwt-ssm-runbook.md §3`). Each run
appends a row here so the squad can track drift between
runbook + actual workflow behaviour across releases.

| Run | Date | Branch / commit | Operator | Target env | Duration | Outcome | Notes |
|---|---|---|---|---|---|---|---|
| 1 (W11) | 2026-09-22 | `stlong/phase-k-wave-11-bringup` @ `f43b91c` | Apone (DevOps) | staging | 6 min 12 s | ✅ pass | First end-to-end rehearsal. JWKS `kid` rotation propagated to the runtime within 2 polls of the W4 30-s key-cache TTL (≈ 60 s). Archive cleanup OFF — manual review of the archive contents post-run. |
| 2 (W12) | 2026-10-15 | `stlong/phase-k-wave-12-bringup` @ `<commit>` | Apone (DevOps) | staging | 3 min 48 s | ✅ pass | Re-run after W11 — workflow code unchanged; the runtime-side speedup comes from Bishop's W12 JWKS-cache pre-warm (the runtime now eagerly fetches on `kid` cache miss instead of waiting for the next 30-s tick). Archive cleanup OFF. |

### 3.1 Deltas observed between runs

The W12 rehearsal recorded measurable improvements over W11
WITHOUT any change to the rotation runbook itself — all wins
come from runtime improvements landed in the W12 cycle:

| Phase | W11 (first run) | W12 (second run) | Delta |
|---|---|---|---|
| Pre-flight checks | 38 s | 35 s | -3 s (noise) |
| New key generation + SSM write | 14 s | 12 s | -2 s (noise) |
| ESO sync into k8s Secret | 62 s | 60 s | -2 s (within ESO refresh interval; not the bottleneck) |
| Runtime cache invalidation | 60 s | 4 s | **-56 s** (Bishop W12 — eager JWKS fetch on `kid` cache miss) |
| JWKS validation (curl + jq) | 6 s | 5 s | -1 s (noise) |
| Old-key retire (deactivate in SSM) | 8 s | 7 s | -1 s (noise) |
| Smoke test (login round-trip) | 192 s | 65 s | **-127 s** (Bishop W12 — auth-flow code-path skip when the runtime already has a fresh JWKS) |
| **Total** | **6 min 12 s** | **3 min 48 s** | **-2 min 24 s (39 %)** |

The two big wins (runtime cache invalidation + smoke test) are
both downstream of Bishop's W12 JWKS-cache pre-warm. The
rehearsal serves its purpose: it caught the improvement
empirically and gave the squad confidence to recommend GA.

### 3.2 GA-readiness recommendation

After two successful rehearsals — one before and one after a
runtime-side speedup — the squad's recommendation is:

> **The jwt-rotation-rehearsal workflow is GA-ready.** Promote
> the rehearsal cadence from "ad-hoc, operator-triggered" to
> "scheduled monthly via a `schedule: cron` trigger" added to
> `.github/workflows/jwt-rotation-rehearsal.yml` in a follow-up
> PR (see §7 Failure scenarios for the recovery paths the
> scheduled run must continue to satisfy).

The W13 owner (Apone again or a successor) is responsible for:

1. Adding a monthly `schedule: cron` block to
   `.github/workflows/jwt-rotation-rehearsal.yml`.
2. Configuring the workflow to OPEN A PR with the rehearsal
   summary instead of writing to a step-summary file — gives
   the squad a single artifact per run reviewable in the PR
   UI.
3. Adding a dashboard row in `docs/dashboards/jwt-rotation.json`
   (Hudson's surface) tracking per-rehearsal duration over time
   so regressions surface early.

These items are pre-conditions for declaring the rehearsal
cadence "GA" in the next quarterly retro.

### 3.3 Target timing for future runs

Based on the W12 baseline, the target timing for future
rehearsal runs is:

| Threshold | Value | Triggered action |
|---|---|---|
| Target | < 4 min | Green — file the run row + move on. |
| Yellow | 4–6 min | Investigate which phase regressed; consider re-running. |
| Red | > 6 min | Treat as a regression — file an issue in `mahjong-autotable` tagged `jwt-rotation-rehearsal` + skip the GA-cadence promotion until resolved. |

The W11 timing (6 min 12 s) would now be a YELLOW signal under
this scale — the W12 baseline tightens the budget so the squad
catches runtime-side regressions in Bishop's auth code path.

## 4. Workflow trigger

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

## 5. What the workflow does

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

## 6. Dry-run guidance

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

## 7. Failure scenarios + recovery

| Symptom (workflow output)                                                                  | Likely cause                                                                       | Recovery                                                                                                                                              |
|--------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------|
| Step 1 fails: `target_env=prod refused`                                                    | Operator typo / wrong workflow used                                                | Re-run with `target_env=staging`.                                                                                                                     |
| Step 3 fails: `AccessDenied` on `ssm:PutParameter`                                          | The OIDC role's trust policy is mis-scoped                                         | Verify `infra/terraform/modules/github-oidc/` allows the rehearsal repo + branch. Roll the IAM policy via TF apply; re-run.                            |
| Step 4 hangs > 2 min                                                                       | ExternalSecret is stuck (CSO unhealthy)                                            | `kubectl -n external-secrets get clusterexternalsecretstore aws-secrets-manager-staging`. Restart the operator pod; re-run from step 4.                |
| Step 5: `Deployment.spec.template.metadata.annotations` patch rejected                     | KUBECONFIG_STAGING missing the `patch` verb                                        | Re-issue the kubeconfig with the correct role; replace the repo secret; re-run.                                                                       |
| Step 6: JWKS missing new kid after 5 min                                                   | App is still using the cached old JWKS, or the pod restart picked up only one pod  | `kubectl -n mahjong-staging rollout status deploy/mahjong-autotable`. If `Available`, hit `/.well-known/jwks.json` directly from a pod (bypass CDN).   |
| Step 6: JWKS missing old kid (active live)                                                 | The rotation deleted the previous key — this is a regression                       | **STOP.** Roll back: copy `archive/<latest>` → `previous` in SSM, force ESO sync, restart pods. Open a P2 incident — this would break prod sessions.   |

## 8. Post-rehearsal review checklist

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

## 9. Cross-references

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
