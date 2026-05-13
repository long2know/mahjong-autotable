/**
 * changshaReducer.test.ts
 *
 * Tests the pure reducer that folds SignalR server events into a
 * client-side ChangshaGameState. Asserts against the **current** reducer
 * behavior (Phase 2 wiring), not aspirational event names — if a test
 * here turns red after a Hicks change, prefer adjusting the test before
 * patching the reducer.
 *
 * Notes on naming:
 * - Reducer uses ClaimMade (with meld.type discriminating pung/kong/chow),
 *   not PungClaimed/KongClaimed/ChowClaimed.
 * - Reducer uses ScoringComplete + HandFinished + BankerRotated +
 *   GameEnded for terminal transitions, not WinDeclared-only.
 * - Per-seat hand splitting: TilesDealt with explicit tileIds populates
 *   the local seat; remote seats receive count-only events.
 */
import { describe, it, expect } from 'vitest';
import {
  changshaReducer,
  initialChangshaState,
  type GameAction,
} from '../changshaReducer';
import type {
  ChangshaGameState,
  MeldState,
  SeatState,
  WinResult,
} from '../types';
import type {
  BankerRotatedEvent,
  ClaimMadeEvent,
  ClaimWindowOpenEvent,
  DiceRolledEvent,
  GameCreatedEvent,
  GameEndedEvent,
  GameStartedEvent,
  HandFinishedEvent,
  PlayerSeatedEvent,
  RoundChangedEvent,
  ScoringCompleteEvent,
  TileDiscardedEvent,
  TilesDealtEvent,
  WinDeclaredEvent,
} from '../signalrClient';

// ── Helpers ──────────────────────────────────────────────────────────────

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

function apply(state: ChangshaGameState, ...actions: GameAction[]): ChangshaGameState {
  return actions.reduce((s, a) => changshaReducer(s, a), state);
}

// ── GameCreated ──────────────────────────────────────────────────────────

describe('changshaReducer / GameCreated', () => {
  it('sets gameId, phase=lobby, and a 4-seat seat array', () => {
    const ev: GameCreatedEvent = {
      gameId: 'game-abc',
      ruleSet: 'changsha-v1',
      seats: [seat(0), seat(1, true), seat(2, true), seat(3, true)],
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'GameCreated',
      payload: ev,
    });

    expect(next.gameId).toBe('game-abc');
    expect(next.phase).toBe('lobby');
    expect(next.seats).toHaveLength(4);
    expect(next.seats.map((s) => s.index)).toEqual([0, 1, 2, 3]);
    expect(next.seats.map((s) => s.nick)).toEqual([
      'player-0',
      'bot-1',
      'bot-2',
      'bot-3',
    ]);
    expect(next.seats[1].isBot).toBe(true);
  });

  it('falls back to default wind/nick when payload omits a seat', () => {
    const ev: GameCreatedEvent = {
      gameId: 'game-partial',
      ruleSet: 'changsha-v1',
      seats: [seat(0)],
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'GameCreated',
      payload: ev,
    });

    expect(next.seats[3].seatWind).toBe('north');
    expect(next.seats[3].nick).toBe('Seat 3');
    expect(next.seats[3].isBot).toBe(false);
  });
});

// ── PlayerSeated ─────────────────────────────────────────────────────────

describe('changshaReducer / PlayerSeated', () => {
  it('populates the named seat with nick and isBot', () => {
    const created: GameAction = {
      type: 'GameCreated',
      payload: {
        gameId: 'g1',
        ruleSet: 'changsha-v1',
        seats: [seat(0), seat(1), seat(2), seat(3)],
      },
    };
    const seated: GameAction = {
      type: 'PlayerSeated',
      payload: {
        gameId: 'g1',
        seatIndex: 2,
        playerId: 'Stephen',
        isBot: false,
      } satisfies PlayerSeatedEvent,
    };
    const next = apply(initialChangshaState(), created, seated);

    expect(next.seats[2].nick).toBe('Stephen');
    expect(next.seats[2].isBot).toBe(false);
    // Other seats untouched
    expect(next.seats[0].nick).toBe('player-0');
  });
});

// ── GameStarted + DiceRolled + BreakPointSet ─────────────────────────────

