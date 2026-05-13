import { useMemo } from 'react';
import { Button, Text } from '@fluentui/react-components';
import type { ChangshaGameState, SeatIndex, MeldState } from '../types';
import {
  findAddedKongs,
  findConcealedKongs,
  sortHandForDisplay,
  tileFromId,
  tileLabel,
} from '../tileUtils';
import { TileFace } from './TileFace';

interface PlayerHandPanelProps {
  state: ChangshaGameState;
  userSeat: SeatIndex;
  onDiscard: (tileId: number) => void;
  /** Optional: declare a kong (concealed or added). Passed a single tile id. */
  onDeclareKong?: (tileIds: number[]) => void;
  /** Optional: declare self-draw win (zimo). */
  onDeclareWin?: () => void;
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

export function PlayerHandPanel({
  state,
  userSeat,
  onDiscard,
  onDeclareKong,
  onDeclareWin,
}: PlayerHandPanelProps) {
  const hand = state.hands.find((h) => h.seatIndex === userSeat);
  // Memoize the sorted view — sortHandForDisplay returns a new array and
  // does NOT mutate state.hand.concealed.
  const sortedConcealed = useMemo(
    () => (hand ? sortHandForDisplay(hand.concealed) : []),
    [hand]
  );

  const concealedKongs = useMemo(
    () => (hand ? findConcealedKongs(hand.concealed) : []),
    [hand]
  );
  const addedKongs = useMemo(
    () => (hand ? findAddedKongs(hand.concealed, hand.melds) : []),
    [hand]
  );

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

  const canDeclareKong = isMyTurn && (concealedKongs.length > 0 || addedKongs.length > 0);
  // Zimo (self-draw win) hint: only enabled on the user's discard phase.
  // Server is authoritative; this is a UI affordance only.
  const canDeclareWinHint = isMyTurn;

  const handleConcealedKong = (tileIds: number[]) => {
    if (onDeclareKong) onDeclareKong(tileIds);
  };

  const handleAddedKong = (tileId: number) => {
    if (onDeclareKong) onDeclareKong([tileId]);
  };

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
        {sortedConcealed.map((tile) => (
          <div
            key={tile.id}
            style={{ display: 'inline-flex', flexDirection: 'column', alignItems: 'center' }}
          >
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

      <div
        style={{
          marginTop: 8,
          display: 'flex',
          alignItems: 'center',
          gap: 8,
          flexWrap: 'wrap',
        }}
      >
        <Text size={200}>
          {isMyTurn
            ? '🔵 Your turn — click a tile to discard'
            : `⏳ Waiting for seat ${state.activeSeat ?? '?'}`}
        </Text>

        {canDeclareWinHint && onDeclareWin && (
          <Button
            appearance="primary"
            size="small"
            onClick={onDeclareWin}
            style={{ backgroundColor: '#dc2626' }}
          >
            🏆 Zimo! (Self-draw win)
          </Button>
        )}

        {canDeclareKong &&
          concealedKongs.map((k) => (
            <Button
              key={`ck-${k.tileId}`}
              appearance="outline"
              size="small"
              onClick={() => handleConcealedKong(k.tileIds)}
              title="Declare concealed kong (4 of a kind in hand)"
            >
              暗杠 Concealed Kong ({tileLabel(k.tile)})
            </Button>
          ))}

        {canDeclareKong &&
          addedKongs.map((k) => (
            <Button
              key={`ak-${k.tileId}`}
              appearance="outline"
              size="small"
              onClick={() => handleAddedKong(k.tileId)}
              title="Add the matching tile from your hand to an existing pung"
            >
              加杠 Added Kong ({tileLabel(k.tile)})
            </Button>
          ))}
      </div>
    </div>
  );
}
