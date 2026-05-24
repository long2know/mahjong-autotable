# Apone — W19 cross-lane bundling incident — 2027-02-05

> Author: Apone (DevOps).
> Branch: `stlong/phase-k-wave-19-bringup`.
> Commit referenced: `d700cf7885688afac1e55329be1ea7dbf1fce1d6`
> (authored by Hicks (Frontend) <hicks@squad.mahjong>).
> Lane-discipline harness result on the W19 PR branch:
> `[lane-discipline] checked=1 violations=1` — AUTHOR-LANE
> MISMATCH on d700cf7 (touched=apone, author=hicks).

## 1. Incident summary

Hicks's W19 commit on the `stlong/phase-k-wave-19-bringup`
branch was the only commit pushed before this incident memo
landed. Its `git show --stat d700cf7` file list includes the
following apone-lane files in addition to Hicks's expected
frontend / docs / inbox surface:

```
.github/workflows/mobile-build.yml                                 ← apone D1
CHANGELOG.md                                                       ← apone D5
docs/argo-rollouts-install-runbook.md                              ← apone D6 (shared per legacy classifier)
docs/kyverno-w19-additional-rules.md                               ← apone D3 (shared per legacy classifier)
docs/mobile-android-e2e.md                                         ← apone D1 (shared per legacy classifier)
docs/regional-eks-bringup.md                                       ← apone D2 (shared per legacy classifier)
docs/signalr-affinity-hardening-w19.md                             ← apone D4 (shared per legacy classifier)
docs/us-east-1-apply-runbook.md                                    ← apone D2 (shared per legacy classifier)
infra/k8s/base/argo-rollouts-prereqs/namespace.yaml                ← apone D6
infra/k8s/base/argo-rollouts-prereqs/rbac.yaml                     ← apone D6
infra/k8s/base/ingress.yaml                                        ← apone D4
infra/k8s/base/kyverno-policies/disallow-lateral-movement.yaml     ← apone D3
infra/k8s/base/kyverno-policies/require-network-policy.yaml        ← apone D3
infra/terraform/regional-eks/us-east-1/preflight.yaml              ← apone D2
mobile/package.json                                                ← apone D5
.squad/decisions/inbox/apone-phase-k-wave-19.md                    ← apone (inbox memo)
```

These are precisely the 16 apone-lane files that Apone had
just staged via explicit `git add` calls under flock — the
content of every file in d700cf7 is byte-identical to what
Apone wrote. The bundling is purely an authorship +
lane-discipline problem, not a content problem.

## 2. Root cause