describe('changshaReducer / GameStarted + DiceRolled + BreakPointSet', () => {
  it('sets dealer, round wind, hand number, dice, and break point', () => {
    const start: GameStartedEvent = {
      gameId: 'g1',
      dealerSeatIndex: 0,
      roundWind: 'east',
      handNumber: 1,
    };
    const dice: DiceRolledEvent = {
      gameId: 'g1',
      rollerSeatIndex: 0,
      dice: { die1: 3, die2: 4, sum: 7 },
    };
    const bp: GameAction = {
      type: 'BreakPointSet',
      payload: {
        gameId: 'g1',
        breakPoint: { wallIndex: 2, stackIndex: 7, tileIndex: 61 },
      },
    };

    const next = apply(
      initialChangshaState(),
      { type: 'GameStarted', payload: start },
      { type: 'DiceRolled', payload: dice },
      bp
    );

    expect(next.bankerSeat).toBe(0);
    expect(next.prevalentWind).toBe('east');
    expect(next.currentHand).toBe(1);
    expect(next.lastDice).toEqual({ die1: 3, die2: 4, sum: 7 });
    expect(next.breakPoint).toEqual({ wallIndex: 2, stackIndex: 7, tileIndex: 61 });
    // BreakPointSet advances phase to dealing
    expect(next.phase).toBe('dealing');
  });
});

// ── TilesDealt (per-seat hand splitting) ─────────────────────────────────

describe('changshaReducer / TilesDealt', () => {
  it('populates concealed tiles when explicit tileIds are present (local seat)', () => {
    const ev: TilesDealtEvent = {
      gameId: 'g1',
      seatIndex: 0,
      tileIds: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13],
      tileCount: 14,
      batchNumber: 4,
      isComplete: true,
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'TilesDealt',
      payload: ev,
    });
    const hand = next.hands.find((h) => h.seatIndex === 0);
    expect(hand?.concealed.map((t) => t.id)).toEqual([
      0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13,
    ]);
    expect(next.phase).toBe('awaitingDiscard');
  });

  it('leaves remote-seat concealed list empty when tileIds is empty (count-only)', () => {
    const ev: TilesDealtEvent = {
      gameId: 'g1',
      seatIndex: 2,
      tileIds: [],
      tileCount: 13,
      batchNumber: 4,
      isComplete: false,
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'TilesDealt',
      payload: ev,
    });
    const hand = next.hands.find((h) => h.seatIndex === 2);
    expect(hand?.concealed).toEqual([]);
    expect(next.phase).toBe('dealing');
  });
});

// ── TileDiscarded ────────────────────────────────────────────────────────

describe('changshaReducer / TileDiscarded', () => {
  it('appends the tile to discardPile and removes it from the seat hand', () => {
    const dealt: GameAction = {
      type: 'TilesDealt',
      payload: {
        gameId: 'g1',
        seatIndex: 0,
        tileIds: [10, 11, 12],
        tileCount: 3,
        batchNumber: 1,
        isComplete: true,
      },
    };
    const discardEv: TileDiscardedEvent = {
      gameId: 'g1',
      seatIndex: 0,
      tileId: 11,
      turnNumber: 1,
    };
    const next = apply(initialChangshaState(), dealt, {
      type: 'TileDiscarded',
      payload: discardEv,
    });

    expect(next.discardPile.map((t) => t.id)).toEqual([11]);
    const hand = next.hands.find((h) => h.seatIndex === 0);
    expect(hand?.concealed.map((t) => t.id)).toEqual([10, 12]);
    expect(next.phase).toBe('awaitingClaim');
  });
});

// ── ClaimWindowOpen ──────────────────────────────────────────────────────

describe('changshaReducer / ClaimWindowOpen', () => {
  it('sets phase=awaitingClaim and stores pending claim opportunities', () => {
    const ev: ClaimWindowOpenEvent = {
      gameId: 'g1',
      discardSeatIndex: 0,
      discardTileId: 5,
      opportunities: [
        { seatIndex: 1, claimType: 'pung', priority: 2 },
        { seatIndex: 2, claimType: 'chow', priority: 3, tileIds: [4, 5, 6] },
      ],
      timeoutMs: 5000,
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'ClaimWindowOpen',
      payload: ev,
    });
    expect(next.phase).toBe('awaitingClaim');
    expect(next.pendingClaims).toHaveLength(2);
    expect(next.pendingClaims?.[0]).toMatchObject({ seatIndex: 1, type: 'pung' });
    expect(next.pendingClaims?.[1]).toMatchObject({ seatIndex: 2, type: 'chow' });
  });
});

// ── ClaimMade (covers pung, kong, chow) ──────────────────────────────────

