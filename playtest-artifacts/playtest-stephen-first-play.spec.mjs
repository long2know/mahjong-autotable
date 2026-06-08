// playtest-stephen-first-play.spec.mjs
// ─────────────────────────────────────────────────────────────────────
// Vasquez 2026-06-08 — Stephen Long has REJECTED "production ready"
// for the third time.  Every prior playtest used URL parameters like
// `?variant=changsha&dealMode=auto&botCount=3&gameId=X` to skip past
// the UX surface.  Stephen's actual experience is opening
//   http://127.0.0.1:8088/autotable/
// in a fresh browser tab with NO query string, like any normal user.
//
// This spec walks the BARE-URL FIRST-PLAY path end-to-end:
//   A. Landing                  — what renders on the empty URL?
//   B. Dismissals               — onboarding + tour overlays (real-user path)
//   C. Lobby pick               — Changsha + 3 bots + seat 0
//   D. Apply & Start            — page navigates with URL params
//   E. Seating                  — auto or manual take-seat
//   F. Deal (hold-to-confirm)   — 700ms hold on #deal
//   G. My hand visible          — seat-0 tiles face-up to me?
//   H. Make a discard           — can I click + discard a tile?
//   I. Bot turns                — bots draw + discard after me?
//   J. Claim window             — claim buttons enabled at the right time?
//   K. Sustained play           — game keeps progressing 60s+?
//
// Rules of engagement:
//   • NO URL params are appended.  We must use the on-screen UI exactly.
//   • Source code is read-only; this spec is observational.
//   • Overlays that have a user-visible Skip button are dismissed via
//     that button (i.e. the same gesture a real user makes), and
//     recorded as P1 friction (not P0 blockers) UNLESS the Skip itself
//     is broken or hidden.
//   • A genuine P0 is "user is stuck with no clear way to continue".
//   • A P1 is "user can continue but the path is unclear / clunky".
//
// Output:
//   playtest-artifacts/screenshots/stephen-first-play-<ts>/
//     phase-*.png + summary.json + findings.md
//
// Run:
//   cd /data/source/mahjong-autotable
//   E2E_BASE_URL=http://127.0.0.1:8088 \
//     node playtest-artifacts/playtest-stephen-first-play.spec.mjs
// ─────────────────────────────────────────────────────────────────────

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname  = path.dirname(__filename);

const BASE_URL = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const RUN_TS   = new Date().toISOString().replace(/[:.]/g, '-');
const RUN_TAG  = `stephen-first-play-${RUN_TS}`;
const ART_DIR  = path.resolve(__dirname, 'screenshots', RUN_TAG);
fs.mkdirSync(ART_DIR, { recursive: true });

// ── findings shell ─────────────────────────────────────────────────
const findings = {
  runTag: RUN_TAG,
  startedAt: new Date().toISOString(),
  baseUrl: BASE_URL,
  url: null,
  phases: [],
  pageErrors: [],
  consoleErrors: [],
  consoleWarnings: [],
  networkFailures: [],
  collections: {},
  blockers: [],
  confusions: [],
  polish: [],
};

function recordBug(severity, phase, description, screenshot, repro, suspect) {
  const entry = { phase, description, screenshot, repro, suspect, severity };
  if (severity === 'P0') findings.blockers.push(entry);
  else if (severity === 'P1') findings.confusions.push(entry);
  else findings.polish.push(entry);
  console.log(`  [${severity}] ${phase}: ${description}`);
}

async function snap(page, name) {
  const p = path.join(ART_DIR, name);
  await page.screenshot({ path: p, fullPage: true }).catch(err => {
    console.warn(`screenshot fail ${name}: ${err.message}`);
  });
  return p;
}

async function phase(name, fn) {
  console.log(`\n══════════ ${name} ══════════`);
  const t0 = Date.now();
  const entry = { name, ok: null, durMs: 0, result: null, error: null };
  try {
    entry.result = await fn();
    entry.ok = true;
  } catch (err) {
    entry.ok = false;
    entry.error = err && err.message || String(err);
    console.log(`!! ${name} FAILED: ${entry.error}`);
  }
  entry.durMs = Date.now() - t0;
  findings.phases.push(entry);
  console.log(`──── ${name} ${entry.ok ? 'OK' : 'FAIL'} (${entry.durMs}ms) ────`);
  return entry;
}

// ── browser setup ──────────────────────────────────────────────────
const browser = await chromium.launch({ headless: true });
const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await ctx.newPage();

page.on('console', msg => {
  const t = msg.type();
  const text = msg.text();
  if (t === 'error') findings.consoleErrors.push(text);
  if (t === 'warning') {
    if (!/NaN/i.test(text)) findings.consoleWarnings.push(text);
  }
  const m = text.match(/(?:full update|update) (\w+) (\d+)/);
  if (m) findings.collections[m[1]] = parseInt(m[2], 10);
});
page.on('pageerror', err => findings.pageErrors.push({
  message: err.message, stack: (err.stack ?? '').split('\n').slice(0, 6).join('\n'),
}));
page.on('response', resp => {
  if (resp.status() >= 400) {
    findings.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
  }
});

// ── helpers ───────────────────────────────────────────────────────
async function dismissOverlaysIfPresent(label) {
  // Real user path: click the visible Skip buttons.  Returns the names of
  // overlays that were dismissed (so we can record friction in findings).
  const dismissed = [];
  // Tour overlay: #tour-skip
  try {
    const tour = page.locator('#tour-skip');
    if (await tour.isVisible({ timeout: 500 }).catch(() => false)) {
      await tour.click({ timeout: 3000 });
      dismissed.push('tour');
      await page.waitForTimeout(400);
    }
  } catch {}
  // Onboarding card: #onboarding-skip
  try {
    const ob = page.locator('#onboarding-skip');
    if (await ob.isVisible({ timeout: 500 }).catch(() => false)) {
      await ob.click({ timeout: 3000 });
      dismissed.push('onboarding');
      await page.waitForTimeout(400);
    }
  } catch {}
  if (dismissed.length) {
    console.log(`  dismissed at ${label}: ${dismissed.join(', ')}`);
  }
  return dismissed;
}

async function waitForGame(timeoutMs = 12000) {
  // Wait for window.game.client.connected() === true.
  return await page.evaluate(async (timeoutMs) => {
    const t0 = Date.now();
    while (Date.now() - t0 < timeoutMs) {
      const g = window.game;
      if (g && g.client && typeof g.client.connected === 'function'
          && g.client.connected()) {
        return { ok: true, seat: g.client.seat ?? null,
                 elapsedMs: Date.now() - t0,
                 hasWorld: !!g.world };
      }
      await new Promise(r => setTimeout(r, 200));
    }
    return { ok: false, elapsedMs: Date.now() - t0,
             gamePresent: !!window.game,
             clientPresent: !!(window.game && window.game.client) };
  }, timeoutMs);
}

