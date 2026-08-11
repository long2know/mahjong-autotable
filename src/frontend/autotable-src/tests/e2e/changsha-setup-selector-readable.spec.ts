// G5 CLOSER (OWNED by hudson-1 — Ripley 2026-08-07 11:49). Ferro's UI-1 is verified
// genuine: dark-listbox.ts auto-enhances every real select.dark-select /
// .settings-v2-select (MutationObserver for lazily-added), the native <select> is
// kept aria-hidden when enhanced, with a color-scheme:dark fallback. This is the
// INTEGRATED closer — it runs against the ACTUAL running app (Ferro UI + Hicks
// FE-1 + backend), NOT a reproduction, and is DISTINCT from Ferro's UI-lane unit
// (dark-listbox-real.contract.spec.ts).
// Targets = the REAL Changsha-VISIBLE selects (NOT #game-type — FE-1 hides the
// relay setup panel in Changsha): the variant picker
// `.ferro-variant-picker-select.dark-select`, the settings-drawer language select
// `[data-testid=settings-language-select]`, and the `[data-testid=rule-preset-picker]`.
// Acceptance per select: the robust popup is EITHER a custom role=listbox (open-able,
// role=option children, REAL-pixel WCAG>=4.5 on options/optgroups) OR the control
// resolves color-scheme:dark; AND the native <select> is aria-hidden when enhanced
// but STILL drives value/change; one trigger (no orphan); keyboard + touch; CLOSE.
// RED@200cad4: the selects are unenhanced native (color-scheme:normal, not aria-
// hidden, no custom listbox). GREEN on the integrated build.
import { test, expect, type Page, type BrowserContext } from '@playwright/test';
import { buildGameUrl, makeConfig, dismissLobbyAndTour, ensureConnected } from './_playability';
import { recordEvidence, shot } from './_uat_red';
import { popupTextContrast } from './helpers/png-contrast';

const CHANGSHA_TARGETS = [
  { sel: '.ferro-variant-picker-select.dark-select', label: 'changsha variant-picker (.ferro-variant-picker-select)' },
  { sel: '[data-testid="settings-language-select"]', label: 'changsha settings-language-select' },
  { sel: '[data-testid="rule-preset-picker"]', label: 'changsha rule-preset-picker' },
];

// Open every plausible container so the exact select becomes reachable.
async function revealAll(page: Page) {
  await page.locator('#settings-toggle').first().click({ timeout: 2500 }).catch(() => {});
  await page.waitForTimeout(250);
  await page.locator('#settings-button').first().click({ timeout: 2500 }).catch(() => {});
  await page.waitForTimeout(350);
}

// Locate the target (existence, not visibility). Detect: the Ferro enhancement (a
// non-<select> trigger / dark-listbox), color-scheme, aria-hidden on the native
// select, and optgroups/disabled on the underlying <select>.
async function findSelector(page: Page, sel: string) {
  return page.evaluate((sel) => {
    const el = document.querySelector(sel) as HTMLElement | null;
    if (!el) return null;
    const r = el.getBoundingClientRect();
    const scope = (el.closest('.settings-row, .form-group, .setup-row, .settings-drawer, .settings-drawer-v2, .ferro-variant-picker, div') || el.parentElement || document.body) as HTMLElement;
    const cand = Array.from(scope.querySelectorAll('[role="combobox"],[role="listbox"],[aria-haspopup="listbox"],[data-dark-listbox],[class*="dark-listbox"],[class*="custom-select"],[class*="select-display"],[class*="select-trigger"],[data-enhanced]')) as HTMLElement[];
    const triggers = cand.filter((c) => c.tagName !== 'SELECT' && c.offsetParent !== null);
    const enh = triggers[0] ?? null;
    const er = enh ? enh.getBoundingClientRect() : null;
    const csEl = enh ?? el;
    const colorScheme = String(getComputedStyle(csEl).colorScheme || '');
    const hasDarkListbox = !!(scope.querySelector('[data-dark-listbox="on"], [data-dark-listbox="true"], [class*="dark-listbox"]') || (enh && (enh.getAttribute('data-dark-listbox') === 'on' || /listbox/i.test(enh.className))));
    return {
      sel, tag: el.tagName, x: r.x, y: r.y, w: r.width, h: r.height, visible: el.offsetParent !== null,
      colorScheme, hasDarkListbox,
      nativeAriaHidden: el.getAttribute('aria-hidden') === 'true',
      optgroups: el.querySelectorAll('optgroup').length,
      disabledOptions: el.querySelectorAll('option[disabled]').length,
      triggerCount: triggers.length,
      enhancement: enh ? { x: er!.x, y: er!.y, w: er!.width, h: er!.height, cls: enh.className, role: enh.getAttribute('role') } : null,
    };
  }, sel);
}

