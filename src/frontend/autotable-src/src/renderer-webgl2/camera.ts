// Phase K Wave 16 — WebGL2 orbital camera (Hicks).
//
// Phase L W2 spike scaffolding.  The production renderer-webgl2
// path needs a camera that lets the user orbit / pan / zoom around
// the mahjong table.  The three.js codepath uses
// `OrbitControls` against a `PerspectiveCamera`; this module ports
// the same orbit / pan / zoom semantics over a plain Float32Array
// view matrix so we don't pay the three.js dependency.
//
// What's HERE in W16:
//   • `createOrbitCamera()` — produce a default-state camera
//     pointed at the mahjong table centre.
//   • `attachMouseControls()` — left-drag orbit, right-drag pan,
//     wheel zoom (matches the three OrbitControls binding).
//   • `attachTouchControls()` — one-finger orbit, two-finger
//     pinch zoom + pan (matches the three OrbitControls binding
//     under `enableTouchControls`).
//   • `viewMatrix()` — build a row-major view matrix from the
//     orbit state.
//   • `projectionMatrix()` — pinhole projection mirroring the
//     existing `perspective4` helper.
//   • `viewProjMatrix()` — composed view × projection ready for
//     the tile-mesh shader's `u_viewProj`.
//
// What's NOT here (Phase L W3+):
//   • Smooth damping (animated re-centre, ease-out on drag-end).
//   • Tween-on-deal animations (W3).
//   • Frustum-culling helpers (W4).

import {
  identity4,
  multiplyMatrix4,
  perspective4,
} from './math';

export interface OrbitState {
  /** Spherical orbit radius in scene units. */
  radius: number;
  /** Azimuth angle around +y (radians, 0 = looking down +x). */
  azimuth: number;
  /** Elevation above the xz plane (radians, +PI/2 = straight down). */
  elevation: number;
  /** Look-at target in scene space. */
  target: [number, number, number];
}

export interface OrbitCamera {
  state: OrbitState;
  /** Pixels per radian for the orbit drag rate.  Lower = more sensitive. */
  rotateSensitivity: number;
  /** Scene-units per pixel for pan. */
  panSensitivity: number;
  /** Multiplicative zoom factor per wheel notch (>1 zooms out). */
  zoomFactor: number;
  /** Soft clamp for the orbit radius. */
  minRadius: number;
  maxRadius: number;
  /** Soft clamp for the elevation angle (avoid flipping at poles). */
  minElevation: number;
  maxElevation: number;
}

export const DEFAULT_ORBIT_STATE: Readonly<OrbitState> = Object.freeze({
  radius: 18.0,
  azimuth: Math.PI / 2,    // start looking down +z
  elevation: Math.PI / 3.5, // ~50° above the table
  target: [0, 0, -3] as [number, number, number],
});

export function createOrbitCamera(initial?: Partial<OrbitState>): OrbitCamera {
  return {
    state: {
      radius: initial?.radius ?? DEFAULT_ORBIT_STATE.radius,
      azimuth: initial?.azimuth ?? DEFAULT_ORBIT_STATE.azimuth,
      elevation: initial?.elevation ?? DEFAULT_ORBIT_STATE.elevation,
      target: [...(initial?.target ?? DEFAULT_ORBIT_STATE.target)] as [number, number, number],
    },
    rotateSensitivity: 0.005,
    panSensitivity: 0.02,
    zoomFactor: 1.1,
    minRadius: 6.0,
    maxRadius: 60.0,
    minElevation: 0.1,
    maxElevation: Math.PI / 2 - 0.05,
  };
}

/** Compute the camera's world-space position from the orbit state. */
export function cameraPosition(cam: OrbitCamera): [number, number, number] {
  const { radius, azimuth, elevation, target } = cam.state;
  const cosE = Math.cos(elevation);
  return [
    target[0] + radius * cosE * Math.cos(azimuth),
    target[1] + radius * Math.sin(elevation),
    target[2] + radius * cosE * Math.sin(azimuth),
  ];
}

/**
 * Build a column-major view matrix that looks from `cameraPosition`
 * toward `state.target` with +y up.  Mirrors `three.Matrix4.lookAt`
 * + an invert step (lookAt produces a model matrix for the eye; the
 * view matrix is its inverse).
 */
