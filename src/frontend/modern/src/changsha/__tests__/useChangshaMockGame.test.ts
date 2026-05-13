/**
 * useChangshaMockGame.test.ts
 *
 * Smoke test for the offline mock hook. The mock hook is currently the
 * primary frontend "playable" surface (dev defaults to mock mode in
 * useChangshaGame.ts), so this asserts that:
 *  - the hook mounts without throwing in a jsdom React 19 environment
 *  - it exposes the action shape the demo controls panel depends on
 *  - the deal + discard mock actions produce visible state transitions
 *
 * If Hicks reshapes the hook's action surface, this test should be
 * adjusted to match the new shape — it intentionally pins the public
 * action keys so we notice breakage at PR time rather than after a
 * regression in production.
 */
import { describe, it, expect } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useChangshaMockGame } from '../useChangshaMockGame';

describe('useChangshaMockGame', () => {
  it('mounts and returns a complete actions surface', () => {
    const { result } = renderHook(() => useChangshaMockGame());

    expect(result.current.state).toBeDefined();
    expect(result.current.state.gameId).toBe('demo-game-001');
    expect(result.current.state.seats).toHaveLength(4);

    // Pin the action surface; demo controls + ChangshaTablePage depend on it.
    const expected = [
      'rollDice',
      'confirmDice',
      'dealMock',
      'discard',
      'simulateClaimWindow',
      'resolveClaim',
      'simulateWin',
      'continueAfterScoring',
      'resetDemo',
    ];
    for (const name of expected) {
      expect(typeof (result.current.actions as Record<string, unknown>)[name]).toBe(
        'function'
      );
    }
  });

  it('dealMock populates per-seat hands and reduces the wall by 53 tiles', () => {
    const { result } = renderHook(() => useChangshaMockGame());

    act(() => {
      result.current.actions.dealMock();
    });

    expect(result.current.state.phase).toBe('awaitingDiscard');
    expect(result.current.state.hands).toHaveLength(4);
    // Banker (seat 0) gets 14, others get 13 — total 53 tiles dealt.
    const handSizes = result.current.state.hands.map((h) => h.concealed.length);
    expect(handSizes).toEqual([14, 13, 13, 13]);
    expect(result.current.state.wallRemaining).toBe(108 - 53);
    expect(result.current.state.activeSeat).toBe(0);
  });

  it('discard removes the chosen tile from the local hand and appends it to the discard pile', () => {
    const { result } = renderHook(() => useChangshaMockGame());

    act(() => {
      result.current.actions.dealMock();
    });

    const tileToDiscard = result.current.state.hands.find((h) => h.seatIndex === 0)!
      .concealed[0]!.id;

    act(() => {
      result.current.actions.discard(tileToDiscard);
    });

    const myHand = result.current.state.hands.find((h) => h.seatIndex === 0)!;
    expect(myHand.concealed.find((t) => t.id === tileToDiscard)).toBeUndefined();
    expect(
      result.current.state.discardPile[result.current.state.discardPile.length - 1].id
    ).toBe(tileToDiscard);
  });

  it('resetDemo returns the state to seating phase with empty hands', () => {
    const { result } = renderHook(() => useChangshaMockGame());

    act(() => {
      result.current.actions.dealMock();
    });
    expect(result.current.state.phase).toBe('awaitingDiscard');

    act(() => {
      result.current.actions.resetDemo();
    });

    expect(result.current.state.phase).toBe('seating');
    expect(result.current.state.hands).toHaveLength(0);
    expect(result.current.state.discardPile).toHaveLength(0);
  });
});