// A proper custom listbox popup: role=listbox with role=option children (Ripley).
async function findListboxPopup(page: Page) {
  return page.evaluate(() => {
    const lists = Array.from(document.querySelectorAll('[role="listbox"],[class*="dark-listbox"],.select-popup,.custom-select-options')) as HTMLElement[];
    for (const el of lists) {
      const r = el.getBoundingClientRect(); const cs = getComputedStyle(el);
      const visible = r.width > 20 && r.height > 16 && cs.display !== 'none' && cs.visibility !== 'hidden' && el.offsetParent !== null;
      const options = el.querySelectorAll('[role="option"]').length;
      const groups = el.querySelectorAll('[role="group"],optgroup,.optgroup').length;
      const disabled = el.querySelectorAll('[aria-disabled="true"],[disabled],.disabled').length;
      const isListbox = (el.getAttribute('role') === 'listbox') || /dark-listbox/.test(el.className);
      if (visible && options > 0) return { x: r.x, y: r.y, w: r.width, h: r.height, options, groups, disabled, isListbox };
    }
    return null;
  });
}

async function probeSelector(page: Page, ctx: BrowserContext, sel: string) {
  await revealAll(page);
  const selector = await findSelector(page, sel);
  const isNativeSelect = selector?.tag === 'SELECT' && selector?.visible && !selector?.enhancement;
  const openTarget = selector?.enhancement ?? selector;
  let popup: any = null; let pixel: any = null; let touchOpened = false; let closed = false;
  let valueBefore: string | null = null; let valueAfter: string | null = null; let drivesValue = false;

  if (selector && !isNativeSelect && openTarget) {
    await page.mouse.click(openTarget.x + openTarget.w / 2, openTarget.y + openTarget.h / 2).catch(() => {});
    await page.waitForTimeout(350);
    popup = await findListboxPopup(page);
    if (!popup) { await page.mouse.click(openTarget.x + openTarget.w / 2, openTarget.y + openTarget.h / 2).catch(() => {}); await page.keyboard.press('Enter').catch(() => {}); await page.waitForTimeout(300); popup = await findListboxPopup(page); }
    if (popup) {
      const clip = { x: Math.max(0, popup.x), y: Math.max(0, popup.y), width: Math.min(popup.w, 1440 - popup.x), height: Math.min(popup.h, 900 - popup.y) };
      const buf = await page.screenshot({ clip });
      await shot(page, `g5-${sel.replace(/[^a-z]/gi, '')}-popup.png`);
      try { pixel = popupTextContrast(buf); } catch (e) { pixel = { error: String(e) }; }
    }
    if (ctx) { try { await page.touchscreen.tap(openTarget.x + openTarget.w / 2, openTarget.y + openTarget.h / 2); await page.waitForTimeout(300); touchOpened = !!(await findListboxPopup(page)); } catch { /* */ } }
    // value/change is driven THROUGH the enhancement (the native is aria-hidden);
    // read the underlying <select> value (works even when aria-hidden).
    valueBefore = await page.locator(sel).first().inputValue().catch(() => null);
    await page.mouse.click(openTarget.x + openTarget.w / 2, openTarget.y + openTarget.h / 2).catch(() => {});
    await page.keyboard.press('ArrowDown').catch(() => {}); await page.keyboard.press('Enter').catch(() => {}); await page.waitForTimeout(250);
    valueAfter = await page.locator(sel).first().inputValue().catch(() => null);
    drivesValue = !!valueBefore && !!valueAfter && valueBefore !== valueAfter;
    // CLOSE: Escape dismisses the popup (no orphan)
    await page.keyboard.press('Escape').catch(() => {}); await page.waitForTimeout(250);
    closed = !(await findListboxPopup(page));
  }

  const readablePixels = pixel && !pixel.error && pixel.contrast >= 4.5 && pixel.textCoverage > 0.003;
  const listboxReadable = !!popup && popup.options > 0 && readablePixels;
  const nativeDarkScheme = /\bdark\b/.test(String(selector?.colorScheme || ''));
  const robustReadable = listboxReadable || nativeDarkScheme;
  const enhancedActive = !!selector?.enhancement || !!selector?.hasDarkListbox || nativeDarkScheme;
  // native aria-hidden when enhanced (Ripley) — vacuously true when not enhanced
  const nativeAriaHiddenWhenEnhanced = !enhancedActive || (selector?.nativeAriaHidden === true);
  const touchOperable = touchOpened || (nativeDarkScheme && (selector?.w ?? 0) >= 24 && (selector?.h ?? 0) >= 20);
  const oneTrigger = (selector?.triggerCount ?? 0) === 1;
  const wantsGroups = (selector?.optgroups ?? 0) > 0;
  const wantsDisabled = (selector?.disabledOptions ?? 0) > 0;
  const groupsRendered = !wantsGroups || (!!popup && popup.groups > 0);
  const disabledRendered = !wantsDisabled || (!!popup && popup.disabled > 0);
  return { selector, isNativeSelect, colorScheme: selector?.colorScheme, nativeAriaHidden: selector?.nativeAriaHidden, popup, pixel, readablePixels, listboxReadable, nativeDarkScheme, robustReadable, enhancedActive, nativeAriaHiddenWhenEnhanced, touchOpened, touchOperable, closed, drivesValue, valueBefore, valueAfter, oneTrigger, triggerCount: selector?.triggerCount, groupsRendered, disabledRendered, optgroups: selector?.optgroups, disabledOptions: selector?.disabledOptions };
}

