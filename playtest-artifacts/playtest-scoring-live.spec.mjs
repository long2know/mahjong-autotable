// Frost 2026-06-04 — Live scoring wire-proof spec.
//
// Stephen wanted PROOF that FanCalculator actually fires during a real
// game AND that the detected fans land on the WebSocket payload the
// frontend reads.  This spec drives multiple real 4-bot Changsha games
// against the running backend at :8088 and asserts:
//
//   1. The backend WebSocket emits a `["result","current",{...}]`
//      collection entry whose `scoreResult.fans` field is a NON-EMPTY
//      array with the canonical wire schema:
//        { fan, points, chinese, pinyin, english }
//   2. Each fan entry's Chinese/Pinyin/English labels are non-empty.
//   3. The aggregate `scoreResult.fanPoints` matches the per-fan sum.
//   4. The total `scoreResult.basePoints` is strictly positive.
//
// Strategy:
//   - Spectate (no `?seat=` param) the 4-bot table so the runtime drives
//     bots end-to-end without needing local input.
//   - Loop up to N independent games (fresh gameId each), watching every
//     server-emitted HandResultEntry via the result.update event AND a
//     CDP-tap on WebSocket frames (wire-level evidence, not just bundle
//     state).
//   - Stop as soon as ANY Hu carries non-empty fans — that's the proof.
//   - Time budget: per-game 75 s; up to 6 games = ~7.5 min max.
//
// A Standard 258-pair Hu on a claimed discard (no concealment, no
// self-draw, mixed suits, no special pattern) legitimately produces
// zero fans — the FanCalculator gates each fan on the appropriate
// situational/structural flag. So we expect to need a few games to
// catch a self-draw Hu (SelfDraw + ConcealedHand fans guaranteed),
// a 7-pair Hu, or a FullFlush.
//
// Run:  E2E_BASE_URL=http://127.0.0.1:8088 node \
//          playtest-artifacts/playtest-scoring-live.spec.mjs

import { chromium } from 'playwright';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const baseUrl = process.env.E2E_BASE_URL || 'http://127.0.0.1:8088';
const stamp = new Date().toISOString().replace(/[:.]/g, '-');
const screenshotDir = join('playtest-artifacts', 'screenshots', `frost-scoring-live-${stamp}`);
mkdirSync(screenshotDir, { recursive: true });

const log = (...m) => console.log('[frost-scoring-live]', ...m);
const dumpJson = (name, data) => {
  const fp = join(screenshotDir, `${name}.json`);
  writeFileSync(fp, JSON.stringify(data, null, 2));
  return fp;
};

const MAX_GAMES = parseInt(process.env.FROST_MAX_GAMES ?? '6', 10);
const PER_GAME_BUDGET_MS = parseInt(process.env.FROST_PER_GAME_MS ?? '75000', 10);

const browser = await chromium.launch();

// Cumulative findings across every game we open.
const summary = {
  baseUrl,
  startedAt: new Date().toISOString(),
  perGame: [],
  wireSnapshot: {
    resultFramesSeen: 0,
    huFramesSeen: 0,
    fansFramesSeen: 0,
    uniqueHuFingerprints: new Set(),
    lastFansFrame: null,
  },
  huObserved: 0,
  huWithFansObserved: 0,
  firstHuWithFans: null,
  pageErrors: [],
};

const seenHuFingerprints = new Set();

