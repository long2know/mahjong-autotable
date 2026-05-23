# Signer-identity invariant — W7 (Apone, DevOps)

> Phase K Wave 7 — Apone (DevOps).

The repo's image-supply-chain stack uses cosign **keyless**
signing (Sigstore Fulcio / Rekor). The signer's OIDC subject is
the URL of the reusable workflow that ran the signer step:

```
^https://github\.com/long2know/mahjong-autotable/\.github/workflows/sign-image\.yml@refs/(heads/main|tags/v.*)$
```

Six surfaces in this repo verify against that regex. They MUST
stay in lock-step — drift creates a silent failure where a
release looks signed locally but is rejected by Kyverno at
admission (the W5 rehearsal incident). Wave 7 added an
automated guard.

## 1. The six tracked surfaces

| # | File | What carries the regex |
|---|---|---|
| 1 | `.github/workflows/sign-image.yml` | `EXPECTED_IDENTITY_REGEXP:` env var on the verify step |
| 2 | `.github/workflows/verify-signature.yml` | `expected-identity-pattern:` default input on the reusable workflow |
| 3 | `.github/workflows/slsa-provenance.yml` | `EXPECTED_IDENTITY_REGEXP:` env-block marker (Wave 7) |
| 4 | `infra/k8s/policies/kyverno-cosign-verify.yaml` | `subjectRegExp:` in the keyless attestor block |
| 5 | `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml` | `subjectRegExp:` in the prod enforce patch |
| 6 | `docs/slsa-provenance.md` §4a | Literal regex in a fenced code block (Wave 7) |

> **Path divergence note (W7):** the Wave 7 spec listed the fifth
> surface as `infra/k8s/policies/kyverno-enforce-patch.yaml`, but
> the actual path is `infra/k8s/overlays/prod/kyverno-enforce-patch.yaml`
> (the patch is in the prod overlay, not under policies). The
> check script uses the real path.

## 2. The guard

`scripts/check_signer_identity.py` extracts the regex from each
of the six files, normalises the escaping convention (the kyverno
YAML uses double-quoted strings with doubled backslashes; the
GitHub-Actions YAML uses unquoted scalars; the doc uses a
fenced code block), and compares each to a canonical value
declared in the script.

The check runs:

* **Locally**, on every `git commit`, via
  [`.pre-commit-config.yaml`](../.pre-commit-config.yaml) (the
  `signer-identity-invariant` hook).
* **Manually**, with `python3 scripts/check_signer_identity.py`
  or `python3 scripts/check_signer_identity.py --show`.

Exit codes:

| Code | Meaning |
|---|---|
| 0 | All six surfaces agree. |
| 1 | One or more surfaces drifted from the canonical regex. |
| 2 | A tracked file is missing (filesystem-level error). |

## 3. Why this invariant matters

The signer-identity regex is the **only** cryptographic binding
between a published image and the workflow that produced it.
Cosign's `--certificate-identity-regexp` constrains which OIDC
subjects can have signed the artefact. If sign-image.yml signs
under subject A but the cluster's Kyverno policy expects subject
B, **every image push is rejected at admission**.

History:

* Wave 5 rehearsal — a JWT-related PR moved the cosign verify
  step from `verify-signature.yml` to an inline step in
  `sign-image.yml` and forgot to update verify-signature.yml's
  default input. Local cosign verify worked (sign-image.yml was
  consistent); the cluster's Kyverno policy was fine; but the
  scheduled image-rescan job that uses verify-signature.yml
  started rejecting EVERY image. ~25 min outage of the rescan
  alerting.
* Wave 5 retro action: codify the regex location across files.
* Wave 6 — three of the six surfaces already documented in
  in-line comments (the W5 retro action carried forward).
* **Wave 7 — automated guard + two new tracked surfaces** (the
  `slsa-provenance.yml` env marker + the slsa-provenance §4a
  doc block).

## 4. Rotation procedure

The regex MUST be rotated as a single coordinated commit. Steps:

