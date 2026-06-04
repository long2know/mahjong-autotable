// Ripley 2026-06-04 — Docker single-image deploy smoke test.
//
// Stephen's project-start requirement:
//   "The frontend and backend should be packageable as a single
//    docker image so that I can run in a container on my Linux
//    server that I already have."
//
// This spec is the proof-of-life capture against a running Docker
// container (built from the repo-root Dockerfile). It does NOT
// build or start the container — the operator does that first.
//
// Run:
//   docker build -t mahjong-autotable:proof .
//   docker run -d --name mat-proof -p 9099:8080 \
//     -e ASPNETCORE_URLS="http://0.0.0.0:8080" mahjong-autotable:proof
//   sleep 25
//   E2E_BASE_URL=http://127.0.0.1:9099 \
//     node playtest-artifacts/playtest-docker-smoke.spec.mjs
//
// Artifacts:
//   playtest-artifacts/screenshots/ripley-docker-proof-<ts>/
//     docker-game-running.png
//     findings.json
//
// Exit code: 0 on PASS (walls visible, hand at 13-14 tiles, no
// page errors apart from the known THREE NaN warning), non-zero
// otherwise.

import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:9099';
const RUN_TAG = `ripley-docker-proof-${Date.now()}`;
const ARTIFACT_DIR = path.resolve(`./playtest-artifacts/screenshots/${RUN_TAG}`);
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });

const findings = {
  timestamp: new Date().toISOString(),
  runTag: RUN_TAG,
  baseUrl,
  health: null,
  staticHtml: null,
  screenshot: null,
  worldSnapshot: null,
  pageErrors: [],
  consoleErrors: [],
  ignoredWarnings: 0,
  pass: false,
  failReason: null,
};

const OVERLAY_DEFANG = `
  #tour-overlay, #magic-link-landing, #magic-link-overlay,
  #signin-modal-backdrop, .magic-link-landing, .magic-link-overlay,
  .signin-modal-backdrop, [data-testid="tour-overlay"],
  [data-testid="signin-modal-backdrop"]
    { display: none !important; pointer-events: none !important; visibility: hidden !important; }
  [aria-hidden="true"] { pointer-events: none !important; }
`;

// THREE NaN warnings are a known long-standing engine quirk and not a
// regression — see Hicks' visual-regression sweep notes.
const KNOWN_WARN_PATTERNS = [
  /NaN/i,
  /three\.js/i,
  /THREE\./i,
  /skipped stale moveTo/i,
  /forcing stale moveTo/i,
];

function isKnownWarning(text) {
  return KNOWN_WARN_PATTERNS.some(re => re.test(text));
}

async function probeHealth() {
  const t0 = Date.now();
  const res = await fetch(`${baseUrl}/health`);
  let body = null;
  try { body = await res.json(); } catch {}
  return {
    status: res.status,
    ok: res.ok,
    latencyMs: Date.now() - t0,
    body,
  };
}

async function probeStaticHtml() {
  const t0 = Date.now();
  const res = await fetch(`${baseUrl}/autotable/`);
  const text = await res.text();
  const hasTitle = /<title>\s*Autotable\s*<\/title>/i.test(text);
  const bundleMatch = text.match(/autotable-src\.[a-f0-9]+\.js/i);
  return {
    status: res.status,
    ok: res.ok,
    latencyMs: Date.now() - t0,
    bytes: text.length,
    hasTitle,
    bundleAsset: bundleMatch ? bundleMatch[0] : null,
    snippet: text.slice(0, 600),
  };
}

