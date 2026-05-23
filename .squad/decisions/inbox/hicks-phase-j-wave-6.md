# Hicks — Phase J Wave 6: auth bootstrap + leaderboard + Playwright suites

> Author: Hicks (Senior Frontend Engineer)
> Branch: `stlong/phase-j-wave-6-completion`
> Commit: `447bacc`
> Scope: Frontend (Wave 6 §Tasks 1 + 2 + 3) — auth/identity bootstrap UI,
> leaderboard surface, three Playwright E2E specs.  Lands on top of
> Bishop's identity + leaderboard backend (`21515fe` + `81beb15` +
> Vasquez's `4bd9e53` tests) and Apone's DevOps gates (`408e0d1` +
> `c3289eb`).

## Summary

Wave 6 wires Bishop's two new REST surfaces — `POST /api/identity` for
persistent player IDs and `GET /api/leaderboard` for the career-stat
board — into the frontend, adds a first-visit onboarding card so
returning visitors keep their profile across reloads, and pays down our
biggest E2E debt with three deterministic Playwright specs covering
the replay surface, sound toggle persistence, and the lobby onboarding
flow.

1.  **Auth bootstrap (`identity.ts`, ~535 lines).**
    `bootstrapIdentity()` POSTs `/api/identity` on every page load to
    refresh the HttpOnly `mahjong_pid` cookie (the cookie sniff
    intentionally returns `null` in JS — that's why we mirror the
    resolved DTO to `localStorage` under `mahjong.identity.cache.v1`
    for offline fallback).  `shouldShowOnboarding()` gates a one-time
    onboarding card on the lobby for first-time visitors (LS flag
    `mahjong.identity.onboarded.v1`).  Continue routes through
    `applyProfileFromOnboarding()` which forces a hub connection,
    polls `getProfile()` up to 2 s, then calls
    `setDisplayName`/`setAvatarColor` so the chip surfaces the new
    identity immediately and the existing debounce-send pipeline
    persists it server-side.

2.  **Leaderboard surface (`leaderboard.ts`, ~543 lines).**
    Five sort axes — `gamesWon` (default) | `totalScore` | `winRate` |
    `longestStreak` | `highestScore` — paged via `limit + offset`,
    filtered by `minGames`, with a 30 s auto-refresh polling loop
    gated by the Page Visibility API so a backgrounded tab doesn't
    hammer the endpoint.  Verbose row fields are normalised at the
    boundary (`highestSingleGameScore → highestScore`,
    `longestWinStreak → longestStreak`) so the rest of the UI stays
    in our compact vocabulary.  When the endpoint 404s / 500s the
    pane shows an inline error row — no crashes when the backend
    half hasn't shipped yet.

3.  **Lobby integration (`lobby.ts`).**  A 3rd tab — Leaderboard —
    joins My Games + Public Games (the existing tab strip already
    delegates on `data-lobby-tab`).
    `installSoundEnabledMirror()` keeps `mahjong:soundEnabled` and
    the settings-drawer Sound checkbox in lock-step — LS is the
    canonical store so the E2E spec can flip it without poking at
    hidden DOM.  `hydrateProfileFromCacheIfAvailable()` runs at
    init so returning visitors see their saved name on the chip
    before any hub connect (the hub only connects when the user
    enters a game, so without this the chip stays at the HTML
    default "Profile" forever in the lobby).

4.  **Profile chip hydration (`profile.ts`).**  Single new public
    export — `hydrateProfileFromCacheIfAvailable()`.  Idempotent
    (bails when `current !== null`).  Routes through the private
    `setCurrent(loadCache())` so the chip's `onProfile` listeners
    fire synchronously before any wire traffic.

5.  **HTML + CSS.**  Onboarding card markup + leaderboard pane +
    testids on the settings-drawer + replay surface.  ~330 lines of
    CSS — `.onboarding-card`, `.lb-grid`, `.lb-avatar`,
    `.leaderboard-footer` plus a 768 px mobile breakpoint that
    collapses the leaderboard table into vertical stacks.

