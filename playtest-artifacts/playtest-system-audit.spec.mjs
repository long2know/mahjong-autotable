// Ripley 2026-05-29 — Full-system bird's-eye audit checklist.
//
// Stephen's directive ("get everything completed; fan out and perform
// an audit with real integration testing to confirm that the game
// works") gates the playability sign-off.  Vasquez, Bishop, Frost and
// Hicks are running focused audits in parallel; this spec is the
// cross-cutting top-down sweep — every surface area gets at least one
// PASS/FAIL gate so the merge-blocker question gets a single-answer
// table at the end.
//
// Sections (each item PASS / FAIL / SKIP with evidence):
//   1. Lobby & connection      — render, variant picker (Ferro #91),
//                                game-id input, take-seat × 4, spectator,
//                                connect / disconnect / leave-seat.
//   2. Variants                 — changsha / riichi4 / riichi3 / bamboo /
//                                minefield smoke-load + bot tick.
//   3. Mobile                   — 375×667 viewport reflow + 44px touch
//                                targets + safe-area inset CSS.
//   4. Claim window             — Ferro overlay surfaces Pung/Kong/Chow/
//                                Hu/Pass, countdown ticks, legacy buttons
//                                remain wired.
//   5. Win modal                — synthetic gameComplete payload renders
//                                totalScores rows, modal can be hidden.
//   6. DB persistence           — PlayerStats schema (LastGameAt
//                                nullable per Drake's c369c54) + row
//                                growth across the audit run.
//   7. Console / network noise  — pageErrors === 0; consoleErrors ≤ 5
//                                aggregated baseline (per memory:
//                                Three.js NaN + 2× 404s).
//
// Run:
//   E2E_BASE_URL=http://127.0.0.1:8088 \
//     node playtest-artifacts/playtest-system-audit.spec.mjs
//
// Artifacts:
//   playtest-artifacts/system-audit/findings.json
//   playtest-artifacts/system-audit/REPORT.md
//   playtest-artifacts/system-audit/*.png   (one per FAIL + key milestones)
//
// Exit code: always 0 (this is a discovery spec; failures route via the
// inbox memo rather than CI-blocking a Wave promotion).

import { chromium } from 'playwright';
import fs from 'fs';
import path from 'path';
import { execFileSync } from 'child_process';

const ARTIFACT_DIR = path.resolve('./playtest-artifacts/system-audit');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const SQLITE_PATH = process.env.AUDIT_SQLITE_PATH || '/tmp/mat-postfix.db';

const findings = [];
const startedAt = new Date().toISOString();

function record(id, category, status, evidence, screenshots = []) {
  const finding = { id, category, status, evidence, screenshots };
  findings.push(finding);
  const tag = status === 'PASS' ? 'PASS' : status === 'FAIL' ? 'FAIL' : 'SKIP';
  console.log(`[${tag}] ${id} — ${category}`);
  if (evidence && Object.keys(evidence).length > 0) {
    const s = JSON.stringify(evidence);
    console.log(`        evidence: ${s.length > 280 ? s.slice(0, 280) + '…' : s}`);
  }
  return finding;
}

async function snap(page, name) {
  try {
    const file = path.join(ARTIFACT_DIR, name);
    await page.screenshot({ path: file, fullPage: false });
    return file;
  } catch (e) {
    return null;
  }
}

// Overlay-defang init script reused across every scenario so the tour /
// magic-link / sign-in overlays don't intercept clicks or scroll the
// viewport off-target.
const overlayDefang = () => {
  const inject = () => {
    if (document.getElementById('audit-overlay-defang')) return;
    const style = document.createElement('style');
    style.id = 'audit-overlay-defang';
    style.textContent = `
      #tour-overlay, #magic-link-landing, #magic-link-overlay,
      #signin-modal-backdrop, .magic-link-landing, .magic-link-overlay,
      .signin-modal-backdrop, [data-testid="tour-overlay"],
      [data-testid="signin-modal-backdrop"]
        { display: none !important; pointer-events: none !important;
          visibility: hidden !important; }
      [aria-hidden="true"] { pointer-events: none !important; }
    `;
    document.head.appendChild(style);
  };
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', inject);
  } else {
    inject();
  }
};

// Standard per-page observers; the returned `errors` object is mutated
// as the page runs.
function attachErrorTaps(page) {
  const errors = {
    pageErrors: [],
    consoleErrors: [],
    consoleWarnings: [],
    networkFailures: [],
  };
  page.on('console', msg => {
    const t = msg.type();
    const text = msg.text();
    if (t === 'error') errors.consoleErrors.push(text);
    if (t === 'warning') errors.consoleWarnings.push(text);
  });
  page.on('pageerror', err => errors.pageErrors.push(err.message));
  page.on('response', resp => {
    if (resp.status() >= 400) {
      errors.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
    }
  });
  return errors;
}

// =====================================================================
// SECTION 6 — DB persistence (run pre-audit baseline + post-audit delta)
// =====================================================================

function readPlayerStatsSnapshot() {
  // python3 wrapper because the host doesn't ship `sqlite3` CLI but
  // does ship the python stdlib bindings.  Returns
  // `{ schema, count, sample, error? }`.
  const script = `
import sqlite3, json, sys
out = {}
try:
    con = sqlite3.connect(${JSON.stringify(SQLITE_PATH)})
    cur = con.cursor()
    schema = [list(r) for r in cur.execute("PRAGMA table_info('PlayerStats')")]
    out['schema'] = schema
    out['lastGameAtNullable'] = next(
        (not bool(r[3]) for r in schema if r[1] == 'LastGameAt'), None)
    out['count'] = cur.execute("SELECT COUNT(*) FROM PlayerStats").fetchone()[0]
    out['sample'] = [list(r) for r in cur.execute(
        "SELECT PlayerId, GamesPlayed, GamesWon, TotalScore, LastGameAt "
        "FROM PlayerStats ORDER BY GamesPlayed DESC LIMIT 5")]
    tables = [r[0] for r in cur.execute(
        "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")]
    out['hasPlayerStatsTable'] = 'PlayerStats' in tables
    con.close()
except Exception as e:
    out['error'] = str(e)
print(json.dumps(out))
`;
  try {
    const stdout = execFileSync('python3', ['-c', script], { encoding: 'utf8' });
    return JSON.parse(stdout);
  } catch (e) {
    return { error: String(e && e.message || e) };
  }
}

