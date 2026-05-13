/**
 * signalrClient.test.ts
 *
 * Asserts that the `invoke` helpers in signalrClient.ts wrap
 * HubConnection.invoke with the contractually-correct method name and
 * payload object, that attachServerEventHandlers wires conn.on for every
 * supplied handler, and that describeConnectionState maps
 * HubConnectionState to our public ConnectionStatus values.
 *
 * Notes:
 * - The wrappers pass a single payload object to .NET hubs today (the
 *   server contract documents method-name + payload). Hicks's Phase 3
 *   work-in-progress (uncommitted) flips this to positional args plus
 *   `fillWithBots` / `ReconnectGame(gameId, seatIndex)`; tests will need
 *   to follow at his PR time — for now they pin the committed shape.
 * - reconnectGame is intentionally a JoinTable alias in the committed
 *   contract; the server replays state via event-log replay.
 * - The wrapper does not expose a state-change event of its own;
 *   disconnection observation is exercised via describeConnectionState
 *   (which the live hook in useLiveChangshaGame consumes).
 */
import { describe, it, expect, vi } from 'vitest';
import { HubConnectionState } from '@microsoft/signalr';
import type { HubConnection } from '@microsoft/signalr';
import {
  attachServerEventHandlers,
  describeConnectionState,
  invoke,
  type ServerEventHandlers,
} from '../signalrClient';

// ── Fake HubConnection ────────────────────────────────────────────────────

function makeFakeConnection(): {
  conn: HubConnection;
  invokeSpy: ReturnType<typeof vi.fn>;
  onSpy: ReturnType<typeof vi.fn>;
  offSpy: ReturnType<typeof vi.fn>;
  registered: Map<string, Set<(...args: unknown[]) => void>>;
} {
  const registered = new Map<string, Set<(...args: unknown[]) => void>>();
  const onSpy = vi.fn((name: string, fn: (...args: unknown[]) => void) => {
    const set = registered.get(name) ?? new Set();
    set.add(fn);
    registered.set(name, set);
  });
  const offSpy = vi.fn((name: string, fn: (...args: unknown[]) => void) => {
    registered.get(name)?.delete(fn);
  });
  const invokeSpy = vi.fn().mockResolvedValue(undefined);

  const conn = {
    invoke: invokeSpy,
    on: onSpy,
    off: offSpy,
  } as unknown as HubConnection;

  return { conn, invokeSpy, onSpy, offSpy, registered };
}

// ── invoke.* wrappers ─────────────────────────────────────────────────────
// The committed wrappers send a single payload object as the second arg
// to HubConnection.invoke. These tests pin that envelope so any drift to
// positional-args (Hicks Phase 3 WIP) surfaces here at PR time.

describe('signalrClient / invoke wrappers', () => {
  it('createGame calls invoke("CreateGame", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    const payload = {
      ruleSet: 'changsha-v1' as const,
      botSeatIndexes: [1, 2, 3],
      seed: 42,
    };
    await invoke.createGame(conn, payload);
    expect(invokeSpy).toHaveBeenCalledWith('CreateGame', payload);
  });

  it('joinTable calls invoke("JoinTable", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.joinTable(conn, { gameId: 'g-1' });
    expect(invokeSpy).toHaveBeenCalledWith('JoinTable', { gameId: 'g-1' });
  });

  it('takeSeat calls invoke("TakeSeat", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.takeSeat(conn, { gameId: 'g-1', seatIndex: 0 });
    expect(invokeSpy).toHaveBeenCalledWith('TakeSeat', {
      gameId: 'g-1',
      seatIndex: 0,
    });
  });

  it('startGame calls invoke("StartGame", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.startGame(conn, { gameId: 'g-1' });
    expect(invokeSpy).toHaveBeenCalledWith('StartGame', { gameId: 'g-1' });
  });

  it('rollDice calls invoke("RollDice", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.rollDice(conn, { gameId: 'g-1' });
    expect(invokeSpy).toHaveBeenCalledWith('RollDice', { gameId: 'g-1' });
  });

  it('acknowledgeDeal calls invoke("AcknowledgeDeal", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.acknowledgeDeal(conn, { gameId: 'g-1', seatIndex: 0 });
    expect(invokeSpy).toHaveBeenCalledWith('AcknowledgeDeal', {
      gameId: 'g-1',
      seatIndex: 0,
    });
  });

  it('discard calls invoke("Discard", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.discard(conn, { gameId: 'g-1', seatIndex: 0, tileId: 42 });
    expect(invokeSpy).toHaveBeenCalledWith('Discard', {
      gameId: 'g-1',
      seatIndex: 0,
      tileId: 42,
    });
  });

  it('claim calls invoke("Claim", payload) with tileIds for chow', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.claim(conn, {
      gameId: 'g-1',
      seatIndex: 1,
      type: 'chow',
      tileIds: [4, 5, 6],
    });
    expect(invokeSpy).toHaveBeenCalledWith('Claim', {
      gameId: 'g-1',
      seatIndex: 1,
      type: 'chow',
      tileIds: [4, 5, 6],
    });
  });

  it('claim works without tileIds (pung / kong path)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.claim(conn, { gameId: 'g-1', seatIndex: 1, type: 'pung' });
    expect(invokeSpy).toHaveBeenCalledWith('Claim', {
      gameId: 'g-1',
      seatIndex: 1,
      type: 'pung',
    });
  });

  it('pass calls invoke("Pass", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.pass(conn, { gameId: 'g-1', seatIndex: 2 });
    expect(invokeSpy).toHaveBeenCalledWith('Pass', {
      gameId: 'g-1',
      seatIndex: 2,
    });
  });

  it('declareKong calls invoke("DeclareKong", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.declareKong(conn, {
      gameId: 'g-1',
      seatIndex: 0,
      tileIds: [12, 13, 14, 15],
    });
    expect(invokeSpy).toHaveBeenCalledWith('DeclareKong', {
      gameId: 'g-1',
      seatIndex: 0,
      tileIds: [12, 13, 14, 15],
    });
  });

  it('declareWin calls invoke("DeclareWin", payload)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.declareWin(conn, { gameId: 'g-1', seatIndex: 0 });
    expect(invokeSpy).toHaveBeenCalledWith('DeclareWin', {
      gameId: 'g-1',
      seatIndex: 0,
    });
  });

  it('reconnectGame is a JoinTable alias (server replays via event log)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.reconnectGame(conn, { gameId: 'g-1' });
    expect(invokeSpy).toHaveBeenCalledWith('JoinTable', { gameId: 'g-1' });
  });
});