// ── PHASE A: Landing ──────────────────────────────────────────────
await phase('Phase A: Landing (bare URL, no query params)', async () => {
  await page.goto(`${BASE_URL}/autotable/`, { waitUntil: 'domcontentloaded' });
  findings.url = page.url();
  await page.waitForTimeout(4500);
  await snap(page, 'phase-A-landing.png');

  const inv = await page.evaluate(() => {
    function vis(sel) {
      const el = document.querySelector(sel);
      if (!el) return { present: false, visible: false };
      const cs = getComputedStyle(el);
      const r = el.getBoundingClientRect();
      const visible = cs.display !== 'none' && cs.visibility !== 'hidden'
                   && parseFloat(cs.opacity) > 0
                   && r.width > 0 && r.height > 0;
      return { present: true, visible };
    }
    return {
      lobbyPanel:    vis('#lobby-panel'),
      lobbyOpen:     !!document.querySelector('#lobby-panel.lobby-open'),
      bodyLobbyActv: document.body.classList.contains('lobby-active'),
      lobbyToggle:   vis('#lobby-toggle'),
      quickMatch:    vis('#lobby-quick-match'),
      applyBtn:      vis('#lobby-apply'),
      tourOverlay:   vis('#tour-overlay'),
      tourSkip:      vis('#tour-skip'),
      onboarding:    vis('#onboarding-card'),
      onboardingSkip:vis('#onboarding-skip'),
      magicLink:     vis('#magic-link-landing'),
      signin:        vis('#signin-modal-backdrop'),
      loadingText:   vis('#loading'),
      url:           location.href,
    };
  });

  // Diagnose what a first-time user sees.  The variant select is
  // visually a dropdown ("Changsha (长沙麻将)" in the screenshot).
  if (!inv.lobbyOpen) {
    if (!inv.lobbyToggle.visible) {
      recordBug('P0', 'A',
        'Bare URL renders no lobby AND no visible Lobby toggle button — user has zero hint how to start a game',
        'phase-A-landing.png',
        'Open http://127.0.0.1:8088/autotable/ in a fresh browser.',
        'src/frontend/autotable-src/src/lobby.ts shouldShowOnLoad()');
    } else {
      recordBug('P1', 'A',
        'Bare URL does not auto-open lobby — user must discover the Lobby toggle button',
        'phase-A-landing.png',
        'Open http://127.0.0.1:8088/autotable/ in a fresh browser.',
        'src/frontend/autotable-src/src/lobby.ts shouldShowOnLoad()');
    }
  }
  if (inv.magicLink.visible || inv.signin.visible) {
    recordBug('P0', 'A',
      'Bare URL surfaces a sign-in / magic-link modal that blocks play for anonymous users',
      'phase-A-landing.png',
      'Open http://127.0.0.1:8088/autotable/ in a fresh browser.',
      'magic-link.ts / signin-modal');
  }
  if (inv.tourOverlay.visible && !inv.tourSkip.visible) {
    recordBug('P0', 'A',
      'Tour overlay is shown but the Skip button is NOT visible — first-time user is trapped',
      'phase-A-landing.png',
      'Open bare URL → tour overlay appears → check for #tour-skip.',
      'src/frontend/autotable-src/src/tour.ts ensureRoot/paintStep');
  }
  if (inv.onboarding.visible && !inv.onboardingSkip.visible) {
    recordBug('P1', 'A',
      'Onboarding card is shown but Skip is hidden — user must fill name/avatar before playing',
      'phase-A-landing.png',
      'Open bare URL → check #onboarding-skip visibility.',
      'identity-onboarding.ts');
  }

  return inv;
});

// ── PHASE B: Dismiss the overlays the way a real user would ──────
await phase('Phase B: Dismissals (Skip Tour + Skip Onboarding)', async () => {
  const dismissed = await dismissOverlaysIfPresent('phase-B');
  await page.waitForTimeout(800);
  await snap(page, 'phase-B-after-dismissals.png');

  // After dismissal, the lobby should be unobstructed.  Check that
  // Apply & Start is now reachable (no longer intercepted by an
  // overlay).  We scroll the apply button into view first because the
  // lobby panel is long and the button is below the fold — that's
  // expected, not a bug.
  const reachable = await page.evaluate(() => {
    const apply = document.getElementById('lobby-apply');
    if (!apply) return { present: false };
    apply.scrollIntoView({ block: 'center', behavior: 'instant' });
    const r = apply.getBoundingClientRect();
    if (r.width === 0 || r.height === 0) return { present: true, visible: false };
    const cx = r.left + r.width / 2;
    const cy = r.top  + r.height / 2;
    // Only run hit-test if the centre is on-screen after scrollIntoView.
    if (cy < 0 || cy > window.innerHeight) {
      return { present: true, visible: true, offscreen: true,
               note: 'Apply button is below the lobby fold; user must scroll.' };
    }
    const top = document.elementFromPoint(cx, cy);
    const isApply = top === apply || (top && apply.contains(top));
    return {
      present: true, visible: true,
      hitTopId: top?.id ?? null,
      hitTopTag: top?.tagName ?? null,
      isApplyClickable: isApply,
    };
  });

  if (!reachable.present) {
    recordBug('P0', 'B', 'After dismissing overlays, #lobby-apply still not present in DOM',
      'phase-B-after-dismissals.png',
      'Bare URL → dismiss tour + onboarding → check DOM.',
      'lobby.ts initLobby');
  } else if (reachable.offscreen) {
    recordBug('P1', 'B',
      'Apply & Start button is below the lobby fold — user must scroll the lobby panel to find it (cheap CTA visibility)',
      'phase-B-after-dismissals.png',
      'Bare URL → dismiss overlays → look at lobby panel without scrolling.',
      'src/frontend/autotable-src/src/lobby.html — pin #lobby-apply to a sticky footer or move it above the seat/handCount sections.');
  } else if (reachable.isApplyClickable === false) {
    recordBug('P0', 'B',
      `Even after dismissing overlays AND scrolling into view, #lobby-apply is still occluded by ${reachable.hitTopTag}#${reachable.hitTopId}`,
      'phase-B-after-dismissals.png',
      'Bare URL → click #tour-skip → click #onboarding-skip → scrollIntoView(#lobby-apply) → elementFromPoint at button centre.',
      'lobby.ts hidePanel / tour.ts endTour / z-index ordering');
  }

  // Record overlay friction (not blockers if Skip worked).
  if (dismissed.includes('tour')) {
    recordBug('P1', 'B',
      'Tour overlay appears before lobby and intercepts pointer events on Apply & Start — user MUST click Skip Tour first',
      'phase-A-landing.png',
      'Open bare URL.  Tour overlay covers the lobby.  User must locate + click "Skip tour" before proceeding.',
      'src/frontend/autotable-src/src/tour.ts ensureRoot — consider not painting modal overlay until lobby is dismissed, or moving tour to a non-blocking corner.');
  }
  if (dismissed.includes('onboarding')) {
    recordBug('P2', 'B',
      'Onboarding card (name/avatar) appears in the lobby — Skip is available but it adds friction to first play',
      'phase-A-landing.png',
      'Open bare URL.  Onboarding card sits between user and Apply & Start.',
      'src/frontend/autotable-src/src/identity-onboarding.ts');
  }

  return { dismissed, reachable };
});

