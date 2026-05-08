/**
 * Reducer that folds Changsha SignalR server events into a single
 * client-side ChangshaGameState used by the React UI.
 *
 * Each action corresponds 1:1 to a server event from
 * docs/rules/changsha-signalr-contract.md.
 */
import type {
  ChangshaGameState,
  GamePhase,
  MeldState,
  SeatHand,
  SeatIndex,
  SeatInfo,
  Wind,
} from './types';
import { tileFromId } from './tileUtils';
import type {
  GameCreatedEvent,
  PlayerSeatedEvent,
  GameStartedEvent,
  DiceRolledEvent,
  BreakPointSetEvent,
  TilesDealtEvent,
  TurnStartedEvent,
  TileDrawnEvent,
  TileDiscardedEvent,
  ClaimWindowOpenEvent,
  ClaimMadeEvent,
  KongReplacementDrawnEvent,
  WinDeclaredEvent,
  ScoringCompleteEvent,
  BankerRotatedEvent,
  RoundChangedEvent,
  HandFinishedEvent,
  GameEndedEvent,
} from './signalrClient';

export type GameAction =
  | { type: 'reset' }
  | { type: 'GameCreated'; payload: GameCreatedEvent }
  | { type: 'PlayerSeated'; payload: PlayerSeatedEvent }
  | { type: 'GameStarted'; payload: GameStartedEvent }
  | { type: 'DiceRolled'; payload: DiceRolledEvent }
  | { type: 'BreakPointSet'; payload: BreakPointSetEvent }
  | { type: 'TilesDealt'; payload: TilesDealtEvent }
  | { type: 'TurnStarted'; payload: TurnStartedEvent }
  | { type: 'TileDrawn'; payload: TileDrawnEvent }
  | { type: 'TileDiscarded'; payload: TileDiscardedEvent }
  | { type: 'ClaimWindowOpen'; payload: ClaimWindowOpenEvent }
  | { type: 'ClaimMade'; payload: ClaimMadeEvent }
  | { type: 'KongReplacementDrawn'; payload: KongReplacementDrawnEvent }
  | { type: 'WinDeclared'; payload: WinDeclaredEvent }
  | { type: 'ScoringComplete'; payload: ScoringCompleteEvent }
  | { type: 'BankerRotated'; payload: BankerRotatedEvent }
  | { type: 'RoundChanged'; payload: RoundChangedEvent }
  | { type: 'HandFinished'; payload: HandFinishedEvent }
  | { type: 'GameEnded'; payload: GameEndedEvent };

const SEATS: SeatIndex[] = [0, 1, 2, 3];
const SEAT_WINDS: Wind[] = ['east', 'south', 'west', 'north'];

export function initialChangshaState(): ChangshaGameState {
  return {
    gameId: '',
    bankerSeat: 0,
    prevalentWind: 'east',
    currentRound: 1,
    currentHand: 1,
    seats: SEATS.map((i) => ({
      index: i,
      nick: `Seat ${i}`,
      isBot: false,
      seatWind: SEAT_WINDS[i],
      score: 0,
    })),
    phase: 'lobby',
    hands: SEATS.map((i) => ({ seatIndex: i, concealed: [], melds: [] })),
    wallRemaining: 108,
    discardPile: [],
  };
}

function ensureHand(state: ChangshaGameState, seatIndex: SeatIndex): SeatHand {
  return (
    state.hands.find((h) => h.seatIndex === seatIndex) ?? {
      seatIndex,
      concealed: [],
      melds: [],
    }
  );
}

function setHand(
  state: ChangshaGameState,
  seatIndex: SeatIndex,
  mutate: (h: SeatHand) => SeatHand
): SeatHand[] {
  const exists = state.hands.some((h) => h.seatIndex === seatIndex);
  if (!exists) {
    return [...state.hands, mutate(ensureHand(state, seatIndex))];
  }
  return state.hands.map((h) => (h.seatIndex === seatIndex ? mutate(h) : h));
}

function asSeatIndex(n: number): SeatIndex {
  return (n % 4) as SeatIndex;
}