// ── attachServerEventHandlers ─────────────────────────────────────────────

describe('signalrClient / attachServerEventHandlers', () => {
  it('registers conn.on for every handler supplied (and skips undefined ones)', () => {
    const { conn, onSpy, registered } = makeFakeConnection();

    const handlers: ServerEventHandlers = {
      GameCreated: vi.fn(),
      DiceRolled: vi.fn(),
      TileDiscarded: vi.fn(),
      WinDeclared: vi.fn(),
      BankerRotated: vi.fn(),
      // Intentionally omitting the rest — they must NOT be registered.
    };

    attachServerEventHandlers(conn, handlers);

    const registeredNames = Array.from(registered.keys()).sort();
    expect(registeredNames).toEqual(
      ['GameCreated', 'DiceRolled', 'TileDiscarded', 'WinDeclared', 'BankerRotated'].sort()
    );
    expect(onSpy).toHaveBeenCalledTimes(5);
  });

  it('forwards payloads to the user handler', () => {
    const { conn, registered } = makeFakeConnection();
    const gameCreated = vi.fn();

    attachServerEventHandlers(conn, { GameCreated: gameCreated });

    const handlerSet = registered.get('GameCreated');
    expect(handlerSet?.size).toBe(1);
    const [innerFn] = Array.from(handlerSet!);
    innerFn({ gameId: 'g-x', ruleSet: 'changsha-v1', seats: [] });

    expect(gameCreated).toHaveBeenCalledWith({
      gameId: 'g-x',
      ruleSet: 'changsha-v1',
      seats: [],
    });
  });

  it('teardown removes every registered listener', () => {
    const { conn, offSpy, registered } = makeFakeConnection();
    const handlers: ServerEventHandlers = {
      GameCreated: vi.fn(),
      DiceRolled: vi.fn(),
    };
    const teardown = attachServerEventHandlers(conn, handlers);
    expect(registered.get('GameCreated')?.size).toBe(1);
    teardown();
    expect(offSpy).toHaveBeenCalledTimes(2);
    expect(registered.get('GameCreated')?.size).toBe(0);
    expect(registered.get('DiceRolled')?.size).toBe(0);
  });

  it('swallows handler exceptions and logs them (does not break other handlers)', () => {
    const consoleErrorSpy = vi
      .spyOn(console, 'error')
      .mockImplementation(() => undefined);
    const { conn, registered } = makeFakeConnection();
    const goodHandler = vi.fn();

    attachServerEventHandlers(conn, {
      GameCreated: () => {
        throw new Error('boom');
      },
      DiceRolled: goodHandler,
    });

    const gcFn = Array.from(registered.get('GameCreated')!)[0];
    expect(() => gcFn({ gameId: 'g', ruleSet: 'changsha-v1', seats: [] })).not.toThrow();
    expect(consoleErrorSpy).toHaveBeenCalled();

    const drFn = Array.from(registered.get('DiceRolled')!)[0];
    drFn({ gameId: 'g', rollerSeatIndex: 0, dice: { die1: 1, die2: 1, sum: 2 } });
    expect(goodHandler).toHaveBeenCalled();

    consoleErrorSpy.mockRestore();
  });
});

// ── describeConnectionState ───────────────────────────────────────────────

describe('signalrClient / describeConnectionState', () => {
  it('maps every HubConnectionState to our ConnectionStatus values', () => {
    expect(describeConnectionState(HubConnectionState.Connecting)).toBe('connecting');
    expect(describeConnectionState(HubConnectionState.Connected)).toBe('connected');
    expect(describeConnectionState(HubConnectionState.Reconnecting)).toBe(
      'reconnecting'
    );
    expect(describeConnectionState(HubConnectionState.Disconnected)).toBe(
      'disconnected'
    );
    expect(describeConnectionState(HubConnectionState.Disconnecting)).toBe(
      'disconnected'
    );
  });

  it('treats Disconnected as an explicit state change (not idle)', () => {
    // This is the contract the React hook relies on for surfacing
    // disconnection in the UI.
    expect(describeConnectionState(HubConnectionState.Disconnected)).not.toBe('idle');
    expect(describeConnectionState(HubConnectionState.Disconnected)).toBe(
      'disconnected'
    );
  });
});
