# GHCR → ECR mirror — signature + SLSA preserving

> Phase K Wave 7 — Apone (DevOps).

This document explains **why**, **when**, and **how** the
`mirror-ghcr-to-ecr.yml` workflow runs. It also documents the
fallback path when ECR is unreachable + the verification commands
for confirming the mirror is byte-exact.

## 1. Why mirror at all

`mahjong-autotable` publishes the canonical image to **GHCR**:
- Signed in `.github/workflows/sign-image.yml` (W1 — cosign keyless).
- SLSA-attested in `.github/workflows/slsa-provenance.yml` (W4+W5).
- Scanned in `.github/workflows/container-scan.yml` (W6 tuned).

EKS clusters pull from **ECR** by preference:
- **Lower latency** — ECR pulls from in-region across the AWS
  backbone; GHCR pulls cross-cloud.
- **IAM-native** — no `imagePullSecret` plumbing (the EKS node IAM
  role grants pull permission directly).
- **Replication** — `infra/terraform/modules/dr-replication/` mirrors
  the primary ECR repo into us-west-2 for DR (W6).

The mirror collapses **canonical-registry-vs-pull-source**: one
binary lives in two places, with **bit-identical signatures and
attestations** in both.

## 2. Why naive mirroring breaks signatures

```bash
# DO NOT do this — it BREAKS cosign + SLSA.
docker pull ghcr.io/long2know/mahjong-autotable:v0.16.0
docker tag  ghcr.io/long2know/mahjong-autotable:v0.16.0 \
            <account>.dkr.ecr.us-east-1.amazonaws.com/mahjong-autotable:v0.16.0
docker push <account>.dkr.ecr.us-east-1.amazonaws.com/mahjong-autotable:v0.16.0
```

Why this fails:

1. **`docker pull` decompresses layers into the local
   content-addressable store.** Layer digests on disk match the
   compressed source digests, but when `docker push` re-uploads
   them, dockerd re-encodes the gzip stream — producing
   **DIFFERENT layer digests**.
2. **Different layer digests = different manifest list digest.**
   The manifest is a JSON document referencing the per-arch
   manifests by digest; one layer digest change ripples up.
3. **Different manifest digest = the cosign signature `<digest>.sig`
   sidecar no longer resolves.** Verifiers look for
   `<digest>.sig` at the destination registry; that artefact
   never gets pushed because `docker push` doesn't know about
   sidecars.
4. **SLSA attestation breaks the same way.** The `.att` sidecar
   is also a content-addressable artefact keyed by digest.

The result is an unsigned, un-attested image carrying the same tag
— a regression that the W4 Kyverno admission policy would
**reject** in `mahjong-prod`. The cluster would fail-closed.

## 3. The signature-preserving primitives

