# Kyverno W23 — audit-mode launch (4th batch)

> Phase K Wave 23 — Apone (DevOps).
> Audience: SRE / on-call operator running the W23 → W28
> audit window on the W23 Kyverno rule pair. Companion
> to
> [`docs/kyverno-w22-additional-rules.md`](./kyverno-w22-additional-rules.md)
> (the W22 enforce-flip retro) and to
> [`docs/kyverno-w19-additional-rules.md`](./kyverno-w19-additional-rules.md)
> (the W19 launch + W20 enforce-flip baseline precedent).

W23 launches the **4th batch** of Kyverno additional
rules in the W19-lineage. Both new rules launch in
**Audit** mode with a **5-wave** grace window (longer
than the W19/W21 5-day grace because of the broader
coverage shape — see [§2](#2-why-the-5-wave-audit-window)).

| Rule | File | W23 launch mode | Earliest enforce-flip |
| --- | --- | --- | --- |
| `require-readonly-rootfs` | [`infra/k8s/base/kyverno-policies/require-readonly-rootfs.yaml`](../infra/k8s/base/kyverno-policies/require-readonly-rootfs.yaml) | Audit, Ignore | W28 |
| `require-runas-non-root` | [`infra/k8s/base/kyverno-policies/require-runas-non-root.yaml`](../infra/k8s/base/kyverno-policies/require-runas-non-root.yaml) | Audit, Ignore | W28 |

## 1. What the W23 rules assert

### 1.1 `require-readonly-rootfs`

Every container in a `mahjong-prod` Pod MUST declare
`securityContext.readOnlyRootFilesystem: true`. A writable
container root filesystem is a persistence +
privilege-escalation primitive:

* A compromised process can drop a binary into the
  image's `/usr/local/bin` and re-exec a backdoored
  sidecar on the next pod-restart loop. The W21
  `disallow-host-paths` rule closes the
  `/var/run/docker.sock` escape; this rule closes the
  in-container persistence escape.
* Distroless base images intentionally ship no shell +
  no package manager.
  `readOnlyRootFilesystem: true` makes that intent
  binding — a compromise that drops `wget` + `sh` into
  `/usr/bin` cannot persist past the pod's working-set
  memory.
* CIS Kubernetes Benchmark 5.7.4 + NSA/CISA Kubernetes
  Hardening Guidance §"Pod Security" both list rootfs
  read-only as a baseline POD hardening invariant.

#### Operator escape hatches

Some legitimate workloads need writable `/tmp` or a
writable cache directory. The `volumeMounts` + `emptyDir`
shape is the canonical escape hatch:

```yaml
spec:
  containers:
    - name: app
      securityContext:
        readOnlyRootFilesystem: true
      volumeMounts:
        - name: tmp
          mountPath: /tmp
        - name: cache
          mountPath: /var/cache/app
  volumes:
    - name: tmp
      emptyDir: {}
    - name: cache
      emptyDir: {}
```

`readOnlyRootFilesystem: true` PLUS per-path `emptyDir`
mounts is the "tightest" Pod-Security shape.

### 1.2 `require-runas-non-root`

Every `mahjong-prod` Pod MUST declare
`securityContext.runAsNonRoot: true` EXPLICITLY at the
Pod-spec level. Composition with the W15 rule:

* **W15** `enforce-prod-default.require-non-root` —
  uses Kyverno's `=()` conditional-anchor operator;
  enforces the invariant ONLY WHEN the field is
  PRESENT.
* **W23** `require-runas-non-root` — enforces field
  PRESENCE.

Together: the field MUST be present + `true`. A future
base-image swap to a root-default image (e.g. an Alpine
sidecar) surfaces as an admission denial rather than
silently regressing.

## 2. Why the 5-wave audit window

The W19/W21 audit windows ran on a 5-DAY grace (clean
data → flip). W23 extends to a 5-WAVE grace for two
reasons:

1. **Broader coverage shape.** The W23 rules apply to
   EVERY container in EVERY mahjong-prod Pod, including
   future workloads (sidecars, ephemeral debug pods,
   admin job runners). The W19 `disallow-lateral-
   movement` + W21 `disallow-host-paths` rules apply to
   a much narrower subset (hostNetwork / hostPort /
   hostPath only). A longer audit window gives operators
   more bandwidth to adopt the `emptyDir` escape hatch
   on any legacy workload that hasn't yet declared
   `readOnlyRootFilesystem: true`.
2. **Composition risk surface.** The W23 rules compose
   with W15/W16/W19/W21/W22 — five existing enforce-mode
   rules. Operators rolling forward against the prod
   admission stack need extra audit time to catch
   composition false-positives (e.g. a workload that
   passes W15 individually but fails W23 + W21
   together).

## 3. Audit-window evidence collection (daily)

Snapshot procedure — executed by the on-call SRE daily
starting on W23-merge day. Captures the
`PolicyReportResult` rows surfaced against the W23
rules. Once five waves pass with **zero new
violations**, the W28 enforce-flip is clear.

```bash
# Snapshot PolicyReport rows surfaced against W23 rules
NS=mahjong-prod
for RULE in require-readonly-rootfs require-runas-non-root; do
  echo "==== $RULE ===="
  kubectl get policyreport -n "$NS" \
      -o jsonpath="{range .items[*]}{range .results[?(@.rule==\"$RULE\")]}{.policy}/{.rule}\t{.result}\t{.resources[*].name}\n{end}{end}"
done

# Cluster-scoped: ClusterPolicyReport for non-namespaced resources
for RULE in require-readonly-rootfs require-runas-non-root; do
  echo "==== ClusterPolicyReport / $RULE ===="
  kubectl get clusterpolicyreport \
      -o jsonpath="{range .items[*]}{range .results[?(@.rule==\"$RULE\")]}{.policy}/{.rule}\t{.result}\t{.resources[*].name}\n{end}{end}"
done

# Cross-reference with Hudson's kyverno-deny-events panel
# (Audit-mode runs surface as `policy_violation` events
# rather than admission denies; query the panel's
# `kyverno_policy_results_total{policy=~"require-readonly-
# rootfs|require-runas-non-root",rule_result="fail"}`
# metric for the daily count).
```

Each snapshot lands in
`docs/audits/kyverno-w23-audit-window/`
`day-<NN>-<YYYY-MM-DD>.txt` (created by the SRE; not
auto-collected).

### 3.1 Expected outcomes

| Wave | Snapshot expectation                                                          |
| ---- | ----------------------------------------------------------------------------- |
| W23  | Initial baseline. Two policies launched in Audit; existing prod pods scanned. |
| W24  | Diff vs W23. Any new pods scheduled into prod surface as PolicyReport rows.   |
| W25  | Half-window checkpoint. Any chronic violations should now be remediated.      |
| W26  | Trending-clean checkpoint. If new violations appear, investigate + remediate. |
| W27  | Pre-flip lock-in. The next wave (W28) is the enforce-flip target.             |
| W28  | Enforce-flip cutover. Single `validationFailureAction: Audit → Enforce` edit. |

## 4. Cutover-day evidence (W28 — earliest)

At W28 (or later, if the W23-W27 evidence shows
chronic violations needing more remediation time), the
flip is byte-minimal on the policy rule shape — only
the spec-level toggles + annotation strings change.

```diff
   spec:
-    validationFailureAction: Audit
+    validationFailureAction: Enforce
     background: true
     webhookTimeoutSeconds: 30
-    failurePolicy: Ignore
+    failurePolicy: Fail
```

Mirrors the W20 + W22 enforce-flip commit shape.

## 5. Rollback path

Single `git revert <merge-commit>`. The W19/W20/W21/W22
policies remain in place; admission behaviour reverts
to W22 baseline (W23 rules removed from the cluster).
No data path is affected — Kyverno admission decisions
are stateless.

## 6. W19-lineage cadence summary

| Wave | Action                                              |
| ---- | --------------------------------------------------- |
| W19  | Launch: `disallow-lateral-movement` + `require-network-policy` (Audit). |
| W20  | Enforce-flip: both rules → Enforce + Fail. |
| W21  | Launch: `require-resource-limits` + `disallow-host-paths` (Audit). |
| W22  | Enforce-flip: both rules → Enforce + Fail. |
| W23  | Launch: `require-readonly-rootfs` + `require-runas-non-root` (Audit, 5-wave grace). |
| W28  | (Earliest) Enforce-flip: both W23 rules → Enforce + Fail. |

Each launch pairs the new rules in a SEPARATE
`ClusterPolicy` from the existing W15/W16 enforce-prod-
default policy, preserving the per-rule audit trail.

## 7. Cross-references

* W19 launch + W20 enforce-flip:
  [`docs/kyverno-w19-additional-rules.md`](./kyverno-w19-additional-rules.md).
* W21 launch:
  [`docs/kyverno-w21-additional-rules.md`](./kyverno-w21-additional-rules.md).
* W22 enforce-flip retro:
  [`docs/kyverno-w22-additional-rules.md`](./kyverno-w22-additional-rules.md).
* W15/W16 enforce-prod-default precedent:
  [`docs/kyverno-enforce-rollout.md`](./kyverno-enforce-rollout.md).
* CIS Kubernetes Benchmark 5.7 (Pod Security):
  <https://www.cisecurity.org/benchmark/kubernetes>.
* NSA/CISA Kubernetes Hardening Guidance:
  <https://media.defense.gov/2022/Aug/29/2003066362/-1/-1/0/CTR_KUBERNETES_HARDENING_GUIDANCE_1.2_20220829.PDF>.

## 8. W23 → W24 hand-off

* W24 on-call SRE runs day-2 + day-3 + day-4 + day-5
  snapshots; appends them to
  `docs/audits/kyverno-w23-audit-window/` as plain-text
  files.
* W25 mid-window check — if any new PolicyReport rows
  appear beyond the W23 initial baseline, file a
  `kyverno-w23-violation` issue tagged `apone` and walk
  the operator runbook in §3 against the affected
  workload.
* W26 mid-window check + Hudson panel review.
* W27 pre-flip lock-in — if violations clean, schedule
  the W28 enforce-flip PR; if chronic, extend the
  grace window by one wave + document in the W27
  inbox memo.
* W28 enforce-flip — single-commit `Audit → Enforce`
  flip on both policies (parallel to the W22 commit
  shape on `require-resource-limits.yaml` +
  `disallow-host-paths.yaml`).
