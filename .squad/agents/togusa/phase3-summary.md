# Phase 3: View Modes — Summary

**Agent:** Togusa (sonnet-4.5)  
**Branch:** `squad/autotable-phase3-view-modes`  
**Date:** 2026-04-17  
**Duration:** ~2 hours

---

## Objective

Add dual camera modes (flat orthographic and 3D perspective) with smooth transitions, keyboard/HUD toggle, and per-seat perspective positioning.

---

## Deliverables

### 1. Camera Controller (`camera.ts`)

- **Dual cameras:** `OrthographicCamera` (flat top-down) and `PerspectiveCamera` (3D angled)
- **Mode toggle:** `setMode()`, `toggleMode()`, `getMode()` methods
- **Per-seat positioning:** `updateSeat(seatIndex)` rotates perspective camera 90° per seat
  - Seat 0 (East): camera at `(0, 12, 15)` looking at origin
  - Seat 1 (South): `(15, 12, 0)`
  - Seat 2 (West): `(0, 12, -15)`
  - Seat 3 (North): `(-15, 12, 0)`
- **Smooth transitions:** 300ms animated camera swap using easeInOutCubic interpolation
  - Lerp position, lookAt target, and FOV
  - No external tweening library — rolled custom easing function
- **Resize handling:** `onResize()` updates both cameras' projection matrices
- **Animation loop:** `update(deltaTime)` advances transition state

### 2. Scene Integration (`scene.ts`)

- Replaced direct camera ownership with `CameraController`
- Animation loop now calls `cameraController.update(deltaTime)` per frame
- Renderer uses `cameraController.getActive()` instead of fixed camera ref
- Return signature changed from `{ camera, scene }` to `{ cameraController, scene }`

### 3. Input Handler (`input.ts`)

- Added `setCamera(camera)` method to update raycaster camera on mode change
- Raycasting works correctly from both orthographic and perspective views
- Click-to-discard interaction preserved across camera modes

### 4. HUD View Button (`hud.ts`)

- New `ViewButton` class: top-right button showing "View: Flat" / "View: Perspective"
- `setToggleCallback()` method notifies main.ts when mode changes
- `updateLabel()` syncs button text with current mode
- Positioned at `top: 10px; right: 230px` to avoid HUD info panel overlap

### 5. Main Bootstrap (`main.ts`)

- Instantiate `CameraController` after scene init
- Wire keyboard listener: press `p` to toggle modes
- Wire view button callback to update input handler camera
- On state change: detect local player seat and call `cameraController.updateSeat(seatIndex)`
- Both keyboard and button triggers:
  1. Toggle camera mode
  2. Update button label
  3. Update input handler camera ref

---

## Implementation Details

### Camera Positions (Perspective Mode)

Following SEAT_POSITIONS convention from Phase 1 scene layout:
- **East (Seat 0):** +Z side, camera behind player looking north
- **South (Seat 1):** -X side, camera rotated 90° CCW
- **West (Seat 2):** -Z side, camera rotated 180°
- **North (Seat 3):** +X side, camera rotated 270°

Each position is 15 units from origin, elevated 12 units, looking at table center.

### Flat Mode (Orthographic)

- Positioned at `(0, 30, 0)` looking straight down
- Frustum size 50 units, scales with aspect ratio
- Good for quick board scan / strategic overview

### Transition Animation

- 300ms duration (per spec; pwmarcz used 100ms, we chose slightly longer for smoothness)
- Custom `easeInOutCubic` function: `t < 0.5 ? 4t³ : 1 - (-2t+2)³/2`
- Interpolates:
  - Position (Vector3)
  - LookAt target (always origin)
  - FOV (perspective only)
- Runs in `update()` called from scene animation loop

### Toggle Triggers

1. **Keyboard:** `p` key listener in main.ts
2. **HUD Button:** Click "View: [mode]" button
3. Both paths:
   - Call `cameraController.toggleMode()`
   - Update button label
   - Update input handler camera

---

## Validation

### Build

```bash
cd src/MahjongClaw.AutotableClient
npm run build
# ✓ 40 modules transformed
# ✓ built in 934ms
```

### Tests

```bash
cd /data/source/mahjong-claw
dotnet build
dotnet test
# Passed: 2341, Failed: 0, Skipped: 0
```

All gates pass.

---

## Technical Notes

1. **No new dependencies:** Rolled custom tweening; no GSAP/tweenjs added
2. **TypeScript strict mode:** All code passes `noUncheckedIndexedAccess`
3. **Camera reference handoff:** Input handler uses `setCamera()` to receive active camera on each toggle
4. **Raycasting compatibility:** Both camera types work with Three.js raycaster; no special handling needed
5. **Per-seat orientation:** Perspective camera rotates to put local player's hand at bottom of screen

---

## Known Constraints

- No orbit/drag controls (Phase 4 polish)
- No tile hover effects (Phase 4)
- Transition is linear interpolation with easing; no physics-based spring
- Spectator mode defaults to Seat 0 (East) perspective

---

## Next Phase

**Phase 4 — Polish**
- Tile animations (slide to discard, draw from wall)
- Kong replacement animation
- Claim animations
- Win overlay with settlement display
- Bird highlight for winning tiles

---

*Phase 3 complete. Camera modes functional, smooth, and spec-compliant.*
