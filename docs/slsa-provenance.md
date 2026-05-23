# SLSA Level 3 in-toto provenance

> Phase K Wave 4 — Apone (DevOps).

This runbook covers the **SLSA L3** in-toto provenance predicate
that [`.github/workflows/slsa-provenance.yml`](../.github/workflows/slsa-provenance.yml)
generates for every image published by [`docker-build.yml`](../.github/workflows/docker-build.yml).

## 1. What SLSA L3 buys us

A [cosign keyless signature](image-signing.md) (Wave 1) proves
"these bits were signed by *this* workflow identity". A SLSA L3
**provenance predicate** is the next ring outward — a signed,
Sigstore-anchored, in-toto-shaped statement that binds:

* the **subject** (image manifest digest),
* the **builder** (the GitHub-Actions reusable workflow that ran,
  pinned by repo + ref + run id + run attempt),
* the **materials** (source repo, commit SHA, ref, workflow file
  path),
* and the **build environment** (runner OS + builder version)

into one **non-falsifiable** statement. SLSA Level 3 specifically
requires that the **builder is isolated from the build** — the
reusable workflow runs in a separate runner pool so the build
cannot tamper with the provenance generator. That's the
distinction between L2 (the build self-asserts) and L3 (a hardened
generator asserts).

End-to-end, the supply-chain chain is now FOUR layers:

| Layer | Workflow / Policy | Guarantee |
|-------|-------------------|-----------|
| W1 | `sign-image.yml` | Image bits were signed by this repo's workflow. |
| W2 | `verify-signature.yml` (reusable) | Image was signed before release-cut. |
| W3 | `release.yml` `verify-sbom` | SPDX SBOM exists for the released digest, signed by this repo. |
| W3 | `kyverno-cosign-verify.yaml` | Admission denies unsigned images at cluster boundary. |
| **W4** | **`slsa-provenance.yml`** | **Non-falsifiable link from source commit → artefact, attested by an isolated builder.** |

## 2. Where the provenance lives

The generated provenance bundle is published in three places:

1. **OCI registry sidecar artefact.** The
   `generator_container_slsa3.yml` reusable workflow attaches the
   provenance to the registry as a sibling artefact tagged
   `<digest>.att`. `cosign download attestation` retrieves it.
2. **Public Rekor transparency log.** Every Sigstore-signed
   artefact is recorded at <https://rekor.sigstore.dev>; the
   provenance log entry is permanent + globally searchable by
   repo / commit.
3. **GitHub Release asset (tag pushes only).** On `v*.*.*` tag
   pushes, the workflow's `attach-to-release` job uploads the
   bundle to the matching GitHub Release as
   `provenance.intoto.jsonl`. Auditors who don't want to install
   the OCI tooling can `curl` it directly off the Release page.

## 3. Provenance file shape

