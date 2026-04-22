// Tile mesh factory with atlas texture support

import * as THREE from 'three';
import type { TileAtlas } from './tileAtlas';

const TILE_WIDTH = 0.8;
const TILE_HEIGHT = 1.2;
const TILE_DEPTH = 0.6;

let tileGeometry: THREE.BoxGeometry | null = null;
let sideMaterial: THREE.MeshStandardMaterial | null = null;

function getSharedGeometry(): THREE.BoxGeometry {
  if (!tileGeometry) {
    tileGeometry = new THREE.BoxGeometry(TILE_WIDTH, TILE_HEIGHT, TILE_DEPTH);
  }
  return tileGeometry;
}

function getSharedSideMaterial(): THREE.MeshStandardMaterial {
  if (!sideMaterial) {
    sideMaterial = new THREE.MeshStandardMaterial({ color: 0xf5f5dc });
  }
  return sideMaterial;
}

export function createTileMesh(
  tileId: string,
  faceUp: boolean,
  atlas: TileAtlas
): THREE.Mesh {
  const geometry = getSharedGeometry();
  const sideMat = getSharedSideMaterial();
  
  // Get UV rect from atlas
  const uvRect = atlas.uvMap.get(tileId as any) ?? atlas.uvMap.get('back')!;
  
  // Create face material with correct UV mapping
  const faceMaterial = new THREE.MeshStandardMaterial({
    map: atlas.texture,
  });
  
  // Clone geometry to set UVs for this specific tile
  const tileGeometry = geometry.clone();
  const uvAttribute = tileGeometry.getAttribute('uv') as THREE.BufferAttribute;
  const uvArray = uvAttribute.array as Float32Array;
  
  // Top face is geometry face index 2 (indices 8-11 in UV array)
  // Each face has 4 vertices, top face starts at vertex 8
  const topFaceUVStart = 8;
  uvArray[topFaceUVStart * 2 + 0] = uvRect.u;          // bottom-left u
  uvArray[topFaceUVStart * 2 + 1] = uvRect.v + uvRect.h; // bottom-left v
  uvArray[(topFaceUVStart + 1) * 2 + 0] = uvRect.u + uvRect.w; // bottom-right u
  uvArray[(topFaceUVStart + 1) * 2 + 1] = uvRect.v + uvRect.h; // bottom-right v
  uvArray[(topFaceUVStart + 2) * 2 + 0] = uvRect.u;          // top-left u
  uvArray[(topFaceUVStart + 2) * 2 + 1] = uvRect.v;          // top-left v
  uvArray[(topFaceUVStart + 3) * 2 + 0] = uvRect.u + uvRect.w; // top-right u
  uvArray[(topFaceUVStart + 3) * 2 + 1] = uvRect.v;          // top-right v
  
  uvAttribute.needsUpdate = true;
  
  // Back material (for bottom face)
  const backUVRect = atlas.uvMap.get('back')!;
  const backMaterial = new THREE.MeshStandardMaterial({
    map: atlas.texture,
  });
  
  // Bottom face is geometry face index 3 (indices 12-15 in UV array)
  const bottomFaceUVStart = 12;
  uvArray[bottomFaceUVStart * 2 + 0] = backUVRect.u;
  uvArray[bottomFaceUVStart * 2 + 1] = backUVRect.v + backUVRect.h;
  uvArray[(bottomFaceUVStart + 1) * 2 + 0] = backUVRect.u + backUVRect.w;
  uvArray[(bottomFaceUVStart + 1) * 2 + 1] = backUVRect.v + backUVRect.h;
  uvArray[(bottomFaceUVStart + 2) * 2 + 0] = backUVRect.u;
  uvArray[(bottomFaceUVStart + 2) * 2 + 1] = backUVRect.v;
  uvArray[(bottomFaceUVStart + 3) * 2 + 0] = backUVRect.u + backUVRect.w;
  uvArray[(bottomFaceUVStart + 3) * 2 + 1] = backUVRect.v;
  
  // Material array: +X, -X, +Y (top), -Y (bottom), +Z, -Z
  const materials = [
    sideMat,      // +X (right)
    sideMat,      // -X (left)
    faceUp ? faceMaterial : backMaterial,  // +Y (top)
    faceUp ? backMaterial : faceMaterial,  // -Y (bottom)
    sideMat,      // +Z (front)
    sideMat,      // -Z (back)
  ];
  
  const mesh = new THREE.Mesh(tileGeometry, materials);
  mesh.castShadow = true;
  mesh.receiveShadow = true;
  
  return mesh;
}

export function flipTileMesh(mesh: THREE.Mesh, faceUp: boolean): void {
  // Rotate tile 180° around X-axis to flip
  const targetRotation = faceUp ? 0 : Math.PI;
  mesh.rotation.x = targetRotation;
}