| Tool | What it preserves |
|---|---|
| `crane copy` (from [`go-containerregistry`](https://github.com/google/go-containerregistry)) | Manifest list + per-arch images. **No gzip re-encoding** — registry-to-registry HTTP-only copy. Destination digest matches source digest. |
| `cosign copy` | `.sig` sidecar (cosign signature) + `.att` sidecar (in-toto attestations, incl. SLSA provenance). Same OCI artefact-discovery path on the destination. |

Combined, they produce a **bit-exact mirror**: same digest, same
signature, same SLSA predicate. Both `cosign verify` and
`slsa-verifier verify-image` succeed against either registry
without changing the signer-identity regex.

## 4. The workflow shape

[`./.github/workflows/mirror-ghcr-to-ecr.yml`](../.github/workflows/mirror-ghcr-to-ecr.yml)

| Trigger | Effect |
|---|---|
| `push` on `v*.*.*` tag | Mirror the just-tagged image. |
| `workflow_dispatch` (operator) | Re-mirror for a specific tag + (optional) operator-supplied digest. Used for transient-failure re-runs. |

Six steps:

1. **Resolve tag + digest** — defensive regex on the tag shape;
   pulls operator-supplied digest from `workflow_dispatch` input.
2. **Configure AWS credentials (OIDC)** — assumes the W6
   least-privilege role provisioned by
   `infra/terraform/modules/github-oidc/`.
3. **Log in to ECR + GHCR.**
4. **`crane copy`** — content-addressable manifest copy.
5. **Digest equality check** — fail-loud if `crane copy` somehow
   produced a different destination digest.
6. **`cosign copy`** — sidecars (`.sig` + `.att`) across.
7. **`cosign verify` + `cosign verify-attestation`** on the ECR
   ref — proves both signatures verify at the destination with
   the canonical signer-identity regex.

The verify steps use the SAME six-file invariant regex documented
in `docs/signer-identity-invariant.md`. If the regex changes
without a coordinated update here, the mirror's verify step fails
and the workflow exits non-zero — surfacing the drift loudly.

## 5. Required secrets + IAM

| Secret | Source | Why |
|---|---|---|
| `AWS_ECR_MIRROR_ROLE_ARN` | Terraform output (W6 `modules/github-oidc/`) | The role the workflow assumes via OIDC. |
| `AWS_ECR_REGION` | Operator | e.g. `us-east-1`. |
| `AWS_ECR_REPOSITORY` | Operator | The ECR repository name (e.g. `mahjong-autotable`). |

The role's least-privilege IAM grant (W6):

```hcl
# in infra/terraform/modules/github-oidc/least-privilege.tf
ecr_push_actions = [
  "ecr:BatchCheckLayerAvailability",
  "ecr:BatchGetImage",
  "ecr:CompleteLayerUpload",
  "ecr:InitiateLayerUpload",
  "ecr:PutImage",
  "ecr:UploadLayerPart",
  "ecr:GetAuthorizationToken",      # MUST be on "*" (AWS constraint)
  "ecr:DescribeRepositories",       # idempotency
]
```

These are the same actions the W6 narrowing gave the role; the
mirror workflow does NOT require additional grants.

## 6. Fallback when ECR is unreachable

The mirror is **best-effort**. The workflow MUST NOT block the
release; a `mirror-ghcr-to-ecr` failure is treated as an
ECR-side outage signal, not a release-blocker.

Fallback procedure:

1. **EKS pods fall back to GHCR.** The Deployment template
   pins `image: ghcr.io/long2know/mahjong-autotable:<tag>` as
   the canonical reference. ECR is consumed via a
   per-namespace `imagePullSecret` only when configured by
   the operator — when ECR is unreachable, the GHCR reference
   resolves (longer latency, but the pod starts).
2. **Re-run the mirror via `workflow_dispatch`** once ECR
   recovers:
   ```bash
   gh workflow run mirror-ghcr-to-ecr.yml \
       -f tag=v0.16.0
   ```
3. **Confirm the mirror is bit-identical:**
   ```bash
   crane digest ghcr.io/long2know/mahjong-autotable:v0.16.0
   crane digest <account>.dkr.ecr.us-east-1.amazonaws.com/mahjong-autotable:v0.16.0
   # Both MUST print the same sha256:<64-hex>.
   ```

## 7. Verification on either side

Identical commands work against either registry — the
`--certificate-identity-regexp` and `--source-uri` pin the
canonical signer, regardless of which registry serves the bits.

```bash
# Signature verification (any registry).
cosign verify \
    --certificate-identity-regexp \
        '^https://github\.com/long2know/mahjong-autotable/\.github/workflows/sign-image\.yml@refs/(heads/main|tags/v.*)$' \
    --certificate-oidc-issuer 'https://token.actions.githubusercontent.com' \
    <REGISTRY>/long2know/mahjong-autotable@<digest>

# SLSA verification (any registry).
slsa-verifier verify-image \
    <REGISTRY>/long2know/mahjong-autotable@<digest> \
    --source-uri github.com/long2know/mahjong-autotable
```

## 8. When NOT to mirror

| Scenario | Should you mirror? |
|---|---|
| `v*.*.*` tag push | **Yes** — automatic. |
| `main`-branch push (non-tag) | **No** — `main` images get a `sha-<sha>` tag in GHCR but are not pinned in any deployment. Mirroring would pollute ECR with hundreds of dev tags. |
| Hot-fix branch | **No** — until the hot-fix lands on `main` + is tagged. |
| RC images (`v0.16.0-rc1`) | **No** by default — the trigger regex is `v[0-9]+\.[0-9]+\.[0-9]+`. RCs land via `workflow_dispatch` if the operator wants. |

## 9. DR replication interplay (W6)

`infra/terraform/modules/dr-replication/` configures
**account-level ECR replication** from us-east-1 → us-west-2
on EVERY push to the primary ECR repo. The W7 mirror lands the
image in us-east-1 ECR; the W6 DR replication carries it to
us-west-2 ECR asynchronously (typical 1–5 min lag). The
two-stage flow is:

```
GHCR (canonical, signed)
   │
   │  W7 mirror-ghcr-to-ecr.yml (crane copy + cosign copy)
   ▼
us-east-1 ECR (signed + attested, same digest)
   │
   │  W6 account-level replication (PREFIX_MATCH)
   ▼
us-west-2 ECR (signed + attested, same digest)
```

A DR failover (W6 procedure) sees the same digest in us-west-2
ECR as in us-east-1; signature verification stays green.

## 10. Cross-references

* [`.github/workflows/mirror-ghcr-to-ecr.yml`](../.github/workflows/mirror-ghcr-to-ecr.yml) — the workflow.
* [`.github/workflows/sign-image.yml`](../.github/workflows/sign-image.yml) — the canonical signer.
* [`.github/workflows/slsa-provenance.yml`](../.github/workflows/slsa-provenance.yml) — the SLSA attestor.
* [`docs/image-signing.md`](image-signing.md) — cosign verification reference.
* [`docs/slsa-provenance.md`](slsa-provenance.md) — SLSA verification reference.
* [`docs/signer-identity-invariant.md`](signer-identity-invariant.md) — the six-file regex lock-step that this mirror's verify step depends on.
* [`infra/terraform/modules/dr-replication/`](../infra/terraform/modules/dr-replication/) — ECR replication topology (W6).
* [`infra/terraform/modules/github-oidc/`](../infra/terraform/modules/github-oidc/) — least-privilege OIDC role (W6).