function assertReadable(label: string, r: Awaited<ReturnType<typeof probeSelector>>) {
  expect(r.selector, `${label}: the REAL Changsha-visible select must exist`).not.toBeNull();
  // ROBUST readable: a custom role=listbox (open-able, role=option children, REAL-
  // pixel WCAG>=4.5) OR the control resolves color-scheme:dark. RED@200cad4: native
  // unenhanced (color-scheme:normal, no listbox).
  expect(r.robustReadable, `${label}: readable — a custom role=listbox (open-able, REAL-pixel WCAG>=4.5) OR color-scheme:dark. colorScheme=${r.colorScheme} listbox=${!!r.popup} pixel=${JSON.stringify(r.pixel)}`).toBe(true);
  // native <select> aria-hidden when enhanced, but still drives value/change
  expect(r.nativeAriaHiddenWhenEnhanced, `${label}: the native <select> must be aria-hidden when enhanced; nativeAriaHidden=${r.nativeAriaHidden} enhancedActive=${r.enhancedActive}`).toBe(true);
  expect(r.drivesValue, `${label}: the enhanced control must drive the native value/change (${r.valueBefore} -> ${r.valueAfter})`).toBe(true);
  expect(r.oneTrigger, `${label}: exactly ONE enhanced trigger (no orphan); triggerCount=${r.triggerCount}`).toBe(true);
  expect(r.groupsRendered, `${label}: optgroups must render (select has ${r.optgroups})`).toBe(true);
  expect(r.disabledRendered, `${label}: disabled options must render as disabled (select has ${r.disabledOptions})`).toBe(true);
  expect(r.touchOperable, `${label}: control must be TOUCH-operable`).toBe(true);
  expect(r.closed, `${label}: the popup must CLOSE (Escape) — no orphan`).toBe(true);
}

test.describe('G5 CLOSER — Ferro dark-listbox on the REAL Changsha selects (integrated)', () => {
  for (const tgt of CHANGSHA_TARGETS) {
    test(`${tgt.label} → readable listbox / color-scheme:dark, native aria-hidden, drives value`, async ({ browser }, testInfo) => {
      testInfo.setTimeout(90_000);
      const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, hasTouch: true });
      const page = await ctx.newPage();
      const base = testInfo.project.use.baseURL as string;
      const cfg = makeConfig({ gameId: `g5-cs-${Date.now()}`, variant: 'changsha', dealMode: 'auto', botCount: 3, botDifficulty: 'Medium', handCount: 4 });
      await page.goto(buildGameUrl(base, cfg), { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(1000); await dismissLobbyAndTour(page); await ensureConnected(page).catch(() => {});
      const r = await probeSelector(page, ctx, tgt.sel);
      recordEvidence(`g5-${tgt.sel.replace(/[^a-z]/gi, '')}.json`, { target: tgt.sel, ...r, note: 'Integrated G5 closer on the REAL Changsha select. RED@200cad4: unenhanced native.' });
      assertReadable(tgt.label, r);
      await ctx.close();
    });
  }
});
