// =============================================================================
//  #137 / #139 — the perspective-toggle key `p` must NEVER commit a claim.
// =============================================================================
//
//  Root cause: the shipped bundle bound the SAME `p` key to two independent
//  window `keydown` listeners —
//    • game.ts        onKeyDown → toggle the perspective / flat camera (a view
//                                 control the P0 gate presses to exercise views)
//    • claim-window-overlay.ts onKeyDown → commit a Pung (an irreversible meld)
//  `preventDefault()` in one listener does NOT stop the other, so a human (or
//  the gate) pressing `p` to change the camera *while a Pung claim window was
//  open* silently melded. Post-#135 that meld is accepted by the server, the
//  human is left holding a meld with no drawn 14th tile — cannot click-to-
//  discard — and the hand wedges forever (handEnds=0). That is exactly the
//  intermittent P0 stall (#137 / #139).
//
//  This is a REAL-input, authoritative-WS regression. The claim window is opened
//  by the real server as bots discard; we press the REAL `p` KEY and assert the
//  bundle emits NO meld claim on the socket (RED on the pre-fix bundle, which
//  emits `{action:"claim","type":"Pung"}`; GREEN once `p` is removed from the
//  claim-overlay keyboard map). The same window then proves the pointer path is
//  intact: clicking the visible Pung control DOES emit the claim.
//
//  We assert WIRE effects (a socket-send spy) and STATE effects (the camera
//  kind, the live claim window), never source text. No injected state, no
//  synthetic DOM events, no client.update, no backdoors.
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
  readCameraType,
  hasExtraHandTile,
  readIsMyPickupTurn,
  takePickup,
  discardByPointer,
} from './_playability';

function resolveBase(baseURL: string | undefined): string {
  return baseURL ?? process.env.E2E_BASE_URL ?? 'http://localhost:8080/autotable/';
}

// OBSERVE — count of meld claims the bundle has put on the socket so far.
function readMeldClaims(page: Page): Promise<number> {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  return page.evaluate(() => ((window as any).__ferroMeldClaims as unknown[]).length);
}

// game.ts and the claim overlay both ignore keydowns whose target is an
// INPUT/TEXTAREA, so make sure a text field never holds focus before we press
// the real view key. We never click the canvas here (that would discard/select).
async function blurTextInputs(page: Page): Promise<void> {
  await page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null;
    if (el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA')) el.blur();
  });
}

test.describe('@playability-gate #137 claim-key collision (frontend regression)', () => {
  test('pressing `p` during a Pung window toggles the view and sends NO meld; clicking the Pung control still claims', async ({
    page,
    baseURL,
  }, testInfo) => {
    // Desktop-canonical: the collision is a physical-keyboard shortcut. Mobile
    // has no `p` view toggle and is covered by the hc1/4-hand gates.
    test.skip(
      testInfo.project.name === 'mobile-chrome',
      'the `p` perspective toggle is a desktop physical-keyboard shortcut; mobile has no `p` view key',
    );
    test.setTimeout(4 * 60_000);

    // OBSERVE-ONLY spy: capture every outbound frame that commits a meld claim
    // (the exact wire shape of game-ui.ts sendClaim / the claim overlay emit).
    // It reads the socket; it never writes to it or mutates game state.
    await page.addInitScript(() => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (window as any).__ferroMeldClaims = [];
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const orig = (WebSocket.prototype as any).send;
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      (WebSocket.prototype as any).send = function (data: any) {
        try {
          const s = typeof data === 'string' ? data : '';
          if (s.includes('"claim"') && /"type":"(Pung|Chow|Kong)"/.test(s)) {
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            (window as any).__ferroMeldClaims.push(s);
          }
        } catch {
          /* ignore */
        }
        // eslint-disable-next-line prefer-rest-params
        return orig.apply(this, arguments);
      };
    });

    const cfg = makeConfig({
      handCount: 4,
      seed: 4100,
      botDifficulty: 'Hard',
      // Unique gameId per invocation so the backend's first-creator-wins never
      // hands us a stale, already-played game; seed=4100 still drives the
      // deterministic shuffle that reliably reaches a Pung claim window.
      gameId: `claim-key-collision-ferro-${process.env.PLAYABILITY_RUN_ID ?? 'local'}-w${testInfo.workerIndex}-${Date.now()}`,
    });

    await defangOverlays(page);
    await page.goto(buildGameUrl(resolveBase(baseURL), cfg), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'game object never booted').toBe(true);
    await dismissLobbyAndTour(page);
    expect(await ensureConnected(page), 'WS never connected').toBe(true);
    expect(await takeSeatByClick(page, cfg.seat), 'seat 0 not taken').toBe(0);
    expect(await clickDeal(page), 'deal press failed').toBe(true);
    expect((await waitForPlayableHand(page, 45_000)).playable, 'no playable hand dealt').toBe(true);

    // Drive REAL play until the server opens a claim window that offers Pung.
    let pungExercised = false;
    const deadline = Date.now() + 3 * 60_000;
    while (Date.now() < deadline && !pungExercised) {
      const claim = await readClaimWindow(page);
      if (claim.open) {
        if (claim.available.includes('Pung')) {
          // ── (a) THE collision trigger: a REAL `p` key while a Pung window is
          //        open. It must flip the camera and emit NO meld. ────────────
          await blurTextInputs(page);
          const camBefore = await readCameraType(page);
          const meldBefore = await readMeldClaims(page);
          await page.keyboard.press('p');
          await page.waitForTimeout(600);

          expect(
            (await readMeldClaims(page)) - meldBefore,
            `pressing "p" (perspective toggle) during a Pung window committed a MELD on the wire — the #137 key collision regressed`,
          ).toBe(0);
          const camAfter = await readCameraType(page);
          expect(camAfter, 'pressing "p" produced no observable camera — the real view toggle broke').not.toBeNull();
          expect(camAfter, `pressing "p" did not flip the camera (still ${String(camBefore)}) — it must remain the perspective toggle`).not.toBe(
            camBefore,
          );
          const afterP = await readClaimWindow(page);
          expect(
            afterP.open && afterP.available.includes('Pung'),
            'the Pung window closed after "p" — the view key must never pass or claim',
          ).toBe(true);

          // ── (b) The pointer path is intact: clicking the visible Pung control
          //        DOES commit the claim on the wire. ──────────────────────────
          const pungChip = page.locator('[data-claim-type="Pung"]').first();
          await expect(pungChip, 'the Pung claim chip must be a visible pointer/touch control').toBeVisible();
          await expect(pungChip).toBeEnabled();
          await pungChip.click();
          await page.waitForTimeout(700);
          expect(
            (await readMeldClaims(page)) - meldBefore,
            'clicking the visible Pung control did NOT emit a meld claim — Pung must stay pointer-accessible',
          ).toBeGreaterThan(0);

          pungExercised = true;
          break;
        }
        // A non-Pung window: decline via the real Pass button so play advances
        // (we never meld from a keypress).
        await page.locator('#claim-pass').first().click({ timeout: 3000 }).catch(() => undefined);
        await page.waitForTimeout(400);
        continue;
      }
      if (await readIsMyPickupTurn(page)) {
        await takePickup(page);
        await page.waitForTimeout(400);
        continue;
      }
      if (await hasExtraHandTile(page)) {
        await discardByPointer(page);
        await page.waitForTimeout(300);
        continue;
      }
      await page.waitForTimeout(500);
    }

    // Anti-vacuous: we actually pressed `p` against a real Pung window.
    expect(
      pungExercised,
      'never reached a real Pung claim window within the budget — the collision path was not exercised',
    ).toBe(true);
  });
});