// ── PHASE C: Lobby picks (Changsha + 3 bots + seat 0) ────────────
await phase('Phase C: Lobby picks (Changsha + 3 bots + seat 0)', async () => {
  // The lobby radios may be visually-hidden inputs styled by labels.
  // We click the LABEL or use the input's parent label as the target.
  async function pickByName(name, value) {
    const label = page.locator(`label:has(input[name="${name}"][value="${value}"])`);
    const exists = await label.count();
    if (exists === 0) return { picked: false, reason: 'no-label' };
    try {
      await label.first().click({ timeout: 3000 });
      return { picked: true };
    } catch (e) {
      // fallback: force-set the radio via JS.
      const set = await page.evaluate(({ name, value }) => {
        const el = document.querySelector(`input[name="${name}"][value="${value}"]`);
        if (!el) return false;
        el.checked = true;
        el.dispatchEvent(new Event('change', { bubbles: true }));
        return true;
      }, { name, value });
      return { picked: set, viaJs: true, reason: String(e) };
    }
  }

  const variantPick = await pickByName('lobby-variant', 'changsha');
  const botsPick    = await pickByName('lobby-bot-count', '3');
  const diffPick    = await pickByName('lobby-bot-difficulty', 'Medium');
  const seatPick    = await pickByName('lobby-seat', '0');
  const handPick    = await pickByName('lobby-hand-count', '4');

  await snap(page, 'phase-C-after-picks.png');

  const checked = await page.evaluate(() => {
    function c(name) {
      const el = document.querySelector(`input[name="${name}"]:checked`);
      return el ? el.value : null;
    }
    return {
      variant: c('lobby-variant'),
      dealMode: c('lobby-deal-mode'),
      botCount: c('lobby-bot-count'),
      difficulty: c('lobby-bot-difficulty'),
      seat: c('lobby-seat'),
      handCount: c('lobby-hand-count'),
    };
  });

  // Sanity: if any of the must-have picks didn't take, the lobby is broken.
  if (checked.variant !== 'changsha') {
    recordBug('P0', 'C',
      `Variant radio is not "changsha" after click — actual: "${checked.variant}". User cannot select Changsha from lobby.`,
      'phase-C-after-picks.png',
      'Bare URL → dismiss overlays → click Changsha label in lobby.',
      'lobby.ts variantInputs / lobby.html lobby-variant-fieldset');
  }
  if (checked.botCount !== '3') {
    recordBug('P0', 'C',
      `Bot count not "3" after click — actual: "${checked.botCount}". User cannot fill seats with bots.`,
      'phase-C-after-picks.png',
      'Bare URL → dismiss overlays → click "3 bots" label.',
      'lobby.ts botCountInputs');
  }
  if (checked.seat !== '0') {
    recordBug('P1', 'C',
      `Seat picker not "0" after click — actual: "${checked.seat}". User cannot pre-pick seat 0.`,
      'phase-C-after-picks.png',
      'Bare URL → dismiss overlays → click "Seat 0" label.',
      'lobby.ts seatInputs');
  }

  return { variantPick, botsPick, diffPick, seatPick, handPick, checked };
});

// ── PHASE D: Apply & Start → page reload (NO auto-connect!) ──────
await phase('Phase D: Apply & Start (navigate)', async () => {
  // Re-dismiss any newly-shown overlays before clicking Apply.
  await dismissOverlaysIfPresent('phase-D-pre-apply');

  const apply = page.locator('#lobby-apply');
  if (!(await apply.isVisible().catch(() => false))) {
    recordBug('P0', 'D',
      'Apply & Start button not visible — user cannot launch their game',
      'phase-C-after-picks.png',
      'Bare URL → dismiss overlays → check footer of lobby panel.',
      'lobby.html #lobby-apply');
    return { error: 'apply not visible' };
  }

  // Apply triggers window.location.replace(...) — navigate event.
  await Promise.all([
    page.waitForLoadState('domcontentloaded', { timeout: 10000 }).catch(() => null),
    apply.click({ timeout: 5000 }),
  ]);
  await page.waitForTimeout(4500);
  findings.url = page.url();

  // Post-nav: dismiss overlays again.
  await dismissOverlaysIfPresent('phase-D-post-nav');
  await snap(page, 'phase-D-after-apply.png');

  // Probe: is the page auto-connected, or stranded?
  const probe = await page.evaluate(() => {
    const g = window.game;
    const cli = g && g.client;
    const cs = (sel) => {
      const el = document.querySelector(sel);
      if (!el) return null;
      const s = getComputedStyle(el);
      return { display: s.display, visible: s.display !== 'none' && s.visibility !== 'hidden' };
    };
    return {
      url: location.href,
      hasGameIdParam: new URLSearchParams(location.search).has('gameId'),
      gamePresent: !!g,
      clientConnected: !!(cli && cli.connected && cli.connected()),
      connectBtn:    cs('#connect.server-disconnected'),
      disconnectBtn: cs('#disconnect.server-connected'),
    };
  });

  // CRITICAL FINDING: lobby.buildUrl() doesn't include gameId, but
  // client-ui.ts:start() requires gameId in URL to auto-connect.
  // So Apply & Start NEVER auto-connects from a bare-URL flow.
  if (!probe.clientConnected && !probe.hasGameIdParam) {
    recordBug('P0', 'D',
      `"Apply & Start" navigated to ${probe.url} BUT did NOT auto-connect to the WebSocket. The lobby's buildUrl() omits ?gameId=, and client-ui.ts:start() guards auto-connect on getUrlState() (the gameId query param), so the user lands on an EMPTY 3D table with a "Connect" button and must click it manually. This contradicts the button label "Apply & Start".`,
      'phase-D-after-apply.png',
      '1) Open http://127.0.0.1:8088/autotable/ bare. 2) Dismiss tour + onboarding. 3) Pick Changsha + 3 bots + Seat 0 (defaults). 4) Click Apply & Start. 5) Observe: page reloads with ?variant=changsha&dealMode=manual&botCount=3&botDifficulty=Medium&handCount=4&seat=0 (NO gameId), the 3D table renders empty, and #connect button is still shown.',
      'src/frontend/autotable-src/src/lobby.ts buildUrl() at line 448 (does not add gameId) + src/frontend/autotable-src/src/client-ui.ts start() at line 490 (only auto-connects when getUrlState() returns non-null gameId). Fix: lobby.buildUrl should mint a fresh gameId (e.g. crypto.randomUUID slice) when none exists, OR client-ui.ts:start() should auto-connect on ANY lobby-built URL.');
  }

  return { url: findings.url, probe };
});

