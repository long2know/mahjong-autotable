# Hicks — Phase K Wave 15 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/hicks/history.md`.  The full chronological
> record is the source of truth.

## Phase K Wave 15 — Frontend bring-up

Branch: `stlong/phase-k-wave-15-bringup` (off main
`e6fef84`).  W14 PR merged into main between waves.

### Deliverables (five)

1. **LH13 hard-pin THIRD retry — deferred to W16.**
   Re-ran the W13/W14 hand-off recipe
   (`gh run list -w pwa-audit.yml -L 30 --json
   conclusion,event,createdAt`) against the live GitHub
   API on the W15 branch.

   - Token provisioning: `gh auth token` empty in the W15
     env (same as W14).  Fallback path
     (`docs/frontend-pwa-audit.md §6.2`):

     ```bash
     TOKEN=$(echo -e "protocol=https\nhost=github.com\n" \
       | git credential fill \
       | awk -F= '/^password=/{print $2}')
     ```

     Returns the user's 40-char PAT.  Pass via
     `GH_TOKEN="$TOKEN" gh ...`.

   - Result: **5 total runs returned. 0 `event=schedule`.
     0 `conclusion=success`.**  All 5 are PR-trigger
     failures.
   - Earliest: `2026-05-23T20:04:35Z`.  Latest:
     `2026-05-24T02:51:27Z`.
   - Gate: `>=3 successful cron runs`.  Predicate fails.
   - Cumulative deferral: W11 → W12 → W13 → W14 → W15
     (= **5-wave deferral**).
   - Doc: `docs/frontend-pwa-audit.md §6.4.1` (Hicks W15
     re-query evidence appended inside the pre-written
     §6.4 W15 block).
   - §6.5 deadlock escalation (Stephen-direct manual
     trigger seed) remains the recommended W16 unblock.
     If Stephen-direct lands ≥ 3 successful manual runs,
     W16 Hicks can hard-pin.  Else W16 trips the §6.3
     6-wave escalation criterion.

2. **`snapshotPathTemplate` convention + visual-regression
   spec migration.**  Two-file change pair:

   - `tests/e2e/playwright.config.ts`: added
     `snapshotPathTemplate:
     '{testFileDir}/__screenshots__/{testFileName}/{arg}{ext}'`
     pinning every spec's baselines under a deterministic
     path that matches the Vasquez/Hicks W14 directory
     layout.
   - `tests/e2e/manifest-screenshots-visual.spec.ts`:
     REMOVED all `page.setContent(...)` calls.  The spec
     now `page.goto(<asset-url>, { waitUntil: 'load' })`
     for each manifest screenshot entry, and uses
     `page.waitForLoadState('networkidle')` in place of
     the previous hand-rolled `img.complete` polling.
     Forward-stage tolerance preserved (origin
     unreachable, no manifest, empty `screenshots[]`,
     navigation fail, baseline missing).

   Convention rationale: Playwright's default
   `{testFilePath}-snapshots/{arg}{platform}{ext}` path
   was making W12-era spec moves produce orphan baseline
   PNGs whenever a spec was renamed.  Pinning
   `{testFileDir}/__screenshots__/{testFileName}/...`
   matches the existing W14 captures + survives spec
   renames cleanly + makes the baselines lane-clear.

   - Doc: `docs/frontend-pwa-audit.md §7.2`
     (convention + §7.2.2 migration narrative).
   - Selector inventory: `tests/selectors.md` W15 Hicks
     footer "Visual-regression spec migration (W15)".

3. **Phase L renderer-webgl2 IMPLEMENTATION kickoff.**  Stood
   up the foundation under
   `src/frontend/autotable-src/src/renderer-webgl2/`:

   - `index.ts` (~11.2 KB source): hand-rolled WebGL2
     primitives — `createWebgl2Context`,
     `compileProgram`, `createTexturedQuadBuffers`,
     `createTexture`, `identity4`, `perspective4`,
     `helloWorld`.  Zero three.js dependency.  Pure
     vertex/fragment shader pipeline + VBO/VAO/EBO
     management.
   - `hello.ts` (~3.8 KB source): URL-gated entry behind
     `?renderer=webgl2-hello`.  Loads
     `/img/tiles-labels.auto.png`, renders one textured
     quad, prints status to a div under the canvas.
   - `src/index.ts`: added the W15 URL-gated dynamic
     import after the existing `handlePwaActionFromUrl()`
     call.  Cold-path users never load the chunk.
   - `vite.config.ts`: added a `manualChunks` rule
     routing `src/renderer-webgl2/` → `renderer-webgl2`
     chunk.

   **Chunk baseline:** `renderer-webgl2.<hash>.js = 6,237 B`
   (3 % of the 180-220 KB Phase L envelope per Stephen's
   W12 audit).  This means we have ≈ 175-215 KB of headroom
   for tile mesh + texture atlas + scene graph in W1–W3 of
   Phase L without breaching the three-renderer-big
   hold-line.

   - Doc: `docs/phase-l-renderer-implementation.md` (NEW —
     bundle math, three.js boundary, risk register, W1
     hand-off).
   - Selector inventory: `tests/selectors.md` W15 Hicks
     footer "`?renderer=webgl2-hello` Phase L spike entry".

