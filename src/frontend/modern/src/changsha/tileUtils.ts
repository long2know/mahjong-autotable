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

/**
 * Hand-display sort order for Changsha: Wan(0) → Tiao(1) → Tong/Bing(2),
 * then by rank 1..9. Stable for ties on (suit, rank); secondary tiebreak
 * uses tile id so the rendered order is deterministic per server-issued
 * id (no in-suit duplicate reshuffles when a draw arrives).
 */
const SUIT_DISPLAY_ORDER: Record<Suit, number> = {
  wan: 0,
  tiao: 1,
  tong: 2,
};

export function compareTilesForDisplay(a: Tile, b: Tile): number {
  const sa = SUIT_DISPLAY_ORDER[a.suit];
  const sb = SUIT_DISPLAY_ORDER[b.suit];
  if (sa !== sb) return sa - sb;
  if (a.rank !== b.rank) return a.rank - b.rank;
  return a.id - b.id;
}

/** Pure, returns a new array; does NOT mutate the input. */
export function sortHandForDisplay(tiles: readonly Tile[]): Tile[] {
  return [...tiles].sort(compareTilesForDisplay);
}

/**
 * Group concealed tiles into "logical" buckets (a logical tile is the
 * shared identity for the 4 physical copies of the same suit+rank,
 * obtained via Math.floor(id / 4)). Used by kong/win detection.
 */
export function logicalIdOf(tileId: number): number {
  return Math.floor(tileId / 4);
}

export interface ConcealedKongCandidate {
  tileId: number;
  tileIds: number[];
  tile: Tile;
}

export function findConcealedKongs(concealed: readonly Tile[]): ConcealedKongCandidate[] {
  const buckets = new Map<number, Tile[]>();
  for (const t of concealed) {
    const k = logicalIdOf(t.id);
    const list = buckets.get(k);
    if (list) list.push(t);
    else buckets.set(k, [t]);
  }
  const out: ConcealedKongCandidate[] = [];
  for (const tiles of buckets.values()) {
    if (tiles.length >= 4) {
      const sorted = [...tiles].sort((a, b) => a.id - b.id);
      out.push({
        tileId: sorted[0].id,
        tileIds: sorted.slice(0, 4).map((t) => t.id),
        tile: sorted[0],
      });
    }
  }
  return out.sort((a, b) => compareTilesForDisplay(a.tile, b.tile));
}

export interface AddedKongCandidate {
  tileId: number;
  tile: Tile;
}

/**
 * Detect added-kong opportunities: scan the player's exposed pungs and
 * find any matching concealed tile. The server's DeclareKong handler
 * accepts a single tileId for added-kong (matched against existing pungs).
 */
export function findAddedKongs(
  concealed: readonly Tile[],
  melds: readonly { type: string; tileIds: number[] }[]
): AddedKongCandidate[] {
  const concealedByLogical = new Map<number, Tile>();
  for (const t of concealed) concealedByLogical.set(logicalIdOf(t.id), t);
  const out: AddedKongCandidate[] = [];
  for (const meld of melds) {
    if (meld.type !== 'pung') continue;
    if (!meld.tileIds.length) continue;
    const logical = logicalIdOf(meld.tileIds[0]);
    const match = concealedByLogical.get(logical);
    if (match) out.push({ tileId: match.id, tile: match });
  }
  return out;
}

export interface ChowCombo {
  /** The two concealed tile ids that pair with the discard. */
  tileIds: [number, number];
  /** Resolved tiles (for preview rendering). */
  tiles: [Tile, Tile];
}

/**
 * Compute all valid chow combinations the user can claim. Returns the
 * pairs of CONCEALED tile ids — the discarded tile is NOT included
 * because the backend already knows the discard.
 *
 * Rules (Changsha v1): a chow is three consecutive tiles of the same suit.
 * Given a discard at suit S rank R, valid concealed pairs are:
 *   - (R-2, R-1)  → discard completes the right end
 *   - (R-1, R+1)  → discard is the middle
 *   - (R+1, R+2)  → discard completes the left end
 *
 * When the player holds multiple copies of a tile (e.g. two 5-Wan), each
 * physical copy generates its own combo. Duplicate unordered pairs are
 * filtered out.
 */
export function computeChowCombos(concealed: readonly Tile[], discard: Tile): ChowCombo[] {
  const sameSuit = concealed.filter((t) => t.suit === discard.suit);
  const byRank = new Map<number, Tile[]>();
  for (const t of sameSuit) {
    const list = byRank.get(t.rank);
    if (list) list.push(t);
    else byRank.set(t.rank, [t]);
  }
  const out: ChowCombo[] = [];
  const tryPair = (rA: number, rB: number) => {
    const aTiles = byRank.get(rA);
    const bTiles = byRank.get(rB);
    if (!aTiles || !bTiles) return;
    for (const a of aTiles) {
      for (const b of bTiles) {
        if (a.id === b.id) continue;
        out.push({ tileIds: [a.id, b.id], tiles: [a, b] });
      }
    }
  };
  const r = discard.rank;
  if (r >= 3) tryPair(r - 2, r - 1);
  if (r >= 2 && r <= 8) tryPair(r - 1, r + 1);
  if (r <= 7) tryPair(r + 1, r + 2);
  const seen = new Set<string>();
  return out.filter((c) => {
    const lo = Math.min(c.tileIds[0], c.tileIds[1]);
    const hi = Math.max(c.tileIds[0], c.tileIds[1]);
    const key = `${lo}-${hi}`;
    if (seen.has(key)) return false;
    seen.add(key);
    return true;
  });
}
