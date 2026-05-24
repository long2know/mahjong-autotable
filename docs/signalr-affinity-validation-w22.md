# SignalR sticky-session validation contract (W22)

> Phase K Wave 22 — Apone (DevOps).
> Audience: SRE / on-call operator landing the W22 SignalR
> sticky-session Kyverno validation contract. Companion to
> [`docs/signalr-affinity-hardening-w19.md`](./signalr-affinity-hardening-w19.md)
> (the W19 hardening this policy guards) and to
> [`docs/signalr-sequence-slo.md`](./signalr-sequence-slo.md)
> (the W12 SignalR re-handshake SLO).

## 1. The contract — what the policy asserts

W19 landed five cookie + snippet annotations on
`infra/k8s/base/ingress.yaml` that together guarantee a
SignalR client lands on the same backend pod across the
WS handshake → upgrade → re-handshake flow. The
[`infra/k8s/base/ingress-validation.yaml`](../infra/k8s/base/ingress-validation.yaml)
ClusterPolicy ASSERTS each annotation at admission time.

Each sub-rule is a separate Kyverno `pattern:` invariant;
a single mis-edit REJECTS the Ingress apply.

### 1.1 `require-affinity-cookie` — `affinity: "cookie"`

W19 enables cookie-based affinity. Dropping the annotation
reverts to round-robin load balancing — the WS upgrade
lands on a fresh pod, breaking the SignalR negotiate →
connect → reconnect flow with a protocol error.

### 1.2 `require-affinity-mode-persistent` — `affinity-mode: "persistent"`

nginx-ingress's `balanced` affinity mode lets the cookie
round-robin to a fresh pod on backend churn. `persistent`
keeps the binding stable for the cookie's TTL. SignalR
requires persistent — a mid-session pod migration drops
all queued frames.

### 1.3 `require-session-cookie-name-mahjong-aff` — `mahjong_aff` cookie name

The cookie name is the stable identifier for the W19
IP-hash fallback. The `configuration-snippet`'s
`$cookie_mahjong_aff` reference is HARDCODED to the
`mahjong_aff` name; renaming the cookie breaks the
fallback path silently — the cookie still works for
present-cookie clients, but stripped-cookie clients lose
affinity entirely.

### 1.4 `require-session-cookie-max-age-86400` — 24h TTL

The 86400-second TTL matches the SignalR
`/hubs/changsha` re-handshake window documented in
[`docs/signalr-sequence-slo.md §3`](./signalr-sequence-slo.md).
Shrinking the TTL causes mid-session pod migrations —
the W11 retro flagged this as a recurring P99 budget
burn at the 5-minute connection cohort.

### 1.5 `require-ip-hash-fallback-snippet` — `configuration-snippet` present

The W19 fallback shape:

```yaml
nginx.ingress.kubernetes.io/configuration-snippet: |
  set $mahjong_hash_key "$cookie_mahjong_aff";
  if ($mahjong_hash_key = "") {
    set $mahjong_hash_key "$proxy_add_x_forwarded_for";
  }
  add_header X-Mahjong-Affinity-Source "$mahjong_hash_key" always;
```

The policy asserts the **annotation is present** — the
W22 launch uses Kyverno's `?*` wildcard pattern ("any
non-empty value"). The exact snippet shape is the W19
hardening's contract; W23+ may tighten the pattern to
assert the `$cookie_mahjong_aff` reference explicitly.

## 2. Why a Kyverno ClusterPolicy (not OPA / mutating webhook)

Kyverno is already the W14/W15/W19/W20/W21/W22 admission-
control substrate for the cluster. Layering a SignalR
contract on top:

* lands in the same RBAC / webhook trust surface — no
  new bootstrap;
* surfaces through the same PolicyReport stream Hudson's
  `kyverno-deny-events` panel consumes;
* carries the same single-`git revert` rollback path.

A mutating webhook is the wrong shape — silently
patching a missing annotation MASKS the operator's
mis-edit. The contract is to REJECT, not REPAIR.

## 3. Audit window + W23 enforce-flip

### 3.1 Audit-mode launch (W22 ship)

The W22 launch sets `validationFailureAction: Audit` +
`failurePolicy: Ignore` — same shape as the W19 + W21
audit-mode launches. The 5-day grace window collects
PolicyReport rows; existing Ingresses that satisfy the
contract surface as `Pass` rows; any diverging Ingress
surfaces as `Fail` for operator review.

### 3.2 Pre-W22 verification (zero-violation baseline)

```bash
$ kubectl -n mahjong-prod get ingress mahjong-autotable \
    -o json \
  | jq -r '.metadata.annotations | to_entries[] |
           select(.key | test("affinity|session-cookie|configuration-snippet")) |
           "\(.key) → \(.value)"'
nginx.ingress.kubernetes.io/affinity → cookie
nginx.ingress.kubernetes.io/affinity-mode → persistent
nginx.ingress.kubernetes.io/session-cookie-name → mahjong_aff
nginx.ingress.kubernetes.io/session-cookie-max-age → 86400
nginx.ingress.kubernetes.io/session-cookie-expires → 86400
nginx.ingress.kubernetes.io/session-cookie-secure → true
nginx.ingress.kubernetes.io/session-cookie-samesite → Lax
nginx.ingress.kubernetes.io/configuration-snippet → set $mahjong_hash_key "$cookie_mahjong_aff"; ...
```

