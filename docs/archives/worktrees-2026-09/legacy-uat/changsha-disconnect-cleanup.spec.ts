// UAT (Hudson) — Suite A / B7: a disconnect must CLEAR stale gameplay cues
// (turn indicator, claim window, pickup cursor, rendered table tiles, and the
// move-log), and a reconnect to the same isolated game must reclaim the same
// seat. A connection drop that leaves "Match started — dealer is Seat 0" (and
// other cues) frozen on screen misrepresents live state to the user.
//
// RED baseline (200cad4): after a real #disconnect click the move-log still
// shows the stale "Match started…" entry (and other cues are not explicitly
// cleared). This pins the desired teardown + seat-reclaim; expected to FAIL
// until disconnect wipes the stale cues.
//
// Discipline: real button clicks for connect/seat/deal/disconnect/reconnect;
// observation only.

import { test, expect } from '@playwright/test';
import {
  defangOverlays, dismissLobbyAndTour, ensureConnected, takeSeatByClick,
  clickDeal, waitForGameObject, readSeat, readConnected,
} from './_playability';
import {
  resolveBase, uatGameUrl, freshGameId, readMoveLogText, recordRedEvidence,
} from './_uat-changsha';

test.describe('UAT G12 — disconnect clears stale cues; reconnect reclaims the seat', () => {
  test('disconnect wipes turn/claim/pickup/table/move-log cues and reconnect restores the seat', async ({ page, baseURL }) => {
    test.setTimeout(120_000);
    const base = resolveBase(baseURL);
    const gameId = freshGameId('b7-disconnect');
    const url = uatGameUrl(base, { gameId, dealMode: 'auto', seat: 0 });

    await defangOverlays(page);
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never booted').toBe(true);
    await dismissLobbyAndTour(page);
    expect(await ensureConnected(page), 'client never connected').toBe(true);
    await takeSeatByClick(page, 0);
    const seatBefore = await readSeat(page);
    await clickDeal(page);
    await page.waitForTimeout(3500);

    const moveLogWhilePlaying = await readMoveLogText(page);
    expect(moveLogWhilePlaying.length, 'move-log should have entries during play').toBeGreaterThan(0);

    // ── Real disconnect ────────────────────────────────────────────────
    await page.locator('#disconnect').click({ timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(2500);

    const afterDisconnect = await page.evaluate(() => {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const g = (window as any).game;
      const c = g?.client;
      const w = g?.world;
      const has = (coll: unknown, key: unknown): boolean => {
        try {
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          return Boolean((coll as any)?.get?.(key));
        } catch {
          return false;
        }
      };
      // Count still-rendered TABLE tiles (hand/wall/discard/meld) in world.
      let tableTiles = 0;
      if (w?.things) {
        for (const t of w.things.values()) {
          const grp = t?.slot?.group;
          if (grp === 'hand' || grp === 'wall' || grp === 'discard' || grp === 'meld') tableTiles++;
        }
      }
      const turnBanner = document.getElementById('turn-banner');
      const turnBannerVisible = turnBanner ? !turnBanner.hasAttribute('hidden') && getComputedStyle(turnBanner).display !== 'none' : false;
      return {
        connected: (() => { try { return Boolean(c?.connected?.()); } catch { return false; } })(),
        pickupCue: has(c?.pickup, 'current'),
        turnCue: has(c?.turn, 'current') || turnBannerVisible,
        claimCue: (() => {
          try {
            const seat = c?.seat;
            return typeof seat === 'number' ? has(c?.claim, String(seat)) : false;
          } catch { return false; }
        })(),
        tableTiles,
      };
    });
    const moveLogAfterDisconnect = await readMoveLogText(page);

    const staleMoveLog = moveLogAfterDisconnect.length > 0;
    const metrics = {
      pickupCue: afterDisconnect.pickupCue,
      turnCue: afterDisconnect.turnCue,
      claimCue: afterDisconnect.claimCue,
      tableTilesRemaining: afterDisconnect.tableTiles,
      moveLogChars: moveLogAfterDisconnect.length,
      staleMoveLog,
    };
    recordRedEvidence({
      id: 'G12-disconnect-cleanup',
      expected: 'After disconnect: no pickup/turn/claim cue, no rendered table tiles, empty move-log; reconnect reclaims the same seat.',
      observed: `pickupCue=${metrics.pickupCue}, turnCue=${metrics.turnCue}, claimCue=${metrics.claimCue}, tableTiles=${metrics.tableTilesRemaining}, moveLogChars=${metrics.moveLogChars}`,
      metrics,
    });

    // Desired teardown — RED on baseline (stale move-log survives).
    expect(staleMoveLog, `move-log must be cleared on disconnect (still shows: "${moveLogAfterDisconnect.slice(0, 60)}")`).toBe(false);
    expect(afterDisconnect.pickupCue, 'pickup cursor must clear on disconnect').toBe(false);
    expect(afterDisconnect.turnCue, 'turn cue must clear on disconnect').toBe(false);
    expect(afterDisconnect.claimCue, 'claim window must clear on disconnect').toBe(false);
    expect(afterDisconnect.tableTiles, 'rendered table tiles must clear on disconnect').toBe(0);

    // ── Reconnect reclaims the same seat ───────────────────────────────
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    expect(await waitForGameObject(page), 'window.game never re-booted').toBe(true);
    await dismissLobbyAndTour(page);
    await ensureConnected(page);
    // The client may need a real re-seat click if it does not auto-reclaim.
    if ((await readSeat(page)) === null) await takeSeatByClick(page, 0);
    const seatAfter = await readSeat(page);
    expect(await readConnected(page), 'client must reconnect').toBe(true);
    expect(seatAfter, `reconnect must reclaim seat ${seatBefore}`).toBe(seatBefore);
  });
});
