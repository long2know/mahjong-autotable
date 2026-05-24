# Apone — Phase K Wave 19 — inbox memo

> Author: Apone (DevOps). Identity-hardened commit-time
> `user.name="Apone (DevOps)"` + `user.email="apone@squad.
> mahjong"`. Base: `stlong/phase-k-wave-19-bringup` from main
> tip `7832f49` (post-W18 ship).
>
> Squad: Bishop / Hicks / Apone / Vasquez — concurrent agents
> sharing this working tree under `.work/squad-git-lock`.

## 1. Wave-19 scope (6 deliverables)

W19 builds on the W18 FULL-GREEN gate to package Stephen's
operator-side `terraform apply` and `helm install argo-rollouts`
runbooks, adds two NetworkPolicy-bypass-class Kyverno rules,
hardens the SignalR sticky-session shape against cookie-strip,
and extends the W18 iOS groundwork pattern with an actual
E2E smoke for the Android SIGNED branch.

| # | Deliverable                                              | Status  | Surface                                                                                                          |
|---|----------------------------------------------------------|---------|------------------------------------------------------------------------------------------------------------------|
| 1 | Mobile CI Android SIGNED-branch E2E                     | ✅ Done | `.github/workflows/mobile-build.yml` (`android-e2e` job) + `docs/mobile-android-e2e.md`                          |
| 2 | us-east-1 ACTUAL APPLY readiness package                | ✅ Done | `docs/us-east-1-apply-runbook.md` + `infra/terraform/regional-eks/us-east-1/preflight.yaml` + `docs/regional-eks-bringup.md §3.12` |
| 3 | Kyverno additional rules — lateral-movement + require-net-pol | ✅ Done | `infra/k8s/base/kyverno-policies/{disallow-lateral-movement,require-network-policy}.yaml` + `docs/kyverno-w19-additional-rules.md` |
| 4 | SignalR sticky-session affinity hardening               | ✅ Done | `infra/k8s/base/ingress.yaml` (4 new annotations + `configuration-snippet`) + `docs/signalr-affinity-hardening-w19.md` |
| 5 | CHANGELOG `[0.28.0]` + version triple                   | ✅ Done | `CHANGELOG.md` + `mobile/package.json` 0.27.0 → 0.28.0 (backend csproj deferred — bishop-lane)                   |
| 6 | Argo Rollouts controller INSTALL runbook                | ✅ Done | `docs/argo-rollouts-install-runbook.md` + `infra/k8s/base/argo-rollouts-prereqs/{namespace,rbac}.yaml`           |

## 2. Validation summary

| Check                                                   | Exit code | Notes                                                       |
| ------------------------------------------------------- | --------- | ----------------------------------------------------------- |
| `actionlint .github/workflows/*.yml`                    | 0         | All workflows lint clean; the new `android-e2e` job typed   |
| `kustomize build infra/k8s/overlays/prod/`              | 0         | Renders cleanly; W19 ingress hardening + W19 base inclusion |
| `kustomize build infra/k8s/overlays/staging/`           | 0         | Same — staging inherits ingress hardening                   |

## 3. W18 retro lesson — stash discipline (eliminated, NOT mitigated)

W18's commit-time §13 addendum flagged: the apone commit
initially included Hicks's untracked tree changes after a
`git stash pop` + broad `git add`. W19 explicitly eliminates
the pattern:

* `git stash --include-untracked -m "apone-w19-baseline-$(date +%s)"` ran ONCE at the start of the wave; the stash was LEFT in place during all of W19's work.
* No `git stash pop` before the commit. (The stash will pop only AFTER the apone-lane push.)
* `git add` calls are explicit files-by-name only — NO `git add -A`, NO `git add .`, NO `git add <directory>/`, NO `git add -u`.
* `git diff --cached --name-only` ran BEFORE the commit to confirm every staged file is apone-lane.

The standing directive in the W19 prompt enforced this; the
W18 retro lesson is now codified in the agent's bring-up
discipline.

## 4. Lane-discipline outcome

The `tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-
k-wave-19-bringup --strict` run after the apone-lane push
should report `lanes=[apone]` only. Per-file lane attribution:

