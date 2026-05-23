# Kubernetes admission policy — Kyverno + cosign image verification

> Phase K Wave 3 — Apone (DevOps).

This runbook covers the cluster-layer enforcement of the supply-chain
signing contract shipped in [Phase K Wave 1 (`sign-image.yml`)](image-signing.md)
and [Wave 2 (`verify-signature.yml`)](../.github/workflows/verify-signature.yml).

The policy file lives at
[`infra/k8s/policies/kyverno-cosign-verify.yaml`](../infra/k8s/policies/kyverno-cosign-verify.yaml)
and ships a single `ClusterPolicy` named **`verify-mahjong-images`**
that REFUSES to admit a Pod (or controller spec — Deployment /
StatefulSet / DaemonSet / Job / CronJob) whose `image:` field
matches `ghcr.io/long2know/mahjong-autotable:*` unless the image
carries a valid cosign keyless signature.

## 1. Why a third enforcement layer

Wave 1 + Wave 2 enforce the contract IN CI:

| Wave | Workflow | What it gates |
|------|----------|---------------|
| W1   | `.github/workflows/sign-image.yml`      | Signs every multi-arch manifest list published on `main` / `v*.*.*` with cosign keyless OIDC, records the signature in Rekor. |
| W2   | `.github/workflows/verify-signature.yml`| Reusable `workflow_call` that verifies a given digest matches the canonical signer identity. Wired into `release.yml` as a pre-publish gate. |
| W3   | `infra/k8s/policies/kyverno-cosign-verify.yaml` | THIS file — admission-time verification at the cluster boundary. |

CI gates can be bypassed (operator commits directly to `main` without
opening a PR; Argo CD sync from a branch the verify workflow hasn't
seen). The cluster-layer policy closes that last gap: even a
hand-rolled `kubectl apply -f` against `mahjong-prod` for an unsigned
image is rejected at admission time.

End-to-end, the three layers compose a defense-in-depth shape:

```
push/PR
   │
   ▼
sign-image.yml (Wave 1)  ─── Rekor (signature record)
   │
   ▼
release.yml + verify-signature.yml (Wave 2)
   │
   ▼  kubectl apply / Argo sync
   │
verify-mahjong-images (Wave 3)  ◄── this runbook
   │
   ▼
admitted Pod
```

## 2. Install Kyverno

Kyverno is the de-facto k8s policy engine for cosign-based image
verification (its `verifyImages` rule type ships native cosign
support — no external policy-controller needed). Install via the
upstream Helm chart:

```bash
helm repo add kyverno https://kyverno.github.io/kyverno/
helm repo update
helm install kyverno kyverno/kyverno \
    --namespace kyverno \
    --create-namespace \
    --version 3.2.7 \
    --set admissionController.replicas=3 \
    --set backgroundController.replicas=2 \
    --set cleanupController.replicas=2 \
    --set reportsController.replicas=2
```

The replica counts above are the production-recommended HA settings.
For a single-node dev cluster, `replicas=1` for every component is
fine.

Verify Kyverno is up:

```bash
kubectl -n kyverno get pods
# Expect: all pods Running, all webhooks healthy.
kubectl get crd | grep kyverno
# Expect: clusterpolicies.kyverno.io, policies.kyverno.io, etc.
```

## 3. Apply the policy

```bash
kubectl apply -f infra/k8s/policies/kyverno-cosign-verify.yaml
```

Verify it loaded:

```bash
kubectl get clusterpolicy verify-mahjong-images -o jsonpath='{.status.ready}'
# Expect: true

kubectl describe clusterpolicy verify-mahjong-images | head -30
# Expect: Spec.ValidationFailureAction: Audit
#         Spec.ValidationFailureActionOverrides:
#           Enforce: [mahjong-prod]
#           Audit:   [mahjong-staging]
```

## 4. Action-mode semantics

The policy ships with **Audit globally** + per-namespace overrides:

| Namespace          | Action  | Behaviour                              |
|--------------------|---------|----------------------------------------|
| `mahjong-prod`     | Enforce | Admission REJECTED for unsigned images |
| `mahjong-staging`  | Audit   | Pod admitted; PolicyReport logged      |
| (any other ns)     | Audit   | Pod admitted; PolicyReport logged      |

A new namespace defaults to Audit so the policy fails SAFE — the
operator MUST explicitly add the namespace to the Enforce list
after verifying the workloads in that namespace consume signed
images cleanly. Adding to the Enforce list is a one-line patch:

```yaml
spec:
  validationFailureActionOverrides:
    - action: Enforce
      namespaces:
        - mahjong-prod
        - mahjong-prod-eu          # ← add here
```

