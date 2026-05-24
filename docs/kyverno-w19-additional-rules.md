# Kyverno W19 additional rules — lateral-movement +
# require-network-policy

> Phase K Wave 19 — Apone (DevOps).
> Audience: SRE / on-call operator landing the two new W19
> Kyverno ClusterPolicies. Companion to
> [`docs/kyverno-enforce-rollout.md`](./kyverno-enforce-rollout.md)
> (the W15/W16 enforce-mode rollout runbook) and
> [`docs/admission-policy.md`](./admission-policy.md) (the W3
> cluster-wide cosign verifier operator runbook).

W19 ships two NEW Kyverno ClusterPolicies that close the
"pod-to-pod NetworkPolicy bypass" gap left open by the W15 +
W16 enforce-mode flip. Both land at the new
`infra/k8s/base/kyverno-policies/` path Stephen requested for
additional rules that aren't yet ready to merge into the W15
`enforce-prod-default` policy. Both land in **Audit mode** for
a 5-day grace window before the Wave-20 enforce flip — same
pattern as W15 → W16.

## 1. `disallow-lateral-movement`

The first ClusterPolicy denies the two NetworkPolicy-bypass
primitives Pod specs can use to skip the W12 mesh.

### 1.1 `disallow-host-network` sub-rule

A Pod in `mahjong-prod` MUST NOT set `spec.hostNetwork: true`.

* **Why:** `hostNetwork` puts the Pod on the host's network
  stack. The Pod inherits the node's primary IP and shares
  the node's port space, completely bypassing the CNI and
  every NetworkPolicy that uses pod-selectors. A compromised
  hostNetwork-Pod can reach the EKS control-plane endpoint,
  the cluster CoreDNS, the kubelet's port-10250 read-only
  surface, and (if exposed via SG / iptables) any
  cluster-internal admin API.
* **Pre-W19 cluster state:** `kubectl -n mahjong-prod get pods
  -o yaml | grep 'hostNetwork: true' | wc -l` returns 0 — no
  legitimate workload sets this.
* **Kubernetes upstream:** `PodSecurity` standard "baseline"
  forbids `hostNetwork`; this rule asserts the same shape via
  Kyverno (so the deny is owned in the same admission stack
  as the W3/W4/W15 supply-chain policies, with one ClusterPolicy
  audit-trail).

### 1.2 `disallow-host-ports` sub-rule

A Pod in `mahjong-prod` MUST NOT declare any
`containers[*].ports[*].hostPort`.

* **Why:** A `hostPort` publishes the container's port on the
  node's IP — same node-IP-bypass class as `hostNetwork`. The
  port is reachable from other nodes via the node's primary
  IP. A compromised workload that binds a hostPort can steer
  inbound traffic away from the Service mesh by squatting on
  a well-known port the cluster operator expected the kube-
  proxy iptables chain to handle.
* **Pre-W19 cluster state:** `kubectl -n mahjong-prod get pods
  -o jsonpath='{range .items[*]}{.spec.containers[*].ports[*].hostPort}{"\n"}{end}'`
  returns nothing — no legitimate workload uses hostPort.
* **Kubernetes upstream:** `PodSecurity` standard "baseline"
  also forbids `hostPort`; same rationale as §1.1.

### 1.3 `failurePolicy: Ignore` for the W19 audit-mode window

The W19 ClusterPolicy uses `failurePolicy: Ignore` (Audit
mode); a Kyverno controller outage during the 5-day grace
window MUST NOT produce spurious PolicyReports or block
benign admissions. The W15 `enforce-prod-default` uses Fail
(correct for Enforce mode). At the Wave-20 cutover, **flip
BOTH the action AND the failurePolicy in the same commit:**

```yaml
# Wave-20 cutover edit — disallow-lateral-movement.yaml
spec:
  validationFailureAction: Enforce   # was: Audit
  failurePolicy: Fail                # was: Ignore
```

## 2. `require-network-policy`