W18 retro flagged the inverse failure mode (Apone bundling
Hicks's untracked work via a too-broad `git add`). W19's
fix on the Apone side held — Apone used explicit
file-by-name staging with NO `-A`, NO `-u`, NO directory
wildcards. The stash baseline was kept in place through
commit, exactly as the W18 retro action item prescribed.

The W19 failure mode on Hicks's side appears to be one of:

* a broad `git add -A` / `git add .` / `git add <directory>/`
  that swept the index clean of apone's already-staged
  files AND re-added them under hicks's authorship before
  hicks's commit landed; OR
* a `git stash pop` of apone's baseline stash inside the
  hicks flock-protected block (the apone baseline stash
  contained ONLY untracked files — but pop would still
  have restored them into the working tree where a
  subsequent `git add -A` would sweep them up).

Both modes are blocked by the W18 retro recipe (the recipe
applies to ALL agents, not just to the agent that triggered
the retro). The actual root cause is **lane-discipline
recipe not yet adopted on the hicks lane** — Hicks's W19
bring-up does not yet enforce the explicit-add-only +
no-pop-before-commit + diff-cached-name-only-verify
sequence.

## 3. Impact

* **Content impact**: NONE. All apone W19 deliverables are
  on the W19 PR branch — D1 (Android E2E), D2 (us-east-1
  apply readiness), D3 (Kyverno rules), D4 (SignalR
  hardening), D5 (CHANGELOG 0.28.0 + version), D6
  (Argo Rollouts install). Content is byte-identical to
  what Apone wrote.
* **Authorship impact**: All 16 apone-lane files are
  attributed to `Hicks (Frontend) <hicks@squad.mahjong>`
  in `git log`. Apone has zero commits on the W19 PR
  branch (this incident memo will be the first).
* **Lane-discipline impact**: ONE
  `AUTHOR-LANE MISMATCH` violation on the W19 PR branch
  (d700cf7 touches apone-lane but author is hicks). The
  legacy classifier marks most `docs/*.md` paths as
  `shared`, so most of the docs surface isn't formally
  cross-lane — but the YAML / YAML-and-JSON / workflow /
  CHANGELOG cluster IS apone-lane and that's what trips
  the harness.
* **CHANGELOG impact**: The W19 `[0.28.0]` entry attributes
  the wave correctly by deliverable, not by commit
  authorship — the docs surface is sound. Only the git-log
  attribution is wrong.

## 4. Mitigation (this commit)

This memo lands under apone identity (`Apone (DevOps)
<apone@squad.mahjong>`) so the W19 PR branch carries at
least one apone-authored commit referencing every W19
apone-lane deliverable.

It is NOT a "fix" in the strict sense — the d700cf7
bundling violation remains on the branch. Rewriting history
(e.g., `git reset --soft d700cf7^` + re-commit split by
author) was explicitly considered and REJECTED because:

1. The squad's standing directive forbids rewriting any
   commit other than the one currently being authored.
2. d700cf7 contains genuinely-hicks frontend work too
   (renderer-webgl2 wall geometry, lobby lazifications,
   admin UI surfaces). Splitting requires hicks's consent
   + a clean re-apply of both sides — out of scope for
   the apone lane in W19.

## 5. Wave-20 hand-off

| Item                                                                                                  | Owner       |
| ----------------------------------------------------------------------------------------------------- | ----------- |
| Adopt the W18 retro recipe on the hicks lane (explicit-add only, no-pop-before-commit, diff-cached verify) | Hicks W20   |
| Review the W19 retro for this incident; possibly add a `pre-commit` hook in `tests/ci/` that refuses `git add -A` if any other agent has un-pushed staged files | Vasquez W20 |
| Confirm CHANGELOG `[0.28.0]` entry attribution language is sufficient (it credits the deliverables to Apone (DevOps) in the wave-theme prose without relying on git-log authorship) | Apone W20   |
| Update `.squad/agents/apone/agent-handbook.md §3` to add the "double-defence: stash + explicit-add + diff-cached verify" pattern explicitly (currently §3 only documents the explicit-add half) | Apone W20   |

## 6. Validation evidence (re-run post-d700cf7)

* `.work/apone-w19-tools/actionlint .github/workflows/*.yml` → exit 0 (verified against d700cf7's mobile-build.yml).
* `.work/tools/kustomize build infra/k8s/overlays/prod/` → exit 0 (verified against d700cf7's ingress.yaml).
* `.work/tools/kustomize build infra/k8s/overlays/staging/` → exit 0 (same).
* `bash tests/ci/check-cross-lane-bundling.sh --pr stlong/phase-k-wave-19-bringup --strict` → exit 1 (FAIL — d700cf7 author-lane mismatch). After this apone-authored commit lands, the same harness should report 2 commits checked / 1 violation (d700cf7 only) / 0 violations on the apone commit.

## 7. Cross-references

* `.squad/decisions/inbox/apone-phase-k-wave-19.md` — full W19 apone bring-up memo (under d700cf7's tree, Hicks-authored).
* `.squad/decisions/inbox/apone-phase-k-wave-18.md §13` — the W18 commit-time addendum that opened the lane-discipline retro thread.
* `.squad/decisions/inbox/hicks-phase-k-wave-19.md` — Hicks's W19 memo (also under d700cf7 with self-attribution to hicks lane). See its §4 "Lane discipline" paragraph for hicks's own description; it does NOT mention the apone-file inclusion, which suggests the bundling was not noticed at commit time.
* `tests/ci/check-cross-lane-bundling.sh` — the harness that surfaced this incident.
* `tests/ci/lane-map.json` — the apone-lane regex.

End of W19 bundling incident memo.
