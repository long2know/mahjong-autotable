// Phase K Wave 4 — Per-table reactive game state.
//
// Wave 3 voice / settings-drawer each performed an independent `GET
// /api/games/{id}/settings` fetch to discover `voiceEnabled` and
// `viewerIsOwner`.  Wave 4 unifies those probes into a single source
// of truth so:
//
//   • Only one round-trip per page load — voice + settings-drawer +
//     any future surface (owner-only HUD chip, kick-player button)
//     consume the same cached snapshot.
//   • Bishop's `GameJoined` SignalR broadcast (Wave-3 backend) can
//     push a refreshed snapshot without surfaces refetching, by
//     calling `updateGameState({ ownerId, voiceEnabled })`.
//   • Subscribers receive callbacks the moment the state is
//     populated; late mounters (e.g. the settings drawer opened only
//     after the user clicks the gear icon) get the cached value
//     synchronously via `getGameState()`.
//
// Surface:
//   • `getGameState()` — snapshot or `null` if not yet populated.
//   • `subscribeGameState(cb)` — fires once on subscribe (if state
//     is populated) and again on every update.  Returns an unsubscribe.
//   • `loadGameState(gameId)` — fetches `/api/games/{id}` once;
//     idempotent per `gameId` for the lifetime of the page.
//   • `updateGameState(partial)` — apply a partial update (used by
//     the SignalR `GameJoined` handler or settings-drawer write
//     callbacks).  Triggers subscriber callbacks.

export interface GameState {
  gameId: string;
  ownerId: string | null;
  voiceEnabled: boolean;
  viewerIsOwner: boolean;
}

type Listener = (state: GameState) => void;

let state: GameState | null = null;
const listeners: Set<Listener> = new Set();
const inflight: Map<string, Promise<GameState | null>> = new Map();

export function getGameState(): GameState | null {
  return state;
}

export function subscribeGameState(cb: Listener): () => void {
  listeners.add(cb);
  if (state !== null) {
    try { cb(state); } catch { /* ignore listener errors */ }
  }
  return () => { listeners.delete(cb); };
}

export function updateGameState(patch: Partial<GameState>): GameState {
  const next: GameState = {
    gameId: patch.gameId ?? state?.gameId ?? '',
    ownerId: patch.ownerId ?? state?.ownerId ?? null,
    voiceEnabled: patch.voiceEnabled ?? state?.voiceEnabled ?? false,
    viewerIsOwner: patch.viewerIsOwner ?? state?.viewerIsOwner ?? false,
  };
  state = next;
  for (const l of listeners) {
    try { l(next); } catch { /* ignore */ }
  }
  return next;
}

interface RawGamePayload {
  id?: string;
  Id?: string;
  gameId?: string;
  GameId?: string;
  ownerId?: string;
  OwnerId?: string;
  owner?: { id?: string; OwnerId?: string };
  voiceEnabled?: boolean;
  VoiceEnabled?: boolean;
  viewerIsOwner?: boolean;
  ViewerIsOwner?: boolean;
}

function parseGamePayload(raw: unknown, fallbackGameId: string): GameState | null {
  if (raw === null || typeof raw !== 'object') return null;
  const o = raw as RawGamePayload;
  const ownerId =
    typeof o.ownerId === 'string' ? o.ownerId
    : (typeof o.OwnerId === 'string' ? o.OwnerId
      : (o.owner !== undefined && typeof o.owner.id === 'string' ? o.owner.id : null));
  const voiceEnabled = o.voiceEnabled === true || o.VoiceEnabled === true;
  const viewerIsOwner = o.viewerIsOwner === true || o.ViewerIsOwner === true;
  const gameId =
    typeof o.gameId === 'string' && o.gameId !== '' ? o.gameId
    : (typeof o.GameId === 'string' && o.GameId !== '' ? o.GameId
      : (typeof o.id === 'string' && o.id !== '' ? o.id
        : (typeof o.Id === 'string' && o.Id !== '' ? o.Id : fallbackGameId)));
  return { gameId, ownerId, voiceEnabled, viewerIsOwner };
}

/**
 * Fetches `/api/games/{id}` for the per-game metadata and merges it
 * into the reactive state.  Concurrent callers share one in-flight
 * request keyed by `gameId`.
 *
 * Falls back to `/api/games/{id}/settings` (Wave-3 endpoint) when the
 * Wave-4 `/api/games/{id}` route returns 404 — Bishop is shipping the
 * richer endpoint in a separate PR but Wave-3 still works for
 * `voiceEnabled` + `viewerIsOwner`.  `ownerId` will be null in that
 * degraded path; the settings-drawer + voice surfaces tolerate it.
 */
export async function loadGameState(gameId: string): Promise<GameState | null> {
  if (state !== null && state.gameId === gameId) return state;
  const existing = inflight.get(gameId);
  if (existing !== undefined) return existing;

  const promise = (async (): Promise<GameState | null> => {
    try {
      let parsed = await fetchGameMeta(gameId);
      if (parsed === null) {
        parsed = await fetchGameSettings(gameId);
      }
      if (parsed !== null) {
        updateGameState(parsed);
      }
      return parsed;
    } finally {
      inflight.delete(gameId);
    }
  })();

  inflight.set(gameId, promise);
  return promise;
}

async function fetchGameMeta(gameId: string): Promise<GameState | null> {
  try {
    const r = await fetch(`/api/games/${encodeURIComponent(gameId)}`, {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (!r.ok) return null;
    const body = (await r.json()) as unknown;
    return parseGamePayload(body, gameId);
  } catch {
    return null;
  }
}

async function fetchGameSettings(gameId: string): Promise<GameState | null> {
  try {
    const r = await fetch(`/api/games/${encodeURIComponent(gameId)}/settings`, {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (!r.ok) return null;
    const body = (await r.json()) as unknown;
    return parseGamePayload(body, gameId);
  } catch {
    return null;
  }
}

/**
 * Tear-down for tests / page-transition cleanup.  Clears the cached
 * snapshot + listener set so the next `loadGameState()` starts fresh.
 */
export function resetGameState(): void {
  state = null;
  listeners.clear();
  inflight.clear();
}
