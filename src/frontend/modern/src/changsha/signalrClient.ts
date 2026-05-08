/**
 * Strongly-typed SignalR client for the Changsha hub.
 * Mirrors docs/rules/changsha-signalr-contract.md verbatim.
 */
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import type {
  ClaimOpportunity,
  ClaimType,
  DiceResult,
  GamePhase,
  GameSummary,
  HandSummary,
  MeldState,
  SeatState,
  Wind,
  WinResult,
  BreakPoint,
} from './types';

// ── Server → Client event payloads ─────────────────────────────────

export interface GameCreatedEvent {
  gameId: string;
  ruleSet: 'changsha-v1';
  seats: SeatState[];
}
export interface PlayerSeatedEvent {
  gameId: string;
  seatIndex: number;
  playerId: string;
  isBot: boolean;
}
export interface GameStartedEvent {
  gameId: string;
  dealerSeatIndex: number;
  roundWind: Wind;
  handNumber: number;
}
export interface DiceRolledEvent {
  gameId: string;
  rollerSeatIndex: number;
  dice: DiceResult;
}
export interface BreakPointSetEvent {
  gameId: string;
  breakPoint: BreakPoint;
}
export interface TilesDealtEvent {
  gameId: string;
  seatIndex: number;
  tileIds: number[];
  tileCount: number;
  batchNumber: number;
  isComplete: boolean;
}
export interface TurnStartedEvent {
  gameId: string;
  seatIndex: number;
  turnNumber: number;
  wallRemaining: number;
  phase: GamePhase;
}
export interface TileDrawnEvent {
  gameId: string;
  seatIndex: number;
  tileId?: number;
  wallRemaining: number;
  isReplacementDraw: boolean;
}
export interface TileDiscardedEvent {
  gameId: string;
  seatIndex: number;
  tileId: number;
  turnNumber: number;
}
export interface ClaimWindowOpenEvent {
  gameId: string;
  discardSeatIndex: number;
  discardTileId: number;
  opportunities: ClaimOpportunity[];
  timeoutMs: number;
}
export interface ClaimMadeEvent {
  gameId: string;
  claimingSeatIndex: number;
  claimType: ClaimType;
  tileId: number;
  meld: MeldState;
}
export interface KongReplacementDrawnEvent {
  gameId: string;
  seatIndex: number;
  tileId?: number;
  wallRemaining: number;
}
export interface WinDeclaredEvent {
  gameId: string;
  winResult: WinResult;
  hand: { concealedTiles: number[]; melds: MeldState[] };
}
export interface ScoringCompleteEvent {
  gameId: string;
  handSummary: HandSummary;
  gameSummary: GameSummary;
}
export interface BankerRotatedEvent {
  gameId: string;
  previousDealerSeatIndex: number;
  newDealerSeatIndex: number;
  reason: 'winnerBecomesDealer' | 'drawRotation' | 'dealerRetained';
}
export interface RoundChangedEvent {
  gameId: string;
  previousRoundWind: Wind;
  newRoundWind: Wind;
  roundNumber: number;
}
export interface HandFinishedEvent {
  gameId: string;
  handNumber: number;
  handSummary: HandSummary;
  nextHandNumber: number;
  nextDealerSeatIndex: number;
  nextRoundWind: Wind;
  isGameOver: boolean;
}
export interface GameEndedEvent {
  gameId: string;
  gameSummary: GameSummary;
  finalScores: Record<number, number>;
  winner: { seatIndex: number; score: number };
}

export interface ServerEventHandlers {
  GameCreated?: (p: GameCreatedEvent) => void;
  PlayerSeated?: (p: PlayerSeatedEvent) => void;
  GameStarted?: (p: GameStartedEvent) => void;
  DiceRolled?: (p: DiceRolledEvent) => void;
  BreakPointSet?: (p: BreakPointSetEvent) => void;
  TilesDealt?: (p: TilesDealtEvent) => void;
  TurnStarted?: (p: TurnStartedEvent) => void;
  TileDrawn?: (p: TileDrawnEvent) => void;
  TileDiscarded?: (p: TileDiscardedEvent) => void;
  ClaimWindowOpen?: (p: ClaimWindowOpenEvent) => void;
  ClaimMade?: (p: ClaimMadeEvent) => void;
  KongReplacementDrawn?: (p: KongReplacementDrawnEvent) => void;
  WinDeclared?: (p: WinDeclaredEvent) => void;
  ScoringComplete?: (p: ScoringCompleteEvent) => void;
  BankerRotated?: (p: BankerRotatedEvent) => void;
  RoundChanged?: (p: RoundChangedEvent) => void;
  HandFinished?: (p: HandFinishedEvent) => void;
  GameEnded?: (p: GameEndedEvent) => void;
}

const SERVER_EVENT_NAMES: (keyof ServerEventHandlers)[] = [
  'GameCreated',
  'PlayerSeated',
  'GameStarted',
  'DiceRolled',
  'BreakPointSet',
  'TilesDealt',
  'TurnStarted',
  'TileDrawn',
  'TileDiscarded',
  'ClaimWindowOpen',
  'ClaimMade',
  'KongReplacementDrawn',
  'WinDeclared',
  'ScoringComplete',
  'BankerRotated',
  'RoundChanged',
  'HandFinished',
  'GameEnded',
];

