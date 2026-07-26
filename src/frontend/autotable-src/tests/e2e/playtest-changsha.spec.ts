// =============================================================================
//  CI real-UI walkthrough — PROMOTED TO A GATING REGRESSION TEST (#122, P1-11)
// =============================================================================
//
//  History: this spec (Phase K W23, Vasquez) used to walk home → lobby →
//  Changsha → connect → seat → deal and only WRITE findings — it could not
//  fail on a broken game. Per issue #122 (P1-11) it is now promoted to REAL
//  gating assertions, and (P1-4 E2E side) adds a real BOTS-PROGRESS gate.
//
//  Scope discipline: this is the "the real UI is not broken up to the known
//  blocker, and the bots engage" regression gate. It asserts ONLY the flow
//  that is verified to work at HEAD through real DOM/canvas interaction:
//    game boots → WS connects → real take-seat → real #deal → the manual deal
//    ceremony engages and BOTS PICK UP (pickup cursor advances across seats).
//  Full 4-hand completion + real Hu + scoring modal is the P0 keystone owned
//  by tests/e2e/playability-gate.spec.ts (blocked-green on #116/#119/#120).
//
//  NO WS backdoor is used here — every forward move is a real pointer/click
//  (see tests/e2e/_playability.ts). Screenshots/findings are still written to
//  playtest-artifacts/ for evidence.
// =============================================================================

import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import {
  makeConfig,
  buildGameUrl,
  defangOverlays,
  dismissLobbyAndTour,
  ensureConnected,
  takeSeatByClick,
  clickDeal,
  readPickup,
  readConnected,
  waitForGameObject,
} from './_playability';

const ARTIFACT_DIR = path.resolve(__dirname, '../../../../../playtest-artifacts');
if (!fs.existsSync(ARTIFACT_DIR)) fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

test.describe('Changsha Mahjong — real-UI walkthrough gate (boot→connect→seat→deal→bots)', () => {
  test('the real UI connects, seats, deals, and the bots engage', async ({ page, baseURL }) => {
    test.setTimeout(120_000);

    const pageErrors: string[] = [];
    page.on('pageerror', (err) => pageErrors.push(err.message));

    const resolvedBase =
      baseURL ?? process.env.E2E_BASE_URL ?? 'http://localhost:8080/autotable/';
    const cfg = makeConfig({ gameId: `changsha-walkthrough-${Date.now()}` });

    // ── Boot ─────────────────────────────────────────────────────────────
    await defangOverlays(page);
    await page.goto(buildGameUrl(resolvedBase, cfg), { waitUntil: 'domcontentloaded' });
    const booted = await waitForGameObject(page);
    await page.screenshot({ path: path.join(ARTIFACT_DIR, 'walkthrough-01-boot.png'), fullPage: true });
    expect(booted, 'window.game (client+world) never initialised — bundle failed to boot').toBe(true);

    // The bundle must not throw a fatal, uncaught page error while booting.
    expect(
      pageErrors,
      `Uncaught page errors during boot: ${JSON.stringify(pageErrors.slice(0, 5))}`,
    ).toEqual([]);

    // ── Real connect + seat ───────────────────────────────────────────────
    await dismissLobbyAndTour(page);
    const connected = await ensureConnected(page);
    expect(connected, 'WS never reached connected() state through the real connect flow').toBe(true);

    const seat = await takeSeatByClick(page, cfg.seat);
    await page.screenshot({ path: path.join(ARTIFACT_DIR, 'walkthrough-02-seat.png'), fullPage: true });
    expect(seat, 'real .take-seat click did not seat the human (client.seat is null)').not.toBeNull();

    // Canvas must be present for real pointer play.
    const canvasCount = await page.locator('#main, canvas').count();
    expect(canvasCount, 'no #main canvas — 3D scene did not mount').toBeGreaterThan(0);

    // ── Real deal ─────────────────────────────────────────────────────────
    const dealButton = page.locator('#deal');
    expect(await dealButton.count(), '#deal button missing').toBeGreaterThan(0);
    const dealt = await clickDeal(page);
    expect(dealt, 'real #deal press failed (button not visible/clickable)').toBe(true);

    // ── BOTS-PROGRESS GATE (P1-4 E2E side) ────────────────────────────────
    // After the real deal, the manual pickup ceremony must ENGAGE and the bots
    // must pick up: the server-authoritative pickup cursor advances through
    // multiple (phase, seat) states. We poll for distinct pickup states — if
    // the deal were broken (no ceremony, no bots), the cursor would never move.
    const pickupStates = new Set<string>();
    const deadline = Date.now() + 40_000;
    let lastPickupRaw: unknown = null;
    while (Date.now() < deadline && pickupStates.size < 2) {
      const p = await readPickup(page);
      if (p.present) {
        pickupStates.add(`${p.phase}@${p.seatIndex}`);
        lastPickupRaw = p.raw;
      }
      if (pickupStates.size >= 2) break;
      await page.waitForTimeout(1000);
    }
    await page.screenshot({ path: path.join(ARTIFACT_DIR, 'walkthrough-03-deal-ceremony.png'), fullPage: true });

    fs.writeFileSync(
      path.join(ARTIFACT_DIR, 'walkthrough-findings.json'),
      JSON.stringify(
        {
          url: page.url(),
          connected: await readConnected(page),
          seat,
          canvasCount,
          pickupStatesObserved: [...pickupStates],
          lastPickup: lastPickupRaw,
          pageErrors,
        },
        null,
        2,
      ),
    );

    expect(
      pickupStates.size,
      `BOTS DID NOT ENGAGE: the manual deal ceremony pickup cursor did not ` +
        `advance across ≥2 states after a real deal (observed: ${[...pickupStates].join(', ') || 'none'}). ` +
        `A broken deal or non-advancing bots would look exactly like this.`,
    ).toBeGreaterThanOrEqual(2);
  });
});
