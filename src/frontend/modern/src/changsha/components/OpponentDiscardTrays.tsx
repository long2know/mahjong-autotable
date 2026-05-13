import { Text } from '@fluentui/react-components';
import type { ChangshaGameState, SeatIndex } from '../types';
import { TileFace } from './TileFace';
import { tileFromId, tileLabel } from '../tileUtils';

interface OpponentDiscardTraysProps {
  state: ChangshaGameState;
  /** The seat the local player owns; their tray is rendered by PlayerHandPanel. */
  userSeat: SeatIndex;
}

/**
 * Bucket the discard log into per-seat tile-id lists, preserving the
 * discard order within each seat. Falls back to no-attribution (returns
 * empty buckets) when the live stream hasn't emitted any discards yet.
 */
function bucketBySeat(state: ChangshaGameState): Record<SeatIndex, number[]> {
  const out: Record<SeatIndex, number[]> = { 0: [], 1: [], 2: [], 3: [] };
  const log = state.discardLog;
  if (!log || log.length === 0) return out;
  for (const entry of log) {
    out[entry.seatIndex].push(entry.tileId);
  }
  return out;
}

/**
 * Render compact discard previews around the autotable viewport.
 * Position is computed relative to the user's seat:
 *   - Next CCW seat (user+1) → right
 *   - Across       (user+2) → top
 *   - Next CW seat (user+3) → left
 *
 * Phase 3 surfaces the per-seat discards visibly so Stephen can see what
 * bots have thrown. Per-seat attribution comes from the reducer's
 * `discardLog` (parallel to `discardPile`), populated by TileDiscarded
 * events and the FullState snapshot.
 */
export function OpponentDiscardTrays({ state, userSeat }: OpponentDiscardTraysProps) {
  const seatRelative = (seat: SeatIndex): 'top' | 'left' | 'right' | 'self' => {
    const diff = (seat - userSeat + 4) % 4;
    if (diff === 0) return 'self';
    if (diff === 1) return 'right';
    if (diff === 2) return 'top';
    return 'left';
  };

  const buckets = bucketBySeat(state);
  const byPosition: Record<'top' | 'left' | 'right', SeatIndex | null> = {
    top: null,
    left: null,
    right: null,
  };
  for (const seat of state.seats) {
    const pos = seatRelative(seat.index);
    if (pos !== 'self') byPosition[pos] = seat.index;
  }

  const seatNick = (idx: SeatIndex | null): string => {
    if (idx === null) return '';
    return state.seats.find((s) => s.index === idx)?.nick ?? `Seat ${idx}`;
  };

  return (
    <div
      style={{
        display: 'grid',
        gridTemplateAreas: '". top ." "left center right" ". bottom ."',
        gridTemplateColumns: 'minmax(140px, 1fr) 2fr minmax(140px, 1fr)',
        gridTemplateRows: 'auto 1fr auto',
        gap: 8,
        margin: '12px 0',
      }}
    >
      <DiscardTray
        area="top"
        label={`${seatNick(byPosition.top)} (across)`}
        tiles={byPosition.top !== null ? buckets[byPosition.top] : []}
        orientation="horizontal"
      />
      <DiscardTray
        area="left"
        label={seatNick(byPosition.left)}
        tiles={byPosition.left !== null ? buckets[byPosition.left] : []}
        orientation="vertical"
      />
      <div
        style={{
          gridArea: 'center',
          minHeight: 40,
          color: '#64748b',
          fontSize: 12,
          textAlign: 'center',
          alignSelf: 'center',
        }}
      >
        <Text size={200}>
          Discard pile: <strong>{state.discardPile.length}</strong> tile(s) · wall remaining{' '}
          <strong>{state.wallRemaining}</strong>
        </Text>
      </div>
      <DiscardTray
        area="right"
        label={seatNick(byPosition.right)}
        tiles={byPosition.right !== null ? buckets[byPosition.right] : []}
        orientation="vertical"
      />
    </div>
  );
}

interface DiscardTrayProps {
  area: 'top' | 'left' | 'right';
  label: string;
  tiles: number[];
  orientation: 'horizontal' | 'vertical';
}

function DiscardTray({ area, label, tiles, orientation }: DiscardTrayProps) {
  const isVertical = orientation === 'vertical';
  return (
    <div
      style={{
        gridArea: area,
        padding: 6,
        borderRadius: 8,
        background: '#f8fafc',
        border: '1px dashed #cbd5e1',
        minHeight: isVertical ? 80 : 56,
        display: 'flex',
        flexDirection: 'column',
        gap: 4,
      }}
    >
      <Text size={100} style={{ color: '#475569', fontWeight: 600 }}>
        {label || '\u00A0'}
      </Text>
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: isVertical ? 'repeat(2, 22px)' : 'repeat(6, 22px)',
          gap: 2,
          justifyContent: isVertical ? 'center' : 'start',
        }}
      >
        {tiles.map((id, i) => {
          const tile = tileFromId(id);
          return (
            <div
              key={`${id}-${i}`}
              style={{ transform: 'scale(0.7)', transformOrigin: 'top left' }}
            >
              <TileFace tile={tile} size="sm" title={tileLabel(tile)} />
            </div>
          );
        })}
      </div>
    </div>
  );
}