All five W22-asserted annotations are PRESENT on the
current `infra/k8s/base/ingress.yaml`. The audit window
opens with a zero-violation baseline.

### 3.3 Apply order

Out-of-band — NOT in `base/kustomization.yaml`. Same
shape as the W19/W21 Kyverno ClusterPolicies:

```bash
kubectl apply -f infra/k8s/base/ingress-validation.yaml

# Verify:
kubectl get clusterpolicy validate-signalr-sticky-session \
  -o jsonpath='{.spec.validationFailureAction}'
# Audit

kubectl get policyreport -A | grep 'validate-signalr-sticky-session'
# Background scan completes within ~30s; expect five
# Pass rows against the prod Ingress.
```

### 3.4 W23 enforce-flip plan

Cutover-day procedure (same shape as W20 + W22):

1. T-24h — final grace-window snapshot:

   ```bash
   kubectl get policyreport -A | grep 'validate-signalr-sticky-session'
   ```

2. T-0 — flip the policy via a SINGLE commit:

   ```yaml
   spec:
     validationFailureAction: Enforce
     failurePolicy: Fail
   ```

3. T+30m — verify the W23 enforce posture:

   ```bash
   kubectl get clusterpolicy validate-signalr-sticky-session \
     -o yaml | grep -E 'validationFailureAction|failurePolicy'
   ```

4. T+24h — confirm no spurious denials.

Rollback path: single `git revert <enforce-flip-commit>`.

## 4. Synthetic admission-deny smoke (cutover-day evidence)

The W20 + W22 enforce-flips ran a synthetic admission-
deny smoke against a deliberately-broken Ingress copy.
The W23 enforce-flip MUST do the same for each of the
five sub-rules. Procedure:

```bash
# For each invariant, copy the prod Ingress, strip the
# asserted annotation, and assert kubectl apply REJECTS:
for ann in \
    "nginx.ingress.kubernetes.io/affinity" \
    "nginx.ingress.kubernetes.io/affinity-mode" \
    "nginx.ingress.kubernetes.io/session-cookie-name" \
    "nginx.ingress.kubernetes.io/session-cookie-max-age" \
    "nginx.ingress.kubernetes.io/configuration-snippet" ; do
  kubectl -n mahjong-prod get ingress mahjong-autotable -o yaml \
    | yq "del(.metadata.annotations.\"$ann\") | .metadata.name = \"smoke-deny\"" \
    | kubectl apply --dry-run=server -f - 2>&1 \
    | grep -q 'AdmissionRequest denied' && echo "OK: $ann denied"
done
```

Five `OK: <ann> denied` lines confirm the contract is
enforced. Captured into
`.work/apone-w23-evidence/signalr-contract-smoke.txt`
at cutover.

## 5. Cross-references

* [`infra/k8s/base/ingress-validation.yaml`](../infra/k8s/base/ingress-validation.yaml)
  — the W22 ClusterPolicy.
* [`infra/k8s/base/ingress.yaml`](../infra/k8s/base/ingress.yaml)
  — the W7/W19-hardened Ingress this policy guards.
* [`docs/signalr-affinity-hardening-w19.md`](./signalr-affinity-hardening-w19.md)
  — W19 cookie contract this policy enforces.
* [`docs/signalr-sequence-slo.md`](./signalr-sequence-slo.md)
  — SignalR re-handshake SLO (drives the 86400s TTL).
* [`docs/signalr-observability-w21.md`](./signalr-observability-w21.md)
  — W21 PrometheusRule + churn alerting (orthogonal
  observability surface).
* [`docs/kyverno-w19-additional-rules.md`](./kyverno-w19-additional-rules.md)
  — W19 audit→enforce precedent (parallel cadence).
* [`docs/kyverno-w22-additional-rules.md`](./kyverno-w22-additional-rules.md)
  — W22 enforce-flip on the W21 audit pair (parallel
  cadence in the SAME wave).

## 6. W22 → W23 hand-off

* The audit-mode window collects PolicyReport rows for
  five days. W23 enforce-flip is the planned cutover.
* W23+ may tighten `require-ip-hash-fallback-snippet`
  from the `?*` wildcard to an explicit
  `$cookie_mahjong_aff` reference assertion — adds a
  second invariant against accidental fallback removal.
* W23+ may also add a SIXTH sub-rule asserting the
  W19 `session-cookie-secure: "true"` +
  `session-cookie-samesite: "Lax"` cookie-attribute
  hardening — currently observable via the W19
  hardening doc but not yet enforced.
