// Phase K Wave 23 — Vasquez (QA) playtest spec
//
// Purpose: produce concrete, screenshot-backed evidence of what works and
// what doesn't when a real user tries to play a Changsha mahjong game.
// Walks home → lobby → Changsha preset → Connect → Take Seat → game canvas
// → Deal and captures a screenshot at every step plus a manifest.

import { test } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// tests/e2e/playtest-changsha.spec.ts -> playtest-artifacts at repo root.
// From src/frontend/autotable-src/tests/e2e/, that's 5 levels up.
const ARTIFACT_DIR = path.resolve(__dirname, '../../../../../playtest-artifacts');

if (!fs.existsSync(ARTIFACT_DIR)) {
  fs.mkdirSync(ARTIFACT_DIR, { recursive: true });
}

interface Findings {
  url: string;
  loadingVisible: boolean;
  loadingText: string;
  lobbyVisible: boolean;
  lobbyQuickMatchCount: number;
  rulePresetSelectCount: number;
  rulePresetOptions: string[];
  changshaOptionFound: boolean;
  connectButtonCount: number;
  takeSeatCount: number;
  takeSeatVisible: number;
  canvasCount: number;
  dealButtonCount: number;
  dealAfterClickResult: string;
  buttonLabels: string[];
  tileTestids: number;
  handTestids: number;
  seatProjectionTestids: number;
  pageErrors: string[];
  consoleErrors: string[];
  networkFailures: string[];
}

