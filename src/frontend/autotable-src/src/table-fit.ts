import { OrthographicCamera, PerspectiveCamera, Vector3 } from 'three';

export interface ScreenArea {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

/** Fit projected world corners, not the canvas aspect or a flat XY rectangle. */
export function fitTableProjection(
  camera: PerspectiveCamera | OrthographicCamera,
  corners: readonly Vector3[], width: number, height: number, area: ScreenArea,
): void {
  if (width <= 0 || height <= 0 || corners.length === 0) return;
  camera.clearViewOffset();
  camera.updateMatrixWorld(true);
  const projected = corners.map(point => point.clone().project(camera));
  const minX = Math.min(...projected.map(p => p.x)), maxX = Math.max(...projected.map(p => p.x));
  const minY = Math.min(...projected.map(p => p.y)), maxY = Math.max(...projected.map(p => p.y));
  const scale = Math.min(
    (area.right - area.left) * 2 / width / (maxX - minX),
    (area.bottom - area.top) * 2 / height / (maxY - minY),
  );
  if (!Number.isFinite(scale) || scale <= 0) return;
  const x = (area.left + area.right) / width - 1 - scale * (minX + maxX) / 2;
  const y = 1 - (area.top + area.bottom) / height - scale * (minY + maxY) / 2;
  // Store the fit in camera state, not only its derived matrix: selection
  // rebuilds the projection during an ordinary press or box drag.
  camera.setViewOffset(
    width, height,
    (scale - 1 - x) * width / (2 * scale),
    (scale - 1 + y) * height / (2 * scale),
    width / scale, height / scale,
  );
}