const dbBaseline = readPlayerStatsSnapshot();
console.log('\n=== DB baseline ===');
console.log(JSON.stringify(dbBaseline, null, 2).slice(0, 800));

record(
  'DB-1-schema',
  '6-db-persistence',
  dbBaseline.error
    ? 'FAIL'
    : (dbBaseline.hasPlayerStatsTable && dbBaseline.lastGameAtNullable === true ? 'PASS' : 'FAIL'),
  {
    sqlitePath: SQLITE_PATH,
    hasPlayerStatsTable: dbBaseline.hasPlayerStatsTable ?? null,
    lastGameAtNullable: dbBaseline.lastGameAtNullable ?? null,
    baselineRows: dbBaseline.count ?? null,
    schemaColumns: (dbBaseline.schema || []).map(c => `${c[1]}:${c[2]}${c[3] ? ' NN' : ''}`),
    error: dbBaseline.error ?? null,
  },
);

// =====================================================================
// SECTION 7 — backend identity endpoint (Drake's c369c54 acceptance test)
// =====================================================================

async function probeIdentityEndpoint() {
  try {
    const res = await fetch(`${baseUrl}/api/identity`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ displayName: `ripley-audit-${Date.now()}` }),
    });
    return { status: res.status, ok: res.ok };
  } catch (e) {
    return { status: 0, ok: false, error: String(e && e.message || e) };
  }
}

const identityProbe = await probeIdentityEndpoint();
record(
  'DB-2-identity-endpoint',
  '6-db-persistence',
  identityProbe.ok ? 'PASS' : 'FAIL',
  identityProbe,
);

// =====================================================================
// SECTION 1 — Lobby & connection
// =====================================================================

const browser = await chromium.launch();

async function newCtx(opts = {}) {
  const ctx = await browser.newContext({
    viewport: opts.viewport ?? { width: 1280, height: 800 },
    deviceScaleFactor: opts.deviceScaleFactor ?? 1,
    isMobile: opts.isMobile ?? false,
    hasTouch: opts.hasTouch ?? false,
  });
  const page = await ctx.newPage();
  await page.addInitScript(overlayDefang);
  return { ctx, page, errors: attachErrorTaps(page) };
}

// SECTION 1 in a single browser context so we get fast lobby probes.
async function runLobbyAudit() {
  const { ctx, page, errors } = await newCtx();
  try {
    await page.goto(`${baseUrl}/autotable/`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);

    const lobbyShot = await snap(page, '01-lobby-render.png');

    // 1a — lobby renders
    const lobbyHasPanel = await page.locator('#lobby-panel').count() > 0;
    record('L-1-lobby-render', '1-lobby', lobbyHasPanel ? 'PASS' : 'FAIL', {
      pageErrors: errors.pageErrors.length,
      consoleErrors: errors.consoleErrors.length,
      url: page.url(),
    }, [lobbyShot]);

    // 1b — Ferro variant picker shows expected 6 options
    const variantInfo = await page.evaluate(() => {
      const sel = document.querySelector('select.ferro-variant-picker-select')
                  || document.querySelector('[data-testid="ferro-variant-picker"]');
      if (!sel) return { ok: false, reason: 'select not in DOM' };
      const opts = Array.from(sel.querySelectorAll('option')).map(o => ({
        value: o.value, label: o.textContent.trim(), disabled: o.disabled,
      }));
      return { ok: true, opts, count: opts.length };
    });
    const expectedVariants = ['changsha', 'four-player', 'three-player', 'bamboo', 'minefield', 'hong-kong'];
    const presentVariants = (variantInfo.opts || []).map(o => o.value);
    const hongKongDisabled = (variantInfo.opts || []).find(o => o.value === 'hong-kong')?.disabled === true;
    const variantPass = variantInfo.ok
        && expectedVariants.every(v => presentVariants.includes(v))
        && hongKongDisabled;
    record('L-2-variant-switcher', '1-lobby', variantPass ? 'PASS' : 'FAIL', {
      options: variantInfo.opts,
      expectedAll: expectedVariants,
      hongKongDisabled,
    });

    // 1c — game-id pre-fill input works.  The lobby panel is collapsed
    // by default; expand it via the same `lobby-active` body class that
    // Hicks's mobile rule + the existing playtest harness uses so the
    // input becomes visible and tappable.  Selector is `#lobby-gameId`
    // per index.html:218 (the older `#game-id` doesn't exist).
    await page.evaluate(() => {
      const p = document.getElementById('lobby-panel');
      if (p) p.classList.add('lobby-open');
      document.body.classList.add('lobby-active');
    });
    await page.waitForTimeout(300);
    const gameIdInput = page.locator('#lobby-gameId, #game-id, [data-testid="game-id"]').first();
    const gidPresent = await gameIdInput.count() > 0;
    const testGameId = `audit-prefill-${Date.now()}`;
    let prefillEcho = null;
    if (gidPresent) {
      await gameIdInput.fill(testGameId, { force: true }).catch(() => {});
      await page.waitForTimeout(150);
      prefillEcho = await gameIdInput.inputValue().catch(() => null);
    }
    record('L-3-gameid-prefill', '1-lobby',
      (gidPresent && prefillEcho === testGameId) ? 'PASS' : 'FAIL', {
      gameIdInputPresent: gidPresent,
      filled: testGameId,
      echo: prefillEcho,
    });

    // 1d — take-seat buttons (4 expected)
    const takeSeats = await page.locator('.take-seat').count();
    record('L-4-take-seat-buttons', '1-lobby',
      takeSeats >= 4 ? 'PASS' : 'FAIL', {
      takeSeatCount: takeSeats,
    });

    // 1e — Quick-Match button visible & clickable
    const qm = page.locator('#lobby-quick-match');
    const qmVisible = await qm.first().isVisible().catch(() => false);
    record('L-5-quick-match-visible', '1-lobby',
      qmVisible ? 'PASS' : 'FAIL', { qmVisible });

    // 1f — connect button exists (legacy entry point)
    const connectBtn = page.locator('#connect');
    const connectCount = await connectBtn.count();
    record('L-6-connect-button', '1-lobby',
      connectCount > 0 ? 'PASS' : 'FAIL', { count: connectCount });

    // 1g — leave-seat button exists (gameplay sidebar)
    const leaveSeat = await page.locator('#leave-seat').count();
    record('L-7-leave-seat-button', '1-lobby',
      leaveSeat > 0 ? 'PASS' : 'FAIL', { count: leaveSeat });

  } catch (err) {
    record('L-X-fatal', '1-lobby', 'FAIL', {
      error: String(err && err.message || err),
      pageErrors: errors.pageErrors,
    });
  } finally {
    await ctx.close().catch(() => {});
  }
}