export function viewMatrix(cam: OrbitCamera): Float32Array {
  const eye = cameraPosition(cam);
  const target = cam.state.target;

  // Forward = eye → target  (note: typical OpenGL view-matrix
  // convention has +z pointing INTO the camera, so we flip later).
  const fx = target[0] - eye[0];
  const fy = target[1] - eye[1];
  const fz = target[2] - eye[2];
  const flen = Math.hypot(fx, fy, fz) || 1;
  const fwdx = fx / flen, fwdy = fy / flen, fwdz = fz / flen;

  // Up vector (world +y).  Right = forward × up.
  const upX = 0, upY = 1, upZ = 0;
  let rightX = fwdy * upZ - fwdz * upY;
  let rightY = fwdz * upX - fwdx * upZ;
  let rightZ = fwdx * upY - fwdy * upX;
  const rlen = Math.hypot(rightX, rightY, rightZ) || 1;
  rightX /= rlen; rightY /= rlen; rightZ /= rlen;

  // Recompute up = right × forward to enforce orthonormality.
  const uX = rightY * fwdz - rightZ * fwdy;
  const uY = rightZ * fwdx - rightX * fwdz;
  const uZ = rightX * fwdy - rightY * fwdx;

  // Column-major view matrix.  -forward in column 2 so +z points
  // out of the screen toward the camera (standard GL convention).
  const m = new Float32Array(16);
  m[0] = rightX;  m[1] = uX;  m[2] = -fwdx;  m[3] = 0;
  m[4] = rightY;  m[5] = uY;  m[6] = -fwdy;  m[7] = 0;
  m[8] = rightZ;  m[9] = uZ;  m[10] = -fwdz; m[11] = 0;
  m[12] = -(rightX * eye[0] + rightY * eye[1] + rightZ * eye[2]);
  m[13] = -(uX * eye[0] + uY * eye[1] + uZ * eye[2]);
  m[14] = -(-fwdx * eye[0] + -fwdy * eye[1] + -fwdz * eye[2]);
  m[15] = 1;
  return m;
}

/**
 * Pinhole projection matrix (column-major).  Wraps `perspective4`
 * so consumers don't need a separate import for the trivial case.
 */
export function projectionMatrix(
  canvas: HTMLCanvasElement,
  fovYRadians = Math.PI / 4,
  near = 0.1,
  far = 200,
): Float32Array {
  const aspect = canvas.width / Math.max(1, canvas.height);
  return perspective4(fovYRadians, aspect, near, far);
}

/** view × projection composed for the tile-mesh shader. */
export function viewProjMatrix(
  cam: OrbitCamera,
  canvas: HTMLCanvasElement,
): Float32Array {
  return multiplyMatrix4(projectionMatrix(canvas), viewMatrix(cam));
}

// ── Mouse / touch input plumbing ──────────────────────────────────

export interface CameraControlsHandle {
  /** Remove every listener attached by the controls. */
  detach(): void;
}

interface InputMode {
  kind: 'orbit' | 'pan';
  startX: number;
  startY: number;
  startState: OrbitState;
}

/**
 * Attach mouse controls to the canvas:
 *   • Left-drag → orbit
 *   • Right-drag (or shift+left-drag) → pan target
 *   • Wheel scroll → zoom (clamped to [minRadius, maxRadius])
 *
 * The supplied `onChange` callback fires once per UI frame after
 * each input event so the renderer can request a redraw.
 */
export function attachMouseControls(
  canvas: HTMLCanvasElement,
  cam: OrbitCamera,
  onChange: () => void,
): CameraControlsHandle {
  let mode: InputMode | null = null;

  const onDown = (e: MouseEvent): void => {
    e.preventDefault();
    const isPan = e.button === 2 || (e.button === 0 && e.shiftKey);
    mode = {
      kind: isPan ? 'pan' : 'orbit',
      startX: e.clientX,
      startY: e.clientY,
      startState: snapshotState(cam.state),
    };
    canvas.setPointerCapture?.((e as unknown as PointerEvent).pointerId ?? 0);
  };

  const onMove = (e: MouseEvent): void => {
    if (mode === null) return;
    const dx = e.clientX - mode.startX;
    const dy = e.clientY - mode.startY;
    if (mode.kind === 'orbit') {
      cam.state.azimuth = mode.startState.azimuth + dx * cam.rotateSensitivity;
      cam.state.elevation = clamp(
        mode.startState.elevation - dy * cam.rotateSensitivity,
        cam.minElevation,
        cam.maxElevation,
      );
    } else {
      // Pan in the camera's screen plane.
      const pan = cam.panSensitivity * cam.state.radius / 18;
      cam.state.target[0] = mode.startState.target[0] - dx * pan * Math.cos(cam.state.azimuth);
      cam.state.target[2] = mode.startState.target[2] - dx * pan * Math.sin(cam.state.azimuth);
      cam.state.target[1] = mode.startState.target[1] + dy * pan;
    }
    onChange();
  };

  const onUp = (): void => {
    mode = null;
  };

  const onWheel = (e: WheelEvent): void => {
    e.preventDefault();
    const dir = e.deltaY > 0 ? cam.zoomFactor : 1 / cam.zoomFactor;
    cam.state.radius = clamp(cam.state.radius * dir, cam.minRadius, cam.maxRadius);
    onChange();
  };

  const onContextMenu = (e: MouseEvent): void => { e.preventDefault(); };

  canvas.addEventListener('mousedown', onDown);
  canvas.addEventListener('mousemove', onMove);
  canvas.addEventListener('mouseup', onUp);
  canvas.addEventListener('mouseleave', onUp);
  canvas.addEventListener('wheel', onWheel, { passive: false });
  canvas.addEventListener('contextmenu', onContextMenu);

  return {
    detach(): void {
      canvas.removeEventListener('mousedown', onDown);
      canvas.removeEventListener('mousemove', onMove);
      canvas.removeEventListener('mouseup', onUp);
      canvas.removeEventListener('mouseleave', onUp);
      canvas.removeEventListener('wheel', onWheel);
      canvas.removeEventListener('contextmenu', onContextMenu);
    },
  };
}