async function runGame(gameIndex) {
  const gameId = `frost-scoring-live-${Date.now()}-${gameIndex}-${Math.floor(Math.random() * 10000)}`;
  const url = `${baseUrl}/autotable/?variant=changsha&dealMode=auto&botCount=4&botDifficulty=Hard&gameId=${gameId}`;
  log(`game ${gameIndex + 1}/${MAX_GAMES}: opening ${url}`);

  const ctx = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await ctx.newPage();

  page.on('pageerror', (e) => {
    summary.pageErrors.push({ gameIndex, message: e.message, stack: (e.stack ?? '').split('\n').slice(0, 4).join('\n') });
  });

  // CDP tap: capture WS frames containing `["result","current",{ ... }]`.
  // Proves the fans came over the actual wire (not synthesised on the
  // bundle by replay logic or a defensive fill-in).
  const wireForGame = {
    resultFramesSeen: 0,
    huFramesSeen: 0,
    fansFramesSeen: 0,
    huFingerprints: [],
  };
  const cdp = await ctx.newCDPSession(page);
  await cdp.send('Network.enable');
  cdp.on('Network.webSocketFrameReceived', ({ response }) => {
    const payload = response?.payloadData;
    if (!payload || typeof payload !== 'string') return;
    if (!payload.includes('"result"')) return;
    try {
      const parsed = JSON.parse(payload);
      const entries = parsed?.entries ?? [];
      for (const entry of entries) {
        if (!Array.isArray(entry) || entry.length !== 3) continue;
        const [kind, key, value] = entry;
        if (kind !== 'result' || key !== 'current' || !value) continue;
        wireForGame.resultFramesSeen += 1;
        if (value.type === 'Hu') {
          wireForGame.huFramesSeen += 1;
          const fp = `${value.winner}|${value.scoreResult?.basePoints ?? 0}|${value.scoreResult?.fanPoints ?? 0}|${(value.scoreResult?.fans ?? []).map((f) => f.fan).join('+')}`;
          if (!wireForGame.huFingerprints.includes(fp)) wireForGame.huFingerprints.push(fp);
          if (Array.isArray(value.scoreResult?.fans) && value.scoreResult.fans.length > 0) {
            wireForGame.fansFramesSeen += 1;
            if (!summary.firstHuWithFans) summary.firstHuWithFans = { gameIndex, gameId, value };
          }
        }
      }
    } catch {}
  });

  await page.goto(url, { waitUntil: 'domcontentloaded' });
  await page.waitForTimeout(3000);

  // Dismiss any onboarding / lobby overlay so the canvas + WS bind.
  for (const sel of ['#tour-skip', '#onboarding-skip', '#lobby-close']) {
    const e = page.locator(sel);
    if (await e.isVisible().catch(() => false)) {
      await e.click({ force: true, timeout: 3000 }).catch(() => {});
      await page.waitForTimeout(300);
    }
  }
  for (const sel of ['#lobby-quick-match', '#connect']) {
    const e = page.locator(sel).first();
    if (await e.isVisible().catch(() => false)) {
      await e.click({ timeout: 5000 }).catch(() => {});
      await page.waitForTimeout(2500);
    }
  }

  // Install a result-update listener that captures every fresh snapshot.
  await page.evaluate(() => {
    window.__frostScoringObservations = [];
    const c = window.game?.world?.client;
    if (!c?.result?.on) return;
    c.result.on('update', (entries) => {
      for (const [key, value] of entries) {
        try {
          window.__frostScoringObservations.push({
            at: Date.now(),
            key: String(key),
            value: value ? JSON.parse(JSON.stringify(value)) : null,
          });
        } catch {}
      }
    });
  });

  const deadline = Date.now() + PER_GAME_BUDGET_MS;
  let huForGame = 0;
  let huWithFansForGame = 0;
  let firstHuWithFansForGame = null;

  while (Date.now() < deadline) {
    // Probe the live `client.result.get('current')` and the captured
    // observation queue. After each Hu, try to advance to the next
    // hand by sending the `match[1]={action:'nextHand'}` signal AND
    // by clicking the result modal's "Next Hand" button if present —
    // belt-and-braces because the runtime auto-advances OR may stall
    // waiting on the bundle to acknowledge the modal.
    const snap = await page.evaluate(() => {
      const c = window.game?.world?.client;
      const obs = window.__frostScoringObservations ?? [];
      let current = null;
      try {
        current = c?.result?.get?.('current') ?? null;
        if (current) current = JSON.parse(JSON.stringify(current));
      } catch {}
      // Drain newer observations than we've seen before.
      const drained = obs.splice(0, obs.length);
      return { current, drained };
    });

    for (const o of snap.drained) {
      const v = o.value;
      if (!v || v.type !== 'Hu') continue;
      const fp = `${v.winner}|${v.scoreResult?.basePoints ?? 0}|${v.scoreResult?.fanPoints ?? 0}|${(v.scoreResult?.fans ?? []).map((f) => f.fan).join('+')}`;
      if (seenHuFingerprints.has(fp)) continue;
      seenHuFingerprints.add(fp);
      huForGame += 1;
      summary.huObserved += 1;
      const fans = v.scoreResult?.fans ?? [];
      if (fans.length > 0) {
        huWithFansForGame += 1;
        summary.huWithFansObserved += 1;
        if (!firstHuWithFansForGame) firstHuWithFansForGame = v;
        if (!summary.firstHuWithFans) summary.firstHuWithFans = { gameIndex, gameId, value: v };
      }
      log(`  Hu#${summary.huObserved} winner=${v.winner} basePoints=${v.scoreResult?.basePoints} fanPoints=${v.scoreResult?.fanPoints} fans=[${fans.map((f) => f.fan).join(', ')}]`);
    }

    // If a Hu modal is showing, try to dismiss it and signal next-hand.
    try {
      const modalVisible = await page.evaluate(() => {
        const el = document.getElementById('result-modal');
        if (!el) return false;
        const cs = window.getComputedStyle(el);
        return cs.display !== 'none' && cs.visibility !== 'hidden';
      });
      if (modalVisible) {
        await page.evaluate(() => {
          try {
            const c = window.game?.world?.client;
            // The frontend's "Next Hand" handler does this — replicate
            // server-side so we don't depend on the modal being clickable.
            c?.match?.set?.(1, { action: 'nextHand' });
          } catch {}
          // Also click the visible button if present.
          const btn = document.getElementById('result-next');
          if (btn) btn.click();
        });
      }
    } catch {}

    // Stop early as soon as ANY hand-with-fans is observed.
    if (summary.firstHuWithFans) break;
    await page.waitForTimeout(750);
  }

  // Screenshot of the final modal state for evidence.
  await page.screenshot({ path: join(screenshotDir, `game-${gameIndex + 1}-final.png`), fullPage: false }).catch(() => {});

  summary.perGame.push({
    gameIndex,
    gameId,
    huForGame,
    huWithFansForGame,
    firstHuWithFansForGame,
    wireForGame,
  });
  summary.wireSnapshot.resultFramesSeen += wireForGame.resultFramesSeen;
  summary.wireSnapshot.huFramesSeen += wireForGame.huFramesSeen;
  summary.wireSnapshot.fansFramesSeen += wireForGame.fansFramesSeen;

  await ctx.close();
}

