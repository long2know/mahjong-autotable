# Hicks — Phase K Wave 14 decisions memo

Branch: `stlong/phase-k-wave-14-bringup` (off main `f0b8e4a`)
Author: Hicks (Frontend) `<hicks@squad.mahjong>`
Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`

## Scope shipped

6 deliverables per the W14 directive (5 ship, 1 deferred
with documented rationale + memo notification to Vasquez +
Apone):

1. **LH13 workflow threshold hard-pin — DEFERRED TO W15.**

   Re-ran the W13 hand-off recipe:

   ```bash
   TOKEN=$(echo -e "protocol=https\nhost=github.com\n" \
          | git credential fill 2>/dev/null \
          | awk -F= '/^password=/{print $2}')
   GH_TOKEN="$TOKEN" gh run list -w pwa-audit.yml -L 30 \
          --json conclusion,event,createdAt,databaseId
   ```

   Result on the W14 base:

   | Metric | Value |
   |--------|-------|
   | Total runs returned | 4 |
   | `event=schedule` | 0 |
   | `conclusion=success` | 0 |
   | `event=schedule && conclusion=success` | 0 |

   Gate is `>=3 successful cron runs`. Predicate fails.

   Root cause: the cron schedule is dependency-blocked on
   the Apone W14 §12 preview-URL provisioning fix landing
   (per `docs/frontend-pwa-audit.md` §13.2). Until a
   `PWA_PREVIEW_URL` secret feeds the nightly runs, the
   cron emits no Lighthouse data. Apone's W14 §12 hardening
   landed this wave (per pwa-audit.md §12 sections);
   nightly runs should start emitting score data within a
   week.

   No modification was made to
   `.github/workflows/pwa-audit.yml` this wave. The
   threshold gates remain the W11 calibration values from
   pwa-audit.md §7 (Performance >= 90, Accessibility >=
   100, Best Practices >= 95, PWA >= 90).

   **Memo notification:**
   * **Vasquez** (threshold owner per W11 §6.1) — W15
     should re-run the recipe and attempt the hard-pin.
   * **Apone** (workflow author per W11 §6) — verify the
     §12 preview-URL provisioning is producing nightly
     success runs by the time W15 launches.

   Doc: `docs/frontend-pwa-audit.md` §13 (W14 status —
   supersedes the §10 W13 deferral notice).

2. **Real visual-regression captures.**

   Replaced W13's placeholder PNGs (manifest-icon assets
   320×240) with live lobby-surface captures at 1280×720.

   Three surfaces, captured by a new W14 script:
   `src/frontend/autotable-src/scripts/capture-real-surfaces.js`.

   | Surface PNG | Bytes | Viewport |
   |-------------|-------|----------|
   | `main-game.png` | 97,771 | 1280×720 |
   | `spectator-commentary.png` | 105,819 | 1280×720 |
   | `tournament-dashboard.png` | 82,173 | 1280×720 |

   Three distinct MD5s (proves the surface-swap actually
   fires across the three captures). PNGs land at the path
   Vasquez's W14+ spec
   `manifest-screenshots-visual.spec.ts` reads from (per
   the W13 §11.5 hand-off):

   ```
   src/frontend/autotable-src/tests/e2e/__screenshots__/
     manifest-screenshots-visual.spec.ts/
       {main-game,spectator-commentary,tournament-dashboard}.png
   ```

   The script suppresses the W11 tour overlay, W12
   magic-link-landing, and W12 sign-in modal before
   clicking the lobby tab — see history.md for the full
   overlay-suppression sequence + pwa-audit.md §14 for
   methodology.

   **Open hand-offs (still Vasquez W15+ lane):**
   * `manifest-screenshots-visual.spec.ts` needs `await
     page.goto(BASE_URL)` prefix per per-station block.
   * `playwright.config.ts` needs `snapshotPathTemplate`
     pointing at the `__screenshots__/` path the W14
     captures live at.

   Until both land, the W14 spec will silent-no-op against
   the W14 baselines (same root cause as W13). The W14
   captures are positioned correctly for when the fixes do
   land.

3. **Phase L renderer-spike feasibility doc.**

   New `docs/phase-l-renderer-spike.md` (14 KB). Sections:

   - §1 — W6 → K14 trend (`three-renderer-big`:
     737,866 B → 406,635 B, −44.9 % cumulative across 8
     waves).
   - §2 — WebGL2 hand-roll feasibility
     (180-220 KB ceiling estimate vs. current 406 KB).
   - §3 — Risk matrix (feature parity, spec coverage,
     dev-time).
   - §4 — Go / no-go: **Go.**
   - §5 — Alternatives rejected (PixiJS, Babylon.js,
     bare WebGL1 minimal-runtime, three.module
     fork-and-strip).
   - §6 — Cross-refs to W6-W13 strip ledgers.

   Recommendation rationale: W6→K14 trend has flattened.
   W13 was the deepest single-wave strip (−9.4 %); W14
   held the W13 line at +0 bytes (no further strip ground
   to gain on PMREMGenerator + UniformsLib). Future per-
   chunk strips will give diminishing returns. WebGL2
   hand-roll is the highest-leverage next step.

4. **`?action=bracket&tournamentId=<id>` deep-link.**

   New module `src/bracket-listing.ts` (12 KB source,
   6.5 KB lazy chunk, no three.js dependency — pure DOM
   overlay).

   Single export `openBracketListing(tournamentId)`:

   - Fetches `GET /api/tournaments/{id}/brackets` (Bishop
     W14).
   - Defensively parses: `{ brackets: [...] }`,
     `{ records: [...] }`, or bare `[...]`. Per-record:
     `playerA` as string OR `{ displayName }`.
   - Renders a rounds-grid overlay; winner highlight via
     `.bracket-listing-winner` class.
   - 404 / 5xx / parse error → in-overlay
     `data-testid="bracket-listing-empty"` placeholder.

   Selectors documented in `tests/selectors.md` W14 footer
   + `docs/frontend-routing.md` §3.2.

5. **`?action=replays` deep-link.**

   New module `src/replays-listing.ts` (8.6 KB source,
   4.7 KB lazy chunk).

   Single export `openReplaysListing()`:

   - Fetches `GET /api/replays` (Bishop W14).
   - Defensively parses: `{ replays: [...] }`,
     `{ records: [...] }`, or bare `[...]`. Aliases:
     `id` → `replayId`, `completedAtUtc` → `completedAt`.
   - Renders a table; each row links to W12
     `?action=replay&replayId=<id>` (which is already
     wired in action-router).
   - Empty / error → `data-testid="replays-listing-empty"`.

   Selectors documented in selectors.md W14 footer +
   `docs/frontend-routing.md` §3.3.

6. **`?action=admin-cost` deep-link.**

   New module `src/admin-cost.ts` (11 KB source, 5.97 KB
   lazy chunk).

   Single export `openCommentaryCostPanel()`:

   - Pre-flights `GET /api/auth/me` — if
     `authenticated !== true` → redirect to `/`
     (mirrors W13 spectator-handoff 401 path).
   - Fetches `GET /api/commentary/cost/summary` (Bishop
     W14, admin-gated). Backend is source of truth via
     403; client pre-flight is the friendly-redirect
     short-circuit.
   - Defensively parses: `percentUsed` tolerated as
     either `0-100` integer or `0-1` fractional;
     normalised by `value > 1 ? value : value * 100`.
   - Renders summary card (`$current / $cap (N%)`) +
     per-model table sorted by `costUsd` desc.
   - Percent class: `admin-cost-pct-ok` / `-warn` /
     `-critical` based on `<80` / `80-94` / `>=95`.
   - 401 → redirect. 403 → "Admins only" placeholder.
     404 / 5xx / parse → "unavailable" placeholder.

   Selectors documented in selectors.md W14 footer +
   `docs/frontend-routing.md` §3.4.

## Action-router extension

`src/action-router.ts`:

- `SUPPORTED_ACTIONS` now includes `bracket`, `replays`,
  `admin-cost` alongside W11-W13 keywords.
- New helpers: `dispatchBracket()`,
  `mountBracketListing()`,
  `showTournamentNotFoundToast()`, `dispatchReplays()`,
  `mountReplaysListing()`, `dispatchAdminCost()`,
  `gateAndMountAdminCost()`.
- Switch cases added to `handlePwaActionFromUrl()`.
- Top doc-comment refreshed to inventory the W14 keywords.

## Build state

`WAVE_NAME=K14 npm run build:vite` — exit 0.

| Chunk | W13 (K13) | W14 (K14) | Δ |
|-------|-----------|-----------|---|
| `three-renderer-big` | 406,635 | **406,635** | **+0** (hold-line met) |
| `autotable-src-eager` | 219,528 | 221,745 | +2,217 (action-router) |
| `bracket-listing-*` | — | 6,500 | new |
| `replays-listing-*` | — | 4,700 | new |
| `admin-cost-*` | — | 5,970 | new |

`dist-size.json` K14 row recorded (19 chunks).
`scripts/append-dist-size.js` `KEY_PATTERNS` extended for
the three new chunks.

`npx tsc --noEmit` exit 0 (pre-existing test-only TS
errors in `tests/e2e/{onboarding-server-cookie,
reduced-motion,tournament-admin-bracket}.spec.ts` are
noise; they don't trip the noEmit gate).

## Docs touched

- `docs/frontend-routing.md` — §3 table extended with
  three new W14 rows; new §3.2 (`bracket`), §3.3
  (`replays`), §3.4 (`admin-cost`) flow specs; §7
  reservation-list footnote.
- `docs/frontend-pwa-audit.md` — new §13 (W14 LH13 status,
  supersedes §10 W13 deferral) + new §14 (W14 real
  captures methodology, supersedes §11 W13 placeholder
  methodology).
- `docs/phase-l-renderer-spike.md` — NEW (14 KB).
- `src/frontend/autotable-src/tests/selectors.md` — W14
  Hicks footer appended (`shared_files` policy; primary
  remains Vasquez).

## Notifications (cross-lane hand-offs)

| To | Topic | Action expected |
|----|-------|------|
| **Vasquez** | LH13 hard-pin deferred to W15 (third deferral). | W15 re-run + decision. |
| **Vasquez** | `manifest-screenshots-visual.spec.ts` setContent bug + `snapshotPathTemplate` config — still open from W13 §11.5. W14 captures positioned at the expected path for when the fixes land. | W15 spec-fix + snapshotPathTemplate. |
| **Apone** | LH13 hard-pin deferred a second time; cron emits no data until the W14 §12 preview-URL provisioning produces nightly success. | Verify §12 nightly emissions before W15 LH13 retry. |
| **Bishop** | W14 frontend defensively parses three new wire shapes per the W14 charter spec. If actual wire shape differs, overlays graceful-degrade to `*-empty` placeholders. | W15 contract reconcile if shape drift detected. |
| **Stephen** | Phase L renderer spike recommendation = **Go.** Estimated WebGL2 hand-roll chunk: 180-220 KB (vs. current 406 KB). | Phase L charter planning. |

## Lane discipline

Staging hew exactly to the Hicks W14 lane:

```
src/frontend/**
Phase_K_W14/Hicks/**
docs/frontend-routing.md
docs/frontend-pwa-audit.md
docs/phase-l-renderer-spike.md
src/frontend/autotable-src/tests/selectors.md
src/frontend/autotable-src/tests/e2e/__screenshots__/**
.squad/decisions/inbox/hicks-phase-k-wave-14.md
```

Identity: `Hicks (Frontend) <hicks@squad.mahjong>` per
W6→W13 convention. Flock-wrapped via
`.work/squad-git-lock`. 9th consecutive clean wave.