export function changshaReducer(
  state: ChangshaGameState,
  action: GameAction
): ChangshaGameState {
  switch (action.type) {
    case 'reset':
      return initialChangshaState();

    case 'GameCreated': {
      const seats: SeatInfo[] = SEATS.map((i) => {
        const incoming = action.payload.seats.find((s) => s.seatIndex === i);
        return {
          index: i,
          nick: incoming?.playerId || `Seat ${i}`,
          isBot: incoming?.isBot ?? false,
          seatWind: (incoming?.wind ?? SEAT_WINDS[i]) as Wind,
          score: 0,
        };
      });
      return {
        ...initialChangshaState(),
        gameId: action.payload.gameId,
        seats,
      };
    }

    case 'PlayerSeated': {
      const { seatIndex, playerId, isBot } = action.payload;
      return {
        ...state,
        seats: state.seats.map((s) =>
          s.index === seatIndex ? { ...s, nick: playerId, isBot } : s
        ),
      };
    }

    case 'GameStarted': {
      return {
        ...state,
        bankerSeat: asSeatIndex(action.payload.dealerSeatIndex),
        prevalentWind: action.payload.roundWind,
        currentHand: action.payload.handNumber,
        phase: 'rollingDice',
      };
    }

    case 'DiceRolled': {
      return {
        ...state,
        phase: 'rollingDice',
        lastDice: action.payload.dice,
      };
    }

    case 'BreakPointSet': {
      return {
        ...state,
        breakPoint: action.payload.breakPoint,
        phase: 'dealing',
      };
    }

    case 'TilesDealt': {
      const { seatIndex, tileIds, tileCount, isComplete } = action.payload;
      const si = asSeatIndex(seatIndex);
      const dealtTiles = tileIds.map(tileFromId);
      const hands = setHand(state, si, (h) => ({
        ...h,
        // Replace concealed list when this batch contains real tile ids
        // (we are the receiving seat); else preserve and only track count.
        concealed: tileIds.length ? [...h.concealed, ...dealtTiles] : h.concealed,
      }));
      // Estimate wall remaining: subtract dealt tile count from initial 108
      // as an aggregate (server's TileDrawn/etc give authoritative numbers).
      const totalConcealed = hands.reduce(
        (sum, h) =>
          sum +
          (h.seatIndex === si && tileIds.length === 0
            ? tileCount
            : h.concealed.length),
        0
      );
      return {
        ...state,
        hands,
        wallRemaining: Math.max(0, 108 - totalConcealed),
        phase: isComplete ? 'awaitingDiscard' : 'dealing',
      };
    }

    case 'TurnStarted': {
      return {
        ...state,
        activeSeat: asSeatIndex(action.payload.seatIndex),
        wallRemaining: action.payload.wallRemaining,
        phase: action.payload.phase as GamePhase,
      };
    }

    case 'TileDrawn': {
      const { seatIndex, tileId, wallRemaining } = action.payload;
      if (typeof tileId !== 'number') {
        return { ...state, wallRemaining };
      }
      const si = asSeatIndex(seatIndex);
      const tile = tileFromId(tileId);
      return {
        ...state,
        wallRemaining,
        hands: setHand(state, si, (h) => ({
          ...h,
          concealed: [...h.concealed, tile],
        })),
      };
    }

    case 'TileDiscarded': {
      const { seatIndex, tileId } = action.payload;
      const si = asSeatIndex(seatIndex);
      const tile = tileFromId(tileId);
      return {
        ...state,
        hands: setHand(state, si, (h) => ({
          ...h,
          concealed: h.concealed.filter((t) => t.id !== tileId),
        })),
        discardPile: [...state.discardPile, tile],
        phase: 'awaitingClaim',
      };
    }

    case 'ClaimWindowOpen': {
      return {
        ...state,
        phase: 'awaitingClaim',
        pendingClaims: action.payload.opportunities.map((o) => ({
          seatIndex: asSeatIndex(o.seatIndex),
          type: o.claimType,
        })),
      };
    }

    case 'ClaimMade': {
      const { claimingSeatIndex, meld } = action.payload;
      const si = asSeatIndex(claimingSeatIndex);
      const meldState: MeldState = {
        type: meld.type,
        tileIds: meld.tileIds,
        claimedFrom: meld.claimedFrom,
      };
      return {
        ...state,
        pendingClaims: undefined,
        hands: setHand(state, si, (h) => ({
          ...h,
          concealed: h.concealed.filter((t) => !meld.tileIds.includes(t.id)),
          melds: [...h.melds, meldState],
        })),
        activeSeat: si,
        phase: 'awaitingDiscard',
      };
    }

    case 'KongReplacementDrawn': {
      const { seatIndex, tileId, wallRemaining } = action.payload;
      if (typeof tileId !== 'number') {
        return { ...state, wallRemaining };
      }
      const si = asSeatIndex(seatIndex);
      const tile = tileFromId(tileId);
      return {
        ...state,
        wallRemaining,
        hands: setHand(state, si, (h) => ({
          ...h,
          concealed: [...h.concealed, tile],
        })),
      };
    }

    case 'WinDeclared': {
      return {
        ...state,
        phase: 'scoring',
        lastWin: action.payload.winResult,
      };
    }

    case 'ScoringComplete': {
      const { handSummary, gameSummary } = action.payload;
      const seats = state.seats.map((s) => ({
        ...s,
        score: gameSummary.scores[s.index] ?? s.score,
      }));
      return {
        ...state,
        seats,
        lastScore: handSummary.scoreResult,
        phase: 'endHand',
      };
    }

    case 'BankerRotated': {
      return {
        ...state,
        bankerSeat: asSeatIndex(action.payload.newDealerSeatIndex),
        phase: 'rotatingBanker',
      };
    }

    case 'RoundChanged': {
      return {
        ...state,
        prevalentWind: action.payload.newRoundWind,
        currentRound: action.payload.roundNumber,
      };
    }

    case 'HandFinished': {
      return {
        ...state,
        currentHand: action.payload.nextHandNumber,
        bankerSeat: asSeatIndex(action.payload.nextDealerSeatIndex),
        prevalentWind: action.payload.nextRoundWind,
        // clear per-hand state for next deal
        hands: SEATS.map((i) => ({ seatIndex: i, concealed: [], melds: [] })),
        discardPile: [],
        pendingClaims: undefined,
        lastWin: undefined,
        lastScore: undefined,
        breakPoint: undefined,
        lastDice: undefined,
        phase: action.payload.isGameOver ? 'endGame' : 'rollingDice',
      };
    }

    case 'GameEnded': {
      return {
        ...state,
        phase: 'endGame',
        seats: state.seats.map((s) => ({
          ...s,
          score: action.payload.finalScores[s.index] ?? s.score,
        })),
      };
    }

    default:
      return state;
  }
}