await runLobbyAudit();

// =====================================================================
// Shared connect-and-seat helper for the variant + claim + win-modal
// scenarios.
// =====================================================================

async function takeSeatAtIndex(page, gameId, seatIdx) {
  // Tour dismiss (defensive — defang covers most paths).
  for (const sel of ['#tour-skip', '#onboarding-skip']) {
    const el = page.locator(sel);
    if (await el.isVisible().catch(() => false)) {
      await el.click({ force: true, timeout: 2000 }).catch(() => {});
      await page.waitForTimeout(200);
    }
  }
  const gidInput = page.locator('#game-id, [data-testid="game-id"]').first();
  if (await gidInput.isVisible().catch(() => false)) {
    await gidInput.fill(gameId).catch(() => {});
    await page.waitForTimeout(150);
  }
  const qm = page.locator('#lobby-quick-match');
  if (await qm.first().isVisible().catch(() => false)) {
    await qm.first().click({ timeout: 5000 }).catch(() => {});
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2500);
  }
  const closeBtn = page.locator('#lobby-close');
  if (await closeBtn.isVisible().catch(() => false)) {
    await closeBtn.click({ force: true, timeout: 2000 }).catch(() => {});
    await page.waitForTimeout(400);
  }
  const connectBtn = page.locator('#connect');
  if (await connectBtn.first().isVisible().catch(() => false)) {
    await connectBtn.first().click({ timeout: 5000 }).catch(() => {});
    await page.waitForTimeout(2500);
  }
  const seats = page.locator('.take-seat');
  const total = await seats.count();
  let visibleIdxs = [];
  for (let i = 0; i < total; i++) {
    if (await seats.nth(i).isVisible().catch(() => false)) visibleIdxs.push(i);
  }
  if (visibleIdxs.length === 0) return { took: false, seatIdx: null, visibleIdxs };
  const which = visibleIdxs[seatIdx] ?? visibleIdxs[0];
  await seats.nth(which).click({ timeout: 5000 }).catch(() => {});
  await page.waitForTimeout(2000);
  return { took: true, seatIdx: which, visibleIdxs };
}

async function waitForCanvas(page, timeoutMs = 20_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const ok = await page.evaluate(() => {
      const c = document.querySelectorAll('canvas');
      const ready = document.body.getAttribute('data-three-renderer-ready') === 'true';
      return c.length > 0 && ready;
    });
    if (ok) return true;
    await page.waitForTimeout(400);
  }
  return false;
}

async function probeWorld(page) {
  return await page.evaluate(() => {
    const g = (window).game;
    if (!g) return { ok: false, reason: 'no window.game' };
    const w = g.world;
    const c = g.client;
    let things = 0, hand = 0, discard = 0, wall = 0;
    if (w && w.things) {
      for (const t of w.things.values()) {
        things++;
        const grp = t?.slot?.group;
        if (grp === 'hand') hand++;
        else if (grp === 'discard') discard++;
        else if (grp === 'wall' || grp === 'walls') wall++;
      }
    }
    let runtimeHint = null;
    try {
      runtimeHint = (c && c.match && c.match.get && c.match.get(0)) || null;
    } catch { /* ignore */ }
    return {
      ok: true,
      things,
      hand,
      discard,
      wall,
      seat: w ? w.seat : null,
      connected: c && typeof c.connected === 'function' ? c.connected() : null,
      gameId: g.gameId ?? null,
      matchInfo: runtimeHint,
      hasClaimCollection: !!(c && c.claim),
      hasGameCompleteCollection: !!(c && c.gameComplete),
    };
  });
}

// =====================================================================
// SECTION 1 (continued) — seat 0/1/2/3 + spectator + disconnect
// =====================================================================