describe('changshaReducer / ClaimMade', () => {
  it('pung: appends a pung meld to the claiming seat, removes used tiles from concealed', () => {
    const dealt: GameAction = {
      type: 'TilesDealt',
      payload: {
        gameId: 'g1',
        seatIndex: 1,
        tileIds: [20, 21],
        tileCount: 2,
        batchNumber: 1,
        isComplete: true,
      },
    };
    const claim: ClaimMadeEvent = {
      gameId: 'g1',
      claimingSeatIndex: 1,
      claimType: 'pung',
      tileId: 22,
      meld: {
        type: 'pung',
        tileIds: [20, 21, 22],
        claimedFrom: 0,
      },
    };
    const next = apply(initialChangshaState(), dealt, {
      type: 'ClaimMade',
      payload: claim,
    });

    const hand = next.hands.find((h) => h.seatIndex === 1);
    expect(hand?.melds).toHaveLength(1);
    expect(hand?.melds[0]).toMatchObject<MeldState>({
      type: 'pung',
      tileIds: [20, 21, 22],
      claimedFrom: 0,
    });
    expect(hand?.concealed.map((t) => t.id)).toEqual([]);
    expect(next.activeSeat).toBe(1);
    expect(next.phase).toBe('awaitingDiscard');
    expect(next.pendingClaims).toBeUndefined();
  });

  it('kong (exposed): appends an exposedKong meld', () => {
    const dealt: GameAction = {
      type: 'TilesDealt',
      payload: {
        gameId: 'g1',
        seatIndex: 2,
        tileIds: [40, 41, 42],
        tileCount: 3,
        batchNumber: 1,
        isComplete: true,
      },
    };
    const claim: ClaimMadeEvent = {
      gameId: 'g1',
      claimingSeatIndex: 2,
      claimType: 'kong',
      tileId: 43,
      meld: {
        type: 'exposedKong',
        tileIds: [40, 41, 42, 43],
        claimedFrom: 0,
      },
    };
    const next = apply(initialChangshaState(), dealt, {
      type: 'ClaimMade',
      payload: claim,
    });
    const hand = next.hands.find((h) => h.seatIndex === 2);
    expect(hand?.melds[0].type).toBe('exposedKong');
    expect(hand?.melds[0].tileIds).toEqual([40, 41, 42, 43]);
  });

  it('chow with explicit tileIds: named tiles move from concealed to exposed meld', () => {
    // Seat 1 holds 4-tong (id=36..39) and 6-tong (id=44..47); claims 5-tong
    // off seat 0 (id 40-43). Use specific copies.
    const dealt: GameAction = {
      type: 'TilesDealt',
      payload: {
        gameId: 'g1',
        seatIndex: 1,
        tileIds: [36, 44, 99],
        tileCount: 3,
        batchNumber: 1,
        isComplete: true,
      },
    };
    const claim: ClaimMadeEvent = {
      gameId: 'g1',
      claimingSeatIndex: 1,
      claimType: 'chow',
      tileId: 40,
      meld: {
        type: 'chow',
        tileIds: [36, 40, 44],
        claimedFrom: 0,
      },
    };
    const next = apply(initialChangshaState(), dealt, {
      type: 'ClaimMade',
      payload: claim,
    });
    const hand = next.hands.find((h) => h.seatIndex === 1);
    expect(hand?.melds).toHaveLength(1);
    expect(hand?.melds[0].tileIds).toEqual([36, 40, 44]);
    // The two named tiles from concealed are gone, the unused tile (99) remains
    expect(hand?.concealed.map((t) => t.id)).toEqual([99]);
  });
});

// ── WinDeclared ──────────────────────────────────────────────────────────

describe('changshaReducer / WinDeclared', () => {
  it('records the WinResult and advances phase to scoring', () => {
    const win: WinResult = {
      winningSeatIndex: 0,
      winType: 'selfDraw',
      winPattern: 'standard',
      winningTileId: 7,
      sourceSeatIndex: 0,
    };
    const ev: WinDeclaredEvent = {
      gameId: 'g1',
      winResult: win,
      hand: { concealedTiles: [], melds: [] },
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'WinDeclared',
      payload: ev,
    });
    expect(next.lastWin).toEqual(win);
    expect(next.phase).toBe('scoring');
  });
});

// ── ScoringComplete + BankerRotated (winner-becomes-dealer) ──────────────

