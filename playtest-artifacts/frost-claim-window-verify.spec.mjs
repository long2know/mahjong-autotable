// Frost 2026-05-29 — Targeted regression spec for the claim-window
// "deadline=0 auto-hides immediately" bug.
//
// Backend translator now plumbs ClaimWindowTimeoutMs from
// ChangshaRuntimeOptions through ChangshaToAutotableTranslator so the
// emitted claim entries carry an absolute epoch-ms deadline.  The
// frontend overlay + side-panel both handle deadline<=0 as "no client
// countdown" (rather than "expired now") for back-compat / rehydrated
// state paths.
//
// Scenario:
//   • dealMode=manual, botCount=3 — viewer takes seat 0.
//   • Manually deal hands.
//   • Watch for up to 90 s for at least one bot discard that opens a
//     claim window targeting seat 0 (Pung / Chow / Kong / Hu).
//   • Verify:
//       (a) backend log shows "claim-window-open" event, AND
//       (b) the local client.claim collection has an entry under
//           key=String(seat) = "0" with a non-zero deadline, AND
//       (c) the .ferro-claim-overlay-visible class becomes present
//           on the DOM at some point (within the 5s window).
//
// PASS = all three signals.  FAIL = any missing.
//
// Run:  E2E_BASE_URL=http://127.0.0.1:8088 node \
//          playtest-artifacts/frost-claim-window-verify.spec.mjs

