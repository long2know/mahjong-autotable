// Hicks — 2026-06-03 polish pass — Leave-seat UX visual proof.
//
// Bishop's `35b7f76` shipped the leave-seat tombstone broadcast on the
// autotable WS so peers' bundles tombstone (seats|nicks)[playerId]
// without a refresh.  This spec drives the same two-tab scenario as
// Bishop's `playtest-leave-seat-broadcast.spec.mjs` but emits the
// artifacts Stephen asked for in his polish brief:
//
//   • Tab B "before" screenshot (seat 0 nameplate showing Player A's
//     nickname).
//   • Tab B "after" screenshot (≤ 1500 ms after Tab A's leave-seat
//     click, seat 0 nameplate is cleared — NO page refresh).
//   • A measured `deltaMs` between the leave click and the moment Tab
//     B's bundle confirms (seats|nicks)[aPid] is tombstoned.
//
// The artifacts land under the polish-pass run dir so `findings.json`
// can reference them by relative path.
//
// Run:
//   E2E_BASE_URL=http://127.0.0.1:8088 \
//     POLISH_RUN_DIR=playtest-artifacts/screenshots/hicks-polish-<ts> \
//     node playtest-artifacts/playtest-leave-seat-ux.spec.mjs
//
// Exit code: 0 on PASS, 1 on FAIL.

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const RUN_TS = new Date().toISOString().replace(/[:.]/g, '-');
const ART_DIR = path.resolve(
  process.env.POLISH_RUN_DIR
    || `./playtest-artifacts/screenshots/hicks-polish-${RUN_TS}`,
);
fs.mkdirSync(ART_DIR, { recursive: true });

const findings = {
  ts: RUN_TS,
  baseUrl,
  outDir: ART_DIR,
  gates: [],
  pass: 0,
  fail: 0,
  deltaMs: null,
  beforePath: null,
  afterPath: null,
};

function record(id, status, evidence = {}) {
  findings.gates.push({ id, status, evidence });
  if (status === 'PASS') findings.pass++;
  else findings.fail++;
  const tag = status === 'PASS' ? '\x1b[32mPASS\x1b[0m' : '\x1b[31mFAIL\x1b[0m';
  console.log(`[${tag}] ${id}`);
  if (Object.keys(evidence).length) {
    const s = JSON.stringify(evidence);
    console.log(`        evidence: ${s.length > 300 ? s.slice(0, 300) + '…' : s}`);
  }
}

async function snap(page, name) {
  const file = path.join(ART_DIR, `${name}.png`);
  await page.screenshot({ path: file, fullPage: false });
  return file;
}

const overlayDefang = () => {
  const inject = () => {
    if (document.getElementById('hicks-leave-seat-defang')) return;
    const style = document.createElement('style');
    style.id = 'hicks-leave-seat-defang';
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
  try {
    const observer = new MutationObserver(() => {
      const overlay = document.getElementById('tour-overlay');
      if (overlay && overlay.parentNode) overlay.parentNode.removeChild(overlay);
    });
    if (document.body) {
      observer.observe(document.body, { childList: true, subtree: true });
    } else {
      document.addEventListener('DOMContentLoaded', () => {
        observer.observe(document.body, { childList: true, subtree: true });
      });
    }
  } catch { /* ignore */ }
  try {
    localStorage.setItem('mahjong.tour.completed.v1', 'true');
  } catch { /* ignore */ }
};

function attachErrorTaps(page) {
  const errors = {
    pageErrors: [],
    consoleErrors: [],
    nanRadius: 0,
  };
  page.on('pageerror', err => errors.pageErrors.push(String(err.message || err)));
  page.on('console', msg => {
    if (msg.type() !== 'error') return;
    const text = msg.text();
    if (/Computed radius is NaN/i.test(text)) {
      errors.nanRadius++;
      return;
    }
    errors.consoleErrors.push(text);
  });
  return errors;
}

async function newCtx(browser, label) {
  const ctx = await browser.newContext({
    viewport: { width: 1280, height: 800 },
  });
  const page = await ctx.newPage();
  await page.addInitScript(overlayDefang);
  const errors = attachErrorTaps(page);
  return { label, ctx, page, errors };
}

async function dismissOverlays(page) {
  for (const sel of ['#tour-skip', '#onboarding-skip']) {
    const el = page.locator(sel);
    if (await el.isVisible().catch(() => false)) {
      await el.click({ force: true, timeout: 2000 }).catch(() => {});
      await page.waitForTimeout(150);
    }
  }
}

async function waitForGameReady(page, timeoutMs = 25_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const ready = await page.evaluate(() => {
      try {
        const g = window.game;
        if (!g || !g.client || typeof g.client.playerId !== 'function') return false;
        const pid = g.client.playerId();
        return typeof pid === 'string' && pid.length > 0;
      } catch {
        return false;
      }
    });
    if (ready) return true;
    await page.waitForTimeout(150);
  }
  return false;
}