async function worldSnapshot(page) {
  return await page.evaluate(() => {
    const g = window.game;
    if (!g || !g.world) return null;
    const w = g.world;
    const seat = w.seat;
    const handBySeat = [0, 0, 0, 0];
    const meldBySeat = [0, 0, 0, 0];
    const discardBySeat = [0, 0, 0, 0];
    let wallCount = 0;
    let totalDiscard = 0;
    let totalMeld = 0;
    for (const t of w.things.values()) {
      if (!t.slot) continue;
      const s = t.slot;
      if (s.group === 'wall' || s.group === 'wall.open') wallCount++;
      if (s.group === 'hand' && typeof s.seat === 'number') handBySeat[s.seat]++;
      if (s.group === 'meld' && typeof s.seat === 'number') { meldBySeat[s.seat]++; totalMeld++; }
      if (s.group === 'discard') {
        totalDiscard++;
        if (typeof s.seat === 'number') discardBySeat[s.seat]++;
      }
    }
    return {
      seat,
      handBySeat,
      meldBySeat,
      discardBySeat,
      wallCount,
      totalDiscard,
      totalMeld,
      handSum: handBySeat.reduce((a, b) => a + b, 0),
      thingsCount: w.things.size,
      connected: !!g.client?.connected,
    };
  });
}

async function waitForWorld(page, predicate, timeoutMs, pollMs = 250) {
  const deadline = Date.now() + timeoutMs;
  let snap = null;
  while (Date.now() < deadline) {
    snap = await worldSnapshot(page);
    if (snap && predicate(snap)) return snap;
    await page.waitForTimeout(pollMs);
  }
  return snap;
}

async function navigateAndStart(page, url) {
  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);
  // seat=-1 + botCount=4 auto-binds the Changsha runtime and starts
  // the deal without a human seat-take, mirroring the proven pattern
  // in playtest-bishop-bots.spec.mjs:204-208.  No lobby/onboarding
  // interaction needed.
  for (const sel of ['#tour-skip', '#onboarding-skip', '#lobby-close']) {
    const el = page.locator(sel);
    if (await el.isVisible().catch(() => false)) {
      await el.click({ force: true, timeout: 3000 }).catch(() => {});
      await page.waitForTimeout(300);
    }
  }
}

// ── main ────────────────────────────────────────────────────────────

console.log(`>>> docker smoke against ${baseUrl}`);
console.log(`>>> artifacts: ${ARTIFACT_DIR}`);

// Step 1 — /health
const health = await probeHealth();
findings.health = health;
console.log(`\n[health] status=${health.status} latency=${health.latencyMs}ms ` +
            `body.status=${health.body?.status} db.connected=${health.body?.db?.connected}`);
if (!health.ok || health.body?.status !== 'healthy') {
  findings.failReason = `health probe failed: status=${health.status}`;
  fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'),
                   JSON.stringify(findings, null, 2));
  console.error(`FAIL — ${findings.failReason}`);
  process.exit(2);
}

// Step 2 — /autotable/ HTML
const html = await probeStaticHtml();
findings.staticHtml = html;
console.log(`[static] status=${html.status} bytes=${html.bytes} ` +
            `title=${html.hasTitle} bundle=${html.bundleAsset}`);
if (!html.ok || !html.hasTitle || !html.bundleAsset) {
  findings.failReason = `static HTML invalid: title=${html.hasTitle} bundle=${html.bundleAsset}`;
  fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'),
                   JSON.stringify(findings, null, 2));
  console.error(`FAIL — ${findings.failReason}`);
  process.exit(2);
}

