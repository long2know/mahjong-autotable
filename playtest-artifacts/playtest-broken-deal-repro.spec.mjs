// Vasquez 2026-05-30 — Stephen's "is the game working? the dealing
// seems very whacky" repro.
//
// Stephen's screenshot (https://localhost:7135, variant=changsha,
// dealMode=auto, botCount=3, botDifficulty=Hard, handCount=4) shows:
//   1. Walls render as flat single-row strips instead of stacked
//      2-high bricks.
//   2. Only ONE tile face-up in front of seat 0 (a single 6筒) —
//      dealer should have 13.
//   3. Gray triangular corner artifacts with stray white tile strips.
//   4. A tiny black "Bot 1/2/3 + Seat 0" label box floating in the
//      middle of the table.
//   5. Move log says "Match started — dealer is Seat 0" — backend
//      thinks the deal succeeded, but the client visualises garbage.
//
// This spec is DIAGNOSTIC ONLY. It does not assert pass/fail; it
// reproduces the load + auto-deal flow, snapshots the full page, and
// dumps `window.game.world.{things,slots,match}` to JSON so Hicks and
// Frost have hard evidence to debug from.
//
// Run:
//   cd /data/source/mahjong-autotable
//   E2E_BASE_URL=https://127.0.0.1:7135 NODE_TLS_REJECT_UNAUTHORIZED=0 \
//     node playtest-artifacts/playtest-broken-deal-repro.spec.mjs
//
// Defaults to https://127.0.0.1:7135 (the ASP.NET dev launch
// profile HTTPS port).  Older specs assumed http://127.0.0.1:8088
// but that profile is no longer in use as of 2026-06-01.
//
// Outputs (timestamped so reruns don't clobber):
//   playtest-artifacts/screenshots/broken-deal-repro-<ts>.png
//   playtest-artifacts/screenshots/broken-deal-repro-<ts>.json
import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';

const baseUrl = process.env.E2E_BASE_URL || 'https://127.0.0.1:7135';
const ARTIFACT_DIR = path.resolve('./playtest-artifacts/screenshots');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });
const ts = new Date().toISOString().replace(/[:.]/g, '-');
const screenshotPath = path.join(ARTIFACT_DIR, `broken-deal-repro-${ts}.png`);
const jsonPath       = path.join(ARTIFACT_DIR, `broken-deal-repro-${ts}.json`);

const findings = {
  ts,
  baseUrl,
  url: '',
  steps: [],
  pageErrors: [],
  consoleErrors: [],
  consoleWarnings: [],
  networkFailures: [],
  diagnostics: {},
};

const browser = await chromium.launch();
const ctx = await browser.newContext({
  viewport: { width: 1280, height: 800 },
  // Backend in dev runs on https://127.0.0.1:7135 with a self-signed
  // ASP.NET dev cert; without this, page.goto throws ERR_CERT_AUTHORITY_INVALID.
  ignoreHTTPSErrors: true,
});
const page = await ctx.newPage();

page.on('console', msg => {
  const t = msg.type();
  const text = msg.text();
  if (t === 'error')   findings.consoleErrors.push(text);
  if (t === 'warning') findings.consoleWarnings.push(text);
});
page.on('pageerror',  err  => findings.pageErrors.push(err.message));
page.on('response',  resp => {
  if (resp.status() >= 400) {
    findings.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
  }
});