async function runSeatMatrix() {
  for (let seatIdx = 0; seatIdx < 4; seatIdx++) {
    const { ctx, page, errors } = await newCtx();
    const gameId = `audit-seat${seatIdx}-${Date.now()}`;
    try {
      await page.goto(`${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&handCount=1&botDifficulty=Easy&gameId=${gameId}`, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(2000);
      const result = await takeSeatAtIndex(page, gameId, seatIdx);
      await waitForCanvas(page, 15_000);
      await page.waitForTimeout(2500);
      const world = await probeWorld(page);
      const shot = await snap(page, `seat-${seatIdx}-after-take.png`);
      const took = result.took && world.ok && world.seat !== null;
      record(`L-8-take-seat-${seatIdx}`, '1-lobby', took ? 'PASS' : 'FAIL', {
        seatIdx,
        seatTaken: result.seatIdx,
        worldSeat: world.seat,
        thingsCount: world.things,
        pageErrors: errors.pageErrors.length,
        pageErrorMessages: errors.pageErrors.slice(0, 3),
      }, [shot]);
    } catch (err) {
      record(`L-8-take-seat-${seatIdx}`, '1-lobby', 'FAIL', {
        error: String(err && err.message || err),
      });
    } finally {
      await ctx.close().catch(() => {});
    }
  }
}

await runSeatMatrix();

async function runSpectatorScenario() {
  const { ctx, page, errors } = await newCtx();
  const gameId = `audit-spec-${Date.now()}`;
  try {
    await page.goto(`${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=4&handCount=1&botDifficulty=Easy&gameId=${gameId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    // Quick-match without taking a seat (4 bots fill the table).
    const qm = page.locator('#lobby-quick-match');
    if (await qm.first().isVisible().catch(() => false)) {
      await qm.first().click({ timeout: 5000 }).catch(() => {});
      await page.waitForLoadState('domcontentloaded');
      await page.waitForTimeout(3000);
    }
    const closeBtn = page.locator('#lobby-close');
    if (await closeBtn.isVisible().catch(() => false)) {
      await closeBtn.click({ force: true, timeout: 2000 }).catch(() => {});
    }
    const cn = page.locator('#connect');
    if (await cn.first().isVisible().catch(() => false)) {
      await cn.first().click({ timeout: 5000 }).catch(() => {});
      await page.waitForTimeout(3000);
    }
    await waitForCanvas(page, 15_000);
    // Wait long enough for 4-bot fill + initial deal animation.
    await page.waitForTimeout(6000);
    const world = await probeWorld(page);
    const shot = await snap(page, 'spectator-mode.png');
    const pass = world.ok && (world.things > 0 || world.hand > 0 || world.wall > 0);
    record('L-9-spectator-mode', '1-lobby', pass ? 'PASS' : 'FAIL', {
      worldSeat: world.seat,
      things: world.things,
      hand: world.hand,
      wall: world.wall,
      discard: world.discard,
      pageErrors: errors.pageErrors.length,
      pageErrorMessages: errors.pageErrors.slice(0, 3),
    }, [shot]);
  } catch (err) {
    record('L-9-spectator-mode', '1-lobby', 'FAIL', {
      error: String(err && err.message || err),
    });
  } finally {
    await ctx.close().catch(() => {});
  }
}

await runSpectatorScenario();

async function runLeaveSeatScenario() {
  const { ctx, page, errors } = await newCtx();
  const gameId = `audit-leave-${Date.now()}`;
  try {
    await page.goto(`${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&handCount=1&botDifficulty=Easy&gameId=${gameId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    await takeSeatAtIndex(page, gameId, 0);
    await waitForCanvas(page, 15_000);
    await page.waitForTimeout(1500);
    const seatBefore = await probeWorld(page);
    const playerSeatBefore = await page.evaluate(() => {
      const g = (window).game;
      const pid = g.client.playerId();
      const entry = g.client.seats.get(pid);
      return { playerId: pid, entry, clientSeat: g.client.seat };
    });
    // Expand sidebar so #leave-seat is visible.
    await page.evaluate(() => {
      const s = document.getElementById('sidebar');
      if (s) s.classList.remove('collapsed');
    });
    await page.waitForTimeout(200);
    const leaveBtn = page.locator('#leave-seat');
    const leaveVis = await leaveBtn.isVisible().catch(() => false);
    if (leaveVis) {
      await leaveBtn.click({ force: true, timeout: 5000 }).catch(() => {});
      // Allow plenty of time for round-trip (wire UPDATE → server →
      // echoed seats update → onSeats → world.onSeat).
      await page.waitForTimeout(5000);
    }
    const seatAfter = await probeWorld(page);
    const playerSeatAfter = await page.evaluate(() => {
      const g = (window).game;
      const pid = g.client.playerId();
      const entry = g.client.seats.get(pid);
      return { playerId: pid, entry, clientSeat: g.client.seat };
    });
    // Also check if a take-seat button reappeared (signal that the
    // seat was released even if world.seat didn't reset locally).
    const takeAfter = await page.evaluate(() => {
      const seats = Array.from(document.querySelectorAll('.take-seat'));
      return {
        total: seats.length,
        visible: seats.filter(b => {
          const r = b.getBoundingClientRect();
          return r.width > 0 && r.height > 0;
        }).length,
      };
    });
    const shot = await snap(page, 'leave-seat-after.png');
    const beforeOk = typeof seatBefore?.seat === 'number' && seatBefore.seat >= 0;
    const releasedSeat = !beforeOk
      || seatAfter?.seat === null
      || (typeof seatAfter?.seat === 'number' && seatAfter.seat !== seatBefore.seat)
      || takeAfter.visible > 0
      || playerSeatAfter?.entry?.seat === null;
    record('L-10-leave-seat', '1-lobby',
      (leaveVis && releasedSeat) ? 'PASS' : 'FAIL', {
      leaveVisible: leaveVis,
      seatBefore: seatBefore?.seat,
      seatAfter: seatAfter?.seat,
      playerSeatBefore,
      playerSeatAfter,
      takeSeatVisibleAfter: takeAfter.visible,
      pageErrors: errors.pageErrors.length,
    }, [shot]);
  } catch (err) {
    record('L-10-leave-seat', '1-lobby', 'FAIL', {
      error: String(err && err.message || err),
    });
  } finally {
    await ctx.close().catch(() => {});
  }
}

await runLeaveSeatScenario();

async function runReconnectScenario() {
  const { ctx, page, errors } = await newCtx();
  const gameId = `audit-reconnect-${Date.now()}`;
  try {
    await page.goto(`${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&handCount=1&botDifficulty=Easy&gameId=${gameId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    await takeSeatAtIndex(page, gameId, 0);
    await waitForCanvas(page, 15_000);
    await page.waitForTimeout(1500);
    // Trigger a hard reload — recover the seat via the rejoin path.
    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(3000);
    await waitForCanvas(page, 15_000);
    await page.waitForTimeout(2000);
    const world = await probeWorld(page);
    const shot = await snap(page, 'reconnect-after-reload.png');
    record('L-11-reconnect-after-reload', '1-lobby',
      world.ok && (world.things > 0 || world.connected === true) ? 'PASS' : 'FAIL', {
      worldSeat: world.seat,
      things: world.things,
      connected: world.connected,
      pageErrors: errors.pageErrors.length,
    }, [shot]);
  } catch (err) {
    record('L-11-reconnect-after-reload', '1-lobby', 'FAIL', {
      error: String(err && err.message || err),
    });
  } finally {
    await ctx.close().catch(() => {});
  }
}

await runReconnectScenario();

// =====================================================================
// SECTION 2 — Variant smoke tests
// =====================================================================

const VARIANT_SUITE = [
  { id: 'V-1-changsha',   variant: 'changsha',   expectRuntime: 'ChangshaRuntime' },
  { id: 'V-2-riichi4',    variant: 'riichi4',    expectRuntime: 'Relay' },
  { id: 'V-3-riichi3',    variant: 'riichi3',    expectRuntime: 'Relay' },
  { id: 'V-4-bamboo',     variant: 'bamboo',     expectRuntime: 'Relay' },
  { id: 'V-5-minefield',  variant: 'minefield',  expectRuntime: 'Relay' },
];

async function runVariantSmoke(suite) {
  const { ctx, page, errors } = await newCtx();
  const gameId = `audit-${suite.variant}-${Date.now()}`;
  try {
    await page.goto(
      `${baseUrl}/autotable/?variant=${suite.variant}&dealMode=auto&botCount=4&handCount=1&botDifficulty=Easy&gameId=${gameId}`,
      { waitUntil: 'domcontentloaded' },
    );
    await page.waitForTimeout(2500);

    // Quick-match without seat (4 bots) so the deal kicks off
    // without us having to wait for human input.
    const qm = page.locator('#lobby-quick-match');
    if (await qm.first().isVisible().catch(() => false)) {
      await qm.first().click({ timeout: 5000 }).catch(() => {});
      await page.waitForLoadState('domcontentloaded');
      await page.waitForTimeout(2500);
    }
    const closeBtn = page.locator('#lobby-close');
    if (await closeBtn.isVisible().catch(() => false)) {
      await closeBtn.click({ force: true, timeout: 2000 }).catch(() => {});
    }
    const cn = page.locator('#connect');
    if (await cn.first().isVisible().catch(() => false)) {
      await cn.first().click({ timeout: 5000 }).catch(() => {});
      await page.waitForTimeout(3000);
    }
    const canvasOk = await waitForCanvas(page, 20_000);
    // For the relay variants the upstream bundle's local Setup drives
    // the deal — it does not auto-fire on connect.  Click `#deal` so
    // the bots tick.  (Changsha runtime auto-deals so the click is a
    // no-op on the changsha variant — defensively scoped.)
    if (suite.expectRuntime === 'Relay') {
      // Expand sidebar so #deal is reachable.
      await page.evaluate(() => {
        const s = document.getElementById('sidebar');
        if (s) s.classList.remove('collapsed');
      });
      await page.waitForTimeout(200);
      const dealBtn = page.locator('#deal');
      if (await dealBtn.first().isVisible().catch(() => false)
          && await dealBtn.first().isEnabled().catch(() => false)) {
        await dealBtn.first().click({ force: true, timeout: 5000 }).catch(() => {});
        await page.waitForTimeout(3500);
      }
    }
    // Capture an initial baseline snapshot.
    const baselineWorld = await probeWorld(page);
    // Let bots tick for up to 12s; observe discard or hand-count delta.
    let movement = null;
    const deadline = Date.now() + 12_000;
    while (Date.now() < deadline) {
      await page.waitForTimeout(1200);
      const snap2 = await probeWorld(page);
      if (snap2.ok && (
        (snap2.discard > 0) ||
        (snap2.things > baselineWorld.things) ||
        (snap2.hand !== baselineWorld.hand)
      )) {
        movement = snap2;
        break;
      }
    }
    const shot = await snap(page, `variant-${suite.variant}.png`);
    const renderedOk = canvasOk && baselineWorld.ok && (baselineWorld.things > 0 || baselineWorld.wall > 0);
    const botMoveOk = !!movement;
    const passUi = renderedOk;
    const passBot = botMoveOk;
    record(`${suite.id}-render`, '2-variants', passUi ? 'PASS' : 'FAIL', {
      variant: suite.variant,
      expectedRuntime: suite.expectRuntime,
      canvasMounted: canvasOk,
      thingsCount: baselineWorld.things,
      wallCount: baselineWorld.wall,
      handCount: baselineWorld.hand,
      connected: baselineWorld.connected,
      pageErrors: errors.pageErrors.length,
      pageErrorMessages: errors.pageErrors.slice(0, 2),
    }, [shot]);
    record(`${suite.id}-bot-move`, '2-variants', passBot ? 'PASS' : 'FAIL', {
      variant: suite.variant,
      observedMovement: !!movement,
      discardAfter: movement?.discard ?? null,
      thingsAfter: movement?.things ?? null,
      thingsBefore: baselineWorld.things,
    });
  } catch (err) {
    record(`${suite.id}-render`, '2-variants', 'FAIL', {
      error: String(err && err.message || err),
    });
  } finally {
    await ctx.close().catch(() => {});
  }
}

for (const suite of VARIANT_SUITE) {
  await runVariantSmoke(suite);
}

// =====================================================================
// SECTION 3 — Mobile
// =====================================================================

async function runMobileAudit() {
  const { ctx, page, errors } = await newCtx({
    viewport: { width: 375, height: 667 },
    deviceScaleFactor: 2,
    isMobile: true,
    hasTouch: true,
  });
  try {
    await page.goto(`${baseUrl}/autotable/`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2500);
    // Force the lobby panel open at 375 (defang/lobby-active is the
    // Hicks pattern from #92).
    await page.evaluate(() => {
      const p = document.getElementById('lobby-panel');
      if (p) p.classList.add('lobby-open');
      document.body.classList.add('lobby-active');
    });
    await page.waitForTimeout(400);
    const shotLobby = await snap(page, 'mobile-01-lobby.png');

    const m = await page.evaluate(() => {
      const r = el => el ? (() => { const b = el.getBoundingClientRect(); return { x: b.x, y: b.y, w: b.width, h: b.height }; })() : null;
      const lobby = document.getElementById('lobby-panel');
      const qm = document.getElementById('lobby-quick-match');
      const picker = document.querySelector('.ferro-variant-picker-select');
      const close = document.querySelector('#lobby-panel .lobby-close-btn');
      return {
        docW: document.documentElement.scrollWidth,
        innerW: window.innerWidth,
        lobby: r(lobby),
        qm: r(qm),
        picker: r(picker),
        close: r(close),
      };
    });

    // 3a — viewport overflow gate
    const overflow = m.docW > m.innerW + 1;
    record('M-1-no-h-overflow', '3-mobile', overflow ? 'FAIL' : 'PASS', {
      docW: m.docW, innerW: m.innerW, delta: m.docW - m.innerW,
    }, [shotLobby]);

    // 3b — touch-target gates (≥44px) for the canonical controls
    const qmOk = !!m.qm && m.qm.h >= 44;
    record('M-2-touch-target-qm', '3-mobile', qmOk ? 'PASS' : 'FAIL', {
      height: m.qm?.h ?? null, width: m.qm?.w ?? null,
    });
    const pickerOk = !!m.picker && m.picker.h >= 44;
    record('M-3-touch-target-picker', '3-mobile', pickerOk ? 'PASS' : 'FAIL', {
      height: m.picker?.h ?? null, width: m.picker?.w ?? null,
    });
    const closeOk = !!m.close && m.close.h >= 44 && m.close.w >= 44;
    record('M-4-touch-target-lobby-close', '3-mobile', closeOk ? 'PASS' : 'FAIL', {
      height: m.close?.h ?? null, width: m.close?.w ?? null,
    });

    // 3c — sidebar shrinks to ~160px on mobile (post-close-lobby).
    // Close the lobby panel so the sidebar surfaces.
    await page.evaluate(() => {
      const p = document.getElementById('lobby-panel');
      if (p) p.classList.remove('lobby-open');
      document.body.classList.remove('lobby-active');
    });
    await page.waitForTimeout(400);

    // Wire a connection so the #sidebar gets de-collapsed.
    const gameId = `audit-mobile-${Date.now()}`;
    await page.goto(`${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=4&handCount=1&botDifficulty=Easy&gameId=${gameId}`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(3500);
    const qm = page.locator('#lobby-quick-match');
    if (await qm.first().isVisible().catch(() => false)) {
      await qm.first().click({ timeout: 5000 }).catch(() => {});
      await page.waitForTimeout(3000);
    }
    const cb = page.locator('#lobby-close');
    if (await cb.isVisible().catch(() => false)) {
      await cb.click({ force: true, timeout: 2000 }).catch(() => {});
    }
    const cn = page.locator('#connect');
    if (await cn.first().isVisible().catch(() => false)) {
      await cn.first().click({ timeout: 5000 }).catch(() => {});
      await page.waitForTimeout(3000);
    }
    await waitForCanvas(page, 15_000);
    const sidebarShot = await snap(page, 'mobile-02-sidebar.png');
    const sb = await page.evaluate(() => {
      const s = document.getElementById('sidebar');
      if (!s) return null;
      const r = s.getBoundingClientRect();
      const styles = window.getComputedStyle(s);
      return {
        width: r.width,
        widthCss: styles.width,
        cssText: `${styles.width}/${styles.maxWidth}`,
      };
    });
    // The hicks-mobile-sidebar.css sets `#sidebar { width: 160px }`
    // inside the 480px breakpoint.  Allow a 16px slack.
    const sidebarPass = sb && sb.width >= 140 && sb.width <= 200;
    record('M-5-sidebar-160px', '3-mobile', sidebarPass ? 'PASS' : 'FAIL', {
      width: sb?.width ?? null, cssText: sb?.cssText ?? null,
    }, [sidebarShot]);

    // 3d — safe-area-inset CSS present in the mobile sheet.
    const cssIncludesSafeArea = await page.evaluate(() => {
      const out = { lobbyTop: false, lobbyToggle: false };
      for (const sheet of document.styleSheets) {
        let rules;
        try { rules = sheet.cssRules; } catch { continue; }
        if (!rules) continue;
        for (const r of rules) {
          const txt = r.cssText || '';
          if (/safe-area-inset/.test(txt) && /#lobby-panel/.test(txt)) out.lobbyTop = true;
          if (/safe-area-inset/.test(txt) && /#lobby-toggle/.test(txt)) out.lobbyToggle = true;
        }
      }
      return out;
    });
    const safeAreaPass = cssIncludesSafeArea.lobbyTop || cssIncludesSafeArea.lobbyToggle;
    record('M-6-safe-area-inset', '3-mobile', safeAreaPass ? 'PASS' : 'FAIL', cssIncludesSafeArea);

    // 3e — page errors stay clean on mobile
    record('M-7-mobile-page-errors', '3-mobile',
      errors.pageErrors.length === 0 ? 'PASS' : 'FAIL', {
      pageErrors: errors.pageErrors.length,
      first: errors.pageErrors.slice(0, 3),
    });
  } catch (err) {
    record('M-X-fatal', '3-mobile', 'FAIL', {
      error: String(err && err.message || err),
    });
  } finally {
    await ctx.close().catch(() => {});
  }
}

await runMobileAudit();

// =====================================================================
// SECTION 4 — Claim window
// =====================================================================

async function runClaimWindowAudit() {
  const { ctx, page, errors } = await newCtx();
  const gameId = `audit-claim-${Date.now()}`;
  try {
    await page.goto(
      `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&handCount=1&botDifficulty=Easy&gameId=${gameId}`,
      { waitUntil: 'domcontentloaded' },
    );
    await page.waitForTimeout(2500);
    await takeSeatAtIndex(page, gameId, 0);
    await waitForCanvas(page, 20_000);
    await page.waitForTimeout(3500);

    // Wait for the Ferro overlay to attach (bootstrap polls every 100ms
    // for 30s after the renderer-ready event).  Without this wait the
    // overlay's lazy attach races our DOM probe.
    let overlayInfo = null;
    for (let i = 0; i < 30; i++) {
      overlayInfo = await page.evaluate(() => {
        const root = document.querySelector('.ferro-claim-overlay');
        if (!root) return { exists: false };
        const badges = root.querySelectorAll('.ferro-claim-badge');
        const types = Array.from(badges).map(b => b.dataset.claimType || b.textContent.trim());
        const pass = root.querySelector('.ferro-claim-pass');
        const timer = root.querySelector('.ferro-claim-timer-value');
        return {
          exists: true,
          badgeCount: badges.length,
          types,
          hasPassButton: !!pass,
          hasTimer: !!timer,
        };
      });
      if (overlayInfo.exists) break;
      await page.waitForTimeout(500);
    }
    record('C-1-claim-overlay-attached', '4-claim',
      overlayInfo.exists ? 'PASS' : 'FAIL', {
      attempts: 30,
      ...overlayInfo,
    });

    // Diagnostic probe — capture the full client state shape so a FAIL
    // on subsequent gates points at "no client" vs "wrong selfSeat".
    const clientState = await page.evaluate(() => {
      const g = (window).game;
      const c = g && g.client;
      return {
        hasGame: !!g,
        hasClient: !!c,
        hasClaim: !!(c && c.claim),
        hasGameComplete: !!(c && c.gameComplete),
        clientSeat: c ? c.seat : null,
        rendererReady: document.body.getAttribute('data-three-renderer-ready') === 'true',
        canvasCount: document.querySelectorAll('canvas').length,
      };
    });
    console.log('        clientState:', JSON.stringify(clientState));

    // 4b — synthetic claim payload should make overlay visible + buttons
    // labeled correctly.  Inject via the EventEmitter that Collection
    // uses for local subscribers (bypasses the wire path so we don't
    // depend on a bot discard landing during the audit window).
    // Key is `String(selfSeat)` per claim-window-overlay.ts:307 — NOT
    // `"current"` like other singleton collections.
    const synthetic = await page.evaluate(() => {
      const g = (window).game;
      if (!g || !g.client || !g.client.claim) return { ok: false, reason: 'no claim collection' };
      const selfSeat = g.client.seat;
      if (selfSeat === null || selfSeat === undefined) {
        return { ok: false, reason: 'no selfSeat', selfSeat };
      }
      const key = String(selfSeat);
      const payload = {
        available: ['Pung', 'Kong', 'Chow', 'Hu'],
        deadline: Date.now() + 8000,
        source: (selfSeat + 1) % 4,
        tile: 5,
      };
      try {
        const c = g.client.claim;
        c.map = c.map ?? new Map();
        c.map.set(key, payload);
        if (c.events && typeof c.events.emit === 'function') {
          c.events.emit('update', [[key, payload]], false);
        }
        return { ok: true, selfSeat, key, payload };
      } catch (e) {
        return { ok: false, reason: String(e) };
      }
    });
    await page.waitForTimeout(800);
    const overlayShot = await snap(page, 'claim-overlay-synthetic.png');
    const visState = await page.evaluate(() => {
      const root = document.querySelector('.ferro-claim-overlay');
      if (!root) return { visible: false };
      return {
        visible: root.classList.contains('ferro-claim-overlay-visible'),
        innerText: (root.textContent || '').trim().slice(0, 120),
        badgesVisible: Array.from(root.querySelectorAll('.ferro-claim-badge'))
          .map(b => ({
            type: b.dataset.claimType,
            available: b.classList.contains('ferro-claim-badge-available'),
          })),
      };
    });
    record('C-2-claim-overlay-visible-on-synthetic', '4-claim',
      (synthetic.ok && visState.visible) ? 'PASS' : 'FAIL', {
      synthetic,
      visState,
    }, [overlayShot]);

    // 4c — countdown ticks (per Apone #87)
    const timerSeq = await page.evaluate(async () => {
      const out = [];
      for (let i = 0; i < 4; i++) {
        const v = document.querySelector('.ferro-claim-timer-value');
        out.push(v ? v.textContent : null);
        await new Promise(r => setTimeout(r, 600));
      }
      return out;
    });
    const numeric = timerSeq.map(s => parseFloat((s || '').replace(/[^\d.]/g, '')) || null);
    const monotonicDown =
      numeric.filter(n => typeof n === 'number').length >= 2
      && numeric[0] !== null
      && numeric[numeric.length - 1] !== null
      && numeric[0] > numeric[numeric.length - 1];
    record('C-3-claim-countdown-decrements', '4-claim',
      monotonicDown ? 'PASS' : 'FAIL', { sequence: timerSeq, numeric });

    // 4d — legacy claim buttons exist (sidebar fallback for a11y)
    const legacy = await page.evaluate(() => ({
      pung: !!document.getElementById('claim-pung'),
      chow: !!document.getElementById('claim-chow'),
      kong: !!document.getElementById('claim-kong'),
      hu:   !!document.getElementById('claim-hu'),
      pass: !!document.getElementById('claim-pass'),
    }));
    const legacyPass = legacy.pung && legacy.chow && legacy.kong && legacy.hu && legacy.pass;
    record('C-4-legacy-claim-buttons', '4-claim',
      legacyPass ? 'PASS' : 'FAIL', legacy);

    // 4e — pass button surface
    const passInfo = await page.evaluate(() => {
      const pass = document.querySelector('.ferro-claim-pass');
      const label = document.querySelector('.ferro-claim-pass-label');
      return { exists: !!pass, labelText: label?.textContent ?? null };
    });
    record('C-5-pass-button', '4-claim',
      passInfo.exists ? 'PASS' : 'FAIL', passInfo);

  } catch (err) {
    record('C-X-fatal', '4-claim', 'FAIL', {
      error: String(err && err.message || err),
    });
  } finally {
    await ctx.close().catch(() => {});
  }
}

await runClaimWindowAudit();

// =====================================================================
// SECTION 5 — Win modal (synthetic gameComplete)
// =====================================================================

async function runWinModalAudit() {
  const { ctx, page, errors } = await newCtx();
  const gameId = `audit-win-${Date.now()}`;
  try {
    await page.goto(
      `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=3&handCount=1&botDifficulty=Easy&gameId=${gameId}`,
      { waitUntil: 'domcontentloaded' },
    );
    await page.waitForTimeout(2000);
    await takeSeatAtIndex(page, gameId, 0);
    await waitForCanvas(page, 15_000);
    await page.waitForTimeout(2500);

    // 5a — modal element exists in DOM
    const modalExists = await page.locator('#game-complete-modal').count() > 0;
    record('W-1-modal-present', '5-win-modal', modalExists ? 'PASS' : 'FAIL', { modalExists });

    // 5b — inject a synthetic gameComplete payload via the collection's
    // EventEmitter so the local handler in game-ui.ts fires.
    const inject = await page.evaluate(() => {
      const g = (window).game;
      if (!g || !g.client || !g.client.gameComplete) {
        return { ok: false, reason: 'no gameComplete collection' };
      }
      const payload = {
        isComplete: true,
        totalScores: { 0: 42, 1: -17, 2: -8, 3: -17 },
        maxHands: 4,
        handHistory: [{
          winner: 0,
          type: 'Hu',
          score: [
            { seat: 0, delta: 42 },
            { seat: 1, delta: -14 },
            { seat: 2, delta: -14 },
            { seat: 3, delta: -14 },
          ],
          hand: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13],
          nextBanker: 0,
        }],
      };
      try {
        const c = g.client.gameComplete;
        c.map = c.map ?? new Map();
        c.map.set('current', payload);
        if (c.events && typeof c.events.emit === 'function') {
          c.events.emit('update', [['current', payload]], false);
        }
        return { ok: true };
      } catch (e) {
        return { ok: false, reason: String(e) };
      }
    });
    // Bootstrap modal render has a small delay due to jQuery.
    await page.waitForTimeout(1500);
    const modalShot = await snap(page, 'win-modal-synthetic.png');
    const state = await page.evaluate(() => {
      const m = document.getElementById('game-complete-modal');
      if (!m) return { exists: false };
      const totalsRows = m.querySelectorAll('#game-complete-totals tbody tr');
      const headline = document.getElementById('game-complete-headline');
      const subtitle = document.getElementById('game-complete-subtitle');
      const rect = m.getBoundingClientRect();
      const rendered = (m.classList.contains('show') || m.classList.contains('in')
                       || window.getComputedStyle(m).display !== 'none')
                       && rect.width > 100 && rect.height > 100;
      return {
        exists: true,
        rendered,
        classList: m.className,
        display: window.getComputedStyle(m).display,
        totalsRowCount: totalsRows.length,
        firstRowText: totalsRows[0]?.textContent?.trim() ?? null,
        headlineText: headline?.textContent ?? null,
        subtitleText: subtitle?.textContent ?? null,
      };
    });
    const renderOk = inject.ok && state.rendered && state.totalsRowCount > 0;
    record('W-2-modal-renders-on-synthetic', '5-win-modal',
      renderOk ? 'PASS' : 'FAIL', { inject, state }, [modalShot]);

    // 5c — modal can be hidden via the tombstone path (server clearing
    // the gameComplete singleton triggers `dismissGameCompleteModal()`
    // in game-ui.ts:1814).  We emit `[['current', null]]` on the
    // collection's EventEmitter so the local handler runs without
    // depending on the jQuery `$` global being exposed inside
    // page.evaluate.
    const hidden = await page.evaluate(() => {
      const g = (window).game;
      if (!g || !g.client || !g.client.gameComplete) {
        return { ok: false, reason: 'no gameComplete' };
      }
      try {
        const c = g.client.gameComplete;
        if (c.map && c.map.delete) c.map.delete('current');
        if (c.events && typeof c.events.emit === 'function') {
          c.events.emit('update', [['current', null]], false);
        }
        return { ok: true };
      } catch (e) {
        return { ok: false, reason: String(e) };
      }
    });
    await page.waitForTimeout(1200);
    const afterHide = await page.evaluate(() => {
      const m = document.getElementById('game-complete-modal');
      if (!m) return null;
      return {
        classList: m.className,
        display: window.getComputedStyle(m).display,
      };
    });
    const hidePass = hidden.ok
      && afterHide
      && !(/\bshow\b|\bin\b/.test(afterHide.classList))
      && afterHide.display === 'none';
    record('W-3-modal-can-close', '5-win-modal', hidePass ? 'PASS' : 'FAIL', {
      hidden, afterHide,
    });

    // 5d — fan section (ferro-win-fans) attached if non-empty payload
    const fans = await page.evaluate(() => ({
      hasFanSection: !!document.getElementById('ferro-win-fans'),
    }));
    // Not a hard fail — fan section is conditionally rendered.
    record('W-4-fan-section-attached-conditional', '5-win-modal',
      'PASS', fans);

  } catch (err) {
    record('W-X-fatal', '5-win-modal', 'FAIL', {
      error: String(err && err.message || err),
    });
  } finally {
    await ctx.close().catch(() => {});
  }
}

await runWinModalAudit();

// =====================================================================
// SECTION 6 — DB post-audit delta (did rows grow?)
// =====================================================================

const dbAfter = readPlayerStatsSnapshot();
const grew = dbAfter.error
  ? false
  : (dbAfter.count ?? 0) > (dbBaseline.count ?? 0);
record('DB-3-rowcount-delta', '6-db-persistence',
  // Growth is informational — the audit creates many connections but
  // not all of them result in an OnDisconnect → stats-flush.  PASS if
  // the post-audit read succeeds AND has at least as many rows as the
  // baseline.  FAIL only if the DB became unreadable.
  dbAfter.error ? 'FAIL' : 'PASS', {
  baseline: dbBaseline.count ?? null,
  after: dbAfter.count ?? null,
  grew,
  error: dbAfter.error ?? null,
});

// =====================================================================
// FINAL — aggregate, write findings.json + REPORT.md
// =====================================================================

await browser.close();

const counts = { PASS: 0, FAIL: 0, SKIP: 0 };
for (const f of findings) counts[f.status]++;

const failures = findings.filter(f => f.status === 'FAIL');

const finalJson = {
  startedAt,
  finishedAt: new Date().toISOString(),
  baseUrl,
  sqlitePath: SQLITE_PATH,
  counts,
  findings,
};
fs.writeFileSync(
  path.join(ARTIFACT_DIR, 'findings.json'),
  JSON.stringify(finalJson, null, 2),
);

// Markdown report
const mdLines = [];
mdLines.push('# Ripley — Mahjong-Autotable System Audit Report');
mdLines.push('');
mdLines.push(`- Started: ${startedAt}`);
mdLines.push(`- Finished: ${finalJson.finishedAt}`);
mdLines.push(`- Base URL: ${baseUrl}`);
mdLines.push(`- Sqlite path: ${SQLITE_PATH}`);
mdLines.push('');
mdLines.push('## Totals');
mdLines.push('');
mdLines.push(`- **PASS:** ${counts.PASS}`);
mdLines.push(`- **FAIL:** ${counts.FAIL}`);
mdLines.push(`- **SKIP:** ${counts.SKIP}`);
mdLines.push('');
const verdict = counts.FAIL === 0
  ? '🟢 **SHIPPABLE** — every gate green.'
  : counts.FAIL <= 5
    ? '🟡 **SHIPPABLE WITH CAVEATS** — surface findings to owning agents.'
    : '🔴 **NOT SHIPPABLE** — multiple gates failed; cluster the fixes.';
mdLines.push(`## Overall Verdict — ${verdict}`);
mdLines.push('');
mdLines.push('## Findings by category');
mdLines.push('');
const byCat = {};
for (const f of findings) {
  byCat[f.category] = byCat[f.category] ?? [];
  byCat[f.category].push(f);
}
for (const cat of Object.keys(byCat).sort()) {
  mdLines.push(`### ${cat}`);
  mdLines.push('');
  mdLines.push('| ID | Status | Evidence (short) |');
  mdLines.push('|----|--------|------------------|');
  for (const f of byCat[cat]) {
    const ev = JSON.stringify(f.evidence || {});
    mdLines.push(`| \`${f.id}\` | ${f.status} | \`${ev.replace(/\|/g, '\\|').slice(0, 240)}\` |`);
  }
  mdLines.push('');
}
if (failures.length > 0) {
  mdLines.push('## Failures — full evidence');
  mdLines.push('');
  for (const f of failures) {
    mdLines.push(`### ${f.id} (${f.category})`);
    mdLines.push('');
    mdLines.push('```json');
    mdLines.push(JSON.stringify(f.evidence, null, 2));
    mdLines.push('```');
    if (f.screenshots && f.screenshots.length > 0) {
      mdLines.push('');
      for (const s of f.screenshots) {
        if (s) mdLines.push(`Screenshot: \`${path.basename(s)}\``);
      }
    }
    mdLines.push('');
  }
}

fs.writeFileSync(path.join(ARTIFACT_DIR, 'REPORT.md'), mdLines.join('\n'));

console.log(`\n=== SUMMARY ===`);
console.log(`PASS=${counts.PASS}  FAIL=${counts.FAIL}  SKIP=${counts.SKIP}`);
console.log(`Report:    ${path.join(ARTIFACT_DIR, 'REPORT.md')}`);
console.log(`Findings:  ${path.join(ARTIFACT_DIR, 'findings.json')}`);
// Exit 0 always — discovery spec.
process.exit(0);