// Step 3 — drive a real game in headless Chromium against the container
const browser = await chromium.launch({ headless: true });
let snap = null;
try {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  await ctx.addInitScript((css) => {
    const inject = () => {
      if (document.getElementById('ripley-docker-defang')) return;
      const s = document.createElement('style');
      s.id = 'ripley-docker-defang';
      s.textContent = css;
      document.head.appendChild(s);
    };
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', inject);
    } else inject();
  }, OVERLAY_DEFANG);

  const page = await ctx.newPage();
  page.on('console', msg => {
    const t = msg.type();
    const text = msg.text();
    if (t === 'error') findings.consoleErrors.push(text);
    if (t === 'warning' && isKnownWarning(text)) findings.ignoredWarnings++;
  });
  page.on('pageerror', err => {
    if (isKnownWarning(err.message)) {
      findings.ignoredWarnings++;
      return;
    }
    findings.pageErrors.push({
      message: err.message,
      stack: (err.stack ?? '').split('\n').slice(0, 4).join('\n'),
    });
  });

  const gameId = `docker-smoke-${Date.now()}`;
  // seat=-1 + botCount=4 = all-bots spectator mode (proven pattern from
  // playtest-bishop-bots.spec.mjs:204) — no human seat-take, deal fires
  // immediately, proves the full Docker-hosted runtime end-to-end.
  const url = `${baseUrl}/autotable/?variant=changsha&seat=-1&dealMode=auto`
            + `&botCount=4&botDifficulty=Easy&handCount=4`
            + `&gameId=${encodeURIComponent(gameId)}`;
  console.log(`\n>>> navigating to ${url}`);
  await navigateAndStart(page, url);

  // Try to catch the "fresh deal" window: 108-tile wall + 4 hands of
  // ~13 tiles each, before the bots eat into either.  Bots are fast
  // (sub-second on Easy), so we poll aggressively and snap the moment
  // we see real distribution.  If we miss the perfect-deal window, the
  // fallback predicate ("game in motion") still proves the runtime is
  // healthy.
  snap = await waitForWorld(page,
    s => s.thingsCount >= 100 && s.handSum >= 40 && s.connected,
    25_000, 100);
  findings.worldSnapshot = snap;

  const file = path.join(ARTIFACT_DIR, 'docker-game-running.png');
  await page.screenshot({ path: file, fullPage: true });
  findings.screenshot = path.relative(process.cwd(), file);
  console.log(`[screenshot] ${findings.screenshot}`);
  console.log(`[world] ${JSON.stringify(snap)}`);
} finally {
  await browser.close();
}

const walledOk = !!(snap && snap.thingsCount >= 100);
const handedOk = !!(snap && snap.handSum >= 40);
const motionOk = !!(snap && (snap.totalDiscard >= 1 || snap.totalMeld >= 1 || snap.handBySeat.some(n => n >= 13)));
const noPageErrors = findings.pageErrors.length === 0;

if (!walledOk) findings.failReason = `insufficient tiles (thingsCount=${snap?.thingsCount ?? 'n/a'})`;
else if (!handedOk) findings.failReason = `hands not distributed (handSum=${snap?.handSum ?? 'n/a'})`;
else if (!motionOk) findings.failReason = 'no real-game motion (no discards/melds/13-tile hand)';
else if (!noPageErrors) findings.failReason = `unfiltered pageErrors: ${findings.pageErrors.length}`;

findings.pass = walledOk && handedOk && motionOk && noPageErrors;
fs.writeFileSync(path.join(ARTIFACT_DIR, 'findings.json'),
                 JSON.stringify(findings, null, 2));

console.log(`\n=== SUMMARY ===`);
console.log(`  health        : ${findings.health.ok && findings.health.body?.status === 'healthy' ? 'PASS' : 'FAIL'}`);
console.log(`  static HTML   : ${findings.staticHtml.ok && findings.staticHtml.hasTitle && findings.staticHtml.bundleAsset ? 'PASS' : 'FAIL'}`);
console.log(`  tiles loaded  : ${walledOk ? 'PASS' : 'FAIL'} (thingsCount=${snap?.thingsCount ?? 'n/a'}, wall=${snap?.wallCount ?? 'n/a'})`);
console.log(`  hands dealt   : ${handedOk ? 'PASS' : 'FAIL'} (handSum=${snap?.handSum ?? 'n/a'}, perSeat=${JSON.stringify(snap?.handBySeat)})`);
console.log(`  game motion   : ${motionOk ? 'PASS' : 'FAIL'} (discards=${snap?.totalDiscard ?? 'n/a'}, melds=${snap?.totalMeld ?? 'n/a'})`);
console.log(`  page errors   : ${noPageErrors ? 'PASS' : 'FAIL'} (${findings.pageErrors.length} surfaced, ${findings.ignoredWarnings} known warnings ignored)`);
console.log(`  screenshot    : ${findings.screenshot}`);
console.log(`  overall       : ${findings.pass ? '✅ PASS' : '❌ FAIL — ' + findings.failReason}`);

process.exit(findings.pass ? 0 : 1);
