# Hicks — Phase K Wave 14 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/hicks/history.md`. The full chronological
> record is the source of truth.

## Phase K Wave 14 — Frontend bring-up

Branch: `stlong/phase-k-wave-14-bringup` (off main
`f0b8e4a`). W13 PR merged into main between waves.

### Deliverables (six)

1. **LH13 hard-pin retry — deferred to W15.**
   Re-ran the W13 hand-off recipe
   (`gh run list -w pwa-audit.yml -L 30 --json
   conclusion,event,createdAt,databaseId`) against the live
   GitHub API.

   - Token provisioning: `gh auth token` empty in the W14
     env. Fallback path (per pwa-audit.md §6.2):
     `git credential fill` against `host=github.com`
     returns the user's 40-char PAT, then
     `GH_TOKEN="$TOKEN" gh ...`.
   - Result: 4 total runs returned. 0 `event=schedule`. 0
     `conclusion=success`. All 4 are PR-trigger failures.
   - Gate: `>=3 successful cron runs`. Predicate fails.
   - Defer to W15. Memo to Vasquez (threshold owner per
     W11 §6.1) + Apone (workflow author per W11 §6) lands
     in `.squad/decisions/inbox/hicks-phase-k-wave-14.md`.
   - Root cause: cron schedule is gated on the W14 §12
     PWA preview-URL provisioning fix landing (Apone's
     lane). Until §12 lands and a `PWA_PREVIEW_URL`
     secret feeds nightly runs, the cron emits no
     Lighthouse score data. The W14 §12 hardening landed
     in Apone W14 (per pwa-audit.md §12 W14 sections);
     nightly should start emitting within a week.
   - Doc: `docs/frontend-pwa-audit.md` §13 (W14 LH13
     status — supersedes the §10 W13 deferral).

2. **Real visual-regression captures.**
   Replaced W13's placeholder PNGs (manifest-icon assets
   sized 320×240) with live lobby-surface captures at
   1280×720. Three surfaces, each at the path the W13
   spec expected:

   - `tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/main-game.png` (97,771 B)
   - `tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/spectator-commentary.png` (105,819 B)
   - `tests/e2e/__screenshots__/manifest-screenshots-visual.spec.ts/tournament-dashboard.png` (82,173 B)

   New script:
   `src/frontend/autotable-src/scripts/capture-real-surfaces.js`
   uses Playwright runtime to capture the three surfaces
   from a vite preview server on port 4173.

   Overlay-suppression sequence (required because W11+ tour
   + W12 magic-link + W12 sign-in modal intercept lobby
   clicks):

   ```js
   // pre-boot
   await context.addInitScript(() => {
     localStorage.setItem('mahjong.tour.completed.v1', 'true');
   });
   // post-boot
   await page.evaluate(() => {
     document.querySelectorAll(
       '#magic-link-landing, #signin-modal, ' +
       '#signin-modal-backdrop, #tour-overlay'
     ).forEach(el => (el.style.display = 'none'));
     document.querySelector('#lobby-panel')?.classList.add('lobby-open');
   });
   await page.click('#lobby-public-games-tab'); // surface-specific
   ```

   Three distinct MD5s confirm the surface-swap actually
   fires. Capture iteration #1 had all three hashes
   identical (tour overlay intercepting clicks); iteration
   #2 had spectator + tournament identical (lobby panel
   was `display: none` by default); iteration #3 landed
   three distinct captures.

   Doc: `docs/frontend-pwa-audit.md` §14 (W14 real
   captures methodology — supersedes the §11 W13
   placeholder methodology).

3. **Phase L renderer-spike feasibility doc.**
   `docs/phase-l-renderer-spike.md` (14 KB, new in W14).

   Sections:

   - §1 — W6→K14 size trend
     (`three-renderer-big`: 737,866 B → 406,635 B,
     −44.9 % cumulative across 8 waves of strips).
   - §2 — WebGL2 hand-roll feasibility (estimated
     180-220 KB chunk vs. the current 406 KB stripped
     three.js).
   - §3 — Risk assessment (matrix of feature parity,
     spec coverage, dev-time estimate).
   - §4 — Go / no-go recommendation: **Go.**
     The W6→K14 trend has flattened (W13's −9.4 % was
     the deepest strip we'll get from PMREMGenerator +
     UniformsLib; W14's 0-byte hold-line is the
     ceiling). Further per-chunk strips give diminishing
     returns. WebGL2 hand-roll has a clear 180-220 KB
     ceiling at half-or-less of the current chunk size.
   - §5 — Alternatives rejected (PixiJS, Babylon.js,
     bare WebGL1 minimal-runtime, three.module
     fork-and-strip).
   - §6 — Cross-references to W6-W13 strip ledgers.

4. **`?action=bracket&tournamentId=<id>` deep-link.**
   New module `src/bracket-listing.ts` (12 KB source,
   6.5 KB lazy chunk). Single export
   `openBracketListing(tournamentId)`:

   - Fetches `GET /api/tournaments/{id}/brackets`
     (Bishop W14).
   - Defensively parses the response: tolerates
     `{ brackets: [...] }`, `{ records: [...] }`, or
     bare `[...]`. Per-record: tolerates `playerA` as
     string OR `{ displayName }`.
   - Renders a rounds-grid overlay (one column per round;
     winner highlight via `.bracket-listing-winner`;
     status badge per match).
   - 404 / 5xx / parse error → in-overlay
     `data-testid="bracket-listing-empty"` placeholder.
   - Selectors documented in `tests/selectors.md` W14
     footer + `docs/frontend-routing.md` §3.2.