async function getPlayerId(page) {
  return await page.evaluate(() => {
    try { return window.game.client.playerId(); }
    catch { return null; }
  });
}

async function setNick(page, nick) {
  await page.evaluate((n) => {
    try {
      const g = window.game;
      g.client.nicks.set(g.client.playerId(), n);
    } catch { /* ignore */ }
  }, nick);
}

async function takeSeat(page, seatIdx) {
  // Prefer the UI button; fall back to the wire-level write so we
  // don't depend on the hold-to-confirm button firing inside a
  // headless context.
  const candidates = [
    `#take-seat-${seatIdx}`,
    `[data-testid="take-seat-${seatIdx}"]`,
    `#take-seat${seatIdx}`,
  ];
  for (const sel of candidates) {
    const el = page.locator(sel);
    if (await el.isVisible().catch(() => false)) {
      await el.click({ force: true, timeout: 3000 }).catch(() => {});
      return { via: sel };
    }
  }
  await page.evaluate((idx) => {
    const g = window.game;
    g.client.seats.set(g.client.playerId(), { seat: idx });
  }, seatIdx);
  return { via: 'client.seats.set' };
}

async function waitForSeatedAs(page, otherPlayerId, seatIdx, timeoutMs = 8_000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const ok = await page.evaluate(([pid, idx]) => {
      try {
        const entry = window.game.client.seats.get(pid);
        return entry != null && entry.seat === idx;
      } catch {
        return false;
      }
    }, [otherPlayerId, seatIdx]);
    if (ok) return true;
    await page.waitForTimeout(80);
  }
  return false;
}

async function leaveSeat(page) {
  await page.evaluate(() => {
    const s = document.getElementById('sidebar');
    if (s) s.classList.remove('collapsed');
  });
  await page.waitForTimeout(120);
  const btn = page.locator('#leave-seat');
  if (await btn.isVisible().catch(() => false)) {
    await btn.click({ force: true, timeout: 3000 }).catch(() => {});
    return { via: '#leave-seat click' };
  }
  await page.evaluate(() => {
    const g = window.game;
    g.client.seats.set(g.client.playerId(), { seat: null });
  });
  return { via: 'client.seats.set null fallback' };
}

async function waitForSeatVacated(page, otherPlayerId, timeoutMs = 1_500) {
  // Tight 1500 ms deadline per Stephen's polish brief — Bishop's
  // tombstone broadcast must clear seat 0's nameplate in Tab B
  // within that window.
  const deadline = Date.now() + timeoutMs;
  let snapshot = null;
  while (Date.now() < deadline) {
    snapshot = await page.evaluate((pid) => {
      try {
        const g = window.game;
        const seatsEntry = g.client.seats.get(pid);
        const nicksEntry = g.client.nicks.get(pid);
        const seatVacated = seatsEntry == null
          || seatsEntry.seat === null
          || seatsEntry.seat === undefined;
        const nickVacated = nicksEntry == null || nicksEntry === '';
        return {
          seatsEntry: seatsEntry === undefined ? null : seatsEntry,
          nicksEntry: nicksEntry === undefined ? null : nicksEntry,
          seatVacated,
          nickVacated,
        };
      } catch (e) {
        return { error: String(e && e.message || e) };
      }
    }, otherPlayerId);
    if (snapshot && snapshot.seatVacated && snapshot.nickVacated) {
      return { ok: true, snapshot };
    }
    // Poll fast for accurate deltaMs.
    await page.waitForTimeout(40);
  }
  return { ok: false, snapshot };
}

// ──────────────────────────────────────────────────────────────────
//  Pre-flight: /health probe
// ──────────────────────────────────────────────────────────────────

const browser = await chromium.launch();

try {
  const res = await fetch(`${baseUrl}/health`);
  if (!res.ok) {
    record('PRE-1-health', 'FAIL', { status: res.status });
    throw new Error('backend /health is not OK');
  }
  const body = await res.json();
  record('PRE-1-health', 'PASS', {
    status: body.status,
    version: body.version,
    dbConnected: body.db?.connected,
  });
} catch (err) {
  record('PRE-1-health', 'FAIL', { error: String(err && err.message || err) });
  fs.writeFileSync(path.join(ART_DIR, 'leave-seat-ux-findings.json'),
    JSON.stringify(findings, null, 2));
  await browser.close();
  process.exit(1);
}

const gameId = `hicks-leave-seat-ux-${Date.now()}`;
const url = (label) =>
  `${baseUrl}/autotable/?variant=changsha&dealMode=manual&botCount=0&gameId=${gameId}#${label}`;

const A = await newCtx(browser, 'A');
const B = await newCtx(browser, 'B');

