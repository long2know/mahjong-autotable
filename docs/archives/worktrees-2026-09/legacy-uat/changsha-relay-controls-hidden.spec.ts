// UAT (Hudson) — Suite A / B2: the Changsha legacy-relay controls
// (Deal / Setup / Dealer / Connect / Game ID / Riichi links) must be ABSENT
// from the accessibility tree AND non-operable even if invoked, once a Changsha
// game is authoritative. These upstream-autotable relay affordances let a
// client unilaterally rebuild/deal/reset the table — illegitimate when the
// server owns state.
//
// RED baseline (200cad4): #deal, #toggle-setup, #deal-type, #toggle-dealer are
// present, visible, enabled, keyboard-focusable, in the a11y tree, and OPERABLE
// (invoking them emits outbound `things`/setup traffic). This spec pins the
// desired posture and is expected to FAIL until these controls are removed or
// neutralised for Changsha.

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  clickDeal, waitForGameObject,
} from './_playability';
import {
  resolveBase, uatGameUrl, freshGameId, recordOutboundWs, probeControl, recordRedEvidence,
} from './_uat-changsha';

// The upstream relay controls that must not remain operable in Changsha.
const LEGACY_RELAY_CONTROLS = ['deal', 'toggle-setup', 'deal-type', 'toggle-dealer'];

test.describe('UAT G7 — legacy relay controls absent from a11y tree & non-operable in Changsha', () => {
  test('Deal/Setup/Dealer/Setup-type are gone from the accessibility tree and inert when invoked', async ({ page, baseURL }) => {
    test.setTimeout(120_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('b2-relay-controls');

    const ws = recordOutboundWs(page);
    await defangOverlays(page);
    await page.goto(uatGameUrl(base, { gameId, dealMode: 'auto' }), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never booted').toBe(true);
    await dismissLobbyAndTour(page);
    expect(await ensureConnected(page), 'client never connected').toBe(true);
    await takeSeatByClick(page, 0);
    await clickDeal(page);
    await page.waitForTimeout(3000);

    // Probe each legacy control's a11y posture.
    const probes = await Promise.all(LEGACY_RELAY_CONTROLS.map((id) => probeControl(page, id)));
    const stillInA11y = probes.filter((p) => p.inAccessibilityTree);
    const stillFocusable = probes.filter((p) => p.focusable);

    // Non-operability check: programmatically invoke the Dealer toggle and a
    // Deal press and assert they push NO outbound tile/setup mutation.
    ws.clear();
    await page.locator('#toggle-dealer').click({ timeout: 3000 }).catch(() => {});
    await page.locator('#deal').click({ timeout: 3000 }).catch(() => {});
    await page.evaluate(() => {
      // Even a direct programmatic click must be inert (defence in depth).
      document.getElementById('toggle-dealer')?.click();
      document.getElementById('deal')?.click();
    });
    await page.waitForTimeout(1500);
    const mutationsAfterInvoke = ws.countThingsMutations();

    const metrics = {
      controlsStillInA11yTree: stillInA11y.length,
      controlsStillFocusable: stillFocusable.length,
      outboundMutationsAfterInvoke: mutationsAfterInvoke,
      probed: LEGACY_RELAY_CONTROLS.length,
    };
    recordRedEvidence({
      id: 'G7-relay-controls-hidden',
      expected: 'Deal/Setup/Dealer/Setup-type are absent from the a11y tree, non-focusable, and inert (0 outbound mutations) when invoked in Changsha.',
      observed: `inA11yTree=${stillInA11y.map((p) => p.id).join(',')} | focusable=${stillFocusable.map((p) => p.id).join(',')} | mutationsAfterInvoke=${mutationsAfterInvoke}`,
      metrics,
    });

    expect(
      stillInA11y.map((p) => p.id),
      'legacy relay controls must be removed from the accessibility tree in Changsha',
    ).toEqual([]);
    expect(
      stillFocusable.map((p) => p.id),
      'legacy relay controls must not be keyboard-focusable in Changsha',
    ).toEqual([]);
    expect(
      mutationsAfterInvoke,
      'invoking a legacy relay control (even programmatically) must be inert — 0 outbound tile mutations',
    ).toBe(0);
  });
});
