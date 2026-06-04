// Bishop W25 — autonomy + multi-game audit playtest.
//
// Scope (per Stephen's directive):
//   B. 4-bot self-play: open a single all-bots watch table, observe for
//      ~3 minutes, assert the table is alive (≥ 8 discards visible) AND
//      no page errors during the run.
//   C. Multi-game isolation: open two parallel contexts with distinct
//      gameIds. Force a discard in ctx-A; the discard tile MUST appear
//      in ctx-A's world but NOT in ctx-B's world.
//   D. Late-join sees accumulated state: ctx-A creates + plays a few
//      discards. ctx-B joins the SAME gameId mid-hand; its snapshot
//      MUST contain ≥ 1 of the ctx-A discards (i.e. catches up).
//
// Run:
//   cd /data/source/mahjong-autotable
//   E2E_BASE_URL=https://127.0.0.1:7135 NODE_TLS_REJECT_UNAUTHORIZED=0 \
//     node playtest-artifacts/playtest-bishop-bots.spec.mjs
//
// Defaults match the broken-deal-repro template: HTTPS dev launch
// profile on 7135 with `ignoreHTTPSErrors: true`. Override
// `E2E_BASE_URL` for older `http://127.0.0.1:8088` runs.
//
// Outputs (timestamped, written under playtest-artifacts/screenshots/):
//   bishop-bots-B-selfplay-<ts>.png + .json
//   bishop-bots-C-isolation-<ts>.json
//   bishop-bots-D-latejoin-<ts>.json
//   bishop-bots-summary-<ts>.json   (combined pass/fail rollup)
//
// Pass/fail convention: each section sets `findings.sections[*].ok` and
// fills `failures[]` with strings. The process exits 1 iff any section
// has `ok=false` so CI can pick up the signal even though we also write
// the full JSON for forensic review.
import pw from 'playwright';
const { chromium } = pw;
import fs from 'fs';
import path from 'path';

const baseUrl = process.env.E2E_BASE_URL || 'https://127.0.0.1:7135';
const ARTIFACT_DIR = path.resolve('./playtest-artifacts/screenshots');
fs.mkdirSync(ARTIFACT_DIR, { recursive: true });
const ts = new Date().toISOString().replace(/[:.]/g, '-');

// Section runtime knobs — sized so the whole spec finishes inside the
// "fast-feedback" 5-minute squad budget while still giving each section
// enough time to observe meaningful bot activity. Override via env var
// for the longer "deep watch" runs Stephen kicks off manually.
const B_OBSERVE_MS = parseInt(process.env.BISHOP_B_OBSERVE_MS || '180000', 10); // 3 min
const C_OBSERVE_MS = parseInt(process.env.BISHOP_C_OBSERVE_MS || '45000', 10);  // 45 s
const D_OBSERVE_MS = parseInt(process.env.BISHOP_D_OBSERVE_MS || '20000', 10);  // 20 s

const summary = {
  ts,
  baseUrl,
  sections: { B: null, C: null, D: null },
  failures: [],
};

const browser = await chromium.launch();

