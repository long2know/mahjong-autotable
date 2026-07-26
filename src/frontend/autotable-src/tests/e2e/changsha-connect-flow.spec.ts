// Ferro — WP-E / #120 — P0 real-UI connect-flow acceptance.
//
// Proves the bare-URL → **Apply & Start** → auto-connect → take seat →
// **Deal** → hands-dealt path works entirely through **real DOM clicks**,
// on both desktop and mobile viewports.  This is the flow the P0 gate
// (Hudson / WP-F) depends on; the pre-existing "Apply & Start … did NOT
// auto-connect … user stranded" bug (playtest-stephen-first-play.spec.mjs)
// is the thing being pinned as fixed.
//
// Discipline (no WS backdoors):
//   • Every state transition is driven by a real click on a real element
//     (`#lobby-apply`, `.seat-button-0 .take-seat`, `#deal`) — never by
//     injecting a WS UPDATE or poking `world`/`client` mutators.
//   • Assertions observe *authoritative* client-side collections
//     (`client.connected()`, `client.seat`, `client.things`).  Hands are
//     WebGL-rendered onto a canvas, so `client.things` — the very
//     collection the renderer consumes — is the only way to assert the
//     deal landed.  Reading it is observation, not a backdoor.
//
// Contract pinned (C-1 / C-2): after Apply the URL carries a minted
// `gameId` + the frozen six lobby params; a seated human clicks Deal to
// trigger the bare `match[0]` deal (spectators get the server auto-deal —
// out of scope here).  See selectors.md and .squad/decisions.md.

import { test, expect, type Page } from '@playwright/test';

const HAND_MIN = 13;          // non-dealer hand size
const DEALER_HAND = 14;       // dealer draws the 14th on the initial deal