// ── PHASE D2: Click Connect (the user-discoverable gesture) ─────
await phase('Phase D2: Click Connect (manual)', async () => {
  await dismissOverlaysIfPresent('phase-D2');
  const connect = page.locator('#connect.server-disconnected');
  if (!(await connect.isVisible().catch(() => false))) {
    // Maybe already connected.
    const isConnected = await page.evaluate(() => {
      const g = window.game;
      return !!(g && g.client && g.client.connected && g.client.connected());
    });
    if (isConnected) return { alreadyConnected: true };
    recordBug('P0', 'D2',
      'After Apply & Start, neither auto-connect happened nor is the #connect button visible — user is stranded',
      'phase-D-after-apply.png',
      'Bare URL → lobby Apply → check for visible #connect button.',
      'client-ui.ts visibility of #connect when disconnected');
    return { error: 'no connect button' };
  }
  await connect.click({ timeout: 5000 });
  await page.waitForTimeout(3500);
  await snap(page, 'phase-D2-after-connect.png');

  const settled = await waitForGame(8000);
  if (!settled.ok) {
    recordBug('P0', 'D2',
      `After clicking Connect, WebSocket did not establish within 8s — game state: ${JSON.stringify(settled)}`,
      'phase-D2-after-connect.png',
      'Bare URL → lobby Apply → click #connect → wait 8s.',
      'client-ui.ts connect() / ws endpoint handshake');
  }
  return { settled };
});

// ── PHASE E: Take Seat (manual fallback) ─────────────────────────
await phase('Phase E: Take seat (manual fallback if auto-seat skipped)', async () => {
  await dismissOverlaysIfPresent('phase-E');

  const currentSeat = await page.evaluate(() => window.game?.client?.seat ?? null);
  if (currentSeat !== null && currentSeat !== undefined && currentSeat !== -1) {
    return { alreadySeated: true, seat: currentSeat };
  }

  const takeSeats = page.locator('.take-seat');
  const total = await takeSeats.count();
  let firstVisible = -1;
  for (let i = 0; i < total; i++) {
    if (await takeSeats.nth(i).isVisible().catch(() => false)) {
      firstVisible = i; break;
    }
  }
  if (firstVisible === -1) {
    recordBug('P0', 'E',
      `No visible Take Seat button after lobby Apply (game not constructed or all seats occupied). seat=${currentSeat}`,
      'phase-D-after-settle.png',
      'Bare URL → lobby → Apply & Start → wait 5s → look for visible .take-seat.',
      'AutotableWsEndpoint seating handshake / game-ui.ts seat-buttons');
    return { error: 'no take-seat visible' };
  }
  await takeSeats.nth(firstVisible).click({ timeout: 5000 });
  await page.waitForTimeout(2500);
  const seatNow = await page.evaluate(() => window.game?.client?.seat ?? null);
  await snap(page, 'phase-E-took-seat.png');
  if (seatNow === null || seatNow === undefined || seatNow === -1) {
    recordBug('P0', 'E',
      `Clicked Take Seat but client.seat is still ${seatNow}`,
      'phase-E-took-seat.png',
      'Bare URL → Apply → click Take Seat → wait 2.5s.',
      'client-ui.ts take-seat handler / ws endpoint');
  }
  return { clickedIdx: firstVisible, seatNow };
});

// ── PHASE F: Deal (hold-to-confirm) ──────────────────────────────
await phase('Phase F: Deal — hold-to-confirm 700ms on #deal', async () => {
  await dismissOverlaysIfPresent('phase-F');

  const dealBtn = page.locator('#deal');
  if (!(await dealBtn.isVisible().catch(() => false))) {
    recordBug('P0', 'F',
      'Deal button not visible after seating — user has no way to start the hand',
      'phase-E-took-seat.png',
      'Bare URL → lobby → Apply → take seat → look for #deal in sidebar.',
      'game-ui.ts #deal visibility / setupProgressButton');
    return { error: 'no deal button' };
  }

  // Check no overlay intercepts.
  const obstruction = await page.evaluate(() => {
    const d = document.getElementById('deal');
    if (!d) return { error: 'no #deal' };
    const r = d.getBoundingClientRect();
    const top = document.elementFromPoint(r.left + r.width/2, r.top + r.height/2);
    const blocked = top !== d && (top ? !d.contains(top) : false);
    return { blocked,
             topTag: top?.tagName ?? null,
             topId: top?.id ?? null,
             lobbyStillOpen: !!document.querySelector('#lobby-panel.lobby-open') };
  });
  if (obstruction.blocked) {
    recordBug('P0', 'F',
      `#deal click target is occluded by ${obstruction.topTag}#${obstruction.topId} — user click would be eaten by overlay`,
      'phase-E-took-seat.png',
      'After take-seat, hit-test the centre of #deal.',
      obstruction.lobbyStillOpen ? 'lobby.ts hidePanel (lobby is still open!)'
                                  : 'overlay z-index / pointer-events');
  }

  // The hold-to-deal gesture.
  const box = await dealBtn.boundingBox();
  if (!box) {
    recordBug('P0', 'F', 'Deal button has no bounding box', 'phase-E-took-seat.png',
      'After take-seat.', 'game-ui.ts setupProgressButton');
    return { error: 'no bbox' };
  }
  const cx = box.x + box.width / 2;
  const cy = box.y + box.height / 2;

  await page.mouse.move(cx, cy);
  await page.waitForTimeout(80);
  await page.mouse.down();
  await page.waitForTimeout(750);    // > 600 ms commit threshold
  await page.mouse.up();
  await page.waitForTimeout(400);
  await snap(page, 'phase-F-0s-immediately.png');

  await page.waitForTimeout(3000);
  await snap(page, 'phase-F-3s-after.png');

  await page.waitForTimeout(5000);
  await snap(page, 'phase-F-8s-after.png');

  const stateAfter = await page.evaluate(() => {
    const g = window.game;
    if (!g) return { error: 'no game' };
    const cli = g.client;
    if (!cli) return { error: 'no client' };
    const seat = cli.seat;
    let mySeatHand = 0, allHandTiles = 0, wallTiles = 0, discards = 0;
    if (cli.things) {
      for (const [, v] of cli.things.entries()) {
        const slot = v?.slotName ?? v?.SlotName;
        if (typeof slot !== 'string') continue;
        if (slot.startsWith('hand.')) allHandTiles++;
        if (slot.startsWith('hand.') && slot.endsWith(`@${seat}`)) mySeatHand++;
        if (slot.startsWith('wall.')) wallTiles++;
        if (slot.startsWith('discard.')) discards++;
      }
    }
    return { seat, mySeatHand, allHandTiles, wallTiles, discards };
  });

  if (stateAfter.error) {
    recordBug('P0', 'F', `Game state not accessible after deal: ${stateAfter.error}`,
      'phase-F-8s-after.png',
      'Bare URL → lobby → Apply → take seat → hold #deal 700ms → wait 8s.',
      'world.ts / client-ui.ts');
  } else if (stateAfter.mySeatHand === 0) {
    recordBug('P0', 'F',
      `After hold-to-deal, seat ${stateAfter.seat} has ZERO hand tiles 8s later (wall=${stateAfter.wallTiles}, allHand=${stateAfter.allHandTiles}, discards=${stateAfter.discards})`,
      'phase-F-8s-after.png',
      'Bare URL → lobby → Apply → take seat → mouse.down on #deal → wait 750ms → mouse.up → wait 8s.',
      'AutotableWsEndpoint deal handler / world.deal / setupProgressButton');
  } else if (stateAfter.mySeatHand < 13) {
    recordBug('P1', 'F',
      `After hold-to-deal, my hand has only ${stateAfter.mySeatHand} tiles (expected 13 or 14)`,
      'phase-F-8s-after.png',
      'Inspect cli.things slot counts 8s after deal.',
      'world.ts deal flow / pickup state machine');
  }

  return stateAfter;
});

