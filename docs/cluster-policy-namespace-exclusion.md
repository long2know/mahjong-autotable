# ClusterPolicy namespace exclusion (kustomize)

> Phase K Wave 13 — Apone (DevOps). Companion to:
> [`infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml`](../infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml)
> (the W13 PatchTransformer file),
> [`infra/k8s/overlays/prod/namespace-transformer.yaml`](../infra/k8s/overlays/prod/namespace-transformer.yaml)
> (the W12 NamespaceTransformer), and
> [`docs/prod-cutover.md` §4](./prod-cutover.md) (the W12
> cross-namespace pattern doc).
>
> Closes the W12 retro D7 open item: "ClusterPolicy namespace
> quirk persists W4 → W12".

## 1. The bug (W4 → W12 history)

The prod overlay's `kyverno-enforce-patch.yaml` ships a Kyverno
`ClusterPolicy` resource. ClusterPolicies are CLUSTER-SCOPED —
they have no `metadata.namespace` by spec. But the kustomize
build of `infra/k8s/overlays/prod/` emitted the policy with
`metadata.namespace: mahjong-prod` from Wave 4 through Wave 12:

```yaml
# Before W13 — `kustomize build infra/k8s/overlays/prod/`
apiVersion: kyverno.io/v1
kind: ClusterPolicy
metadata:
  name: prod-enforce-prod-mahjong-images
  namespace: mahjong-prod          # ← spurious; kubectl ignores it but it's wrong
  ...
```

`kubectl apply` ignores the field on cluster-scoped Kinds (the
API server strips it during admission), so functionally the
policy was applied correctly across W4–W12. The bug shows up in
audit-time tooling:

1. `kubectl diff -k …` produces noisy output — every
   ClusterPolicy diff includes a `-namespace: mahjong-prod`
   delta against the in-cluster state.
