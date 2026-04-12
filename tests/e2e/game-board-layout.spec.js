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
    const [
      tabletNorthSeatTransform,
      tabletSouthSeatTransform,
      tabletEastSeatTransform,
      tabletWestSeatTransform,
      tabletNorthSeatIdentityTransform,
      tabletSouthSeatIdentityTransform,
      tabletEastSeatIdentityTransform,
      tabletWestSeatIdentityTransform,
      tabletNorthSeatBadgesTransform,
      tabletSouthSeatBadgesTransform,
      tabletEastSeatBadgesTransform,
      tabletWestSeatBadgesTransform,
      tabletNorthAnchorTransform,
      tabletSouthAnchorTransform,
      tabletEastAnchorTransform,
      tabletWestAnchorTransform,
      tabletDiscardLaneTransform,
      tabletCompassTransform,
      tabletNorthCompassMarkerTransform,
      tabletEastCompassMarkerTransform,
      tabletSouthCompassMarkerTransform,
      tabletWestCompassMarkerTransform,
      tabletNorthCompassMarkerLayerTransform,
      tabletEastCompassMarkerLayerTransform,
      tabletSouthCompassMarkerLayerTransform,
      tabletWestCompassMarkerLayerTransform,
      tabletWallLegendBreakTransform,
      tabletWallLegendDepletedTransform,
      tabletWallLegendDealerTransform,
      tabletWallLegendRemainingTransform,
      tabletWallLegendBreakStackTransform,
      tabletWallLegendDepletedStackTransform,
      tabletWallLegendDealerStackTransform,
      tabletTopWallTransform,
      tabletBottomWallTransform,
      tabletLeftWallTransform,
      tabletRightWallTransform,
      tabletTopWallStackTransform,
      tabletBottomWallStackTransform,
      tabletLeftWallStackTransform,
      tabletRightWallStackTransform,
      tabletDiscardTileOneTransform,
      tabletDiscardTileTwoTransform,
      tabletDiscardTileThreeTransform,
      tabletDiscardTileFourTransform
    ] = await Promise.all([
      page.locator('.seat-north .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-north .seat-identity').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-identity').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-identity').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-identity').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-north .seat-badges').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-badges').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-badges').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-badges').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-anchors .anchor-north').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-anchors .anchor-south').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-anchors .anchor-east').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-anchors .anchor-west').evaluate((el) => getComputedStyle(el).transform),
      discardLane.evaluate((el) => getComputedStyle(el).transform),
      page.getByLabel('Table compass wind markers').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-north').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-east').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-south').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-west').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-north').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.compass-east').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.compass-south').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.compass-west').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.wall-legend-item-break').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-depleted').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-dealer').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-remaining').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-break .wall-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-depleted .wall-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-dealer .wall-stack').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(0).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(1).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(2).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(3).evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(tabletNorthSeatTransform).not.toBe('none');
    expect(tabletSouthSeatTransform).not.toBe('none');
    expect(tabletEastSeatTransform).not.toBe('none');
    expect(tabletWestSeatTransform).not.toBe('none');
    expect(tabletNorthSeatIdentityTransform).toContain('matrix3d');
    expect(tabletSouthSeatIdentityTransform).toContain('matrix3d');
    expect(tabletEastSeatIdentityTransform).toContain('matrix3d');
    expect(tabletWestSeatIdentityTransform).toContain('matrix3d');
    expect(tabletNorthSeatIdentityTransform).not.toBe(tabletSouthSeatIdentityTransform);
    expect(tabletEastSeatIdentityTransform).not.toBe(tabletWestSeatIdentityTransform);
    expect(tabletNorthSeatBadgesTransform).toContain('matrix3d');
    expect(tabletSouthSeatBadgesTransform).toContain('matrix3d');
    expect(tabletEastSeatBadgesTransform).toContain('matrix3d');
    expect(tabletWestSeatBadgesTransform).toContain('matrix3d');
    expect(tabletNorthSeatBadgesTransform).not.toBe(tabletSouthSeatBadgesTransform);
    expect(tabletEastSeatBadgesTransform).not.toBe(tabletWestSeatBadgesTransform);
    expect(tabletNorthAnchorTransform).toContain('matrix3d');
    expect(tabletSouthAnchorTransform).toContain('matrix3d');
    expect(tabletEastAnchorTransform).toContain('matrix3d');
    expect(tabletWestAnchorTransform).toContain('matrix3d');
    expect(tabletDiscardLaneTransform).toContain('matrix3d');
    expect(tabletCompassTransform).toContain('matrix3d');
    expect(tabletNorthCompassMarkerTransform).toContain('matrix3d');
    expect(tabletEastCompassMarkerTransform).toContain('matrix3d');
    expect(tabletSouthCompassMarkerTransform).toContain('matrix3d');
    expect(tabletWestCompassMarkerTransform).toContain('matrix3d');
    expect(tabletNorthCompassMarkerTransform).not.toBe(tabletSouthCompassMarkerTransform);
    expect(tabletEastCompassMarkerTransform).not.toBe(tabletWestCompassMarkerTransform);
    expect(tabletNorthCompassMarkerLayerTransform).toContain('matrix3d');
    expect(tabletEastCompassMarkerLayerTransform).toContain('matrix3d');
    expect(tabletSouthCompassMarkerLayerTransform).toContain('matrix3d');
    expect(tabletWestCompassMarkerLayerTransform).toContain('matrix3d');
    expect(tabletNorthCompassMarkerLayerTransform).not.toBe(tabletSouthCompassMarkerLayerTransform);
    expect(tabletEastCompassMarkerLayerTransform).not.toBe(tabletWestCompassMarkerLayerTransform);
    expect(tabletWallLegendBreakTransform).toContain('matrix3d');
    expect(tabletWallLegendDepletedTransform).toContain('matrix3d');
    expect(tabletWallLegendDealerTransform).toContain('matrix3d');
    expect(tabletWallLegendRemainingTransform).toContain('matrix3d');
    expect(tabletWallLegendBreakStackTransform).toContain('matrix3d');
    expect(tabletWallLegendDepletedStackTransform).toContain('matrix3d');
    expect(tabletWallLegendDealerStackTransform).toContain('matrix3d');
    expect(tabletWallLegendDepletedTransform).not.toBe(tabletWallLegendDealerTransform);
    expect(tabletWallLegendBreakTransform).not.toBe(tabletWallLegendRemainingTransform);
    expect(tabletWallLegendDepletedStackTransform).not.toBe(tabletWallLegendDealerStackTransform);
    expect(tabletWallLegendBreakStackTransform).not.toBe(tabletWallLegendDealerStackTransform);
    expect(tabletTopWallTransform).toContain('matrix3d');
    expect(tabletBottomWallTransform).toContain('matrix3d');
    expect(tabletLeftWallTransform).toContain('matrix3d');
    expect(tabletRightWallTransform).toContain('matrix3d');
    expect(tabletTopWallStackTransform).toContain('matrix3d');
    expect(tabletBottomWallStackTransform).toContain('matrix3d');
    expect(tabletLeftWallStackTransform).toContain('matrix3d');
    expect(tabletRightWallStackTransform).toContain('matrix3d');
    expect(tabletDiscardTileOneTransform).toContain('matrix3d');
    expect(tabletDiscardTileTwoTransform).toContain('matrix3d');
    expect(tabletDiscardTileThreeTransform).toContain('matrix3d');
    expect(tabletDiscardTileFourTransform).toContain('matrix3d');
    expect(tabletDiscardTileOneTransform).not.toBe(tabletDiscardTileTwoTransform);
    expect(tabletDiscardTileThreeTransform).not.toBe(tabletDiscardTileFourTransform);

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

    const seatCardDepthStyles = await Promise.all([
      page.locator('.seat-north .seat-card').first().evaluate((el) => {
        const style = getComputedStyle(el);
        return { transformStyle: style.transformStyle, boxShadow: style.boxShadow };
      }),
      page.locator('.seat-south .seat-card').first().evaluate((el) => {
        const style = getComputedStyle(el);
        return { transformStyle: style.transformStyle, boxShadow: style.boxShadow };
      }),
      page.locator('.seat-east .seat-card').first().evaluate((el) => {
        const style = getComputedStyle(el);
        return { transformStyle: style.transformStyle, boxShadow: style.boxShadow };
      }),
      page.locator('.seat-west .seat-card').first().evaluate((el) => {
        const style = getComputedStyle(el);
        return { transformStyle: style.transformStyle, boxShadow: style.boxShadow };
      })
    ]);

    seatCardDepthStyles.forEach((seatCardStyle) => {
      expect(seatCardStyle.transformStyle).toBe('preserve-3d');
      expect(seatCardStyle.boxShadow).toContain('rgb');
    });

    const [
      northSeatHeaderTransform,
      southSeatHeaderTransform,
      eastSeatHeaderTransform,
      westSeatHeaderTransform,
      northSeatIdentityTransform,
      southSeatIdentityTransform,
      eastSeatIdentityTransform,
      westSeatIdentityTransform,
      northSeatBadgesTransform,
      southSeatBadgesTransform,
      eastSeatBadgesTransform,
      westSeatBadgesTransform
    ] = await Promise.all([
      page.locator('.seat-north .seat-card > .d-flex').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-card > .d-flex').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-card > .d-flex').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-card > .d-flex').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-north .seat-identity').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-identity').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-identity').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-identity').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-north .seat-badges').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-badges').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-badges').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-badges').first().evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(northSeatHeaderTransform).toContain('matrix3d');
    expect(southSeatHeaderTransform).toContain('matrix3d');
    expect(eastSeatHeaderTransform).toContain('matrix3d');
    expect(westSeatHeaderTransform).toContain('matrix3d');
    expect(northSeatIdentityTransform).toContain('matrix3d');
    expect(southSeatIdentityTransform).toContain('matrix3d');
    expect(eastSeatIdentityTransform).toContain('matrix3d');
    expect(westSeatIdentityTransform).toContain('matrix3d');
    expect(northSeatIdentityTransform).not.toBe(southSeatIdentityTransform);
    expect(eastSeatIdentityTransform).not.toBe(westSeatIdentityTransform);
    expect(northSeatBadgesTransform).toContain('matrix3d');
    expect(southSeatBadgesTransform).toContain('matrix3d');
    expect(eastSeatBadgesTransform).toContain('matrix3d');
    expect(westSeatBadgesTransform).toContain('matrix3d');
    expect(northSeatBadgesTransform).not.toBe(southSeatBadgesTransform);
    expect(eastSeatBadgesTransform).not.toBe(westSeatBadgesTransform);

    const [northSeatRowTransform, southSeatRowTransform, eastSeatRowTransform, westSeatRowTransform] = await Promise.all([
      page.locator('.seat-north .seat-card .tile-row').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-card .tile-row').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-card .tile-row').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-card .tile-row').first().evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(northSeatRowTransform).toContain('matrix3d');
    expect(southSeatRowTransform).toContain('matrix3d');
    expect(eastSeatRowTransform).toContain('matrix3d');
    expect(westSeatRowTransform).toContain('matrix3d');

    const [
      indicatorTransform,
      indicatorTransformStyle,
      indicatorWindTransform,
      indicatorHandTransform,
      anchorTransform,
      anchorTransformStyle,
      northAnchorTransform,
      eastAnchorTransform,
      southAnchorTransform,
      westAnchorTransform,
      discardLaneTransform,
      discardLaneTransformStyle,
      compassTransform,
      wallLegendBreakTransform,
      wallLegendDepletedTransform,
      wallLegendDealerTransform,
      wallLegendRemainingTransform,
      wallLegendBreakStackTransform,
      wallLegendDepletedStackTransform,
      wallLegendDealerStackTransform
    ] = await Promise.all([
      page.getByLabel('Round and hand indicator').evaluate((el) => getComputedStyle(el).transform),
      page.getByLabel('Round and hand indicator').evaluate((el) => getComputedStyle(el).transformStyle),
      page.locator('.table-indicator .indicator-wind').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.table-indicator .indicator-hand').evaluate((el) => getComputedStyle(el).transform),
      page.getByLabel('Seat anchors').evaluate((el) => getComputedStyle(el).transform),
      page.getByLabel('Seat anchors').evaluate((el) => getComputedStyle(el).transformStyle),
      page.locator('.seat-anchors .anchor-north').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-anchors .anchor-east').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-anchors .anchor-south').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-anchors .anchor-west').evaluate((el) => getComputedStyle(el).transform),
      discardLane.evaluate((el) => getComputedStyle(el).transform),
      discardLane.evaluate((el) => getComputedStyle(el).transformStyle),
      page.getByLabel('Table compass wind markers').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-break').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-depleted').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-dealer').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-remaining').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-break .wall-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-depleted .wall-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-dealer .wall-stack').evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(indicatorTransform).toContain('matrix3d');
    expect(indicatorTransformStyle).toBe('preserve-3d');
    expect(indicatorWindTransform).toContain('matrix3d');
    expect(indicatorHandTransform).toContain('matrix3d');
    expect(indicatorWindTransform).not.toBe(indicatorHandTransform);
    expect(anchorTransform).not.toBe('none');
    expect(anchorTransformStyle).toBe('preserve-3d');
    expect(northAnchorTransform).toContain('matrix3d');
    expect(eastAnchorTransform).toContain('matrix3d');
    expect(southAnchorTransform).toContain('matrix3d');
    expect(westAnchorTransform).toContain('matrix3d');
    expect(discardLaneTransform).toContain('matrix3d');
    expect(discardLaneTransformStyle).toBe('preserve-3d');
    expect(compassTransform).toContain('matrix3d');
    expect(wallLegendBreakTransform).toContain('matrix3d');
    expect(wallLegendDepletedTransform).toContain('matrix3d');
    expect(wallLegendDealerTransform).toContain('matrix3d');
    expect(wallLegendRemainingTransform).toContain('matrix3d');
    expect(wallLegendBreakStackTransform).toContain('matrix3d');
    expect(wallLegendDepletedStackTransform).toContain('matrix3d');
    expect(wallLegendDealerStackTransform).toContain('matrix3d');
    expect(wallLegendDepletedTransform).not.toBe(wallLegendDealerTransform);
    expect(wallLegendBreakTransform).not.toBe(wallLegendRemainingTransform);
    expect(wallLegendDepletedStackTransform).not.toBe(wallLegendDealerStackTransform);
    expect(wallLegendBreakStackTransform).not.toBe(wallLegendDealerStackTransform);

    const [
      northCompassMarkerTransform,
      eastCompassMarkerTransform,
      southCompassMarkerTransform,
      westCompassMarkerTransform,
      northCompassMarkerLayerTransform,
      eastCompassMarkerLayerTransform,
      southCompassMarkerLayerTransform,
      westCompassMarkerLayerTransform
    ] = await Promise.all([
      page.locator('.compass-north').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-east').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-south').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-west').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.compass-north').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.compass-east').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.compass-south').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.compass-west').evaluate((el) => getComputedStyle(el, '::before').transform)
    ]);

    expect(northCompassMarkerTransform).toContain('matrix3d');
    expect(eastCompassMarkerTransform).toContain('matrix3d');
    expect(southCompassMarkerTransform).toContain('matrix3d');
    expect(westCompassMarkerTransform).toContain('matrix3d');
    expect(northCompassMarkerTransform).not.toBe(southCompassMarkerTransform);
    expect(eastCompassMarkerTransform).not.toBe(westCompassMarkerTransform);
    expect(northCompassMarkerLayerTransform).toContain('matrix3d');
    expect(eastCompassMarkerLayerTransform).toContain('matrix3d');
    expect(southCompassMarkerLayerTransform).toContain('matrix3d');
    expect(westCompassMarkerLayerTransform).toContain('matrix3d');
    expect(northCompassMarkerLayerTransform).not.toBe(southCompassMarkerLayerTransform);
    expect(eastCompassMarkerLayerTransform).not.toBe(westCompassMarkerLayerTransform);

    const [
      topWallTransform,
      bottomWallTransform,
      leftWallTransform,
      rightWallTransform,
      topWallStackTransform,
      bottomWallStackTransform,
      leftWallStackTransform,
      rightWallStackTransform
    ] = await Promise.all([
      wallPreview.locator('.wall-top').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right .wall-stack').first().evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(topWallTransform).toContain('matrix3d');
    expect(bottomWallTransform).toContain('matrix3d');
    expect(leftWallTransform).toContain('matrix3d');
    expect(rightWallTransform).toContain('matrix3d');
    expect(topWallStackTransform).toContain('matrix3d');
    expect(bottomWallStackTransform).toContain('matrix3d');
    expect(leftWallStackTransform).toContain('matrix3d');
    expect(rightWallStackTransform).toContain('matrix3d');

    const [topWallLabelTransform, bottomWallLabelTransform, leftWallLabelTransform, rightWallLabelTransform] = await Promise.all([
      wallPreview.locator('.wall-top .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right .wall-side-label').evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(topWallLabelTransform).toContain('matrix3d');
    expect(bottomWallLabelTransform).toContain('matrix3d');
    expect(leftWallLabelTransform).toContain('matrix3d');
    expect(rightWallLabelTransform).toContain('matrix3d');

    const [
      northWestCornerTransform,
      northEastCornerTransform,
      southEastCornerTransform,
      southWestCornerTransform,
      northWestCornerUpperTransform,
      northEastCornerUpperTransform,
      southEastCornerUpperTransform,
      southWestCornerUpperTransform
    ] = await Promise.all([
      wallPreview.locator('.wall-corner-nw').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-corner-ne').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-corner-se').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-corner-sw').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-corner-nw').evaluate((el) => getComputedStyle(el, '::after').transform),
      wallPreview.locator('.wall-corner-ne').evaluate((el) => getComputedStyle(el, '::after').transform),
      wallPreview.locator('.wall-corner-se').evaluate((el) => getComputedStyle(el, '::after').transform),
      wallPreview.locator('.wall-corner-sw').evaluate((el) => getComputedStyle(el, '::after').transform)
    ]);

    expect(northWestCornerTransform).toContain('matrix3d');
    expect(northEastCornerTransform).toContain('matrix3d');
    expect(southEastCornerTransform).toContain('matrix3d');
    expect(southWestCornerTransform).toContain('matrix3d');
    expect(northWestCornerUpperTransform).toContain('matrix3d');
    expect(northEastCornerUpperTransform).toContain('matrix3d');
    expect(southEastCornerUpperTransform).toContain('matrix3d');
    expect(southWestCornerUpperTransform).toContain('matrix3d');
    expect(northWestCornerUpperTransform).not.toBe(northEastCornerUpperTransform);
    expect(southWestCornerUpperTransform).not.toBe(southEastCornerUpperTransform);
    expect(northWestCornerUpperTransform).not.toBe(southWestCornerUpperTransform);
  });
});
