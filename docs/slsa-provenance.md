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

## 7b. SLSA-3 readiness assessment (Phase K Wave 15 — Apone)

> Phase K Wave 15 — Apone (DevOps). This section is the W15
> deliverable for the SLSA-3 hardening survey. The W6+
> provenance generated by `slsa-provenance.yml` clears the
> SLSA Level 2 bar (provenance authenticated, non-falsifiable
> link from source commit → artefact), but several specific
> SLSA Level 3 requirements are NOT met today. **No actual
> hardening lands this wave.** W15 documents the gaps; the
> W16 plan lands the remediation PRs.

### 7b.1 SLSA Level 2 vs Level 3 — what's the delta?

Quick recap of the SLSA framework:

| Level | Bar                                                                                       | Where we are     |
|-------|-------------------------------------------------------------------------------------------|------------------|
| L1    | Provenance exists.                                                                        | ✅ (since W4)    |
| L2    | Provenance is authenticated (signed) by a known builder; tamper-resistant transport.       | ✅ (since W4 — Sigstore signing + Rekor publish). |
| L3    | Provenance is **non-forgeable**: builder is **isolated** from the build; signing keys live in a dedicated trust boundary the build cannot reach. | **Partial — see gaps §7b.2.** |
| L4    | Hermetic + reproducible build; two-party review on every release.                          | Out of Phase K + L scope. |

The W4+ workflow `slsa-provenance.yml` uses the upstream
`slsa-github-generator/generator_container_slsa3.yml` reusable
workflow, which **claims SLSA-3 conformance** for the
GitHub-Actions-hosted runner case. The "L3 claim" rests on
three properties the upstream provides:

1. The provenance generator runs in a **separate reusable
   workflow** (different runner pool, different identity).
2. The build's secrets are NOT exposed to the generator (the
   generator sees only the build's output digest + the public
   GH context).
3. The generator's identity (its OIDC subject) is pinned in
   downstream verifiers (`kyverno-cosign-verify.yaml` §verify-
   slsa-provenance, `verify-slsa-on-deploy.yml` §6).

These three combined are what the upstream calls "L3-on-
GitHub-Actions". **For a true SLSA-3 conformance audit**,
several additional hardening surfaces apply.

### 7b.2 W15 gap analysis — three SLSA-3 surfaces

#### 7b.2.1 Provenance signing key isolation

**The SLSA-3 requirement.** The signing key used to sign the
provenance MUST live in a trust boundary the build cannot
reach. Specifically: the build runner cannot read, exfiltrate,
or substitute the signing material.

**Where we are today (W6+ baseline).**

* The build (`docker-build.yml` → SLSA reusable workflow)
  runs on a `ubuntu-latest` GitHub-hosted runner.
* The provenance generator (`generator_container_slsa3.yml`)
  runs on a **separate** `ubuntu-latest` runner pool.
* The signing key is **Sigstore Fulcio short-lived cert**
  issued to the generator workflow's OIDC token — generated
  per-run, never persisted. The build runner never sees the
  signing material.

