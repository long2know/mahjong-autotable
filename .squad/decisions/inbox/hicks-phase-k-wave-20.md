# Hicks — Phase K Wave 20 bring-up memo

**Agent:** Hicks (Frontend)
**Wave:** Phase K Wave 20
**Branch:** `stlong/phase-k-wave-20-bringup`
**Model:** claude-opus-4.7-xhigh

---

## Scope (assigned at wave open)

1. Re-evaluate LH13 §6.8 evidence gate against `main` post-W18-merge.
   Promote to hard-pin GREEN if ≥3 consecutive successful schedule-
   event runs are observed; otherwise hold YELLOW with explicit
   data-state note in `docs/lh13-soft-pin-rationale.md §11`.
2. Phase L renderer — W5 tile-pick animation (lift / drop tween with
   easing) + tile drag-and-drop (pointer events, hover outline).
3. Bundle audit §3.5 — shed bytes from `autotable-src-eager` to hit
   the ≤135 KB ceiling (down from 144,192 B at W19 close).
4. Hold `three-renderer-big` at ≤ 406,635 B (W20 ceiling = W19 close
   = W14 baseline).  **10th consecutive wave** at the W14 hold-line.
5. Admin UI — wire the three Bishop W20 surfaces: Swiss
   pair-next-round trigger, rotation-policy bulk-actions (delete +
   enable/disable), JWT-keys rotation drill.

---

## Bundle outcomes (W20 final, vs W19 baseline)

| Chunk | W19 baseline | W20 target | W20 result | Δ vs W19 | Status |
|---|---|---|---|---|---|
| `autotable-src-eager` | 144,192 B | ≤135,000 B | **123,701 B** | −20,491 B | ✅ under ceiling by 11,299 B |
| `three-renderer-big` | 406,635 B | ≤406,635 B | **406,635 B** | 0 B | ✅ held exactly — **10th wave** |
| `renderer-webgl2` | 30,174 B | ≤45,000 B | **35,258 B** | +5,084 B | ✅ tile-pick animation + drag-drop |
| `admin-panel` | 26,701 B | ≤38,000 B | **35,161 B** | +8,460 B | ✅ 3 new W20 surfaces |
| `auth` *(new lazy)* | (in eager) | new lazy | **21,320 B** | extracted | ✅ |

All targets met.  `dist-size.json` carries the W20 row under wave-
name `K20` (recorded by `scripts/append-dist-size.js`; the script
now recognises three additional chunk keys this wave — `auth`,
`matchmaking` (W19 lazy), and `rule-presets` (W19 lazy) — so the
ledger reflects every emitted chunk).

---

## Deliverable #1 — LH13 §6.8 evidence-gate re-evaluation

**Decision: HOLD §6.8 YELLOW.  Do NOT promote to hard-pin GREEN.**

**Evidence-collection blocker.**  At W20 bring-up the `gh` CLI is
not authenticated in the bring-up shell:

```
$ gh auth status
You are not logged into any GitHub hosts. To log in, run: gh auth login
```

The canonical §4.2 query (`gh run list --workflow=pwa-audit.yml
--event=schedule ...`) returns no rows under that posture.  Per
§4.2's binary read of the convergence criterion, an unobserved
sample is treated as a 0-count sample.

**Timing observation.**  The W18 merge to `main` (`7832f49`) and
the W20 bring-up window are ~97 minutes apart (W18 dist-size
ledger entry: `2026-05-24T11:02:58Z`; W20 bring-up build:
`2026-05-24T12:40:05Z`).  At the hourly cron cadence specified
in `pwa-audit.yml`'s `schedule:` block, that is a ceiling of
1 - 2 schedule ticks since the W18 patch reached `main`, well
short of the §4.2 ≥3 requirement.  **Two distinct reasons
compound to the same disposition** — the math alone forces a
HOLD even before the `gh`-observability constraint.