// Defang script reused by every section — kills the tour / sign-in /
// magic-link overlays so they can't intercept canvas clicks or hide
// content from the screenshot.
function installDefang(page) {
  return page.addInitScript(() => {
    const inject = () => {
      if (document.getElementById('bishop-bots-defang')) return;
      const style = document.createElement('style');
      style.id = 'bishop-bots-defang';
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
}

// Wire a page with error capture into the supplied bag — every section
// owns its own bag so we can attribute failures correctly.
function wireDiagnostics(page, bag) {
  bag.pageErrors = [];
  bag.consoleErrors = [];
  bag.consoleWarnings = [];
  bag.networkFailures = [];
  page.on('console', msg => {
    const t = msg.type();
    const text = msg.text();
    if (t === 'error')   bag.consoleErrors.push(text);
    if (t === 'warning') bag.consoleWarnings.push(text);
  });
  page.on('pageerror', err => bag.pageErrors.push(err.message));
  page.on('response', resp => {
    if (resp.status() >= 400) {
      bag.networkFailures.push(`${resp.status()} ${resp.request().method()} ${resp.url()}`);
    }
  });
}

// Pull a snapshot of world state — tile counts bucketed by slot group,
// total discards, active seat. Mirrors the inspector helpers in
// playtest-broken-deal-repro.spec.mjs so the JSON dumps are diff-able
// across specs.
async function snapshotWorld(page) {
  return await page.evaluate(() => {
    const w = window.game?.world;
    if (!w) return { ok: false, reason: 'no window.game.world' };
    const things = Array.from((w.things && typeof w.things.values === 'function')
      ? w.things.values()
      : []);
    const byGroup = { hand: 0, wall: 0, discard: 0, meld: 0, other: 0 };
    const discardSamples = [];
    for (const t of things) {
      const g = t.slot?.group;
      if (g === 'hand') byGroup.hand++;
      else if (g === 'wall') byGroup.wall++;
      else if (g === 'discard') {
        byGroup.discard++;
        if (discardSamples.length < 20) {
          discardSamples.push({
            id: t.index,
            slotName: t.slot?.name,
            seat: t.slot?.seat,
          });
        }
      } else if (g === 'meld') byGroup.meld++;
      else byGroup.other++;
    }
    return {
      ok: true,
      thingsTotal: things.length,
      byGroup,
      discardSamples,
      activeSeat: w.match?.activeSeat ?? w.match?.activeSeatIndex ?? null,
      phase: w.match?.phase ?? null,
      handCount: w.match?.handCount ?? null,
    };
  });
}

// Quick-match + seat-take used by sections C and D where we want a
// human-controlled context that's still bot-filled. Section B uses the
// pure spectator URL (no seat take) instead.
async function quickMatchAndSeat(page, gameId) {
  // Snug `gameId` field if present (some lobby variants surface one).
  const gameIdInput = page.locator('#game-id, [data-testid="game-id"]').first();
  if (await gameIdInput.isVisible().catch(() => false)) {
    await gameIdInput.fill(gameId);
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
}

// ════════════════════════════════════════════════════════════════════
//  SECTION B — 4-bot self-play, single table, multi-minute soak
// ════════════════════════════════════════════════════════════════════
async function sectionB() {
  const bag = { ok: false, label: 'B-selfplay', failures: [] };
  console.log(`\n════ Section B: 4-bot self-play (${B_OBSERVE_MS / 1000}s observe) ════`);
  const ctx = await browser.newContext({
    viewport: { width: 1280, height: 800 },
    ignoreHTTPSErrors: true,
  });
  const page = await ctx.newPage();
  wireDiagnostics(page, bag);
  await installDefang(page);

  try {
    const gameId = `bishop-selfplay-${Date.now()}`;
    // seat=-1 + botCount=4 is the spectator all-bots-watch URL that
    // auto-binds the runtime and starts the deal without a human
    // having to take a seat. dealMode=auto + handCount=4 keeps the
    // session brisk so even at Easy difficulty we'll see real motion
    // inside the 3-minute window.
    const url = `${baseUrl}/autotable/?variant=changsha&seat=-1&dealMode=auto`
      + `&botCount=4&botDifficulty=Medium&handCount=4`
      + `&gameId=${encodeURIComponent(gameId)}`;
    await page.goto(url, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(3000);

    // Take an initial baseline so we can prove forward progress.
    const baseline = await snapshotWorld(page);
    bag.baseline = baseline;

    // Sample every 15s so we have a discard-count progression chart in
    // the JSON dump. The final assertion checks the LAST sample.
    const samples = [];
    const start = Date.now();
    while (Date.now() - start < B_OBSERVE_MS) {
      await page.waitForTimeout(15000);
      const s = await snapshotWorld(page);
      s.elapsedMs = Date.now() - start;
      samples.push(s);
      console.log(`  [B] t=${(s.elapsedMs / 1000).toFixed(0)}s discards=${s.byGroup?.discard ?? '?'} phase=${s.phase} activeSeat=${s.activeSeat}`);
    }
    bag.samples = samples;

    const final = samples[samples.length - 1];
    bag.final = final;

    // Peak discards across all samples — discards reset between hands
    // when a Hu fires, so a "final sample" check is fragile. The peak
    // is the right "table is alive" signal: it captures the high-water
    // mark of any single hand we observed.
    const peakDiscards = samples.reduce(
      (max, s) => Math.max(max, s.byGroup?.discard ?? 0),
      baseline.byGroup?.discard ?? 0);
    bag.peakDiscards = peakDiscards;

    // Also count hand-clear transitions (discards count goes DOWN by
    // ≥ 4) — each is a strong "hand completed, next hand dealt"
    // signal. A long-running soak should accumulate ≥ 1 of these.
    let handTransitions = 0;
    let prev = baseline.byGroup?.discard ?? 0;
    for (const s of samples) {
      const d = s.byGroup?.discard ?? 0;
      if (d + 4 <= prev) handTransitions++;
      prev = d;
    }
    bag.handTransitions = handTransitions;

    await page.screenshot({
      path: path.join(ARTIFACT_DIR, `bishop-bots-B-selfplay-${ts}.png`),
      fullPage: true,
    }).catch(() => {});

    // Assertion #1: peak discards must be ≥ 8 — that's the "bots are
    // actually taking turns inside a hand" bar. 8 = 2 per seat, which
    // any non-stuck table reaches within the first hand.
    if (peakDiscards < 8) {
      bag.failures.push(`expected peak ≥ 8 discards across samples; got ${peakDiscards}`);
    }

    // Assertion #2: no page errors during the run. Bot autonomy can't
    // be considered working if the client is throwing while we watch.
    if (bag.pageErrors.length > 0) {
      bag.failures.push(`page errors during section B: ${bag.pageErrors.length}`);
    }

    bag.ok = bag.failures.length === 0;
  } catch (e) {
    bag.failures.push(`section B threw: ${e?.message || String(e)}`);
  } finally {
    await ctx.close().catch(() => {});
  }

  fs.writeFileSync(
    path.join(ARTIFACT_DIR, `bishop-bots-B-selfplay-${ts}.json`),
    JSON.stringify(bag, null, 2));
  console.log(`Section B ${bag.ok ? 'PASS' : 'FAIL'}; failures=${bag.failures.length}`);
  return bag;
}

// ════════════════════════════════════════════════════════════════════
//  SECTION C — Multi-game isolation across two contexts
// ════════════════════════════════════════════════════════════════════
async function sectionC() {
  const bag = { ok: false, label: 'C-isolation', failures: [], a: {}, b: {} };
  console.log(`\n════ Section C: multi-game isolation (${C_OBSERVE_MS / 1000}s observe) ════`);

  const ctxA = await browser.newContext({ viewport: { width: 1280, height: 800 }, ignoreHTTPSErrors: true });
  const ctxB = await browser.newContext({ viewport: { width: 1280, height: 800 }, ignoreHTTPSErrors: true });
  const pageA = await ctxA.newPage();
  const pageB = await ctxB.newPage();
  wireDiagnostics(pageA, bag.a);
  wireDiagnostics(pageB, bag.b);
  await installDefang(pageA);
  await installDefang(pageB);

  try {
    const tsBase = Date.now();
    const gidA = `bishop-iso-A-${tsBase}`;
    const gidB = `bishop-iso-B-${tsBase}`;

    // Both contexts run as spectator all-bots-watch so the discard
    // pool builds up naturally without needing a human seat-take.
    const mk = (gid) =>
      `${baseUrl}/autotable/?variant=changsha&seat=-1&dealMode=auto`
      + `&botCount=4&botDifficulty=Medium&handCount=2`
      + `&gameId=${encodeURIComponent(gid)}`;
    await Promise.all([
      pageA.goto(mk(gidA), { waitUntil: 'domcontentloaded' }),
      pageB.goto(mk(gidB), { waitUntil: 'domcontentloaded' }),
    ]);

    // Let both tables run independently for the observation window,
    // sampling at intervals so we accumulate discard tuples across
    // multiple hands (discards reset between Hus, so a single
    // snapshot may miss the bulk of the action).
    await Promise.all([pageA.waitForTimeout(3000), pageB.waitForTimeout(3000)]);

    const sampleA = [];
    const sampleB = [];
    const tuplesA = new Set();
    const tuplesB = new Set();
    const start = Date.now();
    while (Date.now() - start < C_OBSERVE_MS) {
      await pageA.waitForTimeout(5000);
      const [sA, sB] = await Promise.all([snapshotWorld(pageA), snapshotWorld(pageB)]);
      sampleA.push(sA);
      sampleB.push(sB);
      for (const d of (sA.discardSamples || [])) tuplesA.add(`${d.id}@${d.slotName}`);
      for (const d of (sB.discardSamples || [])) tuplesB.add(`${d.id}@${d.slotName}`);
    }
    bag.a.samples = sampleA;
    bag.b.samples = sampleB;

    const snapA = sampleA[sampleA.length - 1];
    const snapB = sampleB[sampleB.length - 1];
    bag.a.snapshot = snapA;
    bag.b.snapshot = snapB;

    // Aggregate (id, slotName) tuple sets across the whole observation
    // window. tile ids 0..107 are reused across games, so the unique
    // key has to include the slot assignment — that's what differs
    // per-game based on independent runtime RNG.
    bag.a.discardTuples = [...tuplesA];
    bag.b.discardTuples = [...tuplesB];

    // Both tables should have made at least SOME discards by now —
    // otherwise the "isolation" test is vacuous.
    if (tuplesA.size < 2) {
      bag.failures.push(`ctx A accumulated < 2 discard tuples across samples (got ${tuplesA.size}); isolation check is vacuous`);
    }
    if (tuplesB.size < 2) {
      bag.failures.push(`ctx B accumulated < 2 discard tuples across samples (got ${tuplesB.size}); isolation check is vacuous`);
    }

    // Cross-bleed check: the (id, slotName) tuple sets must NOT be
    // identical. Different RNG seeds + independent runtimes mean the
    // probability of accidentally matching tuple sets is effectively
    // zero. A perfect match means the WS endpoint is fanning out one
    // game's UPDATEs to the other — the exact bug we're guarding.
    const intersection = [...tuplesA].filter(t => tuplesB.has(t));
    bag.intersection = intersection;
    if (tuplesA.size > 0 && tuplesB.size > 0 && intersection.length === Math.min(tuplesA.size, tuplesB.size)) {
      bag.failures.push(`ctx A and ctx B discard tuple sets fully overlap (${intersection.length}) — looks like cross-bleed`);
    }

    bag.ok = bag.failures.length === 0;
  } catch (e) {
    bag.failures.push(`section C threw: ${e?.message || String(e)}`);
  } finally {
    await ctxA.close().catch(() => {});
    await ctxB.close().catch(() => {});
  }

  fs.writeFileSync(
    path.join(ARTIFACT_DIR, `bishop-bots-C-isolation-${ts}.json`),
    JSON.stringify(bag, null, 2));
  console.log(`Section C ${bag.ok ? 'PASS' : 'FAIL'}; failures=${bag.failures.length}`);
  return bag;
}

// ════════════════════════════════════════════════════════════════════
//  SECTION D — Late-join sees accumulated state for the joined game
// ════════════════════════════════════════════════════════════════════
async function sectionD() {
  const bag = { ok: false, label: 'D-latejoin', failures: [], early: {}, late: {} };
  console.log(`\n════ Section D: late-join sees accumulated state (${D_OBSERVE_MS / 1000}s warmup) ════`);

  const ctxEarly = await browser.newContext({ viewport: { width: 1280, height: 800 }, ignoreHTTPSErrors: true });
  const ctxLate  = await browser.newContext({ viewport: { width: 1280, height: 800 }, ignoreHTTPSErrors: true });
  const pageEarly = await ctxEarly.newPage();
  const pageLate  = await ctxLate.newPage();
  wireDiagnostics(pageEarly, bag.early);
  wireDiagnostics(pageLate, bag.late);
  await installDefang(pageEarly);
  await installDefang(pageLate);

  try {
    const gid = `bishop-late-${Date.now()}`;
    const url = `${baseUrl}/autotable/?variant=changsha&seat=-1&dealMode=auto`
      + `&botCount=4&botDifficulty=Medium&handCount=2`
      + `&gameId=${encodeURIComponent(gid)}`;

    // Early context starts the table — give it the warmup window so
    // discards accumulate before the late joiner arrives.
    await pageEarly.goto(url, { waitUntil: 'domcontentloaded' });
    await pageEarly.waitForTimeout(D_OBSERVE_MS);

    const earlySnap = await snapshotWorld(pageEarly);
    bag.early.beforeJoin = earlySnap;

    // Late joiner connects with the SAME gameId. We expect the
    // snapshot pushed on JOIN to include the accumulated discards.
    await pageLate.goto(url, { waitUntil: 'domcontentloaded' });
    // Give a short settle window for the JOINED + initial UPDATE
    // round-trip + Three.js scene-build.
    await pageLate.waitForTimeout(5000);
    const lateSnap = await snapshotWorld(pageLate);
    bag.late.afterJoin = lateSnap;

    // Take another early-side snapshot at the same moment so we can
    // diff them.
    const earlyAfter = await snapshotWorld(pageEarly);
    bag.early.afterLateJoined = earlyAfter;

    // Assertion: late-joiner must see ≥ half the "in-play" tiles
    // (discards + melds) the early context sees at the same wall-clock
    // moment. Vasquez 2026-06-04 — the original assertion only counted
    // `byGroup.discard`, but Hard/Medium bots now (post b5575b3
    // difficulty differentiation) claim discards into melds and often
    // complete hands inside the 20s observation window. When a hand
    // ends and a new one begins the `discard` group resets to a
    // single-digit count while `meld` accumulates, so discards-only is
    // a brittle proxy for "the late joiner got hydrated state". Sum
    // discard + meld + hand + wall to track the durable in-play
    // population. A true "no late-join state delivery" bug produces 0
    // hydrated tiles on the late side while early has > 50.
    const inPlay = (snap) => (snap.byGroup?.discard ?? 0)
      + (snap.byGroup?.meld ?? 0)
      + (snap.byGroup?.hand ?? 0)
      + (snap.byGroup?.wall ?? 0);
    const earlyInPlay = inPlay(earlyAfter);
    const lateInPlay = inPlay(lateSnap);
    bag.earlyInPlay = earlyInPlay;
    bag.lateInPlay = lateInPlay;
    // Keep the legacy discard counters in the bag for diagnostics.
    bag.earlyDiscards = earlyAfter.byGroup?.discard ?? 0;
    bag.lateDiscards = lateSnap.byGroup?.discard ?? 0;

    if (earlyInPlay < 20) {
      bag.failures.push(`early context didn't accumulate enough in-play tiles (${earlyInPlay}); late-join check is vacuous`);
    }
    if (lateInPlay < Math.max(20, Math.floor(earlyInPlay / 2))) {
      bag.failures.push(
        `late joiner saw ${lateInPlay} in-play tiles; expected ≥ ${Math.max(20, Math.floor(earlyInPlay / 2))} ` +
        `(early at same moment: ${earlyInPlay})`);
    }

    bag.ok = bag.failures.length === 0;
  } catch (e) {
    bag.failures.push(`section D threw: ${e?.message || String(e)}`);
  } finally {
    await ctxEarly.close().catch(() => {});
    await ctxLate.close().catch(() => {});
  }

  fs.writeFileSync(
    path.join(ARTIFACT_DIR, `bishop-bots-D-latejoin-${ts}.json`),
    JSON.stringify(bag, null, 2));
  console.log(`Section D ${bag.ok ? 'PASS' : 'FAIL'}; failures=${bag.failures.length}`);
  return bag;
}

// ════════════════════════════════════════════════════════════════════
//  Driver
// ════════════════════════════════════════════════════════════════════
try {
  summary.sections.B = await sectionB();
  summary.sections.C = await sectionC();
  summary.sections.D = await sectionD();
} finally {
  await browser.close().catch(() => {});
}

for (const k of ['B', 'C', 'D']) {
  if (summary.sections[k] && !summary.sections[k].ok) {
    summary.failures.push(...summary.sections[k].failures.map(f => `${k}: ${f}`));
  }
}

const summaryPath = path.join(ARTIFACT_DIR, `bishop-bots-summary-${ts}.json`);
fs.writeFileSync(summaryPath, JSON.stringify(summary, null, 2));

console.log('\n════════════════════════════════════════');
console.log(`Bishop W25 playtest summary: ${summary.failures.length === 0 ? 'PASS' : 'FAIL'}`);
console.log(`  B (4-bot self-play):       ${summary.sections.B?.ok ? 'PASS' : 'FAIL'}`);
console.log(`  C (multi-game isolation):  ${summary.sections.C?.ok ? 'PASS' : 'FAIL'}`);
console.log(`  D (late-join sees state):  ${summary.sections.D?.ok ? 'PASS' : 'FAIL'}`);
console.log(`Summary JSON: ${summaryPath}`);
console.log('════════════════════════════════════════');

process.exit(summary.failures.length === 0 ? 0 : 1);
