import type { Suit, Tile, Wind } from './types';

/** Unicode glyph lookup: suit → rank (1-9) → character */
const TILE_GLYPHS: Record<Suit, string[]> = {
  wan: ['🀇', '🀈', '🀉', '🀊', '🀋', '🀌', '🀍', '🀎', '🀏'],
  tong: ['🀙', '🀚', '🀛', '🀜', '🀝', '🀞', '🀟', '🀠', '🀡'],
  tiao: ['🀐', '🀑', '🀒', '🀓', '🀔', '🀕', '🀖', '🀗', '🀘'],
};

const SUIT_LABELS: Record<Suit, string> = {
  wan: '万',
  tong: '筒',
  tiao: '条',
};

const SUITS: Suit[] = ['wan', 'tong', 'tiao'];

/** Derive suit and rank from a tile id (0-107, per SignalR contract) */
export function tileFromId(id: number): Tile {
  const suit = SUITS[Math.floor(id / 4 / 9)];
  const rank = (Math.floor(id / 4) % 9) + 1;
  return { id, suit, rank };
}

export function tileGlyph(tile: Tile): string {
  return TILE_GLYPHS[tile.suit][tile.rank - 1];
}

export function tileLabel(tile: Tile): string {
  return `${tile.rank}${SUIT_LABELS[tile.suit]}`;
}

const WIND_LABELS: Record<Wind, string> = {
  east: '东',
  south: '南',
  west: '西',
  north: '北',
};

export function windLabel(wind: Wind): string {
  return WIND_LABELS[wind];
}

export function windEnglish(wind: Wind): string {
  return wind.charAt(0).toUpperCase() + wind.slice(1);
}

/** Generate a 108-tile set (suits only, 4 copies each) as Tile[] */
export function generateFullTileSet(): Tile[] {
  return Array.from({ length: 108 }, (_, i) => tileFromId(i));
}
