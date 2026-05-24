# Frontend bundle audit — W15 shrinkage opportunity inventory

> **Wave:** Phase K Wave 15 (Hicks W15 deliverable #5)
> **Author:** Hicks (Frontend lead)
> **Status:** Live — opportunity inventory for W16 / W17 planning.
> **Scope:** Every chunk in `dist-size.json` EXCEPT `three-renderer-big`
> (which has its own dedicated audit doc + a Phase L migration plan).

## §1 — Why this doc exists

The W6 → W14 program drove `three-renderer-big` from 740 KB to
406 KB (-45 %) — see `docs/phase-l-renderer-spike.md` §1 for the
trajectory.  That campaign exhausted the within-three.js levers,
which is why W14 declared **Go** on the Phase L hand-rolled
WebGL2 renderer.

**This doc audits the REST of the bundle**: every chunk OTHER
than `three-renderer-big`.  None of these have received the same
attention because, individually, none is the headline number on
the dashboard.  Collectively though they're the next ~250–300 KB
of trim potential — a worthwhile target for W16 / W17 before
Phase L's own renderer-replacement campaign starts.

## §2 — W15 chunk inventory

Source: `src/frontend/autotable-src/dist-size.json` K15 row.

| # | Chunk | W15 bytes | W15 KiB | Audit notes |
|---|-------|----------:|--------:|-------------|
| 1 | `three-renderer-big`    | 406,635 | 397.10 | OUT OF SCOPE — Phase L renderer covers it. |
| 2 | `hls`                   | 286,514 | 279.80 | OUT OF SCOPE — vendored hls.js (W7 split), gated to spectator path. |
| 3 | `autotable-src-eager`   | 222,847 | 217.62 | **§3.1 candidate.** |
| 4 | `game-bootstrap`        | 174,561 | 170.47 | **§3.2 candidate.** |
| 5 | `three-renderer-small`  |  75,384 |  73.62 | OUT OF SCOPE — Phase L renderer subsumes it. |
| 6 | `scene-effects`         |  59,041 |  57.66 | **§3.3 candidate.** |
| 7 | `gltf-loader`           |  44,223 |  43.19 | OUT OF SCOPE — already split (W8), shared with renderer-webgl2. |
| 8 | `tournaments`           |  41,100 |  40.14 | **§3.4 candidate.** |
| 9 | `sentry`                | 342,610 | 334.58 | **§3.5 candidate.** (DSN-gated; Sentry boot pays full cost.) |
| 10 | `history`              |  12,408 |  12.12 | Below noise floor — kept as-is. |
| 11 | `chat`                 |  12,306 |  12.02 | Below noise floor — kept as-is. |
| 12 | `tour`                 |  10,454 |  10.21 | Below noise floor — kept as-is. |
| 13 | `voice`                |   9,382 |   9.16 | Below noise floor — kept as-is. |
| 14 | `commentary-panel`     |   8,269 |   8.08 | Below noise floor. |
| 15 | `audit`                |   7,523 |   7.35 | Below noise floor. |
| 16 | `bracket-listing` (W14)|   6,544 |   6.39 | Below noise floor (W14 new). |
| 17 | `renderer-webgl2` (W15)|   6,237 |   6.09 | **W15 baseline** — Phase L W1+ extends. |
| 18 | `admin-cost-forecast` (W15) | 6,108 |   5.96 | **W15 new** — sister to admin-cost. |
| 19 | `admin-cost` (W14)     |   5,966 |   5.83 | Below noise floor (W14 new). |
| 20 | `spectator-livestream` |   5,288 |   5.16 | Below noise floor. |
| 21 | `replays-listing` (W14)|   4,723 |   4.61 | Below noise floor (W14 new). |
| 22 | `scene-shell`          |   2,341 |   2.29 | Coordinator — no trim potential. |

Total W15 dist size (.js only, excl. assets):
**1,742,524 B (1,701.69 KiB)**.

Sentry deserves special call-out — at 342 KB it's the second-
largest non-renderer chunk in the system AND its load is
conditional (boot-time DSN probe).  See §3.5.

## §3 — Candidate optimisations (ranked by ROI)

ROI = estimated KB saved per wave of engineering effort.  The
ranking is provisional; W16 / W17 owners should validate the
estimates before committing to the implementation.

### §3.1 — `autotable-src-eager` lazy-load surgery (highest ROI)

**Current:** 222,847 B (W15).  The eager entry chunk carries
the lobby module + i18n + sentry shim + identity helpers + pwa
registrar + reconnect helpers + about-modal copy + tournaments
panel + audit panel.

**Estimated savings: 30-50 KB** by lazy-mounting two surfaces:

1. **Tour scheduler.**  `src/index.ts` already lazy-imports
   `./tour`, but the *scheduler* code that decides whether to
   lazy-import lives in the eager bundle.  Profile shows ~7 KB
   of identity / i18n / theme code is reachable only from the
   tour scheduler path.  Hoist the scheduler into the lazy
   `tour` chunk (re-use the existing dynamic import boundary
   in `index.ts`).

2. **Avatar migration modal.**  `installAvatarMigrationModalIfNeeded()`
   eagerly imports the modal HTML + the avatar editor.  W14
   analytics show <2 % of sessions actually trigger the modal
   (legacy `#808080` sentinel users).  Lazy-import it from
   inside the existing scheduler so the eager path only pays
   for the LS probe (~1 KB) instead of the full modal (~20 KB).

3. **PWA `?action=*` routing.**  The `action-router.ts` module
   ships eagerly (~5 KB) because `src/index.ts:line 19`
   imports it synchronously.  W14 / W15 added 4 new keywords
   to it.  Moving the router behind a `if (location.search
   .includes('action='))` dynamic-import guard would save
   the full action-router import from cold lobby paths
   (most loads are bare `/`).  This is the easiest win —
   one dynamic-import boundary change in `src/index.ts`.

**Suggested wave:** W16.  Touches only `src/index.ts` + maybe
two helper files.

### §3.2 — `game-bootstrap` shape audit (medium ROI)

**Current:** 174,561 B (W15).  Dynamic-imported when the URL
has a non-empty search string post-action-router strip.

**Estimated savings: 15-25 KB** by splitting cold-path helpers:

1. **Replay viewer code path.**  `game-bootstrap` re-exports
   the replay-launcher today (W12 wire); the replay viewer
   surface is reached via `?game=<id>&replayId=<id>` OR
   `?action=replay`.  The latter already goes through
   `replay-launcher.ts` (its own chunk).  Audit whether the
   game-bootstrap import path still pulls the replay surface
   in eagerly.

2. **Chat / voice initial-state code.**  `game-bootstrap`
   imports the chat + voice modules eagerly so the at-table
   UI has them at first-paint.  Both are already lazy chunks;
   if game-bootstrap is fanning them in via static `import
   { … } from './chat'`, that re-collapses them into the
   bootstrap chunk.  Audit the import shape; move to
   `await import('./chat')` if needed.

**Suggested wave:** W17.  Higher friction than §3.1 because
the at-table boot path is more delicate.  Needs visual-
regression spec coverage before the split (the W14
`visual-regression-real-captures.spec.ts` is the wedge).

### §3.3 — `scene-effects` post-fx deferral (low ROI)

**Current:** 59,041 B (W15).  Dynamic-imported by `scene-
shell` when the post-processing pipeline initialises.

**Estimated savings: 5-10 KB** by deferring the post-FX init:

The scene-effects module ships the outline + bloom + glow
shader graphs.  Of those, outline is the user-visible effect
on hover; bloom + glow are dice / score-stick highlights that
only fire when a hand completes.  Lazy-load bloom + glow on
the FIRST hand completion event instead of at scene-shell
boot.

**Suggested wave:** W17.  Implementation overlaps with the
Phase L renderer migration (post-fx ports to renderer-webgl2
in W2/W3); skip if Phase L lands first.

### §3.4 — `tournaments` bracket-renderer fork-out (low ROI)

**Current:** 41,100 B (W15).  Dynamic-imported on Tournaments
tab activation.

**Estimated savings: 10-15 KB** by moving the bracket renderer
into its own chunk:

The tournaments chunk includes (a) the listing panel that
renders inside the lobby tab + (b) the bracket renderer
(Swiss + double-elim layouts) that only fires when the user
clicks into a specific tournament's bracket detail view.
Split (b) into a `tournament-brackets` chunk lazy-imported
on bracket-detail click.

**Suggested wave:** W17+.  W14 already shipped the W14
`bracket-listing` chunk for the `?action=bracket` deep-link;
this audit item is the *in-tournament-flow* bracket view,
not the deep-link one.  Two distinct code paths today; W17
could consider unifying them through the W14 chunk.

### §3.5 — `sentry` conditional load (highest absolute KB)

**Current:** 342,610 B (W15).  Loaded eagerly via
`initSentry()` in `src/index.ts`.

**Estimated savings: ~100 % of the chunk for non-DSN users**
(i.e. local-dev + any production deployment without
`<meta name="sentry-dsn">` set).  The Sentry SDK boot path
is already a no-op when no DSN is exposed, but the **chunk
itself still ships** to those users.

The fix is a gated dynamic import:

```ts
// src/index.ts (W16+ candidate)
const dsn =
  document.querySelector<HTMLMetaElement>('meta[name="sentry-dsn"]')?.content
  ?? (window as { __SENTRY_DSN__?: string }).__SENTRY_DSN__;
if (dsn !== undefined && dsn !== '') {
  void import('./sentry').then((mod) => mod.initSentry(dsn));
}
```

Production users WITH a DSN configured still pay the full
342 KB; the chunk is only saved for the no-DSN case.  But
this is the single largest "free win" on the dashboard
because local dev + any preview deployment without the DSN
meta tag entirely sheds the chunk.

**Suggested wave:** W16.  Single-file change in `src/index.ts`.

## §4 — Roll-up estimate

| Section | Estimated savings | Wave |
|---------|------------------:|------|
| §3.1 — autotable-src-eager surgery | 30–50 KB | W16 |
| §3.2 — game-bootstrap shape audit  | 15–25 KB | W17 |
| §3.3 — scene-effects deferral      | 5–10 KB  | W17 (skip if Phase L) |
| §3.4 — tournaments bracket fork-out | 10–15 KB | W17+ |
| §3.5 — sentry conditional load     | 0 KB (DSN) / 342 KB (no DSN) | W16 |
| **Total (DSN-configured)**         | **60–100 KB** |     |
| **Total (no DSN)**                 | **400–440 KB** |    |

The no-DSN savings number is the dramatic one — it's roughly
equivalent to the entire W6→W14 three-renderer reduction
campaign (332 KB) plus a small bonus, achievable in a single
W16 wave touching one file.  W16 should prioritise §3.5
unless a sibling lane already plans to land a DSN.

## §4.1 — W16 delivered savings (Hicks)

W16 executed §3.1 + §3.5 jointly via the `src/index.ts`
lazy-mount refactor.  Result, measured from `dist-size.json`
K16 row (raw bytes):

| Chunk | W15 bytes | W16 bytes | Δ | Notes |
|-------|----------:|----------:|--:|-------|
| `autotable-src-eager` | 222,847 | 214,202 | **−8,645** | §3.1 + §3.5 surgery |
| `action-router` (NEW)  |       0 |   8,209 | +8,209    | §3.1 — split out, lazy on `?action=*` |
| `sentry-shim` (NEW)    |       0 |   2,304 | +2,304    | §3.5 — wrapper-only chunk, lazy on PROD/SENTRY_DEBUG |
| `sentry` (SDK; tracked since W16) | (not tracked) | 342,614 | n/a | unchanged shape — still DSN-gated inside wrapper |
| `renderer-webgl2`      |   6,237 |  19,017 | +12,780   | Phase L W2 tile-mesh graph (separate deliverable) |
| `three-renderer-big`   | 406,635 | 406,635 | 0         | Hold-line (6th wave) |

### Net cold-path delta (bare-`/` lobby load)

| Path | Wave | Eager + immediately-fetched chunks (bytes) |
|------|-----:|-------------------------------------------:|
| Bare `/` (no DSN, no `?action=*`) | W15 | 222,847 |
| Bare `/` (no DSN, no `?action=*`) | W16 | **214,202** |

**Savings: 8,645 bytes** on the cold lobby path.  When the user
later activates a `?action=*` PWA shortcut, the 8 KB
`action-router` chunk is paid then — never on the bare-`/`
visit.  When the user is on a PROD build WITH a DSN, the
2.3 KB `sentry-shim` chunk + the 342 KB `sentry` SDK chunk
are paid; without a DSN, both are skipped.

### §3.5 actuality vs. the W15 estimate

The W15 §3.5 estimate ("0 KB DSN / 342 KB no-DSN") assumed the
entire 342 KB sentry chunk was eager.  Empirically, the W15
sentry chunk was ALREADY lazy (DSN check inside `sentry.ts`
gated the `await import('@sentry/browser')`).  The actual W16
delivery is:

* **Local dev / no-DSN preview**: −2.3 KB (sentry-shim wrapper
  never loaded) + the 342 KB SDK chunk continues to not load
  (no change from W15).  Practical "first-paint" delta: same
  ~3 KB drop as the wrapper's eager-graph contribution.
* **PROD with DSN**: 0 KB change on first paint (the wrapper
  was already 3 KB inside the eager chunk; it's now a 2.3 KB
  lazy chunk that fetches on boot).  Functionally identical.
* **Local dev with `localStorage.SENTRY_DEBUG=1`**: same as PROD.

This is below the W15 §3.5 headline.  The headline was the
worst case (no-DSN deploy); the realistic deploys all match the
~3 KB drop instead.  W17 should re-audit §3.5 in light of this
finding — the next win likely needs a separate config-mode
flag (`SENTRY_DEFER_INIT=1`) that defers even the DSN probe.

## §4.2 — W17 + remaining candidates (sections renumbered to §3.6+)

## §4.3 — W21 delivered savings (Hicks)

W21 executed two surgeries jointly to land the §3.6 W21 bring-up
target.  Result, measured from `dist-size.json` K21 row (raw bytes):

| Chunk | W20 bytes | W21 bytes | Δ | Notes |
|-------|----------:|----------:|--:|-------|
| `autotable-src-eager` | 123,701 | **112,219** | **−11,482** | §3.6 profile-drawer extraction + i18n zh-* catalog lazification |
| `profile-drawer` (NEW) | 0 | 3,871 | +3,871 | §3.6 — split from `./profile`, lazy on `lobby-open-profile` chip hover/focus/click |
| `zh-Hans` (NEW JSON chunk) | 0 | 4,437 | +4,437 | §3.6 — zh-Hans i18n catalog, lazy on resolved active locale (auto-detect or `setLanguage()` flip) |
| `zh-Hant` (NEW JSON chunk) | 0 | 4,434 | +4,434 | §3.6 — zh-Hant i18n catalog, lazy on resolved active locale |
| `renderer-webgl2` | 35,258 | 40,292 | +5,034 | Phase L W21 tile-claim-animation + meld-display (separate W21 deliverable, not §3.6) |
| `admin-panel` | 35,161 | 48,984 | +13,823 | W21 +5 W21 admin surfaces (separate W21 deliverable, not §3.6) |
| `three-renderer-big` | 406,635 | 406,635 | 0 | Hold-line (11th wave) |

### Net cold-path delta (bare-`/` lobby load) — W20 → W21

| Path | Wave | Eager + immediately-fetched chunks (bytes) |
|------|-----:|-------------------------------------------:|
| Bare `/` (no DSN, no `?action=*`, en locale) | W20 | 123,701 |
| Bare `/` (no DSN, no `?action=*`, en locale) | W21 | **112,219** |

**Savings: 11,482 bytes** on the cold lobby path (en-locale users).
For zh-* locale users, the resolved-locale catalog (~4.4 KB) loads
asynchronously after first paint; until the chunk lands, `t()`
falls back to the English catalog (the documented fallback path).
Practical impact: ~10-30 ms of English strings at lobby cold-start
for zh-* users; localized strings appear once the JSON chunk
arrives (typically within the same RTT as the eager bundle thanks
to HTTP/2 server-push hints emitted by Vite's modulepreload list).

### §3.6 — profile-drawer surgery rationale

The Wave-5 profile drawer (`installProfileDrawer` + `installProfileToggle`)
shipped eagerly from `./profile` because the lobby's `initLobby()`
called both at boot to wire the side-drawer UI.  Three observations
made it a clean lazification target at W21:

1. **Modern path bypass.**  W17 §3.2 introduced `./profile-page`
   which intercepts the `lobby-open-profile` chip click in CAPTURE
   phase.  On modern paths the drawer's chip handler is dormant —
   `profile-page` wins the click and renders the modern full-screen
   profile UI.  The drawer's DOM listeners (close, save, name input,
   color picker, presets, custom color) are only reached if a
   third-party flow calls `openProfileDrawer()` programmatically.
2. **Self-contained DOM graph.**  Aside from data-layer hooks
   (`getProfile`, `onProfile`, `setDisplayName`, `setAvatarColor`,
   etc., all kept in `./profile`), the drawer is pure DOM
   installation.  No cross-module dependencies on the install
   path; clean dynamic-import boundary.
3. **Same activation surface as `./profile-page`.**  Both modules
   load on the `lobby-open-profile` chip hover/focus/click, so
   the lazy-mount infrastructure is a straight copy of
   `scheduleProfilePageLazyMount` from W17.

W21 split `./profile-drawer.ts` (~3.9 KB minified) out of
`./profile.ts`, added `flushPendingDisplayName()` as a thin
data-layer flush helper the drawer's Save button calls, and
wired `scheduleProfileDrawerLazyMount()` from `./lobby` to mount
on the chip's first activation event.

### §3.6 — i18n zh-* catalog lazification rationale

The W17 §3.6 audit estimated the i18n catalogs at "8-15 KB" — at
W21 the bundled JSON measured 14,656 B raw (en: 4,895 B; zh-Hans:
4,882 B; zh-Hant: 4,879 B).  W21 left `en` bundled (it's the
fallback path inside `t()`) and split the two zh-* catalogs into
their own chunks via dynamic `import('./i18n/zh-Hans.json')` /
`import('./i18n/zh-Hant.json')`.

The synchronous `t()` API is preserved unchanged: when an
unloaded zh-* locale is active, `t()` falls back to the English
catalog (the documented fallback).  `installI18n()` and
`setLanguage()` both kick off `ensureCatalog(activeLocale)`
which loads the chunk and re-emits the locale-change event so
listeners can re-render.  `mergeServerCatalog()` was hardened
to await the base catalog before applying server-supplied
pattern patches.

W21 actually delivered 8,871 B of i18n savings on the eager
chunk (within the W17 §3.6 estimate of 8-15 KB), at the cost
of two new ~4.4 KB chunks that load asynchronously and only
for zh-* users.



The W15 §3.2 / §3.3 / §3.4 sections remain on the W17 docket
(see §4 roll-up).  Three NEW candidates surfaced during the W16
surgery + Phase L tile-mesh work:

### §3.6 — `i18n` lazy-load (W17 candidate, medium ROI)

`src/index.ts` eagerly imports `./i18n` and invokes `installI18n()`
at boot.  The eager call is required because the body[lang=…]
attribute must be set BEFORE any DOM paint to avoid layout flash.
However, the **table of resolved strings** for non-default locales
is plausibly the bulk of the module weight; if the eager call
only needs the default-locale fast-path, the resolved-string
tables for ja / zh / etc. could lazy-import on demand (on a
`lang` setting flip in the locale chooser).

**Estimated savings: 8-15 KB** depending on how big the per-locale
tables are.  Audit method: profile `i18n.ts` for static-import
weight; if ≥ 50 % is locale tables, split them.

### §3.7 — `game-bootstrap` autotable-src-eager re-fold (W17 candidate)

W16 shrank `autotable-src-eager` by 8.6 KB but `game-bootstrap`
remained at 174,561 B.  Audit suggestion: the W14 / W15
`scheduleAuditTabLazyMount` / `scheduleHistoryLazyMount` /
`scheduleOnboardingTour` patterns still live in `src/index.ts`
(eager).  If those scheduler shells could move into the
`game-bootstrap` chunk (which only loads when a query-string is
present), the lobby cold path sheds the entire scheduler
machinery.  Risk: the schedulers fire on the lobby DOM (audit
tab, history modal, profile page) — the page is "lobby-only"
when search === '', but the schedulers themselves attach to
lobby-DOM listeners.  Moving them into game-bootstrap breaks
the "open profile while lobby is empty" flow.

**Estimated savings: 8-12 KB** if the breakage can be
re-architected (move just the LISTENER attachment to eager and
the LOAD path to a lazy module).  W17 audit recommended before
implementation.

### §3.8 — `sentry-shim` re-fold via `SENTRY_DEFER_INIT` config

Per §4.1, the W16 §3.5 surgery delivered ~3 KB instead of the
~100-342 KB headline.  A W17 follow-up is to add a per-deploy
`SENTRY_DEFER_INIT` window flag (set by `<meta name="sentry-defer-init"
content="1">` in the deploy-specific index.html template) that
disables sentry init **entirely** — not even the DSN probe — so
the wrapper chunk is never loaded.  Targets the "PROD with DSN
but we want fast first-paint" cohort.

**Estimated savings: 2.3 KB** (just the shim chunk; the SDK is
already DSN-gated).  Low ROI, but a clean config knob for
performance-sensitive deploys.

## §5 — Methodology notes

* Sizes are raw `fs.statSync().size` bytes from
  `src/frontend/autotable-src/scripts/append-dist-size.js`.
  Gzipped sizes (the wire-transfer cost) are typically
  25-35 % of raw bytes; the relative ranking is preserved
  across raw vs. gzip so the ROI table stands.
* The "below noise floor" threshold is **< 15 KB** raw —
  the engineering cost to lazy-load a sub-15 KB chunk
  typically exceeds the wire-transfer savings at that
  size.
* Estimated savings are author-time guesses based on the
  static-import graph shape; W16 / W17 owners should
  re-measure with `npm run build:vite` after each
  optimisation lands and update `dist-size.json` for the
  audit.

## §6 — Hand-off

W16 picks §3.1 (autotable-src-eager) + §3.5 (sentry gate).
W17 picks §3.2 + §3.3 + §3.4, plus the W16-added §3.6 (i18n),
§3.7 (game-bootstrap re-fold), and §3.8 (sentry-defer-init).
This doc is updated wave-over-wave with the actual numbers + a
"delivered savings" column (see §4.1) once each wave lands.