**Gap.** Both runner pools share the GitHub-hosted runner
fleet. A compromised runner image at the GitHub-fleet level
would, in principle, see both the build AND the generator
traffic. The SLSA spec calls this out as an acceptable
trade-off ("GH-hosted runners are SLSA-3 within the trust
boundary of GitHub itself"), but a stricter conformance audit
would demand:

* **Dedicated runner pool** for the generator (self-hosted
  ephemeral runner in a separate cloud account).
* **Hardware-rooted signing key** (HSM-backed Fulcio
  alternative, or in-cluster KMS-backed cosign key with the
  KMS access restricted to the dedicated runner pool).

**Verdict.** **PARTIAL** — meets L3 within the GitHub-fleet
trust boundary; would NOT meet L3 against a stricter audit
that doesn't trust shared GH-hosted runners.

**Remediation (W16+ candidate).** Stand up a dedicated
self-hosted runner pool in a separate AWS account for the
generator workflow. Estimated 2 waves: W16 designs the
runner-pool isolation; W17 lands the actual runner. Cost:
~$150 / month for a c6i.large runner pool (vs $0 for the
GH-hosted runners).

#### 7b.2.2 Build platform attestation

**The SLSA-3 requirement.** The provenance MUST attest to the
**build platform's identity** — not just the build's identity.
A verifier of the provenance can determine which trusted build
platform produced the artefact and trace any platform-level
compromise back.

**Where we are today (W6+ baseline).**

* The provenance predicate carries `builder.id` =
  `https://github.com/slsa-framework/slsa-github-generator/.github/workflows/generator_container_slsa3.yml@refs/tags/v2.x.y`.
* Downstream verifiers (Kyverno + `verify-slsa-on-deploy.yml`)
  pin a CEL expression against `builder.id` — see
  `docs/slsa-provenance.md §4` for the expression shape.

**Gap.** The pinning is at the **workflow** layer, NOT the
**platform** layer. A successful supply-chain attack against
the SLSA-github-generator repository itself (compromised
maintainer push) would re-use the same `builder.id` URL but
emit a malicious predicate. The SLSA spec recognises this gap
("SLSA platform attestation is a future spec deliverable")
but recommends additional layers:

* **Builder tag pin** (we have this — `@refs/tags/v2.x.y`
  pinning).
* **Builder SHA pin** (we DO NOT have this — verifiers don't
  pin by SHA, only by tag).
* **Builder transparency log** (Rekor entries for the
  generator binary itself) — not yet a thing.

**Verdict.** **PARTIAL** — `builder.id` pinning is the
strongest practical surface today; SHA pinning would close
the residual gap.

**Remediation (W16+ candidate).** Add a CEL conjunction in
`kyverno-cosign-verify.yaml` + `verify-slsa-on-deploy.yml`
that pins the generator's SHA in addition to the tag. Source-
of-truth: the `slsa-framework/slsa-github-generator` repo's
release-tag-to-SHA mapping. Estimated 1 wave: W16 lands the
pin on both verifiers. Risk: tag-to-SHA mapping changes on
generator-side breaking upgrades require a coordinated update;
acceptable cost.

#### 7b.2.3 Isolated build environment

**The SLSA-3 requirement.** The build environment MUST be
isolated from the outside world during the build — no
arbitrary network egress, no persistent state across builds,
no pre-installed software the build doesn't audit.

**Where we are today (W6+ baseline).**

* The build runner is `ubuntu-latest` — full network egress,
  full pre-installed software set (Node, Docker, AWS CLI, gh
  CLI, etc.).
* The build script's network calls are NOT enumerated in the
  provenance predicate.
* No `--isolation` flag on the Docker build (BuildKit's
  experimental isolation features are not used).

**Gap.** Two specific gaps:

* **Network egress.** A build script can `curl` arbitrary
  hosts mid-build. The cosign + SLSA chain attests the OUTPUT
  bits but not the build's network behaviour. A future audit
  would demand egress logging + allow-list pinning.
* **Pre-installed runner tools.** The runner has dozens of
  pre-installed binaries; a compromised binary (e.g. a
  hypothetical malicious `gh` CLI install) would taint the
  build. The provenance lists the runner OS but not the full
  binary set.

**Verdict.** **NOT MET** — the GH-hosted runner's permissive
network + tool set is the canonical SLSA-3 gap for any
GitHub-Actions-hosted build. Closing this gap is the
biggest-effort item in the W15 survey.

**Remediation (W16+ candidate, multi-wave).**

* **Network egress allow-list.** Use a sidecar firewall on
  the runner (`tinyfox-cni` or equivalent) to allow-list only
  GHCR + Sigstore + npm registry. Build scripts that need
  other hosts fail-CLOSED. Estimated 1 wave: W16 designs +
  staging-tests; W17 lands in prod build.
* **Hermetic build container.** Use BuildKit's
  `--allow=network.none` for the inner Docker build phase
  (the outer wrapper still needs network for npm/Sigstore but
  the actual `dotnet publish` can be hermetic). Estimated
  1 wave: W17 lands the BuildKit flag.
* **Provenance materials enumeration.** Extend the predicate
  to enumerate every external input (every `npm install`-
  fetched package digest). Upstream
  `generator_container_slsa3` supports this via the
  `materials` field; we don't currently populate it.
  Estimated 1 wave: W18 lands the materials emission.

### 7b.3 W15 → W16 plan

| Gap surface (W15 §7b.2)                       | Severity   | W16+ wave | Effort           |
|------------------------------------------------|------------|-----------|------------------|
| §7b.2.1 — Signing key isolation                | LOW        | W16+W17   | 2 waves; ~$150/mo runner cost. |
| §7b.2.2 — Builder SHA pinning                  | LOW–MED    | W16       | 1 wave; CEL update only.       |
| §7b.2.3a — Network egress allow-list           | MED        | W16+W17   | 2 waves; staging-test required. |
| §7b.2.3b — Hermetic BuildKit container         | MED–HIGH   | W17       | 1 wave; risk = build breakage on undocumented network deps. |
| §7b.2.3c — Materials enumeration               | HIGH       | W18       | 1 wave; predicate-shape change is downstream-verifier-breaking — coordinate with verifier pinning. |

**Sequencing.** W16 lands the LOW + LOW-MED items first
(§7b.2.2 builder SHA pin + §7b.2.1 design memo for the
runner-pool isolation). W17 lands the MED items (§7b.2.1
actual runner + §7b.2.3a network allow-list + §7b.2.3b
hermetic BuildKit). W18 lands the HIGH item (§7b.2.3c
materials enumeration with verifier-side coordination). The
sequencing is **incremental SLSA-3 closeness** — at each
wave, the gap narrows; no single wave attempts a full L3
conformance audit.

### 7b.4 Why not now (W15)?

Three reasons W15 surveys rather than executes:

1. **Cost-bearing items first.** §7b.2.1's dedicated runner
   pool is ~$150/mo additional infrastructure cost. Stephen's
   call on the budget surface before the wave lands;
   surveying first surfaces the cost shape.
2. **Verifier-breaking changes coordinate.** §7b.2.3c
   materials enumeration changes the predicate shape; the
   downstream Kyverno + `verify-slsa-on-deploy.yml` verifiers
   need to update their CEL expressions in the same release.
   Surveying first lets the verifier-side change land
   simultaneously.
3. **Phase K close-out throughput.** W15 already lands four
   other deliverables (Kyverno enforce pre-wire, HPA tuning
   pre-flight, lane-discipline-nightly heredoc fix, us-east-1
   drift check). Adding the SLSA-3 hardening would balloon
   the wave beyond a reviewable size. Surveying is the right
   W15 deposit; executing across W16-W18 is the right cadence.

### 7b.5 What the W15 survey does NOT change

* The W6+ generated provenance still clears the SLSA-2 bar +
  the GH-Actions-trust-boundary SLSA-3 bar. No production
  posture regression.
* The downstream Kyverno + `verify-slsa-on-deploy.yml`
  verifiers continue to enforce on the existing predicate
  shape. No verifier-side change required at W15.
* The build pipeline + the provenance pipeline + the
  verifier pipeline all run unchanged.

The W15 deliverable is **the gap document + W16+ plan**, not
a code or infra change. The audit trail is this section.

## 8. Cross-references

* [`docs/image-signing.md`](image-signing.md) — Wave-1 cosign signing of the image manifest.
* [`docs/admission-policy.md`](admission-policy.md) — Wave-3 Kyverno admission policy.
* [`.github/workflows/slsa-provenance.yml`](../.github/workflows/slsa-provenance.yml) — the workflow itself.
* [`.github/workflows/verify-slsa-on-deploy.yml`](../.github/workflows/verify-slsa-on-deploy.yml) — Wave-6 pre-merge gate.
* [SLSA v1.0 provenance spec](https://slsa.dev/spec/v1.0/provenance) — upstream predicate schema.
* [slsa-verifier README](https://github.com/slsa-framework/slsa-verifier) — verifier CLI usage.
