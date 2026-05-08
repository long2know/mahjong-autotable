import type { Suit, Tile, SeatIndex, Wind } from './types';

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

export function seatWindForIndex(bankerSeat: SeatIndex, seatIndex: SeatIndex): Wind {
  const winds: Wind[] = ['east', 'south', 'west', 'north'];
  return winds[(seatIndex - bankerSeat + 4) % 4];
}

/** Generate a 108-tile set (suits only, 4 copies each) */
export function generateFullTileSet(): Tile[] {
  const tiles: Tile[] = [];
  const suits: Suit[] = ['wan', 'tong', 'tiao'];
  let id = 0;
  for (const suit of suits) {
    for (let rank = 1; rank <= 9; rank++) {
      for (let copy = 0; copy < 4; copy++) {
        tiles.push({ suit, rank: rank as Tile['rank'], id: `${suit}-${rank}-${copy}` });
        id++;
      }
    }
  }
  return tiles;
}
