/**
 * Live Changsha hook — opens a SignalR connection, reduces server events
 * into ChangshaGameState, and exposes action callbacks that invoke hub
 * commands.
 *
 * Pair with useChangshaMockGame for offline/dev work; useChangshaGame
 * (in ./useChangshaGame.ts) selects between the two based on env + a
 * localStorage override.
 *
 * Phase 3 additions:
 *   - Lobby actions: createGame, fillWithBots, takeSeat, startGame,
 *     reconnectGame, leaveGame.
 *   - localStorage persistence for game id / seat / player name so a
 *     refresh mid-hand reconnects automatically via ReconnectGame.
 */
import { useCallback, useEffect, useReducer, useRef, useState } from 'react';
import type { HubConnection } from '@microsoft/signalr';
import { HubConnectionState } from '@microsoft/signalr';
import {
  attachServerEventHandlers,
  createChangshaConnection,
  describeConnectionState,
  invoke,
  type ConnectionStatus,
  type ServerEventHandlers,
} from './signalrClient';
import {
  changshaReducer,
  initialChangshaState,
} from './changshaReducer';
import type { ClaimType, SeatIndex } from './types';

export const LS_KEYS = {
  gameId: 'mj-autotable:changsha:gameId',
  seatIndex: 'mj-autotable:changsha:seatIndex',
  playerName: 'mj-autotable:changsha:playerName',
} as const;

function readLS(key: string): string | null {
  try {
    return typeof window !== 'undefined' ? window.localStorage.getItem(key) : null;
  } catch {
    return null;
  }
}
function writeLS(key: string, value: string | null) {
  try {
    if (typeof window === 'undefined') return;
    if (value === null) window.localStorage.removeItem(key);
    else window.localStorage.setItem(key, value);
  } catch {
    // ignore — private browsing or quota
  }
}

export interface UseLiveChangshaOptions {
  hubUrl?: string;
  /** Local seat index this client controls (default 0). */
  userSeat?: SeatIndex;
}

export interface ChangshaLiveActions {
  rollDice: () => void;
  confirmDice: () => void;
  dealMock: () => void;
  discard: (tileId: number) => void;
  simulateClaimWindow: () => void;
  resolveClaim: (claimType: string | null, tileIds?: number[]) => void;
  simulateWin: () => void;
  continueAfterScoring: () => void;
  resetDemo: () => void;
  declareKong: (tileIds: number[]) => void;
  declareWin: () => void;
  pass: () => void;
  // Lobby orchestration (Phase 3)
  createGame: (opts?: {
    seed?: number;
    botSeatIndexes?: number[];
  }) => Promise<string | null>;
  fillWithBots: () => Promise<void>;
  takeSeat: (seatIndex: SeatIndex, playerName?: string) => Promise<void>;
  startGame: () => Promise<void>;
  reconnectGame: (gameId: string, seatIndex: SeatIndex) => Promise<boolean>;
  leaveGame: () => void;
}

export interface UseLiveChangshaResult {
  state: ReturnType<typeof initialChangshaState>;
  actions: ChangshaLiveActions;
  connectionStatus: ConnectionStatus;
  lastError?: { code?: string; message: string };
  isLive: true;
  reconnect: () => void;
}