// ── Client → Server command payloads ──────────────────────────────

export interface CreateGamePayload {
  ruleSet: 'changsha-v1';
  botSeatIndexes?: number[];
  seed?: number;
}
export interface JoinTablePayload {
  gameId: string;
}
export interface TakeSeatPayload {
  gameId: string;
  seatIndex: number;
}
export interface StartGamePayload {
  gameId: string;
}
export interface RollDicePayload {
  gameId: string;
}
export interface AcknowledgeDealPayload {
  gameId: string;
  seatIndex: number;
}
export interface DiscardPayload {
  gameId: string;
  seatIndex: number;
  tileId: number;
}
export interface ClaimPayload {
  gameId: string;
  seatIndex: number;
  type: ClaimType;
  tileIds?: number[];
}
export interface DeclareKongPayload {
  gameId: string;
  seatIndex: number;
  tileIds: number[];
}
export interface DeclareWinPayload {
  gameId: string;
  seatIndex: number;
}
export interface PassPayload {
  gameId: string;
  seatIndex: number;
}

// ── Connection factory ────────────────────────────────────────────

export interface CreateConnectionOptions {
  hubUrl?: string;
  gameId?: string;
  seatIndex?: number;
  logLevel?: LogLevel;
}

export function createChangshaConnection(opts: CreateConnectionOptions = {}): HubConnection {
  const base = opts.hubUrl ?? '/hubs/changsha';
  const params: string[] = [];
  if (opts.gameId) params.push(`gameId=${encodeURIComponent(opts.gameId)}`);
  if (typeof opts.seatIndex === 'number') params.push(`seatIndex=${opts.seatIndex}`);
  const url = params.length ? `${base}?${params.join('&')}` : base;

  return new HubConnectionBuilder()
    .withUrl(url)
    .withAutomaticReconnect()
    .configureLogging(opts.logLevel ?? LogLevel.Warning)
    .build();
}

/**
 * Wires a handler-map onto a HubConnection. Returns a teardown fn that
 * removes every listener registered.
 */
export function attachServerEventHandlers(
  conn: HubConnection,
  handlers: ServerEventHandlers
): () => void {
  const wrapped: Array<{ name: string; fn: (...args: unknown[]) => void }> = [];
  for (const name of SERVER_EVENT_NAMES) {
    const h = handlers[name];
    if (!h) continue;
    const fn = (payload: unknown) => {
      try {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        (h as any)(payload);
      } catch (err) {
        console.error(`[changsha hub] handler for ${name} threw`, err);
      }
    };
    conn.on(name, fn);
    wrapped.push({ name, fn });
  }
  return () => {
    for (const { name, fn } of wrapped) conn.off(name, fn);
  };
}

// ── Strongly-typed invoke wrappers ────────────────────────────────

export const invoke = {
  createGame: (c: HubConnection, p: CreateGamePayload): Promise<{ gameId: string }> =>
    c.invoke('CreateGame', p),
  joinTable: (c: HubConnection, p: JoinTablePayload): Promise<{ success: boolean }> =>
    c.invoke('JoinTable', p),
  takeSeat: (
    c: HubConnection,
    p: TakeSeatPayload
  ): Promise<{ success: boolean; seatIndex: number }> => c.invoke('TakeSeat', p),
  startGame: (c: HubConnection, p: StartGamePayload): Promise<{ success: boolean }> =>
    c.invoke('StartGame', p),
  rollDice: (c: HubConnection, p: RollDicePayload): Promise<{ dice: DiceResult }> =>
    c.invoke('RollDice', p),
  acknowledgeDeal: (c: HubConnection, p: AcknowledgeDealPayload): Promise<void> =>
    c.invoke('AcknowledgeDeal', p),
  discard: (c: HubConnection, p: DiscardPayload): Promise<void> => c.invoke('Discard', p),
  claim: (c: HubConnection, p: ClaimPayload): Promise<void> => c.invoke('Claim', p),
  declareKong: (c: HubConnection, p: DeclareKongPayload): Promise<void> =>
    c.invoke('DeclareKong', p),
  declareWin: (c: HubConnection, p: DeclareWinPayload): Promise<void> =>
    c.invoke('DeclareWin', p),
  pass: (c: HubConnection, p: PassPayload): Promise<void> => c.invoke('Pass', p),
  /**
   * Reconnect after a transport interruption. The contract specifies that
   * clients re-send JoinTable to trigger event-log replay; we expose this
   * as a separate method name for clarity at the hook layer.
   */
  reconnectGame: (c: HubConnection, p: JoinTablePayload): Promise<{ success: boolean }> =>
    c.invoke('JoinTable', p),
};

export type ConnectionStatus =
  | 'idle'
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected'
  | 'failed';

export function describeConnectionState(state: HubConnectionState): ConnectionStatus {
  switch (state) {
    case HubConnectionState.Connecting:
      return 'connecting';
    case HubConnectionState.Connected:
      return 'connected';
    case HubConnectionState.Reconnecting:
      return 'reconnecting';
    case HubConnectionState.Disconnecting:
    case HubConnectionState.Disconnected:
      return 'disconnected';
    default:
      return 'idle';
  }
}