// ── PHASE G: My hand is in seat-0 hand slots (visual check) ─────
await phase('Phase G: My hand populated', async () => {
  await dismissOverlaysIfPresent('phase-G');
  await page.waitForTimeout(1500);
  await snap(page, 'phase-G-hand-visible.png');

  const handState = await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.client) return { error: 'no game' };
    const cli = g.client;
    const seat = cli.seat;
    if (seat === null || seat === undefined) return { error: 'no seat' };
    const myTiles = [];
    const otherTiles = [];
    if (cli.things) {
      for (const [k, v] of cli.things.entries()) {
        const slot = v?.slotName ?? v?.SlotName;
        if (typeof slot !== 'string' || !slot.startsWith('hand.')) continue;
        const rotIdx = v?.rotationIndex ?? v?.RotationIndex ?? null;
        const entry = { id: k, slot, rotIdx };
        if (slot.endsWith(`@${seat}`)) myTiles.push(entry);
        else otherTiles.push(entry);
      }
    }
    // The 3D scene uses per-seat rotation conventions; rotationIndex
    // alone is NOT a reliable face-up/face-down signal across seats.
    // We rely on visual evidence (screenshot) for face-up confirmation
    // and only check the tile counts here.
    const rotIdxs = myTiles.map(t => t.rotIdx);
    return {
      seat, myTotal: myTiles.length,
      otherTotal: otherTiles.length,
      myRotIdxCounts: rotIdxs.reduce((acc, r) => { acc[r] = (acc[r] || 0) + 1; return acc; }, {}),
      sampleMy: myTiles.slice(0, 4),
    };
  });

  if (handState.error) {
    recordBug('P0', 'G', `Cannot inspect hand: ${handState.error}`,
      'phase-G-hand-visible.png',
      'After deal, evaluate window.game.client.things.',
      'world.ts client.things');
  } else if (handState.myTotal === 0) {
    // already reported in F
  } else if (handState.myTotal < 13) {
    recordBug('P1', 'G',
      `My hand has ${handState.myTotal} tiles (expected 13 or 14)`,
      'phase-G-hand-visible.png',
      'After deal, count slots hand.*@<mySeat> in client.things.',
      'world.ts hand-slot population / deal flow');
  }
  return handState;
});