/**
 * Attach touch controls to the canvas:
 *   • One finger → orbit
 *   • Two fingers → pinch-zoom (delta distance) + pan (delta centre)
 */
export function attachTouchControls(
  canvas: HTMLCanvasElement,
  cam: OrbitCamera,
  onChange: () => void,
): CameraControlsHandle {
  let startTouches: { x: number; y: number }[] = [];
  let startState: OrbitState | null = null;
  let startDist = 0;

  const onStart = (e: TouchEvent): void => {
    e.preventDefault();
    startTouches = Array.from(e.touches).map(t => ({ x: t.clientX, y: t.clientY }));
    startState = snapshotState(cam.state);
    if (startTouches.length === 2) {
      const a = startTouches[0];
      const b = startTouches[1];
      startDist = Math.hypot(a.x - b.x, a.y - b.y);
    }
  };

  const onMove = (e: TouchEvent): void => {
    if (startState === null) return;
    e.preventDefault();
    if (e.touches.length === 1 && startTouches.length === 1) {
      const dx = e.touches[0].clientX - startTouches[0].x;
      const dy = e.touches[0].clientY - startTouches[0].y;
      cam.state.azimuth = startState.azimuth + dx * cam.rotateSensitivity;
      cam.state.elevation = clamp(
        startState.elevation - dy * cam.rotateSensitivity,
        cam.minElevation,
        cam.maxElevation,
      );
      onChange();
      return;
    }
    if (e.touches.length === 2 && startTouches.length === 2) {
      const a = e.touches[0];
      const b = e.touches[1];
      const dist = Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY);
      if (startDist > 0) {
        const zoom = startDist / Math.max(1, dist);
        cam.state.radius = clamp(startState.radius * zoom, cam.minRadius, cam.maxRadius);
      }
      // Pan via the midpoint delta.
      const startMidX = (startTouches[0].x + startTouches[1].x) / 2;
      const startMidY = (startTouches[0].y + startTouches[1].y) / 2;
      const midX = (a.clientX + b.clientX) / 2;
      const midY = (a.clientY + b.clientY) / 2;
      const pan = cam.panSensitivity * cam.state.radius / 18;
      cam.state.target[0] = startState.target[0] - (midX - startMidX) * pan * Math.cos(cam.state.azimuth);
      cam.state.target[2] = startState.target[2] - (midX - startMidX) * pan * Math.sin(cam.state.azimuth);
      cam.state.target[1] = startState.target[1] + (midY - startMidY) * pan;
      onChange();
    }
  };

  const onEnd = (): void => {
    startState = null;
    startTouches = [];
    startDist = 0;
  };

  canvas.addEventListener('touchstart', onStart, { passive: false });
  canvas.addEventListener('touchmove', onMove, { passive: false });
  canvas.addEventListener('touchend', onEnd);
  canvas.addEventListener('touchcancel', onEnd);

  return {
    detach(): void {
      canvas.removeEventListener('touchstart', onStart);
      canvas.removeEventListener('touchmove', onMove);
      canvas.removeEventListener('touchend', onEnd);
      canvas.removeEventListener('touchcancel', onEnd);
    },
  };
}

// ── Helpers ───────────────────────────────────────────────────────

function snapshotState(s: OrbitState): OrbitState {
  return {
    radius: s.radius,
    azimuth: s.azimuth,
    elevation: s.elevation,
    target: [s.target[0], s.target[1], s.target[2]],
  };
}

function clamp(v: number, lo: number, hi: number): number {
  return v < lo ? lo : v > hi ? hi : v;
}

// ── Re-export the identity helper so callers don't need a separate
// import for the trivial case. ─────────────────────────────────────
export { identity4 };
