const { test, expect } = require('@playwright/test');

test.describe('Mahjong Claw game board layout smoke', () => {
  test('renders center indicator, seat anchors, wall preview, and discard lane', async ({ page }) => {
    await page.goto('/game');

    await expect(page.getByRole('heading', { name: 'Game', exact: true })).toBeVisible();
    await expect(page.getByLabel('Round and hand indicator')).toContainText('East Round');
    await expect(page.getByLabel('Round and hand indicator')).toContainText('Hand 3');

    const seatAnchors = page.getByLabel('Seat anchors');
    await expect(seatAnchors).toContainText('North');
    await expect(seatAnchors).toContainText('East');
    await expect(seatAnchors).toContainText('South');
    await expect(seatAnchors).toContainText('West');

    const wallPreview = page.getByLabel('Mahjong wall preview');
    await expect(wallPreview).toBeVisible();
    await expect(wallPreview.locator('.wall-side')).toHaveCount(4);
    await expect(wallPreview.locator('.wall-stack-break')).toHaveCount(1);

    const discardLane = page.getByLabel('Center discard lane');
    await expect(discardLane).toBeVisible();
    await expect(discardLane.locator('.tile-shell')).toHaveCount(10);

    await expect(page.getByLabel('Wall legend')).toContainText('break marker');
    await expect(page.getByLabel('Wall legend')).toContainText('consumed stack');
  });
});
