// UAT (Hudson) — Suite A / B5: for a Changsha game, the UI must NEVER surface
// Riichi / four_player / "4p" labels — not on first paint, not after the first
// authoritative update, not after a deal, and not after a reconnect. The legacy
// upstream variant picker leaks Riichi/FOUR_PLAYER option labels into the
// accessibility tree even when Changsha is the active variant.
//
// This deliberately inspects the REAL accessibility tree (Playwright a11y
// snapshot), NOT a computed <option> proxy read off a closed <select> — a
// screen-reader user hears these option names, so they count as surfaced.
//
// RED baseline (200cad4): #game-type exposes "Riichi — 4 player" /
// FOUR_PLAYER / THREE_PLAYER accessible names throughout the lifecycle. Pins the
// desired posture; expected to FAIL until the Changsha shell stops rendering the
// Riichi variant options.

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  clickDeal, waitForGameObject,
} from './_playability';
import {
  resolveBase, uatGameUrl, freshGameId, accessibilityLabelsMatching, recordRedEvidence,
} from './_uat-changsha';

const FORBIDDEN = /riichi|four[\s_-]?player|three[\s_-]?player|\b4p\b/i;

test.describe('UAT B5 — Changsha never surfaces Riichi/four_player/4p labels', () => {
  test('no forbidden variant labels across first-paint → authoritative update → deal → reconnect', async ({ page, baseURL }) => {
    test.setTimeout(120_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('b5-no-riichi');
    const url = uatGameUrl(base, { gameId, dealMode: 'auto' });

    // ── Phase 1: first paint (pre-connect DOM) ──────────────────────────
    await defangOverlays(page);
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never booted').toBe(true);
    const atFirstPaint = await accessibilityLabelsMatching(page, FORBIDDEN);

    // ── Phase 2: connected + first authoritative update ─────────────────
    await dismissLobbyAndTour(page);
    expect(await ensureConnected(page), 'client never connected').toBe(true);
    const atConnected = await accessibilityLabelsMatching(page, FORBIDDEN);

    // ── Phase 3: after a deal ───────────────────────────────────────────
    await takeSeatByClick(page, 0);
    await clickDeal(page);
    await page.waitForTimeout(3000);
    const atDealt = await accessibilityLabelsMatching(page, FORBIDDEN);

    // ── Phase 4: after a reconnect (reload the same isolated game) ───────
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never re-booted').toBe(true);
    await dismissLobbyAndTour(page);
    await ensureConnected(page);
    const atReconnect = await accessibilityLabelsMatching(page, FORBIDDEN);

    const union = Array.from(new Set([...atFirstPaint, ...atConnected, ...atDealt, ...atReconnect]));
    const metrics = {
      firstPaint: atFirstPaint.length,
      connected: atConnected.length,
      dealt: atDealt.length,
      reconnect: atReconnect.length,
      distinctForbiddenLabels: union.length,
    };
    recordRedEvidence({
      id: 'B5-no-riichi-labels',
      expected: 'A Changsha game surfaces ZERO Riichi/four_player/4p accessible labels across the whole lifecycle.',
      observed: `distinct forbidden labels=${union.length} → ${union.slice(0, 6).join(' | ')}`,
      metrics,
    });

    expect(atFirstPaint, `Riichi/4p labels present on first paint: ${atFirstPaint.join(' | ')}`).toEqual([]);
    expect(atConnected, `Riichi/4p labels present when connected: ${atConnected.join(' | ')}`).toEqual([]);
    expect(atDealt, `Riichi/4p labels present after deal: ${atDealt.join(' | ')}`).toEqual([]);
    expect(atReconnect, `Riichi/4p labels present after reconnect: ${atReconnect.join(' | ')}`).toEqual([]);
  });
});