// Defang the tour / sign-in overlays so they don't intercept clicks
// or hide canvas content from full-page screenshots.
await page.addInitScript(() => {
  const inject = () => {
    if (document.getElementById('broken-deal-defang')) return;
    const style = document.createElement('style');
    style.id = 'broken-deal-defang';
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

async function step(name, fn) {
  console.log(`\n=== ${name} ===`);
  try {
    const result = await fn();
    findings.steps.push({ name, ok: true, result });
    if (result !== undefined) console.log(`OK ${name}`, JSON.stringify(result));
    else console.log(`OK ${name}`);
    return result;
  } catch (err) {
    const msg = err && err.message || String(err);
    findings.steps.push({ name, ok: false, error: msg });
    console.log(`FAIL ${name}: ${msg}`);
  }
}

const uniqueGameId = `repro-${Date.now()}`;
const fullUrl =
  `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3` +
  `&botDifficulty=Hard&handCount=4&gameId=${uniqueGameId}`;

await step('1-load', async () => {
  await page.goto(fullUrl, { waitUntil: 'domcontentloaded' });
  findings.url = page.url();
  await page.waitForTimeout(2500);
  return { url: findings.url };
});

// Dismiss tour / onboarding banners if rendered.
await step('2-dismiss-tour', async () => {
  for (const sel of ['#tour-skip', '#onboarding-skip']) {
    const el = page.locator(sel);
    if (await el.isVisible().catch(() => false)) {
      await el.click({ force: true, timeout: 3000 });
      await page.waitForTimeout(300);
    }
  }
});

// Quick-match → seat → connect. The lobby auto-routes a single
// quick-match in auto-deal mode, but we still need to take seat 0.
await step('3-quick-match-and-seat', async () => {
  const gameIdInput = page.locator('#game-id, [data-testid="game-id"]').first();
  if (await gameIdInput.isVisible().catch(() => false)) {
    await gameIdInput.fill(uniqueGameId);
    await page.waitForTimeout(150);
  }
  const qm = page.locator('#lobby-quick-match').first();
  if (await qm.isVisible().catch(() => false)) {
    await qm.click({ timeout: 5000 });
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2500);
  }
  const closeBtn = page.locator('#lobby-close');
  if (await closeBtn.isVisible().catch(() => false)) {
    await closeBtn.click({ force: true });
    await page.waitForTimeout(300);
  }
  const connect = page.locator('#connect').first();
  if (await connect.isVisible().catch(() => false)) {
    await connect.click({ timeout: 5000 });
    await page.waitForTimeout(2000);
  }
  const seats = page.locator('.take-seat');
  const total = await seats.count();
  for (let i = 0; i < total; i++) {
    if (await seats.nth(i).isVisible().catch(() => false)) {
      await seats.nth(i).click({ timeout: 5000 });
      await page.waitForTimeout(1200);
      break;
    }
  }
});

// Auto-deal is supposed to kick off automatically once the seat
// fills with bots. Belt-and-suspenders: explicitly invoke
// `world.deal('HANDS')` so the diagnostic isn't gated on the
// auto-trigger landing.
await step('4-force-deal', async () => {
  await page.waitForTimeout(1500);
  return await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.world) return { ok: false, reason: 'no window.game.world' };
    try {
      g.world.deal('HANDS');
      return { ok: true, seat: g.world.seat };
    } catch (e) { return { ok: false, reason: String(e) }; }
  });
});

// Give the deal ceremony ~8s to settle (matches Hicks' walls-facedown
// spec timing). Then dump everything.
await step('5-wait-for-deal-settle', async () => {
  await page.waitForTimeout(8000);
});