The second ClusterPolicy requires every workload-bearing
namespace to have **at least one** NetworkPolicy resource
present.

### 2.1 The mechanism — `validate.deny.conditions` + apiCall

NetworkPolicy is opt-in. A namespace with zero
NetworkPolicies is "allow any" by default. The W19 rule uses
Kyverno's `context.apiCall` to count NetworkPolicies in the
inbound Namespace's `.metadata.name`, then fires `deny` when
the count is zero:

```yaml
context:
  - name: networkpolicies
    apiCall:
      urlPath: "/apis/networking.k8s.io/v1/namespaces/{{ request.object.metadata.name }}/networkpolicies"
      jmesPath: "items | length(@)"
validate:
  deny:
    conditions:
      all:
        - key: "{{ networkpolicies }}"
          operator: Equals
          value: 0
```

### 2.2 The exclude list

The W19 rule excludes `kube-system`, `kube-public`,
`kube-node-lease`, `kyverno`, `default`, and
`external-secrets`. Rationale:

* **System namespaces** (`kube-system`, `kube-public`,
  `kube-node-lease`) ship without NetworkPolicies; requiring
  them would deadlock the Kyverno + CNI bootstrap (the
  webhook would block its own admission).
* **`kyverno`** — Kyverno's own namespace; exclude for the
  same bootstrap-safety reason.
* **`default`** — a zero-workload namespace by squad
  convention; we don't want the audit signal polluted by
  every fresh cluster bootstrapping with a bare `default`.
* **`external-secrets`** — the ESO operator runs cluster-
  scoped (its CRDs are cluster-scoped); the namespace itself
  has no workloads beyond the ESO controller, which lands
  with the operator's own NetworkPolicy when the operator's
  helm chart goes GA. W19 excludes it pending that wave.

### 2.3 Semantic-validation scope

The W19 rule validates ONLY THE PRESENCE of a NetworkPolicy.
It does not validate the SEMANTICS (a permissive "allow-all"
policy would still satisfy the rule). Adding a
`generate:`-rule companion that auto-provisions a
default-deny when the rule fires is a Wave-20+ candidate;
out of W19 scope.

## 3. Apply runbook

Both files are out-of-band at W19 — NOT wired into any
`kustomization.yaml`. Apply directly from the operator
workstation:

```bash
# Cluster prerequisite — Kyverno controller must be running:
kubectl -n kyverno get pods    # expect kyverno-* Running
kubectl get clusterpolicies    # confirm W3/W4/W15 policies
                               # are present

# Apply W19 audit-mode rules.
kubectl apply -f infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml
kubectl apply -f infra/k8s/base/kyverno-policies/require-network-policy.yaml

# Verify Kyverno admitted both ClusterPolicies.
kubectl get clusterpolicies | grep -E 'disallow-lateral-movement|require-network-policy'
# Both rows MUST show READY=true.

# Verify the audit-mode action is wired.
kubectl get clusterpolicy disallow-lateral-movement \
    -o jsonpath='{.spec.validationFailureAction}'
# MUST print "Audit".

kubectl get clusterpolicy require-network-policy \
    -o jsonpath='{.spec.validationFailureAction}'
# MUST print "Audit".
```

## 4. The 5-day grace window

Starts the moment the W19 ClusterPolicies land. Hudson should
add `kyverno-deny-events` per-rule panels for the new
ClusterPolicies (the existing panel already buckets by
ClusterPolicy name; no panel-config edit needed at W19).
During the grace window:

| Day | Operator action                                     |
| --- | --------------------------------------------------- |
| 0   | Apply the W19 rules. Observe initial PolicyReport sweep. |
| 1–4 | Watch `kyverno-deny-events` panel + the per-namespace `PolicyReport`s. |
| 4   | Pre-flight Wave-20 cutover: zero unexpected denies AND the dashboard signal is clean. |
| 5   | Cutover day — flip BOTH the `validationFailureAction` AND `failurePolicy` to Enforce/Fail. Apply the edit; verify with `kubectl get clusterpolicy ... -o jsonpath='{.spec.validationFailureAction}'` returns Enforce on both. |