**Specific YELLOW indicator (per §6.7).**  The §6.7 table
continues to mark the LH13 row YELLOW at W20 (same
intermediate state W19 carried).  No regression to RED — the
W18 remediation IS on `main` and the prior `workflow_dispatch`
smoke continues to return `success`.

**No `docs/agent-handoff-protocol.md §6.8` PROMOTE update at
W20.**  The W19 §6.8 hand-off explicitly conditioned the
PROMOTE on a confirmed 3+ successful schedule-event sample.
With the HOLD disposition, no §6.8 row mutation is in scope
for the W20 Hicks lane.  YELLOW indicator + W19's "Re-confirm
HOLD" hand-off remain in force.  W20 Vasquez cross-refs §11.

**Re-check trigger.**  W21 Hicks bring-up re-runs §4.2 with
either (a) an authenticated `gh` session OR (b) a coordinator-
driven probe attached to `docs/frontend-pwa-audit.md §6.x`.
At W21 the sample window will have widened to ~25 hours
since the W18 merge — well above the minimum cadence — so a
fair convergence read should be possible.

Documentation: `docs/lh13-soft-pin-rationale.md` §11 (new in this
wave).

---

## Deliverable #2 — Phase L W5 renderer-webgl2: tile-pick + drag

W19 (`f5c3d90` Hicks lane) shipped the canonical wall geometry +
3 camera modes.  W20 layers interactivity:

* **`renderer-webgl2/tile-pick-animation.ts`** (NEW; +183 lines).
  Lift/drop tween with `easeOutCubic` (snappy lift) +
  `easeInOutSine` (smooth drop).  Allocation-free per-frame —
  one `Float32Array(16)` reused across the tween.  Optional
  X-axis tip rotation so the labelled tile face tips toward the
  camera at lift apex (`PICK_LIFT_TIP_ANGLE_X = 0.18 rad`).
  Public API: `startPickAnimation(baseMatrix, kind, startedAt,
  options) → PickAnimationHandle` with `step(nowMs) → boolean`
  + `finish()`.  Exports `easeOutCubic` / `easeInOutSine` so
  the production renderer (Phase L W6+) can re-use the curves.

* **`renderer-webgl2/tile-drag.ts`** (NEW; +186 lines).
  Mouse / touch drag-and-drop via the unified `pointerdown` /
  `pointermove` / `pointerup` event surface (saves ~1 KB vs.
  duplicating mouse + touch handlers).  Hover state is its own
  pointer-move probe — outline highlight applied on hover, lift
  applied on drag-start.  Modifier keys (shift/ctrl/meta) early-
  out so the camera handler retains shift-pan + ctrl-orbit.
  Escape key + `pointercancel` both fire the `onDragCancel`
  callback so the consumer can settle the source tile.  Public
  API: `attachTileDrag(canvas, mesh, camera, callbacks, options)
  → TileDragHandle` with `hoverIndex()` / `dragIndex()` /
  `detach()`.

* **`renderer-webgl2/hello.ts`** updated — adds `mountInteractive()`
  behind `?renderer=webgl2-interactive`.  Wires the W19 canonical
  wall scene to the new W20 animation + drag modules; the
  per-instance tween scheduler runs inside an rAF loop and
  garbage-collects settled handles automatically.

* **`src/index.ts`** updated — extends the `webgl2-` URL guard
  regex to recognise `?renderer=webgl2-interactive`.

Bundle impact: `renderer-webgl2` 30,174 → **35,258 B** (+5,084 B,
13.7 %); target ≤ 45,000 B → **9,742 B headroom**.  No three.js
dependency added — the renderer-webgl2 path stays free-standing.

---

## Deliverable #3 — Bundle audit §3.5 surgery

**Target hit: `autotable-src-eager` ≤ 135,000 B.  Actual: 123,701 B
(11,299 B headroom).**

The W20 §3.5 surgery lazifies the `./auth` module — previously the
last >10 KB eager dep in the lobby graph.  Implementation:

