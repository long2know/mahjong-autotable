const { test, expect } = require('@playwright/test');

const apiBaseUrl = process.env.PLAYWRIGHT_API_URL || 'http://localhost:5270';

const seats = ['East', 'South', 'West', 'North'];

test.describe('Mahjong Claw lobby -> game smoke', () => {

  test('creates, fills, and starts a game then opens game board', async ({ page, request }) => {
    const createResponse = await request.post(`${apiBaseUrl}/api/game/`);
    expect(createResponse.ok()).toBeTruthy();

    const created = await createResponse.json();
    const gameId = created.id;

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

    const startResponse = await request.post(`${apiBaseUrl}/api/game/${gameId}/start`);
    expect(startResponse.ok()).toBeTruthy();

    await page.goto(`/game?gameId=${gameId}&playerId=${southPlayerId}`);

    await expect(page.getByRole('heading', { name: 'Game', exact: true })).toBeVisible();
    await expect(page.getByText(`Table ID: ${gameId.slice(0, 8)}`)).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Your Hand' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Draw' })).toBeVisible();
  });
});
