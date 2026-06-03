// Bishop — Ripley L-10 follow-up — leave-seat WS broadcast acceptance.
//
// Ripley's 2026-06-03 prodready-final audit flagged the leave-seat UX as
// the last MAJOR open bug: clicking "Leave seat" in Player A's tab
// successfully releases the seat in the ChangshaGameRuntime but the
// runtime broadcasts on the SignalR `/hubs/changsha` channel, NOT the
// autotable WS `/autotable/ws` that the bundle's seats / nicks
// collections listen on. So Player B's view continues to render seat 0
// as occupied (with Player A's ghost nickname) until a full page
// refresh re-runs the JOIN handshake.
//
// Bishop's fix (this PR): mirror the disconnect path inside
// `TryHandleSeatTakeAsync`'s null branch — after `ReleaseSeatAsync`,
// call `state.RemovePlayerEntries(playerId)` and broadcast the
// resulting (seats|nicks|mouse)[playerId] = null tombstones to peers
// via `BroadcastToOthersAsync`. The {seat:null} raw payload is dropped
// from the passthrough so the tombstone isn't immediately re-stored.
//
// This spec drives the real two-browser scenario:
//   1. Player A and Player B both open the same gameId (changsha, no
//      bots — the manual setup so seat-0 and seat-1 are filled by
//      humans only).
//   2. Player A takes seat 0; Player B confirms the seat is occupied
//      by A from their POV.
//   3. Player A clicks the in-game Leave-seat button.
//   4. Within 5s, Player B's view must reflect seat 0 as empty
//      WITHOUT any page refresh — observed via
//      `window.game.client.seats.get(playerAId)` going null/undefined
//      AND `window.game.client.nicks.get(playerAId)` going
//      null/undefined.
//   5. Both contexts must report `pageErrorsCount === 0`.
//
// Run:
//   E2E_BASE_URL=http://127.0.0.1:8088 \
//     node playtest-artifacts/playtest-leave-seat-broadcast.spec.mjs
//
// Artifacts:
//   playtest-artifacts/leave-seat-broadcast/findings.json
//   playtest-artifacts/leave-seat-broadcast/*.png
//
// Exit code: 0 on PASS, 1 on FAIL.

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const ARTIFACT_DIR = path.resolve('./playtest-artifacts/leave-seat-broadcast');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const ts = new Date().toISOString().replace(/[:.]/g, '-');
const findings = {
  ts,
  baseUrl,
  gates: [],
  pass: 0,
  fail: 0,
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
  try {
    const file = path.join(ARTIFACT_DIR, `${name}-${ts}.png`);
    await page.screenshot({ path: file, fullPage: false });
    return file;
  } catch {
    return null;
  }
}

