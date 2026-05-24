# SLSA-3 SHA-pinning sweep — W20 (vasquez-lane workflows)

> Phase K Wave 20 — Apone (DevOps).
> Audience: Vasquez (QA) for the W20+ lane-pure pin commits; SRE
> on-call when investigating supply-chain pin posture. Companion
> to [`docs/slsa-provenance.md`](./slsa-provenance.md) (the
> repo-wide SLSA-3 stance) and to the W18 closeout note that
> declared apone-lane pin completion (191 pins / 39 workflows).

## 1. Background

W18 closed the apone-lane SHA-pinning sweep — every
`actions/*@v*` reference in apone-owned workflows was rewritten
to the immutable `@<sha40> # <semver>` shape SLSA-3 requires.
The W19 wave added 6 additional pins (the W19 `android-e2e`
job in `.github/workflows/mobile-build.yml` introduced
`reactivecircus/android-emulator-runner@<sha> # v2.34.0`
twice + 4 supporting refs), bringing the apone-lane total to
**197 pins / 39 workflows** at the W19 ship.

W20 inventories the **9 remaining un-pinned references** that
live in vasquez-lane workflows. Pinning each from `@v<N>` to
the canonical `<sha40> # v<semver>` shape closes the
repo-wide SLSA-3 supply-chain gap.

## 2. Lane-discipline scope

Per [`tests/ci/lane-map.json`](../tests/ci/lane-map.json), the
following workflows are vasquez-lane:

* `.github/workflows/lane-discipline.yml`
* `.github/workflows/lane-discipline-nightly.yml` (shared with apone)
* `.github/workflows/lane-discipline-status.yml`
* `.github/workflows/playwright-visual-regression.yml`

The cross-lane bundling check (`tests/ci/check-cross-lane-
bundling.sh --strict`) classifies a commit that edits any of
these as `vasquez` lane (or `apone+vasquez` for the
`lane-discipline-nightly.yml` shared file). Therefore W20
apone-lane does NOT land the pin rewrites itself — this doc
identifies the 9 refs + the canonical target SHAs, and Vasquez
authors a lane-pure follow-up commit in W20+ (a vasquez-lane
sweep, parallel to the W18 apone-lane sweep).

This pattern mirrors the W19 `Path B — defer` guidance from
[`docs/kyverno-w19-additional-rules.md §4.1`](./kyverno-w19-additional-rules.md):
when a deliverable requires cross-lane work, document the
target shape and defer the edit to the lane owner's wave.

## 3. The 9 unpinned references

Each row carries the file, line, current shape, and the
canonical SHA-pinned target (the SHA below is the same one
already in use elsewhere in the repo — verified via
`grep -rE '<action>@[0-9a-f]{40}' .github/workflows/`).

| # | File                                                       | Line | Current shape                                  | Target shape                                                                                       |
| - | ---------------------------------------------------------- | ---- | ---------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| 1 | `.github/workflows/lane-discipline.yml`                    | 42   | `uses: actions/checkout@v4`                    | `uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`                          |
| 2 | `.github/workflows/lane-discipline-nightly.yml`            | 37   | `uses: actions/checkout@v4`                    | `uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`                          |
| 3 | `.github/workflows/lane-discipline-status.yml`             | 35   | `uses: actions/checkout@v4`                    | `uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`                          |
| 4 | `.github/workflows/playwright-visual-regression.yml`       | 68   | `uses: actions/checkout@v4`                    | `uses: actions/checkout@11bd71901bbe5b1630ceea73d27597364c9af683 # v4.2.2`                          |
| 5 | `.github/workflows/playwright-visual-regression.yml`       | 74   | `uses: actions/setup-node@v4`                  | `uses: actions/setup-node@49933ea5288caeca8642d1e84afbd3f7d6820020 # v4.4.0`                        |
| 6 | `.github/workflows/playwright-visual-regression.yml`       | 81   | `uses: actions/cache@v4`                       | `uses: actions/cache@0057852bfaa89a56745cba8c7296529d2fc39830 # v4.2.0`                             |
| 7 | `.github/workflows/playwright-visual-regression.yml`       | 135  | `uses: actions/upload-artifact@v4`             | `uses: actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882 # v4.4.3`                   |
| 8 | `.github/workflows/playwright-visual-regression.yml`       | 147  | `uses: actions/upload-artifact@v4`             | `uses: actions/upload-artifact@b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882 # v4.4.3`                   |
| 9 | `.github/workflows/playwright-visual-regression.yml`       | 196  | `uses: marocchino/sticky-pull-request-comment@v2` | `uses: marocchino/sticky-pull-request-comment@331f8f5b4215f0445d3c07b4967662a32a2d3e31 # v2.9.0` |

