/**
 * Live Changsha hook — opens a SignalR connection, reduces server events
 * into ChangshaGameState, and exposes action callbacks that invoke hub
 * commands.
 *
 * Pair with useChangshaMockGame for offline/dev work; useChangshaGame
 * (in ./useChangshaGame.ts) selects between the two based on env + a
 * localStorage override.
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

export interface UseLiveChangshaOptions {
  hubUrl?: string;
  /** Auto-create a game on first connect using this seed (dev convenience). */
  autoCreateOnConnect?: boolean;
  /** Local seat index this client controls. */
  userSeat?: SeatIndex;
}

export interface ChangshaActions {
  rollDice: () => void;
  confirmDice: () => void;
  dealMock: () => void;
  discard: (tileId: number) => void;
  simulateClaimWindow: () => void;
  resolveClaim: (claimType: string | null) => void;
  simulateWin: () => void;
  continueAfterScoring: () => void;
  resetDemo: () => void;
  // Live-only commands (ignored by mock; safe to call regardless)
  declareKong: (tileIds: number[]) => void;
  declareWin: () => void;
  pass: () => void;
}

export interface UseChangshaGameResult {
  state: ReturnType<typeof initialChangshaState>;
  actions: ChangshaActions;
  connectionStatus: ConnectionStatus;
  lastError?: { code?: string; message: string };
  isLive: true;
  reconnect: () => void;
}

export function useLiveChangshaGame(
  opts: UseLiveChangshaOptions = {}
): UseChangshaGameResult {
  const userSeat: SeatIndex = opts.userSeat ?? 0;
  const [state, dispatch] = useReducer(changshaReducer, undefined, initialChangshaState);
  const [connectionStatus, setConnectionStatus] = useState<ConnectionStatus>('idle');
  const [lastError, setLastError] = useState<{ code?: string; message: string } | undefined>();
  const connRef = useRef<HubConnection | null>(null);
  const gameIdRef = useRef<string>('');
  const [reconnectNonce, setReconnectNonce] = useState(0);

  // Track gameId from state for invokes
  useEffect(() => {
    if (state.gameId) gameIdRef.current = state.gameId;
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
    };

    const conn = createChangshaConnection({
      hubUrl: opts.hubUrl,
      seatIndex: userSeat,
    });
    connRef.current = conn;

    const detach = attachServerEventHandlers(conn, handlers);

    conn.onreconnecting(() => setConnectionStatus('reconnecting'));
    conn.onreconnected(() => {
      setConnectionStatus('connected');
      // Rehydrate via JoinTable replay if we know our game id.
      if (gameIdRef.current) {
        invoke
          .reconnectGame(conn, { gameId: gameIdRef.current })
          .catch((err) =>
            setLastError({ message: `Reconnect rehydrate failed: ${String(err?.message ?? err)}` })
          );
      }
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
  }, [opts.hubUrl, userSeat, reconnectNonce]);

  // ── Action helpers ────────────────────────────────────────────
  const guardedInvoke = useCallback(
    async (label: string, fn: () => Promise<unknown>) => {
      try {
        await fn();
      } catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        setLastError({ message: `${label} failed: ${msg}` });
      }
    },
    []
  );

  const rollDice = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) return;
    void guardedInvoke('RollDice', () => invoke.rollDice(c, { gameId: gameIdRef.current }));
  }, [guardedInvoke]);

  const confirmDice = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) return;
    void guardedInvoke('AcknowledgeDeal', () =>
      invoke.acknowledgeDeal(c, { gameId: gameIdRef.current, seatIndex: userSeat })
    );
  }, [guardedInvoke, userSeat]);

  const dealMock = useCallback(() => {
    // Live server deals automatically after dice ack; this is a no-op placeholder.
  }, []);

  const discard = useCallback(
    (tileId: number) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected) return;
      void guardedInvoke('Discard', () =>
        invoke.discard(c, { gameId: gameIdRef.current, seatIndex: userSeat, tileId })
      );
    },
    [guardedInvoke, userSeat]
  );

  const resolveClaim = useCallback(
    (claimType: string | null) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected) return;
      if (!claimType) {
        void guardedInvoke('Pass', () =>
          invoke.pass(c, { gameId: gameIdRef.current, seatIndex: userSeat })
        );
        return;
      }
      void guardedInvoke('Claim', () =>
        invoke.claim(c, {
          gameId: gameIdRef.current,
          seatIndex: userSeat,
          type: claimType as ClaimType,
        })
      );
    },
    [guardedInvoke, userSeat]
  );

  const declareKong = useCallback(
    (tileIds: number[]) => {
      const c = connRef.current;
      if (!c || c.state !== HubConnectionState.Connected) return;
      void guardedInvoke('DeclareKong', () =>
        invoke.declareKong(c, {
          gameId: gameIdRef.current,
          seatIndex: userSeat,
          tileIds,
        })
      );
    },
    [guardedInvoke, userSeat]
  );

  const declareWin = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) return;
    void guardedInvoke('DeclareWin', () =>
      invoke.declareWin(c, { gameId: gameIdRef.current, seatIndex: userSeat })
    );
  }, [guardedInvoke, userSeat]);

  const passClaim = useCallback(() => {
    const c = connRef.current;
    if (!c || c.state !== HubConnectionState.Connected) return;
    void guardedInvoke('Pass', () =>
      invoke.pass(c, { gameId: gameIdRef.current, seatIndex: userSeat })
    );
  }, [guardedInvoke, userSeat]);

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
    },
    connectionStatus,
    lastError,
    isLive: true,
    reconnect,
  };
}