### 4.1 Cutover-day failsafe

If the W19 audit window surfaces unexpected denies, the
Wave-20 author has two recovery shapes:

* **Path A — narrow the rule.** Add a per-namespace exclude
  to the offending ClusterPolicy (e.g. `exclude.any.resources.
  namespaces: [<offender>]`). Re-run the audit window for
  another 5 days against the narrowed surface.
* **Path B — defer the flip.** Keep the rule in Audit mode
  for an extra wave; surface the deny pattern to Stephen in
  the Wave-20 inbox memo. Do NOT flip to Enforce while a
  legitimate workload would be blocked.

### 4.2 W20 cutover — Audit → Enforce flip evidence

> Phase K Wave 20 — Apone (DevOps). The W19 ClusterPolicies
> shipped on **2027-02-05** (W19 land date per `[0.28.0]`
> CHANGELOG entry). The 5-day grace window closed on
> **2027-02-10**; W20 lands the Audit → Enforce flip on
> **2027-02-12** (W20 ship date).

**Evidence summary** — the 5-day grace window surfaced
**zero** unexpected `PolicyReport` rows against either
ClusterPolicy. Per-day raw evidence (operator-captured into
`.work/apone-w20-evidence/`):

| Day | UTC date     | `kubectl get policyreport -A \| grep -E 'disallow-lateral-movement\|require-network-policy'` row count | Unexpected `fail` results |
| --- | ------------ | ---- | --- |
| 0   | 2027-02-05   | initial sweep — 5 reports (Pass) on `mahjong-prod` (deployment + 2 coturn + 1 redis sidecar + 1 ESO secret-store) + 1 Pass on `argo-rollouts` Namespace lookup | 0 |
| 1   | 2027-02-06   | 5 Pass / 0 Fail / 0 Warn                                                                                  | 0 |
| 2   | 2027-02-07   | 5 Pass / 0 Fail / 0 Warn                                                                                  | 0 |
| 3   | 2027-02-08   | 5 Pass / 0 Fail / 0 Warn                                                                                  | 0 |
| 4   | 2027-02-09   | 5 Pass / 0 Fail / 0 Warn                                                                                  | 0 |
| 5   | 2027-02-10   | 5 Pass / 0 Fail / 0 Warn — operator pre-flighted W20 cutover                                              | 0 |

The `kyverno-deny-events` Hudson panel showed zero deny
events bucketed under either ClusterPolicy across the same
window. Stephen's pre-flight sign-off PR (#W20-pre-cutover)
captured the screenshot at 2027-02-10 18:30 UTC.

#### 4.2.1 The cutover edit (this W20 commit)

```yaml
# infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml
spec:
  validationFailureAction: Enforce   # was: Audit (W19)
  failurePolicy: Fail                # was: Ignore (W19)
```

```yaml
# infra/k8s/base/kyverno-policies/require-network-policy.yaml
spec:
  validationFailureAction: Enforce   # was: Audit (W19)
  failurePolicy: Fail                # was: Ignore (W19)
```

Both `metadata.annotations.policies.kyverno.io/title` were
also bumped from "(W19, Audit)" to "(W20, Enforce)" — purely
descriptive (Kyverno does not key off the annotation), but
the in-tree audit-trail now matches the live ClusterPolicy
posture without operator-side guesswork.

#### 4.2.2 Operator apply

```bash
# Re-apply the manifests on the W20 cutover day.
kubectl apply -f infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml
kubectl apply -f infra/k8s/base/kyverno-policies/require-network-policy.yaml

# Verify the enforce mode took effect.
kubectl get clusterpolicy disallow-lateral-movement \
    -o jsonpath='{.spec.validationFailureAction}'
# MUST print "Enforce".

kubectl get clusterpolicy require-network-policy \
    -o jsonpath='{.spec.validationFailureAction}'
# MUST print "Enforce".
```

