// Phase J Wave 5 — Public matchmaking lobby module.
//
// Polls Bishop's `GET /api/matchmaking/lobby` endpoint every 5 s while
// the "Public Games" tab is visible.  Each entry renders as a card
// with a Join button.  The "Join Random" button invokes the SignalR
// hub's `JoinRandom` RPC; the host's "Make public" toggle invokes
// `SetGamePublic`.
//
// Tab activation / deactivation is driven by lobby.ts (Public Games
// vs My Game).  We expose start/stop helpers so the poll only runs
// while the user is actually looking at the list.
//
// ── Wire contract (Bishop, Phase J Wave 5) ──────────────────────────
//
//   GET /api/matchmaking/lobby   (MatchmakingController.cs)
//     → 200 { games: PublicGame[] }
//
//   PublicGame (LobbyGameDto.cs) = {
//     gameId: string;
//     publicName: string | null;
//     creatorDisplayName: string;
//     seatedCount: number;
//     maxSeats: number;
//     variant: string;
//     createdAt: string;       // ISO-8601 UTC
//   }
//
//   SignalR invoke 'JoinRandom'(variant?)
//     → { matched: true, gameId, seatIndex }
//     → { matched: false }
//
//   SignalR invoke 'SetGamePublic'(gameId, isPublic, publicName?)
//     → { success, isPublic, publicName }
//     Throws HubException when the caller isn't the host or the game
//     has left the Seating phase.

import { EventEmitter } from 'events';

import { invokeHub } from './hub';

export interface PublicGame {
  gameId: string;
  publicName: string | null;
  creatorDisplayName: string;
  seatedCount: number;
  maxSeats: number;
  variant: string;
  createdAt: string;
}

export const MATCHMAKING_POLL_MS = 5000;
export const MAX_PUBLIC_GAMES_RENDERED = 50;

const events = new EventEmitter();
let cache: ReadonlyArray<PublicGame> = [];
let lastError: string | null = null;
let pollTimer: number | null = null;
let inflight: AbortController | null = null;
let active = false;

function emitState(): void {
  events.emit('update', { games: cache, error: lastError });
}

function normalizePublicGame(g: unknown): PublicGame | null {
  if (g === null || typeof g !== 'object') return null;
  const o = g as Record<string, unknown>;
  if (typeof o.gameId !== 'string') return null;
  const publicName = typeof o.publicName === 'string' ? o.publicName : null;
  const creatorDisplayName = typeof o.creatorDisplayName === 'string' ? o.creatorDisplayName : 'Unknown';
  const seatedCount = typeof o.seatedCount === 'number' ? o.seatedCount : 0;
  const maxSeats = typeof o.maxSeats === 'number' ? o.maxSeats : 4;
  const variant = typeof o.variant === 'string' ? o.variant : 'changsha';
  const createdAt = typeof o.createdAt === 'string' ? o.createdAt : new Date().toISOString();
  return { gameId: o.gameId, publicName, creatorDisplayName, seatedCount, maxSeats, variant, createdAt };
}

async function pollOnce(): Promise<void> {
  if (inflight !== null) {
    inflight.abort();
  }
  const ctrl = new AbortController();
  inflight = ctrl;
  try {
    const resp = await fetch('/api/matchmaking/lobby', {
      credentials: 'same-origin',
      signal: ctrl.signal,
    });
    if (!resp.ok) {
      lastError = `HTTP ${resp.status}`;
      emitState();
      return;
    }
    const body = (await resp.json()) as unknown;
    const games: Array<PublicGame> = [];
    if (body !== null && typeof body === 'object') {
      const raw = (body as { games?: unknown }).games;
      if (Array.isArray(raw)) {
        for (const g of raw) {
          const n = normalizePublicGame(g);
          if (n !== null) games.push(n);
          if (games.length >= MAX_PUBLIC_GAMES_RENDERED) break;
        }
      }
    }
    cache = games;
    lastError = null;
    emitState();
  } catch (e) {
    if ((e as DOMException)?.name === 'AbortError') return;
    lastError = (e as Error)?.message ?? 'network error';
    emitState();
  } finally {
    if (inflight === ctrl) inflight = null;
  }
}

