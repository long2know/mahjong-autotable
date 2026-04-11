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
    await expect(wallPreview.locator('.wall-side-dealer')).toHaveCount(1);
    await expect(wallPreview.locator('.wall-corner')).toHaveCount(4);
    await expect(wallPreview.locator('.wall-stack-break')).toHaveCount(1);
    await expect(wallPreview.locator('.wall-stack-break-gap')).toHaveCount(1);

    const discardLane = page.getByLabel('Center discard lane');
    await expect(discardLane).toBeVisible();
    await expect(discardLane.locator('.tile-shell')).toHaveCount(10);

    await expect(page.getByLabel('Wall legend')).toContainText('break marker');
    await expect(page.getByLabel('Wall legend')).toContainText('consumed stack');
    await expect(page.getByLabel('Wall legend')).toContainText('dealer wall');
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

    const cornerStyle = await wallPreview.locator('.wall-corner').first().evaluate((el) => {
      const style = getComputedStyle(el);
      const upperLayer = getComputedStyle(el, '::after');
      return {
        width: parseFloat(style.width),
        height: parseFloat(style.height),
        topOffset: parseFloat(upperLayer.top)
      };
    });

    expect(topStackStyle.width).toBeGreaterThan(topStackStyle.height);
    expect(bottomStackStyle.width).toBeGreaterThan(bottomStackStyle.height);
    expect(topStackStyle.upperLayerOffset).toBeGreaterThan(0);
    expect(bottomStackStyle.upperLayerOffset).toBeGreaterThan(0);

    expect(leftStackStyle.height).toBeGreaterThan(leftStackStyle.width);
    expect(rightStackStyle.height).toBeGreaterThan(rightStackStyle.width);
    expect(leftStackStyle.upperLayerOffset).toBeGreaterThan(0);
    expect(rightStackStyle.upperLayerOffset).toBeGreaterThan(0);

    expect(cornerStyle.width).toBeGreaterThan(0);
    expect(cornerStyle.height).toBeGreaterThan(0);
    expect(cornerStyle.topOffset).toBeGreaterThan(0);

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

    const discardLane = page.getByLabel('Center discard lane');
    const discardTile = page.locator('.discard-lane .tile-shell').first();
    const discardBaseTransform = await discardTile.evaluate((el) => getComputedStyle(el).transform);
    expect(discardBaseTransform).toContain('matrix3d');

    await discardTile.hover();

    const discardHoverTransform = await discardTile.evaluate((el) => getComputedStyle(el).transform);
    expect(discardHoverTransform).toContain('matrix3d');

    await page.setViewportSize({ width: 900, height: 760 });
    const [tabletNorthSeatTransform, tabletSouthSeatTransform, tabletEastSeatTransform, tabletWestSeatTransform, tabletDiscardLaneTransform, tabletCompassTransform] = await Promise.all([
      page.locator('.seat-north .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      discardLane.evaluate((el) => getComputedStyle(el).transform),
      page.getByLabel('Table compass wind markers').evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(tabletNorthSeatTransform).not.toBe('none');
    expect(tabletSouthSeatTransform).not.toBe('none');
    expect(tabletEastSeatTransform).not.toBe('none');
    expect(tabletWestSeatTransform).not.toBe('none');
    expect(tabletDiscardLaneTransform).toContain('matrix3d');
    expect(tabletCompassTransform).toContain('matrix3d');

    await page.setViewportSize({ width: 1280, height: 720 });
    const [northSeatTransform, southSeatTransform, eastSeatTransform, westSeatTransform] = await Promise.all([
      page.locator('.seat-north .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-card').first().evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(northSeatTransform).not.toBe('none');
    expect(southSeatTransform).not.toBe('none');
    expect(eastSeatTransform).not.toBe('none');
    expect(westSeatTransform).not.toBe('none');

    const [indicatorTransform, anchorTransform, discardLaneTransform, discardLaneTransformStyle, compassTransform] = await Promise.all([
      page.getByLabel('Round and hand indicator').evaluate((el) => getComputedStyle(el).transform),
      page.getByLabel('Seat anchors').evaluate((el) => getComputedStyle(el).transform),
      discardLane.evaluate((el) => getComputedStyle(el).transform),
      discardLane.evaluate((el) => getComputedStyle(el).transformStyle),
      page.getByLabel('Table compass wind markers').evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(indicatorTransform).not.toBe('none');
    expect(anchorTransform).not.toBe('none');
    expect(discardLaneTransform).toContain('matrix3d');
    expect(discardLaneTransformStyle).toBe('preserve-3d');
    expect(compassTransform).toContain('matrix3d');

    const [northCompassMarkerTransform, eastCompassMarkerTransform, southCompassMarkerTransform, westCompassMarkerTransform] = await Promise.all([
      page.locator('.compass-north').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-east').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-south').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-west').evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(northCompassMarkerTransform).not.toBe('none');
    expect(eastCompassMarkerTransform).not.toBe('none');
    expect(southCompassMarkerTransform).not.toBe('none');
    expect(westCompassMarkerTransform).not.toBe('none');

    const [topWallTransform, bottomWallTransform, leftWallTransform, rightWallTransform] = await Promise.all([
      wallPreview.locator('.wall-top').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right').evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(topWallTransform).toContain('matrix3d');
    expect(bottomWallTransform).toContain('matrix3d');
    expect(leftWallTransform).toContain('matrix3d');
    expect(rightWallTransform).toContain('matrix3d');

    const [northWestCornerTransform, northEastCornerTransform, southEastCornerTransform, southWestCornerTransform] = await Promise.all([
      wallPreview.locator('.wall-corner-nw').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-corner-ne').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-corner-se').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-corner-sw').evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(northWestCornerTransform).toContain('matrix3d');
    expect(northEastCornerTransform).toContain('matrix3d');
    expect(southEastCornerTransform).toContain('matrix3d');
    expect(southWestCornerTransform).toContain('matrix3d');
  });
});
