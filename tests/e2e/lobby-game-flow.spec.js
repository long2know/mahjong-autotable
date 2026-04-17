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

    // Query state to find which seat the host already occupies
    const stateResponse = await request.get(`${apiBaseUrl}/api/game/${gameId}/state`);
    expect(stateResponse.ok()).toBeTruthy();

    const gameState = await stateResponse.json();
    const hostSeats = new Set(gameState.players.map(p => p.seat));

    let southPlayerId;

    for (const seat of seats) {
      // Skip seats that are already occupied
      if (hostSeats.has(seat)) {
        continue;
      }

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
    await expect(page.getByRole('button', { name: 'Draw', exact: true })).toBeVisible();
  });

  test('creates human vs bot game and auto-seats bot players', async ({ page, request }) => {
    await page.goto('/lobby');

    const createCard = page.locator('.card').filter({ hasText: 'Create Game' }).first();
    await createCard.getByPlaceholder('Enter your name').fill('BotHost');

    const modeSelect = createCard.locator('label', { hasText: 'Game mode' }).locator('..').locator('select');
    await modeSelect.selectOption('HumanVsBot');

    const botSeatSelect = createCard.locator('label', { hasText: 'Bot seats' }).locator('..').locator('select');
    await botSeatSelect.selectOption('2');

    await createCard.getByRole('button', { name: 'Create' }).click();

    const statusAlert = page.locator('.alert').filter({ hasText: 'Game created:' }).first();
    await expect(statusAlert).toBeVisible();
    await expect(statusAlert).toContainText('Human vs Bot');
    await expect(statusAlert).toContainText('2 bot seats');

    const statusText = (await statusAlert.textContent()) ?? '';
    const gameIdMatch = statusText.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
    expect(gameIdMatch).toBeTruthy();
    const gameId = gameIdMatch[0];

    const stateResponse = await request.get(`${apiBaseUrl}/api/game/${gameId}/state`);
    expect(stateResponse.ok()).toBeTruthy();

    const state = await stateResponse.json();
    const playerNames = state.players.map(player => player.name);
    const playerSeats = state.players.map(player => player.seat);

    expect(state.players).toHaveLength(3);
    expect(playerNames).toEqual(expect.arrayContaining(['BotHost', 'Bot 1', 'Bot 2']));
    expect(playerSeats).toEqual(expect.arrayContaining(['East', 'South', 'West']));
  });
});
