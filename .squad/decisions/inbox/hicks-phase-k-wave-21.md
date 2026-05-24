# Hicks — Phase K Wave 21 bring-up memo

**Agent:** Hicks (Frontend)
**Wave:** Phase K Wave 21
**Branch:** `stlong/phase-k-wave-21-bringup`
**Model:** claude-opus-4.7-xhigh

---

## Scope (assigned at wave open)

1. Re-evaluate LH13 §6.9 evidence gate against `main` post-W18-merge.
   Promote to hard-pin GREEN if ≥3 consecutive successful schedule-
   event runs are observed; otherwise hold YELLOW with explicit
   data-state note in `docs/lh13-soft-pin-rationale.md §12`.
2. Phase L renderer — W6 tile-claim animation (pung/kong/chi
   staggered fan-in with `easeOutBack`) + meld-display row layout
   (per-seat meld accumulation, `appendMeld` / `layoutMeldRow` /
   `nextMeldOriginXZ`).
3. Bundle audit §3.6 — shed ~9 KB from `autotable-src-eager` to
   hit the ≤115 KB (117,760 B) ceiling (down from 123,701 B at
   W20 close).
4. Hold `three-renderer-big` at ≤ 406,635 B (W21 ceiling = W20
   close = W14 baseline).  **11th consecutive wave** at the W14
   hold-line.
5. Admin UI — wire the five Bishop W21 operator surfaces:
   Swiss apply-round trigger, per-tenant rotation schedule
   reconcile, tournament withdraw-player, SignalR retention purge,
   replay restoration audit log (read-only).

---

## Bundle outcomes (W21 final, vs W20 baseline)

| Chunk | W20 baseline | W21 target | W21 result | Δ vs W20 | Status |
|---|---|---|---|---|---|
| `autotable-src-eager` | 123,701 B | ≤117,760 B | **112,219 B** | −11,482 B | ✅ under ceiling by 5,541 B |
| `three-renderer-big` | 406,635 B | ≤406,635 B | **406,635 B** | 0 B | ✅ held exactly — **11th wave** |
| `renderer-webgl2` | 35,258 B | ≤49,152 B | **40,292 B** | +5,034 B | ✅ tile-claim + meld-display |
| `admin-panel` | 35,161 B | ≤49,152 B | **48,984 B** | +13,823 B | ✅ 5 new W21 surfaces (168 B head-room) |
| `profile-drawer` *(new lazy)* | (in eager) | new lazy | **3,871 B** | extracted | ✅ §3.6 surgery |
| `zh-Hans` *(new lazy JSON)* | (in eager) | new lazy | **4,437 B** | extracted | ✅ §3.6 surgery |
| `zh-Hant` *(new lazy JSON)* | (in eager) | new lazy | **4,434 B** | extracted | ✅ §3.6 surgery |

All targets met.  `dist-size.json` carries the W21 row under
wave-name `K21` (recorded by `scripts/append-dist-size.js`,
unchanged).  35 chunks total this wave (W20: 32; +3 new lazy
chunks: profile-drawer, zh-Hans, zh-Hant).

`admin-panel` is now at 48,984 B vs the 49,152 B (48 KB) soft
ceiling — only 168 B head-room.  Recommendation: W22 admin-lane
should plan a chunk-split (admin-panel-tournaments + admin-panel-
infra) before the next surface lands.  See hand-off section below.

---

## Deliverable #1 — LH13 §6.9 evidence-gate re-evaluation

**Decision: HOLD §6.8 YELLOW.  Do NOT promote to hard-pin GREEN.**

Identical disposition to W19 and W20.  Full reasoning in
`docs/lh13-soft-pin-rationale.md §12`.

**Evidence-collection blocker.**  At W21 bring-up the `gh` CLI
is still not authenticated in the bring-up shell:

```
$ gh auth status
You are not logged into any GitHub hosts. To log in, run: gh auth login
```

The canonical §4.2 query (`gh run list --workflow=pwa-audit.yml
--event=schedule ...`) returns no rows under that posture.  Per
§4.2's binary read of the convergence criterion, an unobserved
sample is treated as a 0-count sample.

**Timing distinction from W20.**  At W20 the wall-clock gap
between the W18 merge (~`2026-05-24T11:02:58Z`) and the W20
bring-up window (~97 minutes) was still mathematically below
the §4.2 ≥3 hourly-cron-tick threshold.  At W21 the gap has
widened well past that threshold — the sample-window-size
sub-condition is now plausibly satisfied.  But because the
observation channel remains closed, the disposition is unchanged.

