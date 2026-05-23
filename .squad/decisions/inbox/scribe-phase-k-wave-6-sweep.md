# Scribe — Phase K Wave 6 Sweep Memo

**Author:** Scribe (Archive)
**Branch:** `stlong/phase-k-wave-6-bringup`
**Date:** 2026-07-04
**Base:** main `954c8b3` (PR #51 — Phase K Wave 5)

## Mission

Fold the four W6 agent memos (`bishop-phase-k-wave-6.md`,
`hicks-phase-k-wave-6.md`, `apone-phase-k-wave-6.md`,
`vasquez-phase-k-wave-6.md`) into canonical `.squad/decisions.md`
under a new `## Phase K — Wave 6` section; append the closeout
entry to `.squad/agents/scribe/history.md`; report the W7 forward
queue.

## Final gate

| Pass | Fail | Skip | Total | Δ vs Wave 5 (1345) |
|------|------|------|-------|---------------------|
| **1422** | **0** | **0** | **1422** | **+77** |

Zero-skip streak **20 waves** (J.1 → J.10 + K.1 → K.6).
`MaxParallelThreads=2` workaround stays retired courtesy of
Vasquez's W5 `RegressionHostFixture` `[CollectionDefinition]`.

## Wave 6 commit ledger (6 commits ahead of main at sweep time)

| SHA      | Author                  | Lane    | Summary                                                                                          |
|----------|-------------------------|---------|--------------------------------------------------------------------------------------------------|
| `abf7624` | Squad (Coordinator)     | infra   | wire W6 coturn manifest set into base kustomization (`kustomization.yaml` `resources:` block — 7-line append covering coturn 3 files + W2 turn-server.yaml). Flips the gate from 1421/1/0 → **1422/0/0**. |
| `6630c6d` | Vasquez (QA)            | tests   | 76 new W6 facts under `Phase_K_W6/` (Bishop 11 + Hicks 5 + Apone 8 + shim sanity 7 + smokes 25) + regression-class rename + 10 W6 carry-forward facts + 7 Playwright e2e specs + lane-discipline CI + `CommentaryGeneratorTestShim` |
| `ef719df` | Bishop (Backend)        | backend | RS256 JWT + voice livestream HLS controller + SpectatorVoiceHub SignalR + commentary stub + Swiss + double-elim brackets + OAuth runbook + OIDC discovery                       |
| `4fb22b6` | Apone (DevOps)          | devops  | multi-region DR Terraform + GH OIDC narrow + production-shape coturn k8s + container-scan tune + Trivy 30-day allowlist + mobile internal-testing + label-gated SLSA-verifier   |
| `191bf96` | Hicks (Frontend)        | front   | commentary panel + spectator HLS viewer + bracket renderers (Swiss + double-elim) + PWA install button + tour stops + maskable icons + three.js sweep (GLTFLoader dynamic-import + Stats opt-in) |
| (`abf7624` above; listed first by recency) | | | |

**+ this Scribe sweep commit (HEAD on push):** decisions.md +
history.md + this inbox memo.

## Author identity verification

Every commit in this wave is git-authored as the intended agent
identity via the per-invocation `git -c user.name=… -c user.email=…
commit ...` race-safe binding. Verified via
`git log --format='%an <%ae>'` post-push:

- `abf7624` → `Squad (Coordinator) <squad@coordinator.mahjong>` ✓
- `6630c6d` → `Vasquez (QA) <vasquez@squad.mahjong>` ✓
- `ef719df` → `Bishop (Backend) <bishop@squad.mahjong>` ✓
- `4fb22b6` → `Apone (DevOps) <apone@squad.mahjong>` ✓
- `191bf96` → `Hicks (Frontend) <hicks@squad.mahjong>` ✓

**W6 identity-race hardening WIN:** Hicks's pre-flight `git config
user.{name,email}` race-state was still observed in `.git/config`
mid-wave (the W4→W5 incurable race), but the per-invocation `-c`
override BYPASSED it — Hicks's `191bf96` author resolves to
`Hicks (Frontend) <hicks@squad.mahjong>` as intended.

## Lane-discipline CI — first-run findings

Ran `tests/ci/check-cross-lane-bundling.sh --pr HEAD --base
origin/main` against the W6 four agent commits. Result:

| SHA (short)   | author  | lanes touched       | result    | notes                                                                                               |
|---------------|---------|---------------------|-----------|-----------------------------------------------------------------------------------------------------|
| `66f2b1adfb`* | vasquez | `[vasquez]`         | ✓ clean   | Vasquez's intermediate bring-up SHA                                                                 |
| `ef719df3f3`  | bishop  | `[bishop vasquez]`  | ✗ bundle  | Touched `Phase_K_W3/GameVoiceEnabledFlagTests.cs` — legitimate W3 test patch for W6 multi-hub      |
| `4fb22b6919`  | apone   | `[apone]`           | ✓ clean   |                                                                                                     |
| `191bf965cd`  | hicks   | `[hicks vasquez]`   | ✗ bundle  | Touched `tests/selectors.md` — legitimate W6 testid append to shared contract doc                  |

\* Vasquez's W6 final commit on the shipped branch is `6630c6d`.

The 2 violations are **legitimate cross-lane EDITS**, NOT
WIP-absorption bundling (the W3/W4/W5 trend that produced cross-
lane content sweeps). The work content correctly belongs to the
editing agent per the inbox memos. Vasquez documented refining
the script's `Phase_K_W*/<AgentName>/` subfolder attribution to
avoid counting Bishop's own `Phase_K_W6/Bishop/BracketGeneratorDeterminismTests.cs`
against him (without the refinement, **3** false positives).

**W7 path-forward:** each agent opens their OWN PR + lane-
discipline runs on each. The historical wave-level squash-merge
pattern stops at W6. Cross-lane EDITS (legitimate test patches +
shared contract docs) are still handled via the editing agent's
PR; the bundling alarm catches WIP-absorption only.

## Bundle metrics

| Chunk                                  | Wave 5     | Wave 6      | Δ                                  |
|----------------------------------------|------------|-------------|------------------------------------|
| `autotable-src.<hash>.js` (eager)      | 218.7 kB   | **219.68 kB** | +1.0 kB                            |
| `scene-shell.<hash>.js`                | 2.33 kB    | **2.33 kB**   | unchanged ✅                       |
| `game-bootstrap.<hash>.js`             | 169.98 kB  | **169.98 kB** | unchanged ✅                       |
| `three-renderer.<hash>.js` (small)     | 144.9 kB   | **99.1 kB**   | **−45.8 kB**                       |
| `three-renderer.<hash>.js` (big)       | 724.7 kB   | **739.72 kB** | +15 kB — W7 bundler-swap decision  |
| `GLTFLoader.<hash>.js` (NEW)           | —          | **44.61 kB**  | split from small chunk             |
| `stats.module.<hash>.js` (NEW)         | —          | **1.9 kB**    | split, opt-in only (`?stats=1`)    |
| `commentary-panel.<hash>.js` (NEW)     | —          | **3.77 kB** ✅ | target <80 kB                       |
| `spectator-livestream.<hash>.js` (NEW) | —          | **5.41 kB**   | hash route only                    |
| `tournaments.<hash>.js`                | unchanged  | unchanged*   | bracket-renderer code inlined      |

\* The bracket-renderer strategy module is dynamic-imported on
the first `rerenderBracket()` call.

**Three-renderer 700 KB ceiling NOT met** (chunk weighs 739.72
kB, +15 kB over W5). Parcel's namespace-re-export tree-shake
limit on three.js is the cause; real reductions require a
bundler swap (esbuild/rollup handle namespace re-exports better)
or a deep refactor to `three/src/*` direct imports.
**`docs/frontend-three-budget.md`** carries the W7 path-forward
options. **W7 must NOT re-attempt the target without a bundler
decision.**

## W7 forward queue (status)

### Bishop (4 items)

- RS256 SSM provisioning hand-off to Apone (private key material)
- Losers-bracket resurrection (Phase L)
- Real ffmpeg livestream pipeline (Phase L)
- Google OAuth verification submission (Stephen action — 4-6
  week turnaround; scope justifications pre-written in
  `docs/oauth-production-setup.md` §7.1)

### Hicks (4 items)

- Bundler swap decision (Vite vs Rspack vs keep-Parcel) to break
  the three.js 700 KB ceiling
- CSP allowlist for `cdn.jsdelivr.net` (HLS.js polyfill source)
- Phase L commentary JSON contract verification once Bishop
  ships the real generator
- OutlinePass replacement spike (Apone's container-size pressure)

### Apone (5 items, forward queue)

- Helm chart-of-charts for post-bootstrap add-ons (W5 → W6 →
  W7 deferral; increasingly overdue)
- Route53 + ACM + WAF terraform module (domain-bound)
- Signature-preserving GHCR→ECR mirror via `crane copy` /
  `cosign copy`
- Mobile External-Testing promotion automation
  (`workflow_dispatch`-only with approvals)
- Pre-commit hook for the six-file signer-URL lock-step

### Vasquez (3 items)

- Single-lane PR enforcement (each agent opens own PR;
  bring-up branches stop bundling at W7)
- OIDC RS256 hard contract (currently soft-passed via reflection;
  tighten to `token_endpoint` + `authorization_endpoint` +
  `id_token_signing_alg_values_supported` once Bishop flips the
  algorithm default)
- Three-renderer trend tracking per wave in a dedicated
  Playwright spec

### Scribe / coordinator (4 carry-forward into W7 prompt template)

- Per-invocation `git -c user.name=X -c user.email=Y commit ...`
  remains canonical
- `flock -w 120 9 ... 9>/tmp/squad-git-lock` mutex stacked with
  the per-invocation binding
- Selective `git add <path>` only — NEVER `git add -A` /
  `git add .` during cross-agent waves
- `Phase_K_W*/<AgentName>/` test subfolder attribution in the
  lane-discipline path-mapping

## Author identity for THIS commit

Scribe sweep commit authored via per-invocation race-safe
binding:

```
git -c user.name="Scribe (Archive)" -c user.email="scribe@squad.mahjong" commit -m "..."
```

Verified post-commit via `git log -1 --format='%an <%ae>'`
returning `Scribe (Archive) <scribe@squad.mahjong>`.

## Files staged (selective adds only — NEVER `git add -A`)

- `.squad/decisions.md` (new W6 section appended after W5 DONE
  marker)
- `.squad/agents/scribe/history.md` (W6 closeout entry appended)
- `.squad/decisions/inbox/scribe-phase-k-wave-6-sweep.md` (this
  file — force-added because `.squad/decisions/inbox/` is
  gitignored)

Pre-session untracked files NOT staged (`.copilot/skills/error-recovery/`,
`.github/workflows/squad-*.yml`, `.tool-actionlint/`,
`.tool-terraform/`, `.work/`).

## Closing

Branch `stlong/phase-k-wave-6-bringup` ready for PR against `main`.
Test gate **1422/0/0** at close; zero-skip streak **20 waves**;
identity-race hardening landed and HELD; lane-discipline CI
shipped + caught 2 legitimate cross-lane EDITS on first run;
W3/W4/W5 cross-lane content bundling trend broken at W6.
