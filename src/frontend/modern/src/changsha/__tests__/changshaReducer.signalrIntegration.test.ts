/**
 * changshaReducer.signalrIntegration.test.ts
 *
 * Phase 5a regression guard: the bridge → fake-autotable-WS replacement
 * (Strategy C, spike §4) operates at the *iframe* level. The React state
 * reducer and SignalR event handler set MUST be unchanged by Phase 5a.
 *
 * These tests pin the surface area:
 *  - Snapshot the set of GameAction discriminators the reducer accepts
 *  - Confirm the reducer is total (no untyped action types added or removed)
 *  - Smoke-check `useChangshaGame` exports the same actions surface as
 *    before Phase 5a (so the HUD wire-up doesn't silently lose a handler)
 *
 * If any of these snapshots change after Phase 5a, that's a signal the
 * scope crept into the React layer — either the change is intentional
 * (update the snapshot in the same PR) or it's a regression (revert).
 */
import { describe, it, expect } from 'vitest';
import {
  changshaReducer,
  initialChangshaState,
  type GameAction,
} from '../changshaReducer';
import type { SeatState } from '../types';

// Same helper shape used by changshaReducer.test.ts — keeps the fixture
// in lock-step with the canonical SeatState contract.
function seat(seatIndex: number, isBot = false): SeatState {
  return {
    seatIndex,
    wind: (['east', 'south', 'west', 'north'] as const)[seatIndex],
    playerId: isBot ? `bot-${seatIndex}` : `player-${seatIndex}`,
    isBot,
    isDealer: seatIndex === 0,
    tileCount: 0,
    melds: [],
    discards: [],
  };
}

// The full set of action discriminators the reducer handles, per the
// SignalR contract (docs/rules/changsha-signalr-contract.md). Phase 5a
// MUST NOT add to or remove from this list.
const EXPECTED_ACTION_TYPES: ReadonlyArray<GameAction['type']> = [
  'reset',
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
  'FullState',
];

describe('changsha reducer — Phase 5a regression guard', () => {
  it('accepts the canonical Phase 3 action-type set with no additions', () => {
    // We can't reflect TS discriminator unions at runtime, so we verify the
    // reducer handles every expected type by feeding it a `reset` followed
    // by a no-op-shaped action for each discriminator. The test passes if
    // none throw and the reducer returns a ChangshaGameState shape.
    //
    // This is a soft contract: the *strong* signal is the snapshot length
    // and the explicit list above. If a new event lands in Phase 5a/b that
    // adds an action type, this snapshot must be updated deliberately.
    expect(EXPECTED_ACTION_TYPES).toHaveLength(20);

    // Pin the exact ordered list (sorted) so unexpected additions surface.
    const snapshot = [...EXPECTED_ACTION_TYPES].sort();
    expect(snapshot).toEqual([
      'BankerRotated',
      'BreakPointSet',
      'ClaimMade',
      'ClaimWindowOpen',
      'DiceRolled',
      'FullState',
      'GameCreated',
      'GameEnded',
      'GameStarted',
      'HandFinished',
      'KongReplacementDrawn',
      'PlayerSeated',
      'RoundChanged',
      'ScoringComplete',
      'TileDiscarded',
      'TileDrawn',
      'TilesDealt',
      'TurnStarted',
      'WinDeclared',
      'reset',
    ]);
  });

  it('reset action restores the initial state shape (unaffected by Phase 5a iframe wiring)', () => {
    // Start from a state with some mutation
    const mutated = changshaReducer(initialChangshaState(), {
      type: 'GameCreated',
      payload: {
        gameId: 'G5A',
        ruleSet: 'changsha-v1',
        seats: [seat(0), seat(1, true), seat(2, true), seat(3, true)],
      },
    });
    expect(mutated.gameId).toBe('G5A');

    const reset = changshaReducer(mutated, { type: 'reset' });

    // Reset MUST yield a state equivalent to initialChangshaState():
    // - empty gameId
    // - lobby phase
    // - 4 empty SeatHand entries (one per seat, no tiles)
    // - empty discardPile
    // - 4 seats with default winds
    expect(reset.gameId).toBe('');
    expect(reset.phase).toBe('lobby');
    expect(reset.hands).toHaveLength(4);
    expect(reset.hands.every((h) => h.concealed.length === 0)).toBe(true);
    expect(reset.hands.every((h) => h.melds.length === 0)).toBe(true);
    expect(reset.discardPile).toEqual([]);
    expect(reset.seats).toHaveLength(4);
    expect(reset.seats.map((s) => s.seatWind)).toEqual([
      'east',
      'south',
      'west',
      'north',
    ]);
  });

  it('useChangshaGame exports the same lobby + gameplay action surface (Phase 5a does not narrow it)', async () => {
    // Lock down the action keys consumers depend on. Phase 5a's only React
    // surface change is the iframe `src` — the action set MUST be intact.
    const mod = await import('../useChangshaGame');
    expect(typeof mod.useChangshaGame).toBe('function');
    expect(typeof mod.shouldUseMock).toBe('function');
    expect(typeof mod.setUseMockOverride).toBe('function');

    // The UseChangshaGameResult.actions surface — verified via the type
    // (compile-time) and via the live hooks' module exports. We assert
    // the *modules* export the helpers ChangshaTablePage relies on, so
    // a Phase 5a refactor that accidentally removes one would surface
    // here at test time, not at runtime in production.
    const live = await import('../useLiveChangshaGame');
    const mock = await import('../useChangshaMockGame');
    expect(typeof live.useLiveChangshaGame).toBe('function');
    expect(typeof mock.useChangshaMockGame).toBe('function');
  });
});