await step('6-dump-world-state', async () => {
  const state = await page.evaluate(() => {
    const w = window.game?.world;
    if (!w) return { error: 'window.game.world not present' };

    // world.things is a Map<id, Thing> — Array.from(map.values()).
    const things = Array.from((w.things && typeof w.things.values === 'function')
      ? w.things.values()
      : []);

    // Tile orientation in this engine is encoded by `rotationIndex`
    // into the owning slot's `rotations[]` quaternion list.
    // For hand slots: rotations = [STANDING, FACE_UP, FACE_DOWN].
    // For wall slots: rotations = [FACE_DOWN, FACE_UP].
    // We classify face-up/face-down per-slot-group so the buckets
    // are meaningful regardless of slot type.
    const classify = (t) => {
      const slot = t.slot;
      if (!slot) return 'unslotted';
      const group = slot.group;
      const idx = t.rotationIndex;
      if (group === 'hand')    return idx === 1 ? 'face-up' : (idx === 2 ? 'face-down' : 'standing');
      if (group === 'wall')    return idx === 0 ? 'face-down' : 'face-up';
      if (group === 'discard') return 'face-up'; // discards always face-up
      if (group === 'meld')    return idx === 1 ? 'face-up' : 'face-down';
      return `idx-${idx}`;
    };

    const faceUpCount   = things.filter(t => classify(t) === 'face-up').length;
    const faceDownCount = things.filter(t => classify(t) === 'face-down').length;

    // world.slots is a Map<string, Slot> (world.ts:29), so Object.keys
    // would return []. Pull keys/entries via Map API.
    const slotsAsMap = (w.slots && typeof w.slots.keys === 'function');
    const slotKeys = slotsAsMap
      ? Array.from(w.slots.keys())
      : Object.keys(w.slots || {});
    const wallSlotKeys    = slotKeys.filter(k => k.startsWith('wall.'));
    const discardSlotKeys = slotKeys.filter(k => k.startsWith('discard'));
    const handSlotKeys    = slotKeys.filter(k => k.startsWith('hand.') || k.includes('hand@'));
    const meldSlotKeys    = slotKeys.filter(k => k.startsWith('meld'));

    // Per-seat hand inventory — the screenshot says seat 0 has 1
    // tile, so we want the EXACT count by seat.
    const slotsBySeat = [0, 1, 2, 3].map(s => {
      const sHandKeys = slotKeys.filter(k =>
        (k.startsWith('hand.') && k.endsWith(`@${s}`)) ||
        k.includes(`hand@${s}`));
      return {
        seat: s,
        handSlotCount: sHandKeys.length,
        tilesInHand: things.filter(t => {
          const n = t.slot?.name;
          if (!n) return false;
          if (t.slot.group !== 'hand') return false;
          return t.slot.seat === s;
        }).length,
        tilesFaceUp: things.filter(t =>
          t.slot?.group === 'hand' && t.slot.seat === s && classify(t) === 'face-up'
        ).length,
        tilesFaceDown: things.filter(t =>
          t.slot?.group === 'hand' && t.slot.seat === s && classify(t) === 'face-down'
        ).length,
        sampleHandSlotNames: sHandKeys.slice(0, 4),
      };
    });

    // Sample tiles: 6 in hand, 6 in walls, 3 in discard.
    // Thing.place() (thing.ts:41) is a METHOD that returns
    // {position: Vector3, rotation: Euler} — direct `.position` is
    // not set on Thing.  Walk through place() so we capture the
    // actual rendered (x,y,z); `y` encodes wall stacking height.
    const sample = (arr, n) => arr.slice(0, n).map(t => {
      let pl = null;
      try {
        const p = typeof t.place === 'function' ? t.place() : null;
        if (p && p.position) {
          pl = {
            x: +p.position.x?.toFixed(2),
            y: +p.position.y?.toFixed(2),
            z: +p.position.z?.toFixed(2),
          };
        }
      } catch { /* swallow */ }
      let slotPos = null;
      try {
        const sp = t.slot?.position;
        if (sp) slotPos = { x: +sp.x?.toFixed(2), y: +sp.y?.toFixed(2), z: +sp.z?.toFixed(2) };
      } catch { /* swallow */ }
      return {
        id: t.index,
        typeIndex: t.typeIndex,
        slotName: t.slot?.name,
        slotGroup: t.slot?.group,
        slotSeat: t.slot?.seat,
        rotationIndex: t.rotationIndex,
        classify: classify(t),
        place: pl,
        slotPos,
      };
    });
    const handThings    = things.filter(t => t.slot?.group === 'hand');
    const wallThings    = things.filter(t => t.slot?.group === 'wall');
    const discardThings = things.filter(t => t.slot?.group === 'discard');

    // Wall slots have a `.place` enumeration but only via Slot fields.
    // Slot.position is a Vector3; capture y to spot flat (single
    // layer y) vs stacked (alternating layer heights) walls.
    const wallSlotShape = wallSlotKeys.slice(0, 16).map(k => {
      const sl = slotsAsMap ? w.slots.get(k) : w.slots[k];
      let pos = null;
      try {
        if (sl?.position) pos = { x: +sl.position.x?.toFixed(2), y: +sl.position.y?.toFixed(2), z: +sl.position.z?.toFixed(2) };
      } catch { /* swallow */ }
      return {
        name: k,
        seat: sl?.seat,
        group: sl?.group,
        position: pos,
        rotationsLen: Array.isArray(sl?.rotations) ? sl.rotations.length : null,
      };
    });

    // Wall-y distribution across ALL wall slots — flat walls have a
    // single Y value, stacked walls have two (one per layer).
    const wallYBuckets = {};
    for (const k of wallSlotKeys) {
      const sl = slotsAsMap ? w.slots.get(k) : w.slots[k];
      const y = sl?.position?.y;
      if (y === undefined || y === null) continue;
      const key = y.toFixed(3);
      wallYBuckets[key] = (wallYBuckets[key] || 0) + 1;
    }

    return {
      seat: w.seat,
      phase: (w.match && (w.match.phase ?? w.match.state)) ?? null,
      gameType: w.conditions?.gameType,
      thingCount: things.length,
      faceUpCount,
      faceDownCount,
      unclassified: things.length - faceUpCount - faceDownCount,
      slotsBySeat,
      slotCountTotal: slotKeys.length,
      wallSlots: wallSlotKeys.length,
      discardSlots: discardSlotKeys.length,
      handSlots: handSlotKeys.length,
      meldSlots: meldSlotKeys.length,
      tilesInWall: wallThings.length,
      tilesInDiscard: discardThings.length,
      tilesInHandTotal: handThings.length,
      sampleHandTiles: sample(handThings, 6),
      sampleWallTiles: sample(wallThings, 6),
      sampleDiscardTiles: sample(discardThings, 3),
      wallSlotShape,
      wallYBuckets,
      // Generic 6-tile slice as the prompt requested.
      sampleThings: sample(things, 6),
    };
  });

  findings.diagnostics = state;
  console.log(JSON.stringify(state, null, 2));
  return { thingCount: state.thingCount, wallSlots: state.wallSlots };
});

