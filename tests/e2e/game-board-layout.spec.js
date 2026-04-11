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

    const compass = page.getByLabel('Table compass wind markers');
    await expect(compass).toBeVisible();
    await expect(compass.locator('.compass-marker')).toHaveCount(4);
    await expect(compass.locator('.compass-marker-active')).toHaveCount(1);
    await expect(compass.locator('.compass-marker-active')).toContainText('E');

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

  test('keeps 3D wall and seat tile depth styling intact', async ({ page }) => {
    await page.goto('/game');

    const wallPreview = page.getByLabel('Mahjong wall preview');
    await expect(wallPreview).toBeVisible();

    const wallTransform = await wallPreview.evaluate((el) => getComputedStyle(el).transform);
    expect(wallTransform).not.toBe('none');

    const readWallStackStyle = async (selector, axis) => wallPreview.locator(selector).first().evaluate((el, pseudoAxis) => {
      const style = getComputedStyle(el);
      const upperLayer = getComputedStyle(el, '::after');
      return {
        width: parseFloat(style.width),
        height: parseFloat(style.height),
        upperLayerOffset: parseFloat(pseudoAxis === 'top' ? upperLayer.top : upperLayer.left)
      };
    }, axis);

    const [topStackStyle, bottomStackStyle, leftStackStyle, rightStackStyle] = await Promise.all([
      readWallStackStyle('.wall-top .wall-stack', 'top'),
      readWallStackStyle('.wall-bottom .wall-stack', 'top'),
      readWallStackStyle('.wall-left .wall-stack', 'left'),
      readWallStackStyle('.wall-right .wall-stack', 'left')
    ]);

    expect(topStackStyle.width).toBeGreaterThan(topStackStyle.height);
    expect(bottomStackStyle.width).toBeGreaterThan(bottomStackStyle.height);
    expect(topStackStyle.upperLayerOffset).toBeGreaterThan(0);
    expect(bottomStackStyle.upperLayerOffset).toBeGreaterThan(0);

    expect(leftStackStyle.height).toBeGreaterThan(leftStackStyle.width);
    expect(rightStackStyle.height).toBeGreaterThan(rightStackStyle.width);
    expect(leftStackStyle.upperLayerOffset).toBeGreaterThan(0);
    expect(rightStackStyle.upperLayerOffset).toBeGreaterThan(0);

    const [southTilt, northTilt, eastTilt, westTilt] = await Promise.all([
      page.locator('.seat-tiles.seat-south').first().evaluate((el) => getComputedStyle(el).getPropertyValue('--tile-tilt').trim()),
      page.locator('.seat-tiles.seat-north').first().evaluate((el) => getComputedStyle(el).getPropertyValue('--tile-tilt').trim()),
      page.locator('.seat-tiles.seat-east').first().evaluate((el) => getComputedStyle(el).getPropertyValue('--tile-tilt').trim()),
      page.locator('.seat-tiles.seat-west').first().evaluate((el) => getComputedStyle(el).getPropertyValue('--tile-tilt').trim())
    ]);

    expect(southTilt).toContain('rotateX');
    expect(northTilt).toContain('rotateX(-');
    expect(eastTilt).toContain('rotateY');
    expect(westTilt).toContain('rotateY(-');

    const tileDepthStyle = await page.locator('.seat-tiles.seat-south .tile-shell').first().evaluate((el) => {
      const style = getComputedStyle(el);
      return {
        transformStyle: style.transformStyle,
        boxShadow: style.boxShadow
      };
    });

    expect(tileDepthStyle.transformStyle).toBe('preserve-3d');
    expect(tileDepthStyle.boxShadow).toContain('rgb');
  });
});
