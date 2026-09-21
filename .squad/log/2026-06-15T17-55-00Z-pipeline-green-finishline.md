# 2026-06-15T17-55-00Z — pipeline-green + finish-line session log

**Wave name:** pipeline-green + finish-line (model→opus-4.8; PRs #101–#105)
**Scope:** Bump every agent default to `claude-opus-4.8` (max + long_context/1M), then drive the build pipelines green and the game over the finish line.
**Mode:** background, parallel agents.

## Model default change
- `6abfc7c` (Coordinator) — `config.json` + 3 charters now default to **claude-opus-4.8, max effort, long_context (1M)**. Supersedes the 2026-05-22 `opus-4.7-xhigh` default.

## Merged PRs

| Agent | PR | Commit | Tests | Status |
|-------|-----|--------|-------|--------|
| Apone | #101 | `9435742` | drift 10→0, CI green | ✅ Merged |
| Apone | #102 | `7a399b5` | scanner catches inline-list, CI green | ✅ Merged |
| Ferro | #103 | `dce81a3` | 3 specs / 5 runs green | ✅ Merged |
| Hicks | #104 | `d92bab2` | 11/11 green | ✅ Merged |
| Vasquez | #105 | `53d61b0` | 242 passed / 0 failed | ✅ Merged |

## Verdict — ✅ PLAYABLE under Production CSP
Vasquez verified against `d92bab2` on BOTH a dotnet Production backend (:8093, `CspStrictStyles=true`) and the Docker gold-standard image `mat-verify:d92bab2` (:8094). Strict `style-src 'self'` (no `'unsafe-inline'`); lobby CSP-violation count = 0; 3/3 end-to-end playtests `gameCompleted=true`. **Full e2e suite vs prod-CSP backend: 242 passed / 0 failed / 234 skipped.**

## Final pipeline state (on `d92bab2`)
- e2e-playwright ✅ · slsa-drift-detection ✅ · multi-arch-smoke ✅ · pre-commit ✅ · sign-image ✅

## Key discoveries (full write-up in decisions.md)
1. **Dev-vs-Prod CSP divergence (MAJOR):** `appsettings.Production.json` `CspStrictStyles=true` strips `'unsafe-inline'` → inline styles work in Dev but are blocked in the deployed Docker container. Repro Prod-only failures against a Production-CSP backend, not Dev.
2. **CSP-cascade hypothesis was FALSE:** only 1 of 11 "cascade" tests was CSP-caused; the other 10 were independent pre-existing bugs.
3. **slsa-drift scanner blind spot:** regex missed the inline-list `- uses:` form (12 unpinned refs invisible) → pin all + broaden regex.
4. **CSSOM vs inline-style under CSP:** `el.style.*` (CSSOM) is not subject to `style-src`; `<style>`/`setAttribute('style')`/`innerHTML` style attrs ARE blocked.
5. **Static dist serving:** backend serves `ContentRoot/../../../frontend/autotable`, so each worktree's `dotnet run` serves its own dist — parallel Production-CSP backends (:8091/8092/8093) need no rsync-to-main.

## Full write-up
See `.squad/decisions.md` §**"2026-06-15 — pipeline-green + finish-line (model→opus-4.8; PRs #101–#105)"** for per-agent reports + the 5 DISCOVERIES.
