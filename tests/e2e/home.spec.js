const { test, expect } = require('@playwright/test');

const baseUrl = process.env.PLAYWRIGHT_BASE_URL;

test.describe('Mahjong Claw UI smoke', () => {
  test.beforeEach(() => {
    if (!baseUrl) {
      test.skip(true, 'PLAYWRIGHT_BASE_URL not set');
    }
  });

  test('home page renders hero', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Mahjong Claw' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Enter Lobby' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'View Game Board' })).toBeVisible();
  });
});
