import { useCallback, useState } from 'react';
import type {
  ChangshaGameState,
  SeatIndex,
  SeatHand,
  PendingClaim,
  DiceResult,
} from './types';
import { generateFullTileSet } from './tileUtils';

function shuffle<T>(arr: T[]): T[] {
  const a = [...arr];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [a[i], a[j]] = [a[j], a[i]];
  }
  return a;
}

function initialState(): ChangshaGameState {
  return {
    gameId: 'demo-game-001',
    bankerSeat: 0,
    prevalentWind: 'east',
    currentRound: 1,
    currentHand: 1,
    seats: [
      { index: 0, nick: 'You', isBot: false, seatWind: 'east', score: 0 },
      { index: 1, nick: 'Bot-South', isBot: true, seatWind: 'south', score: 0 },
      { index: 2, nick: 'Bot-West', isBot: true, seatWind: 'west', score: 0 },
      { index: 3, nick: 'Bot-North', isBot: true, seatWind: 'north', score: 0 },
    ],
    phase: 'seating',
    hands: [],
    wallRemaining: 108,
    discardPile: [],
  };
}

/**
 * Phase 1 mock hook — returns a ChangshaGameState driven by useState
 * plus demo actions to cycle through UI phases.
 * Phase 2 replaces this with a real SignalR client.
 */
export function useChangshaMockGame() {
  const [state, setState] = useState<ChangshaGameState>(initialState);

  const rollDice = useCallback(() => {
    const die1 = Math.floor(Math.random() * 6) + 1;
    const die2 = Math.floor(Math.random() * 6) + 1;
    const sum = die1 + die2;
    const wallIndex = ((sum - 1) % 4);
    const stackIndex = sum;
    const dice: DiceResult = { die1, die2, sum };
    setState((s) => ({
      ...s,
      phase: 'rollingDice',
      lastDice: dice,
      breakPoint: { wallIndex, stackIndex, tileIndex: wallIndex * 27 + stackIndex },
    }));
  }, []);

  const confirmDice = useCallback(() => {
    setState((s) => ({ ...s, phase: 'dealing' }));
  }, []);

  const dealMock = useCallback(() => {
    const tiles = shuffle(generateFullTileSet());
    const hands: SeatHand[] = [0, 1, 2, 3].map((si) => {
      const start = si * 13;
      const count = si === 0 ? 14 : 13; // banker gets 14
      return {
        seatIndex: si as SeatIndex,
        concealed: tiles.slice(start, start + count),
        melds: [],
      };
    });
    const dealt = 13 * 4 + 1; // 53 tiles dealt
    setState((s) => ({
      ...s,
      phase: 'awaitingDiscard',
      hands,
      wallRemaining: 108 - dealt,
      activeSeat: 0,
    }));
  }, []);

  const discard = useCallback((tileId: number) => {
    setState((s) => {
      const myHand = s.hands.find((h) => h.seatIndex === 0);
      if (!myHand) return s;
      const tile = myHand.concealed.find((t) => t.id === tileId);
      if (!tile) return s;
      return {
        ...s,
        hands: s.hands.map((h) =>
          h.seatIndex === 0
            ? { ...h, concealed: h.concealed.filter((t) => t.id !== tileId) }
            : h
        ),
        discardPile: [...s.discardPile, tile],
        activeSeat: 1 as SeatIndex,
      };
    });
  }, []);

  const simulateClaimWindow = useCallback(() => {
    const claims: PendingClaim[] = [
      { seatIndex: 0, type: 'pung' },
      { seatIndex: 0, type: 'chow' },
    ];
    setState((s) => ({
      ...s,
      phase: 'awaitingClaim',
      pendingClaims: claims,
    }));
  }, []);

  const resolveClaim = useCallback((_claimType: string | null) => {
    setState((s) => ({
      ...s,
      phase: 'awaitingDiscard',
      pendingClaims: undefined,
    }));
  }, []);

  const simulateWin = useCallback(() => {
    setState((s) => ({
      ...s,
      phase: 'scoring',
      lastWin: {
        winningSeatIndex: 0,
        winType: 'selfDraw',
        winPattern: 'fullFlush',
        winningTileId: 0,
        sourceSeatIndex: 0,
      },
      lastScore: {
        category: 'bigWin',
        basePoints: 6,
        payments: [
          { fromSeatIndex: 1, toSeatIndex: 0, amount: 6, reason: 'bigWin-selfDraw' },
          { fromSeatIndex: 2, toSeatIndex: 0, amount: 6, reason: 'bigWin-selfDraw' },
          { fromSeatIndex: 3, toSeatIndex: 0, amount: 6, reason: 'bigWin-selfDraw' },
        ],
      },
    }));
  }, []);

  const continueAfterScoring = useCallback(() => {
    setState((s) => ({
      ...s,
      phase: 'awaitingDiscard',
      lastWin: undefined,
      lastScore: undefined,
      seats: s.seats.map((seat) => {
        const score = s.lastScore;
        if (!score) return seat;
        let delta = 0;
        for (const p of score.payments) {
          if (p.toSeatIndex === seat.index) delta += p.amount;
          if (p.fromSeatIndex === seat.index) delta -= p.amount;
        }
        return { ...seat, score: seat.score + delta };
      }),
    }));
  }, []);

  const resetDemo = useCallback(() => {
    setState(initialState());
  }, []);

  return {
    state,
    actions: {
      rollDice,
      confirmDice,
      dealMock,
      discard,
      simulateClaimWindow,
      resolveClaim,
      simulateWin,
      continueAfterScoring,
      resetDemo,
    },
  };
}
