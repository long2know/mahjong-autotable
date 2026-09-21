// UAT (Hudson) — Suite A / B6: in an Auto-deal Changsha game, an ARBITRARY wall
// tile drag/flip performed with a REAL pointer must cause NO visual/local
// collection mutation and NO outbound `things` update. The wall is
// server-authoritative; a human grabbing a wall tile and dragging it is not a
// legal move and must be ignored client-side (the tile must not be picked up,
// held, flipped, or re-slotted, and nothing may be pushed to the server).
//
// RED baseline (200cad4): the relay client lets the pointer HOLD a wall tile
// (sets `claimedBy` locally) and pushes `["things", <id>, { claimedBy, ... }]`
// UPDATE frames to the server. This spec pins the desired authoritative
// behaviour; expected to FAIL until wall interaction is gated to server rules.
//
// NB scope: this is the AUTO-wall drag no-op case. It is NOT the G4 physical
// wall world-coordinate polyline test (owned elsewhere) — it asserts collection
// immutability + zero outbound traffic, not wall geometry.

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  clickDeal, waitForGameObject, projectTileToCanvas,
} from './_playability';
import {
  resolveBase, uatGameUrl, freshGameId, recordOutboundWs, thingsFingerprint, recordRedEvidence,
} from './_uat-changsha';

async function firstWallTileId(page: import('@playwright/test').Page): Promise<number | null> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = (window as any).game?.world;
    if (!w?.things) return null;
    for (const [id, t] of w.things.entries()) {
      if (t?.slot?.group === 'wall') return id as number;
    }
    return null;
  });
}

async function wallClaimedByCount(page: import('@playwright/test').Page): Promise<number> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = (window as any).game?.world;
    let n = 0;
    if (w?.things) {
      for (const t of w.things.values()) {
        if (t?.slot?.group === 'wall' && t.claimedBy !== null && t.claimedBy !== undefined) n++;
      }
    }
    return n;
  });
}

test.describe('UAT G16 — arbitrary Auto-wall drag is a no-op (no local mutation, no outbound things)', () => {
  test('a real pointer drag on a wall tile neither holds/flips it locally nor pushes an update', async ({ page, baseURL }) => {
    test.setTimeout(120_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('b6-wall-drag');

    const pageErrors: string[] = [];
    page.on('pageerror', (e) => pageErrors.push(String(e)));
    const ws = recordOutboundWs(page);

    await defangOverlays(page);
    await page.goto(uatGameUrl(base, { gameId, dealMode: 'auto' }), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never booted').toBe(true);
    await dismissLobbyAndTour(page);
    expect(await ensureConnected(page), 'client never connected').toBe(true);
    await takeSeatByClick(page, 0);
    await clickDeal(page);
    await page.waitForTimeout(3500);

    const wallId = await firstWallTileId(page);
    expect(wallId, 'no wall tile present to drag').not.toBeNull();

    const fpBefore = await thingsFingerprint(page);
    const claimedBefore = await wallClaimedByCount(page);
    ws.clear();

    // Real pointer drag: hover the wall tile, press, drag across the felt, release.
    const proj = await projectTileToCanvas(page, wallId as number);
    expect(proj.ok, `could not project wall tile ${wallId} to canvas`).toBe(true);
    await page.mouse.move(proj.clientX, proj.clientY, { steps: 8 });
    await page.waitForTimeout(120);
    await page.mouse.down();
    await page.mouse.move(proj.clientX + 60, proj.clientY - 40, { steps: 12 });
    await page.mouse.move(proj.clientX + 120, proj.clientY - 80, { steps: 12 });
    await page.waitForTimeout(120);
    await page.mouse.up();
    await page.waitForTimeout(1500);

    const fpAfter = await thingsFingerprint(page);
    const claimedAfter = await wallClaimedByCount(page);
    const outboundThings = ws.countThingsMutations();

    const metrics = {
      outboundThingsMutations: outboundThings,
      wallClaimedByDelta: claimedAfter - claimedBefore,
      localFingerprintChanged: fpAfter.hash !== fpBefore.hash,
      pageErrors: pageErrors.length,
    };
    recordRedEvidence({
      id: 'G16-auto-wall-nondraggable',
      expected: 'An arbitrary real-pointer wall drag is a no-op: wall tiles keep claimedBy=null, the local things fingerprint is unchanged, and 0 outbound things updates are sent.',
      observed: `outboundThings=${outboundThings}, claimedByDelta=${metrics.wallClaimedByDelta}, fingerprintChanged=${metrics.localFingerprintChanged}`,
      metrics,
    });

    expect(metrics.wallClaimedByDelta, 'dragging a wall tile must not hold/claim it locally').toBe(0);
    expect(fpAfter.hash, 'dragging a wall tile must not mutate the local things collection').toBe(fpBefore.hash);
    expect(outboundThings, 'dragging a wall tile must not push any outbound things update').toBe(0);
  });
});
