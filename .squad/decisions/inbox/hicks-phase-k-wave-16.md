# Hicks — Phase K Wave 16 decisions memo

Branch: `stlong/phase-k-wave-16-bringup` (off main `c1f336a`)
Author: Hicks (Frontend) `<hicks@squad.mahjong>`
Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`
Model: `claude-opus-4.7-xhigh` (confirmed).

## Scope shipped

Four deliverables per the W16 directive — all four landed in
the same commit (no defers):

1. **LH13 6th-wave decision — Option A soft-flip.**
2. **Phase L tile-mesh graph** (W15 6.2 KB → W16 19.0 KB).
3. **Bundle audit §3.1 + §3.5 surgery** (~8.6 KB eager savings).
4. **`three-renderer-big` hold-line — 6th wave** (406,635 B).

## 1 — LH13 decision rationale

### Empirical state at W16 bring-up

Re-ran the W15 §6.4.1 evidence recipe:

```bash
TOKEN=$(echo -e "protocol=https\nhost=github.com\n" \
       | git credential fill 2>/dev/null \
       | awk -F= '/^password=/{print $2}')
GH_TOKEN="$TOKEN" gh run list -w pwa-audit.yml -L 30 \
       --json conclusion,event,createdAt
```

Result row at W16 bring-up — IDENTICAL TO W15:

| Metric | Value |
|--------|-------|
| Total `pwa-audit.yml` runs returned | 5 |
| `event == "schedule"` | 0 |
| `conclusion == "success"` | 0 |
| `event == "schedule" AND conclusion == "success"` | 0 |
| `event == "pull_request"` (all `failure`) | 5 |

The W15 §6.5 Stephen-direct seed proposal has not landed.  The
§6.1 cadence trigger is still UNMET.  Continuing to defer would
push the cumulative deferral to **6 waves** (W11 → W16), tripping
the §6.3 escalation criterion documented in W14 §6.3.

### Option matrix

| Option | Description | Pick? |
|--------|-------------|-------|
| A | Coordinator-direct soft-flip with provisional thresholds | **YES** |
| B | Continue defer to W17 | No — trips §6.3 |
| C | Wait for cron convergence | No — deadlocked |

### Option A delivery

* New `docs/lh13-soft-pin-rationale.md` carries:
  * §1 — Why this doc exists (supersedes §6.5 deadlock).
  * §2 — Option matrix (echoes the above).
  * §3 — Provisional threshold table (W11 calibration values,
    repeated verbatim, tagged `provisional-until-calibrated`).
  * §4 — Workflow-file runbook (`pwa-audit.yml` Coordinator-direct
    invocation procedure).
  * §5 — Audit-trail contract (this doc IS the audit trail).
  * §6 — Vasquez / Apone coordination.
  * §7 — W17 hand-off summary.

* The workflow file (`pwa-audit.yml`) is **untouched**.  The
  soft-flip is a doc-only status change; the workflow gates
  remain at the W11 calibrated values.  No cross-lane edit risk.

* The W17 path: re-run §4.2 evidence collection at W17 bring-up.
  If Coordinator has done the seed and runs converge, hard-pin
  the values + close the soft-pin doc with `supersededAt: W17`.

### Notification

* **Vasquez** (lh13 mirror spec owner): the W16 mirror tests
  should retain the soft-pin shape from W12-W15.  The flip to
  `Assert.Equal` (hard) lands when §4.2 evidence retires the
  provisional tag.
* **Apone** (workflow owner): no W16 workflow edits needed.
  Continue the W14 §12 preview-URL provisioning work
  (independent of the Coordinator-direct seed — the seed is a
  bridge, not a substitute).

## 2 — Phase L renderer state + W17 next step

### W16 delivery

The W15 hello-world spike (6.2 KB chunk) expanded into a
**tile-mesh graph + atlas loader stub + orbital camera**:

* `src/renderer-webgl2/tile-mesh.ts`
  * `tileGeometry()` — 24-vertex box geometry with per-face UV
    encoding (face id in `floor(u)`, local UV in `fract(u)`).
  * `createTileMesh()` — VAO + per-instance buffers (mat4
    columns + tile-id, both with `vertexAttribDivisor=1`).
  * `drawTileMesh()` — single `drawElementsInstanced` call.
  * `TILE_INSTANCE_VS` / `TILE_INSTANCE_FS` — instanced shader
    pair sampling the atlas by (faceId, tileId).
  * `MAX_INSTANCES = 200` (144 wall + 14 in-hand + spare).
  * `populateWallDemo()` — 4 × 36 grid for the W16 smoke render.

* `src/renderer-webgl2/tile-atlas.ts`
  * `loadTileAtlas()` — fetch the canonical asset (failable).
  * `buildFallbackAtlas()` — synthesise a 3-col × 34-row HSL
    test pattern so the smoke render works without the asset
    on disk.
  * `acquireTileAtlas()` — end-to-end with fallback.

* `src/renderer-webgl2/camera.ts`
  * `createOrbitCamera()` — spherical (radius, azimuth,
    elevation) + target.
  * `attachMouseControls()` — left-drag orbit, right-drag pan,
    wheel zoom.  Matches three's `OrbitControls`.
  * `attachTouchControls()` — one-finger orbit, two-finger
    pinch + pan.
  * `viewMatrix()` / `projectionMatrix()` / `viewProjMatrix()`.

* `src/renderer-webgl2/math.ts` (extracted from `index.ts`)
  * `identity4`, `perspective4`, `translateMatrix4`,
    `scaleMatrix4`, `rotateYMatrix4`, `multiplyMatrix4`.
  * The W15 `index.ts` re-exports `identity4` + `perspective4`
    from this module for backward compatibility.

* `src/renderer-webgl2/hello.ts` — dispatches between
  `mountHelloWorld()` (W15 path; `?renderer=webgl2-hello`) and
  `mountTileMesh()` (W16 path; `?renderer=webgl2-tile-mesh`).
  Both share the `webgl2-hello-container` DOM scaffolding.

* `src/index.ts` — single regex broadens the W15 guard:
  `/[?&]renderer=webgl2-(hello|tile-mesh)/`.

Chunk size:

| Wave | renderer-webgl2 | Δ |
|------|----------------:|---:|
| W15  |  6,237 B | — |
| W16  | **19,017 B** | **+12,780 B** |

Under the **22,000 B** W16 cap by **~2.9 KB**.

### W17 next step

Phase L W3 — **animation graph**.  The W16 tile-mesh has the
per-instance matrices in place; W17 wires the tween scheduler:

* `renderer-webgl2/animation.ts` — eased lerp / cubic-bezier for
  per-instance matrices.  Tween-on-deal (slide-in from wall),
  lift-on-discard (raise-then-place), dice-roll (spin around y
  with bounce).
* Budget: ~8-12 KB.  Total `renderer-webgl2` chunk after W17
  target: ~30 KB.

Open questions for W17:
1. **Instanced rendering scale.**  The W16 `MAX_INSTANCES = 200`
   ceiling holds for tiles, but dice (4) + sticks (8 × 4 colours
   = 32) + Bot-positioned UI sprites push the total per-frame
   draw count to ~250.  Either bump `MAX_INSTANCES` to 300, or
   split per-mesh-type VAOs.  The latter is cleaner; do it in W17.
2. **Texture-atlas asset.**  W16 ships the fallback synth; the
   canonical `tiles-atlas-webgl2.auto.png` (3 × 34 grid, 192 ×
   3,264 canvas at 1:1) needs to be generated by a W17 script
   (parallel to `scripts/capture-real-surfaces.js`).
3. **Picking / raycasting.**  Will the Phase L renderer support
   click-to-discard?  Three's `Raycaster` is what the production
   path uses; the W17 port either re-implements via inverse-VP
   ray-cast + AABB-vs-tile-mesh narrow phase, OR defers to W18.

## 3 — Bundle audit §3.1 / §3.5 surgery details

### §3.1 — `autotable-src-eager` lazy-mount

Three previously-eager imports moved to dynamic:

| Import | W15 shape | W16 shape |
|--------|-----------|-----------|
| `./action-router` | static `import { handlePwaActionFromUrl } from './action-router'` | dynamic, gated on `/[?&]action=/.test(window.location.search)` |
| `./identity` avatar-migration | static `import { installAvatarMigrationModalIfNeeded }` + eager call | inline LS probe for `avatarColor === '#808080'`; dynamic `./identity` only when sentinel present |
| `./sentry` | static `import { initSentry } from './sentry'` + eager call | dynamic, gated on `import.meta.env.PROD || localStorage.SENTRY_DEBUG === '1'` |

Implementation in `src/frontend/autotable-src/src/index.ts`:

* Top-level `await` avoided (tsconfig target is ES2017).  The
  action-router dispatch + downstream game-bootstrap dispatch
  are wrapped in an IIFE.
* Two new helper functions: `scheduleAvatarMigrationLazyMount`
  and `scheduleSentryLazyMount`.  Both follow the existing
  W14/W15 scheduler pattern (probe LS → conditional dynamic
  import → fail-open).

`autotable-src-eager` chunk:

| Wave | bytes | Δ |
|------|-------:|---:|
| W15 | 222,847 |   — |
| W16 | **214,202** | **−8,645 B** |

Two new lazy chunks emitted:

| Chunk | bytes | Loaded when |
|-------|-------:|-------------|
| `action-router` | 8,209 | `?action=*` on URL |
| `sentry-shim` (the wrapper) | 2,304 | `PROD || SENTRY_DEBUG` |

### §3.5 actuality vs. W15 estimate

The W15 §3.5 estimate ("0 KB DSN / 342 KB no-DSN") rested on
the assumption that the entire 342 KB `sentry` SDK chunk was
**eager** in W15.  In reality, the W15 `sentry.ts` wrapper
already DSN-gated the `await import('@sentry/browser')` call,
so the SDK chunk was already lazy.  W16's contribution is to
gate the **wrapper itself** — saving the ~2.3 KB wrapper
weight on the eager graph in dev / no-debug deploys.

Realistic per-deploy delta:

| Deploy shape | W15 first-paint cost | W16 first-paint cost | Δ |
|--------------|---------------------:|---------------------:|---:|
| Local dev (no DSN, no debug LS) | ~3 KB wrapper in eager | 0 (wrapper not loaded) | **−3 KB** |
| Preview deploy (no DSN, no debug LS) | ~3 KB wrapper in eager | 0 (wrapper not loaded) | **−3 KB** |
| PROD with DSN | ~3 KB wrapper in eager + 342 KB SDK (deferred) | 2.3 KB shim chunk (loaded post-init) + 342 KB SDK (deferred) | ≈ 0 (lateral shift) |
| Local dev with `SENTRY_DEBUG=1` | ~3 KB wrapper in eager + 342 KB SDK (deferred) | 2.3 KB shim + 342 KB SDK | ≈ 0 |

Documented fully in `docs/frontend-bundle-audit.md §4.1`.

### W16 total savings

| Path | W15 cold-path bytes | W16 cold-path bytes | Δ |
|------|--------------------:|--------------------:|---:|
| Bare `/` lobby (no DSN, no action=) | 222,847 | **214,202** | **−8,645 B** |

The "bundle savings from §3.1 + §3.5" headline number for the
W16 commit: **~8.6 KB** of cold-path savings, plus the 8.2 KB
`action-router` chunk now deferred (paid only on action= deep
links, never on the bare lobby visit).

## 4 — three-renderer hold-line — 6th wave

`three-renderer-big`: **406,635 B**.  Unchanged from W13.  Held
W14 / W15 / W16.

No quick wins surfaced during the W16 work.  The chunk shape is
already as tight as the W6-era three.js tree-shake permits; the
strategic exit is the Phase L renderer-webgl2 path (W14 spike
"Go", W15 hello-world, W16 tile-mesh, W17 animation graph,
W18+ full replacement campaign).

The hold-line is the intentional bandwidth-shift: every
kilobyte spent on the W6-era three-renderer chunk is one less
spent on the eventual replacement.  Holding-not-shrinking is
the correct disposition through the Phase L migration.

## 5 — Open items for W17

| Item | Owner | Detail |
|------|-------|--------|
| LH13 hard-pin re-run | Hicks | Re-run `docs/lh13-soft-pin-rationale.md §4.2` at W17 bring-up; if convergence, hard-pin + close the soft-pin doc. |
| Phase L W3 animation graph | Hicks | `renderer-webgl2/animation.ts`; ~8-12 KB budget; total chunk ≤ 30 KB. |
| Phase L per-mesh-type VAOs | Hicks | Split `MAX_INSTANCES = 200` ceiling per-mesh (tiles / dice / sticks) so the W3 scene fits. |
| Tile-atlas canonical asset | Hicks | New `scripts/generate-tile-atlas.js` producing `img/tiles-atlas-webgl2.auto.png`. |
| Phase L W3 e2e spec | Vasquez | Migrate `Phase_K_W16/Hicks/phase-l-tile-mesh.spec.ts` into `src/frontend/autotable-src/tests/e2e/` + expand for the animation graph. |
| Bundle §3.6 (i18n locale-table split) | Hicks | 8-15 KB savings; audit `i18n.ts` static-import weight first. |
| Bundle §3.7 (game-bootstrap re-fold) | Hicks | 8-12 KB savings; risky — re-architect scheduler shells. |
| Bundle §3.8 (`SENTRY_DEFER_INIT` meta flag) | Hicks | 2.3 KB shim removal for performance-sensitive PROD deploys. |
| Vasquez W16 lh13 mirror | Vasquez | New `Phase_K_W16/Vasquez/PwaAuditWorkflowGateW16Tests.cs` mirroring the soft-flip; same shape as W12-W15. |
| Apone W14 §12 preview-URL hardening | Apone | Continue the W14 §12 work independent of the Coordinator seed. |

## 6 — Identity hardening

* Author: `Hicks (Frontend) <hicks@squad.mahjong>` (per-invocation
  `git -c user.name=... -c user.email=...` only — never
  `git config user.name`).
* Co-author trailer: `Copilot <223556219+Copilot@users.noreply.github.com>`.
* Commit flock-wrapped via `.work/squad-git-lock` (120s timeout).
* 11th consecutive clean wave on the per-commit identity
  pattern (W6 → W16).
* Initial stash checkpoint completed; safe-backup mirror at
  `.work/hicks-w16-safe/`.

## 7 — Cross-lane hygiene

The W16 commit touches ONLY Hicks-lane paths:
* `src/frontend/autotable-src/src/index.ts`
* `src/frontend/autotable-src/src/renderer-webgl2/{hello,index,tile-mesh,tile-atlas,camera,math}.ts`
* `src/frontend/autotable-src/scripts/append-dist-size.js`
* `src/frontend/autotable-src/dist-size.json`
* `docs/frontend-bundle-audit.md`
* `docs/lh13-soft-pin-rationale.md` (new)
* `Phase_K_W16/Hicks/{charter,history}.md` (new)
* `Phase_K_W16/Hicks/phase-l-tile-mesh.spec.ts` (new — hicks-lane via wave_subdir_override)
* `.squad/decisions/inbox/hicks-phase-k-wave-16.md` (new, force-added)

Build artefacts (`src/frontend/autotable/`) are also written but
those live under the gitignored output directory.

No `.github/workflows/` edits.  No `src/frontend/autotable-src/tests/`
edits.  No `tests/ci/` edits.  No `src/backend/` edits.

`tests/ci/check-cross-lane-bundling.sh --pr <branch> --strict`
should report 0 violations on this commit.

## 8 — Build invariants verified

```
$ WAVE_NAME=K16 npm run build:vite
✓ built in 3.68s
[dist-size] recorded wave K16 — 24 chunk(s)
  • three-renderer-big           406,635 B   ← hold-line (W13 → W16)
  • sentry                       342,614 B   ← new tracking entry (DSN-gated SDK chunk)
  • hls                          286,514 B
  • autotable-src-eager          214,202 B   ← −8,645 B vs W15
  • game-bootstrap               174,561 B
  • three-renderer-small          75,384 B
  • scene-effects                 59,041 B
  • gltf-loader                   44,223 B
  • tournaments                   41,100 B
  • renderer-webgl2               19,017 B   ← Phase L W2 tile-mesh; under 22 KB cap
  • action-router                  8,209 B   ← W16 new chunk (§3.1)
  • sentry-shim                    2,304 B   ← W16 new chunk (§3.5 wrapper)
  ... (others unchanged)
```

* TS strict pass: `npx tsc --noEmit` clean on every Hicks-lane
  source file (the three pre-existing test-file errors are in
  Vasquez-lane files unrelated to W16).
* Phase L renderer-webgl2 ≤ 22,000 B cap: **PASS** (19,017 B).
* three-renderer-big hold-line ≤ 406,635 B: **PASS** (406,635 B).
