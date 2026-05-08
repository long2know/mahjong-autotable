import { Text } from '@fluentui/react-components';
import type { ChangshaGameState, SeatIndex, MeldState } from '../types';
import { tileLabel, tileFromId } from '../tileUtils';
import { TileFace } from './TileFace';

interface PlayerHandPanelProps {
  state: ChangshaGameState;
  userSeat: SeatIndex;
  onDiscard: (tileId: number) => void;
}

function renderMeld(meld: MeldState, idx: number) {
  const isConcealed = meld.type === 'concealedKong';
  return (
    <span
      key={idx}
      style={{
        display: 'inline-flex',
        gap: 2,
        marginRight: 12,
        padding: '2px 4px',
        border: '1px solid #aaa',
        borderRadius: 6,
        backgroundColor: isConcealed ? '#e0e0e0' : '#e8f5e9',
        alignItems: 'center',
      }}
    >
      {meld.tileIds.map((tid) => {
        const t = tileFromId(tid);
        return (
          <TileFace
            key={tid}
            tile={t}
            size="sm"
            faceDown={isConcealed}
            title={tileLabel(t)}
          />
        );
      })}
      <Text size={100} style={{ alignSelf: 'flex-end', marginLeft: 4 }}>
        {meld.type}
      </Text>
    </span>
  );
}

export function PlayerHandPanel({ state, userSeat, onDiscard }: PlayerHandPanelProps) {
  const hand = state.hands.find((h) => h.seatIndex === userSeat);
  if (!hand) {
    return (
      <div style={{ textAlign: 'center', padding: 16 }}>
        <Text>No tiles dealt yet.</Text>
      </div>
    );
  }

  const isMyTurn = state.activeSeat === userSeat && state.phase === 'awaitingDiscard';
  const claimTileId =
    state.phase === 'awaitingClaim' && state.discardPile.length
      ? state.discardPile[state.discardPile.length - 1].id
      : undefined;

  return (
    <div style={{ padding: '8px 16px' }}>
      {hand.melds.length > 0 && (
        <div style={{ marginBottom: 8 }}>
          <Text size={200} weight="semibold">
            Melds:{' '}
          </Text>
          {hand.melds.map((m, i) => renderMeld(m, i))}
        </div>
      )}
      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: 4 }}>
        {hand.concealed.map((tile) => (
          <div key={tile.id} style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center' }}>
            <TileFace
              tile={tile}
              size="md"
              highlighted={tile.id === claimTileId}
              disabled={!isMyTurn}
              onClick={isMyTurn ? () => onDiscard(tile.id) : undefined}
              title={`${tileLabel(tile)} — ${isMyTurn ? 'click to discard' : 'waiting'}`}
            />
            <Text size={100}>{tileLabel(tile)}</Text>
          </div>
        ))}
      </div>
      <Text size={200} style={{ marginTop: 4 }} block>
        {isMyTurn ? '🔵 Your turn — click a tile to discard' : `⏳ Waiting for seat ${state.activeSeat ?? '?'}`}
      </Text>
    </div>
  );
}
