// Phase J Wave 7 — Replay launcher.
//
// Single entry point for "open the replay viewer for game X".  Handles:
//   • Try `GET /api/games/{gameId}/replay` (Bishop's Wave-7 endpoint).
//   • On 404, fall back to the in-memory client.gameComplete payload
//     (the Wave-3 behaviour).
//   • Open the existing 2D replay screen with the resolved event list.
//
// The launcher routes through a module-level callback (`launcher`) that
// `game-ui.ts` wires at boot time — that lets us keep the Replay class
// unaware of the server fetch logic while still letting any surface
// (leaderboard row, profile page row, post-game modal) trigger a
// replay by passing only a `gameId`.

import { type HandResultEntry } from './types';

export interface ServerReplayEvent {
  turn: number;
  phase: string;
  actor: number;        // seat 0..3, or -1 for system events
  action: string;
  tilesJson: string;
  timestampUtc: string;
}

export interface ServerReplayResponse {
  gameId: string;
  events: ReadonlyArray<ServerReplayEvent>;
  handHistory?: ReadonlyArray<HandResultEntry>;
}

export type ReplayLauncher = (payload: ServerReplayResponse, options?: { finals?: boolean }) => void;

let launcher: ReplayLauncher | null = null;

/** game-ui.ts calls this at boot once the Replay singleton exists. */
export function registerReplayLauncher(fn: ReplayLauncher): void {
  launcher = fn;
}

/**
 * Open the replay viewer for `gameId`.  Feature-detects Bishop's
 * Wave-7 server endpoint; on 404 / network error falls back to an
 * empty event list so the viewer at least renders its shell.
 *
 * Phase K Wave 2 — `options.finals` carries the "auto-scroll to the
 * final hand" intent.  Tournament finals links + the `?finals=true`
 * URL deep-link both route through this entry point so the behaviour
 * stays consistent.
 */
export async function openReplayForGame(
  gameId: string,
  options?: { finals?: boolean },
): Promise<void> {
  if (gameId === '' || launcher === null) return;
  const finals = options?.finals === true;
  // Stamp `?finals=true` on the URL when the caller asked for it so
  // a shared / bookmarked replay link reopens at the final hand.
  if (finals) {
    try {
      const url = new URL(window.location.href);
      url.searchParams.set('finals', 'true');
      window.history.replaceState(null, '', url.toString());
    } catch { /* file:// or sandboxed contexts — best effort */ }
  }
  try {
    const resp = await fetch(
      `/api/games/${encodeURIComponent(gameId)}/replay`,
      {
        credentials: 'same-origin',
        headers: { 'Accept': 'application/json' },
      },
    );
    if (resp.status === 404) {
      // Endpoint not implemented yet — render an empty viewer with a
      // placeholder so the user sees we tried.
      launcher({ gameId, events: [], handHistory: [] }, { finals });
      return;
    }
    if (!resp.ok) {
      launcher({ gameId, events: [], handHistory: [] }, { finals });
      return;
    }
    const body = (await resp.json()) as unknown;
    const normalized = normalizeServerReplay(gameId, body);
    launcher(normalized, { finals });
  } catch {
    launcher({ gameId, events: [], handHistory: [] }, { finals });
  }
}

/** Read the `finals=true` flag from the current URL.  Used by replay.ts
 *  to auto-scroll to the final hand when a deep link lands. */
export function readFinalsFlagFromUrl(): boolean {
  try {
    const params = new URLSearchParams(window.location.search);
    return params.get('finals') === 'true';
  } catch {
    return false;
  }
}

function normalizeServerReplay(gameId: string, raw: unknown): ServerReplayResponse {
  const o = (raw !== null && typeof raw === 'object' ? raw : {}) as Record<string, unknown>;
  const events: ServerReplayEvent[] = [];
  const rawEvents = Array.isArray(o.events) ? o.events : [];
  for (const e of rawEvents) {
    if (e === null || typeof e !== 'object') continue;
    const ev = e as Record<string, unknown>;
    events.push({
      turn: typeof ev.turn === 'number' ? ev.turn : 0,
      phase: typeof ev.phase === 'string' ? ev.phase : '',
      actor: typeof ev.actor === 'number' ? ev.actor : -1,
      action: typeof ev.action === 'string' ? ev.action : '',
      tilesJson: typeof ev.tilesJson === 'string'
        ? ev.tilesJson
        : (typeof ev.tiles === 'string' ? ev.tiles : ''),
      timestampUtc: typeof ev.timestampUtc === 'string'
        ? ev.timestampUtc
        : (typeof ev.timestamp === 'string' ? ev.timestamp : ''),
    });
  }
  // Optional handHistory (Phase J Wave 3 shape).
  let handHistory: HandResultEntry[] | undefined;
  if (Array.isArray(o.handHistory)) {
    handHistory = [];
    for (const h of o.handHistory) {
      if (h === null || typeof h !== 'object') continue;
      const hh = h as Record<string, unknown>;
      handHistory.push({
        winner: typeof hh.winner === 'number' ? hh.winner : -1,
        type: (hh.type === 'Hu' || hh.type === 'Draw' || hh.type === 'ZhaHu')
          ? hh.type
          : 'Draw',
        score: Array.isArray(hh.score) ? hh.score as HandResultEntry['score'] : [],
        hand: Array.isArray(hh.hand) ? hh.hand as number[] : [],
        nextBanker: typeof hh.nextBanker === 'number' ? hh.nextBanker : 0,
      });
    }
  }
  return { gameId, events, handHistory };
}

/** Parse a tilesJson payload from a server event.  Best-effort. */
export function parseTilesJson(json: string): number[] {
  if (json === '' || json === undefined) return [];
  try {
    const v = JSON.parse(json) as unknown;
    if (Array.isArray(v)) {
      const out: number[] = [];
      for (const t of v) {
        if (typeof t === 'number' && isFinite(t)) out.push(t);
      }
      return out;
    }
  } catch { /* ignore */ }
  return [];
}