## 5. Test the policy

### 5.1 Positive test — signed image admits

Apply a Deployment using the canonical signed image:

```bash
cat <<'EOF' | kubectl -n mahjong-prod apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: cosign-positive-test
spec:
  replicas: 1
  selector:
    matchLabels: { app: cosign-positive-test }
  template:
    metadata:
      labels: { app: cosign-positive-test }
    spec:
      containers:
        - name: app
          image: ghcr.io/long2know/mahjong-autotable:latest
EOF
```

Expected:

```
deployment.apps/cosign-positive-test created
```

The pod admits. `kubectl describe pod` should show the image
rewritten to a digest-qualified reference (the policy's
`mutateDigest: true` setting). Clean up:

```bash
kubectl -n mahjong-prod delete deployment cosign-positive-test
```

### 5.2 Negative test — unsigned image rejects

Apply a Deployment using an INTENTIONALLY-unsigned image (any tag
not produced by `sign-image.yml`, e.g. a hand-pushed local build):

```bash
cat <<'EOF' | kubectl -n mahjong-prod apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: cosign-negative-test
spec:
  replicas: 1
  selector:
    matchLabels: { app: cosign-negative-test }
  template:
    metadata:
      labels: { app: cosign-negative-test }
    spec:
      containers:
        - name: app
          image: ghcr.io/long2know/mahjong-autotable:dev-unsigned-test
EOF
```

Expected:

```
Error from server: error when creating "STDIN":
admission webhook "validate.kyverno.svc-fail" denied the request:

resource Deployment/mahjong-prod/cosign-negative-test was blocked due to the following policies

verify-mahjong-images:
  verify-cosign-keyless-mahjong: |
    failed to verify image ghcr.io/long2know/mahjong-autotable:dev-unsigned-test:
    .attestors[0].entries[0].keyless: no matching signatures
```

The same Deployment applied to `mahjong-staging` (Audit mode) admits
but a PolicyReport is created:

```bash
kubectl -n mahjong-staging get policyreport
NAME                                          PASS   FAIL   WARN   ERROR   SKIP   AGE
cpol-verify-mahjong-images                    0      1      0      0       0      5s

kubectl -n mahjong-staging get policyreport cpol-verify-mahjong-images -o yaml
# Expect: results[0].result: fail, results[0].message references the
#         unsigned image.
```

### 5.3 Canary procedure for the Wave-4 prod hard-pin

The Wave-4 supplemental policy at
[`infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`](../infra/k8s/overlays/prod/kyverno-enforce-patch.yaml)
adds a SECOND ClusterPolicy (`enforce-prod-mahjong-images`)
scoped exclusively to `mahjong-prod`. It's a fail-safe pin —
regardless of what `verify-mahjong-images` may be configured to
do globally, prod rejects unsigned images. The end-to-end canary
that proves both policies are wired correctly:

**Step 1 — build + push the unsigned canary image.** This is the
ONLY image we ever intentionally publish unsigned. Tag it so
nobody mistakes it for a release artefact:

```bash
docker build -t ghcr.io/long2know/mahjong-autotable:dev-unsigned-canary .
docker push  ghcr.io/long2know/mahjong-autotable:dev-unsigned-canary
# DO NOT run sign-image.yml against this digest; the canary's whole
# purpose is to be unsigned.
```

**Step 2 — staging admit + warn.** Apply the canary to staging.
The Wave-3 cluster policy in Audit mode for `mahjong-staging`
should ADMIT the pod AND log a failing PolicyReport:

```bash
cat <<'EOF' | kubectl -n mahjong-staging apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: cosign-canary-staging
spec:
  replicas: 1
  selector: { matchLabels: { app: cosign-canary-staging } }
  template:
    metadata: { labels: { app: cosign-canary-staging } }
    spec:
      containers:
        - name: app
          image: ghcr.io/long2know/mahjong-autotable:dev-unsigned-canary
EOF

# Expect: deployment created, pod running (admit), PolicyReport "fail":
kubectl -n mahjong-staging get pods -l app=cosign-canary-staging
kubectl -n mahjong-staging get policyreport
```

**Step 3 — prod reject.** Try to deploy the SAME image to prod.
BOTH the Wave-3 policy (`verify-mahjong-images`, Enforce override
for `mahjong-prod`) AND the Wave-4 hard-pin
(`enforce-prod-mahjong-images`, single-purpose Enforce on
`mahjong-prod`) should reject the admission:

```bash
cat <<'EOF' | kubectl -n mahjong-prod apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: cosign-canary-prod
spec:
  replicas: 1
  selector: { matchLabels: { app: cosign-canary-prod } }
  template:
    metadata: { labels: { app: cosign-canary-prod } }
    spec:
      containers:
        - name: app
          image: ghcr.io/long2know/mahjong-autotable:dev-unsigned-canary
EOF

# Expect: admission webhook denied the request, message references
#         BOTH policies (the request is gated by EITHER firing).
```

**Step 4 — clean up:**

```bash
kubectl -n mahjong-staging delete deployment cosign-canary-staging
# Prod resource never created (Step 3 failed at admission).
# Optional: delete the unsigned image from GHCR via the UI / API.
```

The canary should be run after every Kyverno upgrade + after
every Wave-N change that touches either policy file. It's the
only smoke that proves the cluster-layer + namespace-layer
enforcement composes correctly under realistic edit-pressure.

## 6. Observability

### 6.1 Policy Reports (staging)

Audit-mode failures land in PolicyReports. Surface them in your
monitoring stack:

```bash
# Snapshot:
kubectl get policyreport -A | grep -v '0\s\+0\s\+0\s\+0' | head -20

# Stream as part of cluster-wide audit:
kubectl get policyreport -A -w
```

Prometheus exporter (Kyverno's `kyverno-policy-reports-controller`
metrics endpoint, scraped automatically by the Helm-shipped
`ServiceMonitor` when prometheus-operator is installed): the metric
`policy_results_total{policy="verify-mahjong-images",result="fail"}`
should remain at 0 in staging.

### 6.2 Webhook failure events (prod)

Enforce-mode rejections produce k8s admission `Event`s on the
parent ReplicaSet:

```bash
kubectl -n mahjong-prod get events --field-selector reason=FailedCreate | head
```

Alert rule (suggested, Prometheus AlertManager):

```yaml
- alert: KyvernoCosignVerifyDeniedAdmission
  expr: increase(kyverno_admission_review_denied_total{policy="verify-mahjong-images"}[5m]) > 0
  for: 1m
  labels: { severity: critical }
  annotations:
    summary: "Kyverno blocked an unsigned mahjong image"
    description: "verify-mahjong-images denied {{ $value }} admissions in last 5min. Investigate which Deployment + image triggered it."
```

## 7. Maintenance

### 7.1 Rename the signing workflow

If `sign-image.yml` is ever renamed or relocated, the
`subjectRegExp` in this policy MUST be updated in lock-step with:

* `.github/workflows/sign-image.yml` (the signer itself)
* `.github/workflows/verify-signature.yml` (default
  `expected-identity-pattern` input)
* `infra/k8s/policies/kyverno-cosign-verify.yaml` (this file)

All three live under the canonical-signer-URL invariant — change
one, change all three; otherwise verification mismatch.

### 7.2 Cosign upgrade

Kyverno's `verifyImages` uses a built-in cosign client (no separate
binary install needed in-cluster). The cosign protocol is stable
across 2.x → 2.4.x; signature artefacts produced by older cosign
versions remain verifiable by newer versions. No coordinated upgrade
required when bumping cosign in `sign-image.yml`.

### 7.3 New canonical signer (e.g. release.yml signs the SBOM)

Phase K Wave 3 also added an SBOM-signing step to `release.yml`
(see [`docs/sbom.md`](sbom.md) and the
[release.yml `verify-sbom` job](../.github/workflows/release.yml)).
The Kyverno policy here covers IMAGES only — SBOM signatures
verify out-of-band via `cosign verify-blob`. If/when a future wave
ships per-attestation in-toto verification (e.g. SLSA provenance
predicates attached to the image), add a second `attestors` block
to this policy whose `subjectRegExp` matches `…/release.yml@refs/tags/v*$`.

## 8. Cross-references

* [`docs/image-signing.md`](image-signing.md) — Wave 1 keyless signing rationale + verification procedure.
* [`.github/workflows/sign-image.yml`](../.github/workflows/sign-image.yml) — canonical signer.
* [`.github/workflows/verify-signature.yml`](../.github/workflows/verify-signature.yml) — reusable verify gate.
* [`.github/workflows/release.yml`](../.github/workflows/release.yml) — pre-publish image + SBOM gates.
* [`docs/sbom.md`](sbom.md) — SBOM generation + signing.
* [Kyverno docs — Image Verification](https://kyverno.io/docs/writing-policies/verify-images/)
* [Sigstore — Cosign Keyless](https://docs.sigstore.dev/cosign/signing/overview/)
