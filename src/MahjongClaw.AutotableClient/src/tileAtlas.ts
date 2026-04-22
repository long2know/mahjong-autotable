// Procedural tile atlas generator for Changsha 258 tiles (108 tiles = 27 unique faces × 4 copies)
// Generates canvas texture with all tile faces + back

import * as THREE from 'three';

const TILE_FACE_WIDTH = 128;
const TILE_FACE_HEIGHT = 192;

// Changsha 258: 3 suits × 9 ranks = 27 unique faces
const SUITS = ['bamboo', 'character', 'dot'] as const;
const RANKS = [1, 2, 3, 4, 5, 6, 7, 8, 9] as const;

type TileId = `${typeof SUITS[number]}-${typeof RANKS[number]}` | 'back';

interface UVRect {
  u: number;
  v: number;
  w: number;
  h: number;
}

export interface TileAtlas {
  texture: THREE.CanvasTexture;
  uvMap: Map<TileId, UVRect>;
}

function drawBambooSymbol(ctx: CanvasRenderingContext2D, x: number, y: number, rank: number): void {
  ctx.strokeStyle = '#2d5016';
  ctx.lineWidth = 4;
  
  // Draw horizontal bars representing bamboo sticks
  const barCount = Math.min(rank, 3);
  const barSpacing = 20;
  const startY = y - (barCount - 1) * barSpacing / 2;
  
  for (let i = 0; i < barCount; i++) {
    ctx.beginPath();
    ctx.moveTo(x - 25, startY + i * barSpacing);
    ctx.lineTo(x + 25, startY + i * barSpacing);
    ctx.stroke();
  }
}

function drawDotSymbol(ctx: CanvasRenderingContext2D, x: number, y: number, rank: number): void {
  ctx.fillStyle = '#8b0000';
  
  // Draw circles arranged in standard dot pattern
  const dotRadius = 12;
  const spacing = 30;
  
  if (rank === 1) {
    ctx.beginPath();
    ctx.arc(x, y, dotRadius, 0, Math.PI * 2);
    ctx.fill();
  } else if (rank <= 3) {
    for (let i = 0; i < rank; i++) {
      ctx.beginPath();
      ctx.arc(x, y - spacing + i * spacing, dotRadius, 0, Math.PI * 2);
      ctx.fill();
    }
  } else {
    // For 4+, arrange in grid pattern
    const cols = rank === 4 || rank === 5 ? 2 : 3;
    const rows = Math.ceil(rank / cols);
    for (let i = 0; i < rank; i++) {
      const row = Math.floor(i / cols);
      const col = i % cols;
      const offsetX = (col - (cols - 1) / 2) * spacing;
      const offsetY = (row - (rows - 1) / 2) * spacing;
      ctx.beginPath();
      ctx.arc(x + offsetX, y + offsetY, dotRadius, 0, Math.PI * 2);
      ctx.fill();
    }
  }
}

function drawCharacterSymbol(ctx: CanvasRenderingContext2D, x: number, y: number): void {
  // Draw the character "萬" (wan/man) symbol
  ctx.fillStyle = '#000080';
  ctx.font = 'bold 48px serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText('萬', x, y);
}

function drawTileFace(ctx: CanvasRenderingContext2D, suit: typeof SUITS[number], rank: typeof RANKS[number], x: number, y: number): void {
  // Background
  ctx.fillStyle = '#f5f5dc'; // Beige tile face
  ctx.fillRect(x, y, TILE_FACE_WIDTH, TILE_FACE_HEIGHT);
  
  // Border
  ctx.strokeStyle = '#8b7355';
  ctx.lineWidth = 3;
  ctx.strokeRect(x + 2, y + 2, TILE_FACE_WIDTH - 4, TILE_FACE_HEIGHT - 4);
  
  // Rank number at top
  ctx.fillStyle = '#000000';
  ctx.font = 'bold 36px sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'top';
  ctx.fillText(rank.toString(), x + TILE_FACE_WIDTH / 2, y + 15);
  
  // Suit symbol in middle
  const centerX = x + TILE_FACE_WIDTH / 2;
  const centerY = y + TILE_FACE_HEIGHT / 2 + 10;
  
  if (suit === 'bamboo') {
    drawBambooSymbol(ctx, centerX, centerY, rank);
  } else if (suit === 'dot') {
    drawDotSymbol(ctx, centerX, centerY, rank);
  } else if (suit === 'character') {
    drawCharacterSymbol(ctx, centerX, centerY);
  }
  
  // Suit label at bottom
  ctx.font = '14px sans-serif';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'bottom';
  ctx.fillStyle = '#333333';
  ctx.fillText(suit.toUpperCase(), x + TILE_FACE_WIDTH / 2, y + TILE_FACE_HEIGHT - 10);
}

