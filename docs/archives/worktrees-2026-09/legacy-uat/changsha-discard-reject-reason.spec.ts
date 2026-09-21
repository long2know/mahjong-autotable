// Ripley fast-tests lane — G8 (SKELETON): a rejected discard must surface a
// VISIBLE, human-readable reason. When a player attempts an illegal discard
// (out of turn, or before the deal), the authoritative server rejects it and the
// UI must render a legible reason ("Not your turn", "No hand yet", …) rather than
// silently swallowing or (worse) locally faking the move.
//
// Status: SKELETON (test.fixme). The body drives a real out-of-turn discard
// attempt via pointer and asserts a visible reason surface appears, but the exact
// reason-banner selector and the authoritative reject signalling are part of the
// not-yet-landed Bishop/Hicks discard-validation wire. Activate (remove `.fixme`)
// once the reject-reason UI lands. RED@200cad4: the relay bundle neither validates
// the discard nor shows any reason.
//
// Real-UI only: real pointer discard attempt; no emitDiscard, no injection.

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  waitForGameObject, discardByPointer, uatGameUrl, freshGameId, resolveBase,
  recordRedEvidence,
} from './helpers/changsha-real-pointer';

// Candidate reason surfaces the authoritative UI may use.
const REASON_SELECTORS = ['#discard-reject', '.discard-reject-reason', '.reject-banner', '#turn-banner .reject', '[data-reject-reason]'];

test.describe('UAT G8 — a rejected discard shows a visible reason', () => {
  test.fixme('attempting an out-of-turn / pre-deal discard surfaces a legible reject reason', async ({ page, baseURL }) => {
    test.setTimeout(90_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('g8-discard-reject');

    await defangOverlays(page);
    await page.goto(uatGameUrl(base, { gameId, dealMode: 'auto', seat: 1 }), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page)).toBe(true);
    await dismissLobbyAndTour(page);
    await ensureConnected(page);
    await takeSeatByClick(page, 1);

    // Attempt a discard while it is NOT this seat's turn (seat 1 right after boot,
    // dealer seat 0 acts first) — the server must reject and the UI must explain.
    const attempt = await discardByPointer(page).catch(() => ({ ok: false, reason: 'gesture-threw' }));

    let reasonVisible = false;
    let reasonText = '';
    for (const sel of REASON_SELECTORS) {
      const loc = page.locator(sel).first();
      if (await loc.isVisible().catch(() => false)) {
        reasonVisible = true;
        reasonText = (await loc.textContent().catch(() => '')) ?? '';
        break;
      }
    }

    recordRedEvidence({
      id: 'G8-discard-reject-reason',
      expected: 'An illegal discard is rejected by the server AND the UI shows a visible, legible reason; the tile is not locally moved.',
      observed: `discardGestureOk=${attempt.ok}, reasonVisible=${reasonVisible}, reasonText="${reasonText.trim().slice(0, 60)}"`,
      metrics: { discardGestureOk: attempt.ok, reasonVisible, reasonChars: reasonText.trim().length },
    });

    expect(reasonVisible, 'a rejected discard must surface a visible reason banner').toBe(true);
    expect(reasonText.trim().length, 'the reject reason must be human-readable text').toBeGreaterThan(0);
  });
});