/** Start the 5-second poll loop.  Idempotent. */
export function startPolling(): void {
  if (active) return;
  active = true;
  void pollOnce();
  pollTimer = window.setInterval(() => { void pollOnce(); }, MATCHMAKING_POLL_MS);
}

/** Stop the poll loop and cancel any in-flight request. */
export function stopPolling(): void {
  active = false;
  if (pollTimer !== null) {
    window.clearInterval(pollTimer);
    pollTimer = null;
  }
  if (inflight !== null) {
    inflight.abort();
    inflight = null;
  }
}

export function isPolling(): boolean {
  return active;
}

export function getCachedGames(): ReadonlyArray<PublicGame> {
  return cache;
}

export function getLastError(): string | null {
  return lastError;
}

/** Subscribe to poll updates.  Returns an unsubscribe handle. */
export function onUpdate(
  handler: (state: { games: ReadonlyArray<PublicGame>; error: string | null }) => void,
): () => void {
  events.on('update', handler);
  handler({ games: cache, error: lastError });
  return () => events.off('update', handler);
}

/** One-shot manual refresh (e.g. on tab activate). */
export function refresh(): Promise<void> {
  return pollOnce();
}

// ── Action helpers ─────────────────────────────────────────────────

export interface JoinRandomResult {
  gameId: string;
  seatIndex: number;
}

/**
 * Ask the SignalR hub for any joinable public game.  Returns null
 * when the hub responds `{ matched: false }`.  Throws on transport
 * errors so the caller can surface an inline error toast.
 */
export async function joinRandom(variant?: string): Promise<JoinRandomResult | null> {
  const result = await invokeHub<unknown>('JoinRandom', variant ?? null);
  if (result === null || typeof result !== 'object') return null;
  const o = result as Record<string, unknown>;
  if (o.matched !== true) return null;
  if (typeof o.gameId !== 'string') return null;
  const seatIndex = typeof o.seatIndex === 'number' ? o.seatIndex : 0;
  return { gameId: o.gameId, seatIndex };
}

/**
 * Flip the current game's "public" flag via the SignalR hub.  Returns
 * the server's `{ success, isPublic, publicName }` payload so the UI
 * can echo the final state (which may differ from the requested
 * publicName if the server normalised / truncated it).  Caller is
 * responsible for passing the gameId of a game they actually host —
 * Bishop's hub rejects unauthorised flips with a HubException.
 */
export interface SetGamePublicResult {
  success: boolean;
  isPublic: boolean;
  publicName: string | null;
}

export async function setGamePublic(args: {
  gameId: string;
  isPublic: boolean;
  publicName?: string;
}): Promise<SetGamePublicResult> {
  const result = await invokeHub<unknown>(
    'SetGamePublic',
    args.gameId,
    args.isPublic,
    args.publicName ?? null,
  );
  // Trigger an immediate poll so the host sees their game appear (or
  // disappear) from the public list right away.
  if (active) void pollOnce();
  if (result === null || typeof result !== 'object') {
    return { success: false, isPublic: args.isPublic, publicName: null };
  }
  const o = result as Record<string, unknown>;
  return {
    success: o.success === true,
    isPublic: o.isPublic === true,
    publicName: typeof o.publicName === 'string' ? o.publicName : null,
  };
}

// ── Navigation helper ──────────────────────────────────────────────

/**
 * Navigate to the indicated game.  Preserves the current variant and
 * any handCount/dealMode/seed query params so the join lands the user
 * in the lobby's chosen configuration.  Used by both the Join card
 * button and the Join Random button.
 */
export function navigateToGame(gameId: string, seatIndex?: number): void {
  const params = new URLSearchParams(window.location.search);
  params.set('gameId', gameId);
  if (seatIndex !== undefined && seatIndex >= 0 && seatIndex <= 3) {
    params.set('seat', String(seatIndex));
  } else {
    params.delete('seat');
  }
  const url = window.location.pathname + '?' + params.toString();
  window.location.replace(url);
}