5. **`?action=replays` deep-link.**
   New module `src/replays-listing.ts` (8.6 KB source,
   4.7 KB lazy chunk). Single export `openReplaysListing()`:

   - Fetches `GET /api/replays` (Bishop W14).
   - Defensively parses: `{ replays: [...] }`,
     `{ records: [...] }`, or bare `[...]`. Aliases:
     `id` → `replayId`, `completedAtUtc` → `completedAt`.
   - Renders a table; each row links to W12
     `?action=replay&replayId=<id>` (which is already
     wired in action-router).
   - Empty / error → in-overlay `*-empty` placeholder.
   - Selectors documented in selectors.md W14 footer +
     `docs/frontend-routing.md` §3.3.

6. **`?action=admin-cost` deep-link.**
   New module `src/admin-cost.ts` (11 KB source, 5.97 KB
   lazy chunk). Single export `openCommentaryCostPanel()`:

   - Pre-flights `GET /api/auth/me` — if
     `authenticated !== true` → `redirectToLobbyForSignIn()`
     (mirrors the W13 spectator-handoff 401 path).
   - Fetches `GET /api/commentary/cost/summary` (Bishop
     W14, admin-gated).
   - Defensively parses: tolerates `percentUsed` as
     either `0-100` integer or `0-1` fractional;
     normalises by `value > 1 ? value : value * 100`.
   - Renders summary card
     (`$current / $cap (N%)`) + per-model table sorted
     by `costUsd` desc.
   - Percent class: `admin-cost-pct-ok` / `-warn` /
     `-critical` based on `<80` / `80-94` / `>=95`.
   - 401 → redirect (defensive backstop after the
     pre-flight). 403 → in-overlay "Admins only"
     placeholder. 404 / 5xx / parse → "unavailable"
     placeholder.
   - Selectors documented in selectors.md W14 footer +
     `docs/frontend-routing.md` §3.4.

### Action-router extension

`src/action-router.ts`:

- `SUPPORTED_ACTIONS` now includes `bracket`, `replays`,
  `admin-cost` alongside the W11-W13 keywords
  (`new-game`, `spectate`, `tournament`/`tournaments`,
  `replay`).
- New helpers: `dispatchBracket()`, `mountBracketListing()`,
  `showTournamentNotFoundToast()`, `dispatchReplays()`,
  `mountReplaysListing()`, `dispatchAdminCost()`,
  `gateAndMountAdminCost()`.
- Switch cases added to `handlePwaActionFromUrl()`.
- Top doc-comment refreshed to inventory the W14 keywords.

### Build state

`WAVE_NAME=K14 npm run build:vite` — exit 0.

| Chunk | W13 (K13) | W14 (K14) | Δ |
|-------|-----------|-----------|---|
| `three-renderer-big` | 406,635 | **406,635** | **+0** (hold-line met) |
| `autotable-src-eager` | 219,528 | 221,745 | +2,217 (action-router extensions) |
| `bracket-listing-*` | — | 6,500 | new |
| `replays-listing-*` | — | 4,700 | new |
| `admin-cost-*` | — | 5,970 | new |

`dist-size.json` K14 row recorded (19 chunks).
`scripts/append-dist-size.js` `KEY_PATTERNS` extended to
record the three new chunks.

`npx tsc --noEmit` exit 0 (pre-existing test-only errors in
`tests/e2e/{onboarding-server-cookie,reduced-motion,
tournament-admin-bracket}.spec.ts` are noise; they don't
trip the noEmit gate).

### Wave hand-offs (W15)

1. **LH13 hard-pin retry (third attempt).** Vasquez /
   Apone notified via inbox memo. Re-run
   `gh run list -w pwa-audit.yml -L 30` against the W14
   base once the §12 preview-URL provisioning has been
   live for a week. If `>=3 schedule/success` rows
   exist, compute p95 and update
   `.github/workflows/pwa-audit.yml` `assertions:` block.

2. **Visual-regression spec fix (still Vasquez W15).**
   Two outstanding items from W13 §11.5 hand-off:
   - Prefix per-station block in
     `manifest-screenshots-visual.spec.ts` with
     `await page.goto(BASE_URL)`.
   - Configure `snapshotPathTemplate` in
     `playwright.config.ts` so the spec reads from the
     same `__screenshots__/` path the W14 capture script
     writes to.
   Until both land, the W14 spec will silent-no-op
   against the W14 baselines (same root cause as W13).

3. **Bishop W14 wire-shape reconcile (potential W15).**
   The W14 frontend defensively parses Bishop's wire
   shape per the W14 charter spec. If Bishop's actual
   W14 endpoints emit a different shape, the W14
   overlays graceful-degrade to their `*-empty`
   placeholders. A W15 contract reconcile may be needed
   to assert pixel-perfect parity between Bishop's wire
   shape and Hicks's parser. Memo flags this.

4. **Phase L renderer-spike Go.** The spike doc
   recommends **Go** on WebGL2 hand-roll. Phase L
   charter (whenever it lands) should pick up the
   WebGL2 implementation as the headline deliverable.
   Estimated chunk size: 180-220 KB (vs. current
   406 KB). Doc: `docs/phase-l-renderer-spike.md`.

### Identity hardening

- Author: `Hicks (Frontend) <hicks@squad.mahjong>`.
- Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`.
- Commit flock-wrapped via `.work/squad-git-lock`.
- 9th consecutive clean wave on the per-commit identity
  pattern (W6 → W14).
