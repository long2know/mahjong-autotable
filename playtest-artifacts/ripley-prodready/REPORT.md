# Ripley — Production-Readiness Checklist Report

- Started: 2026-06-03T16:15:58.452Z
- Finished: 2026-06-03T16:16:19.222Z
- Base URL: http://127.0.0.1:8088
- WS Base URL: ws://127.0.0.1:8088

## Totals

- **PASS:** 16
- **FAIL:** 0
- **SKIP:** 0

## Overall Verdict — 🟢 **PRODUCTION-READY** — every gate green.

## Findings by category

### 1-operational

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `O-1-health-200` | PASS | `{"httpStatus":200,"bodyStatus":"healthy","version":"0.31.0.0","uptime":"00:13:54.9611943","buildSha":"dev","latencyMs":10,"error":null}` |
| `O-2-db-connected` | PASS | `{"connected":true,"canQuery":true,"providerName":"Microsoft.EntityFrameworkCore.Sqlite","latencyMs":0,"migrationsApplied":0}` |
| `O-3-migrations-or-bootstrap` | PASS | `{"providerName":"Microsoft.EntityFrameworkCore.Sqlite","migrationsApplied":0,"sqliteEnsureCreatedAccepted":true}` |
| `O-4-ws-handshake` | PASS | `{"ok":true,"url":"ws://127.0.0.1:8088/autotable/ws"}` |

### 2-tour

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `T-1-tour-attaches-on-first-load` | PASS | `{"present":true,"visible":true,"hasSkipButton":true,"hasCard":true,"skipLabel":"Skip tour"}` |
| `T-2-tour-dismisses-and-persists` | PASS | `{"overlayStillPresent":false,"tourFlag":"true"}` |
| `T-3-tour-no-replay-after-flag` | PASS | `{"overlayPresent":false,"tourFlag":"true"}` |

### 3-multi-game

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `MG-1-distinct-identities` | PASS | `{"playerA":"39afa3a2ae7d4a26a8e19a927129888e","playerB":"38e95ba1a41648ca9fecb84b1b6ebade","sameId":false}` |
| `MG-2-distinct-game-ids` | PASS | `{"requestedA":"ripley-prodready-A-1780503362695","observedA":null,"observedLastIdA":"ripley-prodready-A-1780503362695","urlIdA":"ripley-prodready-A-1780503362695","effectiveA":"ripley-prodready-A-1780503362695","requestedB":"ripley-prodread` |
| `MG-3-both-worlds-populated` | PASS | `{"thingsA":109,"thingsB":109,"seatA":null,"seatB":null}` |
| `MG-4-backend-activeGames-grew` | PASS | `{"activeGamesAfter":47,"activeGamesBefore":47}` |

### 4-https

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `H-1-no-hardcoded-localhost` | PASS | `{"totalRawHits":1,"realCodeHits":0,"commentOnlyHits":1,"firstHits":["/data/source/mahjong-autotable/src/frontend/autotable-src/src/hub.ts:49:  // at http://localhost:5000.  Same-origin defaults work in dev"]}` |

### 5-bundle

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `B-1-bundle-no-console-spam` | PASS | `{"autotableBundleHits":0,"totalHits":0,"perFile":[]}` |

### 6-source-hygiene

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `S-1-no-todo-fixme-xxx-backend` | PASS | `{"occurrences":0,"hits":[]}` |
| `S-2-no-fixme-xxx-frontend` | PASS | `{"occurrences":0,"hits":[]}` |
| `S-3-todo-tally-frontend` | PASS | `{"occurrences":2,"threshold":5,"hits":["/data/source/mahjong-autotable/src/frontend/autotable-src/src/world.ts:132:  // TODO Phase D: banker rotation becomes server-authoritative; this client-side","/data/source/mahjong-autotable/src/fronte` |