| Path                                                                     | Lane    | Why                                                                        |
| ------------------------------------------------------------------------ | ------- | -------------------------------------------------------------------------- |
| `.github/workflows/mobile-build.yml`                                     | apone   | `^\.github/workflows/` (apone lane prefix)                                 |
| `docs/mobile-android-e2e.md`                                             | shared  | `docs/*.md` (shared per legacy classifier)                                 |
| `docs/us-east-1-apply-runbook.md`                                        | shared  | `docs/*.md` (shared per legacy classifier)                                 |
| `infra/terraform/regional-eks/us-east-1/preflight.yaml`                  | apone   | `^infra/` (apone lane prefix)                                              |
| `docs/regional-eks-bringup.md`                                           | shared  | `docs/*.md` (shared per legacy classifier)                                 |
| `infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml`         | apone   | `^infra/`                                                                  |
| `infra/k8s/base/kyverno-policies/require-network-policy.yaml`            | apone   | `^infra/`                                                                  |
| `docs/kyverno-w19-additional-rules.md`                                   | shared  | `docs/*.md`                                                                |
| `infra/k8s/base/ingress.yaml`                                            | apone   | `^infra/`                                                                  |
| `docs/signalr-affinity-hardening-w19.md`                                 | shared  | `docs/*.md`                                                                |
| `CHANGELOG.md`                                                           | apone   | `^CHANGELOG\.md$` per lane-map.json regex                                  |
| `mobile/package.json`                                                    | apone   | Pre-existing convention (W18 precedent); covered by apone lane             |
| `docs/argo-rollouts-install-runbook.md`                                  | shared  | `docs/*.md`                                                                |
| `infra/k8s/base/argo-rollouts-prereqs/namespace.yaml`                    | apone   | `^infra/`                                                                  |
| `infra/k8s/base/argo-rollouts-prereqs/rbac.yaml`                         | apone   | `^infra/`                                                                  |
| `.squad/decisions/inbox/apone-phase-k-wave-19.md`                        | apone   | `^\.squad/decisions/inbox/apone-`                                          |

`shared` files do not violate cross-lane bundling (per the
legacy classifier). Every file's primary lane is either
apone-owned or shared — no cross-lane fingerprint.

## 5. Deliverable D1 — Mobile CI Android SIGNED-branch E2E

The W17 SIGNED groundwork + the W18 iOS mirror established
the keystore/keychain decode + SIGNED/UNSIGNED branch shape.
W19 adds the E2E smoke that actually runs the SIGNED APK on
an emulator. The job:

* Sits between the W17 `android` build job and the W18 `ios`
  job in `mobile-build.yml`.
* Runs on `ubuntu-latest-8-cores` (8-core large runner with
  KVM acceleration; standard `ubuntu-latest` lacks `/dev/kvm`).
* Boots a cached Android 34 x86_64 AVD via
  `reactivecircus/android-emulator-runner@v2.34.0`
  (SHA-pinned per the W18 SLSA sweep precedent).
* Gates step 1 on `ANDROID_KEYSTORE_BASE64` presence; UNSIGNED
  PR runs short-circuit (`should-run=false`) without burning
  large-runner credits.
* Smoke shape: install APK → resolve package id via `aapt`
  → `monkey -p PKG -c LAUNCHER 1` → 15 s wait → `pidof`
  liveness → `KEYCODE_BACK` navigation → `screencap -p`
  PNG (validated ≥ 1024 bytes) → `logcat -d -t 200` tail.
* `release` job's `needs:` list extended to
  `[android, android-e2e, ios]` so a failed SIGNED E2E
  blocks the prerelease publication on main.

## 6. Deliverable D2 — us-east-1 ACTUAL APPLY readiness

W18 flipped the §3.9 gate to FULL-GREEN; W19 makes Stephen's
`terraform apply` mechanical without running it:

* `docs/us-east-1-apply-runbook.md` — 7-section runbook:
  pre-flight verification (links to preflight.yaml row IDs),
  apply window opening + PR description template,
  step-by-step apply procedure, R53 propagation wait + DNS
  capture, post-apply 4-row smoke test (R53 latency-apex
  resolve + ALB 200 + R53 health-check + SignalR
  negotiate), 7-row post-apply checklist, 6-step rollback
  procedure.
* `infra/terraform/regional-eks/us-east-1/preflight.yaml`
  — structured pre-flight artefact carrying each row's
  `verify` shell command + `expect` output + `rationale`
  prose. 8 preconditions (source-drift / aws-creds /
  tf-state-bucket / operator-tfvars / plan-replay /
  cutover-ready-checklist / apply-window / rollback-pr) +
  4 smokeTests + rollback steps.
