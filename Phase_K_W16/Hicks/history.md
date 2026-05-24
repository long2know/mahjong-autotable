# Hicks — Phase K Wave 16 history (wave-scoped)

> Wave-scoped excerpt of the persistent history at
> `.squad/agents/hicks/history.md`.  The full chronological
> record is the source of truth.

## Phase K Wave 16 — Frontend bring-up

Branch: `stlong/phase-k-wave-16-bringup` (off main `c1f336a`).
W15 PR merged into main between waves.

### Deliverables (four)

1. **LH13 6th-wave decision — Option A soft-flip.**

   Re-ran the §6.4.1 evidence query.  The W16 row reproduces
   W15 §6.4.1 exactly: 0 cron runs, 0 successes, only
   PR-triggered failure runs.  The Coordinator-direct seed
   from §6.5 has not landed.  Continuing to defer would push
   the cumulative deferral to **6 waves** (W11 calibration
   → W16 defer), tripping the §6.3 escalation criterion.

   **Picked Option A** per the W16 directive:
   documentation-only soft-flip with provisional thresholds
   (the W11 calibration values, repeated verbatim, tagged
   `provisional-until-calibrated`).  New doc:
   `docs/lh13-soft-pin-rationale.md` carries the full rationale
   + the Coordinator-direct runbook in §4.

   The workflow file `pwa-audit.yml` is **untouched**.  The
   soft-flip is a status change in the docs; the workflow
   gates remain at the W11 calibrated values.

   Provisional thresholds (§3 of the new doc):

   | Category | Provisional | Authority |
   |----------|------------:|-----------|
   | Performance | 0.85 | W11 §7 p50 |
   | Accessibility | 1.00 | W11 §7 |
   | Best Practices | 0.95 | W11 §7 |
   | SEO | 0.90 | W11 §7 |
   | PWA (geo-mean) | 0.90 | W10+ hard floor |

   Status: YELLOW (preserved).  Becomes HARD when either
   (a) ≥ 3 successful cron runs converge within ±2 points
   of these values, OR (b) the Coordinator explicitly approves.

2. **Phase L tile-mesh graph.**

   Expanded `src/renderer-webgl2/` from the W15 6.2 KB
   hello-world to a **19.0 KB** tile-mesh graph.  Three new
   files + one math-helpers module:

   * `tile-mesh.ts` — instanced quad geometry for ≤ 200 tiles
     (144 wall + 14 in-hand + spare for dora/discard).  One
     `drawElementsInstanced` call.  Per-instance model matrix
     + tile-id buffers.  Custom vertex shader handles the
     mat4-as-4-vec4 attribute split.
   * `tile-atlas.ts` — 3-col × 34-row atlas loader with a
     fallback that synthesises an HSL-cell test pattern when
     the canonical asset isn't on disk (so the smoke render
     works without `tiles-atlas-webgl2.auto.png`).
   * `camera.ts` — orbital camera with mouse (left-drag orbit,
     right-drag pan, wheel zoom) + touch (one-finger orbit,
     two-finger pinch-zoom + pan).  Hand-rolled view + viewProj
     matrices, no three.js dependency.
   * `math.ts` — shared 4×4 column-major matrix helpers
     (identity, perspective, translate, scale, rotateY,
     multiply).  Used by tile-mesh, camera, and the W15
     hello-world (refactored to import from math).

   The W15 `hello.ts` mount logic now sniffs
   `?renderer=webgl2-tile-mesh` and dispatches to
   `mountTileMesh()` (vs. `mountHelloWorld()`).  Both share
   the container scaffolding.

   Bundle math:

   | Wave | `renderer-webgl2` chunk |
   |------|-------------------------:|
   | W15 |  6,237 B |
   | W16 | **19,017 B** |
   | Δ   | **+12,780 B** |

   Under the 22,000 B W16 cap by ~2.9 KB headroom.

   No three.js dependency on the W16 path.  `three-renderer-big`
   chunk size: unchanged at **406,635 B** (held).