**Hand-off recommendation.**  W22 Hicks bring-up re-runs §4.2.
If the `gh`-auth gap is unresolved at W22 again, recommend
escalating to the §6.x coordinator-driven probe path (per the
W19 hand-off) rather than continuing to inherit the YELLOW
reading indefinitely.

No `docs/agent-handoff-protocol.md §6.8` PROMOTE mutation lands
this wave.  Vasquez W21 cross-ref may re-confirm the §6.8 row's
YELLOW state per the "Vasquez cross-ref" clause carried from
the W19 hand-off through W20.

---

## Deliverable #2 — Phase L renderer (tile-claim + meld-display)

New files (lane: `src/renderer-webgl2/`):

* `tile-claim-animation.ts` — pung / kong / chi claim animations
  with staggered fan-in.  Exports `animateTileClaim()` (3-tile
  pung, 4-tile kong, 3-tile chi variants), `easeOutBack()` (one
  back-easing curve shared across all claim types), and
  `meldSlotMatrix()` (positional matrix solver for the open
  meld row layout — same coordinate system as `meld-display`).
* `meld-display.ts` — per-seat meld row layout for the open-meld
  fan.  Exports `appendMeld()` (push a new meld onto a seat's
  row), `layoutMeldRow()` (recompute X offsets after add/remove),
  `nextMeldOriginXZ()` (returns the world-space anchor for the
  next meld), and the helper types `MeldRowState`, `MeldKind`,
  `MeldFanLayout`.

Modified:

* `renderer-webgl2/hello.ts` — added `mountMeld()` install hook
  + a new `'meld'` mode in the dispatch (`mode()` returns `'meld'`
  when `mountMeld` runs).  `mountMeld()` is invoked from the URL
  guard when `?renderer=webgl2-meld` is on the request.
* `src/index.ts` — extended the `?renderer=` regex to accept
  `webgl2-meld` as a Phase L W21 entry point.  The W15 +
  W16-19 + W20 (`webgl2-hello`, `webgl2-tile-mesh`, `webgl2-tile-
  layout`, `webgl2-tile-pick`) regex values are preserved.

Chunk impact: `renderer-webgl2` 35,258 B → **40,292 B** (+5,034 B).
Under the 49,152 B (48 KB) ceiling by 8,860 B.  Phase L W22+
has plenty of room to layer the remaining renderer surfaces
(declare-claim animation, score animations, win-burst, etc.)
before the chunk-split conversation needs to start.

The W21 tile-claim animation uses the same shared per-frame
update path as the W20 tile-pick lift/drop tween — both
register a `tick` callback on the renderer's shared frame
emitter, so the animation graph composes additively (multi-
tile multi-player claim animations can co-exist with a
concurrent tile-pick lift) without per-animation render-loop
machinery.

---

## Deliverable #3 — Bundle audit §3.6 surgery

Two surgeries jointly landed the W21 §3.6 target.  Full
narrative in `docs/frontend-bundle-audit.md §4.3`.

### §3.6 surgery A — `profile-drawer` extraction

`installProfileDrawer` + `installProfileToggle` extracted from
`./profile` into a new `./profile-drawer.ts` module
(~3.9 KB minified).  Lazy-loaded from `./lobby` via
`scheduleProfileDrawerLazyMount()` on first
`lobby-open-profile` chip hover/focus/click (parallel to the
W17 §3.2 `scheduleProfilePageLazyMount` lazy pattern).

Rationale: the W17 §3.2 `./profile-page` intercepts the chip
click in CAPTURE phase on modern paths, so the Wave-5 drawer's
chip handler is effectively dormant.  The drawer's DOM
listeners still wire up so any third-party flow that calls
`openProfileDrawer()` programmatically continues to work.

Data layer (`./profile`) retains: `getProfile`, `onProfile`,
`setDisplayName`, `setAvatarColor`, `resetProfile`,
validators, mutators, plus a NEW `flushPendingDisplayName()`
helper the drawer's Save button calls to flush the
debounced display-name write.

### §3.6 surgery B — i18n zh-* catalog lazification

