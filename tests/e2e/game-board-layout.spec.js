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

  test('keeps directional 3D wall stack side-face transforms at tablet and desktop breakpoints', async ({ page }) => {
    await page.goto('/game');

    const wallPreview = page.getByLabel('Mahjong wall preview');
    await expect(wallPreview).toBeVisible();

    const readWallSideFaceTransforms = async () => Promise.all([
      wallPreview.locator('.wall-top .wall-stack').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      wallPreview.locator('.wall-bottom .wall-stack').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      wallPreview.locator('.wall-left .wall-stack').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      wallPreview.locator('.wall-right .wall-stack').first().evaluate((el) => getComputedStyle(el, '::after').transform)
    ]);

    await page.setViewportSize({ width: 900, height: 760 });
    const [tabletTopFaceTransform, tabletBottomFaceTransform, tabletLeftFaceTransform, tabletRightFaceTransform] = await readWallSideFaceTransforms();

    expect(tabletTopFaceTransform).toContain('matrix3d');
    expect(tabletBottomFaceTransform).toContain('matrix3d');
    expect(tabletLeftFaceTransform).toContain('matrix3d');
    expect(tabletRightFaceTransform).toContain('matrix3d');
    expect(tabletTopFaceTransform).not.toBe(tabletBottomFaceTransform);
    expect(tabletLeftFaceTransform).not.toBe(tabletRightFaceTransform);

    await page.setViewportSize({ width: 1280, height: 720 });
    const [desktopTopFaceTransform, desktopBottomFaceTransform, desktopLeftFaceTransform, desktopRightFaceTransform] = await readWallSideFaceTransforms();

    expect(desktopTopFaceTransform).toContain('matrix3d');
    expect(desktopBottomFaceTransform).toContain('matrix3d');
    expect(desktopLeftFaceTransform).toContain('matrix3d');
    expect(desktopRightFaceTransform).toContain('matrix3d');
    expect(desktopTopFaceTransform).not.toBe(desktopBottomFaceTransform);
    expect(desktopLeftFaceTransform).not.toBe(desktopRightFaceTransform);

    expect(desktopTopFaceTransform).not.toBe(tabletTopFaceTransform);
    expect(desktopBottomFaceTransform).not.toBe(tabletBottomFaceTransform);
    expect(desktopLeftFaceTransform).not.toBe(tabletLeftFaceTransform);
    expect(desktopRightFaceTransform).not.toBe(tabletRightFaceTransform);
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
      tabletNorthSeatLayerTransform,
      tabletSouthSeatLayerTransform,
      tabletEastSeatLayerTransform,
      tabletWestSeatLayerTransform,
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
      tabletNorthAnchorLayerTransform,
      tabletSouthAnchorLayerTransform,
      tabletEastAnchorLayerTransform,
      tabletWestAnchorLayerTransform,
      tabletCenterInsetTransform,
      tabletCenterRingTransform,
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
      tabletWallLegendRemainingValueTransform,
      tabletTopWallTransform,
      tabletBottomWallTransform,
      tabletLeftWallTransform,
      tabletRightWallTransform,
      tabletTopWallStackTransform,
      tabletBottomWallStackTransform,
      tabletLeftWallStackTransform,
      tabletRightWallStackTransform,
      tabletTopWallStackHighlightTransform,
      tabletBottomWallStackHighlightTransform,
      tabletLeftWallStackHighlightTransform,
      tabletRightWallStackHighlightTransform,
      tabletTopWallLabelTransform,
      tabletBottomWallLabelTransform,
      tabletLeftWallLabelTransform,
      tabletRightWallLabelTransform,
      tabletTopWallLabelLayerTransform,
      tabletBottomWallLabelLayerTransform,
      tabletLeftWallLabelLayerTransform,
      tabletRightWallLabelLayerTransform,
      tabletNorthSeatTileHighlightTransform,
      tabletSouthSeatTileHighlightTransform,
      tabletEastSeatTileHighlightTransform,
      tabletWestSeatTileHighlightTransform,
      tabletNorthSeatTileFrameTransform,
      tabletSouthSeatTileFrameTransform,
      tabletEastSeatTileFrameTransform,
      tabletWestSeatTileFrameTransform,
      tabletDiscardTileOneTransform,
      tabletDiscardTileTwoTransform,
      tabletDiscardTileThreeTransform,
      tabletDiscardTileFourTransform
    ] = await Promise.all([
      page.locator('.seat-north .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-north .seat-card').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-south .seat-card').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-east .seat-card').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-west .seat-card').first().evaluate((el) => getComputedStyle(el, '::before').transform),
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
      page.locator('.seat-anchors .anchor-north').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-anchors .anchor-south').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-anchors .anchor-east').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-anchors .anchor-west').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.center-area').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.center-area').evaluate((el) => getComputedStyle(el, '::after').transform),
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
      page.locator('.wall-legend-item-remaining strong').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top .wall-stack').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-bottom .wall-stack').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-left .wall-stack').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-right .wall-stack').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-top .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top .wall-side-label').evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-bottom .wall-side-label').evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-left .wall-side-label').evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-right .wall-side-label').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-north .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-south .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-east .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-west .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-north .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      page.locator('.seat-south .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      page.locator('.seat-east .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      page.locator('.seat-west .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      page.locator('.discard-lane .tile-shell').nth(0).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(1).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(2).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(3).evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(tabletNorthSeatTransform).not.toBe('none');
    expect(tabletSouthSeatTransform).not.toBe('none');
    expect(tabletEastSeatTransform).not.toBe('none');
    expect(tabletWestSeatTransform).not.toBe('none');
    expect(tabletNorthSeatLayerTransform).toContain('matrix3d');
    expect(tabletSouthSeatLayerTransform).toContain('matrix3d');
    expect(tabletEastSeatLayerTransform).toContain('matrix3d');
    expect(tabletWestSeatLayerTransform).toContain('matrix3d');
    expect(tabletNorthSeatLayerTransform).not.toBe(tabletSouthSeatLayerTransform);
    expect(tabletEastSeatLayerTransform).not.toBe(tabletWestSeatLayerTransform);
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

    const [
      tabletNorthScoreBadgeTransform,
      tabletSouthScoreBadgeTransform,
      tabletEastScoreBadgeTransform,
      tabletWestScoreBadgeTransform,
      tabletNorthDealerBadgeTransform,
      tabletEastTurnBadgeTransform
    ] = await Promise.all([
      page.locator('.seat-north .seat-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-north .seat-badge-dealer').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-badge-turn').first().evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(tabletNorthScoreBadgeTransform).toContain('matrix3d');
    expect(tabletSouthScoreBadgeTransform).toContain('matrix3d');
    expect(tabletEastScoreBadgeTransform).toContain('matrix3d');
    expect(tabletWestScoreBadgeTransform).toContain('matrix3d');
    expect(tabletNorthScoreBadgeTransform).not.toBe(tabletSouthScoreBadgeTransform);
    expect(tabletEastScoreBadgeTransform).not.toBe(tabletWestScoreBadgeTransform);
    expect(tabletNorthDealerBadgeTransform).toContain('matrix3d');
    expect(tabletEastTurnBadgeTransform).toContain('matrix3d');
    expect(tabletNorthDealerBadgeTransform).not.toBe(tabletNorthScoreBadgeTransform);
    expect(tabletEastTurnBadgeTransform).not.toBe(tabletEastScoreBadgeTransform);

    expect(tabletNorthAnchorTransform).toContain('matrix3d');
    expect(tabletSouthAnchorTransform).toContain('matrix3d');
    expect(tabletEastAnchorTransform).toContain('matrix3d');
    expect(tabletWestAnchorTransform).toContain('matrix3d');
    expect(tabletNorthAnchorLayerTransform).toContain('matrix3d');
    expect(tabletSouthAnchorLayerTransform).toContain('matrix3d');
    expect(tabletEastAnchorLayerTransform).toContain('matrix3d');
    expect(tabletWestAnchorLayerTransform).toContain('matrix3d');
    expect(tabletNorthAnchorLayerTransform).not.toBe(tabletSouthAnchorLayerTransform);
    expect(tabletEastAnchorLayerTransform).not.toBe(tabletWestAnchorLayerTransform);
    expect(tabletCenterInsetTransform).toContain('matrix3d');
    expect(tabletCenterRingTransform).toContain('matrix3d');
    expect(tabletCenterInsetTransform).not.toBe(tabletCenterRingTransform);
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
    expect(tabletWallLegendRemainingValueTransform).toContain('matrix3d');
    expect(tabletWallLegendRemainingValueTransform).not.toBe(tabletWallLegendRemainingTransform);
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
    expect(tabletTopWallStackHighlightTransform).toContain('matrix3d');
    expect(tabletBottomWallStackHighlightTransform).toContain('matrix3d');
    expect(tabletLeftWallStackHighlightTransform).toContain('matrix3d');
    expect(tabletRightWallStackHighlightTransform).toContain('matrix3d');
    expect(tabletTopWallStackHighlightTransform).not.toBe(tabletBottomWallStackHighlightTransform);
    expect(tabletLeftWallStackHighlightTransform).not.toBe(tabletRightWallStackHighlightTransform);
    expect(tabletTopWallLabelTransform).toContain('matrix3d');
    expect(tabletBottomWallLabelTransform).toContain('matrix3d');
    expect(tabletLeftWallLabelTransform).toContain('matrix3d');
    expect(tabletRightWallLabelTransform).toContain('matrix3d');
    expect(tabletTopWallLabelLayerTransform).toContain('matrix3d');
    expect(tabletBottomWallLabelLayerTransform).toContain('matrix3d');
    expect(tabletLeftWallLabelLayerTransform).toContain('matrix3d');
    expect(tabletRightWallLabelLayerTransform).toContain('matrix3d');
    expect(tabletTopWallLabelLayerTransform).not.toBe(tabletBottomWallLabelLayerTransform);
    expect(tabletLeftWallLabelLayerTransform).not.toBe(tabletRightWallLabelLayerTransform);
    expect(tabletNorthSeatTileHighlightTransform).toContain('matrix3d');
    expect(tabletSouthSeatTileHighlightTransform).toContain('matrix3d');
    expect(tabletEastSeatTileHighlightTransform).toContain('matrix3d');
    expect(tabletWestSeatTileHighlightTransform).toContain('matrix3d');
    expect(tabletNorthSeatTileHighlightTransform).not.toBe(tabletSouthSeatTileHighlightTransform);
    expect(tabletEastSeatTileHighlightTransform).not.toBe(tabletWestSeatTileHighlightTransform);
    expect(tabletNorthSeatTileFrameTransform).toContain('matrix3d');
    expect(tabletSouthSeatTileFrameTransform).toContain('matrix3d');
    expect(tabletEastSeatTileFrameTransform).toContain('matrix3d');
    expect(tabletWestSeatTileFrameTransform).toContain('matrix3d');
    expect(tabletNorthSeatTileFrameTransform).not.toBe(tabletSouthSeatTileFrameTransform);
    expect(tabletEastSeatTileFrameTransform).not.toBe(tabletWestSeatTileFrameTransform);
    expect(tabletDiscardTileOneTransform).toContain('matrix3d');
    expect(tabletDiscardTileTwoTransform).toContain('matrix3d');
    expect(tabletDiscardTileThreeTransform).toContain('matrix3d');
    expect(tabletDiscardTileFourTransform).toContain('matrix3d');
    expect(tabletDiscardTileOneTransform).not.toBe(tabletDiscardTileTwoTransform);
    expect(tabletDiscardTileThreeTransform).not.toBe(tabletDiscardTileFourTransform);

    const [tabletDiscardFrontRowTransform, tabletDiscardMiddleRowTransform, tabletDiscardBackRowTransform] = await Promise.all([
      page.locator('.discard-lane .tile-shell').nth(0).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(4).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(8).evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(tabletDiscardFrontRowTransform).toContain('matrix3d');
    expect(tabletDiscardMiddleRowTransform).toContain('matrix3d');
    expect(tabletDiscardBackRowTransform).toContain('matrix3d');
    expect(tabletDiscardFrontRowTransform).not.toBe(tabletDiscardMiddleRowTransform);
    expect(tabletDiscardMiddleRowTransform).not.toBe(tabletDiscardBackRowTransform);

    await page.setViewportSize({ width: 1280, height: 720 });
    const [
      northSeatTransform,
      southSeatTransform,
      eastSeatTransform,
      westSeatTransform,
      northSeatLayerTransform,
      southSeatLayerTransform,
      eastSeatLayerTransform,
      westSeatLayerTransform
    ] = await Promise.all([
      page.locator('.seat-north .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-card').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-north .seat-card').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-south .seat-card').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-east .seat-card').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-west .seat-card').first().evaluate((el) => getComputedStyle(el, '::before').transform)
    ]);

    expect(northSeatTransform).not.toBe('none');
    expect(southSeatTransform).not.toBe('none');
    expect(eastSeatTransform).not.toBe('none');
    expect(westSeatTransform).not.toBe('none');
    expect(northSeatLayerTransform).toContain('matrix3d');
    expect(southSeatLayerTransform).toContain('matrix3d');
    expect(eastSeatLayerTransform).toContain('matrix3d');
    expect(westSeatLayerTransform).toContain('matrix3d');
    expect(northSeatLayerTransform).not.toBe(southSeatLayerTransform);
    expect(eastSeatLayerTransform).not.toBe(westSeatLayerTransform);

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
      westSeatBadgesTransform,
      northSeatTileHighlightTransform,
      southSeatTileHighlightTransform,
      eastSeatTileHighlightTransform,
      westSeatTileHighlightTransform,
      northSeatTileFrameTransform,
      southSeatTileFrameTransform,
      eastSeatTileFrameTransform,
      westSeatTileFrameTransform
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
      page.locator('.seat-west .seat-badges').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-north .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-south .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-east .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-west .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-north .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      page.locator('.seat-south .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      page.locator('.seat-east .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::after').transform),
      page.locator('.seat-west .seat-tiles .tile-shell').first().evaluate((el) => getComputedStyle(el, '::after').transform)
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

    const [
      northScoreBadgeTransform,
      southScoreBadgeTransform,
      eastScoreBadgeTransform,
      westScoreBadgeTransform,
      northDealerBadgeTransform,
      eastTurnBadgeTransform
    ] = await Promise.all([
      page.locator('.seat-north .seat-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-south .seat-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-west .seat-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-north .seat-badge-dealer').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-east .seat-badge-turn').first().evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(northScoreBadgeTransform).toContain('matrix3d');
    expect(southScoreBadgeTransform).toContain('matrix3d');
    expect(eastScoreBadgeTransform).toContain('matrix3d');
    expect(westScoreBadgeTransform).toContain('matrix3d');
    expect(northScoreBadgeTransform).not.toBe(southScoreBadgeTransform);
    expect(eastScoreBadgeTransform).not.toBe(westScoreBadgeTransform);
    expect(northDealerBadgeTransform).toContain('matrix3d');
    expect(eastTurnBadgeTransform).toContain('matrix3d');
    expect(northDealerBadgeTransform).not.toBe(northScoreBadgeTransform);
    expect(eastTurnBadgeTransform).not.toBe(eastScoreBadgeTransform);

    expect(northSeatTileHighlightTransform).toContain('matrix3d');
    expect(southSeatTileHighlightTransform).toContain('matrix3d');
    expect(eastSeatTileHighlightTransform).toContain('matrix3d');
    expect(westSeatTileHighlightTransform).toContain('matrix3d');
    expect(northSeatTileHighlightTransform).not.toBe(southSeatTileHighlightTransform);
    expect(eastSeatTileHighlightTransform).not.toBe(westSeatTileHighlightTransform);
    expect(northSeatTileFrameTransform).toContain('matrix3d');
    expect(southSeatTileFrameTransform).toContain('matrix3d');
    expect(eastSeatTileFrameTransform).toContain('matrix3d');
    expect(westSeatTileFrameTransform).toContain('matrix3d');
    expect(northSeatTileFrameTransform).not.toBe(southSeatTileFrameTransform);
    expect(eastSeatTileFrameTransform).not.toBe(westSeatTileFrameTransform);

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
      northAnchorLayerTransform,
      eastAnchorLayerTransform,
      southAnchorLayerTransform,
      westAnchorLayerTransform,
      centerInsetTransform,
      centerRingTransform,
      discardLaneTransform,
      discardLaneTransformStyle,
      compassTransform,
      wallLegendBreakTransform,
      wallLegendDepletedTransform,
      wallLegendDealerTransform,
      wallLegendRemainingTransform,
      wallLegendBreakStackTransform,
      wallLegendDepletedStackTransform,
      wallLegendDealerStackTransform,
      wallLegendRemainingValueTransform
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
      page.locator('.seat-anchors .anchor-north').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-anchors .anchor-east').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-anchors .anchor-south').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-anchors .anchor-west').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.center-area').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.center-area').evaluate((el) => getComputedStyle(el, '::after').transform),
      discardLane.evaluate((el) => getComputedStyle(el).transform),
      discardLane.evaluate((el) => getComputedStyle(el).transformStyle),
      page.getByLabel('Table compass wind markers').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-break').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-depleted').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-dealer').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-remaining').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-break .wall-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-depleted .wall-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-dealer .wall-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.wall-legend-item-remaining strong').evaluate((el) => getComputedStyle(el).transform)
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
    expect(northAnchorLayerTransform).toContain('matrix3d');
    expect(eastAnchorLayerTransform).toContain('matrix3d');
    expect(southAnchorLayerTransform).toContain('matrix3d');
    expect(westAnchorLayerTransform).toContain('matrix3d');
    expect(northAnchorLayerTransform).not.toBe(southAnchorLayerTransform);
    expect(eastAnchorLayerTransform).not.toBe(westAnchorLayerTransform);
    expect(centerInsetTransform).toContain('matrix3d');
    expect(centerRingTransform).toContain('matrix3d');
    expect(centerInsetTransform).not.toBe(centerRingTransform);
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
    expect(wallLegendRemainingValueTransform).toContain('matrix3d');
    expect(wallLegendRemainingValueTransform).not.toBe(wallLegendRemainingTransform);
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
      rightWallStackTransform,
      topWallStackHighlightTransform,
      bottomWallStackHighlightTransform,
      leftWallStackHighlightTransform,
      rightWallStackHighlightTransform
    ] = await Promise.all([
      wallPreview.locator('.wall-top').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right .wall-stack').first().evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top .wall-stack').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-bottom .wall-stack').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-left .wall-stack').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-right .wall-stack').first().evaluate((el) => getComputedStyle(el, '::before').transform)
    ]);

    expect(topWallTransform).toContain('matrix3d');
    expect(bottomWallTransform).toContain('matrix3d');
    expect(leftWallTransform).toContain('matrix3d');
    expect(rightWallTransform).toContain('matrix3d');
    expect(topWallStackTransform).toContain('matrix3d');
    expect(bottomWallStackTransform).toContain('matrix3d');
    expect(leftWallStackTransform).toContain('matrix3d');
    expect(rightWallStackTransform).toContain('matrix3d');
    expect(topWallStackHighlightTransform).toContain('matrix3d');
    expect(bottomWallStackHighlightTransform).toContain('matrix3d');
    expect(leftWallStackHighlightTransform).toContain('matrix3d');
    expect(rightWallStackHighlightTransform).toContain('matrix3d');
    expect(topWallStackHighlightTransform).not.toBe(bottomWallStackHighlightTransform);
    expect(leftWallStackHighlightTransform).not.toBe(rightWallStackHighlightTransform);

    const [
      topWallLabelTransform,
      bottomWallLabelTransform,
      leftWallLabelTransform,
      rightWallLabelTransform,
      topWallLabelLayerTransform,
      bottomWallLabelLayerTransform,
      leftWallLabelLayerTransform,
      rightWallLabelLayerTransform
    ] = await Promise.all([
      wallPreview.locator('.wall-top .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-bottom .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-left .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-right .wall-side-label').evaluate((el) => getComputedStyle(el).transform),
      wallPreview.locator('.wall-top .wall-side-label').evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-bottom .wall-side-label').evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-left .wall-side-label').evaluate((el) => getComputedStyle(el, '::before').transform),
      wallPreview.locator('.wall-right .wall-side-label').evaluate((el) => getComputedStyle(el, '::before').transform)
    ]);

    expect(topWallLabelTransform).toContain('matrix3d');
    expect(bottomWallLabelTransform).toContain('matrix3d');
    expect(leftWallLabelTransform).toContain('matrix3d');
    expect(rightWallLabelTransform).toContain('matrix3d');
    expect(topWallLabelLayerTransform).toContain('matrix3d');
    expect(bottomWallLabelLayerTransform).toContain('matrix3d');
    expect(leftWallLabelLayerTransform).toContain('matrix3d');
    expect(rightWallLabelLayerTransform).toContain('matrix3d');
    expect(topWallLabelLayerTransform).not.toBe(bottomWallLabelLayerTransform);
    expect(leftWallLabelLayerTransform).not.toBe(rightWallLabelLayerTransform);

    const [desktopDiscardFrontRowTransform, desktopDiscardMiddleRowTransform, desktopDiscardBackRowTransform] = await Promise.all([
      page.locator('.discard-lane .tile-shell').nth(0).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(4).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(8).evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(desktopDiscardFrontRowTransform).toContain('matrix3d');
    expect(desktopDiscardMiddleRowTransform).toContain('matrix3d');
    expect(desktopDiscardBackRowTransform).toContain('matrix3d');
    expect(desktopDiscardFrontRowTransform).not.toBe(desktopDiscardMiddleRowTransform);
    expect(desktopDiscardMiddleRowTransform).not.toBe(desktopDiscardBackRowTransform);

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

  test('keeps discard lane rim pseudo-layers in directional 3D at tablet and desktop', async ({ page }) => {
    await page.goto('/game');

    const discardLane = page.getByLabel('Center discard lane');
    await expect(discardLane).toBeVisible();

    await page.setViewportSize({ width: 900, height: 760 });
    const [
      tabletLaneTransform,
      tabletLaneInsetTransform,
      tabletLaneFrameTransform,
      tabletFrontLeftTileTransform,
      tabletFrontRightTileTransform,
      tabletBackLeftTileTransform,
      tabletBackRightTileTransform
    ] = await Promise.all([
      discardLane.evaluate((el) => getComputedStyle(el).transform),
      discardLane.evaluate((el) => getComputedStyle(el, '::before').transform),
      discardLane.evaluate((el) => getComputedStyle(el, '::after').transform),
      page.locator('.discard-lane .tile-shell').nth(0).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(3).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(8).evaluate((el) => getComputedStyle(el).transform),
      page.locator('.discard-lane .tile-shell').nth(9).evaluate((el) => getComputedStyle(el).transform)
    ]);

    expect(tabletLaneTransform).toContain('matrix3d');
    expect(tabletLaneInsetTransform).toContain('matrix3d');
    expect(tabletLaneFrameTransform).toContain('matrix3d');
    expect(tabletLaneInsetTransform).not.toBe(tabletLaneFrameTransform);
    expect(tabletFrontLeftTileTransform).toContain('matrix3d');
    expect(tabletFrontRightTileTransform).toContain('matrix3d');
    expect(tabletBackLeftTileTransform).toContain('matrix3d');
    expect(tabletBackRightTileTransform).toContain('matrix3d');
    expect(tabletFrontLeftTileTransform).not.toBe(tabletFrontRightTileTransform);
    expect(tabletBackLeftTileTransform).not.toBe(tabletBackRightTileTransform);

    await page.setViewportSize({ width: 1280, height: 720 });
    const [desktopLaneInsetTransform, desktopLaneFrameTransform] = await Promise.all([
      discardLane.evaluate((el) => getComputedStyle(el, '::before').transform),
      discardLane.evaluate((el) => getComputedStyle(el, '::after').transform)
    ]);

    expect(desktopLaneInsetTransform).toContain('matrix3d');
    expect(desktopLaneFrameTransform).toContain('matrix3d');
    expect(desktopLaneInsetTransform).not.toBe(desktopLaneFrameTransform);
    expect(desktopLaneInsetTransform).not.toBe(tabletLaneInsetTransform);
    expect(desktopLaneFrameTransform).not.toBe(tabletLaneFrameTransform);
  });

  test('keeps seat summary cards and badges in directional 3D at tablet and desktop', async ({ page }) => {
    await page.goto('/game');

    const seatSummary = page.locator('.seat-summary').first();
    await expect(seatSummary).toBeVisible();

    await page.setViewportSize({ width: 900, height: 760 });
    const [
      tabletNorthSummaryTransform,
      tabletEastSummaryTransform,
      tabletSouthSummaryTransform,
      tabletWestSummaryTransform,
      tabletNorthSummaryLayerTransform,
      tabletEastSummaryLayerTransform,
      tabletSouthSummaryLayerTransform,
      tabletWestSummaryLayerTransform,
      tabletNorthMetaTransform,
      tabletEastMetaTransform,
      tabletSouthMetaTransform,
      tabletWestMetaTransform,
      tabletNorthWindTransform,
      tabletEastWindTransform,
      tabletSouthWindTransform,
      tabletWestWindTransform,
      tabletNorthPlayerTransform,
      tabletEastPlayerTransform,
      tabletSouthPlayerTransform,
      tabletWestPlayerTransform,
      tabletNorthScoreStackTransform,
      tabletEastScoreStackTransform,
      tabletSouthScoreStackTransform,
      tabletWestScoreStackTransform,
      tabletNorthBadgeTransform,
      tabletEastBadgeTransform,
      tabletSouthBadgeTransform,
      tabletWestBadgeTransform,
      tabletNorthBadgeLayerTransform,
      tabletEastBadgeLayerTransform,
      tabletSouthBadgeLayerTransform,
      tabletWestBadgeLayerTransform
    ] = await Promise.all([
      page.locator('.seat-summary .seat-summary-north').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-south').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-west').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-east').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-south').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-west').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-meta').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-meta').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-south .seat-summary-meta').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-west .seat-summary-meta').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-wind').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-wind').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-south .seat-summary-wind').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-west .seat-summary-wind').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-player').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-player').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-south .seat-summary-player').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-west .seat-summary-player').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-score-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-score-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-south .seat-summary-score-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-west .seat-summary-score-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-south .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-west .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-south .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-west .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el, '::before').transform)
    ]);

    const tabletDealerBadgeTransforms = await page.locator('.seat-summary .seat-summary-badge-dealer').first().evaluate((el) => {
      const scoreBadge = el.closest('.seat-summary-item')?.querySelector('.seat-summary-badge-score');

      return {
        dealerTransform: getComputedStyle(el).transform,
        dealerLayerTransform: getComputedStyle(el, '::before').transform,
        scoreTransform: scoreBadge ? getComputedStyle(scoreBadge).transform : null,
        scoreLayerTransform: scoreBadge ? getComputedStyle(scoreBadge, '::before').transform : null
      };
    });

    expect(tabletNorthSummaryTransform).toContain('matrix3d');
    expect(tabletEastSummaryTransform).toContain('matrix3d');
    expect(tabletSouthSummaryTransform).toContain('matrix3d');
    expect(tabletWestSummaryTransform).toContain('matrix3d');
    expect(tabletNorthSummaryTransform).not.toBe(tabletSouthSummaryTransform);
    expect(tabletEastSummaryTransform).not.toBe(tabletWestSummaryTransform);

    expect(tabletNorthSummaryLayerTransform).toContain('matrix3d');
    expect(tabletEastSummaryLayerTransform).toContain('matrix3d');
    expect(tabletSouthSummaryLayerTransform).toContain('matrix3d');
    expect(tabletWestSummaryLayerTransform).toContain('matrix3d');
    expect(tabletNorthSummaryLayerTransform).not.toBe(tabletSouthSummaryLayerTransform);
    expect(tabletEastSummaryLayerTransform).not.toBe(tabletWestSummaryLayerTransform);

    expect(tabletNorthMetaTransform).toContain('matrix3d');
    expect(tabletEastMetaTransform).toContain('matrix3d');
    expect(tabletSouthMetaTransform).toContain('matrix3d');
    expect(tabletWestMetaTransform).toContain('matrix3d');
    expect(tabletNorthMetaTransform).not.toBe(tabletSouthMetaTransform);
    expect(tabletEastMetaTransform).not.toBe(tabletWestMetaTransform);

    expect(tabletNorthWindTransform).toContain('matrix3d');
    expect(tabletEastWindTransform).toContain('matrix3d');
    expect(tabletSouthWindTransform).toContain('matrix3d');
    expect(tabletWestWindTransform).toContain('matrix3d');
    expect(tabletNorthWindTransform).not.toBe(tabletSouthWindTransform);
    expect(tabletEastWindTransform).not.toBe(tabletWestWindTransform);

    expect(tabletNorthPlayerTransform).toContain('matrix3d');
    expect(tabletEastPlayerTransform).toContain('matrix3d');
    expect(tabletSouthPlayerTransform).toContain('matrix3d');
    expect(tabletWestPlayerTransform).toContain('matrix3d');
    expect(tabletNorthPlayerTransform).not.toBe(tabletSouthPlayerTransform);
    expect(tabletEastPlayerTransform).not.toBe(tabletWestPlayerTransform);

    expect(tabletNorthScoreStackTransform).toContain('matrix3d');
    expect(tabletEastScoreStackTransform).toContain('matrix3d');
    expect(tabletSouthScoreStackTransform).toContain('matrix3d');
    expect(tabletWestScoreStackTransform).toContain('matrix3d');
    expect(tabletNorthScoreStackTransform).not.toBe(tabletSouthScoreStackTransform);
    expect(tabletEastScoreStackTransform).not.toBe(tabletWestScoreStackTransform);

    expect(tabletNorthBadgeTransform).toContain('matrix3d');
    expect(tabletEastBadgeTransform).toContain('matrix3d');
    expect(tabletSouthBadgeTransform).toContain('matrix3d');
    expect(tabletWestBadgeTransform).toContain('matrix3d');
    expect(tabletNorthBadgeTransform).not.toBe(tabletSouthBadgeTransform);
    expect(tabletEastBadgeTransform).not.toBe(tabletWestBadgeTransform);

    expect(tabletNorthBadgeLayerTransform).toContain('matrix3d');
    expect(tabletEastBadgeLayerTransform).toContain('matrix3d');
    expect(tabletSouthBadgeLayerTransform).toContain('matrix3d');
    expect(tabletWestBadgeLayerTransform).toContain('matrix3d');
    expect(tabletNorthBadgeLayerTransform).not.toBe(tabletSouthBadgeLayerTransform);
    expect(tabletEastBadgeLayerTransform).not.toBe(tabletWestBadgeLayerTransform);

    expect(tabletDealerBadgeTransforms.dealerTransform).toContain('matrix3d');
    expect(tabletDealerBadgeTransforms.dealerLayerTransform).toContain('matrix3d');
    expect(tabletDealerBadgeTransforms.dealerTransform).not.toBe(tabletDealerBadgeTransforms.scoreTransform);
    expect(tabletDealerBadgeTransforms.dealerLayerTransform).not.toBe(tabletDealerBadgeTransforms.scoreLayerTransform);

    await page.setViewportSize({ width: 1280, height: 720 });
    const [
      desktopNorthSummaryTransform,
      desktopEastSummaryTransform,
      desktopNorthSummaryLayerTransform,
      desktopEastSummaryLayerTransform,
      desktopNorthMetaTransform,
      desktopEastMetaTransform,
      desktopNorthWindTransform,
      desktopEastWindTransform,
      desktopNorthPlayerTransform,
      desktopEastPlayerTransform,
      desktopNorthScoreStackTransform,
      desktopEastScoreStackTransform,
      desktopNorthBadgeTransform,
      desktopEastBadgeTransform,
      desktopNorthBadgeLayerTransform,
      desktopEastBadgeLayerTransform
    ] = await Promise.all([
      page.locator('.seat-summary .seat-summary-north').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-east').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-meta').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-meta').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-wind').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-wind').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-player').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-player').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-score-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-score-stack').evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el).transform),
      page.locator('.seat-summary .seat-summary-north .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.seat-summary .seat-summary-east .seat-summary-badge-score').first().evaluate((el) => getComputedStyle(el, '::before').transform)
    ]);

    const desktopDealerBadgeTransforms = await page.locator('.seat-summary .seat-summary-badge-dealer').first().evaluate((el) => {
      const scoreBadge = el.closest('.seat-summary-item')?.querySelector('.seat-summary-badge-score');

      return {
        dealerTransform: getComputedStyle(el).transform,
        dealerLayerTransform: getComputedStyle(el, '::before').transform,
        scoreTransform: scoreBadge ? getComputedStyle(scoreBadge).transform : null,
        scoreLayerTransform: scoreBadge ? getComputedStyle(scoreBadge, '::before').transform : null
      };
    });

    expect(desktopNorthSummaryTransform).toContain('matrix3d');
    expect(desktopEastSummaryTransform).toContain('matrix3d');
    expect(desktopNorthSummaryLayerTransform).toContain('matrix3d');
    expect(desktopEastSummaryLayerTransform).toContain('matrix3d');
    expect(desktopNorthSummaryLayerTransform).not.toBe(desktopEastSummaryLayerTransform);
    expect(desktopNorthSummaryLayerTransform).not.toBe(tabletNorthSummaryLayerTransform);
    expect(desktopEastSummaryLayerTransform).not.toBe(tabletEastSummaryLayerTransform);
    expect(desktopNorthMetaTransform).toContain('matrix3d');
    expect(desktopEastMetaTransform).toContain('matrix3d');
    expect(desktopNorthWindTransform).toContain('matrix3d');
    expect(desktopEastWindTransform).toContain('matrix3d');
    expect(desktopNorthPlayerTransform).toContain('matrix3d');
    expect(desktopEastPlayerTransform).toContain('matrix3d');
    expect(desktopNorthScoreStackTransform).toContain('matrix3d');
    expect(desktopEastScoreStackTransform).toContain('matrix3d');
    expect(desktopNorthBadgeTransform).toContain('matrix3d');
    expect(desktopEastBadgeTransform).toContain('matrix3d');
    expect(desktopNorthBadgeLayerTransform).toContain('matrix3d');
    expect(desktopEastBadgeLayerTransform).toContain('matrix3d');
    expect(desktopNorthBadgeLayerTransform).not.toBe(desktopEastBadgeLayerTransform);
    expect(desktopNorthBadgeLayerTransform).not.toBe(tabletNorthBadgeLayerTransform);
    expect(desktopEastBadgeLayerTransform).not.toBe(tabletEastBadgeLayerTransform);
    expect(desktopNorthSummaryTransform).not.toBe(tabletNorthSummaryTransform);
    expect(desktopEastSummaryTransform).not.toBe(tabletEastSummaryTransform);
    expect(desktopNorthMetaTransform).not.toBe(tabletNorthMetaTransform);
    expect(desktopEastMetaTransform).not.toBe(tabletEastMetaTransform);
    expect(desktopNorthWindTransform).not.toBe(tabletNorthWindTransform);
    expect(desktopEastWindTransform).not.toBe(tabletEastWindTransform);
    expect(desktopNorthPlayerTransform).not.toBe(tabletNorthPlayerTransform);
    expect(desktopEastPlayerTransform).not.toBe(tabletEastPlayerTransform);
    expect(desktopNorthScoreStackTransform).not.toBe(tabletNorthScoreStackTransform);
    expect(desktopEastScoreStackTransform).not.toBe(tabletEastScoreStackTransform);
    expect(desktopNorthBadgeTransform).not.toBe(tabletNorthBadgeTransform);
    expect(desktopEastBadgeTransform).not.toBe(tabletEastBadgeTransform);

    expect(desktopDealerBadgeTransforms.dealerTransform).toContain('matrix3d');
    expect(desktopDealerBadgeTransforms.dealerLayerTransform).toContain('matrix3d');
    expect(desktopDealerBadgeTransforms.dealerTransform).not.toBe(desktopDealerBadgeTransforms.scoreTransform);
    expect(desktopDealerBadgeTransforms.dealerLayerTransform).not.toBe(desktopDealerBadgeTransforms.scoreLayerTransform);
    expect(desktopDealerBadgeTransforms.dealerTransform).not.toBe(tabletDealerBadgeTransforms.dealerTransform);
    expect(desktopDealerBadgeTransforms.dealerLayerTransform).not.toBe(tabletDealerBadgeTransforms.dealerLayerTransform);
  });

  test('keeps wall legend capsule pseudo-layers in directional 3D at tablet and desktop', async ({ page }) => {
    await page.goto('/game');

    const wallLegend = page.getByLabel('Wall legend');
    await expect(wallLegend).toBeVisible();

    await page.setViewportSize({ width: 900, height: 760 });
    const [
      tabletBreakLayerTransform,
      tabletDepletedLayerTransform,
      tabletDealerLayerTransform,
      tabletRemainingLayerTransform
    ] = await Promise.all([
      page.locator('.wall-legend-item-break').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.wall-legend-item-depleted').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.wall-legend-item-dealer').evaluate((el) => getComputedStyle(el, '::before').transform),
      page.locator('.wall-legend-item-remaining').evaluate((el) => getComputedStyle(el, '::before').transform)
    ]);

    expect(tabletBreakLayerTransform).toContain('matrix3d');
    expect(tabletDepletedLayerTransform).toContain('matrix3d');
    expect(tabletDealerLayerTransform).toContain('matrix3d');
    expect(tabletRemainingLayerTransform).toContain('matrix3d');
    expect(tabletBreakLayerTransform).not.toBe(tabletRemainingLayerTransform);
    expect(tabletDepletedLayerTransform).not.toBe(tabletDealerLayerTransform);

    await page.setViewportSize({ width: 1280, height: 720 });
    const [desktopBreakLayerTransform, desktopDepletedLayerTransform, desktopDealerLayerTransform, desktopRemainingLayerTransform] =
      await Promise.all([
        page.locator('.wall-legend-item-break').evaluate((el) => getComputedStyle(el, '::before').transform),
        page.locator('.wall-legend-item-depleted').evaluate((el) => getComputedStyle(el, '::before').transform),
        page.locator('.wall-legend-item-dealer').evaluate((el) => getComputedStyle(el, '::before').transform),
        page.locator('.wall-legend-item-remaining').evaluate((el) => getComputedStyle(el, '::before').transform)
      ]);

    expect(desktopBreakLayerTransform).toContain('matrix3d');
    expect(desktopDepletedLayerTransform).toContain('matrix3d');
    expect(desktopDealerLayerTransform).toContain('matrix3d');
    expect(desktopRemainingLayerTransform).toContain('matrix3d');
    expect(desktopBreakLayerTransform).not.toBe(desktopRemainingLayerTransform);
    expect(desktopDepletedLayerTransform).not.toBe(desktopDealerLayerTransform);
    expect(desktopBreakLayerTransform).not.toBe(tabletBreakLayerTransform);
    expect(desktopDepletedLayerTransform).not.toBe(tabletDepletedLayerTransform);
    expect(desktopDealerLayerTransform).not.toBe(tabletDealerLayerTransform);
    expect(desktopRemainingLayerTransform).not.toBe(tabletRemainingLayerTransform);
  });
});
