# Image signing — cosign keyless OIDC

> Phase K Wave 1 — Apone (DevOps).

Every multi-arch image published by [`docker-build.yml`](../.github/workflows/docker-build.yml)
to `ghcr.io/long2know/mahjong-autotable` is signed by
[`sign-image.yml`](../.github/workflows/sign-image.yml) via **cosign
keyless OIDC**. This document covers the why, the how, and the operator
/ auditor verification procedure.

## Why keyless

A traditional cosign signing scheme requires a long-lived private key
(`cosign.key` + `cosign.pub`). That key is itself a credential — it
must be rotated, audited, stored in a secret manager, and shielded
from CI logs. Lose it and your supply chain is compromised.

**Keyless** trades the private key for **short-lived certificates**
issued by [Sigstore Fulcio](https://docs.sigstore.dev/fulcio/overview/)
against an **OIDC token**. In our case the OIDC token comes from the
GitHub Actions workflow itself (`id-token: write` lets the runner mint
a JWT signed by `https://token.actions.githubusercontent.com`). Fulcio
verifies the JWT, issues a 10-minute X.509 signing cert whose subject
encodes the canonical signer identity — for us:

```
https://github.com/long2know/mahjong-autotable/.github/workflows/sign-image.yml@refs/heads/main
```

The signature + cert + transparency-log entry are written to the same
OCI registry as the image (as a sibling artefact, `<digest>.sig`) and
also publicly recorded in the [Rekor](https://docs.sigstore.dev/rekor/overview/)
transparency log. Anyone can verify the signature without us shipping
a public key — they only need to know which OIDC issuer + identity to
trust.

This is the same model GitHub itself, Kubernetes, Distroless, npm
provenance, and dozens of other major OSS projects use.

## What is signed

We sign the **multi-arch manifest list digest** (the digest that
`ghcr.io/long2know/mahjong-autotable:latest` resolves to). One
signature → both `linux/amd64` and `linux/arm64` per-arch images
inherit the attestation, because the manifest list is the canonical
"the image we shipped" identity.

When a consumer pulls `:latest`, Docker resolves the manifest list,
picks the matching architecture, and the signature on the manifest
list digest cryptographically attests to both per-arch image digests
inside it.

## Pipeline

```
push main
   │
   ▼
docker-build.yml ─────────────────────► ghcr.io/long2know/mahjong-autotable
   │  (build multi-arch manifest list)         │
   │  (manifest digest: sha256:...)            │ image present, not yet signed
   ▼                                            ▼
workflow_run completed=success         pull works; verify FAILS (no sig yet)
   │
   ▼
sign-image.yml
   ├─ docker buildx imagetools inspect → resolves manifest digest
   ├─ cosign sign --yes (keyless OIDC)
   │      ├─ OIDC token (id-token: write) → Fulcio
   │      └─ Fulcio cert → sign manifest digest
   │             └─ signature written to ghcr.io as <digest>.sig
   │             └─ entry written to Rekor (transparency log)
   ▼
cosign verify (sanity check, same workflow)
   ├─ certificate-identity-regexp = ^…/sign-image.yml@refs/(heads/main|tags/v.*)$
   ├─ certificate-oidc-issuer    = https://token.actions.githubusercontent.com
   ▼
✅ signed + verified
```

## Required permissions

The signing job runs with the minimum permissions cosign needs:

```yaml
permissions:
  contents: read
  packages: write    # write the .sig artefact to ghcr.io
  id-token: write    # mint the OIDC token Fulcio swaps for a signing cert
```

`id-token: write` is the only "elevated" permission and is confined
to this workflow. It is **not** granted to `docker-build.yml` — that
workflow only needs `packages: write` to push the image. Confining
`id-token: write` to the signing workflow limits the blast radius of
a compromised job.

## How to verify a published image — operator runbook

Anyone with `cosign` installed (`brew install cosign` /
`apt install cosign` / `go install github.com/sigstore/cosign/v2/cmd/cosign@latest`)
can verify any published Mahjong-Autotable image:

```bash
COSIGN_EXPERIMENTAL=0 cosign verify \
  --certificate-identity-regexp '^https://github\.com/long2know/mahjong-autotable/\.github/workflows/sign-image\.yml@refs/(heads/main|tags/v.*)$' \
  --certificate-oidc-issuer 'https://token.actions.githubusercontent.com' \
  ghcr.io/long2know/mahjong-autotable:latest
```

A green run prints a JSON envelope with:

- `critical.identity.docker-reference` — the image we signed.
- `critical.image.docker-manifest-digest` — the digest we signed.
- `optional.Bundle.SignedEntryTimestamp` — Rekor entry timestamp.
- `optional.Issuer` — `https://token.actions.githubusercontent.com`.
- `optional.Subject` — the signing-workflow URL (matches the regex).

A **non-zero** exit code or a "no matching signatures" message means
the image was either:

1. Built outside the signing workflow (don't run it).
2. Pushed without going through `sign-image.yml` (e.g. the signing
   job failed). Re-run `sign-image.yml` from the GitHub UI.
3. Tampered with after publication (highly unlikely on GHCR but
   would be detected by Rekor mismatch).

### Verify by digest (pinned, recommended for production deploys)

Production deployments **must** pin by digest, not by `:latest` /
`:sha-<sha>` tags. Tags are mutable; digests are not.

```bash
DIGEST=$(docker buildx imagetools inspect \
  ghcr.io/long2know/mahjong-autotable:latest \
  --format '{{.Manifest.Digest}}')

cosign verify \
  --certificate-identity-regexp '^https://github\.com/long2know/mahjong-autotable/\.github/workflows/sign-image\.yml@refs/(heads/main|tags/v.*)$' \
  --certificate-oidc-issuer 'https://token.actions.githubusercontent.com' \
  "ghcr.io/long2know/mahjong-autotable@$DIGEST"
```

Use the same `@$DIGEST` form in your Kubernetes `image:` field or
`docker run` argument so the runtime is guaranteed byte-identical to
the verified payload.

### Verify by tag (CI / dev workflows)

When pinning by tag is required (e.g. a smoke test running against
`:latest`), cosign verifies the manifest the tag currently resolves
to. If the tag is re-pointed mid-deployment, the next verify call
sees the new manifest:

```bash
cosign verify \
  --certificate-identity-regexp '^https://github\.com/long2know/mahjong-autotable/\.github/workflows/sign-image\.yml@refs/(heads/main|tags/v.*)$' \
  --certificate-oidc-issuer 'https://token.actions.githubusercontent.com' \
  ghcr.io/long2know/mahjong-autotable:v0.10.0
```

Note the regex also matches tag-builds (`refs/tags/v*`) since
`docker-build.yml` triggers on `v*.*.*` tag pushes and the signing
workflow follows.

## Auditor checklist

For supply-chain audits the published evidence is:

| Question | Where to look |
|---|---|
| Was this image built by the project? | `cosign verify --certificate-identity-regexp ...` — proves the OIDC issuer + workflow path. |
| Has the signature been tampered with? | Rekor transparency log — search the digest at https://search.sigstore.dev. |
| When was it signed? | Rekor entry `integratedTime`. |
| Are both per-arch images covered? | Manifest list digest signed → both `linux/amd64` and `linux/arm64` images in the list inherit the signature. |
| What dependencies are in the image? | SBOM workflow — CycloneDX + SPDX (see [`sbom.md`](sbom.md)). |
| Were any CRITICAL/HIGH CVEs known at sign time? | Trivy gate in SBOM workflow — see same doc. |

## Failure modes

| Symptom | Cause | Recovery |
|---|---|---|
| `cosign verify` returns "no matching signatures" | `sign-image.yml` hasn't run yet (race vs build), or it failed transiently | Re-run `sign-image.yml` from Actions UI. |
| Verify fails with identity mismatch | Workflow path was renamed; old images carry old subject | Update verification regex; keep old regex around as long as old digests are still deployed. |
| Verify fails with issuer mismatch | OIDC issuer hostname changed | Update `--certificate-oidc-issuer`. Track GitHub's [OIDC change log](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect). |
| Fulcio outage during sign | Fulcio transient unavailability | Re-run `sign-image.yml`. Image is still pullable in the meantime, but unsigned — don't promote to prod until verified. |

## Production gate

The production deploy runbook ([`production-deployment-runbook.md`](production-deployment-runbook.md))
**requires** a successful `cosign verify` against the pinned digest
**before** the rolling-update step. The Kubernetes admission policy
(future ESO / Kyverno / Cosign policy-controller work — tracked as a
Phase K follow-up) will enforce this at the cluster layer; for now
it's an operator-checklist gate.

## Related docs

- [`sbom.md`](sbom.md) — supply-chain SBOM + vulnerability scan.
- [`docker.md`](docker.md) — image build + multi-arch contract.
- [`production-deployment-runbook.md`](production-deployment-runbook.md) — end-to-end prod runbook.
- [`secret-management.md`](secret-management.md) — what is and isn't a secret.

## References

- [Sigstore Fulcio](https://docs.sigstore.dev/fulcio/overview/)
- [Sigstore Rekor](https://docs.sigstore.dev/rekor/overview/)
- [GitHub OIDC for cosign keyless](https://docs.github.com/en/actions/deployment/security-hardening-your-deployments/about-security-hardening-with-openid-connect)
- [`sigstore/cosign-installer`](https://github.com/sigstore/cosign-installer)