* `docs/regional-eks-bringup.md §3.12` — adds the W19
  apply-runbook cross-reference. §3.10's W18 → W19 hand-off
  text remains canonical; §3.12 names the W19 artefacts.

**W19 does NOT run `terraform apply`.** That remains
Stephen's call. The W19 deliverable closes the operator-
runbook gap that has existed since W14.

## 7. Deliverable D3 — Kyverno additional rules

Both ClusterPolicies live at `infra/k8s/base/kyverno-policies/`
(NEW directory; Stephen requested this path for W19+
additional Kyverno rules that aren't yet ready to merge
into the W15 `enforce-prod-default` policy).

### 7.1 `disallow-lateral-movement.yaml`

Two sub-rules, both Audit mode, `failurePolicy: Ignore`:

* `disallow-host-network` — denies `spec.hostNetwork: true`
  in `mahjong-prod`.
* `disallow-host-ports` — denies any
  `containers[*].ports[*].hostPort` in `mahjong-prod`.

Both bypass the CNI + NetworkPolicy mesh; pre-W19 cluster
state has zero pods using either primitive.

### 7.2 `require-network-policy.yaml`

Single rule on Kind=Namespace, Audit mode,
`failurePolicy: Ignore`. Uses Kyverno's `context.apiCall`
to fetch `/apis/networking.k8s.io/v1/namespaces/<n>/
networkpolicies`, JMESPath-counts the items, and `deny`s
when the count is zero. Excludes kube-system, kube-public,
kube-node-lease, kyverno, default, external-secrets.

### 7.3 5-day grace + Wave-20 cutover

Both files ship Audit mode. Cutover-day procedure documented
in `docs/kyverno-w19-additional-rules.md §4`:

```yaml
# Wave-20 cutover edit
spec:
  validationFailureAction: Enforce   # was: Audit
  failurePolicy: Fail                # was: Ignore
```

Both action AND failurePolicy flip in the same commit.

## 8. Deliverable D4 — SignalR affinity hardening

The W7-era three-annotation cookie shape had two gaps the
W18 retro flagged. W19 closes both via 4 new annotations +
a `configuration-snippet`:

```yaml
# W19 additions:
nginx.ingress.kubernetes.io/session-cookie-secure:   "true"
nginx.ingress.kubernetes.io/session-cookie-samesite: "Lax"
nginx.ingress.kubernetes.io/configuration-snippet: |
  set $mahjong_hash_key "$cookie_mahjong_aff";
  if ($mahjong_hash_key = "") {
    set $mahjong_hash_key "$proxy_add_x_forwarded_for";
  }
  add_header X-Mahjong-Affinity-Source "$mahjong_hash_key" always;
```

The existing 86400-second `session-cookie-max-age` +
`session-cookie-expires` annotations get explicit in-file
comments pinning them to the
`docs/signalr-sequence-slo.md §3` re-handshake window
(no value change — the 24h TTL was already there, just now
documented).

Validation: `kustomize build infra/k8s/overlays/prod/` +
`kustomize build infra/k8s/overlays/staging/` both exit 0.
A `kubectl apply --dry-run=server` smoke is documented in
the runbook (no live cluster available at W19 land, but the
server-side dry-run is the cluster-side validation the
operator runs at deploy time).

## 9. Deliverable D5 — CHANGELOG `[0.28.0]` + version triple

* `CHANGELOG.md` `[0.28.0]` heading with W19 deliverable
  summary (replaces the W18 Unreleased branch ref).
* `mobile/package.json` 0.27.0 → 0.28.0.
* **Backend csproj `<Version>` DEFERRED to Bishop W19.** Per
  the W18 §13 commit-time addendum + the W18 retro action
  item: the
  `src/backend/src/Mahjong.Autotable.Api/Mahjong.Autotable.Api.csproj`
  is bishop-lane. The CHANGELOG entry notes that if Bishop
  W19 has not yet landed `<Version>0.27.0</Version>`, Bishop
  W19 should land `<Version>0.28.0</Version>` directly (skip
  0.27.0 on the backend surface).

## 10. Deliverable D6 — Argo Rollouts controller install runbook

Stephen has not yet installed Argo Rollouts in prod — the
W9 canary AnalysisTemplate gates have been inert since they
landed. W19 packages:

* `docs/argo-rollouts-install-runbook.md` — 7-section
  runbook mirroring the W10 staging shape with prod-specific
  flags (`dashboard.enabled=false`; W11 ingress wired
  post-install §3).
