# Vasquez — Broken Deal Visual Repro

**Date:** 2026-06-01
**Author:** Vasquez (Integration / Playwright Specialist)
**Requested by:** Stephen Long (via Copilot)
**HEAD tested:** `5144dc8` — main tip at run time (= `dd2608d` + 2 commits;
both are persistence / docs only and do not touch the deal or render path,
so this is functionally the SHA Stephen flagged: `dd2608d` — *"docs(audit):
Wave 5 — game playable end-to-end (squash)"*).
**Stephen's quote:** *"Also.. is the game working? The dealing seems very whacky."*

## TL;DR

> **The backend deal is *correct*. The frontend rendering is *broken*.**
> All 3 of the bugs Stephen could **prove** from the screenshot
> (flat walls, corner artifacts, in‑canvas Bot label box) reproduce
> deterministically.  The "only 1 tile in front of seat 0" bug does
> **not** reproduce after the ~7 s deal animation settles — it was
> almost certainly a mid‑animation snapshot.  Hand‑off is therefore
> **HICKS** (frontend rendering & HUD), with no backend changes needed.

---

## Reproduction

### URL

```
https://127.0.0.1:7135/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Hard&handCount=4&gameId=repro-<unique-ts>
```

(Backend port `8088` from the directive was *not* listening at run time —
the live dev backend is on `https://127.0.0.1:7135` with the standard
ASP.NET dev self‑signed cert.  Spec uses `ignoreHTTPSErrors: true`.)

### Driver

```bash
cd /data/source/mahjong-autotable
E2E_BASE_URL=https://127.0.0.1:7135 NODE_TLS_REJECT_UNAUTHORIZED=0 \
  node playtest-artifacts/playtest-broken-deal-repro.spec.mjs
```

Spec path: `playtest-artifacts/playtest-broken-deal-repro.spec.mjs`
(loads page → dismisses tour → quick‑match → seats seat 0 → invokes
`window.game.world.deal('HANDS')` → waits 8 s → snapshots full page →
dumps `world.things` + slot inventory + sample `thing.place()` positions
to JSON).

---

## Evidence — captured at HEAD `dd2608d`

**Screenshot:** `playtest-artifacts/screenshots/broken-deal-repro-2026-06-01T19-52-51-434Z.png`
**State JSON:** `playtest-artifacts/screenshots/broken-deal-repro-2026-06-01T19-52-51-434Z.json`

### `world.things` / `world.slots` snapshot, T = deal + 8 s

| metric                | value                                |
| --------------------- | ------------------------------------ |
| `thingCount`          | **197**                              |
| face‑up tiles         | **14**                               |
| face‑down tiles       | **122**                              |
| `match.phase`         | `null`                               |
| wall slots            | **152** (`wall.X.Y@N`, 38 × 4 seats) |
| discard slots         | **88**                               |
| hand slots            | **60** (15 × 4 seats)                |
| meld slots            | **64** (16 × 4 seats)                |
| tiles in wall         | **83**                               |
| tiles in hand (total) | **53** (14 + 13 + 13 + 13)           |
| tiles in discard      | **0**                                |

### Per‑seat hand inventory

| seat | hand slots | tiles in hand | face‑up | face‑down |
| ---- | ---------- | ------------- | ------- | --------- |
| 0    | 15         | **14**        | **14**  | 0         |
| 1    | 15         | 13            | 0       | 13        |
| 2    | 15         | 13            | 0       | 13        |
| 3    | 15         | 13            | 0       | 13        |

> Dealer (seat 0) has **14 / 14 face‑up**; non‑local seats have
> 13 / 13 face‑down each. This is **the textbook correct post‑deal
> state for Changsha East** (13 + 1 draw, all visible to local seat,
> all foreign hands concealed).  *Backend dealing is **not** broken.*

### Sample wall tile `thing.place()` (engine‑computed render coords)

```json
{ "slotName": "wall.6.0@0", "place": { "x": 69,   "y": 24.5,  "z": 2 } }
{ "slotName": "wall.6.1@0", "place": { "x": 69,   "y": 24.5,  "z": 6 } }   ← layer-1 stack
{ "slotName": "wall.2.0@1", "place": { "x": 149.5,"y": 45,    "z": 2 } }
```

`thing.place().z` returns **two distinct values (2 and 6)** for the same
x,y pair — i.e. the engine *does* compute a 2‑high stacked wall.  The
**rendered scene shows single‑layer flat walls** (see screenshot), so
the bug lives between `place()` and the mesh writer / instanced matrix.

### Stephen's 5 bugs — repro status

| # | Stephen's bug                                                         | Repro?           | Evidence                                                                                                                                  |
| - | --------------------------------------------------------------------- | ---------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| 1 | Walls are flat single‑row strips, not stacked 2‑high bricks           | **✅ YES**        | Screenshot — all 4 walls render as single layer. `place().z` shows engine wants z=2 / z=6 (two layers); render ignores it.                |
| 2 | Only ONE tile face‑up in front of seat 0 (single 6筒) — should be 13/14 | **❌ NO**         | Post‑settle Playwright snapshot shows **14 face‑up in seat 0 hand**.  Stephen almost certainly screenshotted *during* the deal animation. |
| 3 | Gray triangular wedge artifacts at all 4 corners + stray white strips | **✅ YES**        | Screenshot — visible at all 4 corners.  **THREE.js logs `Computed radius is NaN. The 'position' attribute is likely to have NaN values.`** |
| 4 | Tiny black "Bot 1/2/3 + Seat 0" label box floating dead‑centre        | **✅ YES** (sort of) | Screenshot — a black scoreboard texture **and** a `3 bots — Medium · seats 1, 2, 3 / Bot 1 (S) / Bot 2 (W) / Bot 3 (N)` HUD overlay are rendered on top of the table.  Position depends on viewport — Stephen got centre, my run got bottom‑left, but it's the same widget. |
| 5 | Move log says "Match started — dealer is Seat 0" but visuals wrong    | **✅ YES**        | Both move‑log entries (`Match started — dealer is Seat 0`, `Dice rolled: 6 + 3 = 9`) visible in screenshot.  Backend confirms deal succeeded.|

### Diagnostic signal — the smoking gun

`page.on('console', 'error')` captured:

```
THREE.BufferGeometry.computeBoundingSphere(): Computed radius is NaN.
The "position" attribute is likely to have NaN values. Is …
```

This is direct evidence of a geometry whose vertex positions contain
`NaN`.  In THREE.js, NaN positions render as degenerate / triangular
artifacts at the origin — exactly the gray wedges Stephen sees at all
4 corners.  Likely a `Math.atan2` or division‑by‑zero in a placement /
rotation helper that happens at exactly 4 of the 4 corner positions
(seats 0/1/2/3 corners).

### Pre‑existing noise (NOT regressions)

* `404 GET /api/games/<id>` and `/api/games/<id>/settings` — same lobby
  bootstrap 404s captured in `vasquez-integration-audit.md`; not the
  bug.
* WebGL `GPU stall due to ReadPixels` — driver noise, harmless.

---

## Comparison vs. last‑known‑good (`final-game-proof.png`)

`playtest-artifacts/final-game-proof.png` (Wave 5 audit, 2026‑05‑29) was
taken with the **manual deal** + dealer‑extra round flow on a single
matched seat.  It shows neither the corner wedges nor the centred score
panel — but it also doesn't fully render walls because manual‑deal
walls are torn down by the time the dealer extras get drawn, so the
wall‑stacking regression can't be inferred from it directly.

`playtest-artifacts/walls-facedown/04-post-deal.png` (Hicks W18) is the
closest direct comparator and **does** show stacked walls.  This
strongly suggests the regression landed *after* Hicks' walls‑facedown
spec passed — i.e. somewhere in the Wave 5 push.

---

## Hand‑off

### 🛠 HICKS (frontend) — **OWNER of all 3 reproducing bugs**

1. **Bug #1 — flat walls.** `Thing.place()` returns z ∈ {2, 6} for
   same (x,y) pairs, so the engine *does* author a 2‑layer stacked
   wall, but the mesh writer is dropping the z coord or always using
   layer‑0 offset.  Investigate:
   * `src/frontend/autotable-src/src/thing-group.ts` — `setInstance` /
     mesh matrix write path.  Verify the place's `position.z` lands in
     the instanced‑mesh translation matrix.
   * `src/frontend/autotable-src/src/setup-slots.ts` — does the wall
     init still emit `originX/originZ` pairs for both layers?  Recent
     setup‑slot rewrites are the prime suspect.
   * Compare git diff between HEAD `dd2608d` and the SHA where
     `playtest-walls-facedown.spec.mjs` last produced
     `04-post-deal.png` with stacked walls.

2. **Bug #3 — corner triangular artifacts.** THREE logs
   `Computed radius is NaN. The "position" attribute is likely to
   have NaN values.`  Investigate:
   * `src/frontend/autotable-src/src/thing-group.ts` and any
     `BufferGeometry` constructor sites — guard against `NaN` in
     position attributes (likely `0 / 0`, `Math.atan2(0, 0)`, or a
     `Math.acos` argument > 1 in a 4‑seat rotation helper).
   * The artifacts appear at corner seats which suggests the per‑seat
     rotation transform is producing NaN when an input is exactly
     axis‑aligned (e.g. `up.cross(forward)` colinear case).

3. **Bug #4 — centred / floating Bot label box + score panel.**
   Investigate:
   * `src/frontend/autotable-src/src/game-ui.ts` — score / bot listing
     widgets.  Their absolute‑position CSS appears to be defaulting
     into the canvas region instead of being pinned to a side panel.
     A recent Bot strategy / lobby refactor (Frost, Wave 4) added the
     `3 bots — Medium · seats 1, 2, 3` block; check its mount point
     and z‑index.
   * The black score panel (`00052 / 00053 / 25000 / Seat 0`) appears
     to be the in‑canvas "mini scoreboard" texture — could be a
     scoreboard plane mesh whose transform also got hit by the
     same NaN bug (#3).  Worth testing together.

### 🛠 FROST (backend) — **NO ACTION REQUIRED** (just FYI)

* `ChangshaDealingCeremony` and `ChangshaToAutotableTranslator` are
  producing **correct** snapshots: 14 face‑up to dealer, 13 face‑down
  to each non‑dealer, 83 wall tiles remaining, all `slot.name` values
  intact (`wall.X.Y@N`, `hand.K@N`).
* If Hicks finds the geometry NaN traces back to a malformed
  position field in the WS payload, ping back — but right now there's
  no backend signal.

### 🛠 (Stephen) — confirming bug #2

Bug #2 ("only 1 tile in front of seat 0") *does not reproduce* in a
post‑settle headless snapshot.  Two plausible explanations:

1. Stephen screenshotted mid‑animation (~T+2 s into the deal).  At
   T+8 s the hand is fully populated.
2. There's a render race where on a sufficiently slow machine the
   dealer's last 13 face‑up tiles don't land until well after the
   "Match started" log line.  Worth re‑checking on a throttled CPU
   profile.

---

## Files

* **Spec:** `playtest-artifacts/playtest-broken-deal-repro.spec.mjs`
* **Screenshot (HTTPS run):**
  `playtest-artifacts/screenshots/broken-deal-repro-2026-06-01T19-52-51-434Z.png`
* **State dump:**
  `playtest-artifacts/screenshots/broken-deal-repro-2026-06-01T19-52-51-434Z.json`

— Vasquez