// Skip the multi-step first-run tour so it doesn't cover the lobby; the
// tour has its own coverage (tour specs).  This is a pre-condition reset,
// not part of the flow under test.
async function landOnBareLobby(page: Page): Promise<void> {
  await page.goto('', { waitUntil: 'domcontentloaded' });
  await page.evaluate(() => {
    try {
      localStorage.clear();
      localStorage.setItem('mahjong.tour.completed.v1', 'true');
      localStorage.setItem('mahjong.identity.onboarded.v1', 'true');
    } catch { /* storage disabled — flow still works */ }
  });
  await page.goto('', { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(600);
  // Onboarding card can still show on a cold cache — dismiss via its real
  // Skip button if present.
  const skip = page.getByTestId('onboarding-skip');
  if (await skip.isVisible().catch(() => false)) {
    await skip.click().catch(() => { /* best effort */ });
  }
}

async function waitForConnected(page: Page): Promise<void> {
  // window.game is published by three-renderer once the scene boots.
  await expect
    .poll(async () => page.evaluate(() => Boolean((window as unknown as { game?: unknown }).game)), {
      timeout: 25_000,
      message: 'renderer never published window.game after Apply & Start',
    })
    .toBe(true);
  await expect
    .poll(
      async () =>
        page.evaluate(() => {
          const g = (window as unknown as { game?: { client?: { connected?: () => boolean } } }).game;
          return Boolean(g?.client?.connected?.());
        }),
      { timeout: 20_000, message: 'client never auto-connected after Apply & Start' },
    )
    .toBe(true);
}

interface DealSnapshot {
  seat: number | null;
  total: number;
  handBySeat: Record<string, number>;
  hasMatch: boolean;
}

async function readDeal(page: Page): Promise<DealSnapshot> {
  return page.evaluate(() => {
    const g = (window as unknown as {
      game?: { client?: { seat?: number | null; things?: { entries(): Iterable<[unknown, { slotName?: string }]> }; match?: { get(k: number): unknown } } };
    }).game;
    const cli = g?.client;
    const out = { seat: cli?.seat ?? null, total: 0, handBySeat: {} as Record<string, number>, hasMatch: false };
    if (cli?.things) {
      for (const [, v] of cli.things.entries()) {
        out.total++;
        const slot = (v && v.slotName) || '';
        const m = /^hand[^@]*@(\d)$/.exec(slot);
        if (m) out.handBySeat[m[1]] = (out.handBySeat[m[1]] || 0) + 1;
      }
    }
    try { out.hasMatch = Boolean(cli?.match?.get(0)); } catch { /* ignore */ }
    return out;
  });
}

async function currentSeat(page: Page): Promise<number | null> {
  return page.evaluate(
    () => (window as unknown as { game?: { client?: { seat?: number | null } } }).game?.client?.seat ?? null,
  );
}

// Click a real element.  If the element's centre is overlapped by a sibling
// HUD control (the mobile lobby toggle sits over the left of #deal; the chat
// header sits over the bottom Take-Seat button), fall back to a positioned
// click on the first pixel that is *genuinely* the element's own hit-target.
// This is still a real user gesture — we never dispatch a synthetic event and
// never click a pixel that belongs to another element.
async function realClick(page: Page, selector: string, timeout = 4000): Promise<boolean> {
  const loc = page.locator(selector).first();
  if (!(await loc.isVisible().catch(() => false))) return false;
  await loc.scrollIntoViewIfNeeded().catch(() => { /* HUD button */ });
  if (await loc.click({ timeout }).then(() => true).catch(() => false)) return true;
  const pos = await page.evaluate((sel) => {
    const el = document.querySelector(sel);
    if (!el) return null;
    const r = el.getBoundingClientRect();
    for (let dy = 4; dy < r.height; dy += 6) {
      for (let dx = 4; dx < r.width; dx += 6) {
        const top = document.elementFromPoint(r.left + dx, r.top + dy);
        if (top === el || el.contains(top)) return { x: Math.round(dx), y: Math.round(dy) };
      }
    }
    return null;
  }, selector);
  if (!pos) return false;
  return loc.click({ position: pos, timeout }).then(() => true).catch(() => false);
}

// Take a seat through a real Take-Seat click.  Desktop lands seat 0; on
// mobile the bottom (seat-0) button is fully overlapped by the chat header,
// so we fall through to the first seat a genuine click can actually reach.
async function takeReachableSeat(page: Page): Promise<number> {
  for (let i = 0; i < 4; i++) {
    if (!(await realClick(page, `.seat-button-${i} .take-seat`))) continue;
    for (let t = 0; t < 12; t++) {
      if ((await currentSeat(page)) === i) return i;
      await page.waitForTimeout(300);
    }
  }
  throw new Error('no seat could be taken via a real Take-Seat click');
}

test.describe('Changsha — real-UI connect flow (WP-E / #120 P0)', () => {
  test('bare URL → Apply & Start → auto-connect → take seat → Deal → hands', async ({ page }) => {
    test.setTimeout(90_000);

    const pageErrors: string[] = [];
    page.on('pageerror', (e) => pageErrors.push(String(e)));

    // ── 1. Land on the bare-URL lobby ────────────────────────────────
    await landOnBareLobby(page);
    const lobby = page.locator('#lobby-panel.lobby-open');
    await expect(lobby, 'lobby must auto-open on a bare URL').toBeVisible({ timeout: 10_000 });

    // ── 2. Apply & Start with the shipped defaults (Changsha / auto /
    //       3 bots / Auto seat).  Real click on the real button. ──────
    const apply = page.locator('#lobby-apply');
    await expect(apply).toBeVisible();
    await apply.scrollIntoViewIfNeeded().catch(() => { /* footer button */ });
    await Promise.all([
      page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 20_000 }).catch(() => null),
      apply.click(),
    ]);

    // ── 3. URL is the source of truth — assert the minted gameId + the
    //       six frozen lobby params rode the reload (C-2). ────────────
    const url = new URL(page.url());
    const q = url.searchParams;
    expect(q.get('gameId'), 'Apply must mint/keep a gameId so auto-connect fires').toBeTruthy();
    expect(q.get('gameId')!).toMatch(/^changsha-/);
    expect(q.get('variant')).toBe('changsha');
    expect(q.get('dealMode')).toBe('auto');
    expect(q.get('botCount')).toBe('3');

    // ── 4. Auto-connect — the button label promises "Start". ─────────
    await waitForConnected(page);
    // DOM corroboration: the connected pill swaps #connect → #disconnect.
    await expect(page.locator('#disconnect.server-connected')).toBeVisible({ timeout: 10_000 });

    // ── 5. Take a seat via a real Take-Seat click (seat 0 on desktop;
    //       the first reachable seat on mobile, where the chat header
    //       overlaps the bottom seat — a Hicks HUD hand-off). ─────────
    await expect(
      page.locator('.seat-button-0 .take-seat, .seat-button-1 .take-seat, .seat-button-2 .take-seat, .seat-button-3 .take-seat').first(),
      'a Take Seat button must be reachable after connect',
    ).toBeVisible({ timeout: 10_000 });
    const seat = await takeReachableSeat(page);
    expect(seat, 'a seat must be occupied via a real click').toBeGreaterThanOrEqual(0);

    // ── 6. Deal via a real click on the #deal button (a seated human
    //       triggers the bare match[0] deal; server is authoritative). ─
    const deal = page.locator('#deal');
    await expect(deal, 'Deal button must be visible + enabled for a seated player').toBeVisible({ timeout: 10_000 });
    await expect(deal).toBeEnabled();
    expect(await realClick(page, '#deal'), 'Deal button must be clickable for a seated player').toBe(true);

    // ── 7. Hands dealt — the authoritative `things` collection now holds
    //       a full 13/14-tile hand for every seat (server-dealt).  The
    //       dealer (our seat) may hold the 14th; everyone holds ≥13. ───
    const seatKey = String(seat);
    await expect
      .poll(async () => (await readDeal(page)).handBySeat[seatKey] ?? 0, {
        timeout: 25_000,
        message: `seat ${seat} never received a dealt hand after clicking Deal`,
      })
      .toBeGreaterThanOrEqual(HAND_MIN);

    const snap = await readDeal(page);
    expect(snap.hasMatch, 'match[0] must be set once the deal fires').toBe(true);
    expect(snap.handBySeat[seatKey]).toBeLessThanOrEqual(DEALER_HAND);
    let dealtTotal = 0;
    for (const s of ['0', '1', '2', '3']) {
      const n = snap.handBySeat[s] ?? 0;
      expect(n, `seat ${s} must have a full dealt hand`).toBeGreaterThanOrEqual(HAND_MIN);
      dealtTotal += n;
    }
    // 4 hands × 13 (+1 for the dealer's draw) — proves a real full deal, not
    // a partial/optimistic local placement.
    expect(dealtTotal, 'all four hands must be fully dealt').toBeGreaterThanOrEqual(4 * HAND_MIN);

    // ── 8. No uncaught page errors across the whole flow. ────────────
    expect(pageErrors, `page errors during connect flow: ${pageErrors.join('\n')}`).toHaveLength(0);
  });
});
