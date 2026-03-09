const { test, expect } = require('@playwright/test');

const apiBaseUrl = process.env.PLAYWRIGHT_API_URL || 'http://localhost:5270';

const seats = ['East', 'South', 'West', 'North'];

test.describe('Mahjong Claw lobby -> game smoke', () => {

  test('creates in lobby UI, fills seats via API, then starts and opens game board', async ({ page, request }) => {
    await page.goto('/lobby');

    const createCard = page.locator('.card').filter({ hasText: 'Create Game' }).first();
    await createCard.getByPlaceholder('Enter your name').fill('SmokeHost');
    await createCard.getByRole('button', { name: 'Create' }).click();

    const statusAlert = page.locator('.alert').filter({ hasText: 'Game created:' }).first();
    await expect(statusAlert).toBeVisible();

    const statusText = (await statusAlert.textContent()) ?? '';
    const gameIdMatch = statusText.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
    expect(gameIdMatch).toBeTruthy();
    const gameId = gameIdMatch[0];

    let southPlayerId;

    for (const seat of seats) {
      const joinResponse = await request.post(`${apiBaseUrl}/api/game/${gameId}/join`, {
        data: {
          name: `Smoke-${seat}`,
          seat
        }
      });

      expect(joinResponse.ok()).toBeTruthy();

      const joined = await joinResponse.json();
      if (seat === 'South') {
        southPlayerId = joined.playerId;
      }
    }

    expect(southPlayerId).toBeTruthy();

    const joinCard = page.locator('.card').filter({ hasText: 'Join / Start Game' }).first();
    await joinCard.getByPlaceholder('Enter your name').fill('Smoke-South');
    await joinCard.locator('select').selectOption('South');

    await joinCard.getByRole('button', { name: 'Start' }).click();
    await expect(page.locator('.alert.alert-info').first()).toContainText('Game started.');

    await joinCard.getByRole('button', { name: 'Open Game' }).click();

    await expect(page).toHaveURL(new RegExp(`/game\\?gameId=${gameId}`));
    await expect(page.getByRole('heading', { name: 'Game', exact: true })).toBeVisible();
    await expect(page.getByText(`Table ID: ${gameId.slice(0, 8)}`)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Your Hand' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Draw' })).toBeVisible();
  });
});
