import { useEffect, useState } from 'react';
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
  /** Kept for mock-mode compat; ignored in live mode (server auto-rolls). */
  onRoll: () => void;
  /** Dismiss handler — in live mode this acknowledges deal; mock kicks off the deal. */
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

/**
 * Display-only dice modal. The Changsha server auto-rolls inside StartGame
 * (RollDice → Deal happens server-side without any client call), so this
 * modal exists purely to surface the rolled values + the resulting break
 * point. It briefly animates random faces until the DiceRolled event
 * arrives, then displays the real values and offers an OK to dismiss.
 *
 * Phase 3 removes the interactive Roll button entirely — earlier phases
 * had it invoking RollDice which the hub does not implement, producing
 * a hard error.
 */
export function DiceRollModal({ state, onConfirm }: DiceRollModalProps) {
  const open = state.phase === 'rollingDice' || state.phase === 'dealing';
  const hasRolled = state.lastDice !== undefined;

  const [animating, setAnimating] = useState(true);
  const [displayDice, setDisplayDice] = useState<[number, number]>([1, 1]);

  // Keep animating until the dice arrive from the server.
  useEffect(() => {
    if (!open) {
      setAnimating(true);
      return;
    }
    if (hasRolled) {
      setAnimating(false);
      return;
    }
    const interval = setInterval(() => {
      setDisplayDice([
        Math.floor(Math.random() * 6) + 1,
        Math.floor(Math.random() * 6) + 1,
      ]);
    }, 80);
    return () => clearInterval(interval);
  }, [open, hasRolled]);

  if (!open) return null;

  const shown: [number, number] = hasRolled && state.lastDice
    ? [state.lastDice.die1, state.lastDice.die2]
    : displayDice;

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
            {!hasRolled && (
              <Text align="center" block size={200}>
                Server is rolling…
              </Text>
            )}
            {hasRolled && state.breakPoint && (
              <Text align="center" block style={{ marginTop: 12 }}>
                Sum: {state.lastDice!.sum} — Break point: Wall{' '}
                {state.breakPoint.wallIndex}, stack {state.breakPoint.stackIndex}
              </Text>
            )}
            {hasRolled && !state.breakPoint && (
              <Text align="center" block style={{ marginTop: 12 }} size={200}>
                Sum: {state.lastDice!.sum}
              </Text>
            )}
          </DialogContent>
        </DialogBody>
        <DialogActions>
          {hasRolled && (
            <Button appearance="primary" onClick={onConfirm}>
              OK
            </Button>
          )}
        </DialogActions>
      </DialogSurface>
    </Dialog>
  );
}
