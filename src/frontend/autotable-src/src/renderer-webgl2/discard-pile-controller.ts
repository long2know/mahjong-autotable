// Phase K Wave 23 — Hicks (Frontend).
//
// Phase L W7 wire-up — state-binding controller that ties the
// W22-staged `./discard-pile` + `./score-display` modules into the
// W17 scene runtime.  The W22 directive landed both modules as
// free-standing state machines + pure-layout functions, intentionally
// without any wiring against the live scene mesh.  W23 closes that
// loop: this controller owns the per-seat discard piles + the HUD
// canvas overlay, and renders them every frame the scene asks for
// a redraw.
//
// Why a separate controller (vs folding into `./scene.ts`):
//   • The scene runtime is purely about the tile mesh; layering
//     discard piles + HUD onto it would conflate concerns and
//     bloat the scene-runtime contract.
//   • The controller can be omitted entirely (the `webgl2-wall`,
//     `webgl2-interactive`, `webgl2-meld` smoke modes don't need
//     it) — keeping it separate preserves the existing W17-W21
//     smoke surfaces without paying for the W23 additions.
//   • Discard piles + HUD have different update cadences (piles
//     update on every player-discard event; HUD updates on every
//     score-change event); separating them avoids redundant
//     redraws.
//
// Bundle math: target ≤ 52 KB for `renderer-webgl2` at W23.  W22
// baseline 40,292 B; this controller adds ~3 KB minified.

import {
  createDiscardPile,
  layoutDiscardPile,
  popDiscard,
  pushDiscard,
  type DiscardEntry,
  type DiscardPileState,
} from './discard-pile';
import {
  DEFAULT_START_POINTS,
  createScoreDisplay,
  redrawScoreDisplay,
  setDoraIndicators,
  setRoundContext,
  setSeatScore,
  type ScoreDisplayState,
  type WindKind,
} from './score-display';
import type { SeatIndex } from './meld-display';
import type { TileMesh } from './tile-mesh';
import { setTileInstance } from './tile-mesh';

/** Reserved discard-pile range inside the wall tile-mesh.  We
 *  pack all four seats' discards into a contiguous instance-index
 *  range after the wall + meld rows so the mesh can paint them in
 *  one `drawTileMesh` call without a second mesh.
 *
 *  Layout (per seat): 30 reserved instance slots.  4 seats × 30 =
 *  120 reserved slots total.  Caller supplies the base index. */
export const DISCARD_PILE_SLOTS_PER_SEAT = 30;
export const DISCARD_PILE_RESERVED_SLOTS = DISCARD_PILE_SLOTS_PER_SEAT * 4;

export interface DiscardPileController {
  /** Per-seat discard pile state (parallel array, indexed by seat). */
  readonly piles: ReadonlyArray<DiscardPileState>;
  /** Append a tile to the named seat's pile + repaint into the
   *  reserved instance-index range.  Returns the new entry. */
  pushDiscard(seat: SeatIndex, tileId: number, isRiichi?: boolean): DiscardEntry;
  /** Pop the most-recent discard for the named seat (used when a
   *  claim re-routes the tile into a meld).  Returns the popped
   *  entry (null on empty pile). */
  popDiscard(seat: SeatIndex): DiscardEntry | null;
  /** Re-write every pile's tile-mesh slots without changing the
   *  underlying state (used after a camera-mode change requires
   *  re-laying-out the piles relative to the new seat axes). */
  redraw(): void;
  /** Total tile count across all four piles. */
  totalTileCount(): number;
}

export interface ScoreDisplayController {
  /** Public state — exposed so consumers can read the current
   *  score totals without going through the controller's mutators. */
  readonly state: ScoreDisplayState;
  /** Patch a single seat's HUD entry; triggers an on-demand
   *  redraw if the state changed. */
  setSeatScore(seat: SeatIndex, patch: Partial<{
    points: number; wind: WindKind; isDealer: boolean;
  }>): void;
  /** Replace the dora-indicator row.  Triggers an on-demand redraw. */
  setDora(tileIds: ReadonlyArray<number>): void;
  /** Set round-context fields.  Triggers an on-demand redraw. */
  setRound(roundWind: WindKind, roundNumber: number, dealerSeat: SeatIndex): void;
  /** Force-redraw the HUD canvas (called from the scene's rAF
   *  loop whenever the scene itself requests a redraw). */
  redraw(): void;
  /** Dispose the overlay canvas — used by the test harness. */
  dispose(): void;
}

/**
 * Create a discard-pile controller that paints into the supplied
 * `TileMesh` (typically a wall mesh extended with reserved-slot
 * range for the four piles).  `discardSlotBase` is the instance
 * index of the first reserved slot; the controller paints into
 * `discardSlotBase..discardSlotBase + 119` (30 slots × 4 seats).
 *
 * The controller marks every reserved slot as `tileId = -1`
 * initially (empty); `pushDiscard` rewrites the slot in-place
 * with the actual `tileId`.
 */