export function useLiveChangshaGame(
  opts: UseLiveChangshaOptions = {}
): UseLiveChangshaResult {
  const initialSeat: SeatIndex = (() => {
    const ls = readLS(LS_KEYS.seatIndex);
    if (ls !== null) {
      const n = Number.parseInt(ls, 10);
      if (n >= 0 && n <= 3) return n as SeatIndex;
    }
    return opts.userSeat ?? 0;
  })();
  const [userSeat, setUserSeat] = useState<SeatIndex>(initialSeat);
  const userSeatRef = useRef<SeatIndex>(initialSeat);
  useEffect(() => {
    userSeatRef.current = userSeat;
  }, [userSeat]);

  const [state, dispatch] = useReducer(changshaReducer, undefined, initialChangshaState);
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('idle');
  const [lastError, setLastError] = useState<{ code?: string; message: string } | undefined>();
  const connRef = useRef<HubConnection | null>(null);
  const gameIdRef = useRef<string>('');
  const [reconnectNonce, setReconnectNonce] = useState(0);

  useEffect(() => {
    if (state.gameId) {
      gameIdRef.current = state.gameId;
      writeLS(LS_KEYS.gameId, state.gameId);
    }
  }, [state.gameId]);

  // Open & manage the connection lifecycle
  useEffect(() => {
    let cancelled = false;
    const handlers: ServerEventHandlers = {
      GameCreated: (p) => dispatch({ type: 'GameCreated', payload: p }),
      PlayerSeated: (p) => dispatch({ type: 'PlayerSeated', payload: p }),
      GameStarted: (p) => dispatch({ type: 'GameStarted', payload: p }),
      DiceRolled: (p) => dispatch({ type: 'DiceRolled', payload: p }),
      BreakPointSet: (p) => dispatch({ type: 'BreakPointSet', payload: p }),
      TilesDealt: (p) => dispatch({ type: 'TilesDealt', payload: p }),
      TurnStarted: (p) => dispatch({ type: 'TurnStarted', payload: p }),
      TileDrawn: (p) => dispatch({ type: 'TileDrawn', payload: p }),
      TileDiscarded: (p) => dispatch({ type: 'TileDiscarded', payload: p }),
      ClaimWindowOpen: (p) => dispatch({ type: 'ClaimWindowOpen', payload: p }),
      ClaimMade: (p) => dispatch({ type: 'ClaimMade', payload: p }),
      KongReplacementDrawn: (p) =>
        dispatch({ type: 'KongReplacementDrawn', payload: p }),
      WinDeclared: (p) => dispatch({ type: 'WinDeclared', payload: p }),
      ScoringComplete: (p) => dispatch({ type: 'ScoringComplete', payload: p }),
      BankerRotated: (p) => dispatch({ type: 'BankerRotated', payload: p }),
      RoundChanged: (p) => dispatch({ type: 'RoundChanged', payload: p }),
      HandFinished: (p) => dispatch({ type: 'HandFinished', payload: p }),
      GameEnded: (p) => dispatch({ type: 'GameEnded', payload: p }),
      FullState: (p) => dispatch({ type: 'FullState', payload: p }),
    };

    const conn = createChangshaConnection({
      hubUrl: opts.hubUrl,
    });
    connRef.current = conn;

    const detach = attachServerEventHandlers(conn, handlers);

    const tryRehydrate = (c: HubConnection) => {
      const persistedGameId = readLS(LS_KEYS.gameId);
      const persistedSeatRaw = readLS(LS_KEYS.seatIndex);
      if (!persistedGameId) return;
      const persistedSeat = persistedSeatRaw !== null ? Number.parseInt(persistedSeatRaw, 10) : NaN;
      const seatToUse: SeatIndex =
        Number.isFinite(persistedSeat) && persistedSeat >= 0 && persistedSeat <= 3
          ? (persistedSeat as SeatIndex)
          : userSeatRef.current;
      invoke
        .reconnectGame(c, { gameId: persistedGameId, seatIndex: seatToUse })
        .catch((err) => {
          // Game probably no longer exists; clear stale persisted state.
          if (typeof err?.message === 'string' && /not found|notFound|gameNotFound/i.test(err.message)) {
            writeLS(LS_KEYS.gameId, null);
          }
          setLastError({ message: `Reconnect rehydrate failed: ${String(err?.message ?? err)}` });
        });
    };

    conn.onreconnecting(() => setConnectionStatus('reconnecting'));
    conn.onreconnected(() => {
      setConnectionStatus('connected');
      tryRehydrate(conn);
    });
    conn.onclose((err) => {
      setConnectionStatus('disconnected');
      if (err) setLastError({ message: String(err.message ?? err) });
    });

    setConnectionStatus('connecting');
    conn
      .start()
      .then(() => {
        if (cancelled) return;
        setConnectionStatus(describeConnectionState(conn.state));
        tryRehydrate(conn);
      })
      .catch((err) => {
        if (cancelled) return;
        setConnectionStatus('failed');
        setLastError({ message: `Hub connect failed: ${String(err?.message ?? err)}` });
      });

    return () => {
      cancelled = true;
      detach();
      void conn.stop();
      connRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [opts.hubUrl, reconnectNonce]);

  // ── Action helpers ────────────────────────────────────────────
  const guardedInvoke = useCallback(
    async <T>(label: string, fn: () => Promise<T>): Promise<T | null> => {
      try {
        return await fn();
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        setLastError({ message: `${label} failed: ${msg}` });
        return null;
      }
    },
    []
  );

  const requireConnected = useCallback((label: string): HubConnection | null => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) {
      setLastError({ message: `${label}: hub not connected` });
      return null;
    }
    return c;
  }, []);

  const rollDice = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) return;
    void guardedInvoke('RollDice', () => invoke.rollDice(c, { gameId: gameIdRef.current }));
  }, [guardedInvoke]);

  const confirmDice = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) return;
    void guardedInvoke('AcknowledgeDeal', () =>
      invoke.acknowledgeDeal(c, { gameId: gameIdRef.current, seatIndex: userSeatRef.current })
    );
  }, [guardedInvoke]);

  const dealMock = useCallback(() => {
    // Live server deals automatically after StartGame; this is a no-op placeholder.
  }, []);

  const discard = useCallback(
    (tileId: number) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected) return;
      void guardedInvoke('Discard', () =>
        invoke.discard(c, {
          gameId: gameIdRef.current,
          seatIndex: userSeatRef.current,
          tileId,
        })
      );
    },
    [guardedInvoke]
  );

  const resolveClaim = useCallback(
    (claimType: string | null, tileIds?: number[]) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected) return;
      if (!claimType) {
        void guardedInvoke('Pass', () =>
          invoke.pass(c, { gameId: gameIdRef.current, seatIndex: userSeatRef.current })
        );
        return;
      }
      void guardedInvoke('Claim', () =>
        invoke.claim(c, {
          gameId: gameIdRef.current,
          seatIndex: userSeatRef.current,
          type: claimType as ClaimType,
          tileIds,
        })
      );
    },
    [guardedInvoke]
  );

  const declareKong = useCallback(
    (tileIds: number[]) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected) return;
      void guardedInvoke('DeclareKong', () =>
        invoke.declareKong(c, {
          gameId: gameIdRef.current,
          seatIndex: userSeatRef.current,
          tileIds,
        })
      );
    },
    [guardedInvoke]
  );

  const declareWin = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) return;
    void guardedInvoke('DeclareWin', () =>
      invoke.declareWin(c, { gameId: gameIdRef.current, seatIndex: userSeatRef.current })
    );
  }, [guardedInvoke]);

  const passClaim = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) return;
    void guardedInvoke('Pass', () =>
      invoke.pass(c, { gameId: gameIdRef.current, seatIndex: userSeatRef.current })
    );
  }, [guardedInvoke]);

  // ── Lobby orchestration (Phase 3) ─────────────────────────────
  const createGame = useCallback(
    async (createOpts?: { seed?: number; botSeatIndexes?: number[] }): Promise<string | null> => {
      const c = requireConnected('CreateGame');
      if (!c) return null;
      const result = await guardedInvoke('CreateGame', () =>
        invoke.createGame(c, {
          ruleSet: 'changsha-v1',
          botSeatIndexes: createOpts?.botSeatIndexes,
          seed: createOpts?.seed,
        })
      );
      const gameId = result?.gameId;
      if (gameId) {
        gameIdRef.current = gameId;
        writeLS(LS_KEYS.gameId, gameId);
      }
      return gameId ?? null;
    },
    [guardedInvoke, requireConnected]
  );

  const fillWithBots = useCallback(async () => {
    const c = requireConnected('FillWithBots');
    if (!c) return;
    await guardedInvoke('FillWithBots', () =>
      invoke.fillWithBots(c, { gameId: gameIdRef.current })
    );
  }, [guardedInvoke, requireConnected]);

  const takeSeat = useCallback(
    async (seatIndex: SeatIndex, playerName?: string) => {
      const c = requireConnected('TakeSeat');
      if (!c) return;
      const result = await guardedInvoke('TakeSeat', () =>
        invoke.takeSeat(c, {
          gameId: gameIdRef.current,
          seatIndex,
          playerName,
        })
      );
      const assigned = (result?.seatIndex ?? seatIndex) as SeatIndex;
      setUserSeat(assigned);
      userSeatRef.current = assigned;
      writeLS(LS_KEYS.seatIndex, String(assigned));
      if (playerName) writeLS(LS_KEYS.playerName, playerName);
    },
    [guardedInvoke, requireConnected]
  );

  const startGame = useCallback(async () => {
    const c = requireConnected('StartGame');
    if (!c) return;
    await guardedInvoke('StartGame', () =>
      invoke.startGame(c, { gameId: gameIdRef.current })
    );
  }, [guardedInvoke, requireConnected]);

  const reconnectGame = useCallback(
    async (gameId: string, seatIndex: SeatIndex): Promise<boolean> => {
      const c = requireConnected('ReconnectGame');
      if (!c) return false;
      const result = await guardedInvoke('ReconnectGame', () =>
        invoke.reconnectGame(c, { gameId, seatIndex })
      );
      if (result?.success) {
        gameIdRef.current = gameId;
        writeLS(LS_KEYS.gameId, gameId);
        setUserSeat(seatIndex);
        userSeatRef.current = seatIndex;
        writeLS(LS_KEYS.seatIndex, String(seatIndex));
        return true;
      }
      return false;
    },
    [guardedInvoke, requireConnected]
  );

  const leaveGame = useCallback(() => {
    writeLS(LS_KEYS.gameId, null);
    writeLS(LS_KEYS.seatIndex, null);
    gameIdRef.current = '';
    dispatch({ type: 'reset' });
  }, []);

  const simulateClaimWindow = useCallback(() => {
    // No-op in live mode — server controls claim windows.
  }, []);

  const simulateWin = useCallback(() => {
    declareWin();
  }, [declareWin]);

  const continueAfterScoring = useCallback(() => {
    // Server emits HandFinished automatically after scoring; nothing to do.
  }, []);

  const resetDemo = useCallback(() => {
    dispatch({ type: 'reset' });
  }, []);

  const reconnect = useCallback(() => {
    setLastError(undefined);
    setConnectionStatus('connecting');
    setReconnectNonce((n) => n + 1);
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
      declareKong,
      declareWin,
      pass: passClaim,
      createGame,
      fillWithBots,
      takeSeat,
      startGame,
      reconnectGame,
      leaveGame,
    },
    connectionStatus,
    lastError,
    isLive: true,
    reconnect,
  };
}