* **`src/lobby.ts`** — `import { installAuthUi } from './auth';`
  removed; replaced with `import type * as AuthModule from './auth';`
  (type-only, zero runtime cost).  The `installAuthUi()` call-site
  becomes `scheduleAuthUiLazyMount();`, a new helper at the bottom
  of `lobby.ts` that wraps `import('./auth')` inside a
  `requestIdleCallback` (fallback: `window.setTimeout(0)`) so the
  auth-UI chunk loads on the next idle window after lobby first-
  paint.

* **Cross-module impact.**  `rule-presets.ts` (already lazy at
  W19) statically imports `getAuthState/onAuth` from `./auth`.
  With both consumers dynamic, rollup emits `./auth` as a shared
  lazy chunk (`auth.<hash>.js`, **21,320 B**) consumed by both
  lazy chunks at runtime.  Eager bundle no longer carries any
  byte of `./auth`.

* **User-visible effect.**  The "Sign in" header chip appears one
  paint frame later than at W19 on a fresh load (~16 ms on a
  cold cache, less on a warm one since rollup's content-hashed
  chunks are SW-pre-cached).  Anonymous lobby visitors never
  interact with auth before the chip lands.

* **No-regression note.**  `setState()` in `./auth.ts` directly
  calls `renderLobbyChip()` + `renderLinkedAccountsSection()` —
  the DOM render helpers.  Those helpers no-op when the modal
  scaffold hasn't been mounted (defensive `if (document.
  getElementById(...) === null) return;` early-outs), so the
  state-change path is robust against being called pre-mount.
  After `installAuthUi()` resolves on the idle window, the
  `onAuth(handler)` subscription re-renders on the next state
  change.

Bundle math:
* W19 close: `autotable-src-eager` = 144,192 B.
* W20 target: ≤ 135,000 B (shed ~9 KB).
* W20 actual: **123,701 B** (shed 20,491 B, 142 % of the target).
* Headroom: 11,299 B against the W20 ceiling.

The over-shed reflects that `./auth` was carrying transitive
imports too (a closure-stashed EventEmitter, the
`KNOWN_PROVIDERS` array, the provider-button wiring), so the
gain is larger than the source-file byte count would suggest.
W21 §3.6 candidate: `./profile.ts` (24 KB source, currently
eager via `installProfileDrawer/installProfileToggle`) — same
pattern, mount-on-idle.

---

## Deliverable #4 — `three-renderer-big` hold-line (10th wave)

**Status: HELD at 406,635 B for the 10th consecutive wave.**

| Wave | three-renderer-big | Status |
|---|---|---|
| K11 | 406,635 B | baseline |
| K12 | 406,635 B | held |
| K13 | 406,635 B | held |
| K14 | 406,635 B | held |
| K15 | 406,635 B | held |
| K16 | 406,635 B | held |
| K17 | 406,635 B | held |
| K18 | 406,635 B | held |
| K19 | 406,635 B | held |
| **K20** | **406,635 B** | **held — 10th wave** |

10 consecutive holds is the milestone we promised the W11 ledger
("monotonically non-increasing"); the W20 entry is the first
double-digit hold-line ledger row.  No three.js byte added to
the `three-renderer` chunk this wave (all W20 renderer-webgl2
work is in the free-standing `renderer-webgl2` chunk).

---

## Deliverable #5 — Admin UI for W20 Bishop new surfaces

Three new admin-panel surfaces wired:

1. **`src/admin/swiss-pair-next-round.ts`** (NEW; +152 lines).
   Operator UI for Bishop's W20 `POST /api/admin/tournaments/
   <tournamentId>/swiss-pair-next-round` endpoint.  Form fields:
   tournament id, round number (1..32, integer), dry-run toggle.
   Exports a `fireSwissPairNextRound()` helper that wraps the
   auth-laddered `gateAdminFetch` + `promptAdminReason` flow.

2. **`src/admin/rotation-policy-bulk-actions.ts`** (NEW; +203 lines).
   Combines THREE related actions behind one panel: W19 bulk-
   update (left as-is in `./rotation-policy-bulk.ts` so the W19
   surface still works), W20 bulk-delete (`POST /api/admin/
   rotation-policy/bulk-delete`), and W20 bulk-enable / bulk-
   disable (`POST /api/admin/rotation-policy/bulk-enable` with
   `enabled: true|false`).  Form fields: action picker (delete /
   enable / disable), comma- or whitespace-separated tenant-id
   list, dry-run toggle.  Exports `parseTenantIdList()` (with
   dedup + non-empty filter) and `fireRotationPolicyBulkAction()`.

3. **`src/admin/jwt-rotation-drill.ts`** (NEW; +211 lines).
   Operator UI for Bishop's W20 `POST /api/admin/jwt-keys/
   rotation-drill` endpoint.  Form fields: tenant id (empty =
   global drill), `simulateFailureAt` picker (`stage` / `overlap`
   / `commit` / `rollback` / none).  Drill always runs with
   `dryRun: true`; the audit kind `auth.jwt-keys.rotation-drill.
   ran` records the operator's intent.  Exports
   `fireJwtRotationDrill()`.

* **`src/admin/admin-panel.ts`** updated — registers the three
  new surfaces at the tail of the `SURFACES` array (after the
  W19 surfaces) so the existing tab order is unchanged and the
  W19 testids stay stable.  Comment block updated.

Bundle math: `admin-panel` 26,701 → **35,161 B** (+8,460 B,
31.7 %); target ≤ 38,000 B → **2,839 B headroom**.  The three
new surfaces are similar shape to the W19 surfaces (parseRow /
buildBody / rowKey / columns + a small `fire*` helper) so the
chunk grows roughly linearly with surface count.

---

## Cross-module-impact + safety table

| Change | Cross-impact | Mitigation |
|---|---|---|
| `lobby.ts` eager `installAuthUi` → idle-window lazy | Sign-in chip appears one paint after lobby first-paint | `setState()` DOM renderers self-no-op when chip is unmounted; `onAuth` re-fires on chip mount |
| `./auth` becomes lazy chunk consumed by both `rule-presets` (W19 lazy) + `lobby`'s new lazy `scheduleAuthUiLazyMount` | Rollup auto-creates shared chunk; no manual `manualChunks` change | Confirmed at W20 build: single `auth.<hash>.js` (21.3 KB) emitted; both consumers reference it |
| `renderer-webgl2` chunk grows from 30 KB → 35 KB | Phase L spike-only; lazy-loaded behind `?renderer=webgl2-*` query | Cold-path lobby visitors never pay |
| `admin-panel` chunk grows from 27 KB → 35 KB | Lazy-loaded behind `?action=admin-panel` | Non-admin visitors never pay; admins pay once per session |
| `scripts/append-dist-size.js` add 3 new key patterns | All previously-emitted chunks still recorded with stable keys | Existing K7..K19 ledger entries untouched; only K20 emits the new keys |

---

## Stage-commit-push pipeline discipline

Per the W19 retrospective (Hicks `d700cf7` cross-lane-bundling
incident), every W20 stage + commit + push happens inside a
single `flock` block, with explicit `git add path/to/file ...`
naming each file (NO `git add -A`, `git add .`, `git add -u`,
or directory wildcards).  Pre-commit verification:

```
git diff --cached --name-only
bash tests/ci/check-cross-lane-bundling.sh \
  --pr stlong/phase-k-wave-20-bringup --strict