3. **Bundle audit §3.1 + §3.5 surgery.**

   `src/index.ts` refactored to lazy-mount three previously-
   eager imports:

   * **`./action-router`** (was static import in W15) — now
     gated on `/[?&]action=/.test(window.location.search)` and
     dynamic-imported only on the action-deep-link paths.
     Vite splits it into a new `action-router.<hash>.js` chunk:
     **8,209 B**.

   * **`./identity` avatar-migration modal** (was eager
     `installAvatarMigrationModalIfNeeded()` call) — now gated
     on an LS sentinel probe (`mahjong.identity.cache.v1`
     contains `avatarColor === '#808080'`).  The identity module
     stays in `autotable-src-eager` because the lobby imports
     other parts of it; the avatar-migration code path simply
     isn't reached.

   * **`./sentry`** (was eager `void initSentry()` call) — now
     gated on `import.meta.env.PROD === true || localStorage
     .SENTRY_DEBUG === '1'`.  Vite splits the wrapper into
     `sentry.<hash>.js` (**2,304 B**) as a sibling of the
     existing 342 KB SDK chunk.

   `autotable-src-eager` chunk size:

   | Wave | bytes | Δ |
   |------|-------:|---:|
   | W15 | 222,847 |   — |
   | W16 | **214,202** | **−8,645 B** |

   Cold-path delta on bare `/` lobby (no DSN, no action=):
   −8,645 B saved (~8.4 KB).  When the action-router is
   eventually needed, the 8.2 KB chunk loads then — never on
   the cold path.

   §3.5 actuality vs. W15 estimate: the W15 doc estimated
   "0 KB DSN / 342 KB no-DSN" assuming the SDK was eager.
   Empirically, the W15 SDK chunk was ALREADY lazy (inner DSN
   check gated `await import('@sentry/browser')`).  Realistic
   W16 §3.5 delivery: ~3 KB drop on the eager wrapper.  Full
   table + W17 next-step (§3.8 `SENTRY_DEFER_INIT` flag) in
   `docs/frontend-bundle-audit.md §4.1`.

4. **three-renderer hold-line — 6th consecutive wave.**

   `three-renderer-big`: **406,635 B** (unchanged from W13
   calibration; held W14 / W15 / W16).  No quick wins surfaced
   during the W16 work; the chunk shape is W6-era three.js
   tree-shake-resistant code (the Phase L renderer-webgl2 path
   is the strategic exit).

   The hold-line is the intentional bandwidth-shift to Phase L
   (see W14 spike doc §3): every kilobyte of engineering time
   spent on the W6-era three-renderer chunk is one less spent
   on the eventual replacement.  W16 is the **6th** consecutive
   hold-line wave (W11 → W16).

### Cross-lane hygiene

The W16 commit touches ONLY Hicks-lane paths:
- `src/frontend/autotable-src/src/index.ts`
- `src/frontend/autotable-src/src/renderer-webgl2/*.ts` (new + edits)
- `src/frontend/autotable-src/scripts/append-dist-size.js`
- `src/frontend/autotable-src/dist-size.json`
- `docs/frontend-bundle-audit.md`
- `docs/lh13-soft-pin-rationale.md` (new)
- `Phase_K_W16/Hicks/{charter,history}.md` (new)
- `.squad/decisions/inbox/hicks-phase-k-wave-16.md` (new, force-added)

No `.github/workflows/pwa-audit.yml` edits — the soft-flip is
documentation-only.  No `src/frontend/autotable-src/tests/`
edits — the W16 e2e smoke spec is held in `Phase_K_W16/Hicks/`
under the wave_subdir_override rule (hicks-lane attribution)
until Vasquez W17+ migrates it into `tests/e2e/`.

### Hand-off

W17 picks up the LH13 hard-pin (if §4.2 evidence converges),
Phase L W3 (animation graph), and the W16-deferred bundle-
audit candidates §3.6 / §3.7 / §3.8.  See
`.squad/decisions/inbox/hicks-phase-k-wave-16.md` for the
full hand-off memo.