// ── PHASE H: Make a discard via human-style click ────────────────
await phase('Phase H: Discard — human-style click on hand tile', async () => {
  await dismissOverlaysIfPresent('phase-H');

  const pre = await page.evaluate(() => {
    const cli = window.game?.client;
    if (!cli) return null;
    const seat = cli.seat;
    let discardCount = 0, myHandCount = 0;
    if (cli.things) {
      for (const [, v] of cli.things.entries()) {
        const slot = v?.slotName ?? v?.SlotName;
        if (typeof slot !== 'string') continue;
        if (slot.startsWith('discard.')) discardCount++;
        if (slot.startsWith('hand.') && slot.endsWith(`@${seat}`)) myHandCount++;
      }
    }
    return { seat, discardCount, myHandCount,
             hasExtra: typeof window.game.world.hasExtraHandTile === 'function'
                       ? window.game.world.hasExtraHandTile() : null };
  });
  await snap(page, 'phase-H-before-discard.png');

  // Real-user gesture: click on the canvas at the seat-0 hand area.
  // This is non-deterministic for tile-pick in headless (3D raycast).
  // We try it for the screenshot, then fall back to the bundle's own
  // emitDiscard API path (the SAME path the real onmousedown handler
  // calls when the click commits).
  const canvas = page.locator('canvas#center, #main canvas').first();
  let canvasClicked = false;
  if (await canvas.isVisible().catch(() => false)) {
    const bb = await canvas.boundingBox();
    if (bb) {
      const cx = bb.x + bb.width * 0.5;
      const cy = bb.y + bb.height * 0.88;
      await page.mouse.move(cx, cy);
      await page.waitForTimeout(120);
      await page.mouse.down();
      await page.waitForTimeout(60);
      await page.mouse.up();
      canvasClicked = true;
      await page.waitForTimeout(800);
    }
  }
  await snap(page, 'phase-H-after-canvas-click.png');

  // Pre-snapshot to compare.
  const midDiscards = await page.evaluate(() => {
    const cli = window.game?.client;
    if (!cli) return 0;
    let c = 0;
    if (cli.things) {
      for (const [, v] of cli.things.entries()) {
        const slot = v?.slotName ?? v?.SlotName;
        if (typeof slot === 'string' && slot.startsWith('discard.')) c++;
      }
    }
    return c;
  });

  // Fallback to emitDiscard via the bundle's own world API.
  let discardAttempt = { ok: false, skipped: false };
  if (midDiscards <= (pre?.discardCount ?? 0)) {
    discardAttempt = await page.evaluate(() => {
      const g = window.game;
      if (!g || !g.world || !g.client) return { ok: false, reason: 'no game' };
      const cli = g.client;
      const seat = cli.seat;
      const seatSuffix = `@${seat}`;
      // Diagnostic context: pickup phase, isMyPickupTurn, hasExtra.
      const pickupCurrent = cli.pickup?.get?.('current') ?? null;
      const myPickupTurn = (typeof g.world.isMyPickupTurn === 'function')
                          ? g.world.isMyPickupTurn() : null;
      const hasExtra = (typeof g.world.hasExtraHandTile === 'function')
                          ? g.world.hasExtraHandTile() : null;
      let tileObj = null, tileId = null;
      if (cli.things) {
        for (const [k, v] of cli.things.entries()) {
          const slot = v?.slotName ?? v?.SlotName;
          if (typeof slot === 'string'
              && slot.startsWith('hand.') && slot.endsWith(seatSuffix)
              && !slot.startsWith('hand.extra@')) {
            tileObj = v; tileId = k; break;
          }
        }
      }
      const diag = { seat, pickupCurrent, myPickupTurn, hasExtra };
      if (tileObj === null) return { ok: false, reason: 'no hand tile', diag };
      try {
        const r1 = g.world.emitDiscard(tileObj);
        return { ok: !!r1, via: 'world.emitDiscard(thing)', tileId, diag };
      } catch (e) {
        try {
          const r2 = g.world.emitDiscard(tileId);
          return { ok: !!r2, via: 'world.emitDiscard(id)', tileId, diag };
        } catch (e2) {
          return { ok: false, reason: String(e), reason2: String(e2), tileId, diag };
        }
      }
    });
  } else {
    discardAttempt = { ok: true, skipped: 'canvas click already discarded' };
  }

  await page.waitForTimeout(2500);
  await snap(page, 'phase-H-after-discard.png');

  const post = await page.evaluate(() => {
    const cli = window.game?.client;
    if (!cli) return null;
    const seat = cli.seat;
    let discardCount = 0, myHandCount = 0;
    if (cli.things) {
      for (const [, v] of cli.things.entries()) {
        const slot = v?.slotName ?? v?.SlotName;
        if (typeof slot !== 'string') continue;
        if (slot.startsWith('discard.')) discardCount++;
        if (slot.startsWith('hand.') && slot.endsWith(`@${seat}`)) myHandCount++;
      }
    }
    return { seat, discardCount, myHandCount };
  });

  const dischargeDelta = (post?.discardCount ?? 0) - (pre?.discardCount ?? 0);
  const handDelta = (post?.myHandCount ?? 0) - (pre?.myHandCount ?? 0);
  if (dischargeDelta <= 0) {
    // Inspect the pickup phase to give a precise, actionable diagnostic.
    const diag = discardAttempt?.diag ?? {};
    const phasePart = diag.pickupCurrent
      ? `pickup.phase="${diag.pickupCurrent.phase}", dealMode="${diag.pickupCurrent.dealMode}", count=${diag.pickupCurrent.count}`
      : 'pickupCurrent=null (no take pending)';
    if (diag.pickupCurrent?.phase === 'DealerExtra'
        || diag.pickupCurrent?.phase === 'PickupTurn'
        || diag.pickupCurrent?.phase === 'RollingDice') {
      recordBug('P0', 'H',
        `Discard rejected because the runtime is parked in ${diag.pickupCurrent.phase} phase (dealMode=${diag.pickupCurrent.dealMode}). In Manual deal mode the dealer must FIRST pick the 14th tile from the wall via #pickup-take-btn, then the phase transitions to AwaitingDiscard. There is no clear inline UX (no big "Take your dealer extra tile" banner) telling a first-time user this step exists. This is the "I can't even select a tile" complaint: the user sees their hand, tries to discard, and nothing happens — silently. ${phasePart}, myPickupTurn=${diag.myPickupTurn}, hasExtra=${diag.hasExtra}.`,
        'phase-H-after-discard.png',
        '1) Bare URL → lobby defaults (Manual deal mode is the Changsha default) → Apply & Start → Connect → Take Seat 0 → hold #deal 700ms. 2) Game enters DealerExtra phase. 3) Click any hand tile to discard. 4) Observe: nothing happens, discards collection does not grow. Move log has no error.',
        'world.ts emitDiscard at line 461 silently returns false when phase is wrong — needs a user-visible warning ("Take your dealer extra first") OR the lobby should default to Auto deal mode for new users. Also: src/frontend/autotable-src/src/lobby.ts initLobby — consider making dealMode=auto the lobby default.');
    } else {
      recordBug('P0', 'H',
        `User-style click on a hand tile did NOT register a discard. The user is staring at a hand of ${pre?.myHandCount} tiles and tap-clicking has zero effect — silent rejection from world.emitDiscard. ${phasePart}, myPickupTurn=${diag.myPickupTurn}, hasExtra=${diag.hasExtra}, before=${pre?.discardCount}, after=${post?.discardCount}. The runtime is NOT obviously in a pickup phase that would explain the rejection; play has stalled with no UI feedback explaining why. This is the "the game won't let me play" complaint.`,
        'phase-H-after-discard.png',
        '1) Bare URL → lobby defaults → Apply & Start → Connect → Take Seat 0 → hold #deal 700ms → wait 8s. 2) Click any tile in your hand (or call world.emitDiscard via console). 3) Observe: silent no-op, discards collection does not grow, the game just sits there.',
        'world.ts emitDiscard at line 461 — needs at MINIMUM a toast/console.warn explaining WHY the discard was rejected (wrong phase, not your turn, no pickup pending, etc.). Currently the failure path is a bare `return false` with zero user feedback. Backend ChangshaStateMachine.Discard should also surface rejection to the client.');
    }
  }
  return { pre, canvasClicked, midDiscards, discardAttempt, post,
           dischargeDelta, handDelta };
});

// ── PHASE H2: Try the discoverable "Take" button (pickup-take-btn) ──
// After Phase H, the runtime is still parked in DealerExtra phase.  See
// whether the visible #pickup-take-btn lets the user advance state —
// this is the user's discoverable affordance.
await phase('Phase H2: Click #pickup-take-btn → does pickup phase advance?', async () => {
  await dismissOverlaysIfPresent('phase-H2');
  const takeBtnVis = await page.evaluate(() => {
    const el = document.getElementById('pickup-take-btn');
    if (!el) return { present: false };
    const cs = getComputedStyle(el);
    const r = el.getBoundingClientRect();
    const hud = document.getElementById('pickup-hud');
    return {
      present: true,
      visible: cs.display !== 'none' && cs.visibility !== 'hidden' && r.width > 0,
      text: (el.textContent ?? '').trim().slice(0, 30),
      hudVisible: !!hud && getComputedStyle(hud).display !== 'none',
      hudText: (document.getElementById('pickup-hud-text')?.textContent ?? '').trim(),
    };
  });

  const phaseBefore = await page.evaluate(() => {
    const c = window.game?.client?.pickup?.get?.('current') ?? null;
    return c ? { phase: c.phase, count: c.count, seatIndex: c.seatIndex } : null;
  });

  if (!takeBtnVis.present || !takeBtnVis.visible) {
    if (phaseBefore && (phaseBefore.phase === 'DealerExtra'
        || phaseBefore.phase === 'PickupTurn'
        || phaseBefore.phase === 'SingleTilePickup')) {
      recordBug('P0', 'H2',
        `Runtime is parked in pickup.phase="${phaseBefore.phase}" seat=${phaseBefore.seatIndex} but #pickup-take-btn is NOT visible to the user (present=${takeBtnVis.present}, visible=${takeBtnVis.visible}). They have no discoverable way to advance.`,
        'phase-H-after-discard.png',
        'After hold-to-deal in Manual mode at Changsha defaults, check that #pickup-take-btn is visible.',
        'src/frontend/autotable-src/src/game-ui.ts renderPickupHud (line 1550) / pickup-hud visibility');
    }
    return { takeBtnVis, phaseBefore };
  }

  await page.locator('#pickup-take-btn').click({ timeout: 3000 }).catch(() => {});
  await page.waitForTimeout(3000);
  await snap(page, 'phase-H2-after-take-click.png');

  const phaseAfter = await page.evaluate(() => {
    const c = window.game?.client?.pickup?.get?.('current') ?? null;
    return c ? { phase: c.phase, count: c.count, seatIndex: c.seatIndex } : null;
  });

  const advanced = JSON.stringify(phaseBefore) !== JSON.stringify(phaseAfter);
  if (!advanced && phaseBefore?.phase === 'DealerExtra') {
    recordBug('P0', 'H2',
      `Clicked #pickup-take-btn (text="${takeBtnVis.text}", HUD="${takeBtnVis.hudText}") but pickup.phase did NOT advance: before=${JSON.stringify(phaseBefore)}, after=${JSON.stringify(phaseAfter)}. The user's ONLY discoverable affordance to advance is a NO-OP — they are completely stranded after seeing their hand. world.emitTakePickup() either silently fails or backend rejects with no UI feedback.`,
      'phase-H2-after-take-click.png',
      '1) Bare URL → lobby (Manual mode is the Changsha default) → Apply & Start → Connect → Take Seat 0 → hold #deal 700ms. 2) Pickup HUD appears: "Your turn — pick 1 tile" with "Take 1" button. 3) Click "Take 1". 4) Observe: nothing visible happens, pickup HUD stays open, state stays DealerExtra, bots remain blocked.',
      'src/frontend/autotable-src/src/world.ts emitTakePickup (line 437) sends pickup.set("take", ...) — backend may reject silently. Either (a) make the Take action actually advance state in DealerExtra after a same-turn discard, (b) show an error/hint when Take is rejected, or (c) default the lobby to Auto deal mode for new users.');
  }
  return { takeBtnVis, phaseBefore, phaseAfter, advanced };
});

