// GATE (likely GREEN lock): full 4-hand human-vs-3-bot game reaches authoritative
// GameComplete via REAL clicks, in flat + perspective, on desktop + mobile
// (project matrix). Complements the existing playability-gate.
import { test, expect } from '@playwright/test';
import {
  buildGameUrl, makeConfig, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  clickDeal, waitForPlayableHand, discardByPointer, claimByClick, readClaimWindow,
  readGameComplete, isResultModalVisible, clickNextHand, readIsMyPickupTurn, takePickup,
  pressViewToggle, readCameraType,
} from './_playability';
import { recordEvidence, shot, installErrorGate } from './_uat_red';

test.describe('GATE gamecomplete: 4-hand authoritative GameComplete (real clicks)', () => {
  test('reaches GameComplete; toggles flat/perspective; zero errors', async ({ page }, testInfo) => {
    testInfo.setTimeout(240_000);
    const gate = installErrorGate(page);
    await page.setViewportSize(testInfo.project.name === 'mobile-chrome' ? { width: 390, height: 844 } : { width: 1600, height: 900 });
    const base = testInfo.project.use.baseURL as string;
    const cfg = makeConfig({ gameId: `red-gc-${testInfo.project.name}-${Date.now()}`, dealMode: 'auto', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
    await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(1200); await dismissLobbyAndTour(page); await ensureConnected(page);
    await takeSeatByClick(page, 0); await clickDeal(page); await waitForPlayableHand(page, 60_000).catch(() => {});

    const cam0 = await readCameraType(page);
    await pressViewToggle(page); await page.waitForTimeout(400);
    const cam1 = await readCameraType(page);
    await pressViewToggle(page); await page.waitForTimeout(300); // back

    const deadline = Date.now() + 200_000;
    let discards = 0, claimsPassed = 0, nextHands = 0;
    let gc = await readGameComplete(page);
    while (Date.now() < deadline && !gc.isComplete) {
      const claim = await readClaimWindow(page);
      if (claim.open) { await claimByClick(page); claimsPassed++; await page.waitForTimeout(400); }
      if (await readIsMyPickupTurn(page)) { await takePickup(page).catch(() => {}); await page.waitForTimeout(300); }
      const d = await discardByPointer(page); if (d.ok) discards++;
      if (await isResultModalVisible(page)) { if (await clickNextHand(page, 6000)) nextHands++; await page.waitForTimeout(600); }
      gc = await readGameComplete(page);
      await page.waitForTimeout(600);
    }
    await shot(page, `gc-${testInfo.project.name}-final.png`);
    recordEvidence(`red-gamecomplete-${testInfo.project.name}.json`, {
      project: testInfo.project.name, reachedGameComplete: gc.isComplete, discards, claimsPassed, nextHands,
      cameraToggle: { cam0, cam1, toggled: cam0 !== cam1 }, consoleErrors: gate.errors,
    });

    expect(cam0 !== cam1, 'view toggle must switch flat<->perspective').toBe(true);
    expect(gc.isComplete, `must reach authoritative GameComplete (discards=${discards}, nextHands=${nextHands})`).toBe(true);
    expect(gate.errors, `zero console/page errors; saw ${JSON.stringify(gate.errors.slice(0, 3))}`).toEqual([]);
  });
});