4. **`?action=cost-forecast&days=<n>` deep-link routing.**
   Wired against Bishop's W15 `GET
   /api/commentary/cost/forecast?days=<n>` endpoint.

   - `src/admin-cost-forecast.ts` (NEW, ~11.6 KB source):
     overlay mirroring `admin-cost.ts` pattern.  Exports
     `normaliseDays(raw): number` (clamps `[1, 90]`, default
     `30`) + `openCommentaryCostForecastPanel(days):
     Promise<void>`.  Renders projected month-end currency,
     confidence percentage with `strong | moderate | weak`
     band class, observed/window day pair.  Tolerates
     Bishop W15 wire-shape drift:
     `projectedMonthEndCostUsd` / `projectedCostUsd` /
     `projectedCost`; `daysOfData` / `daysWithData`;
     `confidence` auto-detect (`0-100` int OR `0-1`
     fractional).  Status-code surfaces: `400` invalid
     window, `401` redirect-to-lobby, `403` admins-only,
     `404` not-available, `5xx` / network unavailable.
   - `src/action-router.ts`: added `'cost-forecast'` to
     `SUPPORTED_ACTIONS`, added `dispatchCostForecast()` +
     `gateAndMountCostForecast()` helpers after the
     `admin-cost` block, added a `switch` case in
     `handlePwaActionFromUrl()`.  URL rewrite: pre-
     dispatch `/?action=cost-forecast&days=<n>`; post-
     dispatch `/admin/commentary-cost/forecast` (`action`
     + `days` stripped from query string via
     `history.replaceState`).

   **Chunk baseline:** `admin-cost-forecast.<hash>.js =
   6,108 B`.  Sister to `admin-cost = 5,966 B`.
   Action-router plumbing added `+1,102 B` to
   `autotable-src-eager` (`221,745 → 222,847 B`) —
   slightly above the 0.5 KB target but the new dispatch
   helper inlines URL parsing + `history.replaceState` +
   error handling per the action-router convention.

   - Doc: `docs/frontend-routing.md §7.1` (cost-forecast
     flow narrative + URL shape + 401 redirect contract +
     wire-shape tolerance).  §3 routing table updated.
   - Selector inventory: `tests/selectors.md` W15 Hicks
     footer "`?action=cost-forecast&days=<n>` overlay".

5. **Bundle shrinkage opportunity audit (NEW doc).**
   Wrote `docs/frontend-bundle-audit.md` covering every
   chunk except `three-renderer-big` (which is Phase L
   territory).  Identified candidate W16/W17
   optimisations with ROI ordering:

   - §3.1: Sentry conditional load on DSN-presence —
     highest absolute KB recoverable (≈ 342 KB for users
     without a configured DSN, which is most of them).
   - §3.2: `autotable-src-eager` surgery — highest
     practical ROI; isolate the W11–W15 action-router
     dispatcher boilerplate behind a small static
     dispatch table + lazy-load the dispatch helpers.
   - §3.3: `hls.js` → conditional gate on actual HLS
     stream URL (saves ≈ 286 KB on users who never view a
     livestream).
   - §3.4: `gltf-loader` → already lazy-mounted per W12;
     audit confirmed clean, no W16 action.
   - §3.5: `scene-effects` (59 KB) → audit the W6 dust +
     particle assets for tree-shake opportunities.

### Build state (K14 → K15)

```
                       K14        K15        Δ
