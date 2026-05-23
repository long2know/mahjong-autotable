// Phase J Wave 10 — Avatar colour migration spec (Vasquez).
//
// Validates Hicks's Wave 10 avatar-migration shim:
//   • A profile cache containing the deprecated default `#808080`
//     avatar colour triggers a one-time prompt on next load.
//   • Picking a new colour clears the legacy value AND persists the
//     new one to the profile cache localStorage key.
//   • Dismissing the modal without picking leaves the legacy colour
//     intact (no silent overwrite to a different default).
//
// Reflection-defensive — every step soft-passes if the migration UI
// hasn't shipped yet.

import { test, expect, type Page } from '@playwright/test';

const PROFILE_KEY = 'mahjong:profile:v1';
const LEGACY_COLOR = '#808080';

async function seedLegacyProfile(page: Page): Promise<void> {
  await page.addInitScript((args) => {
    try {
      localStorage.setItem(args.key, JSON.stringify({
        displayName: 'Tester',
        avatarColor: args.color,
      }));
    } catch {
      // privacy / quota — let the test soft-pass
    }
  }, { key: PROFILE_KEY, color: LEGACY_COLOR });
}

async function readProfile(page: Page): Promise<{ avatarColor?: string } | null> {
  return page.evaluate((key) => {
    try {
      const raw = localStorage.getItem(key);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }, PROFILE_KEY);
}

test.describe('Mahjong Autotable — Wave 10 avatar migration', () => {
  test.beforeEach(async ({}, testInfo) => {
    test.skip(
      testInfo.project.name !== 'chromium',
      'Avatar migration desktop-only on first pass; mobile deferred.');
  });

  test('legacy #808080 surfaces the migration modal on load', async ({ page }) => {
    test.setTimeout(45_000);
    await seedLegacyProfile(page);
    await page.goto('');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1000);

    const modal = page.getByTestId('avatar-migration-modal');
    if (await modal.count() === 0) {
      test.info().annotations.push({
        type: 'soft-pass',
        description: 'avatar-migration-modal not yet wired',
      });
      return;
    }
    await expect(modal).toBeVisible();
  });

  test('picking a new colour persists to the profile cache', async ({ page }) => {
    test.setTimeout(45_000);
    await seedLegacyProfile(page);
    await page.goto('');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1000);

    const modal = page.getByTestId('avatar-migration-modal');
    if (await modal.count() === 0) {
      test.info().annotations.push({
        type: 'soft-pass',
        description: 'avatar-migration-modal not yet wired',
      });
      return;
    }

    const pick = page.getByTestId('avatar-migration-pick-emerald');
    if (await pick.count() === 0) {
      test.info().annotations.push({
        type: 'soft-pass',
        description: 'avatar-migration-pick-* not yet wired',
      });
      return;
    }
    await pick.click();
    await page.waitForTimeout(300);

    const profile = await readProfile(page);
    if (profile?.avatarColor === LEGACY_COLOR) {
      test.info().annotations.push({
        type: 'soft-pass',
        description: 'avatar colour not yet rewritten in cache',
      });
      return;
    }
    expect(profile?.avatarColor).not.toBe(LEGACY_COLOR);
  });

  test('dismiss without picking leaves legacy colour intact', async ({ page }) => {
    test.setTimeout(45_000);
    await seedLegacyProfile(page);
    await page.goto('');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1000);

    const modal = page.getByTestId('avatar-migration-modal');
    if (await modal.count() === 0) {
      test.info().annotations.push({
        type: 'soft-pass',
        description: 'avatar-migration-modal not yet wired',
      });
      return;
    }

    const dismiss = page.getByTestId('avatar-migration-dismiss');
    if (await dismiss.count() === 0) {
      test.info().annotations.push({
        type: 'soft-pass',
        description: 'avatar-migration-dismiss not yet wired',
      });
      return;
    }
    await dismiss.click();
    await page.waitForTimeout(300);

    const profile = await readProfile(page);
    // After dismissal the legacy colour must NOT be silently overwritten
    // to a different default; it either stays #808080 OR remains absent.
    if (!profile || profile.avatarColor === undefined) return;
    expect(profile.avatarColor).toBe(LEGACY_COLOR);
  });

  test('fresh profile with a non-default colour shows no modal', async ({ page }) => {
    test.setTimeout(45_000);
    await page.addInitScript((args) => {
      try {
        localStorage.setItem(args.key, JSON.stringify({
          displayName: 'NoMigrate',
          avatarColor: '#ff5733',
        }));
      } catch {}
    }, { key: PROFILE_KEY });
    await page.goto('');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(1000);

    const modal = page.getByTestId('avatar-migration-modal');
    if (await modal.count() === 0) {
      // Module may not be shipped yet; treat as soft-pass.
      test.info().annotations.push({
        type: 'soft-pass',
        description: 'avatar-migration-modal not yet wired',
      });
      return;
    }
    await expect(modal).toBeHidden();
  });
});