await step('7-screenshot-full-page', async () => {
  await page.screenshot({ path: screenshotPath, fullPage: true });
  return { screenshotPath };
});

// Pull a few visible-DOM signals matching Stephen's bullets:
//   - any small label box dead-centre with "Bot 1/2/3" text?
//   - move log content
await step('8-dom-text-signals', async () => {
  const signals = await page.evaluate(() => {
    const out = { moveLog: [], floatingLabels: [], canvasRect: null };
    const log = document.querySelector('#log, .log, [data-testid="move-log"]');
    if (log) out.moveLog = (log.innerText || '').split('\n').slice(-12);
    const cnv = document.querySelector('canvas');
    if (cnv) {
      const r = cnv.getBoundingClientRect();
      out.canvasRect = { x: r.x, y: r.y, w: r.width, h: r.height };
    }
    // Hunt for any element containing "Bot 1" / "Bot 2" / "Bot 3" /
    // "Seat 0" together with bounding box approx centred over the canvas.
    const all = Array.from(document.querySelectorAll('div, span, td, p'));
    const cx = (out.canvasRect?.x ?? 0) + (out.canvasRect?.w ?? 0) / 2;
    const cy = (out.canvasRect?.y ?? 0) + (out.canvasRect?.h ?? 0) / 2;
    for (const el of all) {
      const txt = (el.innerText || '').trim();
      if (!txt) continue;
      if (!(txt.includes('Bot 1') && txt.includes('Bot 2'))) continue;
      const r = el.getBoundingClientRect();
      if (r.width === 0 || r.height === 0) continue;
      const elCx = r.x + r.width / 2;
      const elCy = r.y + r.height / 2;
      const distFromCanvasCentre = Math.hypot(elCx - cx, elCy - cy);
      if (distFromCanvasCentre < 220) {
        out.floatingLabels.push({
          tag: el.tagName.toLowerCase(),
          id: el.id || null,
          cls: el.className || null,
          text: txt.slice(0, 200),
          rect: { x: Math.round(r.x), y: Math.round(r.y), w: Math.round(r.width), h: Math.round(r.height) },
          distFromCanvasCentre: Math.round(distFromCanvasCentre),
        });
      }
    }
    return out;
  });
  findings.diagnostics.domSignals = signals;
  return { centredLabelCount: signals.floatingLabels.length };
});

await browser.close();

// Append run summary + write findings JSON.
findings.summary = {
  thingCount:        findings.diagnostics.thingCount,
  faceUpCount:       findings.diagnostics.faceUpCount,
  faceDownCount:     findings.diagnostics.faceDownCount,
  wallSlots:         findings.diagnostics.wallSlots,
  discardSlots:      findings.diagnostics.discardSlots,
  handSlots:         findings.diagnostics.handSlots,
  tilesInWall:       findings.diagnostics.tilesInWall,
  tilesInDiscard:    findings.diagnostics.tilesInDiscard,
  tilesInHandTotal:  findings.diagnostics.tilesInHandTotal,
  wallYBuckets:      findings.diagnostics.wallYBuckets,
  slotsBySeat:       findings.diagnostics.slotsBySeat,
  phase:             findings.diagnostics.phase,
  pageErrorsCount:    findings.pageErrors.length,
  consoleErrorsCount: findings.consoleErrors.length,
  networkFailuresCount: findings.networkFailures.length,
};

fs.writeFileSync(jsonPath, JSON.stringify(findings, null, 2));

console.log('\n=== SUMMARY ===');
console.log(JSON.stringify(findings.summary, null, 2));
console.log('\nscreenshot:', screenshotPath);
console.log('state dump:', jsonPath);

if (findings.pageErrors.length) {
  console.log('\nPAGE ERRORS:');
  for (const e of findings.pageErrors.slice(0, 10)) console.log(' -', e);
}