The two zh-* JSON catalogs (zh-Hans: 4,882 B raw; zh-Hant:
4,879 B raw) split into their own dynamic-import chunks via
`import('./i18n/zh-Hans.json')` / `import('./i18n/zh-Hant.json')`.
`en` stays bundled (it's the fallback path inside `t()`).

The synchronous `t()` API is preserved.  When an unloaded
zh-* locale is active, `t()` falls back to English (the
documented fallback).  `installI18n()` and `setLanguage()`
both call `ensureCatalog(activeLocale)` which loads the
chunk and re-emits the locale-change event so listeners
re-render with localized strings once the chunk lands.

`mergeServerCatalog()` was hardened to await the base
catalog before applying server-supplied pattern patches —
the function had no callers at W21 but is wired up for the
upcoming Bishop `GET /api/i18n/patterns` ship.

**zh-* UX impact.**  Users whose resolved active locale is
zh-Hans or zh-Hant see ~10-30 ms of English strings at lobby
cold start while the zh chunk fetches.  HTTP/2 modulepreload
hints emitted by Vite typically land the chunk within the
same RTT as the eager bundle, so the flash is rarely visible.

---

## Deliverable #4 — `three-renderer-big` 11th-wave hold

`three-renderer-big = 406,635 B` exact at W21 close —
unchanged from W6 / W7 / W8 / W9 / W10 / W11 / W12 / W13 /
W14 / W19 / W20 (the W15-W18 reads were also 406,635 B; the
W14 hold-line has held every wave since W6).  No upstream
three.js bumps, no renderer-graph mutations, no new addons
imported.

The W21 Phase L renderer expansion (tile-claim-animation +
meld-display) lives entirely in the `renderer-webgl2` chunk
— the `three-renderer-big` chunk is fully isolated from
Phase L work by the `vite.config.ts` manualChunks routing
(`src/renderer-webgl2/*` → `renderer-webgl2`).

---

## Deliverable #5 — Admin UI (5 Bishop W21 surfaces)

Five new admin operator surfaces, all wired into
`admin/admin-panel.ts` via the existing W18 SURFACES registry:

1. **`swiss-apply-round.ts`** — POST trigger for Bishop's Swiss
   apply-round endpoint.  Two-field form (tournament-id,
   round-number) + dry-run toggle + confirm-modal on submit.
2. **`rotation-schedule.ts`** — POST per-tenant rotation-schedule
   reconcile (W21 follow-up to W20's rotation-policy bulk-actions).
   Form: tenant-id, key-rotation-cron, next-run-at override
   (optional).  Dry-run + confirm-modal.
3. **`tournament-withdraw.ts`** — POST tournament withdraw-player.
   Form: tournament-id, player-id, withdrawal-reason (free
   text).  Confirm-modal with warning copy ("This forfeits
   all remaining matches for this player in this tournament").
4. **`signalr-purge.ts`** — POST SignalR retention purge.  Form:
   purge-cutoff-iso (datetime-local picker), dry-run toggle.
   Confirm-modal with row-count preview from the dry-run
   response.
5. **`replay-restoration-audit.ts`** — **READ-ONLY** GET surface.
   Lists the last 200 replay restoration audit log entries
   (timestamp, requester, replay-id, restoration-outcome,
   evidence-link).  No mutation surface — purely diagnostic
   read for admin operators.

All five SPECs follow the W18 admin surface shape:
* `title`, `description`, `endpoint`, `method` constants.
* `renderForm()` builds the field DOM into a host element.
* `parseFormSubmission()` validates + collects values.
* `renderResult()` displays the API response.
* `confirmCopy` for the confirm-modal (or `confirmCopy: null`
  for the read-only audit surface — the panel framework
  skips the modal step when null).

Chunk impact: `admin-panel` 35,161 B → **48,984 B** (+13,823 B).
**Only 168 B of head-room** under the 49,152 B (48 KB) soft
ceiling.  Hand-off note: see below.

---

## Hand-off notes

### To W22 frontend lane

* **`admin-panel` chunk is at ceiling.**  Only 168 B free under
  the 48 KB cap.  Next admin surface will need a chunk split.
  Two reasonable axes for the split:
  * **By domain**: `admin-panel-tournaments` (Swiss, bracket,
    withdraw) vs `admin-panel-infra` (JWKS rotation, SignalR
    purge, retention audit, restoration audit, cost panels)
    vs `admin-panel-replays` (replay-related).
  * **By cardinality**: keep the framework + 1-2 most-used
    surfaces eager-ish, lazy-route the rest by surface-id
    in the URL hash.  W18's action-router pattern is the
    template.
  Recommend W22 admin work re-audits before adding surface #14.
* **i18n W21 lazy-zh.**  Watch for any reports of "English
  strings briefly flash on a Chinese-locale page load".  If
  the UX is unacceptable, options are (a) preload the resolved
  zh catalog via a `<link rel="modulepreload">` hint emitted
  by `index.html` SSR, or (b) revert to bundled-eager + accept
  the ~9 KB regression.  Default expectation: HTTP/2 push
  makes the flash invisible.
* **§3.7 game-bootstrap re-fold.**  Audit doc §3.7 lists
  the next 8-12 KB target for the eager chunk.  Risk: moving
  scheduler shells into game-bootstrap breaks the "open
  profile while lobby is empty" flow.  Recommend a separate
  spike wave (not jointly with surface work) so the
  re-architecture has dedicated review time.

### To Bishop W21 (backend)

* All 5 W21 admin surfaces expect Bishop's W21 endpoint
  contract.  Wire-shapes documented inline in each `*.ts`
  SPEC's `description` field.
* No new request fields beyond Bishop's W21 spec.  If any
  endpoint adds optional fields post-W21, the admin form
  needs a follow-up to expose them.

### To Vasquez W21 (handoff-protocol)

* `docs/lh13-soft-pin-rationale.md §12` records the W21
  HOLD-YELLOW disposition.  If the §6.8 row is touched at
  W21, please cross-ref §12 (the §11 cross-ref carried
  through W20).
* No `docs/agent-handoff-protocol.md` mutation in scope
  for the Hicks lane this wave.

### To coordinator

* LH13 §6.8 has now held YELLOW for THREE consecutive bring-up
  waves (W19, W20, W21) on the SAME blocker: `gh` CLI
  un-authenticated in the bring-up shell.  Recommend
  escalating the §6.x coordinator-driven probe path (per
  the W19 hand-off) at W22 if the auth posture is
  unchanged.  The wall-clock data is overwhelmingly likely
  to show ≥3 consecutive `schedule`-event successes by
  now; the bottleneck is purely the bring-up shell's
  inability to read the run history.

---

## Files touched (W21)

Frontend lane (`src/frontend/autotable-src/src/`):

* **NEW** `renderer-webgl2/tile-claim-animation.ts`
* **NEW** `renderer-webgl2/meld-display.ts`
* `renderer-webgl2/hello.ts` (added `mountMeld()` + `'meld'`
  mode)
* `index.ts` (URL regex extension for `webgl2-meld`)
* **NEW** `admin/swiss-apply-round.ts`
* **NEW** `admin/rotation-schedule.ts`
* **NEW** `admin/tournament-withdraw.ts`
* **NEW** `admin/signalr-purge.ts`
* **NEW** `admin/replay-restoration-audit.ts`
* `admin/admin-panel.ts` (5 new SURFACES registrations)
* `profile.ts` (drawer functions removed; added
  `flushPendingDisplayName()`; inlined `expandHex()`)
* **NEW** `profile-drawer.ts` (extracted drawer surface)
* `lobby.ts` (replaced eager `installProfileDrawer()` +
  `installProfileToggle()` with `scheduleProfileDrawerLazy
  Mount()`; new function at bottom mirrors W17 §3.2 pattern)
* `i18n.ts` (zh-* catalogs now dynamic-imported; `t()` /
  `installI18n()` / `setLanguage()` / `mergeServerCatalog()`
  updated for nullable catalog state)

Docs:

* `docs/lh13-soft-pin-rationale.md` (§12 W21 status appended)
* `docs/frontend-bundle-audit.md` (§4.3 W21 delivered-savings
  appended above the §4.2 candidates section so the wave-
  by-wave delivered-savings narrative reads in chronological
  order at the start of the doc)

Data:

* `src/frontend/autotable-src/dist-size.json` (K21 row
  appended by `scripts/append-dist-size.js`)

Inbox:

* `.squad/decisions/inbox/hicks-phase-k-wave-21.md` (this file)

---

## Sign-off

All 5 W21 deliverables landed.  Lane-discipline preserved
(every file under `src/frontend/autotable-src/src/`, the
`docs/` doc updates, the dist-size ledger, and the
`.squad/decisions/inbox/hicks-*.md` memo).  Bundle ceilings
all green at W21 close.

`three-renderer-big` held at 406,635 B for the **11th
consecutive wave** since the W14 baseline.

— Hicks (Frontend), W21