// Tour overlay defang — `#tour-overlay` intercepts pointer events.
const overlayDefang = () => {
  const inject = () => {
    if (document.getElementById('leave-seat-broadcast-defang')) return;
    const style = document.createElement('style');
    style.id = 'leave-seat-broadcast-defang';
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

  // Also actively remove the tour overlay via MutationObserver — the
  // CSS defang above handles the static case; the observer catches any
  // late-rendered overlay (the tour script attaches after the DOM ready
  // signal so a pure-CSS override sometimes loses the race).
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

  // Also set the local-storage flag so the tour considers itself
  // completed — defensive defense-in-depth.
  try {
    localStorage.setItem('mahjong.tour.completed.v1', 'true');
  } catch { /* ignore */ }
};

function attachErrorTaps(page) {
  const errors = {
    pageErrors: [],
    consoleErrors: [],
  };
  page.on('pageerror', err => errors.pageErrors.push(String(err.message || err)));
  page.on('console', msg => {
    if (msg.type() === 'error') errors.consoleErrors.push(msg.text());
  });
  return errors;
}

const browser = await chromium.launch();

async function newCtx(label) {
  const ctx = await browser.newContext({
    viewport: { width: 1280, height: 800 },
  });
  const page = await ctx.newPage();
  await page.addInitScript(overlayDefang);
  const errors = attachErrorTaps(page);
  return { label, ctx, page, errors };
}

// ──────────────────────────────────────────────────────────────────
//  Helpers
// ──────────────────────────────────────────────────────────────────

async function dismissOverlays(page) {
  // Skip the tour if it's still rendered (defang script runs early,
  // but click-the-button is the canonical dismiss path).
  for (const sel of ['#tour-skip', '#onboarding-skip']) {
    const el = page.locator(sel);
    if (await el.isVisible().catch(() => false)) {
      await el.click({ force: true, timeout: 2000 }).catch(() => {});
      await page.waitForTimeout(150);
    }
  }
}

async function waitForGameReady(page, timeoutMs = 20_000) {
  // Poll until `window.game.client.playerId()` is a non-empty string,
  // which signals the JOINED handshake has resolved and the bundle's
  // Client is ready for seats/nicks reads.
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

async function takeSeatViaButton(page, seatIdx) {
  // Match the system-audit spec's `takeSeatAtIndex` shape: prefer the
  // overt sidebar take-seat button, fall back to driving the client
  // call directly. The latter is the wire-level equivalent the bundle
  // installs on the button onclick (game-ui.ts:568).
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
  // From this page's POV, wait until the seats collection reports
  // `otherPlayerId` occupies `seatIdx`.
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
    await page.waitForTimeout(150);
  }
  return false;
}

async function leaveSeat(page) {
  // Prefer the in-UI button so we exercise the same click path a real
  // user would. The button is wired at game-ui.ts:579 to
  // `client.seats.set(playerId, { seat: null })`. Fall back to the
  // wire-level write if the button isn't visible (e.g., sidebar is
  // collapsed in a small viewport).
  await page.evaluate(() => {
    const s = document.getElementById('sidebar');
    if (s) s.classList.remove('collapsed');
  });
  await page.waitForTimeout(150);
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

async function waitForSeatVacated(page, otherPlayerId, timeoutMs = 5_000) {
  // The acceptance criterion: from Player B's POV, the seats AND nicks
  // entries for Player A's playerId must clear (null / undefined)
  // within `timeoutMs`. We poll the bundle's local collection state —
  // this is exactly what the rendered scene reads from.
  const deadline = Date.now() + timeoutMs;
  let snapshot = null;
  while (Date.now() < deadline) {
    snapshot = await page.evaluate((pid) => {
      try {
        const g = window.game;
        const seatsEntry = g.client.seats.get(pid);
        const nicksEntry = g.client.nicks.get(pid);
        // The bundle's Collection.get returns undefined when the key
        // is tombstoned. We also accept seat === null as a legacy
        // "soft" empty state (the bundle's `{seat: null}` write).
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
    await page.waitForTimeout(150);
  }
  return { ok: false, snapshot };
}

// ──────────────────────────────────────────────────────────────────
//  Pre-flight: /health probe
// ──────────────────────────────────────────────────────────────────

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
  fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'),
    JSON.stringify(findings, null, 2));
  await browser.close();
  process.exit(1);
}

// ──────────────────────────────────────────────────────────────────
//  Scenario: two contexts, Player A leaves seat 0
// ──────────────────────────────────────────────────────────────────

const gameId = `bishop-leave-seat-${Date.now()}`;
// Use manual deal + 0 bots so seats stay in Seating phase (the runtime
// only honors ReleaseSeatAsync while the game is in Seating). The
// bundle URL params mirror the system-audit spec; botCount=0 keeps
// auto-fill from racing the leave-seat by claiming seat 0 with a bot.
const url = (label) =>
  `${baseUrl}/autotable/?variant=changsha&dealMode=manual&botCount=0&gameId=${gameId}#${label}`;

const A = await newCtx('A');
const B = await newCtx('B');

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

  // Player A takes seat 0.
  const takeRes = await takeSeatViaButton(A.page, 0);
  // Give the runtime a moment to bind + StateChanged → push the
  // translator's seats entry to Player B.
  await A.page.waitForTimeout(1500);
  await B.page.waitForTimeout(1500);

  const seatedFromA = await waitForSeatedAs(A.page, aPid, 0, 5_000);
  const seatedFromB = await waitForSeatedAs(B.page, aPid, 0, 5_000);
  await snap(A.page, 'A-seated-seat-0');
  await snap(B.page, 'B-sees-A-seated');
  record('S-3-A-takes-seat-0',
    (seatedFromA && seatedFromB) ? 'PASS' : 'FAIL',
    { takeVia: takeRes.via, seatedFromA, seatedFromB });
  if (!(seatedFromA && seatedFromB)) {
    throw new Error('seat-0 take did not propagate to peer');
  }

  // Snapshot Bob's view of Alice BEFORE leave so we have a baseline.
  const preLeave = await B.page.evaluate((pid) => {
    const g = window.game;
    return {
      seatsEntry: g.client.seats.get(pid) ?? null,
      nicksEntry: g.client.nicks.get(pid) ?? null,
    };
  }, aPid);

  // Player A presses Leave seat.
  const leaveRes = await leaveSeat(A.page);
  const leaveSentAt = Date.now();

  // ── The acceptance gate. ───────────────────────────────────────
  // Within 5 seconds, Player B's bundle must see (seats|nicks)[aPid]
  // tombstoned WITHOUT any reload. This is the L-10 regression Ripley
  // flagged — pre-fix, it requires a page refresh because the runtime
  // only broadcasts on SignalR.
  const result = await waitForSeatVacated(B.page, aPid, 5_000);
  const elapsedMs = Date.now() - leaveSentAt;
  await snap(A.page, 'A-after-leave');
  await snap(B.page, 'B-after-A-leave');

  record('S-4-B-sees-A-leave-within-5s',
    result.ok ? 'PASS' : 'FAIL',
    {
      leaveVia: leaveRes.via,
      elapsedMs,
      preLeave,
      postLeave: result.snapshot,
    });

  // Also verify the LOCAL view on A reflects the leave (sanity: the
  // local set runs synchronously so this should always be true; the
  // gate is mostly about catching a regression where the bundle's
  // local optimistic update is somehow undone by a server echo).
  const aPostLeave = await A.page.evaluate(() => {
    const g = window.game;
    const pid = g.client.playerId();
    const entry = g.client.seats.get(pid);
    return {
      pid,
      seatsEntry: entry === undefined ? null : entry,
      clientSeat: g.client.seat,
    };
  });
  const aVacatedLocal = aPostLeave.seatsEntry == null
    || aPostLeave.seatsEntry.seat === null
    || aPostLeave.seatsEntry.seat === undefined;
  record('S-5-A-local-seat-cleared',
    aVacatedLocal ? 'PASS' : 'FAIL',
    aPostLeave);

  record('S-6-A-no-page-errors',
    A.errors.pageErrors.length === 0 ? 'PASS' : 'FAIL',
    { pageErrorsCount: A.errors.pageErrors.length,
      pageErrors: A.errors.pageErrors.slice(0, 5) });
  record('S-7-B-no-page-errors',
    B.errors.pageErrors.length === 0 ? 'PASS' : 'FAIL',
    { pageErrorsCount: B.errors.pageErrors.length,
      pageErrors: B.errors.pageErrors.slice(0, 5) });

  // Bonus: a fresh take-seat by Bob on the now-vacated seat 0 should
  // succeed and propagate. Pins the full round-trip end-to-end.
  const bobTake = await takeSeatViaButton(B.page, 0);
  await B.page.waitForTimeout(1500);
  await A.page.waitForTimeout(1500);
  const bobNowSeated = await waitForSeatedAs(B.page, bPid, 0, 5_000)
    && await waitForSeatedAs(A.page, bPid, 0, 5_000);
  record('S-8-bob-can-claim-seat-0-after-A-leaves',
    bobNowSeated ? 'PASS' : 'FAIL',
    { takeVia: bobTake.via, bobNowSeated });

} catch (err) {
  record('S-X-fatal', 'FAIL', { error: String(err && err.message || err),
    stack: String(err && err.stack || '').slice(0, 800) });
} finally {
  await A.ctx.close().catch(() => {});
  await B.ctx.close().catch(() => {});
}

await browser.close();

// ──────────────────────────────────────────────────────────────────
//  Roll-up
// ──────────────────────────────────────────────────────────────────

fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'),
  JSON.stringify(findings, null, 2));

console.log('\n=== Roll-up ===');
console.log(`PASS: ${findings.pass}`);
console.log(`FAIL: ${findings.fail}`);
console.log(`findings.json: ${path.join(ARTIFACT_DIR, 'findings.json')}`);

process.exit(findings.fail === 0 ? 0 : 1);