for (let i = 0; i < MAX_GAMES; i++) {
  await runGame(i);
  if (summary.firstHuWithFans) break;
}

await browser.close();

// ── Assertions ────────────────────────────────────────────────────
const issues = [];

if (!summary.firstHuWithFans) {
  issues.push(`FAIL: no Hu with non-empty fans observed across ${MAX_GAMES} games.`);
} else {
  const sr = summary.firstHuWithFans.value.scoreResult ?? {};
  const fans = Array.isArray(sr.fans) ? sr.fans : [];
  if (fans.length === 0) issues.push('FAIL: scoreResult.fans empty.');
  let pointSum = 0;
  for (const f of fans) {
    if (!f || typeof f.fan !== 'string' || f.fan.length === 0) {
      issues.push(`FAIL: fan entry missing camelCase id: ${JSON.stringify(f)}`);
      continue;
    }
    if (!f.chinese) issues.push(`FAIL: fan '${f.fan}' missing Chinese label.`);
    if (!f.pinyin) issues.push(`FAIL: fan '${f.fan}' missing Pinyin label.`);
    if (!f.english) issues.push(`FAIL: fan '${f.fan}' missing English label.`);
    if (typeof f.points !== 'number' || f.points <= 0) {
      issues.push(`FAIL: fan '${f.fan}' points <= 0 (${f.points}).`);
    }
    if (typeof f.points === 'number') pointSum += f.points;
  }
  if (typeof sr.fanPoints !== 'number' || sr.fanPoints !== pointSum) {
    issues.push(`FAIL: scoreResult.fanPoints (${sr.fanPoints}) != Σ fan.points (${pointSum}).`);
  }
  if (typeof sr.basePoints !== 'number' || sr.basePoints <= 0) {
    issues.push(`FAIL: scoreResult.basePoints invalid (${sr.basePoints}).`);
  }
  if (summary.wireSnapshot.fansFramesSeen === 0) {
    issues.push('FAIL: no fans WS frame captured via CDP — wire-proof missing.');
  }
}

const wireSnapshotForJson = {
  ...summary.wireSnapshot,
  uniqueHuFingerprints: Array.from(seenHuFingerprints),
};

const findingsPath = dumpJson('findings', {
  ...summary,
  wireSnapshot: wireSnapshotForJson,
  issues,
});

log(`screenshots: ${screenshotDir}`);
log(`findings: ${findingsPath}`);
log(`games run: ${summary.perGame.length} / ${MAX_GAMES}`);
log(`total Hu observed: ${summary.huObserved} (with fans: ${summary.huWithFansObserved})`);
log(`wireSnapshot: result=${summary.wireSnapshot.resultFramesSeen}, Hu=${summary.wireSnapshot.huFramesSeen}, fans=${summary.wireSnapshot.fansFramesSeen}`);

if (summary.firstHuWithFans) {
  const v = summary.firstHuWithFans.value;
  const sr = v.scoreResult ?? {};
  log(`FIRST Hu-with-fans @ game ${summary.firstHuWithFans.gameIndex + 1}: winner=${v.winner}, basePoints=${sr.basePoints}, fanPoints=${sr.fanPoints}, category=${sr.category}`);
  for (const f of (sr.fans ?? [])) {
    log(`  · ${f.fan} (${f.chinese} / ${f.pinyin} / ${f.english}) = ${f.points} pts`);
  }
}

if (issues.length > 0) {
  log('ISSUES:');
  for (const i of issues) log(`  · ${i}`);
}

const pass = issues.filter((i) => i.startsWith('FAIL:')).length === 0;
log(`\n[frost-scoring-live] result: ${pass ? 'PASS' : 'FAIL'}`);
process.exit(pass ? 0 : 2);