test.describe('Changsha Mahjong — playable end-to-end walkthrough', () => {
  test('step-by-step gameplay', async ({ page }) => {
    test.setTimeout(180_000);

    const pageErrors: string[] = [];
    const consoleErrors: string[] = [];
    const networkFailures: string[] = [];

    page.on('console', msg => {
      const text = `[browser:${msg.type()}] ${msg.text()}`;
      console.log(text);
      if (msg.type() === 'error') consoleErrors.push(msg.text());
    });
    page.on('pageerror', err => {
      const text = `[browser:pageerror] ${err.message}`;
      console.log(text);
      pageErrors.push(err.message);
    });
    page.on('response', resp => {
      if (resp.status() >= 400) {
        const entry = `${resp.status()} ${resp.request().method()} ${resp.url()}`;
        console.log(`[network:fail] ${entry}`);
        networkFailures.push(entry);
      }
    });
    page.on('requestfailed', req => {
      const entry = `FAILED ${req.method()} ${req.url()} -- ${req.failure()?.errorText ?? 'unknown'}`;
      console.log(`[network:reqfail] ${entry}`);
      networkFailures.push(entry);
    });

    // Init script — defang full-page overlays that intercept pointer events
    // even though they have aria-hidden="true" (a real accessibility bug AND
    // a real playability bug).  Without this, every click on lobby/seat/deal
    // buttons gets eaten by #tour-overlay, #magic-link-landing, and the
    // #signin-modal-backdrop subtree.
    await page.addInitScript(() => {
      const inject = () => {
        if (document.getElementById('vasquez-playtest-overlay-defang')) return;
        const style = document.createElement('style');
        style.id = 'vasquez-playtest-overlay-defang';
        style.textContent = `
          #tour-overlay,
          #magic-link-landing,
          #magic-link-overlay,
          #signin-modal-backdrop,
          .magic-link-landing,
          .magic-link-overlay,
          .signin-modal-backdrop,
          [data-testid="tour-overlay"],
          [data-testid="signin-modal-backdrop"]
            { display: none !important; pointer-events: none !important; visibility: hidden !important; }
          [aria-hidden="true"] { pointer-events: none !important; }
        `;
        document.head.appendChild(style);
      };
      if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', inject);
      } else {
        inject();
      }
    });

    const findings: Findings = {
      url: '',
      loadingVisible: false,
      loadingText: '',
      lobbyVisible: false,
      lobbyQuickMatchCount: 0,
      rulePresetSelectCount: 0,
      rulePresetOptions: [],
      changshaOptionFound: false,
      connectButtonCount: 0,
      takeSeatCount: 0,
      takeSeatVisible: 0,
      canvasCount: 0,
      dealButtonCount: 0,
      dealAfterClickResult: '',
      buttonLabels: [],
      tileTestids: 0,
      handTestids: 0,
      seatProjectionTestids: 0,
      pageErrors,
      consoleErrors,
      networkFailures,
    };

    // STEP 1 — home loads
    console.log('=== STEP 1: load /autotable/ ===');
    await page.goto('/autotable/', { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(3000);
    findings.url = page.url();
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '01-home-loaded.png'), fullPage: true });

    // STEP 1b — dismiss any onboarding/tour
    console.log('=== STEP 1b: dismiss tour + onboarding ===');
    try {
      const tourSkip = page.locator('#tour-skip');
      if (await tourSkip.isVisible().catch(() => false)) {
        await tourSkip.click({ timeout: 3000, force: true });
        console.log('STEP 1b clicked #tour-skip');
        await page.waitForTimeout(600);
      }
    } catch (err) {
      console.log(`STEP 1b tour-skip err: ${(err as Error).message}`);
    }
    try {
      const onboardingSkip = page.locator('#onboarding-skip');
      if (await onboardingSkip.isVisible().catch(() => false)) {
        await onboardingSkip.click({ timeout: 3000, force: true });
        console.log('STEP 1b clicked #onboarding-skip');
        await page.waitForTimeout(600);
      }
    } catch (err) {
      console.log(`STEP 1b onboarding-skip err: ${(err as Error).message}`);
    }
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '01b-after-tour-dismissed.png'), fullPage: true });

    // STEP 2 — lobby detection
    console.log('=== STEP 2: lobby detection ===');
    findings.loadingVisible = await page.locator('#loading').isVisible().catch(() => false);
    findings.loadingText = (await page.locator('#loading').textContent().catch(() => '')) || '';
    findings.lobbyQuickMatchCount = await page.locator('[data-testid="lobby-quick-match"], #lobby-quick-match').count();
    findings.lobbyVisible = findings.lobbyQuickMatchCount > 0 &&
      (await page.locator('[data-testid="lobby-quick-match"], #lobby-quick-match').first().isVisible().catch(() => false));
    console.log(`STEP 2 lobby-quick-match count: ${findings.lobbyQuickMatchCount}, visible: ${findings.lobbyVisible}`);
    console.log(`STEP 2 loading-el visible: ${findings.loadingVisible} text="${findings.loadingText.trim()}"`);
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '02-lobby-state.png'), fullPage: true });

    // STEP 3 — Changsha preset
    console.log('=== STEP 3: Changsha rule preset ===');
    const presetSelector = page.locator('#lobby-rule-preset-select, [data-testid="lobby-rule-preset-select"]');
    findings.rulePresetSelectCount = await presetSelector.count();
    if (findings.rulePresetSelectCount > 0) {
      findings.rulePresetOptions = await presetSelector.first().locator('option').allInnerTexts();
      const changshaOption = findings.rulePresetOptions.find(o => /changsha/i.test(o));
      findings.changshaOptionFound = !!changshaOption;
      console.log(`STEP 3 presets available: ${JSON.stringify(findings.rulePresetOptions)}`);
      if (changshaOption) {
        try {
          await presetSelector.first().selectOption({ label: changshaOption });
          console.log(`STEP 3 selected preset: ${changshaOption}`);
        } catch (err) {
          console.log(`STEP 3 select err: ${(err as Error).message}`);
        }
      }
    } else {
      console.log('STEP 3 rule-preset-select NOT FOUND (no DB-seeded presets surfaced)');
    }
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '03-after-preset-select.png'), fullPage: true });

    // STEP 4 — Quick Match
    console.log('=== STEP 4: Quick Match click ===');
    const quickMatch = page.locator('[data-testid="lobby-quick-match"], #lobby-quick-match');
    if ((await quickMatch.count()) > 0 && (await quickMatch.first().isVisible().catch(() => false))) {
      try {
        await quickMatch.first().click({ timeout: 5000 });
        console.log('STEP 4 clicked lobby-quick-match');
        await page.waitForTimeout(4000);
      } catch (err) {
        console.log(`STEP 4 quick-match click err: ${(err as Error).message}`);
      }
    } else {
      const connectByName = page.getByRole('button', { name: /connect/i });
      findings.connectButtonCount = await connectByName.count();
      if (findings.connectButtonCount > 0) {
        try {
          await connectByName.first().click({ timeout: 5000 });
          await page.waitForTimeout(4000);
          console.log('STEP 4 clicked legacy Connect');
        } catch (err) {
          console.log(`STEP 4 connect click err: ${(err as Error).message}`);
        }
      }
    }
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '04-after-quick-match.png'), fullPage: true });

    // STEP 5 — Take Seat
    console.log('=== STEP 5: Take Seat ===');
    const takeSeatByClass = page.locator('.take-seat');
    findings.takeSeatCount = await takeSeatByClass.count();
    let visibleSeats = 0;
    let firstClickIdx = -1;
    for (let i = 0; i < findings.takeSeatCount; i++) {
      if (await takeSeatByClass.nth(i).isVisible().catch(() => false)) {
        visibleSeats++;
        if (firstClickIdx === -1) firstClickIdx = i;
      }
    }
    findings.takeSeatVisible = visibleSeats;
    console.log(`STEP 5 take-seat total=${findings.takeSeatCount} visible=${visibleSeats}`);
    if (firstClickIdx >= 0) {
      try {
        await takeSeatByClass.nth(firstClickIdx).click({ timeout: 5000 });
        console.log(`STEP 5 clicked .take-seat[${firstClickIdx}]`);
        await page.waitForTimeout(3000);
      } catch (err) {
        console.log(`STEP 5 click err: ${(err as Error).message}`);
      }
    }
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '05-after-take-seat.png'), fullPage: true });

    // STEP 6 — canvas / three scene
    console.log('=== STEP 6: canvas / three.js scene ===');
    findings.canvasCount = await page.locator('canvas').count();
    console.log(`STEP 6 canvas count: ${findings.canvasCount}`);
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '06-game-scene.png'), fullPage: true });

    // STEP 7 — Deal
    console.log('=== STEP 7: Deal ===');
    const dealById = page.locator('#deal');
    findings.dealButtonCount = await dealById.count();
    if (findings.dealButtonCount > 0) {
      try {
        const el = dealById.first();
        const visible = await el.isVisible().catch(() => false);
        const enabled = await el.isEnabled().catch(() => false);
        findings.dealAfterClickResult = `visible=${visible} enabled=${enabled}`;
        console.log(`STEP 7 deal ${findings.dealAfterClickResult}`);
        if (visible) {
          try {
            await el.click({ timeout: 5000 });
            console.log('STEP 7 clicked #deal');
            findings.dealAfterClickResult += ' click=ok';
          } catch (err) {
            console.log(`STEP 7 deal click err: ${(err as Error).message}`);
            findings.dealAfterClickResult += ` click_error=${(err as Error).message.slice(0, 200)}`;
          }
          await page.waitForTimeout(3500);
        }
      } catch (err) {
        console.log(`STEP 7 deal probe err: ${(err as Error).message}`);
      }
    }
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '07-after-deal.png'), fullPage: true });

    // STEP 7b — try clicking Connect (legacy SignalR connect button)
    console.log('=== STEP 7b: legacy Connect button ===');
    const connectBtn = page.locator('#connect');
    if ((await connectBtn.count()) > 0 && (await connectBtn.first().isVisible().catch(() => false))) {
      try {
        await connectBtn.first().click({ timeout: 5000 });
        console.log('STEP 7b clicked #connect');
        await page.waitForTimeout(3000);
      } catch (err) {
        console.log(`STEP 7b connect click err: ${(err as Error).message}`);
      }
    } else {
      console.log('STEP 7b #connect not visible / not present');
    }
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '07b-after-connect.png'), fullPage: true });

    // STEP 8 — enumerate buttons
    console.log('=== STEP 8: visible button labels ===');
    const allButtons = await page.getByRole('button').all();
    const labels: string[] = [];
    for (const b of allButtons) {
      try {
        if (!(await b.isVisible())) continue;
        const t = (await b.textContent())?.trim();
        if (!t) continue;
        const id = (await b.getAttribute('id')) || '';
        const cls = ((await b.getAttribute('class')) || '').slice(0, 60);
        labels.push(`${t} (id=${id} cls=${cls})`);
      } catch { /* ignore */ }
    }
    findings.buttonLabels = labels;
    console.log(`STEP 8 visible buttons (${labels.length}):`);
    for (const l of labels) console.log(`  - ${l}`);

    // STEP 9 — tile / hand surfaces
    console.log('=== STEP 9: tile / hand surfaces ===');
    findings.tileTestids = await page.locator('[data-testid*="tile"]').count();
    findings.handTestids = await page.locator('[data-testid*="hand"]').count();
    findings.seatProjectionTestids = await page.locator('[data-testid*="seat"]').count();
    console.log(
      `STEP 9 tile-* testids: ${findings.tileTestids}, ` +
      `hand-* testids: ${findings.handTestids}, ` +
      `seat-* testids: ${findings.seatProjectionTestids}`,
    );

    // STEP 10 — final
    console.log('=== STEP 10: final state ===');
    await page.screenshot({ path: path.join(ARTIFACT_DIR, '10-final-state.png'), fullPage: true });

    fs.writeFileSync(
      path.join(ARTIFACT_DIR, 'findings.json'),
      JSON.stringify(findings, null, 2),
    );
    console.log('=== Findings written to playtest-artifacts/findings.json ===');
  });
});
