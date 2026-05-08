import { useState, useEffect, useCallback } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  Text,
} from '@fluentui/react-components';
import type { ChangshaGameState, SeatIndex } from '../types';

interface DiceRollModalProps {
  state: ChangshaGameState;
  userSeat: SeatIndex;
  onRoll: () => void;
  onConfirm: () => void;
}

const diceStyle: React.CSSProperties = {
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  width: 64,
  height: 64,
  fontSize: 36,
  border: '2px solid #555',
  borderRadius: 10,
  margin: '0 8px',
  backgroundColor: '#fff',
  userSelect: 'none',
};

const DICE_FACES = ['⚀', '⚁', '⚂', '⚃', '⚄', '⚅'];

export function DiceRollModal({ state, userSeat, onRoll, onConfirm }: DiceRollModalProps) {
  const open = state.phase === 'rolling' || (state.phase === 'seating' && state.lastDice === undefined);
  const isUserBanker = state.bankerSeat === userSeat;
  const hasRolled = state.lastDice !== undefined;

  // Animate rolling
  const [animating, setAnimating] = useState(false);
  const [displayDice, setDisplayDice] = useState<[number, number]>([1, 1]);

  useEffect(() => {
    if (!animating) return;
    const interval = setInterval(() => {
      setDisplayDice([
        Math.floor(Math.random() * 6) + 1,
        Math.floor(Math.random() * 6) + 1,
      ]);
    }, 80);
    const timeout = setTimeout(() => {
      clearInterval(interval);
      setAnimating(false);
    }, 1200);
    return () => {
      clearInterval(interval);
      clearTimeout(timeout);
    };
  }, [animating]);

  // When dice land, show actual values
  const shown = animating ? displayDice : (state.lastDice ?? [1, 1]);

  const handleRoll = useCallback(() => {
    setAnimating(true);
    // Fire the actual roll after animation delay
    setTimeout(() => onRoll(), 1200);
  }, [onRoll]);

  // Only show when in the right phase
  if (state.phase !== 'rolling' && !(state.phase === 'seating')) return null;

  const bankerNick = state.seats.find((s) => s.index === state.bankerSeat)?.nick ?? 'Banker';

  return (
    <Dialog open={open} modalType="alert">
      <DialogSurface>
        <DialogTitle>🎲 Dice Roll — {bankerNick} (Banker)</DialogTitle>
        <DialogBody>
          <DialogContent>
            <div style={{ textAlign: 'center', padding: '16px 0' }}>
              <span style={diceStyle}>{DICE_FACES[shown[0] - 1]}</span>
              <span style={diceStyle}>{DICE_FACES[shown[1] - 1]}</span>
            </div>
            {hasRolled && !animating && state.breakPoint && (
              <Text align="center" block style={{ marginTop: 12 }}>
                Sum: {state.lastDice![0] + state.lastDice![1]} — Break point: Wall{' '}
                {state.breakPoint.wallIndex}, stack {state.breakPoint.stackIndex}
              </Text>
            )}
          </DialogContent>
        </DialogBody>
        <DialogActions>
          {!hasRolled && !animating && isUserBanker && (
            <Button appearance="primary" onClick={handleRoll}>
              Roll Dice
            </Button>
          )}
          {!hasRolled && !animating && !isUserBanker && (
            <Button appearance="primary" onClick={handleRoll}>
              Auto-Roll
            </Button>
          )}
          {hasRolled && !animating && (
            <Button appearance="primary" onClick={onConfirm}>
              Confirm &amp; Deal
            </Button>
          )}
        </DialogActions>
      </DialogSurface>
    </Dialog>
  );
}
