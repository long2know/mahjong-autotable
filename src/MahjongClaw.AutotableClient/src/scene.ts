import * as THREE from 'three';
import { clientState, type GameState } from './state';
import { generateTileAtlas, tileIdFromLabel, type TileAtlas } from './tileAtlas';
import { createTileMesh } from './tile';

const TILE_WIDTH = 0.8;
const TILE_HEIGHT = 1.2;
const TILE_GAP = 0.1;

interface SeatPosition {
  seat: string;
  position: THREE.Vector3;
  rotation: number;
}

// Counter-clockwise from player POV
const SEAT_POSITIONS: SeatPosition[] = [
  { seat: 'East', position: new THREE.Vector3(0, 0, 15), rotation: 0 },
  { seat: 'South', position: new THREE.Vector3(15, 0, 0), rotation: Math.PI / 2 },
  { seat: 'West', position: new THREE.Vector3(0, 0, -15), rotation: Math.PI },
  { seat: 'North', position: new THREE.Vector3(-15, 0, 0), rotation: -Math.PI / 2 },
];

export function initScene(container: HTMLElement, tileAtlas: TileAtlas): { camera: THREE.Camera; scene: THREE.Scene } {
  console.log('[Scene] Initializing state-driven scene...');
  
  // Scene setup
  const scene = new THREE.Scene();
  scene.background = new THREE.Color(0x2a4a3a);

  // Orthographic camera (top-down view)
  const aspect = window.innerWidth / window.innerHeight;
  const frustumSize = 50;
  const camera = new THREE.OrthographicCamera(
    (frustumSize * aspect) / -2,
    (frustumSize * aspect) / 2,
    frustumSize / 2,
    frustumSize / -2,
    0.1,
    1000
  );
  camera.position.set(0, 50, 0);
  camera.lookAt(0, 0, 0);

  // Renderer
  const renderer = new THREE.WebGLRenderer({ antialias: true });
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.setPixelRatio(window.devicePixelRatio);
  container.appendChild(renderer.domElement);

  // Lighting
  const ambientLight = new THREE.AmbientLight(0xffffff, 0.7);
  scene.add(ambientLight);

  const directionalLight = new THREE.DirectionalLight(0xffffff, 0.6);
  directionalLight.position.set(10, 20, 10);
  scene.add(directionalLight);

  // Table surface
  const tableGeometry = new THREE.PlaneGeometry(40, 40);
  const tableMaterial = new THREE.MeshStandardMaterial({ color: 0x1a5a3a });
  const table = new THREE.Mesh(tableGeometry, tableMaterial);
  table.rotation.x = -Math.PI / 2;
  table.receiveShadow = true;
  scene.add(table);

  // Group for all dynamic game elements
  const gameGroup = new THREE.Group();
  scene.add(gameGroup);

  // Current turn indicator
  const turnIndicator = new THREE.Mesh(
    new THREE.RingGeometry(1.5, 2, 32),
    new THREE.MeshBasicMaterial({ color: 0xffff00, side: THREE.DoubleSide })
  );
  turnIndicator.rotation.x = -Math.PI / 2;
  turnIndicator.position.y = 0.05;
  turnIndicator.visible = false;
  scene.add(turnIndicator);

  function rebuildScene(state: GameState | null): void {
    // Clear previous game elements
    while (gameGroup.children.length > 0) {
      const child = gameGroup.children[0];
      if (child) {
        gameGroup.remove(child);
      }
    }
    
    if (!state) {
      console.log('[Scene] No state; scene cleared');
      turnIndicator.visible = false;
      return;
    }
    
    console.log(`[Scene] Rebuilding scene for game ${state.id}`);
    
    // Render wall pile
    renderWallPile(gameGroup, state, tileAtlas);
    
    // Render each seat's elements
    for (const seatPos of SEAT_POSITIONS) {
      const player = state.players.find(p => p.seat === seatPos.seat);
      if (!player) continue;
      
      renderSeatElements(gameGroup, state, player, seatPos, tileAtlas);
    }
    
    // Update turn indicator
    if (state.currentPlayerId) {
      const currentPlayer = state.players.find(p => p.id === state.currentPlayerId);
      if (currentPlayer) {
        const seatPos = SEAT_POSITIONS.find(s => s.seat === currentPlayer.seat);
        if (seatPos) {
          turnIndicator.position.copy(seatPos.position);
          turnIndicator.visible = true;
        }
      }
    } else {
      turnIndicator.visible = false;
    }
  }

  function renderWallPile(parent: THREE.Group, state: GameState, atlas: TileAtlas): void {
    const wallRemaining = state.wallTotal - state.tilesInPlay;
    if (wallRemaining <= 0) return;
    
    // Simple representation: stack of face-down tiles in one corner with count label
    const pilePosition = new THREE.Vector3(-12, 0, -12);
    
    // Show up to 10 tiles visually
    const visibleTileCount = Math.min(wallRemaining, 10);
    for (let i = 0; i < visibleTileCount; i++) {
      const tile = createTileMesh('back', false, atlas);
      tile.position.copy(pilePosition);
      tile.position.y = TILE_HEIGHT / 2 + i * 0.15;
      parent.add(tile);
    }
    
    // Add text label showing count
    const canvas = document.createElement('canvas');
    canvas.width = 256;
    canvas.height = 64;
    const ctx = canvas.getContext('2d');
    if (ctx) {
      ctx.fillStyle = '#ffffff';
      ctx.font = 'bold 48px sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(`Wall: ${wallRemaining}`, 128, 32);
    }
    const texture = new THREE.CanvasTexture(canvas);
    const labelMaterial = new THREE.MeshBasicMaterial({ map: texture, transparent: true });
    const labelGeometry = new THREE.PlaneGeometry(4, 1);
    const label = new THREE.Mesh(labelGeometry, labelMaterial);
    label.position.copy(pilePosition);
    label.position.y = 0.1;
    label.rotation.x = -Math.PI / 2;
    parent.add(label);
  }

  function renderSeatElements(
    parent: THREE.Group,
    state: GameState,
    player: { id: string; name: string; seat: string; handCount: number },
    seatPos: SeatPosition,
    atlas: TileAtlas
  ): void {
    const group = new THREE.Group();
    group.position.copy(seatPos.position);
    group.rotation.y = seatPos.rotation;
    
    // Determine if this is the local player's seat
    const isLocalPlayer = state.viewerHand.length > 0 && state.viewerHand.length === player.handCount;
    
    // Render concealed hand (face-up for local player, face-down for others)
    const handTiles = isLocalPlayer ? state.viewerHand : [];
    const handTileCount = player.handCount;
    
    for (let i = 0; i < handTileCount; i++) {
      let tile: THREE.Mesh;
      if (isLocalPlayer && i < handTiles.length) {
        const handTile = handTiles[i];
        if (handTile) {
          const tileId = tileIdFromLabel(handTile.label) ?? 'back';
          tile = createTileMesh(tileId, true, atlas);
          // Store tile ID for raycasting
          tile.userData['tileId'] = handTile.id;
        } else {
          tile = createTileMesh('back', false, atlas);
        }
      } else {
        tile = createTileMesh('back', false, atlas);
      }
      
      // Position tiles in a row
      const x = (i - handTileCount / 2) * (TILE_WIDTH + TILE_GAP);
      tile.position.set(x, TILE_HEIGHT / 2, -5);
      group.add(tile);
    }
    
    // Render open melds
    const playerMelds = state.melds.filter(m => m.playerId === player.id);
    let meldOffsetX = (handTileCount / 2 + 1) * (TILE_WIDTH + TILE_GAP);
    
    for (const meld of playerMelds) {
      for (let i = 0; i < meld.tiles.length; i++) {
        const meldTile = meld.tiles[i];
        if (!meldTile) continue;
        
        const tileId = tileIdFromLabel(meldTile.label) ?? 'back';
        const tile = createTileMesh(tileId, true, atlas);
        
        // Stack 4th tile for kongs
        if (i === 3 && meld.type.toLowerCase().includes('kong')) {
          tile.position.set(meldOffsetX - (TILE_WIDTH + TILE_GAP), TILE_HEIGHT * 1.5, -5);
        } else {
          tile.position.set(meldOffsetX, TILE_HEIGHT / 2, -5);
          meldOffsetX += TILE_WIDTH + TILE_GAP;
        }
        
        tile.rotation.y = Math.PI / 8; // Slight angle for melds
        group.add(tile);
      }
      
      meldOffsetX += TILE_GAP * 2; // Extra gap between melds
    }
    
    // Render discards in 6-column grid
    const playerDiscards = state.discards.filter(d => d.playerId === player.id);
    const discardCols = 6;
    
    for (let i = 0; i < playerDiscards.length; i++) {
      const discard = playerDiscards[i];
      if (!discard) continue;
      
      const tileId = tileIdFromLabel(discard.tileLabel) ?? 'back';
      const tile = createTileMesh(tileId, true, atlas);
      
      const col = i % discardCols;
      const row = Math.floor(i / discardCols);
      const x = (col - discardCols / 2) * (TILE_WIDTH + TILE_GAP);
      const z = 2 + row * (TILE_WIDTH + TILE_GAP);
      
      tile.position.set(x, TILE_HEIGHT / 2, z);
      group.add(tile);
    }
    
    parent.add(group);
  }

  // Subscribe to state changes
  clientState.onStateChanged((state) => {
    rebuildScene(state);
  });

  // Initial render if state already exists
  rebuildScene(clientState.getState());

  // Handle window resize
  window.addEventListener('resize', () => {
    const newAspect = window.innerWidth / window.innerHeight;
    camera.left = (frustumSize * newAspect) / -2;
    camera.right = (frustumSize * newAspect) / 2;
    camera.top = frustumSize / 2;
    camera.bottom = frustumSize / -2;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
  });

  // Animation loop
  function animate() {
    requestAnimationFrame(animate);
    renderer.render(scene, camera);
  }

  animate();
  console.log('[Scene] State-driven scene initialized');
  
  return { camera, scene };
}
