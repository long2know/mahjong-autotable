import * as THREE from 'three';

export function initScene(container: HTMLElement): void {
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
  const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
  scene.add(ambientLight);

  const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
  directionalLight.position.set(5, 10, 5);
  scene.add(directionalLight);

  // Table surface
  const tableGeometry = new THREE.PlaneGeometry(40, 40);
  const tableMaterial = new THREE.MeshStandardMaterial({ color: 0x1a5a3a });
  const table = new THREE.Mesh(tableGeometry, tableMaterial);
  table.rotation.x = -Math.PI / 2;
  scene.add(table);

  // Create 136-tile wall (Changsha 258: 108 tiles + 28 extra for standard mahjong layout visualization)
  // Layout: 4 walls × 17 tiles × 2 rows
  const tileWidth = 0.8;
  const tileHeight = 1.2;
  const tileDepth = 0.6;
  const gap = 0.05;

  const tileGeometry = new THREE.BoxGeometry(tileWidth, tileHeight, tileDepth);
  
  // Placeholder materials: dark back, lighter edges
  const backMaterial = new THREE.MeshStandardMaterial({ color: 0x2a2a2a });
  const edgeMaterial = new THREE.MeshStandardMaterial({ color: 0x4a4a4a });
  
  // Use multi-material for different faces
  const tileMaterials = [
    edgeMaterial, // +X
    edgeMaterial, // -X
    backMaterial, // +Y (top, face-down shows back)
    backMaterial, // -Y (bottom)
    edgeMaterial, // +Z
    edgeMaterial, // -Z
  ];

  const tilesPerWall = 17;
  const rowsPerWall = 2;
  const wallOffset = 15;

  // East wall (bottom, facing -Z)
  for (let row = 0; row < rowsPerWall; row++) {
    for (let i = 0; i < tilesPerWall; i++) {
      const tile = new THREE.Mesh(tileGeometry, tileMaterials);
      tile.position.set(
        (i - tilesPerWall / 2) * (tileWidth + gap),
        tileHeight / 2 + row * tileHeight,
        wallOffset
      );
      scene.add(tile);
    }
  }

  // South wall (right, facing -X)
  for (let row = 0; row < rowsPerWall; row++) {
    for (let i = 0; i < tilesPerWall; i++) {
      const tile = new THREE.Mesh(tileGeometry, tileMaterials);
      tile.position.set(
        wallOffset,
        tileHeight / 2 + row * tileHeight,
        (i - tilesPerWall / 2) * (tileWidth + gap)
      );
      tile.rotation.y = Math.PI / 2;
      scene.add(tile);
    }
  }

  // West wall (top, facing +Z)
  for (let row = 0; row < rowsPerWall; row++) {
    for (let i = 0; i < tilesPerWall; i++) {
      const tile = new THREE.Mesh(tileGeometry, tileMaterials);
      tile.position.set(
        (tilesPerWall / 2 - i) * (tileWidth + gap),
        tileHeight / 2 + row * tileHeight,
        -wallOffset
      );
      scene.add(tile);
    }
  }

  // North wall (left, facing +X)
  for (let row = 0; row < rowsPerWall; row++) {
    for (let i = 0; i < tilesPerWall; i++) {
      const tile = new THREE.Mesh(tileGeometry, tileMaterials);
      tile.position.set(
        -wallOffset,
        tileHeight / 2 + row * tileHeight,
        (tilesPerWall / 2 - i) * (tileWidth + gap)
      );
      tile.rotation.y = Math.PI / 2;
      scene.add(tile);
    }
  }

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
  console.log('Scene initialized: 136 tiles in 4-wall formation');
}