describe('changshaReducer / ScoringComplete + BankerRotated', () => {
  it('ScoringComplete updates seat scores and advances to endHand', () => {
    const ev: ScoringCompleteEvent = {
      gameId: 'g1',
      handSummary: {
        handNumber: 1,
        roundWind: 'east',
        dealerSeatIndex: 0,
        isDraw: false,
        scoreResult: {
          category: 'smallWin',
          basePoints: 1,
          payments: [],
        },
      },
      gameSummary: {
        gameId: 'g1',
        totalHands: 16,
        currentRound: 1,
        roundWind: 'east',
        handInRound: 1,
        dealerSeatIndex: 0,
        scores: { 0: 3, 1: -1, 2: -1, 3: -1 },
      },
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'ScoringComplete',
      payload: ev,
    });
    expect(next.phase).toBe('endHand');
    expect(next.seats.map((s) => s.score)).toEqual([3, -1, -1, -1]);
    expect(next.lastScore?.basePoints).toBe(1);
  });

  it('BankerRotated updates bankerSeat (winner-becomes-dealer when reason=winnerBecomesDealer)', () => {
    const ev: BankerRotatedEvent = {
      gameId: 'g1',
      previousDealerSeatIndex: 0,
      newDealerSeatIndex: 2,
      reason: 'winnerBecomesDealer',
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'BankerRotated',
      payload: ev,
    });
    expect(next.bankerSeat).toBe(2);
    expect(next.phase).toBe('rotatingBanker');
  });
});

// ── HandFinished + RoundChanged + GameEnded ──────────────────────────────

describe('changshaReducer / terminal transitions', () => {
  it('HandFinished rolls dealer + clears per-hand state and re-enters rollingDice', () => {
    const state = initialChangshaState();
    const ev: HandFinishedEvent = {
      gameId: 'g1',
      handNumber: 1,
      handSummary: {
        handNumber: 1,
        roundWind: 'east',
        dealerSeatIndex: 0,
        isDraw: false,
      },
      nextHandNumber: 2,
      nextDealerSeatIndex: 2,
      nextRoundWind: 'east',
      isGameOver: false,
    };
    const next = changshaReducer(
      { ...state, discardPile: [{ id: 1, suit: 'wan', rank: 1 }] },
      { type: 'HandFinished', payload: ev }
    );
    expect(next.currentHand).toBe(2);
    expect(next.bankerSeat).toBe(2);
    expect(next.discardPile).toEqual([]);
    expect(next.phase).toBe('rollingDice');
  });

  it('RoundChanged updates prevalentWind and roundNumber', () => {
    const ev: RoundChangedEvent = {
      gameId: 'g1',
      previousRoundWind: 'east',
      newRoundWind: 'south',
      roundNumber: 2,
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'RoundChanged',
      payload: ev,
    });
    expect(next.prevalentWind).toBe('south');
    expect(next.currentRound).toBe(2);
  });

  it('GameEnded sets phase=endGame and applies finalScores', () => {
    const ev: GameEndedEvent = {
      gameId: 'g1',
      gameSummary: {
        gameId: 'g1',
        totalHands: 16,
        currentRound: 4,
        roundWind: 'north',
        handInRound: 4,
        dealerSeatIndex: 1,
        scores: { 0: 10, 1: -3, 2: -3, 3: -4 },
      },
      finalScores: { 0: 10, 1: -3, 2: -3, 3: -4 },
      winner: { seatIndex: 0, score: 10 },
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'GameEnded',
      payload: ev,
    });
    expect(next.phase).toBe('endGame');
    expect(next.seats.map((s) => s.score)).toEqual([10, -3, -3, -4]);
  });

  it('HandFinished with isGameOver=true lands in endGame phase', () => {
    const ev: HandFinishedEvent = {
      gameId: 'g1',
      handNumber: 16,
      handSummary: {
        handNumber: 16,
        roundWind: 'north',
        dealerSeatIndex: 3,
        isDraw: false,
      },
      nextHandNumber: 16,
      nextDealerSeatIndex: 3,
      nextRoundWind: 'north',
      isGameOver: true,
    };
    const next = changshaReducer(initialChangshaState(), {
      type: 'HandFinished',
      payload: ev,
    });
    expect(next.phase).toBe('endGame');
  });
});

// ── reset ────────────────────────────────────────────────────────────────

describe('changshaReducer / reset', () => {
  it('returns a fresh initial state', () => {
    const dirty = changshaReducer(initialChangshaState(), {
      type: 'GameCreated',
      payload: {
        gameId: 'dirty',
        ruleSet: 'changsha-v1',
        seats: [seat(0)],
      },
    });
    expect(dirty.gameId).toBe('dirty');
    const reset = changshaReducer(dirty, { type: 'reset' });
    expect(reset.gameId).toBe('');
    expect(reset.phase).toBe('lobby');
  });
});
