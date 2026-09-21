# Session Log — Phase G Wave

**Date:** 2026-05-20T20-30-58Z
**Wave:** Phase G — Bot pickup scheduler + sidebar lobby + privacy-mask cleanup
**Participants:** Bishop (backend), Hicks (frontend), Vasquez (tests)

## Three commits shipped

| Commit | Agent | What |
|--------|-------|------|
| 99d0e66 | Hicks | feat(frontend): Phase G — sidebar lobby UI for variant/dealMode/botCount/botDifficulty |
| efbbddc | Vasquez | test(backend): Phase G — bot pickup scheduler + privacy mask acceptance tests |
| a22f10c | Bishop | feat(backend): Phase G — bot pickup tick scheduler + privacy mask slot-parse fix |

## Test gate

`dotnet test src/backend/Mahjong.Autotable.slnx --nologo` → **330 passed / 0 failed / 9 skipped / 339 total** in ~15s.
Full suite: Phase F baseline (319/0/9) + Phase G additions (11 new facts) all green. No flakes across 3 consecutive runs.

## Bundle transition

- **Before:** `autotable-src.6d5fae4c.js` + `autotable-src.1c6f6789.css`
- **After:** `autotable-src.33f97fad.js` + `autotable-src.7934372e.css`
- **Stale files pruned:** `6d5fae4c.js`, `1c6f6789.css`

## Decisions

Three decision memos written to `.squad/decisions/inbox/`:
- `bishop-phase-g-backend.md` — bot pickup scheduler contract + privacy-mask fix rationale
- `hicks-phase-g-frontend.md` — lobby UI design + query-param mapping
- `vasquez-phase-g-tests.md` — acceptance test contracts + reflection-safe test design

Branch: `stlong/phase-g-bot-scheduler-lobby`, 3 commits ahead of `origin/main` @ `1e9134a`. All pushed.
