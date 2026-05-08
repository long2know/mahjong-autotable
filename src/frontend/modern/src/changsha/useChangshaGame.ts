/**
 * Unified Changsha hook entry point.
 * Selects between the live SignalR hook and the offline mock based on:
 *   1. localStorage('changsha.useMock') override ('1'/'0')
 *   2. import.meta.env.DEV  (default: mock in dev, live in prod)
 *
 * The choice is fixed at mount; callers should call setUseMockOverride()
 * and reload the page to switch modes (this avoids dangling SignalR
 * connections and keeps hook order stable).
 */
import { useState } from 'react';
import { useChangshaMockGame } from './useChangshaMockGame';
import { useLiveChangshaGame } from './useLiveChangshaGame';
import type { ChangshaGameState, SeatIndex } from './types';
import type { ConnectionStatus } from './signalrClient';

const STORAGE_KEY = 'changsha.useMock';

export function shouldUseMock(): boolean {
  try {
    const v = localStorage.getItem(STORAGE_KEY);
    if (v === '1') return true;
    if (v === '0') return false;
  } catch {
    /* localStorage unavailable */
  }
  return Boolean(import.meta.env.DEV);
}

export function setUseMockOverride(useMock: boolean | null): void {
  try {
    if (useMock === null) localStorage.removeItem(STORAGE_KEY);
    else localStorage.setItem(STORAGE_KEY, useMock ? '1' : '0');
  } catch {
    /* ignore */
  }
}

export interface UseChangshaGameResult {
  state: ChangshaGameState;
  actions: {
    rollDice: () => void;
    confirmDice: () => void;
    dealMock: () => void;
    discard: (tileId: number) => void;
    simulateClaimWindow: () => void;
    resolveClaim: (claimType: string | null) => void;
    simulateWin: () => void;
    continueAfterScoring: () => void;
    resetDemo: () => void;
    declareKong?: (tileIds: number[]) => void;
    declareWin?: () => void;
    pass?: () => void;
  };
  isLive: boolean;
  connectionStatus: ConnectionStatus;
  lastError?: { code?: string; message: string };
  reconnect: () => void;
}

export interface UseChangshaGameOptions {
  userSeat?: SeatIndex;
  /** Force a specific mode regardless of env/localStorage. */
  forceMock?: boolean;
}

/**
 * Mock-mode hook. Wraps useChangshaMockGame in the unified result shape.
 */
export function useChangshaMockMode(): UseChangshaGameResult {
  const mock = useChangshaMockGame();
  return {
    state: mock.state,
    actions: mock.actions,
    isLive: false,
    connectionStatus: 'idle',
    reconnect: () => undefined,
  };
}

/**
 * Live-mode hook. Wraps useLiveChangshaGame in the unified result shape.
 */
export function useChangshaLiveMode(opts: UseChangshaGameOptions = {}): UseChangshaGameResult {
  const live = useLiveChangshaGame({ userSeat: opts.userSeat });
  return {
    state: live.state,
    actions: live.actions,
    isLive: true,
    connectionStatus: live.connectionStatus,
    lastError: live.lastError,
    reconnect: live.reconnect,
  };
}

/**
 * Default entry point. Picks the implementation once at mount.
 * Components that need to switch modes at runtime should reload the
 * page via setUseMockOverride() + window.location.reload().
 *
 * NOTE: To keep hook order stable, callers MUST NOT call this twice with
 * different forceMock values within the same component instance. The page
 * is expected to render either ChangshaTableLive or ChangshaTableMock —
 * see ChangshaTablePage for the conditional rendering pattern.
 */
export function useChangshaGame(opts: UseChangshaGameOptions = {}): UseChangshaGameResult {
  // Capture the mode decision once at first render.
  const [useMock] = useState(() =>
    typeof opts.forceMock === 'boolean' ? opts.forceMock : shouldUseMock()
  );
  // We use the captured decision to render one of two sub-hook components
  // via the helper hooks. Since the decision is stable for the lifetime of
  // this component, we can branch on it (rules-of-hooks honored: same
  // branch every render).
  // eslint-disable-next-line react-hooks/rules-of-hooks
  return useMock ? useChangshaMockMode() : useChangshaLiveMode(opts);
}

