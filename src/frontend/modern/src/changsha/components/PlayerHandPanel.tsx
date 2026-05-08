import { Button, Text } from '@fluentui/react-components';
import type { ChangshaGameState, SeatIndex, MeldState } from '../types';
import { tileGlyph, tileLabel, tileFromId } from '../tileUtils';

interface PlayerHandPanelProps {
  state: ChangshaGameState;
  userSeat: SeatIndex;
  onDiscard: (tileId: number) => void;
}

const tileButtonStyle: React.CSSProperties = {
  fontSize: 28,
  minWidth: 40,
  height: 48,
  padding: '2px 6px',
  margin: 2,
  cursor: 'pointer',
  border: '1px solid #ccc',
  borderRadius: 6,
  backgroundColor: '#fffde7',
};

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
      }}
    >
      {meld.tileIds.map((tid) => {
        const t = tileFromId(tid);
        return (
          <span key={tid} style={{ fontSize: 24 }} title={tileLabel(t)}>
            {isConcealed ? '🀫' : tileGlyph(t)}
          </span>
        );
      })}
      <Text size={100} style={{ alignSelf: 'flex-end' }}>
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
      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center' }}>
        {hand.concealed.map((tile) => (
          <div key={tile.id} style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center' }}>
            <button
              style={{
                ...tileButtonStyle,
                cursor: isMyTurn ? 'pointer' : 'default',
                opacity: isMyTurn ? 1 : 0.7,
              }}
              title={`${tileLabel(tile)} — click to discard`}
              disabled={!isMyTurn}
              onClick={() => onDiscard(tile.id)}
            >
              {tileGlyph(tile)}
            </button>
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
