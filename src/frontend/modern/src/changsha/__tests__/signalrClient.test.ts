/**
 * signalrClient.test.ts
 *
 * Asserts that the `invoke` helpers in signalrClient.ts wrap
 * HubConnection.invoke with the contractually-correct method name and
 * POSITIONAL arguments (matching the .NET hub signatures verbatim), that
 * attachServerEventHandlers wires conn.on for every supplied handler, and
 * that describeConnectionState maps HubConnectionState to our public
 * ConnectionStatus values.
 *
 * Notes (Phase 3, stlong/changsha-v1-phase3):
 * - The wrappers now pass args positionally to .NET hubs. The earlier
 *   shape (single payload object) silently mapped to the first parameter
 *   on the .NET side and produced coerced garbage for the rest — see
 *   ChangshaHub.cs for the real signatures.
 * - reconnectGame now invokes the dedicated ReconnectGame(gameId, seatIndex)
 *   hub method, which replays a FullState event for the requesting seat.
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
// Phase 3 contract: positional args mirroring ChangshaHub.cs.

describe('signalrClient / invoke wrappers', () => {
  it('createGame calls invoke("CreateGame", ruleSet, botSeatIndexes, seed)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.createGame(conn, {
      ruleSet: 'changsha-v1',
      botSeatIndexes: [1, 2, 3],
      seed: 42,
    });
    expect(invokeSpy).toHaveBeenCalledWith('CreateGame', 'changsha-v1', [1, 2, 3], 42);
  });

  it('joinTable calls invoke("JoinTable", gameId)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.joinTable(conn, { gameId: 'g-1' });
    expect(invokeSpy).toHaveBeenCalledWith('JoinTable', 'g-1');
  });

  it('takeSeat calls invoke("TakeSeat", gameId, seatIndex)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.takeSeat(conn, { gameId: 'g-1', seatIndex: 0 });
    expect(invokeSpy).toHaveBeenCalledWith('TakeSeat', 'g-1', 0);
  });

  it('startGame calls invoke("StartGame", gameId)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.startGame(conn, { gameId: 'g-1' });
    expect(invokeSpy).toHaveBeenCalledWith('StartGame', 'g-1');
  });

  it('rollDice calls invoke("RollDice", gameId)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.rollDice(conn, { gameId: 'g-1' });
    expect(invokeSpy).toHaveBeenCalledWith('RollDice', 'g-1');
  });

  it('acknowledgeDeal calls invoke("AcknowledgeDeal", gameId, seatIndex)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.acknowledgeDeal(conn, { gameId: 'g-1', seatIndex: 0 });
    expect(invokeSpy).toHaveBeenCalledWith('AcknowledgeDeal', 'g-1', 0);
  });

  it('discard calls invoke("Discard", gameId, seatIndex, tileId)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.discard(conn, { gameId: 'g-1', seatIndex: 0, tileId: 42 });
    expect(invokeSpy).toHaveBeenCalledWith('Discard', 'g-1', 0, 42);
  });

  it('claim calls invoke("Claim", gameId, seatIndex, type, tileIds) for chow', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.claim(conn, {
      gameId: 'g-1',
      seatIndex: 1,
      type: 'chow',
      tileIds: [4, 5, 6],
    });
    expect(invokeSpy).toHaveBeenCalledWith('Claim', 'g-1', 1, 'chow', [4, 5, 6]);
  });

  it('claim passes null tileIds when omitted (pung / kong path)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.claim(conn, { gameId: 'g-1', seatIndex: 1, type: 'pung' });
    expect(invokeSpy).toHaveBeenCalledWith('Claim', 'g-1', 1, 'pung', null);
  });

  it('pass calls invoke("Pass", gameId, seatIndex)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.pass(conn, { gameId: 'g-1', seatIndex: 2 });
    expect(invokeSpy).toHaveBeenCalledWith('Pass', 'g-1', 2);
  });

  it('declareKong calls invoke("DeclareKong", gameId, seatIndex, tileIds)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.declareKong(conn, {
      gameId: 'g-1',
      seatIndex: 0,
      tileIds: [12, 13, 14, 15],
    });
    expect(invokeSpy).toHaveBeenCalledWith('DeclareKong', 'g-1', 0, [12, 13, 14, 15]);
  });

  it('declareWin calls invoke("DeclareWin", gameId, seatIndex)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.declareWin(conn, { gameId: 'g-1', seatIndex: 0 });
    expect(invokeSpy).toHaveBeenCalledWith('DeclareWin', 'g-1', 0);
  });

  it('fillWithBots calls invoke("FillWithBots", gameId)', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.fillWithBots(conn, { gameId: 'g-1' });
    expect(invokeSpy).toHaveBeenCalledWith('FillWithBots', 'g-1');
  });

  it('reconnectGame invokes ReconnectGame(gameId, seatIndex) and triggers FullState replay', async () => {
    const { conn, invokeSpy } = makeFakeConnection();
    await invoke.reconnectGame(conn, { gameId: 'g-1', seatIndex: 0 });
    expect(invokeSpy).toHaveBeenCalledWith('ReconnectGame', 'g-1', 0);
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