6.  **Playwright E2E (3 specs, `chromium` project — `test.skip()`
    inside each test gates on `testInfo.project.name`).**

    -  `replay.spec.ts` — pushes a synthetic `gameComplete` entry into
       the live `client.gameComplete` collection via `page.evaluate`.
       `Collection.set()` emits locally when `client.connected()` is
       false (which is the case before any `?gameId=` is on the URL),
       so this triggers the real `game-ui.ts:setupGameCompleteModal`
       click handler — exercising the production code path without
       having to race a real 4-bot game through to completion (which
       took 90 s+ and was hopelessly flaky).  Clicks the modal Replay
       button, asserts `[data-testid="replay-screen"]` flips visible,
       drives play / step-fwd / step-back through their on-click
       branches, and verifies the timeline label format stays sane
       (`"Move N / M"` OR `"No moves recorded"` depending on whether
       the synthetic history seeded moves).

    -  `sound-toggle.spec.ts` — opens the settings drawer, flips the
       `settings-sound` checkbox twice, asserts `mahjong:soundEnabled`
       flips `'1' ↔ '0'` in LS and persists across reload.

    -  `lobby-flow.spec.ts` — first-visit onboarding lifecycle:
       cleared storageState → card visible → fill name + pick
       avatar → Continue → card hidden → LS flag set → chip
       surfaces the new name; reload → card stays hidden, chip
       stays populated.

## Wire contract (verified against `21515fe` + `81beb15`)

-  `POST /api/identity` (REST) →
   ```json
   { "playerId": "9b3a…",
     "displayName": "Player-AB12CD",
     "avatarColor": "#1E88E5",
     "createdAt": "2026-05-…",
     "lastSeenAt": "2026-05-…" }
   ```
   No `isNewProfile` flag — frontend uses the LS `onboarded` flag
   as the authoritative "first visit" signal.  Cookie
   `mahjong_pid` is HttpOnly, so `document.cookie` returns null for
   it; that's by design.

-  `GET /api/leaderboard?sort&limit&offset&minGames` →
   ```json
   { "total": 142,
     "rows": [ { "rank": 1, "playerId": "9b3a…", "displayName": "Bishop",
                 "avatarColor": "#1E88E5", "gamesPlayed": 87,
                 "gamesWon": 42, "winRate": 0.4827586,
                 "totalScore": 1240,
                 "highestSingleGameScore": 96,
                 "longestWinStreak": 7 } ] }
   ```
   Defaults: `limit=50` (max 100, silently clamped), `offset=0`,
   `minGames=5`, `sort=gamesWon`.  Unknown sort values fall back
   to `gamesWon`.

## Testids added (frontend selector contract)

-  Onboarding card: `onboarding-card`, `onboarding-display-name`,
   `onboarding-avatar-preset`, `onboarding-avatar-input`,
   `onboarding-continue`, `onboarding-skip`.
-  Leaderboard pane: `lobby-leaderboard-tab`, `leaderboard-table`,
   `leaderboard-row`, `leaderboard-sort`, `leaderboard-prev`,
   `leaderboard-next`, `leaderboard-refresh`, `leaderboard-error`,
   `leaderboard-empty`.
-  Settings + replay (pre-existing surfaces that needed testids for
   the new specs): `settings-sound`, `game-complete-replay`,
   `replay-screen`, `replay-play`, `replay-step-back`,
   `replay-step-fwd`, `replay-close`.

## Gates

-  **TS strict** (`npx tsc --noEmit --strict --target es6 …
   src/index.ts`) → 0 errors.
-  **Parcel build** → `autotable-src.2391eb20.js` +
   `autotable-src.6633d8fb.css` (the two pre-existing split chunks
   `094cde3a.css` + `df85b4c4.css` were unchanged so Parcel
   re-emitted identical bytes — same hash).
