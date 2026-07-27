// =============================================================================
//  #137 REGRESSION — the perspective-toggle key (`p`) must never commit a claim.
// =============================================================================
//
//  Root cause of #137: the shipped bundle bound the SAME `p` key to two things —
//    • game.ts onKeyDown → toggle the perspective / flat camera (a view control)
//    • claim-window-overlay.ts onKeyDown → commit a Pung (an irreversible meld)
//  So a human pressing `p` to change the camera while a claim window (with a Pung
//  opportunity) happened to be open SILENTLY MELDED. Post-#135 that meld was
//  accepted by the server, leaving the human holding a meld with no drawn 14th
//  tile — unable to click-to-discard (world.hasExtraHandTile() counts only
//  hand-group tiles) — and the hand wedged forever (handEnds=0). That is exactly
//  the stall the P0 playability gate hit.
//
//  This gate presses the REAL `p` key while a REAL, server-opened meld-claim
//  window is open and asserts the bundle sends NO meld claim on the wire. It is
//  RED on the pre-fix bundle (`{action:"claim","type":"Pung"}` is emitted) and
//  GREEN once `p` is removed from the claim-overlay keyboard map.
//
//  Real keyboard + real WS only. We NEVER inject state or drive a claim: the
//  claim windows are opened by the real server as bots discard. Discards use a
//  canvas-only click guard so the pointer can never itself hit the bottom claim
//  badges (a separate overlap) — isolating the assertion to the `p` KEY.
// =============================================================================

import { test, expect } from '@playwright/test';
import type { Page } from '@playwright/test';
import {
  makeConfig,
  buildGameUrl,
  defangOverlays,
  dismissLobbyAndTour,
  ensureConnected,
  takeSeatByClick,
  clickDeal,
  waitForPlayableHand,
  waitForGameObject,
  readClaimWindow,
  readMyHandTiles,
  readDiscardCount,
  projectTileToCanvas,
  hasExtraHandTile,
  readIsMyPickupTurn,
  takePickup,
  readCameraType,
} from './_playability';

function resolveBase(baseURL: string | undefined): string {
  return baseURL ?? process.env.E2E_BASE_URL ?? 'http://localhost:8080/autotable/';
}

const MELD_TYPES = ['Pung', 'Chow', 'Kong'];

// Count of meld claims the bundle has emitted on the socket so far.
function readMeldClaims(page: Page): Promise<number> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return page.evaluate(() => ((window as any).__meldClaimsSent as unknown[]).length);
}

// Discard a hand tile via a REAL canvas click, but ONLY when the projected
// screen point actually resolves to the WebGL canvas — never an overlay badge.
// This keeps the pointer from ever committing a meld itself, so a captured meld
// claim can only have come from the `p` keypress under test. Returns true when a
// discard fired.
async function guardedCanvasDiscard(page: Page): Promise<boolean> {
  if ((await readClaimWindow(page)).open) return false; // never click over the badges
  const before = await readDiscardCount(page);
  const tiles = await readMyHandTiles(page);
  if (tiles.length === 0) return false;
  const mid = Math.floor(tiles.length / 2);
  const order = [mid];
  for (let off = 1; order.length < Math.min(5, tiles.length); off++) {
    if (mid + off < tiles.length) order.push(mid + off);
    if (mid - off >= 0 && order.length < 5) order.push(mid - off);
  }
  for (const idx of order) {
    const proj = await projectTileToCanvas(page, tiles[idx]);
    if (!proj.ok) continue;
    // Only click if the point is the canvas AND no claim window snuck open.
    const target = await page.evaluate(
      ({ x, y }) => {
        const el = document.elementFromPoint(x, y);
        return { tag: el?.tagName ?? null, id: (el as HTMLElement | null)?.id ?? null };
      },
      { x: proj.clientX, y: proj.clientY },
    );
    if (target.tag !== 'CANVAS' && target.id !== 'main') continue;
    if ((await readClaimWindow(page)).open) return false;
    await page.mouse.move(proj.clientX, proj.clientY, { steps: 6 });
    await page.waitForTimeout(90);
    await page.mouse.down();
    await page.waitForTimeout(80);
    await page.mouse.up();
    await page.waitForTimeout(1000);
    if ((await readDiscardCount(page)) > before) return true;
  }
  return false;
}