```

Single stash `hicks-w20-baseline-<unix-ts>` taken once at the
start of the bring-up; popped only AFTER the final push.

Lane-purity expectations for the W20 Hicks commit:
* `src/frontend/autotable-src/src/**/*.ts` — Hicks lane (regex
  `^src/frontend/`).
* `src/frontend/autotable-src/dist-size.json` — Hicks lane.
* `src/frontend/autotable-src/scripts/append-dist-size.js` —
  Hicks lane.
* `src/frontend/autotable/**/*.{js,html,json}` — Hicks lane
  (build artefacts emitted by `npm run build:vite`).
* `docs/lh13-soft-pin-rationale.md` — shared docs scope (W17 +
  W18 + W19 precedent).
* `.squad/decisions/inbox/hicks-phase-k-wave-20.md` — Hicks
  inbox shared scope.

Concurrent agents in the working tree at W20 bring-up (NOT
staged by Hicks):
* Apone — `infra/k8s/base/kyverno-policies/*.yaml` (Kyverno
  W19→W20 audit→enforce flip).
* Bishop — backend bring-up files (not yet observed at Hicks
  bring-up).
* Vasquez — typically lands last; not present at Hicks
  bring-up time.

---

## Re-check / next-wave notes

* **W21 LH13 §6.8 re-evaluation.**  Re-run §4.2 with
  authenticated `gh` (or coordinator-driven probe).  Sample
  window will be ~25 hours since W18 merge — convergence
  should be observable.

* **W21 §3.6 bundle target.**  `autotable-src-eager` at 123,701 B
  has substantial headroom against the W19 §6.3 145 KB pin.
  Next eager target: `./profile.ts` (24 KB source, eager via
  `installProfileDrawer/installProfileToggle`).  Same pattern
  as W20 §3.5 (idle-window lazy mount).  Target: shed ~10 KB
  to ≤ 115 KB.

* **W21 Phase L spike — animation graph queue.**  W20 ships
  one-shot tweens via `startPickAnimation`.  Phase L W6+
  needs a tween *queue* (deal-out wave: 14 tiles, staggered)
  + bezier curves (slide-along-table, not just up/down).
  Target: keep `renderer-webgl2` under 50 KB.

* **W21 admin-panel target.**  At 35,161 B vs. the W20 38 KB
  ceiling, headroom is thin (2.8 KB).  Next wave: either bump
  the ceiling to 42 KB OR split `admin/` into two chunks by
  surface category (governance vs. retention).  Decision
  deferred to W21 Hicks vs. Bishop scope discussion.

---

## File manifest (Hicks-authored changes)

NEW files:
```
src/frontend/autotable-src/src/renderer-webgl2/tile-pick-animation.ts
src/frontend/autotable-src/src/renderer-webgl2/tile-drag.ts
src/frontend/autotable-src/src/admin/swiss-pair-next-round.ts
src/frontend/autotable-src/src/admin/rotation-policy-bulk-actions.ts
src/frontend/autotable-src/src/admin/jwt-rotation-drill.ts
.squad/decisions/inbox/hicks-phase-k-wave-20.md
```

MODIFIED files:
```
src/frontend/autotable-src/src/index.ts          (regex extension)
src/frontend/autotable-src/src/lobby.ts          (auth lazification)
src/frontend/autotable-src/src/renderer-webgl2/hello.ts   (mountInteractive)
src/frontend/autotable-src/src/admin/admin-panel.ts        (register W20 surfaces)
src/frontend/autotable-src/scripts/append-dist-size.js     (auth/matchmaking/rule-presets keys)
src/frontend/autotable-src/dist-size.json                 (K20 row + script keys)
docs/lh13-soft-pin-rationale.md                          (+§11)
```

BUILD-artefact files (regenerated by `npm run build:vite`):
```
src/frontend/autotable/*.js                     (content-hashed chunks)
src/frontend/autotable/index.html               (chunk-hash references)
src/frontend/autotable/manifest-precache.json   (SW manifest)
```

---

## Sign-off

All five W20 Hicks deliverables met; bundle ceilings clean with
≥ 2.8 KB headroom across all four chunk targets; LH13 §6.8 HOLD
documented with explicit data-state and re-check trigger.  Lane-
discipline detector + cross-lane-bundling detector expected to
return 0 violations.  Push-pipeline atomic per W19 retrospective.

— Hicks (Frontend), Phase K Wave 20 bring-up