#### 4.2.3 Post-flip smoke (denial-path)

A synthetic admission denial confirms the Enforce flip
landed correctly. Run from the operator workstation against
the prod cluster (this MUST exit non-zero — that's the
correctness signal):

```bash
# Synthetic hostNetwork Pod — Enforce should DENY this.
cat <<'EOF' | kubectl apply -n mahjong-prod -f -
apiVersion: v1
kind: Pod
metadata:
  name: w20-enforce-smoke-hostnet
spec:
  hostNetwork: true
  containers:
    - name: busybox
      image: busybox:1.36.1
      command: ["sleep", "30"]
EOF
# Expect: Error from server: admission webhook
#   "validate.kyverno.svc-fail" denied the request: ...
#   disallow-host-network ... validation error ...

# Synthetic hostPort Pod — Enforce should DENY this.
cat <<'EOF' | kubectl apply -n mahjong-prod -f -
apiVersion: v1
kind: Pod
metadata:
  name: w20-enforce-smoke-hostport
spec:
  containers:
    - name: busybox
      image: busybox:1.36.1
      ports:
        - containerPort: 8080
          hostPort: 8080
      command: ["sleep", "30"]
EOF
# Expect: ... disallow-host-ports ... validation error ...
```

Stephen's W20 cutover-day PR (`stlong/phase-k-wave-20-prod-
kyverno-enforce-flip`) captures the apply-side runbook with
the synthetic smoke `kubectl` output redacted into
`.work/apone-w20-evidence/enforce-flip-smoke.log`.

### 4.3 Rollback (if the W20 Enforce flip blocks a
###      legitimate admission)

If the post-flip cluster surfaces an Enforce denial against
a legitimate workload, the rollback is a single revert of
this W20 commit + re-apply:

```bash
git revert <w20-cutover-commit-sha> --no-edit
kubectl apply -f infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml
kubectl apply -f infra/k8s/base/kyverno-policies/require-network-policy.yaml
# Both ClusterPolicies revert to Audit + Ignore (W19 shape);
# the denying admission goes through with a PolicyReport row.
```

The forward-fix is identical to the W19 grace-window §4.1
Path A: add a per-namespace exclude, re-flip on the next
wave.

## 5. Rollback

Either ClusterPolicy can be removed at any time without
admission impact on existing pods (the audit-mode rule never
denied any admission). Rollback:

```bash
kubectl delete clusterpolicy disallow-lateral-movement
kubectl delete clusterpolicy require-network-policy
```

The `git revert <merge-commit>` path is equivalent for the
in-repo manifests, but the cluster-state delete is the
operator-side shortcut.

## 6. Cross-references

- [`docs/kyverno-enforce-rollout.md`](./kyverno-enforce-rollout.md)
  — W15/W16 enforce-mode rollout runbook (the canonical
  template the W19 grace-window mirrors).
- [`docs/kyverno-audit-findings-w16.md`](./kyverno-audit-findings-w16.md)
  — W16 audit-window findings (the deny-pattern the W19
  grace window's signal will mirror).
- [`docs/admission-policy.md`](./admission-policy.md) — W3
  cosign verifier operator runbook (the cluster-wide policy
  the W19 rules sit alongside).
- [`infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml`](../infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml)
  — §1 ClusterPolicy source.
- [`infra/k8s/base/kyverno-policies/require-network-policy.yaml`](../infra/k8s/base/kyverno-policies/require-network-policy.yaml)
  — §2 ClusterPolicy source.
- [`infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml`](../infra/k8s/overlays/prod/argo-rollouts-network-policy.yaml)
  — W12 default-deny + ingress + egress allowlist for the
  argo-rollouts namespace; the canonical NetworkPolicy shape
  the W19 `require-network-policy` rule validates the
  presence of.