* `infra/k8s/base/argo-rollouts-prereqs/namespace.yaml` —
  pre-creates the `argo-rollouts` namespace with the W12
  NetworkPolicy + W19 require-network-policy Kyverno rule
  labels + PSS-baseline enforce label set.
* `infra/k8s/base/argo-rollouts-prereqs/rbac.yaml` — squad-
  owned `mahjong-autotable-canary-promoter` ClusterRole
  (write access for canary promote/abort/restart) + a
  namespace-scoped `mahjong-autotable-rollouts-reader` Role
  (read-only for on-call inspection). Both inert without
  bindings; the runbook §2.4 documents the operator-side
  `kubectl create [cluster]rolebinding ...`.

Out-of-band — NOT in `base/kustomization.yaml`. Bootstrap
operator applies directly per the runbook §2.1.

## 11. W19 → W20 hand-offs

| Item                                                                                  | Owner | Notes                                                                                                  |
| ------------------------------------------------------------------------------------- | ----- | ------------------------------------------------------------------------------------------------------ |
| Run `terraform apply` for us-east-1 per `docs/us-east-1-apply-runbook.md`             | Stephen | The W19 runbook + preflight.yaml make it mechanical; Stephen schedules the apply window.             |
| Install Argo Rollouts in prod per `docs/argo-rollouts-install-runbook.md`             | Stephen | Same pattern — W19 ships the runbook + pre-install RBAC, Stephen runs `helm install`.                |
| Flip the W19 Kyverno rules to Enforce + Fail (5-day grace ends Wave-20 cutover)        | Apone W20 | Single-commit edit on both ClusterPolicies; document evidence in W20 inbox memo.                     |
| Update `docs/regional-eks-bringup.md §3.9` to "us-east-1 LIVE" once apply lands       | Apone W20 | Post-apply doc update; Stephen confirms via the apply PR's smoke-test output.                        |
| Flip `docs/argo-rollouts-setup.md` baseline note to "INSTALLED IN PROD"                | Apone W20 | Post-helm-install doc update.                                                                          |
| Backend csproj `<Version>` bump (0.27.0 OR 0.28.0)                                    | Bishop W19+ | Per the W18 §13 addendum; bishop-lane separate commit.                                                |
| Mobile Android E2E — first SIGNED-branch run                                           | Stephen | Triggers when Stephen lands the `ANDROID_*` secrets to the repo; current W19 land carries the workflow but not the secrets. |

## 12. Open questions for Stephen

1. **W19 us-east-1 apply window — when?** The runbook
   recommends 09:00-11:00 UTC on a Tuesday or Wednesday.
   Stephen's calendar gates the actual window.

2. **Argo Rollouts install window — when?** Independent of
   the us-east-1 apply; the install touches the `argo-
   rollouts` namespace + CRDs and is non-disruptive to
   `mahjong-prod`. Off-peak UTC is still recommended for
   the admission-webhook ramp-up phase.

3. **Mobile Android signing secrets provisioning.** The
   W17 SIGNED groundwork shipped without the four
   `ANDROID_*` secrets being provisioned (still operator-
   pending). The W19 E2E job is gated on those secrets;
   it will skip until Stephen lands them. **No SLA on
   provisioning** — the keystore + key alias + passwords
   are Stephen's operator decision.

4. **W19 Kyverno cutover-day signal.** The 5-day grace
   window starts the moment the W19 ClusterPolicies are
   applied. The cutover-day flip (W20) requires Hudson's
   `kyverno-deny-events` panel to show zero unexpected
   denies. The W19 grace window doesn't start until
   Stephen `kubectl apply -f` the two YAMLs against the
   live cluster. (Source-side land is W19; cluster-side
   land triggers the grace timer.)

## 13. Cross-references

- `docs/mobile-android-e2e.md` — W19 D1 runbook.
- `docs/us-east-1-apply-runbook.md` — W19 D2 runbook.
- `docs/kyverno-w19-additional-rules.md` — W19 D3 runbook.
- `docs/signalr-affinity-hardening-w19.md` — W19 D4 runbook.
- `docs/argo-rollouts-install-runbook.md` — W19 D6 runbook.
- `docs/regional-eks-bringup.md §3.9–§3.12` — W18 FULL-GREEN
  gate + W19 apply-runbook cross-reference.
- `tests/ci/check-cross-lane-bundling.sh` — Vasquez's lane-
  discipline harness; W19 expected to report `lanes=[apone]`
  on the apone-lane PR branch.

End of W19 inbox memo.