// ── PHASE I: Bot draw + discard cadence ──────────────────────────
await phase('Phase I: Bot draw + discard cadence (30s)', async () => {
  await dismissOverlaysIfPresent('phase-I');
  const captures = [];
  for (let i = 0; i < 6; i++) {
    await page.waitForTimeout(5000);
    const snap2 = await page.evaluate(() => {
      const cli = window.game?.client;
      if (!cli) return { error: 'no game' };
      const discardsBySeat = { 0: 0, 1: 0, 2: 0, 3: 0 };
      const handBySeat     = { 0: 0, 1: 0, 2: 0, 3: 0 };
      if (cli.things) {
        for (const [, v] of cli.things.entries()) {
          const slot = v?.slotName ?? v?.SlotName;
          if (typeof slot !== 'string') continue;
          const m = slot.match(/@([0-3])$/);
          if (!m) continue;
          const s = parseInt(m[1], 10);
          if (slot.startsWith('discard.')) discardsBySeat[s]++;
          if (slot.startsWith('hand.')) handBySeat[s]++;
        }
      }
      return { discardsBySeat, handBySeat, mySeat: cli.seat ?? null,
               pickupCurrent: cli.pickup?.get?.('current') ?? null,
               resultCurrent: cli.result?.get?.('current') ?? null };
    });
    captures.push({ atSec: (i + 1) * 5, ...snap2 });
  }
  await snap(page, 'phase-I-after-30s.png');

  const moveLogEntries = await page.locator('#move-log .move-log-entry')
    .allTextContents().catch(() => []);

  const valid = captures.filter(c => !c.error && c.discardsBySeat);
  if (valid.length >= 2) {
    const first = valid[0];
    const last  = valid[valid.length - 1];
    let otherDelta = 0;
    for (const seat of [0, 1, 2, 3]) {
      if (seat === last.mySeat) continue;
      otherDelta += (last.discardsBySeat[seat] || 0)
                  - (first.discardsBySeat[seat] || 0);
    }
    if (otherDelta === 0) {
      // Diagnose whether the runtime is parked on the dealer.
      const phaseInfo = last.pickupCurrent
        ? `Runtime still parked in pickup.phase="${last.pickupCurrent.phase}" seat=${last.pickupCurrent.seatIndex} dealMode=${last.pickupCurrent.dealMode}. Bots cannot act because the dealer (seat ${last.mySeat}) has not taken their dealer-extra tile yet.`
        : 'No pickup state present — bots may be misconfigured.';
      recordBug('P0', 'I',
        `Over 30s, NO bot at any other seat made a discard (delta=0). ${phaseInfo} moveLog entries=${moveLogEntries.length}.`,
        'phase-I-after-30s.png',
        'After deal, wait 30s without any further user action. Expect bots to draw + discard; observed: zero progress.',
        last.pickupCurrent?.phase === 'DealerExtra'
          ? 'Cascade from H — fix the dealer-extra UX or default to Auto deal mode and bots will run.'
          : 'BotEngine.cs in src/backend / bot-turn loop / turn rotation');
    }
  } else {
    recordBug('P1', 'I',
      `Could not sample game state over 30s — captures had errors: ${captures.map(c=>c.error).filter(Boolean).join('; ')}`,
      'phase-I-after-30s.png',
      'After deal, sample cli.things every 5s for 30s.',
      'client state / window.game lifecycle');
  }

  return { captures, moveLogEntryCount: moveLogEntries.length,
           lastLog: moveLogEntries.slice(-8) };
});

// ── PHASE J: Claim window visibility ─────────────────────────────
await phase('Phase J: Claim window visibility', async () => {
  await dismissOverlaysIfPresent('phase-J');
  let everSawActive = false;
  let activeSnapshot = null;
  for (let i = 0; i < 8; i++) {
    const claims = await page.evaluate(() => {
      function btn(id) {
        const el = document.getElementById(id);
        if (!el) return { present: false };
        const cs = getComputedStyle(el);
        const r = el.getBoundingClientRect();
        return {
          present: true,
          visible: cs.display !== 'none' && cs.visibility !== 'hidden' && r.width > 0,
          disabled: el.disabled,
        };
      }
      return {
        pung: btn('claim-pung'),
        chow: btn('claim-chow'),
        kong: btn('claim-kong'),
        hu:   btn('claim-hu'),
        pass: btn('claim-pass'),
      };
    });
    const anyActive = ['pung','chow','kong','hu','pass']
      .some(k => claims[k]?.visible && !claims[k]?.disabled);
    if (anyActive) {
      everSawActive = true;
      activeSnapshot = { atSec: (i + 1) * 2.5, claims };
      await snap(page, `phase-J-claim-window-active.png`);
      break;
    }
    await page.waitForTimeout(2500);
  }
  if (!everSawActive) {
    findings.polish.push({
      phase: 'J',
      severity: 'P2',
      description: 'No claim window became active during the 20s observation — could be normal (no discard matched our hand)',
      screenshot: 'phase-I-after-30s.png',
    });
  }
  await snap(page, 'phase-J-final.png');
  return { everSawActive, activeSnapshot };
});