test.describe('@playability-gate #137 keyboard-collision regression', () => {
  test('pressing the perspective key (p) during a claim window never sends a meld claim', async ({
    page,
    baseURL,
  }, testInfo) => {
    // Desktop-canonical: the collision is a physical-keyboard shortcut. The
    // hc1/hc8/hc16 + 4-hand human gates already cover mobile playability.
    test.skip(
      testInfo.project.name === 'mobile-chrome',
      'p-key collision is a desktop physical-keyboard shortcut; mobile has no `p` view toggle',
    );
    test.setTimeout(4 * 60_000);

    // OBSERVE-ONLY spy: capture every outbound frame that commits a meld claim
    // (the exact wire shape game-ui.ts:sendClaim / the claim overlay emit). This
    // reads the socket; it never writes to it or mutates game state.
    await page.addInitScript(() => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (window as any).__meldClaimsSent = [];
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const orig = (WebSocket.prototype as any).send;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (WebSocket.prototype as any).send = function (data: any) {
        try {
          const s = typeof data === 'string' ? data : '';
          if (s.includes('"claim"') && /"type":"(Pung|Chow|Kong)"/.test(s)) {
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            (window as any).__meldClaimsSent.push(s);
          }
        } catch { /* ignore */ }
        // eslint-disable-next-line prefer-rest-params
        return orig.apply(this, arguments);
      };
    });

    const cfg = makeConfig({
      handCount: 4,
      seed: 4100,
      botDifficulty: 'Hard',
      gameId: `claim-key-collision-${process.env.PLAYABILITY_RUN_ID ?? 'local'}-w${testInfo.workerIndex}`,
    });

    await defangOverlays(page);
    await page.goto(buildGameUrl(resolveBase(baseURL), cfg), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'game object never booted').toBe(true);
    await dismissLobbyAndTour(page);
    expect(await ensureConnected(page), 'WS never connected').toBe(true);
    const seat = await takeSeatByClick(page, cfg.seat);
    expect(seat, 'seat 0 not taken').toBe(0);
    expect(await clickDeal(page), 'deal press failed').toBe(true);
    expect((await waitForPlayableHand(page, 45_000)).playable, 'no playable hand dealt').toBe(true);

    let meldWindowsExercised = 0;
    let pPresses = 0;
    const deadline = Date.now() + 3 * 60_000;
    while (Date.now() < deadline && meldWindowsExercised < 3) {
      const claim = await readClaimWindow(page);
      if (claim.open) {
        if (claim.available.some((a) => MELD_TYPES.includes(a))) {
          const before = await readMeldClaims(page);
          // THE collision trigger: press the perspective-view key while a meld
          // claim window is open. The bundle must NOT read this as a meld.
          await page.keyboard.press('p');
          pPresses++;
          await page.waitForTimeout(500);
          expect(
            (await readMeldClaims(page)) - before,
            `pressing "p" (perspective toggle) during a claim window offering ${JSON.stringify(
              claim.available,
            )} committed a MELD — the #137 keyboard collision has regressed`,
          ).toBe(0);
          meldWindowsExercised++;
        }
        // Decline via the real Esc pass shortcut so the hand keeps moving (never
        // meld). We deliberately do NOT click #claim-pass here: the additive
        // bottom-center claim overlay (z-index 1080, pointer-events:auto while a
        // window is open) can sit over the side-panel Pass button, so a Playwright
        // #claim-pass click issued right after the `p` view-toggle occasionally
        // resolves onto the overlay's Chow badge and commits a meld — a
        // test-interaction artifact that has nothing to do with the `p` key under
        // test. Esc (overlay.commitPass) is unambiguous and cannot hit a badge.
        await page.keyboard.press('Escape');
        await page.waitForTimeout(400);
        continue;
      }
      if (await readIsMyPickupTurn(page)) {
        await takePickup(page);
        await page.waitForTimeout(400);
        continue;
      }
      if (await hasExtraHandTile(page)) {
        await guardedCanvasDiscard(page);
        await page.waitForTimeout(300);
        continue;
      }
      await page.waitForTimeout(600);
    }

    // The whole game must never have leaked a single meld claim from a keypress.
    expect(await readMeldClaims(page), 'a meld claim was emitted from a keypress during the game').toBe(0);
    // Anti-vacuous: prove we actually pressed `p` against real meld windows.
    expect(
      meldWindowsExercised,
      `expected to exercise ≥1 real meld-claim window with the p-key (pPresses=${pPresses}); ` +
        'the collision path was never tested',
    ).toBeGreaterThan(0);
    // And `p` still does its real job: a camera kind is observable.
    expect(await readCameraType(page), 'perspective/flat camera not observable').not.toBeNull();
  });
});