-  **Backend `dotnet test`** (`src/backend/Mahjong.Autotable.slnx`) →
   **456 / 0 / 0** (was 445/0/0 at HEAD before Bishop's WIP became
   real commits — his 21515fe + Vasquez's 4bd9e53 added the +11).
-  **Docker** — `mahjong-autotable:wave6` builds clean; live smoke
   confirms `/health = 200`, `POST /api/identity` returns a minted
   profile, `GET /api/leaderboard?limit=5&minGames=0` returns the
   expected `{total, rows[]}` shape.
-  **Playwright full suite** (chromium + mobile-chrome projects) →
   10 passed, 4 properly skipped (project-scoped — replay /
   sound-toggle / lobby-flow are desktop-only on first pass,
   `mobile-drawer-toggle` is mobile-only).

## Decisions / notes for the next wave

-  **Onboarding gate is LS-driven, not cookie-driven.**  The
   `mahjong_pid` cookie is HttpOnly so we can't sniff its presence
   from JS to decide whether to show the card.  LS flag
   `mahjong.identity.onboarded.v1` is the source of truth.  Clearing
   localStorage re-shows the card on next visit, which matches what a
   support-desk "reset onboarding" toggle would do.
-  **Profile chip hydration must happen at *lobby init*, not on hub
   connect.**  `profile.ts:installProfileLoadedListener()` only wires
   the SignalR `ProfileLoaded` handler once `hubIsConnected()` is
   true, and the hub only connects when entering a game.  Without
   `hydrateProfileFromCacheIfAvailable()` returning visitors saw the
   default "Profile" until they joined a match.  See `profile.ts:~226`.
-  **`UpdateProfile` RPC does NOT re-broadcast `ProfileLoaded`.**  It
   returns the DTO as RPC response only.  External callers can't use
   `setCurrent` (it's private), so `applyProfileFromOnboarding()`
   routes through `setDisplayName`/`setAvatarColor` — which require
   `current !== null` to do anything, hence the polling wait on
   `getProfile()`.
-  **Replay spec deliberately doesn't race a real game.**  A
   4-bot Easy hand with `handCount=1` + `seed=42` takes 90 s+ and
   still doesn't always reach the game-complete modal in 150 s.
   Synthesising the gameComplete entry through the real Collection
   path exercises the genuine click handler and replay surface
   wiring — and runs in <2 s.  When Bishop ships per-move history on
   the wire we can revisit and assert against real move data.
-  **Stale Parcel artifacts.**  `093…` and `df8…` CSS bundles in the
   working tree are split-chunks Parcel re-emits identical-byte every
   build (vendor + bootstrap CSS — they only change when those upstream
   deps move).  Don't `git rm` them — leave them alongside the main
   bundle so `index.html` references stay valid.

## What I did NOT do

-  Did not modify Bishop's services, controllers, or hub.  His Wave 6
   backend was uncommitted local WIP when I started — by the time I
   finished my changes his work had landed as `21515fe` + `81beb15` +
   `4bd9e53` so the frontend now talks to real, tested endpoints
   instead of speculation.
-  Did not touch Apone's DevOps workflows (`.github/workflows/squad-*`,
   `.tool-actionlint/`, `.copilot/skills/error-recovery/`) — those are
   untracked in his lane and out of my scope.
-  Did not modify `playwright.config.ts` — the existing `chromium` +
   `mobile-chrome` project split is fine.  Project-scoped skips inside
   each spec do the gate.

## File inventory

NEW:
-  `src/frontend/autotable-src/src/identity.ts` (535 lines)
-  `src/frontend/autotable-src/src/leaderboard.ts` (543 lines)
-  `src/frontend/autotable-src/tests/e2e/replay.spec.ts` (181 lines)
-  `src/frontend/autotable-src/tests/e2e/sound-toggle.spec.ts` (94 lines)
-  `src/frontend/autotable-src/tests/e2e/lobby-flow.spec.ts` (108 lines)

MODIFIED:
-  `src/frontend/autotable-src/index.html` (+143 lines: onboarding
   card + leaderboard pane + testids)
-  `src/frontend/autotable-src/src/main.css` (+330 lines)
-  `src/frontend/autotable-src/src/lobby.ts` (+132 lines: tab + sound
   mirror + identity + leaderboard wiring + profile hydration)
-  `src/frontend/autotable-src/src/profile.ts` (+19 lines: new
   `hydrateProfileFromCacheIfAvailable()` export)
-  `src/frontend/autotable-src/.gitignore` (+2 lines: `test-results/`,
   `playwright-report/`)
-  `src/frontend/autotable-src/index.html` (Parcel-emitted bundle
   refs)
-  `src/frontend/autotable/index.html` (Parcel-emitted, generated)

DIST (Parcel-emitted, generated):
-  `src/frontend/autotable/autotable-src.2391eb20.js` (new hash)
-  `src/frontend/autotable/autotable-src.6633d8fb.css` (new hash)
-  Pre-existing split chunks (`094cde3a.css`, `df85b4c4.css`)
   unchanged.

— Hicks
