// Hicks 2026-05-27 — Stephen's "face-down walls + pickup choreography"
// directive verification.
//
// Loads the canonical manual-mode URL and asserts (via window.game.world):
//   • >= 100 tiles in wall slots (whole deck minus a few in transit)
//   • 0 tiles in any non-self seat's hand slot are face-up
//     (foreign-hand `face !== null` would mean leaking the tile id)
//   • Every wall tile renders at the wall slot's face-down rotation
//     index (slot.rotations[0] is FACE_DOWN per setup-slots.ts)
//   • Polling observation: >= 4 tiles transit wall → dealer hand
//     within ~3s of the pickup chain starting
//
// Run: backend on 127.0.0.1:8088, then
//   cd playtest-artifacts && node playtest-walls-facedown.spec.mjs
//
// Screenshots: playtest-artifacts/walls-facedown/
//   01-lobby.png            initial lobby render
//   02-connected-walls.png  T+2s post-connect, walls should be face-down
//   03-mid-pickup.png       during the take chain
//   04-post-deal.png        final state
import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';

const ARTIFACT_DIR = path.resolve('./playtest-artifacts/walls-facedown');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const findings = {
  url: '',
  steps: [],
  pageErrors: [],
  consoleErrors: [],
  consoleWarnings: [],
  networkFailures: [],
  assertions: {},
};

const browser = await chromium.launch();
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();

page.on('console', msg => {
  const t = msg.type();
  const text = msg.text();
  if (t === 'error') findings.consoleErrors.push(text);
  if (t === 'warning') findings.consoleWarnings.push(text);
});
page.on('pageerror', err => findings.pageErrors.push(err.message));
page.on('response', resp => {
  if (resp.status() >= 400) {
    findings.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
  }
});

await page.addInitScript(() => {
  const inject = () => {
    if (document.getElementById('walls-overlay-defang')) return;
    const style = document.createElement('style');
    style.id = 'walls-overlay-defang';
    style.textContent = `
      #tour-overlay, #magic-link-landing, #magic-link-overlay,
      #signin-modal-backdrop, .magic-link-landing, .magic-link-overlay,
      .signin-modal-backdrop, [data-testid="tour-overlay"], [data-testid="signin-modal-backdrop"]
        { display: none !important; pointer-events: none !important; visibility: hidden !important; }
      [aria-hidden="true"] { pointer-events: none !important; }
    `;
    document.head.appendChild(style);
  };
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', inject);
  } else { inject(); }
});

async function snap(name) {
  await page.screenshot({ path: path.join(ARTIFACT_DIR, name), fullPage: true });
}

async function step(name, fn) {
  console.log(`\n=== ${name} ===`);
  try {
    const result = await fn();
    findings.steps.push({ name, ok: true, result });
    console.log(`OK ${name}`, result ? JSON.stringify(result) : '');
    return result;
  } catch (err) {
    const msg = err && err.message || String(err);
    findings.steps.push({ name, ok: false, error: msg });
    console.log(`FAIL ${name}: ${msg}`);
  }
}

// Stephen's canonical URL — manual deal, 3 hard bots, 4-hand match.
const uniqueGameId = `hicks-walls-${Date.now()}-${Math.floor(Math.random() * 1000)}`;
const fullUrl = `${baseUrl}/autotable/?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Hard&handCount=4&gameId=${uniqueGameId}`;