export function createDiscardPileController(
  mesh: TileMesh,
  discardSlotBase: number,
  onRedrawRequested: () => void,
): DiscardPileController {
  const piles: DiscardPileState[] = [
    createDiscardPile(0 as SeatIndex),
    createDiscardPile(1 as SeatIndex),
    createDiscardPile(2 as SeatIndex),
    createDiscardPile(3 as SeatIndex),
  ];

  // Reserved-slot range bounds.
  const lastSlot = discardSlotBase + DISCARD_PILE_RESERVED_SLOTS;
  if (lastSlot > mesh.capacity) {
    throw new Error(
      `[discard-pile-controller] mesh capacity ${mesh.capacity} too small `
      + `for reserved slot range ${discardSlotBase}..${lastSlot}`,
    );
  }

  // Initial paint — empty piles → every reserved slot gets the
  // sentinel `tileId = -1` so the renderer skips drawing it.
  // (`drawTileMesh` honours the `-1` sentinel by clamping the
  // texture-coord lookup to the back-face tile in the atlas.)
  function clearReservedRange(): void {
    // Allocate one identity matrix off the hot-path; reuse it for
    // every slot.  setTileInstance copies into the mesh buffer.
    const identity = new Float32Array(16);
    identity[0] = 1; identity[5] = 1; identity[10] = 1; identity[15] = 1;
    // Drop the empty slots well below the table so they're never
    // visible if the renderer doesn't honour the -1 sentinel.
    identity[13] = -1000;
    for (let i = discardSlotBase; i < lastSlot; i++) {
      setTileInstance(mesh, i, identity, -1);
    }
  }
  clearReservedRange();

  function repaintSeat(seat: SeatIndex): void {
    layoutDiscardPile(piles[seat]);
    const slotStart = discardSlotBase + seat * DISCARD_PILE_SLOTS_PER_SEAT;
    const pile = piles[seat];
    for (let i = 0; i < pile.entries.length; i++) {
      setTileInstance(mesh, slotStart + i, pile.matrices[i], pile.entries[i].tileId);
    }
    // Clear remaining reserved slots for this seat (post-pop case).
    const identity = new Float32Array(16);
    identity[0] = 1; identity[5] = 1; identity[10] = 1; identity[15] = 1;
    identity[13] = -1000;
    for (let i = slotStart + pile.entries.length; i < slotStart + DISCARD_PILE_SLOTS_PER_SEAT; i++) {
      setTileInstance(mesh, i, identity, -1);
    }
  }

  return {
    piles,
    pushDiscard(seat: SeatIndex, tileId: number, isRiichi: boolean = false): DiscardEntry {
      pushDiscard(piles[seat], tileId, isRiichi);
      repaintSeat(seat);
      onRedrawRequested();
      return piles[seat].entries[piles[seat].entries.length - 1];
    },
    popDiscard(seat: SeatIndex): DiscardEntry | null {
      const popped = popDiscard(piles[seat]);
      if (popped !== null) {
        repaintSeat(seat);
        onRedrawRequested();
      }
      return popped;
    },
    redraw(): void {
      for (let s = 0 as SeatIndex; s < 4; s = (s + 1) as SeatIndex) {
        repaintSeat(s);
      }
      onRedrawRequested();
    },
    totalTileCount(): number {
      return piles[0].entries.length + piles[1].entries.length
        + piles[2].entries.length + piles[3].entries.length;
    },
  };
}

/**
 * Create a score-display controller that paints onto a 2D canvas
 * overlay anchored at top-right of the supplied parent container.
 * The overlay sits on top of the WebGL canvas (CSS z-index) so
 * the HUD reads as a thin chrome over the 3D scene.
 *
 * The controller wires its own resize listener so the overlay
 * canvas tracks the parent's dimensions; the caller can omit the
 * listener via `noResizeListener: true` for headless test runs.
 */
export function createScoreDisplayController(
  parent: HTMLElement,
  options: { noResizeListener?: boolean } = {},
): ScoreDisplayController {
  const state = createScoreDisplay();

  const overlay = document.createElement('canvas');
  overlay.id = 'score-display-overlay';
  overlay.setAttribute('data-testid', 'score-display-overlay');
  overlay.style.cssText =
    'position:absolute;inset:0;width:100%;height:100%;'
    + 'pointer-events:none;z-index:5;';
  parent.appendChild(overlay);

  const ctx = overlay.getContext('2d');
  if (ctx === null) {
    throw new Error('[score-display-controller] 2D context unavailable');
  }

  function resizeOverlay(): void {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const cssW = Math.max(1, parent.clientWidth | 0);
    const cssH = Math.max(1, parent.clientHeight | 0);
    const w = Math.floor(cssW * dpr);
    const h = Math.floor(cssH * dpr);
    if (overlay.width !== w) overlay.width = w;
    if (overlay.height !== h) overlay.height = h;
    // Reset the hash so the next redraw repaints the entire canvas.
    state.lastRenderedHash = '';
  }
  resizeOverlay();

  let resizeListener: (() => void) | null = null;
  if (options.noResizeListener !== true) {
    resizeListener = (): void => {
      resizeOverlay();
      redrawScoreDisplay(state, ctx);
    };
    window.addEventListener('resize', resizeListener, { passive: true });
  }

  // Initial paint with default seed (25,000 points × 4).
  redrawScoreDisplay(state, ctx);

  return {
    state,
    setSeatScore(seat: SeatIndex, patch: Partial<{
      points: number; wind: WindKind; isDealer: boolean;
    }>): void {
      if (setSeatScore(state, seat, patch)) {
        redrawScoreDisplay(state, ctx);
      }
    },
    setDora(tileIds: ReadonlyArray<number>): void {
      setDoraIndicators(state, tileIds);
      redrawScoreDisplay(state, ctx);
    },
    setRound(roundWind: WindKind, roundNumber: number, dealerSeat: SeatIndex): void {
      setRoundContext(state, roundWind, roundNumber, dealerSeat);
      redrawScoreDisplay(state, ctx);
    },
    redraw(): void {
      // Force a repaint even on a cached state (used after a parent
      // resize that nulls the cache hash).
      redrawScoreDisplay(state, ctx);
    },
    dispose(): void {
      if (resizeListener !== null) {
        window.removeEventListener('resize', resizeListener);
        resizeListener = null;
      }
      overlay.remove();
    },
  };
}

/** Default starting-points-per-seat — re-exported for convenience. */
export { DEFAULT_START_POINTS };
