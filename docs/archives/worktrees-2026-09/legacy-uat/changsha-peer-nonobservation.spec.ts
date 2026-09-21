// Ripley fast-tests lane — G18 (SKELETON): peer non-observation. Two independent
// clients (A and B) share one Changsha game. When A performs real actions that
// mutate its own private state (drags/deals/rolls), client B must NEVER receive
// A's private `things`/`match`/`dice` frames — the authoritative server withholds
// peer-private state instead of relaying it.
//
// Status: SKELETON (test.fixme) AND SCHEMA-FLAGGED. This gate is RED@200cad4
// (the relay bundle broadcasts every UPDATE, so B sees A's frames verbatim), but
// turning it GREEN requires the authoritative privacy/withholding schema — the
// same server-side privacy contract that backs the P1–P5 / G19 privacy lane.
// >>> FLAG for Ripley: G18 depends on the new backend privacy schema and should
//     move to the hudson-1 privacy lane if it cannot be satisfied schema-free. <<<
// The body is left implemented so it can be lifted directly into that lane.
//
// Real-UI only: A acts via real pointer/controls; B is a passive second browser
// context that only OBSERVES its own client state. No injection on either side.

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick, clickDeal,
  waitForGameObject, openPeerClient, uatGameUrl, freshGameId, resolveBase,
  recordRedEvidence,
} from './helpers/changsha-real-pointer';

async function peerReceivedPrivateFrames(page: import('@playwright/test').Page): Promise<{ things: number; match: boolean; dice: boolean }> {
  return page.evaluate(() => {
    const g = (window as unknown as { game?: { client?: { things?: Map<unknown, unknown>; match?: Map<number, unknown> } } }).game;
    const client = g?.client;
    const thingsCount = client?.things ? (client.things as Map<unknown, unknown>).size : 0;
    const match0 = client?.match?.get(0) as { dealer?: unknown; dice?: unknown } | undefined;
    return {
      things: thingsCount,
      match: !!match0 && match0.dealer !== undefined,
      dice: !!match0 && match0.dice !== undefined,
    };
  });
}

test.describe('UAT G18 — peer non-observation (B never receives A private frames)', () => {
  test.fixme('client B never receives client A private things/match/dice frames', async ({ browser, baseURL }) => {
    test.setTimeout(120_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('g18-peer-nonobs');

    // Client A — seats and drives real actions.
    const a = await openPeerClient(browser);
    await defangOverlays(a.page);
    await a.page.goto(uatGameUrl(base, { gameId, dealMode: 'auto', seat: 0 }), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(a.page)).toBe(true);
    await dismissLobbyAndTour(a.page);
    await ensureConnected(a.page);
    await takeSeatByClick(a.page, 0);

    // Client B — joins the SAME game as a different seat and only observes.
    const b = await openPeerClient(browser);
    await defangOverlays(b.page);
    await b.page.goto(uatGameUrl(base, { gameId, dealMode: 'auto', seat: 1 }), { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(b.page)).toBe(true);
    await dismissLobbyAndTour(b.page);
    await ensureConnected(b.page);
    await takeSeatByClick(b.page, 1);

    // A performs private-state-mutating actions.
    await clickDeal(a.page);
    await a.page.waitForTimeout(3000);

    // B must not have received A's private things/match/dice.
    const seen = await peerReceivedPrivateFrames(b.page);

    recordRedEvidence({
      id: 'G18-peer-nonobservation',
      expected: 'Client B never receives client A private things/match/dice frames (server withholds peer-private state, does not relay).',
      observed: `B.things=${seen.things}, B.matchDealer=${seen.match}, B.dice=${seen.dice}`,
      metrics: { peerThings: seen.things, peerMatch: seen.match, peerDice: seen.dice },
    });

    await a.ctx.close();
    await b.ctx.close();

    expect(seen.things, 'B must not receive A private tile (things) frames').toBe(0);
    expect(seen.match, 'B must not receive A private match frames').toBe(false);
    expect(seen.dice, 'B must not receive A private dice frames').toBe(false);
  });
});