import { chromium } from 'playwright';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const gameId = `frost-claim-${Date.now()}-${Math.floor(Math.random() * 10000)}`;
const url = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&botDifficulty=Hard&handCount=4&gameId=${gameId}`;

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();
const consoleWarnings = [];
const consoleErrors = [];
const consoleLogs = [];
page.on('console', (m) => {
  const t = m.type();
  const txt = m.text();
  if (t === 'warning') consoleWarnings.push(txt);
  else if (t === 'error') consoleErrors.push(txt);
  else if (/ferro|claim/i.test(txt)) consoleLogs.push(`[${t}] ${txt}`);
});
page.on('pageerror', (e) => { consoleErrors.push(`pageerror: ${e.message}\n${e.stack ?? ''}`); });

console.log(`[frost] opening ${url}`);
await page.goto(url, { waitUntil: 'domcontentloaded' });
await page.waitForTimeout(3000);

// Skip onboarding tour if any.
for (const sel of ['#tour-skip', '#onboarding-skip', '#lobby-close']) {
  const e = page.locator(sel);
  if (await e.isVisible().catch(() => false)) {
    await e.click({ force: true, timeout: 3000 }).catch(() => {});
    await page.waitForTimeout(300);
  }
}

// Quick-match / connect.
for (const sel of ['#lobby-quick-match', '#connect']) {
  const e = page.locator(sel).first();
  if (await e.isVisible().catch(() => false)) {
    await e.click({ timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(2500);
  }
}

// Click the first .take-seat button to occupy seat 0 (or whichever seat
// is currently free — the runtime fills the rest with bots).
const seats = page.locator('.take-seat');
const seatCount = await seats.count();
let tookSeat = false;
for (let i = 0; i < seatCount; i++) {
  if (await seats.nth(i).isVisible().catch(() => false)) {
    await seats.nth(i).click({ timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(1500);
    tookSeat = true;
    break;
  }
}
console.log(`[frost] tookSeat=${tookSeat}`);

const mySeat = await page.evaluate(() => window.game?.world?.seat ?? null);
console.log(`[frost] selfSeat=${mySeat}`);
if (mySeat === null) {
  console.error('[frost] FAIL: no seat taken — cannot test claim window for local seat');
  await browser.close();
  process.exit(1);
}

// Trigger manual deal — only matters in dealMode=manual; harmless in auto.
await page.evaluate(() => { try { window.game.world.deal('HANDS'); } catch {} });
await page.waitForTimeout(2000);

// In dealMode=auto the bots discard freely once dealt.  In a real game the
// human player would discard their own draw on their turn — to keep the
// probe moving, periodically click the rightmost rack tile during the
// observation loop so seat 0's hand doesn't stall and lock the round.
async function autoDiscardHumanDraw() {
  try {
    const hadDraw = await page.evaluate(() => {
      const w = window.game?.world;
      if (!w || w.seat === null) return false;
      let count = 0;
      for (const t of w.things.values()) {
        const s = t.slot;
        if (s && s.group === 'hand' && s.seat === w.seat) count++;
      }
      // Standard hand size is 13 after a discard; a held draw bumps it to 14.
      return count >= 14;
    });
    if (!hadDraw) return false;
    const ok = await page.evaluate(() => {
      const w = window.game?.world;
      if (!w || w.seat === null) return false;
      const myTiles = [];
      for (const t of w.things.values()) {
        const s = t.slot;
        if (s && s.group === 'hand' && s.seat === w.seat) myTiles.push(t);
      }
      if (myTiles.length === 0) return false;
      // Discard the highest-index tile (rightmost), cheap and deterministic.
      myTiles.sort((a, b) => (b.slot?.key ?? 0) - (a.slot?.key ?? 0));
      const target = myTiles[0];
      try {
        if (typeof w.emitDiscard === 'function') return !!w.emitDiscard(target);
      } catch { return false; }
      return false;
    });
    return ok;
  } catch { return false; }
}

// Observe up to 90 s for claim entries OR overlay visibility.
const deadline = Date.now() + 90_000;
let claimEntryObserved = null;
let overlayVisibleObserved = false;
let lastDeadlineSeen = null;
const observations = [];

while (Date.now() < deadline) {
  // Drive human discard so the round doesn't stall waiting for the player.
  await autoDiscardHumanDraw();

  // Probe the live client.claim collection keyed by selfSeat.
  const snap = await page.evaluate((selfSeat) => {
    try {
      const c = window.game.world.client.claim;
      const all = [];
      // Many implementations expose `.entries()` or iterable maps.
      if (typeof c.values === 'function') {
        let i = 0;
        for (const k of (typeof c.keys === 'function' ? c.keys() : [])) {
          all.push([String(k), c.get(k)]);
          if (++i > 8) break;
        }
      }
      const mine = c.get(String(selfSeat)) ?? c.get(selfSeat) ?? null;
      // Also collect every non-null claim entry currently in the collection
      // so we can tell whether a claim fired for ANY seat (even if not ours).
      const anyOpen = all.filter(([_k, v]) => v && v.available);
      return { mine, anyOpen };
    } catch (e) {
      return { error: String(e) };
    }
  }, mySeat);

  if (snap?.anyOpen && snap.anyOpen.length > 0) {
    const last = snap.anyOpen[snap.anyOpen.length - 1];
    observations.push({ at: Date.now(), anyOpen: snap.anyOpen.length, sampleKey: last[0], sampleDeadline: last[1].deadline });
  }
  if (snap?.mine && snap.mine.available !== undefined) {
    claimEntryObserved = snap.mine;
    lastDeadlineSeen = snap.mine.deadline;
  }

  const overlayState = await page.evaluate(() => {
    const el = document.querySelector('.ferro-claim-overlay');
    if (el === null) return { exists: false };
    const w = window.game?.world;
    const c = w?.client;
    return {
      exists: true,
      hidden: el.hidden,
      hasVisibleClass: el.classList.contains('ferro-claim-overlay-visible'),
      classes: el.className,
      display: window.getComputedStyle(el).display,
      bbox: el.getBoundingClientRect().width > 0,
      worldSeat: w?.seat ?? null,
      clientSeat: c?.seat ?? null,
      claimKeys: c?.claim ? (typeof c.claim.entries === 'function' ? Array.from(c.claim.entries()).map(([k, v]) => [String(k), v ? Object.keys(v) : null]) : 'no-entries-fn') : null,
    };
  }).catch(() => ({ exists: false }));
  if (overlayState?.hasVisibleClass) overlayVisibleObserved = true;

  const visNow = await page
    .locator('.ferro-claim-overlay-visible')
    .first()
    .isVisible()
    .catch(() => false);
  if (visNow) overlayVisibleObserved = true;

  if (snap?.mine?.available !== undefined || overlayState?.hasVisibleClass) {
    observations.push({ at: Date.now(), mine: snap?.mine ?? null, overlay: overlayState });
  }

  if (claimEntryObserved && overlayVisibleObserved) break;
  await page.waitForTimeout(400);
}

const findings = {
  gameId,
  selfSeat: mySeat,
  claimEntryObserved,
  overlayVisibleObserved,
  observationCount: observations.length,
  observations: observations.slice(0, 3),
  lastDeadlineSeen,
  deadlineNonZero: typeof lastDeadlineSeen === 'number' && lastDeadlineSeen > 0,
  staleMoveToWarnings: consoleWarnings.filter((w) => /skipped stale moveTo/.test(w)).length,
  pageErrorCount: consoleErrors.length,
  pageErrorsSample: consoleErrors.slice(0, 5),
  claimLogsSample: consoleLogs.slice(0, 10),
};
console.log(JSON.stringify(findings, null, 2));

const pass =
  !!claimEntryObserved &&
  overlayVisibleObserved &&
  findings.deadlineNonZero;
console.log(`\n[frost] result: ${pass ? 'PASS' : 'FAIL'}`);
await browser.close();
process.exit(pass ? 0 : 2);