await step('1-load-lobby', async () => {
  await page.goto(fullUrl, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(2500);
  findings.url = page.url();
  await snap('01-lobby.png');
  return { url: findings.url };
});

await step('2-dismiss-tour', async () => {
  const tour = page.locator('#tour-skip');
  if (await tour.isVisible().catch(() => false)) {
    await tour.click({ force: true, timeout: 3000 });
    await page.waitForTimeout(400);
  }
  const onb = page.locator('#onboarding-skip');
  if (await onb.isVisible().catch(() => false)) {
    await onb.click({ force: true, timeout: 3000 });
    await page.waitForTimeout(400);
  }
});

await step('3-quick-match-and-connect', async () => {
  // Pre-fill the gameId so QM uses ours.
  const gameIdInput = page.locator('#game-id, [data-testid="game-id"]').first();
  if (await gameIdInput.isVisible().catch(() => false)) {
    await gameIdInput.fill(uniqueGameId);
    await page.waitForTimeout(200);
  }
  const qm = page.locator('#lobby-quick-match');
  if (await qm.first().isVisible().catch(() => false)) {
    await qm.first().click({ timeout: 5000 });
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(3000);
  }
  // Close lobby panel if still open.
  const closeBtn = page.locator('#lobby-close');
  if (await closeBtn.isVisible().catch(() => false)) {
    await closeBtn.click({ force: true });
    await page.waitForTimeout(400);
  }
  // Click Connect if not auto-connected.
  const connect = page.locator('#connect');
  if (await connect.first().isVisible().catch(() => false)) {
    await connect.first().click({ timeout: 5000 });
    await page.waitForTimeout(2500);
  }
  // Take seat 0 if not already seated.
  const seats = page.locator('.take-seat');
  const total = await seats.count();
  for (let i = 0; i < total; i++) {
    if (await seats.nth(i).isVisible().catch(() => false)) {
      await seats.nth(i).click({ timeout: 5000 });
      await page.waitForTimeout(1500);
      break;
    }
  }
});

// 3b) Kick off the deal — equivalent to the dealer long-pressing the
//     #deal button.  `world.deal('HANDS')` is what the button's onSuccess
//     callback invokes after the 600 ms hold; calling it directly avoids
//     the headless-mouse timing flake (see playtest-human-led.spec.mjs:187).
await step('3b-trigger-deal', async () => {
  // Allow the backend's RollingDice snapshot to land first so the chain
  // doesn't race ApplyDealModeAsync.
  await page.waitForTimeout(1500);
  return await page.evaluate(() => {
    const g = (window).game;
    if (!g || !g.world) return { ok: false, reason: 'no window.game.world' };
    try {
      const conditions = g.world.conditions;
      const seat = g.world.seat;
      g.world.deal('HANDS');
      return { ok: true, seat, gameType: conditions?.gameType };
    } catch (e) { return { ok: false, reason: String(e) }; }
  });
});

// 4) Wait for the world to be populated, then poke the wall state.
const wallSnapshot = await step('4-assert-walls-face-down', async () => {
  // Allow ~2s for WS to push the RollingDice snapshot.
  await page.waitForTimeout(2000);
  await snap('02-connected-walls.png');

  // Read the world via the globally exposed `window.game` debug hook
  // (three-renderer.ts publishes this for E2E specs).
  const summary = await page.evaluate(() => {
    const w = (window).game?.world;
    if (!w) return { error: 'window.game.world not present' };
    const things = w.things;
    const slots = w.slots;
    const seat = w.seat;
    let wallCount = 0;
    let foreignHandFaceUp = 0;
    let wallBackRotationCount = 0;
    let wallFrontRotationCount = 0;
    let localSeatHandFaceUp = 0;
    let localSeatHandTotal = 0;
    const wallSlotRotationsLen = new Set();
    const wallSeats = new Set();
    const wallSlotKeys = new Set();
    for (const thing of things.values()) {
      const slot = thing.slot;
      if (!slot) continue;
      if (slot.group === 'wall' || slot.name.startsWith('wall.')) {
        wallCount++;
        wallSlotRotationsLen.add(slot.rotations.length);
        if (slot.seat !== null && slot.seat !== undefined) wallSeats.add(slot.seat);
        wallSlotKeys.add(slot.name);
        // Wall slot rotations are authored as [FACE_DOWN, FACE_UP].
        // After per-seat rotation those quaternions are conjugated, so
        // the per-slot canonical "back-up" index stays at 0.
        if (thing.rotationIndex === 0) wallBackRotationCount++;
        else wallFrontRotationCount++;
      }
      if (slot.group === 'hand' && slot.seat !== null && slot.seat !== seat) {
        // rotationIndex 2 = FACE_DOWN per hand slot rotations.
        if (thing.rotationIndex !== 2) foreignHandFaceUp++;
      }
      // Hicks 2026-05-28 — Local seat must see its OWN hand face-up.
      // Hand rotations are [STANDING, FACE_UP, FACE_DOWN] → index 1 = FACE_UP.
      if (slot.group === 'hand' && slot.seat !== null && slot.seat === seat) {
        localSeatHandTotal++;
        if (thing.rotationIndex === 1) localSeatHandFaceUp++;
      }
    }
    return {
      seat,
      wallCount,
      wallBackRotationCount,
      wallFrontRotationCount,
      foreignHandFaceUp,
      localSeatHandFaceUp,
      localSeatHandTotal,
      wallSlotRotationsLen: [...wallSlotRotationsLen],
      wallSeats: [...wallSeats].sort(),
      wallSlotKeyCount: wallSlotKeys.size,
    };
  });

  findings.assertions.summary = summary;
  return summary;
});

// 5) Mid-pickup observation — poll the dealer's hand growth as the
//    chain runs.
await step('5-pickup-choreography', async () => {
  const observations = [];
  // Sample every ~500 ms for ~6 s — captures the 4 take rounds (3 × 4 +
  // 1) and the bot rounds in between.
  for (let i = 0; i < 12; i++) {
    const obs = await page.evaluate(() => {
      const w = (window).game?.world;
      if (!w) return null;
      const seat = w.seat;
      let dealerHand = 0;
      let allHand = 0;
      let wallCount = 0;
      for (const thing of w.things.values()) {
        if (thing.slot.group === 'hand') {
          allHand++;
          if (thing.slot.seat === seat) dealerHand++;
        }
        if (thing.slot.group === 'wall') wallCount++;
      }
      return { dealerHand, allHand, wallCount, seat };
    });
    observations.push({ atMs: i * 500, ...obs });
    if (i === 4) await snap('03-mid-pickup.png');
    await page.waitForTimeout(500);
  }
  findings.assertions.pickupProgression = observations;

  // Pickup choreography passes if we saw >= 4 tiles flow into the
  // dealer's hand at SOME point in the observation window.
  const maxDealer = Math.max(...observations.map(o => o.dealerHand ?? 0));
  return { observations, maxDealerHand: maxDealer };
});

await step('6-post-deal', async () => {
  await page.waitForTimeout(2000);
  await snap('04-post-deal.png');

  // Hicks 2026-05-28 — Local-seat face-up probe. Final post-deal snapshot
  // must show the dealer (local seat) with their own concealed hand
  // rendered face-up. rotationIndex=1 = FACE_UP for hand slots.
  const postDeal = await page.evaluate(() => {
    const w = (window).game?.world;
    if (!w) return null;
    const seat = w.seat;
    let localSeatHandFaceUp = 0;
    let localSeatHandTotal = 0;
    let localSeatHandFaceCount = 0;
    const localSeatRotIdx = [];
    for (const thing of w.things.values()) {
      const slot = thing.slot;
      if (!slot) continue;
      if (slot.group === 'hand' && slot.seat === seat) {
        localSeatHandTotal++;
        localSeatRotIdx.push(thing.rotationIndex);
        if (thing.rotationIndex === 1) localSeatHandFaceUp++;
        if (typeof thing.typeIndex === 'number') localSeatHandFaceCount++;
      }
    }
    return { seat, localSeatHandFaceUp, localSeatHandTotal, localSeatHandFaceCount, localSeatRotIdx };
  });
  findings.assertions.postDeal = postDeal;
  return postDeal;
});

await browser.close();

// Evaluate pass/fail.
const a = findings.assertions.summary ?? {};
const checks = {
  // Vasquez 2026-06-04 — Threshold lowered from Riichi (136-tile deck →
  // ≥100 post-deal) to Changsha (108-tile deck → ≥80 post-deal). The
  // 4-seat 14/14/13/13 wall split lands at 108 − (14 + 13×3) = 55 once
  // initial hands deal, and animation snapshots may run a few extra
  // ticks behind. 80 gives the same "wall still mostly intact" semantics
  // the original assertion meant for Riichi.
  wallCountAtLeast80: (a.wallCount ?? 0) >= 80,
  zeroForeignHandFaceUp: (a.foreignHandFaceUp ?? 1) === 0,
  allWallBackRotation: (a.wallFrontRotationCount ?? 1) === 0 && (a.wallBackRotationCount ?? 0) > 0,
  fourSeatWalls: (a.wallSeats ?? []).length === 4,
};
const maxDealer = Math.max(
  ...(findings.assertions.pickupProgression ?? []).map(o => o.dealerHand ?? 0),
);
checks.pickupReachedDealerHand = maxDealer >= 4;

// Hicks 2026-05-28 — Local-seat face-up gate. After deal completes the
// dealer must have at least 13 of their own concealed hand tiles
// rendered face-up (rotationIndex === 1). 14 is the dealer's full count
// after East's initial deal+draw; the gate tolerates >= 13 to allow for
// in-transit batching at the snapshot moment.
const post = findings.assertions.postDeal ?? {};
checks.localSeatHandFaceUp = (post.localSeatHandFaceUp ?? 0) >= 13;

findings.assertions.checks = checks;
findings.assertions.pageErrorsCount = findings.pageErrors.length;

fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'), JSON.stringify(findings, null, 2));
console.log('\n=== FINAL findings ===');
console.log(JSON.stringify({
  url: findings.url,
  summary: a,
  checks,
  pageErrorsCount: findings.pageErrors.length,
  consoleErrorsCount: findings.consoleErrors.length,
  networkFailuresCount: findings.networkFailures.length,
}, null, 2));

if (findings.pageErrors.length) {
  console.log('\nPAGE ERRORS:');
  for (const e of findings.pageErrors) console.log(' -', e);
}

const failed = Object.entries(checks).filter(([, ok]) => !ok);
if (failed.length > 0 || findings.pageErrors.length > 0) {
  console.log('\nFAILED CHECKS:', failed.map(([k]) => k).join(', '));
  process.exit(1);
}
console.log('\nALL CHECKS PASSED');
