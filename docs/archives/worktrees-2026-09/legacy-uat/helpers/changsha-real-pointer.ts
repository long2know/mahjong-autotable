// Ripley fast-tests lane — canonical real-pointer + mobile helper for the
// schema-INDEPENDENT Changsha browser RED gates (G5/G6/G7/G8/G10/G12/G16/G18/
// G20/G21). This module is the single import surface for the `changsha-*.spec.ts`
// gates so the real-UI discipline (no client.update / no emitDiscard / no
// synthetic DOM / no injection) lives in one place.
//
// It re-exports the proven real-pointer OBSERVE/ADVANCE primitives from the
// upstream `_playability` harness and the Changsha UAT harness (`_uat-changsha`),
// and adds a small mobile/viewport + peer (multi-context) toolkit used by the
// responsive and non-observation gates.
//
// hudson-1 owns tests/e2e/helpers/changsha-raw-ws.ts — this file never touches it.

import type { Page, BrowserContext, Browser } from '@playwright/test';

export {
  // real-pointer + lifecycle primitives (upstream harness)
  makeConfig, buildGameUrl, defangOverlays, dismissLobbyAndTour, ensureConnected,
  takeSeatByClick, clickDeal, waitForGameObject, readSeat, readConnected,
  projectTileToCanvas, discardByPointer, readClaimWindow, hasExtraHandTile,
  waitForPlayableHand, readPickup, readMyHandTiles,
} from '../_playability';

export {
  // Changsha UAT harness (fresh gameId, WS outbound recorder, fingerprint,
  // a11y probes, machine-readable RED-evidence writer)
  UAT_ARTIFACT_DIR, ensureUatArtifactDir, recordRedEvidence, freshGameId,
  uatGameUrl, resolveBase, recordOutboundWs, thingsFingerprint, ownVsOppHand,
  readMoveLogText, probeControl, accessibilityLabelsMatching, probeSelectA11y,
} from '../_uat-changsha';

/** Viewport presets for the responsive gates (G20). */
export const VIEWPORTS = {
  mobile: { tag: 'mobile-390x844', w: 390, h: 844 },
  tabletPortrait: { tag: 'tablet-portrait-768x1024', w: 768, h: 1024 },
  tabletLandscape: { tag: 'tablet-landscape-1024x768', w: 1024, h: 768 },
} as const;

export interface ControlBounds {
  id: string; x: number; y: number; w: number; h: number;
  inViewport: boolean; hitSelf: boolean;
}

/**
 * Measure a set of control ids for bounds, in-viewport-ness, and centre
 * hit-testability, plus the fraction of the viewport NOT covered by the 3D
 * canvas (dead space). Pure DOM observation — no injection.
 */
export async function measureControls(
  page: Page, ids: string[], vw: number, vh: number,
): Promise<{ ctrls: ControlBounds[]; canvasDeadRatio: number }> {
  return page.evaluate((args) => {
    const { ids, vw, vh } = args as { ids: string[]; vw: number; vh: number };
    const ctrls: ControlBounds[] = [];
    for (const id of ids) {
      const el = document.getElementById(id);
      if (!el) { ctrls.push({ id, x: 0, y: 0, w: 0, h: 0, inViewport: false, hitSelf: false }); continue; }
      const r = el.getBoundingClientRect();
      const cx = r.left + r.width / 2, cy = r.top + r.height / 2;
      const top = document.elementFromPoint(cx, cy) as HTMLElement | null;
      ctrls.push({
        id,
        x: Math.round(r.x), y: Math.round(r.y), w: Math.round(r.width), h: Math.round(r.height),
        inViewport: r.left >= 0 && r.top >= 0 && r.right <= vw && r.bottom <= vh && r.width > 0 && r.height > 0,
        hitSelf: !!(top && (top === el || el.contains(top) || top.contains(el))),
      });
    }
    const canvas = document.getElementById('main');
    const cr = canvas?.getBoundingClientRect();
    const canvasArea = cr ? cr.width * cr.height : 0;
    const canvasDeadRatio = canvasArea > 0 ? 1 - canvasArea / (vw * vh) : 1;
    return { ctrls, canvasDeadRatio };
  }, { ids, vw, vh }) as Promise<{ ctrls: ControlBounds[]; canvasDeadRatio: number }>;
}

/** Axis-aligned overlap test for two measured control bounds. */
export function boundsOverlap(a: ControlBounds, b: ControlBounds): boolean {
  return !(a.x + a.w <= b.x || b.x + b.w <= a.x || a.y + a.h <= b.y || b.y + b.h <= a.y);
}

/**
 * Open a second, fully isolated browser context (a distinct peer client) for the
 * non-observation gate (G18). Returns the context + a fresh page so the caller
 * can point client B at the same gameId as client A and assert B never receives
 * A's private state. Caller owns closing the returned context.
 */
export async function openPeerClient(browser: Browser): Promise<{ ctx: BrowserContext; page: Page }> {
  const ctx = await browser.newContext();
  const page = await ctx.newPage();
  return { ctx, page };
}
