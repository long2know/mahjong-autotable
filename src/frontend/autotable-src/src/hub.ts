// Phase J Wave 5 — SignalR hub connection wrapper.
//
// Bishop's `ChangshaHub` (mapped at `/hubs/changsha`) exposes the
// Phase J Wave 5 profile + matchmaking surfaces:
//
//   • Server → Client:
//       'ProfileLoaded' { playerId, displayName, avatarColor, … stats }
//   • Client → Server (invoke):
//       'UpdateProfile'(displayName, avatarColor?) → same DTO as above
//       'SetGamePublic'(gameId, isPublic, publicName?)
//       'JoinRandom'(variant?) → { matched, gameId?, seatIndex? }
//
// The frontend's primary transport is the autotable WS bundle
// (BaseClient at `/ws`); SignalR is layered alongside it for the
// metadata / matchmaking surfaces only.  Both connections share the
// same `playerId` because the SignalR Hub's `Context.ConnectionId` is
// itself the per-tab identifier — see Bishop's
// PlayerProfile.cs:11–15 for the contract.
//
// `getHubConnection()` returns the singleton connection, lazily
// starting it on first use.  Idempotent — concurrent callers wait on
// the same promise.
//
// Failure modes:
//   • Backend reachable but hub start fails → logged + retried with
//     a fresh promise the next time `getHubConnection()` is called.
//   • Backend unreachable → caller observes a rejected promise; UI
//     should fall back to the cached profile / a graceful empty list.

import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';

let connection: HubConnection | null = null;
let startPromise: Promise<HubConnection> | null = null;
const connectListeners = new Set<(c: HubConnection) => void>();

function hubUrl(): string {
  // SignalR negotiates a transport URL relative to the page origin.
  // The backend serves /hubs/changsha (see Program.cs:174) which is
  // co-located with the static frontend bundle.
  //
  // Dev mode (`vite serve`, port 5173 by default): we now rely on
  // the Vite dev-server proxy (see vite.config.ts `server.proxy`)
  // to forward `/hubs/changsha` (HTTP + WebSocket) to the backend
  // at http://localhost:5000.  Same-origin defaults work in dev
  // and production without any URL gymnastics.  The legacy
  // `?hub=<url>` override is kept for contributors who need to
  // point at a remote backend (e.g. an in-cluster preview env).
  const params = new URLSearchParams(window.location.search);
  const override = params.get('hub');
  if (override !== null && override !== '') return override;

  // Same-origin in every mode now — the Vite dev proxy (W8) makes
  // this work in `vite serve`, the production build co-locates
  // hub + bundle at the same origin.
  return '/hubs/changsha';
}

function buildConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(hubUrl())
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();
}

/**
 * Returns the singleton SignalR connection, starting it on first
 * call.  Concurrent callers share the same start promise.  If the
 * previous start failed, retries with a fresh attempt.
 */
export async function getHubConnection(): Promise<HubConnection> {
  if (connection !== null && connection.state === HubConnectionState.Connected) {
    return connection;
  }
  if (startPromise !== null) return startPromise;

  if (connection === null) {
    connection = buildConnection();
  }
  const local = connection;

  startPromise = local.start()
    .then(() => {
      for (const cb of connectListeners) {
        try { cb(local); } catch { /* ignore listener errors */ }
      }
      return local;
    })
    .catch((err: unknown) => {
      // Reset so the next caller gets a fresh attempt.
      startPromise = null;
      connection = null;
      throw err;
    });
  return startPromise;
}

/**
 * Subscribe to (re-)connect events.  Fires every time the hub
 * transitions into the `Connected` state — i.e. on initial start and
 * after every automatic reconnect.
 */
export function onHubConnected(callback: (c: HubConnection) => void): () => void {
  connectListeners.add(callback);
  if (connection !== null && connection.state === HubConnectionState.Connected) {
    try { callback(connection); } catch { /* ignore */ }
  }
  return () => { connectListeners.delete(callback); };
}

/** True when the hub is currently in the Connected state. */
export function hubIsConnected(): boolean {
  return connection !== null && connection.state === HubConnectionState.Connected;
}

/**
 * Convenience: invoke a hub method, awaiting connection first.
 * Returns whatever the server method returns.
 */
export async function invokeHub<T = unknown>(method: string, ...args: unknown[]): Promise<T> {
  const conn = await getHubConnection();
  return conn.invoke<T>(method, ...args);
}

/**
 * Tears down the hub connection.  Used by the Client `disconnect`
 * teardown so a fresh `connect` cycle gets a fresh hub.
 */
export async function stopHubConnection(): Promise<void> {
  if (connection === null) return;
  const local = connection;
  connection = null;
  startPromise = null;
  try { await local.stop(); } catch { /* ignore */ }
}
