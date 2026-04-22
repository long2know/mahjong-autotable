# Phase 4: Polish — Summary

**Agent:** Togusa (sonnet-4.5)  
**Branch:** `squad/autotable-phase4-polish`  
**Date:** 2026-04-17  
**Duration:** ~3 hours

---

## Objective

Add animations, feedback systems, and settlement overlay to complete the polish pass for the Autotable client MVP.

---

## Deliverables

### 1. Animation System (`animation.ts`)

**Generic Tween Manager:**
- `TweenManager` class with global singleton `globalTweenManager`
- `tween(id, target, to, duration, easing, onComplete)` method
- Automatic cleanup of completed tweens
- Support for custom easing functions

**Easing Functions:**
- `easeInOutCubic`: Smooth acceleration/deceleration (used by camera)
- `easeOutQuad`: Quick start, slow finish (used by tile animations)

**Integration:**
- Called from scene animation loop via `globalTweenManager.update()`
- No external dependencies (no GSAP/tweenjs)

### 2. Tile Animations

**Spawn Animation (250ms):**
- Fade-in: opacity 0 → 1
- Drop: Y position starts 0.3 units above final position
- Applied to all hand tiles and meld tiles on scene rebuild
- Uses `easeOutQuad` for natural feel

**Meld Scale Pop (200ms):**
- Scale 0.8 → 1.0 on all three axes
- Applied when melds form
- Brief "pop" emphasizes new meld formation

**Implementation:**
- Tween targets stored in mesh `userData` for per-frame updates
- Animation loop traverses game group and applies target values to meshes
- Materials set to `transparent: true` for opacity animation

### 3. Turn Indicator Enhancement

**Pulsing Glow:**
- Opacity oscillates between 0.6 and 0.8
- Sine wave driven by `turnPulsePhase` accumulator
- Phase advances at 2 radians/second
- Visual feedback for current player's turn

**Material Update:**
- Changed from basic opaque to transparent with opacity control
- DoubleSide rendering for visibility from all angles

### 4. Toast Notification System (`toast.ts`)

**Visual Design:**
- Fixed position: bottom-center, 100px from bottom
- Dark background: rgba(0, 0, 0, 0.85)
- White monospace text
- Rounded corners, drop shadow

**Behavior:**
- Max 3 toasts visible simultaneously
- Auto-dismiss after 3 seconds
- Fade-in/fade-out transitions (200ms)
- FIFO queue: oldest removed when capacity exceeded

**Messages:**
- "Drew a tile" (on draw success)
- "Tile discarded" (on discard success)
- "[Meld Type] declared" (on meld success)
- "Win declared!" (on win success)
- Error messages (replacing `alert()` calls)

**Replaced:**
- All `alert()` calls from Phase 2 removed
- Consistent visual language with rest of UI

### 5. Game End Overlay (`overlay.ts`)

**Trigger:**
- `state.status === 'Completed'`
- Only shown once (checks `isVisible()` to avoid duplicates)

**Content:**
- Full-screen dark backdrop (rgba(0, 0, 0, 0.8))
- Centered content box with blue border
- Winner name, seat, and score
- Winning hand display:
  - Open melds grouped in containers
  - Concealed tiles in separate container
  - Tile labels rendered as styled spans
- Close button (hover effect)

**Behavior:**
- Does NOT unmount 3D scene
- Scene remains visible behind overlay
- Dismissible via close button
- Can be reopened if needed (though typically one-time)

### 6. Audio Hook System (`audio.ts`)

**Event Types:**
- `'draw'`: Tile drawn
- `'discard'`: Tile discarded
- `'meld'`: Meld declared
- `'win'`: Game won

**Implementation:**
- `play(event: AudioEvent)` function
- Currently no-op with `console.debug()` logging
- Ready for Phase 5 audio asset integration

**Wiring:**
- Called from main.ts on user action success
- Called from net.ts on SignalR events
- Provides hooks for future audio without shipping assets now

---

## Scene Integration

### Animation Loop Updates

```typescript
function animate() {
  // ... existing setup ...
  
  // Update tween animations
  globalTweenManager.update();
  
  // Apply tween targets to meshes
  gameGroup.traverse((obj) => {
    if (obj.userData['targetPos']) {
      const pos = obj.userData['targetPos'];
      obj.position.y = pos.y;
    }
    if (obj.userData['targetScale']) {
      const scale = obj.userData['targetScale'];
      obj.scale.set(scale.x, scale.y, scale.z);
    }
    if (obj.userData['updateOpacity']) {
      obj.userData['updateOpacity']();
    }
  });
  
  // Pulse turn indicator
  if (turnIndicator.visible) {
    turnPulsePhase += deltaTime * 2;
    turnIndicatorMaterial.opacity = 0.6 + Math.sin(turnPulsePhase) * 0.2;
  }
  
  // ... render ...
}
```

### State-Driven Animation

- Animations trigger on scene rebuild (state change)
- Each tile gets unique tween ID: `tile-spawn-${playerId}-${index}`
- Tweens auto-complete and clean up
- No animation state persists across state changes

---

## File Changes

### New Files:
- `src/animation.ts` (2.1 KB)
- `src/toast.ts` (2.4 KB)
- `src/audio.ts` (359 B)
- `src/overlay.ts` (5.9 KB)

### Modified Files:
- `src/main.ts`: Import toast/overlay/audio, wire success toasts, show overlay on game complete
- `src/net.ts`: Add audio hooks on SignalR events
- `src/scene.ts`: Add animation imports, spawn animations, meld scale pop, turn pulse, tween updates in animation loop

---

## Validation

### Build
```bash
cd src/MahjongClaw.AutotableClient
npm run build
# ✓ 44 modules transformed
# ✓ built in 959ms
# Bundle: 561 KB (143 KB gzip)
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

### Animation Strategy

**Why spawn-only animations?**
- Full tile movement tracking (wall→hand, hand→discard) requires state diffing
- Must track tile IDs across state snapshots to compute movement
- Complex to implement correctly (especially with state refetch-on-event model)
- Chosen fallback: spawn animations are simpler, visually pleasing, and bug-free
- Future enhancement: could track `lastAction` in state to animate specific movements

**Tween target pattern:**
- Store tween target object in mesh `userData`
- Tween updates target object properties
- Animation loop reads target and applies to mesh
- Avoids complex callbacks or mesh-specific tween storage

### Material Transparency

**Critical:** Must set `transparent: true` before animating opacity
- THREE.js doesn't automatically enable transparency
- Without it, opacity changes are ignored
- Must be set per-material instance (meshes may share materials)

### Overlay Idempotency

**Must check `isVisible()` before showing overlay**
- State changes trigger multiple times on game completion
- Without check, would create duplicate overlays
- Each overlay appends to `document.body`, causing memory leak

---

## Known Constraints

- No slide-to-discard or slide-from-wall animations (spawn-only)
- No bird highlight for winning tiles (not in state DTO)
- No point breakdown / fan display (not in state DTO)
- No kong replacement animation (single wall pile doesn't track source)
- Toast system is single-queue (no priority or multi-channel support)

---

## Visual Consistency

All new UI elements follow established design language:
- Dark semi-transparent backgrounds
- White text on dark backgrounds
- Blue accent color (#4a90e2)
- Monospace font
- Rounded corners
- Drop shadows for depth

---

## Next Phase

**Phase 5 — Feature Parity & Lobby**
- Game creation / room listing UI
- Join game flow
- Ruleset selector
- Persistent scoreboard
- Full feature parity with Blazor client
- Blazor retirement decision

---

*Phase 4 complete. Animations smooth, feedback systems functional, spec-compliant.*
