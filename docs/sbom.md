# Software Bill of Materials (SBOM) + Supply-Chain Attestation

> Phase J Wave 9 — Apone (DevOps).

This project ships an **SBOM + vulnerability scan workflow** at
[`.github/workflows/sbom.yml`](../.github/workflows/sbom.yml) that runs
on every push to `main`, every PR touching the Dockerfile or any
`.csproj` / `package.json`, and weekly via cron (Monday 09:00 UTC).

The workflow produces:

| Artefact | Format | Where it lives |
|---|---|---|
| Container CycloneDX SBOM | `cyclonedx-json` | Workflow artefact `sbom.cyclonedx.json` (retained 30 days); also attached to GitHub's Dependency Graph |
| Container SPDX SBOM | `spdx-json` (SPDX 2.3) | Workflow artefact `sbom.spdx.json` |
| Trivy table report | `table` (stdout) | Workflow log |
| Trivy SARIF report | `sarif` | Uploaded to GitHub code-scanning (Security tab → "Code scanning alerts") |

## Why two SBOM formats?

CycloneDX is the canonical OWASP-led format and the one Trivy /
Grype / Dependency-Track consume natively. SPDX 2.3 is what the US
Government's [EO 14028](https://www.cisa.gov/sbom) compliance tooling
and Microsoft's SBOM Tool expect. We emit both so downstream consumers
(release engineers, security auditors, regulator-facing pipelines) can
pick the one their tooling wants without an extra conversion hop.

## Severity gate

The Trivy step runs with:

```yaml
severity: "CRITICAL,HIGH"
exit-code: "1"
ignore-unfixed: true
vuln-type: "os,library"
```

so the workflow fails (and blocks the PR) on any CRITICAL or HIGH CVE
that has a known fix. `ignore-unfixed: true` keeps the gate practical
— unfixed CVEs are surfaced in the SARIF dashboard but do not block
merges until a patched upstream version exists.

A separate Trivy step with `exit-code: '0'` runs `if: always()` to emit
the SARIF report regardless of whether the gating step failed. That
way the GitHub Security tab always has the findings record, even when
the gating step is RED.

### Suppressing a finding

When a CRITICAL/HIGH must be tolerated short-term (e.g. an upstream
fix exists but breaks an API we depend on), add an entry to
`.trivyignore` at the repo root:

```
# CVE-2026-12345 — netfx false positive against System.Text.Json; we
# don't deserialise untrusted JSON in the affected code path. Tracked
# in #481. Re-evaluate when Microsoft ships the .NET 11 patch.
CVE-2026-12345
```

Suppressions must include a code comment justifying the entry and a
tracking issue. Re-review every quarter.

## Provenance / signing

Wave 9 ships the SBOM + scan pipeline. **Image signing** (cosign +
keyless OIDC) is tracked as a follow-up in
`.squad/decisions/inbox/apone-phase-j-wave-9.md` — it's a one-step
extension to the existing workflow (add a `cosign sign` step after
the build-push, point it at the GHCR-issued OIDC ID token). The
ground-truth signing identity will be
`https://github.com/long2know/mahjong-autotable/.github/workflows/sbom.yml@refs/heads/main`.

## Why the weekly cron?

A push-triggered workflow only re-scans an image when its source has
changed. New CVEs are published every day against deps that haven't
been touched in weeks. The Monday 09:00 UTC cron re-scans the
latest `main` image so a CVE published against a pinned dep gets
flagged within seven days.

## Local reproduction

To run the same scan locally before pushing:

```bash
# Build the image locally.
docker build -t mahjong-autotable:local .

# Generate the CycloneDX SBOM.
docker run --rm \
  -v "$PWD:/work" -v /var/run/docker.sock:/var/run/docker.sock \
  anchore/syft \
  mahjong-autotable:local \
  -o cyclonedx-json > sbom.cyclonedx.json

# Run Trivy with the same gating threshold the workflow uses.
docker run --rm \
  -v /var/run/docker.sock:/var/run/docker.sock \
  aquasec/trivy:latest image \
  --severity CRITICAL,HIGH \
  --ignore-unfixed \
  --exit-code 1 \
  mahjong-autotable:local
```

If the local Trivy run is RED, the workflow will be RED too. Fixing
the finding locally first is much faster than waiting for a workflow
re-run.

## Related docs

- [`kubernetes.md`](kubernetes.md) — production deployment pattern
  (Wave 9 also adds the pre-rollout migration Job).
- [`docker.md`](docker.md) — image build + runtime contract.
- [`backup-restore.md`](backup-restore.md) — DR runbook;
  cross-references the SBOM artefacts when restoring to a sealed
  image tag.