The `provenance.intoto.jsonl` file is a single-line
[in-toto statement](https://github.com/in-toto/attestation/blob/main/spec/v1/statement.md)
wrapped in a [DSSE envelope](https://github.com/secure-systems-lab/dsse).
Decoded, it looks like:

```json
{
  "_type": "https://in-toto.io/Statement/v1",
  "predicateType": "https://slsa.dev/provenance/v1",
  "subject": [
    {
      "name": "ghcr.io/long2know/mahjong-autotable",
      "digest": { "sha256": "abc123…" }
    }
  ],
  "predicate": {
    "buildDefinition": {
      "buildType": "https://github.com/slsa-framework/slsa-github-generator/container@v1",
      "externalParameters": {
        "workflow": {
          "ref": "refs/tags/v0.13.0",
          "repository": "https://github.com/long2know/mahjong-autotable",
          "path": ".github/workflows/slsa-provenance.yml"
        }
      },
      "internalParameters": {
        "GITHUB_RUN_ID": "1234567890",
        "GITHUB_SHA": "974a7a9…"
      }
    },
    "runDetails": {
      "builder": {
        "id": "https://github.com/slsa-framework/slsa-github-generator/.github/workflows/generator_container_slsa3.yml@refs/tags/v2.0.0"
      },
      "metadata": {
        "invocationId": "https://github.com/long2know/mahjong-autotable/actions/runs/1234567890/attempts/1"
      }
    }
  }
}
```

The DSSE envelope wraps that statement with the Fulcio-issued
signing certificate + signature; together they prove that the
specific reusable-workflow run produced the predicate.

## 4. Verification with `slsa-verifier`

Install [`slsa-verifier`](https://github.com/slsa-framework/slsa-verifier)
locally:

```bash
go install github.com/slsa-framework/slsa-verifier/v2/cli/slsa-verifier@v2.7.0
```

Verify against a published image + Release-attached provenance file:

```bash
# Resolve the digest of the tag you want to verify.
DIGEST=$(docker buildx imagetools inspect --raw \
    ghcr.io/long2know/mahjong-autotable:v0.13.0 | sha256sum | awk '{print "sha256:" $1}')

# Download the provenance from the Release page.
gh release download v0.13.0 \
    --repo long2know/mahjong-autotable \
    --pattern 'provenance.intoto.jsonl'

# Run the verifier.
slsa-verifier verify-image \
    "ghcr.io/long2know/mahjong-autotable@${DIGEST}" \
    --provenance-path provenance.intoto.jsonl \
    --source-uri github.com/long2know/mahjong-autotable \
    --source-tag v0.13.0
```

Expected output (truncated):

```
Verified signature against tlog entry index <N> at URL: https://rekor.sigstore.dev/...
Verifying artifact ghcr.io/long2know/mahjong-autotable@sha256:abc123…: PASSED
PASSED: SLSA verification passed
```

To verify against the OCI sidecar (no Release file needed):

```bash
slsa-verifier verify-image \
    "ghcr.io/long2know/mahjong-autotable@${DIGEST}" \
    --source-uri github.com/long2know/mahjong-autotable \
    --source-tag v0.13.0
```

The verifier pulls the attestation from the registry automatically.

## 4a. Signer-identity invariant (Phase K Wave 7 — Apone)

`slsa-verifier verify-image` pins the **source URI** of the build
(`github.com/long2know/mahjong-autotable`), but it does NOT pin the
**signer-identity regex**. The signer-identity check happens via
the cosign sidecar that this workflow also produces — and the
canonical regex is locked across SIX repo surfaces by
[`scripts/check_signer_identity.py`](../scripts/check_signer_identity.py).
The hook fails the commit if ANY of the six files drifts.

The canonical regex (single-escaped form) is:

```
^https://github\.com/long2know/mahjong-autotable/\.github/workflows/sign-image\.yml@refs/(heads/main|tags/v.*)$
```

The six surfaces in lock-step:

| # | File | Where the regex lives |
|---|---|---|
| 1 | `.github/workflows/sign-image.yml` | `EXPECTED_IDENTITY_REGEXP` env var |
| 2 | `.github/workflows/verify-signature.yml` | `expected-identity-pattern` default input |
| 3 | `.github/workflows/slsa-provenance.yml` | `EXPECTED_IDENTITY_REGEXP` env var (W7 marker) |
| 4 | `infra/k8s/policies/kyverno-cosign-verify.yaml` | `subjectRegExp` (keyless attestor) |
| 5 | `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml` | `subjectRegExp` (prod enforce patch) |
| 6 | `docs/slsa-provenance.md` (this section) | §4a literal |

Rotation procedure: see
[`docs/signer-identity-invariant.md`](signer-identity-invariant.md).

## 5. What a verify failure means

A non-zero exit from `slsa-verifier` means ONE of:

* The image digest you supplied does NOT match the subject of any
  attestation in Rekor → **the image you have is NOT the one we
  built and signed**. Stop and investigate; the image may be a
  registry impersonation.
* The signing cert was issued to a DIFFERENT workflow path
  (`--source-uri` mismatch) → **someone else signed an image
  carrying our name**. Refuse to deploy.
* The Rekor entry was not found → **the provenance was never
  written to the transparency log**. This is either a
  Sigstore-side outage (rare) or the image predates the
  Wave-4 workflow.
* The DSSE signature did not verify → **the provenance file was
  tampered with after generation**. Refuse to deploy.

A pass means: the bits you hold, the source commit you can read
on GitHub, and the workflow run logs on Actions are
cryptographically tied together. That's the floor SLSA L3 is
designed to give you.

## 6. Bumping the SLSA generator version + migration history

The pin in `slsa-provenance.yml` is a fully-qualified `vX.Y.Z`
ref:

```yaml
uses: slsa-framework/slsa-github-generator/.github/workflows/generator_generic_slsa3.yml@v2.0.0
```

The generator project [requires](https://github.com/slsa-framework/slsa-github-generator#referencing-the-generators)
this exact pin shape (NOT a `vX.Y` or `vX` shorthand, NOT a sha)
so the runner can detect which audited release of itself is being
invoked.

Bump procedure:

1. Check the generator's
   [releases](https://github.com/slsa-framework/slsa-github-generator/releases)
   page and the
   [changelog](https://github.com/slsa-framework/slsa-github-generator/blob/main/CHANGELOG.md)
   for the new version.
2. Edit `slsa-provenance.yml`: bump the `@v` ref **AND** update
   the example in §1 of this file.
3. Open a PR; observe a green `slsa-provenance` run on the merge
   commit; run §4 against the resulting image to confirm
   `slsa-verifier` still passes end-to-end.
4. If §4 fails, revert immediately — the previous pin is the
   trust anchor for every artefact ever published.

### 6.1 Wave-4 → Wave-5 generator migration (single-subject → multi-subject)

Wave-4 used `generator_container_slsa3.yml@v2.0.0` — a
container-specific generator that emits a SINGLE-SUBJECT predicate
(the image manifest digest only) and auto-attaches it to the OCI
registry as a sidecar artefact.

Wave-5 switched to `generator_generic_slsa3.yml@v2.0.0` — the
GENERIC SLSA generator that accepts a base64-encoded
`sha256sum`-format subjects list. We pass TWO subjects:

1. The image manifest list digest, named
   `ghcr.io/long2know/mahjong-autotable@sha256:<digest>`.
2. The CycloneDX SBOM file (`sbom.cyclonedx.json`), named after
   the filename so an auditor with the file in hand can verify
   the hash with `sha256sum`.

The output is a single `provenance-and-sbom.intoto.jsonl` whose
in-toto statement carries both subjects under one DSSE envelope,
one Sigstore signature, one Rekor entry. The Wave-5
`attest-oci` job additionally publishes the predicate to the
image as an OCI sidecar via `cosign attest --type slsaprovenance1`
so the Wave-5 Kyverno `attestations:` block (see
[`docs/admission-policy.md` §6](admission-policy.md)) discovers
it through the standard `cosign download attestation` path.

**Backward compatibility.** Wave-4 attestations remain in Rekor
forever (the public transparency log is append-only) and remain
verifiable with the Wave-4 invocation:

```bash
# Wave-4 (single-subject, container generator):
slsa-verifier verify-image \
    "ghcr.io/long2know/mahjong-autotable@${DIGEST}" \
    --source-uri github.com/long2know/mahjong-autotable \
    --source-tag v0.13.0
```

Wave-5 (and later) attestations verify with:

```bash
# Wave-5+ (multi-subject, generic generator):
slsa-verifier verify-artifact \
    --provenance-path provenance-and-sbom.intoto.jsonl \
    --source-uri github.com/long2know/mahjong-autotable \
    --source-tag v0.14.0 \
    sbom.cyclonedx.json
```

`verify-artifact` accepts the artefact path as its trailing
positional and checks it against ALL subjects in the predicate.
Run it against `sbom.cyclonedx.json` to verify the SBOM
attestation, and against the image (`verify-image`) to verify
the image attestation — both pass against the same single
predicate.

**Why the migration?** Wave-4 emitted two parallel attestation
flows (`slsa-provenance.yml` for the image; `sbom.yml` for the
SBOM, unsigned). An auditor reading the SLSA predicate could not
cryptographically confirm the SBOM in their hand came from the
same build run. Wave-5 closes that audit gap: ONE statement,
TWO subjects, ONE verifiable claim — "this build produced
EXACTLY these artefacts, here are the materials + builder that
produced them both".

## 7. Why a fresh workflow vs extending `sign-image.yml`

The same rationale that justifies `sign-image.yml` being a
separate file from `docker-build.yml` (failure-isolation,
permission-scoping) applies one more time:

* The SLSA generator runs on a SEPARATE runner pool — the
  reusable workflow is the L3 isolation boundary.
* `sign-image.yml` is `id-token: write` for cosign; the SLSA
  generator needs additional `actions: read` so it can introspect
  this workflow's environment. Splitting them keeps each
  workflow's permission grant minimal.
* Failure in provenance generation (Sigstore outage, generator
  bug) MUST NOT block image publication; keeping the two
  workflows decoupled means an attestation outage delays the
  attestation, not the image. The image is still pullable +
  cosign-signed; the SLSA predicate arrives on the next
  workflow re-run.

## 7a. `slsa-verifier` in the admission webhook + on PR (Phase K Wave 6 — Apone)

Wave-5 introduced the Kyverno `attestations:` block that verifies
the SLSA predicate at admission time (in-cluster). Wave 6 adds
TWO additional verification layers:

### 7a.1 `slsa-verifier` v2 binary inside the admission webhook container

Beyond Kyverno's cosign-via-policy verification, the admission
webhook container bundles the `slsa-verifier` v2.6.0 binary
(installed at `/usr/local/bin/slsa-verifier` in the webhook
Dockerfile). The binary is invoked as a SECOND-pass check on
every admit event:

```bash
slsa-verifier verify-image \
    <registry>/mahjong-autotable@<digest> \
    --source-uri github.com/long2know/mahjong-autotable
```

Why two passes:

* Kyverno's cosign integration evaluates the policy at admission
  but does NOT enforce the `slsa-verifier` semantic checks the
  upstream CLI implements (subject normalisation, multi-subject
  predicate handling, builder ID regex matching at the verifier
  layer). The W6 CLI pass is defence in depth.
* When Kyverno's cosign upstream changes its verification
  semantics (e.g. a 2.x → 3.x cosign-policy major bump), the
  `slsa-verifier` binary remains a stable, separately-versioned
  signal. A regression in either Kyverno or the CLI is caught
  by the OTHER layer.

### 7a.2 `verify-slsa-on-deploy.yml` — pre-merge gate

`.github/workflows/verify-slsa-on-deploy.yml` runs `slsa-verifier
verify-image` on every PR labelled `deploy:prod`. The PR cannot
merge until the verifier passes against the image the prod
overlay would deploy. Mechanics:

* Trigger: `pull_request` opened/synchronized/labeled, gated on
  the `deploy:prod` label.
* Image lookup: pulled from `infra/k8s/overlays/prod/kustomization.yaml`'s
  `images:` block (preferring the `digest:` field).
* Failure: PR cannot merge (the workflow becomes a required
  status check; configure in repo settings).
* Sticky PR comment: pass/fail visible to reviewers without
  drilling into the Actions tab.

This gate is the CI-side mirror of the admission-time check:
the SAME binary verifies the SAME predicate against the SAME
source URI in BOTH places. A deploy:prod PR that passes here
WILL be admitted by the admission webhook in steady state; if
it isn't, the discrepancy is a regression in one of the two
verification layers and warrants investigation.

### 7a.3 Operator runbook

The verifier outputs the verified predicate JSON (via
`--print-provenance`) as a workflow artefact. Download it from
the PR's Actions run when investigating an admission rejection
or audit-trail question.

## 8. Cross-references

* [`docs/image-signing.md`](image-signing.md) — Wave-1 cosign signing of the image manifest.
* [`docs/admission-policy.md`](admission-policy.md) — Wave-3 Kyverno admission policy.
* [`.github/workflows/slsa-provenance.yml`](../.github/workflows/slsa-provenance.yml) — the workflow itself.
* [`.github/workflows/verify-slsa-on-deploy.yml`](../.github/workflows/verify-slsa-on-deploy.yml) — Wave-6 pre-merge gate.
* [SLSA v1.0 provenance spec](https://slsa.dev/spec/v1.0/provenance) — upstream predicate schema.
* [slsa-verifier README](https://github.com/slsa-framework/slsa-verifier) — verifier CLI usage.