function drawTileBack(ctx: CanvasRenderingContext2D, x: number, y: number): void {
  // Dark background
  ctx.fillStyle = '#2a4a3a';
  ctx.fillRect(x, y, TILE_FACE_WIDTH, TILE_FACE_HEIGHT);
  
  // Border
  ctx.strokeStyle = '#1a3a2a';
  ctx.lineWidth = 4;
  ctx.strokeRect(x + 2, y + 2, TILE_FACE_WIDTH - 4, TILE_FACE_HEIGHT - 4);
  
  // Diagonal hatch pattern
  ctx.strokeStyle = '#3a5a4a';
  ctx.lineWidth = 2;
  
  for (let i = -TILE_FACE_HEIGHT; i < TILE_FACE_WIDTH; i += 12) {
    ctx.beginPath();
    ctx.moveTo(x + i, y);
    ctx.lineTo(x + i + TILE_FACE_HEIGHT, y + TILE_FACE_HEIGHT);
    ctx.stroke();
  }
  
  for (let i = -TILE_FACE_HEIGHT; i < TILE_FACE_WIDTH; i += 12) {
    ctx.beginPath();
    ctx.moveTo(x + i, y + TILE_FACE_HEIGHT);
    ctx.lineTo(x + i + TILE_FACE_HEIGHT, y);
    ctx.stroke();
  }
  
  // Center diamond
  ctx.fillStyle = '#4a6a5a';
  ctx.beginPath();
  const cx = x + TILE_FACE_WIDTH / 2;
  const cy = y + TILE_FACE_HEIGHT / 2;
  const diamondSize = 30;
  ctx.moveTo(cx, cy - diamondSize);
  ctx.lineTo(cx + diamondSize, cy);
  ctx.lineTo(cx, cy + diamondSize);
  ctx.lineTo(cx - diamondSize, cy);
  ctx.closePath();
  ctx.fill();
}

export function generateTileAtlas(): TileAtlas {
  console.log('[TileAtlas] Generating procedural tile atlas...');
  
  // Layout: 9 tiles per row (one suit), 4 rows total (3 suits + back)
  const cols = 9;
  const rows = 4;
  const canvasWidth = cols * TILE_FACE_WIDTH;
  const canvasHeight = rows * TILE_FACE_HEIGHT;
  
  const canvas = document.createElement('canvas');
  canvas.width = canvasWidth;
  canvas.height = canvasHeight;
  
  const ctx = canvas.getContext('2d');
  if (!ctx) {
    throw new Error('Failed to get 2D context');
  }
  
  const uvMap = new Map<TileId, UVRect>();
  
  // Generate tile faces
  let row = 0;
  for (const suit of SUITS) {
    let col = 0;
    for (const rank of RANKS) {
      const tileId = `${suit}-${rank}` as TileId;
      const x = col * TILE_FACE_WIDTH;
      const y = row * TILE_FACE_HEIGHT;
      
      drawTileFace(ctx, suit, rank, x, y);
      
      // UV coordinates (normalized 0-1)
      uvMap.set(tileId, {
        u: x / canvasWidth,
        v: y / canvasHeight,
        w: TILE_FACE_WIDTH / canvasWidth,
        h: TILE_FACE_HEIGHT / canvasHeight,
      });
      
      col++;
    }
    row++;
  }
  
  // Draw tile back in last row, first position
  drawTileBack(ctx, 0, 3 * TILE_FACE_HEIGHT);
  uvMap.set('back', {
    u: 0,
    v: (3 * TILE_FACE_HEIGHT) / canvasHeight,
    w: TILE_FACE_WIDTH / canvasWidth,
    h: TILE_FACE_HEIGHT / canvasHeight,
  });
  
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.minFilter = THREE.LinearFilter;
  texture.magFilter = THREE.LinearFilter;
  
  console.log(`[TileAtlas] Generated ${uvMap.size} tile textures (${canvasWidth}x${canvasHeight})`);
  
  return { texture, uvMap };
}

// Helper to convert server tile label to our tileId format
export function tileIdFromLabel(label: string): TileId | null {
  // Label format: "🀐 1 Bamboo", "🀇 2 Character", "🀙 3 Dot"
  const match = label.match(/(\d+)\s+(Bamboo|Character|Dot)/i);
  if (!match || !match[1] || !match[2]) {
    return null;
  }
  
  const rank = parseInt(match[1], 10) as typeof RANKS[number];
  const suitName = match[2].toLowerCase();
  
  if (rank < 1 || rank > 9) {
    return null;
  }
  
  if (suitName === 'bamboo') {
    return `bamboo-${rank}`;
  } else if (suitName === 'character') {
    return `character-${rank}`;
  } else if (suitName === 'dot') {
    return `dot-${rank}`;
  }
  
  return null;
}
