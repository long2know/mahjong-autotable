# Historical worktree experiments — September 2026

This directory preserves meaningful historical work from retired agent
worktrees. It is an **inactive source archive**, not a set of production
changes or executable regression suites.

## Contents

- `issue-122/*.patch.json`: the two test-only commits made after the original
  issue #122 playability-gate pull request was merged:
  `6dd255d65dbcd7637e062c86104036b61861e80f` and
  `76acb688ea16f883ee70fe752750d8718b9063bc`. They experiment with an in-page
  dealer observer and explicit confirmation that a real claim was accepted.
- `legacy-uat/`: 17 untracked TypeScript prototypes from the
  `mahjong-autotable-uat-tests` worktree, based on
  `200cad420b9225c4660b47f7f49d1711df2c5712`.
- `provenance.json`: original paths, revisions, byte counts, SHA-256 checksums,
  archival paths, and the reasons the experiments were not forward-ported.

The current post-meld rendered-position regression candidate is deliberately
not duplicated here: its integration belongs in the active test suite after
separate review and validation.

## Why these are not active tests

The application and its test helpers have advanced since these experiments.
Some prototypes intentionally describe a failing baseline; others drive
legacy Deal/Setup controls that are now hidden or absent in authoritative
Changsha mode. Their imports are retained for historical fidelity and do not
make this archive a standalone runnable test project.

Do not add this directory to TypeScript, ESLint, or Playwright discovery.
Do not copy these files into the active suites or apply the old commits
wholesale. Any future use should select a still-relevant assertion, adapt it
to the current public interaction contract, and validate it independently.

## Byte-preserving format

The 17 TypeScript files are stored unchanged. The two commit patches are
stored as UTF-8 strings in JSON envelopes so whitespace-fixing hooks cannot
alter significant blank context lines in a Git patch.

For a patch envelope, encoding its `content` string as UTF-8 reconstructs the
original patch bytes. Verify those bytes against `payloadSha256` before
reviewing or using them. `provenance.json` also records the checksum of each
stored archive file.

No runtime databases, browser profiles, storage state, cookies, environment
files, logs, screenshots, or other QA evidence belong in this directory.
Private runtime preservation is handled separately outside the repository.