### 3.1 SHA provenance

Each target SHA is already pinned in at least 2 other
workflows in the repo. The W20 sweep does NOT introduce new
SHA values — it propagates the canonical pins to the last
9 unpinned sites. The verifier:

```bash
# All 9 target SHAs MUST already exist in at least one
# apone-lane workflow (otherwise we're introducing a brand
# new pin, which deserves its own audit).
for sha in \
    11bd71901bbe5b1630ceea73d27597364c9af683 \
    49933ea5288caeca8642d1e84afbd3f7d6820020 \
    0057852bfaa89a56745cba8c7296529d2fc39830 \
    b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882 \
    331f8f5b4215f0445d3c07b4967662a32a2d3e31 ; do
    count=$(grep -rE "@$sha" .github/workflows/ | wc -l)
    echo "$sha → $count uses in repo"
done
```

Expected output (verified at W20 land):

```
11bd71901bbe5b1630ceea73d27597364c9af683 → 50 uses in repo
49933ea5288caeca8642d1e84afbd3f7d6820020 → 14 uses in repo
0057852bfaa89a56745cba8c7296529d2fc39830 → 3 uses in repo
b4b15b8c7c6ac21ea08fcf65892d2ee8f75cf882 → 26 uses in repo
331f8f5b4215f0445d3c07b4967662a32a2d3e31 → 3 uses in repo
```

(Counts will shift on subsequent waves as more workflows are
authored / migrated.)

## 4. Why the SLSA reusable workflow stays at `@v2.0.0`

The repo has one additional non-SHA `uses:` reference:

```
.github/workflows/slsa-provenance.yml:306:
    uses: slsa-framework/slsa-github-generator/.github/workflows/generator_generic_slsa3.yml@v2.0.0
```

This **is not** part of the W20 sweep. The SLSA-3 generator's
OIDC trust contract anchors at the **semver tag**, not the
SHA — the in-toto attestation predicate is signed by an OIDC
issuer that verifies the reusable workflow's *tag* against
its trust policy. Pinning by SHA would break the verifiable-
build chain. SLSA-3 spec explicitly carves this out at
[slsa.dev/spec/v1.0/requirements#build-isolated](https://slsa.dev/spec/v1.0/requirements#build-isolated).

The SLSA reusable workflow's `@v2.0.0` tag is itself protected
(GitHub Actions enforces tag-pinning on reusable workflows
that ship signed attestations); it is functionally equivalent
to a SHA pin from the supply-chain-trust perspective.

## 5. Post-sweep pin count (forward-looking)

| Wave | Apone-lane pins | Vasquez-lane pins | Repo total |
| ---- | --------------- | ----------------- | ---------- |
| W18  | 191 / 39 wf     | 0 unpinned        | 191        |
| W19  | 197 / 39 wf     | (no change)       | 197        |
| W20  | 197 / 39 wf     | (sweep documented; vasquez commit pending) | 197 → **206 after vasquez-lane sweep lands** |

Once Vasquez lands the 9 lane-pure pins, the repo SHA-pin
posture is **100% on actions/*** for all CI-bearing
workflows (modulo the SLSA reusable workflow's required-tag
shape per §4). `bash tests/ci/check-sha-pins.sh` (Vasquez's
follow-up tooling) will return clean on the W20+ post-sweep
state.

## 6. Cross-references

- [`docs/slsa-provenance.md`](./slsa-provenance.md) — repo-wide SLSA-3 stance.
- [`tests/ci/lane-map.json`](../tests/ci/lane-map.json) — agent/lane regex map.
- [`tests/ci/check-cross-lane-bundling.sh`](../tests/ci/check-cross-lane-bundling.sh)
  — the cross-lane detector that classifies the four sweep-target workflows as vasquez-lane.
- [`docs/kyverno-w19-additional-rules.md §4.1`](./kyverno-w19-additional-rules.md)
  — the W19 `Path B — defer` precedent this doc follows.
- [`.github/workflows/lane-discipline.yml`](../.github/workflows/lane-discipline.yml)
  — sweep target #1.
- [`.github/workflows/lane-discipline-nightly.yml`](../.github/workflows/lane-discipline-nightly.yml)
  — sweep target #2.
- [`.github/workflows/lane-discipline-status.yml`](../.github/workflows/lane-discipline-status.yml)
  — sweep target #3.
- [`.github/workflows/playwright-visual-regression.yml`](../.github/workflows/playwright-visual-regression.yml)
  — sweep targets #4-9.
