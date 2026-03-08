const { test, expect } = require('@playwright/test');

test.describe('Mahjong Claw UI smoke', () => {

  test('home page renders hero', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Mahjong Claw' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Enter Lobby' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'View Game Board' })).toBeVisible();
  });
});