1. **Decide the new canonical value.** The regex must match the
   OIDC subject that GitHub-Actions Fulcio will issue for the
   signer workflow runs. The pattern is fixed by Sigstore /
   GitHub:

   ```
   ^https://github\.com/<org>/<repo>/\.github/workflows/<workflow-file>@refs/(heads/<branch>|tags/<tag-glob>)$
   ```

   You only rotate when ONE of these changes:

   | Reason | What changes in the regex |
   |---|---|
   | Repo rename / transfer | `<org>` and/or `<repo>` |
   | Signer workflow renamed | `<workflow-file>` |
   | Signer moved to a different default branch | `<branch>` |
   | Tag-naming convention changed | `<tag-glob>` |

2. **Update the canonical value in
   `scripts/check_signer_identity.py`** (`CANONICAL_REGEX`).

3. **Update all six tracked files** in the SAME commit:

   ```bash
   # GitHub Actions workflows + docs (unquoted YAML / plain text):
   #   replace the regex literal — single-escaped backslashes.
   #
   # Kyverno YAML + verify-signature.yml default (double-quoted):
   #   replace the regex literal — DOUBLED backslashes.
   ```

4. **Run the guard:**

   ```bash
   python3 scripts/check_signer_identity.py --show
   ```

   All six MUST show ✓. If not, fix and re-run.

5. **Verify cosign actually signs under the new subject** in
   CI on the rotation PR — the post-sign `cosign verify` step
   in `sign-image.yml` is the canary. It MUST be green before
   merge.

6. **Commit + push** with a message that lists the six files —
   reviewers should be able to confirm the six-file coverage
   from the commit diff alone.

## 5. Installing the hook + CI parity

### 5.1 Per-developer install (local gate)

```bash
# Once per developer machine:
pipx install pre-commit            # or pip install --user pre-commit
cd <repo>
pre-commit install --install-hooks

# Verify:
pre-commit run signer-identity-invariant --all-files
```

The hook runs on every `git commit` thereafter.

### 5.2 CI parity (Wave 8 — gates merge)

Wave 7 left CI parity as a follow-up; Wave 8 closes it. The
[`.github/workflows/pre-commit-check.yml`](../.github/workflows/pre-commit-check.yml)
workflow:

* Runs on every PR + push to `main`.
* Invokes `pre-commit run --all-files` — the SAME command
  developers run locally.
* Fails the PR check if any hook fails — including
  `signer-identity-invariant`, `check-yaml`,
  `end-of-file-fixer`, `trailing-whitespace`,
  `check-merge-conflict`, `check-added-large-files`.
* Dumps `python3 scripts/check_signer_identity.py --show`
  to the job log unconditionally (diagnostic — the canonical
  regex + each tracked-file value appear in the workflow log
  without rummaging through pre-commit's per-hook output).
* Pins `pre-commit~=3.7` + caches hook venvs under
  `~/.cache/pre-commit` keyed on `.pre-commit-config.yaml`.

The workflow is REQUIRED-FOR-MERGE on the `main` branch
protection rule. A developer who skips local install is still
gated by CI; a developer who installs locally gets the same
failure 5 seconds earlier on `git commit`. This closes the
"per-developer opt-in" gap the W7 hook left open.

### 5.3 Failure mode triage

If the PR check fails:

| Failure | What to do |
|---|---|
| `signer-identity-invariant` reports DRIFT | Follow §4 rotation procedure — update all six files in one commit. |
| `signer-identity-invariant` reports FILE MISSING | A tracked file was deleted / renamed — restore the file OR update `TRACKED_FILES` in `scripts/check_signer_identity.py` AND the table in §1. |
| `check-yaml` fails on a helm/kustomize template | Check the file is in the `exclude:` block of `.pre-commit-config.yaml` (helm + kyverno YAML use template tags that confuse the strict YAML parser). |
| `check-added-large-files` fails | A binary > 512 KB was committed — add an exclude entry OR Git-LFS the file. |
| Hook env cache miss | Slow first run is expected; subsequent runs are cached. No action needed. |

## 6. Cross-references

* [`scripts/check_signer_identity.py`](../scripts/check_signer_identity.py) — the implementation.
* [`.pre-commit-config.yaml`](../.pre-commit-config.yaml) — the hook wiring.
* [`docs/image-signing.md`](image-signing.md) — cosign verification reference.
* [`docs/slsa-provenance.md`](slsa-provenance.md) §4a — the documentation surface in the six-file set.
* [`docs/retro-2026-05.md`](retro-2026-05.md) §3.x — the W5 incident retro that motivated this guard.