try {
  await A.page.goto(url('A'), { waitUntil: 'domcontentloaded' });
  await B.page.goto(url('B'), { waitUntil: 'domcontentloaded' });
  await A.page.waitForTimeout(1500);
  await B.page.waitForTimeout(1500);
  await dismissOverlays(A.page);
  await dismissOverlays(B.page);

  const aReady = await waitForGameReady(A.page);
  const bReady = await waitForGameReady(B.page);
  record('S-1-bundles-ready', (aReady && bReady) ? 'PASS' : 'FAIL', {
    aReady, bReady,
  });
  if (!(aReady && bReady)) throw new Error('bundles did not boot');

  const aPid = await getPlayerId(A.page);
  const bPid = await getPlayerId(B.page);
  record('S-2-distinct-player-ids',
    (aPid && bPid && aPid !== bPid) ? 'PASS' : 'FAIL',
    { aPid, bPid });
  if (!(aPid && bPid && aPid !== bPid)) throw new Error('player ids missing');

  // Give each tab a recognizable nickname so the seat-0 nameplate in
  // tab B has a string to clear in the "before / after" screenshots.
  await setNick(A.page, 'HicksA-PolishPass');
  await setNick(B.page, 'HicksB-Observer');
  await A.page.waitForTimeout(300);
  await B.page.waitForTimeout(300);

  // Tab A takes seat 0.
  const takeRes = await takeSeat(A.page, 0);
  await A.page.waitForTimeout(1200);
  await B.page.waitForTimeout(1200);

  const seatedFromA = await waitForSeatedAs(A.page, aPid, 0, 5_000);
  const seatedFromB = await waitForSeatedAs(B.page, aPid, 0, 5_000);
  record('S-3-A-takes-seat-0',
    (seatedFromA && seatedFromB) ? 'PASS' : 'FAIL',
    { takeVia: takeRes.via, seatedFromA, seatedFromB });
  if (!(seatedFromA && seatedFromB)) {
    throw new Error('seat-0 take did not propagate to peer');
  }

  // BEFORE screenshot — Tab B sees Tab A's nick on seat 0.
  const beforePath = await snap(B.page, 'leave-seat-B-before');
  findings.beforePath = path.relative(path.resolve('.'), beforePath);
  const preLeave = await B.page.evaluate((pid) => {
    const g = window.game;
    return {
      seatsEntry: g.client.seats.get(pid) ?? null,
      nicksEntry: g.client.nicks.get(pid) ?? null,
    };
  }, aPid);

  // Tab A leaves seat 0.
  const leaveRes = await leaveSeat(A.page);
  const leaveSentAt = Date.now();

  // Tight 1500 ms acceptance gate — Bishop's broadcast must clear
  // both seats[aPid] and nicks[aPid] in Tab B before this expires.
  const result = await waitForSeatVacated(B.page, aPid, 1_500);
  const elapsedMs = Date.now() - leaveSentAt;
  findings.deltaMs = elapsedMs;

  // AFTER screenshot, regardless of pass/fail so we always have proof.
  const afterPath = await snap(B.page, 'leave-seat-B-after');
  findings.afterPath = path.relative(path.resolve('.'), afterPath);

  record('S-4-B-sees-A-leave-within-1500ms',
    result.ok ? 'PASS' : 'FAIL',
    {
      leaveVia: leaveRes.via,
      elapsedMs,
      preLeave,
      postLeave: result.snapshot,
    });

  record('S-5-A-no-page-errors',
    A.errors.pageErrors.length === 0 ? 'PASS' : 'FAIL',
    { pageErrorsCount: A.errors.pageErrors.length,
      pageErrors: A.errors.pageErrors.slice(0, 5) });
  record('S-6-B-no-page-errors',
    B.errors.pageErrors.length === 0 ? 'PASS' : 'FAIL',
    { pageErrorsCount: B.errors.pageErrors.length,
      pageErrors: B.errors.pageErrors.slice(0, 5) });

  findings.pageErrorsTotal = A.errors.pageErrors.length + B.errors.pageErrors.length;
  findings.nanRadiusTotal = A.errors.nanRadius + B.errors.nanRadius;

} catch (err) {
  record('S-X-fatal', 'FAIL', { error: String(err && err.message || err),
    stack: String(err && err.stack || '').slice(0, 800) });
} finally {
  await A.ctx.close().catch(() => {});
  await B.ctx.close().catch(() => {});
}

await browser.close();

fs.writeFileSync(path.join(ART_DIR, 'leave-seat-ux-findings.json'),
  JSON.stringify(findings, null, 2));

console.log('\n=== Roll-up ===');
console.log(`PASS: ${findings.pass}`);
console.log(`FAIL: ${findings.fail}`);
console.log(`deltaMs: ${findings.deltaMs}`);
console.log(`before:  ${findings.beforePath}`);
console.log(`after:   ${findings.afterPath}`);

process.exit(findings.fail === 0 ? 0 : 1);