three-renderer-big     406,635    406,635    0   (HOLD — 5th wave)
three-renderer-small    75,384     75,384    0
autotable-src-eager    221,745    222,847   +1,102  (cost-forecast plumbing)
admin-cost-forecast    --          6,108   +6,108  (NEW W15)
renderer-webgl2        --          6,237   +6,237  (NEW W15)
admin-cost              5,966      5,966    0
audit                   7,523      7,523    0
bracket-listing         6,544      6,544    0
chat                   12,306     12,306    0
commentary-panel        8,269      8,269    0
game-bootstrap        174,561    174,561    0
gltf-loader            44,223     44,223    0
history                12,408     12,408    0
hls                   286,514    286,514    0
replays-listing         4,723      4,723    0
scene-effects          59,041     59,041    0
scene-shell             2,341      2,341    0
spectator-livestream    5,288      5,288    0
tour                   10,454     10,454    0
tournaments            41,100     41,100    0
voice                   9,382      9,382    0
```

Three-renderer-big held @ 406,635 B for the **fifth
consecutive wave** (W11 baseline → W12 → W13 → W14 → W15).
This is the Phase L pre-spike hold-line; once Phase L W1
starts adding three.js consumers, the line moves to
"renderer-webgl2 + renderer-three composite ≤ Phase L
envelope (180-220 KB above the W15 6,237 B baseline)".

### TS strict status

`npx tsc --noEmit` from `src/frontend/autotable-src/`
exited clean modulo three pre-existing test-file noise
entries documented in W14:

- `tests/e2e/onboarding-server-cookie.spec.ts:59` —
  `toBeTruthy on never`
- `tests/e2e/reduced-motion.spec.ts:21` — reducedMotion
  fixture property
- `tests/e2e/tournament-admin-bracket.spec.ts:145` —
  `toBeTruthy on never`

None of these are caused by W15 changes; they pre-date W11.

### Build commands (verified)

```bash
cd src/frontend/autotable-src
WAVE_NAME=K15 npm run build:vite    # builds + records K15 row
npx tsc --noEmit                     # TS strict
```

`dist-size.json` K15 row records 21 chunks (19 existing +
2 new W15: `renderer-webgl2`, `admin-cost-forecast`).

### Identity hardening (10th consecutive clean wave)

- Author: `Hicks (Frontend) <hicks@squad.mahjong>`.
- Co-author trailer:
  `Copilot <223556219+Copilot@users.noreply.github.com>`.
- Flock-wrapped commit via `.work/squad-git-lock` so
  Vasquez (concurrent on shared `tests/selectors.md`)
  can't race.
- Per-commit identity via `git -c user.name=... -c
  user.email=... commit -m "..."` (does NOT mutate global
  config — see `docs/repo-guidelines.md`).

Track record (W6 → W15): **10 consecutive clean waves**.

### Hand-offs to W16

1. **LH13 hard-pin (Hicks W16).**  If §6.5 Stephen-direct
   seed produces ≥ 3 successful manual-triggered
   `pwa-audit.yml` runs on `main`, hard-pin the workflow
   + flip Vasquez mirror to hard-assert.  Else the 6-wave
   §6.3 escalation criterion trips and the Coordinator
   picks a disposition (sunset / archive / migrate).
2. **Phase L W1 (Hicks W16).**  Land the tile mesh graph
   (~15 KB) as the second discrete addition under
   `src/renderer-webgl2/`.  See
   `docs/phase-l-renderer-implementation.md §7` for the
   bundle math + the three.js boundary that must hold.
3. **Bundle audit §3.1 + §3.2 (Hicks W16/W17).**
   Sentry conditional load + autotable-src-eager surgery
   are the two W16 candidates.  See
   `docs/frontend-bundle-audit.md §3`.
4. **Vasquez (concurrent W16).**  W14 W15 deferral
   chain in `tests/selectors.md` continues to belong to
   Vasquez on the QA mirror side; Hicks W15 added only
   the Hicks-side footer.

### Files modified / created in W15

**Created (5):**
- `src/frontend/autotable-src/src/renderer-webgl2/index.ts`
- `src/frontend/autotable-src/src/renderer-webgl2/hello.ts`
- `src/frontend/autotable-src/src/admin-cost-forecast.ts`
- `docs/phase-l-renderer-implementation.md`
- `docs/frontend-bundle-audit.md`

**Modified (9):**
- `src/frontend/autotable-src/src/action-router.ts`
- `src/frontend/autotable-src/src/index.ts`
- `src/frontend/autotable-src/tests/e2e/playwright.config.ts`
- `src/frontend/autotable-src/tests/e2e/manifest-screenshots-visual.spec.ts`
- `src/frontend/autotable-src/tests/selectors.md` (W15 Hicks footer)
- `src/frontend/autotable-src/vite.config.ts`
- `src/frontend/autotable-src/scripts/append-dist-size.js`
- `src/frontend/autotable-src/dist-size.json` (auto-updated)
- `docs/frontend-pwa-audit.md` (§6.4.1 + §7.2)
- `docs/frontend-routing.md` (§3 table + §7.1 flow)

Plus the rebuilt `src/frontend/autotable/` dist artefacts.

### W15 closing posture

W15 lands the 5-item charter clean.  Phase L is no longer
a spike — it's an implementation in motion with a
measured `renderer-webgl2 = 6,237 B` baseline + a written
W1 hand-off doc.  The three-renderer-big hold-line
survives a fifth wave running.  LH13 enters its 5-wave
cumulative deferral with §6.5 Stephen-direct as the W16
unblock path.  W16 Hicks inherits Phase L W1 + the §3.1
Sentry + §3.2 autotable-src-eager bundle surgery + LH13
disposition.