2. `kustomize build … | yq` reports a `Namespaced` shape for
   `ClusterPolicy` which trips static checks that assume
   "cluster-scoped Kinds never declare metadata.namespace"
   (Kyverno's own admission webhook flags it as a warning).
3. Operators reading `kustomize build` output have to know to
   mentally strip the namespace from cluster-scoped Kinds —
   tribal-knowledge layer the W13 wave commits to closing.

## 2. Why W12's NamespaceTransformer caused this

W11's `namespace: mahjong-prod` directive in the kustomization
unconditionally stamped the namespace on every emitted resource
— same behaviour, different surface. W12 swapped that directive
for an explicit `NamespaceTransformer` with `unsetOnly: true`
to fix a different bug (preserving the pre-declared
`argo-rollouts` namespace on cross-namespace manifests). The
W12 transformer carried over the same single fieldSpec:

```yaml
fieldSpecs:
  - path: metadata/namespace
    create: true
```

That fieldSpec matches every Kind (no `kind:` filter). It
applies `metadata.namespace=mahjong-prod` to every emitted
resource, including cluster-scoped Kinds. Same bug, new code.

### 2.1 Why "just add a `kind:` filter" doesn't work

The natural fix — add `kind: Deployment` (and friends) to the
fieldSpec so cluster-scoped Kinds are skipped — does NOT work
with kustomize v5.4.3 (the W11 baseline):

```yaml
# This DOES NOT work in v5.4.3 — empirically applies namespace
# to ALL Kinds including ClusterPolicy.
fieldSpecs:
  - kind: Deployment
    path: metadata/namespace
    create: true
```

The W13 reproduction (see `cluster-scoped-fieldspecs.yaml`
header for the minimal repro) confirms the built-in
`NamespaceTransformer` IGNORES the `kind:` gvk filter on
fieldSpec entries — even a single-Kind filter applies to all
Kinds in the resource graph. Kustomize ALSO has a hard-coded
list of "known cluster-scoped" Kinds (ClusterRole, Namespace,
CRD, ...) but that list does NOT include third-party CRDs like
Kyverno `ClusterPolicy`, so the transformer treats them as
namespaced.

The upstream issue thread is at
<https://github.com/kubernetes-sigs/kustomize/issues/5074>
(general "namespace transformer doesn't honour gvk filter on
custom Kinds"). The recommended workaround is the one this
wave ships: a follow-up `PatchTransformer` that REMOVES
`metadata.namespace` on each cluster-scoped Kind.

## 3. The W13 fix — PatchTransformer enumeration

`infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml`
ships a series of `PatchTransformer` documents, one per
cluster-scoped Kind that the overlay (or its transitive
chart/manifest consumers) emits. Each transformer's body is
a JSON6902 `op: remove` against `/metadata/namespace`.

Kinds enumerated in the W13 baseline:

* `ClusterPolicy` (Kyverno) — the W4 / W12 enforce policies.
* `ClusterRole` — upstream RBAC from nginx-ingress, argo-rollouts,
  eso, cert-manager, kyverno.
* `ClusterRoleBinding` — counterpart to `ClusterRole`.
* `CustomResourceDefinition` — every CRD shipped by any helm
  chart wired into this overlay (none today; cheap future-proofing).
* `MutatingWebhookConfiguration` — cert-manager + kyverno
  admission controllers.
* `PersistentVolume` — cluster-scoped storage objects (not
  emitted today; cheap insurance).
* `StorageClass` — cluster-scoped.
* `ValidatingWebhookConfiguration` — counterpart to
  `MutatingWebhookConfiguration`.

To add a new cluster-scoped Kind, append another
`---`-separated `PatchTransformer` document to the file. Keep
the list sorted alphabetically by Kind (lint convention).

## 4. Wire-up — `kustomization.yaml`

The W13 patch updates `kustomization.yaml`:

```yaml
transformers:
  - namespace-transformer.yaml
  - cluster-scoped-fieldspecs.yaml   # ← W13 addition
```

ORDER MATTERS. The `NamespaceTransformer` runs first and stamps
`mahjong-prod` on every Kind including cluster-scoped ones; the
`cluster-scoped-fieldspecs.yaml` PatchTransformers then run and
remove the namespace from each cluster-scoped Kind they target.
If the order is flipped (`cluster-scoped-fieldspecs.yaml` first),
the `remove` op fails with "missing value" because the field
isn't there yet.

## 5. Verification

Before W13:

```bash
$ kustomize build infra/k8s/overlays/prod/ \
    | awk '/^kind:/{kind=$2} /^  namespace:/{print kind,$2}' \
    | sort -u
ClusterPolicy mahjong-prod   ← BUG
ConfigMap mahjong-prod
Deployment mahjong-prod
...
```

After W13:

```bash
$ kustomize build infra/k8s/overlays/prod/ \
    | awk '/^kind:/{kind=$2} /^  namespace:/{print kind,$2}' \
    | sort -u
ConfigMap mahjong-prod
Deployment mahjong-prod
ExternalSecret mahjong-prod
HorizontalPodAutoscaler mahjong-prod
Ingress argo-rollouts          ← preserved (W12 unsetOnly)
Ingress mahjong-prod
Job mahjong-prod
NetworkPolicy argo-rollouts    ← preserved (W12 unsetOnly)
NetworkPolicy mahjong-prod
PersistentVolumeClaim mahjong-prod
Secret mahjong-prod
Service mahjong-prod
# ClusterPolicy absent from this list — the namespace field
# is stripped (the resource is still emitted; just without a
# `namespace:` line in metadata).
```

The fix is non-destructive: `ClusterPolicy` is still emitted
exactly as before EXCEPT the spurious `metadata.namespace:
mahjong-prod` line is gone.

## 6. Cross-namespace invariant — preserved

The W12 cross-namespace pattern is unchanged. Pre-declared
namespaces (`argo-rollouts` on the dashboard ingress + the
NetworkPolicy trio) still survive the transformer pipeline
because:

1. The `NamespaceTransformer.unsetOnly: true` skips resources
   that already have a `namespace:` declared (W12 contract).
2. The follow-up `PatchTransformer`s only target CLUSTER-SCOPED
   Kinds; namespaced resources in other namespaces (Ingress,
   NetworkPolicy in `argo-rollouts`) are NOT in the target list.

The verification block in §5 above shows the W12 cross-namespace
result intact — Ingress + NetworkPolicy split between
`argo-rollouts` and `mahjong-prod` namespaces post-W13.

## 7. Future-proofing — pre-commit Kind drift check

A `scripts/lint-namespaced-kinds.sh` (W14 stretch) is
proposed to scan `kustomize build infra/k8s/overlays/prod/`
output and FAIL if any Kind not in the namespaced-allow-list
emits `metadata.namespace`. The script would replace the
visual inspection in §5 above with an executable gate.
Tracked as a W14+ DevOps backlog item.

## 8. Cross-references

- [`infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml`](../infra/k8s/overlays/prod/cluster-scoped-fieldspecs.yaml)
  — the W13 PatchTransformer enumeration.
- [`infra/k8s/overlays/prod/namespace-transformer.yaml`](../infra/k8s/overlays/prod/namespace-transformer.yaml)
  — the W12 NamespaceTransformer (unchanged in W13).
- [`infra/k8s/overlays/prod/kustomization.yaml`](../infra/k8s/overlays/prod/kustomization.yaml)
  — the W13 wire-up; `transformers:` block order matters.
- [`docs/prod-cutover.md` §4](./prod-cutover.md) — the W12
  cross-namespace pattern doc; the W13 ClusterPolicy exclusion
  is the W12 follow-up.
- W12 retro
  [`docs/retro-2026-10.md` §3.4](./retro-2026-10.md) — the
  open item this wave closes.
- Kustomize upstream issue tracker —
  <https://github.com/kubernetes-sigs/kustomize/issues/5074>.
