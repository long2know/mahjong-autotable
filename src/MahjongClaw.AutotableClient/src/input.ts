// Input handling: raycasting for tile selection and pointer events
import * as THREE from 'three';
import type { GameState } from './state';

export type InputEventType =
  | 'discardRequested'
  | 'drawRequested'
  | 'meldRequested'
  | 'winRequested'
  | 'pass';

export interface DiscardEvent {
  type: 'discardRequested';
  tileId: string;
}

export interface DrawEvent {
  type: 'drawRequested';
}

export interface MeldEvent {
  type: 'meldRequested';
  meldType: string;
  tileIds: string[];
}

export interface WinEvent {
  type: 'winRequested';
  isSelfDraw: boolean;
}

export interface PassEvent {
  type: 'pass';
}

export type InputEvent = DiscardEvent | DrawEvent | MeldEvent | WinEvent | PassEvent;

type InputListener = (event: InputEvent) => void;

class InputHandler {
  private camera: THREE.Camera;
  private scene: THREE.Scene;
  private container: HTMLElement;
  private raycaster = new THREE.Raycaster();
  private pointer = new THREE.Vector2();
  private listeners: InputListener[] = [];
  private enabled = true;
  private currentState: GameState | null = null;
  private playerId: string | null = null;

  constructor(camera: THREE.Camera, scene: THREE.Scene, container: HTMLElement) {
    this.camera = camera;
    this.scene = scene;
    this.container = container;
    this.setupPointerEvents();
  }

  setCamera(camera: THREE.Camera): void {
    this.camera = camera;
  }

  setPlayerId(playerId: string | null): void {
    this.playerId = playerId;
  }

  setState(state: GameState | null): void {
    this.currentState = state;
  }

  onInput(callback: InputListener): () => void {
    this.listeners.push(callback);
    return () => {
      const index = this.listeners.indexOf(callback);
      if (index !== -1) {
        this.listeners.splice(index, 1);
      }
    };
  }

  setEnabled(enabled: boolean): void {
    this.enabled = enabled;
  }

  private setupPointerEvents(): void {
    this.container.addEventListener('pointerdown', this.handlePointerDown);
  }

  private handlePointerDown = (event: PointerEvent): void => {
    if (!this.enabled || !this.currentState || !this.playerId) {
      return;
    }

    // Calculate normalized device coordinates
    const rect = this.container.getBoundingClientRect();
    this.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

    // Update raycaster
    this.raycaster.setFromCamera(this.pointer, this.camera);

    // Check if we hit any tile meshes
    const intersects = this.raycaster.intersectObjects(this.scene.children, true);

    if (intersects.length === 0) {
      return;
    }

    // Find first tile mesh with userData.tileId
    for (const intersect of intersects) {
      const object = intersect.object;
      if (object.userData['tileId'] && typeof object.userData['tileId'] === 'string') {
        const tileId = object.userData['tileId'] as string;
        this.handleTileClick(tileId);
        return;
      }
    }
  };

  private handleTileClick(tileId: string): void {
    if (!this.currentState || !this.playerId) {
      return;
    }

    // Check if this tile is in our hand
    const isOurTile = this.currentState.viewerHand.some(t => t.id === tileId);
    if (!isOurTile) {
      return;
    }

    // Check if we can discard
    if (!this.canDiscard()) {
      console.log('[Input] Cannot discard right now');
      return;
    }

    // Emit discard event
    this.emit({ type: 'discardRequested', tileId });
  }

  private canDiscard(): boolean {
    if (!this.currentState || !this.playerId) {
      return false;
    }

    // Check if it's our turn
    if (this.currentState.currentPlayerId !== this.playerId) {
      return false;
    }

    // Check if we're in discard phase (hand count should be 14, or 3n+2)
    const handCount = this.currentState.viewerHand.length;
    // After drawing, hand is 14 tiles (or 13 + melds), need to discard
    // Simple heuristic: if hand count % 3 == 2, we need to discard (14 % 3 = 2)
    return handCount > 0 && handCount % 3 === 2;
  }

  private emit(event: InputEvent): void {
    for (const listener of this.listeners) {
      listener(event);
    }
  }

  dispose(): void {
    this.container.removeEventListener('pointerdown', this.handlePointerDown);
    this.listeners = [];
  }
}

export function createInputHandler(
  camera: THREE.Camera,
  scene: THREE.Scene,
  container: HTMLElement
): InputHandler {
  return new InputHandler(camera, scene, container);
}
