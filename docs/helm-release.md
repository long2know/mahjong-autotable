# Helm chart release pipeline

> Phase K Wave 21 — Apone (DevOps).
> Audience: SRE / on-call operator publishing `helm/mahjong`
> chart releases to GHCR as OCI artefacts.
> Companion to [`helm/README.md`](../helm/README.md) (the
> helm chart's per-template reference) and
> [`docs/helm-charts.md`](./helm-charts.md) (the W7/W8
> helm-chart design).

## 1. What landed at W21

A new GitHub Actions workflow + a runbook (this file):

| File | Purpose |
| --- | --- |
| `.github/workflows/helm-release.yml` | Validate + package + push + sign helm chart on `helm-v*.*.*` tag |
| `docs/helm-release.md` (this file) | Operator runbook |

The pipeline:

1. **Trigger** — push of a tag matching `helm-v[0-9]+.[0-9]+.[0-9]+`.
2. **Validate** — `helm lint` + `helm template` against
   default + staging + prod values files.
3. **Package** — `helm package` produces a `.tgz` artefact.
4. **Push** — `helm push` to
   `oci://ghcr.io/long2know/charts/mahjong`.
5. **Sign** — keyless cosign signature via GitHub OIDC.
6. **Verify** — cosign verifies the published signature
   in the same job.

## 2. Why a SEPARATE release pipeline (not extending `release.yml`)

The existing `release.yml` workflow handles the
container-image release (docker build + sign + push to
GHCR for the `v*` tag pattern). Helm-chart releases follow
a DIFFERENT cadence + version namespace:

| Concern | Application image release | Helm chart release |
| --- | --- | --- |
| Trigger | `v0.30.0` tag | `helm-v0.6.0` tag |
| Workflow | `.github/workflows/release.yml` | `.github/workflows/helm-release.yml` |
| Artefact type | OCI container image | OCI manifest (chart tgz) |
| Versioning | App semver | Chart semver (independent) |
| Registry | `ghcr.io/long2know/mahjong-autotable` | `ghcr.io/long2know/charts/mahjong` |
| Signing | W4 cosign keypair | Keyless cosign (GitHub OIDC) |

Conflating the two would force a chart-version bump on
every app release (the W11 Chart.yaml at 0.5.0 might stay
there across application versions 0.20 → 0.30).

## 3. Tagging procedure

The operator releases a chart by tagging the commit that
contains the desired `helm/mahjong/Chart.yaml` `version:`
field:

```bash
# 1. Bump helm/mahjong/Chart.yaml version: field on a
#    PR. Land the PR onto main.
# 2. From main, tag the post-merge commit:
git checkout main
git pull
git tag -s helm-v0.6.0 -m "helm-chart 0.6.0 — W21"
git push origin helm-v0.6.0
```

The workflow validates that the tag's numeric body
matches the Chart.yaml `version:` field (the version is
parsed from the tag and passed to `helm package
--version`; if Chart.yaml says 0.5.0 but the tag is
`helm-v0.6.0`, the operator gets `helm-v0.6.0` on the
output — the tag is the source of truth).

## 4. Consumer-side usage

```bash
# Pull the chart:
helm pull oci://ghcr.io/long2know/charts/mahjong --version 0.6.0

# Install directly:
helm install mahjong oci://ghcr.io/long2know/charts/mahjong \
  --version 0.6.0 \
  --values my-values.yaml

# Upgrade:
helm upgrade mahjong oci://ghcr.io/long2know/charts/mahjong \
  --version 0.6.1
```

## 5. Signature verification (consumer-side)

Operators MUST verify the cosign signature before installing:

```bash
DIGEST=$(crane digest ghcr.io/long2know/charts/mahjong:0.6.0)

cosign verify \
  --certificate-identity-regexp "https://github.com/long2know/mahjong-autotable/.github/workflows/helm-release.yml@refs/tags/helm-v.*" \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com \
  "ghcr.io/long2know/charts/mahjong@${DIGEST}"
```

Expected output:

```
Verification for ghcr.io/long2know/charts/mahjong@sha256:... --
The following checks were performed on each of these signatures:
  - The cosign claims were validated
  - Existence of the claims in the transparency log was verified ...
```

## 6. Operator runbook — landing a chart release

### 6.1 Pre-flight

1. Confirm the chart renders cleanly locally:

   ```bash
   helm lint helm/mahjong
   helm template mahjong helm/mahjong --values helm/mahjong/values.yaml > /dev/null
   helm template mahjong helm/mahjong --values helm/mahjong/values.yaml --values helm/mahjong/values-prod.yaml > /dev/null
   helm template mahjong helm/mahjong --values helm/mahjong/values.yaml --values helm/mahjong/values-staging.yaml > /dev/null
   ```

2. Bump `helm/mahjong/Chart.yaml` `version:` field on a
   PR. Append a CHANGELOG entry on the chart README. Land
   the PR onto main.

### 6.2 Release

```bash
git checkout main && git pull
git tag -s helm-v0.6.0 -m "helm-chart 0.6.0 — W21"
git push origin helm-v0.6.0
```

The workflow auto-runs. Watch:

```bash
gh run watch --workflow helm-release.yml
```

### 6.3 Post-release verification

```bash
# Confirm the chart lands at GHCR:
crane manifest ghcr.io/long2know/charts/mahjong:0.6.0 | jq

# Confirm signature lands:
cosign verify ghcr.io/long2know/charts/mahjong@$(crane digest ghcr.io/long2know/charts/mahjong:0.6.0) \
  --certificate-identity-regexp "https://github.com/long2know/mahjong-autotable/.github/workflows/helm-release.yml@refs/tags/helm-v.*" \
  --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

## 7. Failure modes

| Symptom | Likely cause | Mitigation |
| --- | --- | --- |
| Tag push doesn't trigger the workflow | Tag doesn't match `helm-v*.*.*` pattern | Re-tag with correct pattern; the original tag is left in place but harmless |
| `helm lint` fails | Schema regression in Chart.yaml or template | Fix the chart locally; revert the version bump if necessary |
| `helm template` fails on prod values | Missing required value | Add the value to `helm/mahjong/values-prod.yaml` |
| `helm push` returns 403 | GHCR permissions misconfigured | Verify `packages: write` permission in workflow + org-level package settings |
| `cosign sign` fails with OIDC error | `id-token: write` permission missing | Confirm workflow `permissions:` block includes `id-token: write` |

## 8. Cross-references

* [`.github/workflows/helm-release.yml`](../.github/workflows/helm-release.yml)
  — the workflow documented here.
* [`.github/workflows/release.yml`](../.github/workflows/release.yml)
  — application image release (parallel structure).
* [`helm/mahjong/Chart.yaml`](../helm/mahjong/Chart.yaml)
  — chart manifest.
* [`docs/helm-charts.md`](./helm-charts.md)
  — W7/W8 helm chart design notes.
* [`docs/signer-keypair.md`](./signer-keypair.md)
  — W4 application-image cosign keypair (NOT used for
  helm-chart signing — that uses keyless OIDC).

## 9. W21 → W22 hand-off

W22 candidate work:

* **Multi-chart support** — wire a matrix strategy if/when
  helm/ contains more than the single `mahjong` chart.
* **Pre-release `-rc.N` channel** — accept
  `helm-v0.6.0-rc.1` tags + push to a separate
  `oci://ghcr.io/long2know/charts/mahjong-rc` registry.
  Deferred until needed.
* **Release-notes automation** — auto-generate chart
  release notes from CHANGELOG.md based on tag commit
  range. Deferred until the chart has a dedicated
  CHANGELOG.