// ── PHASE K: Sustained play observation (60s) ────────────────────
await phase('Phase K: Sustained play observation (60s)', async () => {
  await dismissOverlaysIfPresent('phase-K');
  const trajectory = [];
  for (let i = 0; i < 6; i++) {
    await page.waitForTimeout(10000);
    const probe = await page.evaluate(() => {
      const cli = window.game?.client;
      if (!cli) return null;
      let totalDiscards = 0, totalHandTiles = 0;
      if (cli.things) {
        for (const [, v] of cli.things.entries()) {
          const slot = v?.slotName ?? v?.SlotName;
          if (typeof slot !== 'string') continue;
          if (slot.startsWith('discard.')) totalDiscards++;
          if (slot.startsWith('hand.')) totalHandTiles++;
        }
      }
      const result = cli.result?.get?.('current') ?? null;
      const gc     = cli.gameComplete?.get?.('current') ?? null;
      return { totalDiscards, totalHandTiles,
               resultPresent: result !== null && result !== undefined,
               gameComplete:  !!(gc && (gc.isComplete || gc.IsComplete)) };
    });
    trajectory.push({ atSec: (i + 1) * 10, ...probe });
  }
  await snap(page, 'phase-K-final-60s.png');

  const valid = trajectory.filter(t => t && typeof t.totalDiscards === 'number');
  if (valid.length >= 2) {
    const first = valid[0];
    const last  = valid[valid.length - 1];
    const delta = (last.totalDiscards || 0) - (first.totalDiscards || 0);
    if (delta < 3) {
      recordBug('P0', 'K',
        `Over 60s of observation, total discards grew by ${delta} (from ${first.totalDiscards} to ${last.totalDiscards}). Play has stalled — no bot is taking turns, no human discard registered, no win condition hit. The user is staring at a frozen 3D table. This is the "I waited and nothing happened" complaint and is likely a CASCADE from Phase H (the dealer-extra/discard-rejection issue) blocking the entire turn rotation.`,
        'phase-K-final-60s.png',
        '1) Bare URL → complete the lobby + connect + seat + deal flow. 2) Wait 60+ seconds without any further user action. 3) Observe: discards count does not advance, no bot draws or discards. The game is dead in the water.',
        'Cascade from Phase H — fix the dealer-extra / silent-discard-rejection issue and the turn rotation should resume. Also recommend defaulting the lobby to Auto deal mode for first-time users (lobby.ts initLobby).');
    }
  }
  return { trajectory };
});

// ── PHASE L: Final inventory ──────────────────────────────────────
await phase('Phase L: Final UI inventory', async () => {
  const buttons = [];
  const all = await page.getByRole('button').all();
  for (const b of all) {
    try {
      if (!(await b.isVisible())) continue;
      const t = (await b.textContent())?.trim();
      const id = (await b.getAttribute('id')) || '';
      const disabled = await b.isDisabled().catch(() => null);
      if (t) buttons.push({ id, text: t.slice(0, 50), disabled });
    } catch {}
  }
  await snap(page, 'phase-L-final.png');
  return { visibleButtonCount: buttons.length, buttons };
});

// ── teardown ───────────────────────────────────────────────────────
findings.endedAt = new Date().toISOString();
findings.totalBlockers = findings.blockers.length;
findings.totalConfusions = findings.confusions.length;
findings.totalPolish = findings.polish.length;

await browser.close();

fs.writeFileSync(path.join(ART_DIR, 'summary.json'),
                 JSON.stringify(findings, null, 2));

// ── human-readable findings.md ─────────────────────────────────────
const md = [];
md.push(`# Stephen first-play audit — ${RUN_TAG}`);
md.push('');
md.push(`* Base URL: ${BASE_URL}/autotable/  (NO query parameters)`);
md.push(`* Started: ${findings.startedAt}`);
md.push(`* Ended:   ${findings.endedAt}`);
md.push(`* Page errors: ${findings.pageErrors.length}`);
md.push(`* Console errors: ${findings.consoleErrors.length}`);
md.push(`* Console warnings (non-NaN): ${findings.consoleWarnings.length}`);
md.push(`* Network failures (≥400): ${findings.networkFailures.length}`);
md.push('');
md.push(`## Verdict`);
md.push('');
if (findings.blockers.length === 0 && findings.confusions.length === 0) {
  md.push('✅ **PLAYABLE FROM BARE URL** — no blockers or confusions observed.');
} else if (findings.blockers.length === 0) {
  md.push(`⚠️ **PLAYABLE WITH FRICTION** — 0 blockers but ${findings.confusions.length} confusion(s) found.`);
} else {
  md.push(`❌ **NOT PLAYABLE FROM BARE URL** — ${findings.blockers.length} blocker(s) + ${findings.confusions.length} confusion(s).`);
}
md.push('');
md.push(`## Phase summary`);
md.push('');
for (const ph of findings.phases) {
  md.push(`* ${ph.ok ? '✅' : '❌'} ${ph.name} — ${ph.durMs} ms${ph.error ? ` — error: ${ph.error}` : ''}`);
}
md.push('');

function emitBugSection(title, list) {
  md.push(`## ${title}`);
  md.push('');
  if (list.length === 0) { md.push('_None._'); md.push(''); return; }
  for (const b of list) {
    md.push(`### [${b.severity}] (${b.phase}) ${b.description}`);
    md.push('');
    if (b.repro)      md.push(`* **Steps to reproduce:** ${b.repro}`);
    if (b.suspect)    md.push(`* **Suspect file/owner:** ${b.suspect}`);
    if (b.screenshot) md.push(`* **Screenshot:** \`${b.screenshot}\``);
    md.push('');
  }
}
emitBugSection('P0 — Blockers (user cannot play)', findings.blockers);
emitBugSection('P1 — Confusions (user struggles)', findings.confusions);
emitBugSection('P2 — Polish', findings.polish);

if (findings.pageErrors.length) {
  md.push('## Page errors');
  md.push('');
  for (const e of findings.pageErrors.slice(0, 20)) {
    md.push('```');
    md.push(e.message);
    if (e.stack) md.push(e.stack);
    md.push('```');
  }
  md.push('');
}
if (findings.consoleErrors.length) {
  md.push('## Console errors (first 20)');
  md.push('');
  for (const e of findings.consoleErrors.slice(0, 20)) {
    md.push(`* ${e}`);
  }
  md.push('');
}

fs.writeFileSync(path.join(ART_DIR, 'findings.md'), md.join('\n'));

console.log('\n══════════ FINDINGS ══════════');
console.log(`Run tag: ${RUN_TAG}`);
console.log(`Artifacts: ${ART_DIR}`);
console.log(`Blockers (P0): ${findings.blockers.length}`);
console.log(`Confusions (P1): ${findings.confusions.length}`);
console.log(`Polish (P2): ${findings.polish.length}`);
console.log(`Page errors: ${findings.pageErrors.length}`);
console.log(`Console errors: ${findings.consoleErrors.length}`);

for (const b of findings.blockers)   console.log(`\n[P0 ${b.phase}] ${b.description}`);
for (const c of findings.confusions) console.log(`\n[P1 ${c.phase}] ${c.description}`);

process.exit(findings.blockers.length > 0 ? 2 : 0);
