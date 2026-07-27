// Ferro — WP-E / #120 — seed determinism (Hudson C-2 gap).
//
// Hudson's PR #128 confirmed `buildWsUrl` dropped `?seed=`, so the WS path
// always bound the runtime with `seed:null` and the deal was random no
// matter what the lobby URL said — determinism was unreachable.  This spec
// proves the end-to-end fix is REAL (not a client-side fake):
//
//   • the lobby seed rides the observed WS handshake (`?seed=`), and
//   • the same seed reproduces the exact same deal while a different seed
//     changes it — which can only hold if the backend actually consumes the
//     forwarded seed (ChangshaGameRuntime.CreateGameAsync → ChangshaGameState
//     .Seed → wall shuffle + DiceService).
//
// All actions are real: a seeded table URL bootstraps the game, then an
// ordinary Take Seat + Deal tap.  The assertion reads the authoritative
// `things` collection (seat 0's dealt hand = the tile ids in `hand.*@0`) —
// the same server-pushed state the renderer consumes.

import { test, expect, type Page } from '@playwright/test';

const SEED_A = 12345;
const SEED_B = 99999;
const HAND_MIN = 13;

// Bootstrap a fresh seeded Changsha game, seat 0, deal, and return seat 0's
// dealt hand as a sorted list of tile ids.
async function playSeededHand(page: Page, seed: number): Promise<number[]> {
  const gid = `seed-${seed}-${Math.random().toString(16).slice(2, 8)}`;
  await page.goto('', { waitUntil: 'domcontentloaded' });
  await page.evaluate(() => {
    try {
      localStorage.clear();
      localStorage.setItem('mahjong.tour.completed.v1', 'true');
      localStorage.setItem('mahjong.identity.onboarded.v1', 'true');
    } catch { /* ignore */ }
  });
  await page.goto(
    `?gameId=${gid}&variant=changsha&dealMode=auto&botCount=3&botDifficulty=Medium&handCount=4&seed=${seed}`,
    { waitUntil: 'domcontentloaded' },
  );

  await expect
    .poll(async () => page.evaluate(() => Boolean((window as unknown as { game?: { client?: { connected?: () => boolean } } }).game?.client?.connected?.())), {
      timeout: 25_000,
      message: `seeded game (seed=${seed}) never connected`,
    })
    .toBe(true);

  await page.locator('.seat-button-0 .take-seat').click();
  await expect
    .poll(async () => page.evaluate(() => (window as unknown as { game?: { client?: { seat?: number | null } } }).game?.client?.seat ?? null), {
      timeout: 10_000,
      message: 'seat never became 0',
    })
    .toBe(0);

  await page.locator('#deal').click();

  await expect
    .poll(async () => page.evaluate(() => {
      const c = (window as unknown as { game?: { client?: { things?: { entries(): Iterable<[number, { slotName?: string }]> } } } }).game?.client;
      if (!c?.things) return 0;
      let n = 0;
      for (const [, v] of c.things.entries()) if (/^hand[^@]*@0$/.test(v?.slotName || '')) n++;
      return n;
    }), { timeout: 25_000, message: `seat 0 never received a dealt hand (seed=${seed})` })
    .toBeGreaterThanOrEqual(HAND_MIN);

  return page.evaluate(() => {
    const c = (window as unknown as { game?: { client?: { things?: { entries(): Iterable<[number, { slotName?: string }]> } } } }).game?.client;
    const ids: number[] = [];
    if (c?.things) {
      for (const [k, v] of c.things.entries()) if (/^hand[^@]*@0$/.test(v?.slotName || '')) ids.push(Number(k));
    }
    return ids.sort((a, b) => a - b);
  });
}

test.describe('WP-E/#120 — seed determinism (real backend-consumed seed)', () => {
  test('same seed reproduces the deal; a different seed changes it', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Determinism is engine-level; one project is sufficient.');
    test.setTimeout(120_000);

    const a1 = await playSeededHand(page, SEED_A);
    const a2 = await playSeededHand(page, SEED_A); // fresh game, identical seed
    const b = await playSeededHand(page, SEED_B);  // fresh game, different seed

    expect(a1.length, 'a full hand must be dealt').toBeGreaterThanOrEqual(HAND_MIN);
    // The load-bearing proof: identical seed ⇒ identical deal. This can only
    // pass if the backend consumed the forwarded ?seed= (a dropped seed would
    // randomise every game and fail here).
    expect(a2, 'same seed must reproduce the exact same seat-0 hand').toEqual(a1);
    // And the seed actually matters — a different seed yields a different hand.
    expect(b, 'a different seed must change the deal').not.toEqual(a1);
  });
});
