// Camera controller for flat (orthographic) and perspective view modes
import * as THREE from 'three';

export type CameraMode = 'flat' | 'perspective';

interface CameraState {
  position: THREE.Vector3;
  lookAt: THREE.Vector3;
  fov?: number;
}

interface TransitionState {
  active: boolean;
  startTime: number;
  duration: number;
  startState: CameraState;
  endState: CameraState;
}

export class CameraController {
  private orthoCamera: THREE.OrthographicCamera;
  private perspCamera: THREE.PerspectiveCamera;
  private currentMode: CameraMode = 'flat';
  private seatIndex: number = 0;
  private transition: TransitionState | null = null;
  private aspect: number;

  constructor(aspect: number) {
    this.aspect = aspect;

    // Create orthographic camera (flat top-down)
    const frustumSize = 50;
    this.orthoCamera = new THREE.OrthographicCamera(
      (frustumSize * aspect) / -2,
      (frustumSize * aspect) / 2,
      frustumSize / 2,
      frustumSize / -2,
      0.1,
      100
    );
    this.orthoCamera.position.set(0, 30, 0);
    this.orthoCamera.lookAt(0, 0, 0);

    // Create perspective camera
    this.perspCamera = new THREE.PerspectiveCamera(45, aspect, 0.1, 100);
    this.updatePerspectiveCamera(0);
  }

  getActive(): THREE.Camera {
    return this.currentMode === 'flat' ? this.orthoCamera : this.perspCamera;
  }

  getMode(): CameraMode {
    return this.currentMode;
  }

  setMode(mode: CameraMode): void {
    if (mode === this.currentMode) {
      return;
    }

    const fromCamera = this.getActive();
    this.currentMode = mode;
    const toCamera = this.getActive();

    // Start transition animation
    this.startTransition(fromCamera, toCamera);
  }

  toggleMode(): void {
    this.setMode(this.currentMode === 'flat' ? 'perspective' : 'flat');
  }

  updateSeat(seatIndex: number): void {
    this.seatIndex = seatIndex;
    this.updatePerspectiveCamera(seatIndex);
  }

  onResize(width: number, height: number): void {
    this.aspect = width / height;

    // Update orthographic camera
    const frustumSize = 50;
    this.orthoCamera.left = (frustumSize * this.aspect) / -2;
    this.orthoCamera.right = (frustumSize * this.aspect) / 2;
    this.orthoCamera.top = frustumSize / 2;
    this.orthoCamera.bottom = frustumSize / -2;
    this.orthoCamera.updateProjectionMatrix();

    // Update perspective camera
    this.perspCamera.aspect = this.aspect;
    this.perspCamera.updateProjectionMatrix();
  }

  update(deltaTime: number): void {
    if (!this.transition) {
      return;
    }

    const elapsed = Date.now() - this.transition.startTime;
    const progress = Math.min(elapsed / this.transition.duration, 1);
    const t = this.easeInOutCubic(progress);

    // Interpolate position
    const currentCamera = this.getActive();
    currentCamera.position.lerpVectors(
      this.transition.startState.position,
      this.transition.endState.position,
      t
    );

    // Interpolate lookAt by updating the target
    const targetLookAt = new THREE.Vector3().lerpVectors(
      this.transition.startState.lookAt,
      this.transition.endState.lookAt,
      t
    );
    currentCamera.lookAt(targetLookAt);

    // Interpolate FOV for perspective camera
    if (currentCamera instanceof THREE.PerspectiveCamera) {
      if (this.transition.startState.fov !== undefined && this.transition.endState.fov !== undefined) {
        currentCamera.fov = THREE.MathUtils.lerp(
          this.transition.startState.fov,
          this.transition.endState.fov,
          t
        );
        currentCamera.updateProjectionMatrix();
      }
    }

    if (progress >= 1) {
      this.transition = null;
    }
  }

  private updatePerspectiveCamera(seatIndex: number): void {
    // Calculate camera position based on seat
    // Seat 0 (East, +Z): camera behind looking at origin
    // Seat 1 (South, -X): 90° CCW rotation
    // Seat 2 (West, -Z): 180° rotation
    // Seat 3 (North, +X): 270° rotation
    const angle = (seatIndex * Math.PI) / 2;
    const distance = 15;
    const height = 12;

    const x = Math.sin(angle) * distance;
    const z = Math.cos(angle) * distance;

    this.perspCamera.position.set(x, height, z);
    this.perspCamera.lookAt(0, 0, 0);
    this.perspCamera.updateProjectionMatrix();
  }

  private startTransition(fromCamera: THREE.Camera, toCamera: THREE.Camera): void {
    // Capture current state
    const fromPos = fromCamera.position.clone();
    const fromTarget = new THREE.Vector3();
    fromCamera.getWorldDirection(fromTarget);
    fromTarget.multiplyScalar(10).add(fromPos);

    // Calculate look-at from direction
    const fromLookAt = new THREE.Vector3();
    fromCamera.getWorldDirection(fromLookAt);
    fromLookAt.multiplyScalar(-1);
    fromLookAt.add(fromPos);
    fromLookAt.set(0, 0, 0); // Always look at origin

    const fromFov = fromCamera instanceof THREE.PerspectiveCamera ? fromCamera.fov : undefined;

    // Capture target state
    const toPos = toCamera.position.clone();
    const toLookAt = new THREE.Vector3(0, 0, 0);
    const toFov = toCamera instanceof THREE.PerspectiveCamera ? toCamera.fov : undefined;

    this.transition = {
      active: true,
      startTime: Date.now(),
      duration: 300,
      startState: {
        position: fromPos,
        lookAt: fromLookAt,
        fov: fromFov,
      },
      endState: {
        position: toPos,
        lookAt: toLookAt,
        fov: toFov,
      },
    };
  }

  private easeInOutCubic(t: number): number {
    return t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2;
  }
}

export function createCameraController(aspect: number): CameraController {
  return new CameraController(aspect);
}
