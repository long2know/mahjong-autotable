// Phase K Wave 11 — `?action=*` deep-link routing for PWA shortcuts.
//
// The W10 manifest declares three shortcut entries that dispatch users
// at the autotable origin with a deep-link query parameter:
//
//   `/?action=new-game`   → start a new mahjong table
//   `/?action=spectate`   → open the spectator livestream picker
//   `/?action=tournament` → activate the tournament dashboard
//
// (The W10 manifest's `tournaments` plural alias is also honoured for
// backwards compatibility with any installed PWAs that picked it up
// before the W11 manifest rewrite landed.)
//
// Phase K Wave 12 adds a fourth keyword:
//
//   `/?action=replay&replayId=<guid>` → fetch + open the replay viewer
//
// Phase K Wave 13 extends `?action=spectate` with a `gameId` co-param
// that routes the spectator directly into a known game via Bishop's
// W12 handoff token endpoint:
//
//   `/?action=spectate&gameId=<guid>`
//     → POST /api/spectator/handoff
//     → on 200: navigate to `/spectate/{gameId}?token=<jwt>` + bootstrap
//       the spectator-livestream viewer for the game
//     → on 401: redirect to the lobby root (sign-in modal lives there)
//     → on 404 / any other error: "Game not found" toast
//
// The bare `?action=spectate` (no gameId) keeps the W11 / W12 lobby-
// tab fallback so the PWA shortcut still works as a "pick a game to
// watch" launcher when no specific game is targeted.
//
// Bishop's W12 backend lane ships `GET /api/replays/{replayId}` (the
// new id-addressable replay endpoint, alongside the existing
// `GET /api/games/{gameId}/replay`).  This router intercepts the
// `?action=replay` URL, fetches the replay payload, navigates to
// `/replay/{replayId}`, and bootstraps the in-page replay viewer.
// 404 → user-facing error toast ("Replay not found").
//
// Without this router, the W2 boot guard in `index.ts` treats any
// non-empty `window.location.search` as a game-bootstrap trigger,
// imports the heavy renderer chunk, and tries to enter a non-existent
// table — yielding a console error and a black canvas.  The router
// intercepts the action keyword first, strips it from the URL via
// `history.replaceState` (so a refresh doesn't repeatedly re-fire the
// shortcut), routes the side-effect, and returns `true` to tell the
// caller to skip the game-bootstrap import.
//
// Hard-asserted by Vasquez's W11 spec — wiring lives here, the spec
// exercises the producer side via `?action=*` URL probes.  See
// `docs/frontend-routing.md` for the contract.
//
// Producer contract:
//   • `new-game`   → clicks `[data-action="new-game"]` once the DOM
//                    is ready (lobby's "New game" button has been
//                    annotated with the data-action selector in W11).
//   • `spectate`   → rewrites the URL to `/spectate`, then activates
//                    the lobby's public-games tab as the spectatable-
//                    games catalogue.  (A future wave can swap to a
//                    dedicated `/spectate` route when the SPA router
//                    lands; W11 keeps the visible page the lobby so
//                    users see something on cold launch.)
//   • `tournament` → rewrites the URL to `/tournament/list`, then
//                    activates the lobby's tournaments tab.
//   • `replay`     → reads `replayId=<guid>` co-param, fetches
//                    `/api/replays/{replayId}` (Bishop W12), rewrites
//                    the URL to `/replay/{replayId}`, and hands the
//                    payload to `openReplayForGame()`.  Missing /
//                    malformed replayId → "Replay not found" toast.

const SUPPORTED_ACTIONS = new Set([
  'new-game', 'spectate', 'tournament', 'tournaments', 'replay',
]);

/**
 * Parse `?action=*` from the current URL.  Returns the normalised
 * action keyword or `null` if no recognized action is present.  Does
 * NOT modify the URL; callers strip the param via `clearActionParam()`
 * after dispatch so re-loads don't re-trigger the shortcut.
 */
export function parseActionFromUrl(): string | null {
  const params = new URLSearchParams(window.location.search);
  const raw = params.get('action');
  if (raw === null) return null;
  const norm = raw.trim().toLowerCase();
  if (!SUPPORTED_ACTIONS.has(norm)) return null;
  // Normalise the `tournaments` plural alias from the W10 manifest to
  // the W11 canonical `tournament` keyword so downstream code only
  // sees one form.
  return norm === 'tournaments' ? 'tournament' : norm;
}

/**
 * Remove the `?action=*` query parameter from the URL without
 * triggering a page reload.  Preserves any other query parameters the
 * caller may have on the URL (e.g. UTM tags) and falls back to a
 * no-op if `history.replaceState` is unavailable.
 */
export function clearActionParam(): void {
  try {
    const url = new URL(window.location.href);
    url.searchParams.delete('action');
    const next = url.pathname + (url.searchParams.toString() === '' ? '' : `?${url.searchParams.toString()}`) + url.hash;
    window.history.replaceState(window.history.state, '', next);
  } catch {
    /* legacy browsers without history API — leave URL intact */
  }
}

/**
 * Click `[data-action="new-game"]` once the DOM and the lobby's
 * tab-wiring are ready.  Idempotent — guarded by a single-fire flag.
 */
function dispatchNewGame(): void {
  const click = (): boolean => {
    const btn = document.querySelector<HTMLElement>('[data-action="new-game"]');
    if (btn === null) return false;
    btn.click();
    return true;
  };
  if (document.readyState === 'complete' || document.readyState === 'interactive') {
    if (click()) return;
  }
  // The button is rendered eagerly in `index.html`, but defensively
  // wait for DOMContentLoaded if we beat the parse.
  document.addEventListener('DOMContentLoaded', () => {
    // Best-effort retry — give the lobby tabs one microtask to bind
    // their event handlers before we synthesise the click.
    window.setTimeout(() => { click(); }, 0);
  }, { once: true });
}

/**
 * Rewrite the URL to `/spectate` (no reload) and activate the lobby's
 * public-games tab as the spectatable-games catalogue.  The full
 * spectate-by-id flow still lives at `#/spectate/{tableId}` (W6 hash
 * route).  This shortcut takes a user from a cold PWA launch into a
 * "pick a table to watch" state.
 *
 * Phase K Wave 13 — when a `gameId=<guid>` co-param is present, the
 * dispatch delegates to `dispatchSpectateWithGameId()` which mints
 * Bishop's W12 handoff JWT and routes directly into the spectator
 * viewer for that game.  The bare `?action=spectate` form keeps the
 * W11 / W12 lobby-tab fallback.
 */
function dispatchSpectate(): void {
  const params = new URLSearchParams(window.location.search);
  const rawGameId = (params.get('gameId') ?? '').trim();
  if (rawGameId !== '') {
    dispatchSpectateWithGameId(rawGameId);
    return;
  }

  try {
    const url = new URL(window.location.href);
    url.pathname = '/spectate';
    url.searchParams.delete('action');
    window.history.replaceState(window.history.state, '', url.pathname + url.search + url.hash);
  } catch {
    /* ignore */
  }
  const activate = (): boolean => {
    const tab = document.getElementById('lobby-public-games-tab') as HTMLButtonElement | null;
    if (tab === null) return false;
    tab.click();
    return true;
  };
  if (!activate()) {
    document.addEventListener('DOMContentLoaded', () => {
      window.setTimeout(() => { activate(); }, 0);
    }, { once: true });
  }
}

/**
 * Phase K Wave 13 — `?action=spectate&gameId=<guid>` dispatch.
 *
 * Mints Bishop's W12 short-lived spectator-handoff JWT (5-minute TTL,
 * scope `spectator:<gameId>`) via `POST /api/spectator/handoff`, then
 * navigates to the canonical `/spectate/{gameId}?token=<jwt>` URL and
 * bootstraps the existing W6 spectator-livestream viewer by setting
 * `window.location.hash = '#/spectate/<gameId>'` (the viewer's
 * `installSpectatorRoute()` hashchange listener owns the mount).
 *
 * The token is kept on the query-string so a refresh / share-link
 * round-trip carries the spectator credential along; a future wave
 * can extend `openSpectatorLivestream()` to read the token from the
 * URL and append `?token=<jwt>` to the HLS playlist fetch when the
 * caller doesn't have a session cookie (mobile webview / embedded
 * native client paths).  For W13 the existing cookie-auth playback
 * still works because the caller already has a session (the handoff
 * endpoint required one to mint the token).
 *
 * Failure modes:
 *   • 401 (no session) → navigate to lobby root (`/`); the sign-in
 *     modal is mounted at boot and the user can authenticate there.
 *   • 404 / 5xx / network error → "Game not found" toast.  We
 *     deliberately don't differentiate; the user-visible string is
 *     the same.
 *   • Malformed JSON response → same "Game not found" toast.
 *
 * The fetch + toast modules are lazy-imported to keep the
 * `?action=spectate&gameId=…` cold-launch off the eager lobby chunk.
 */
function dispatchSpectateWithGameId(gameId: string): void {
  // Strip the action + gameId params from the URL synchronously
  // before the network round-trip so a refresh during the fetch
  // doesn't re-fire the shortcut.  The path rewrite below (in the
  // success branch) will set the canonical `/spectate/{gameId}?token=…`.
  try {
    const url = new URL(window.location.href);
    url.searchParams.delete('action');
    url.searchParams.delete('gameId');
    window.history.replaceState(
      window.history.state,
      '',
      url.pathname + (url.searchParams.toString() === '' ? '' : `?${url.searchParams.toString()}`) + url.hash,
    );
  } catch {
    /* legacy browsers — best effort */
  }

  void fetchHandoffAndOpenSpectator(gameId);
}

async function fetchHandoffAndOpenSpectator(gameId: string): Promise<void> {
  let resp: Response;
  try {
    resp = await fetch(
      '/api/spectator/handoff',
      {
        method: 'POST',
        credentials: 'same-origin',
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json',
        },
        body: JSON.stringify({ gameId }),
      },
    );
  } catch {
    await showGameNotFoundToast();
    return;
  }

  if (resp.status === 401) {
    redirectToLobbyForSignIn();
    return;
  }

  if (!resp.ok) {
    await showGameNotFoundToast();
    return;
  }

  let body: { token?: unknown; expiresAt?: unknown } | null = null;
  try {
    body = await resp.json() as { token?: unknown; expiresAt?: unknown };
  } catch {
    await showGameNotFoundToast();
    return;
  }

  const token = typeof body?.token === 'string' ? body.token : '';
  if (token === '') {
    await showGameNotFoundToast();
    return;
  }

  // Rewrite the URL to `/spectate/{gameId}?token=<jwt>` and set the
  // hash to `#/spectate/{gameId}` so the W6 hashchange listener in
  // `spectator-livestream.installSpectatorRoute()` mounts the
  // viewer.  The token sits on the query-string for share-link
  // symmetry; the existing viewer ignores it (W13) but the URL is
  // the canonical reference for a spectator session.
  const encodedId = encodeURIComponent(gameId);
  const encodedToken = encodeURIComponent(token);
  const canonicalPath = `/spectate/${encodedId}`;
  const canonicalQuery = `?token=${encodedToken}`;
  const canonicalHash = `#/spectate/${encodedId}`;
  try {
    window.history.replaceState(
      window.history.state,
      '',
      canonicalPath + canonicalQuery + canonicalHash,
    );
  } catch {
    /* ignore */
  }

  // Bootstrap the spectator viewer.  The existing `installSpectator
  // Route()` fires its hashchange handler on install + on hash
  // change; calling `openSpectatorLivestream()` directly is the
  // most reliable path because the `replaceState()` call above does
  // not emit a `hashchange` event when the hash component is added
  // alongside a path rewrite.
  try {
    const mod = await import('./spectator-livestream');
    mod.installSpectatorRoute();
    await mod.openSpectatorLivestream({ tableId: gameId });
  } catch {
    await showGameNotFoundToast();
  }
}

function redirectToLobbyForSignIn(): void {
  // No dedicated `/login` route exists in the SPA — the sign-in
  // modal is mounted at boot under `installAuthUi()` and lives at
  // the lobby root.  Send the user there so they can authenticate
  // and re-try the spectator deep-link.
  try {
    const url = new URL(window.location.href);
    url.pathname = '/';
    url.search = '';
    url.hash = '';
    window.location.replace(url.toString());
  } catch {
    window.location.href = '/';
  }
}

async function showGameNotFoundToast(): Promise<void> {
  try {
    const { showToast } = await import('./toast');
    showToast('Game not found', 'error');
  } catch {
    // Toast module failed to load — fall back to console so the
    // failure isn't completely silent.  Production builds keep the
    // toast chunk eagerly available so this branch is exotic.
    // eslint-disable-next-line no-console
    console.warn('[action-router] spectator game not found, toast unavailable');
  }
}

/**
 * Rewrite the URL to `/tournament/list` (no reload) and activate the
 * lobby's tournaments tab.  The tournament module is W10-lazy-loaded
 * on tab click, so this routes the user into the bracket dashboard
 * without an extra interaction.
 */
function dispatchTournament(): void {
  try {
    const url = new URL(window.location.href);
    url.pathname = '/tournament/list';
    url.searchParams.delete('action');
    window.history.replaceState(window.history.state, '', url.pathname + url.search + url.hash);
  } catch {
    /* ignore */
  }
  const activate = (): boolean => {
    const tab = document.getElementById('lobby-tournaments-tab') as HTMLButtonElement | null;
    if (tab === null) return false;
    tab.click();
    return true;
  };
  if (!activate()) {
    document.addEventListener('DOMContentLoaded', () => {
      window.setTimeout(() => { activate(); }, 0);
    }, { once: true });
  }
}

/**
 * Phase K Wave 12 — `?action=replay&replayId=<guid>` dispatch.
 *
 * Reads the `replayId` co-param, fetches the replay payload from
 * Bishop's W12 `GET /api/replays/{replayId}` endpoint, rewrites the
 * URL to the canonical `/replay/{replayId}` path, and hands the
 * payload to the existing `openReplayForGame()` launcher (which
 * targets the in-page 2D replay viewer registered by `game-ui.ts`).
 *
 * Failure modes:
 *   • Missing / empty `replayId` co-param → "Replay not found" toast,
 *     URL param stripped, user lands on the bare lobby.
 *   • Endpoint 404 / network error → same toast.  We do NOT fall
 *     back to the legacy `GET /api/games/{gameId}/replay` (the W12
 *     contract is id-addressable; falling back to the game-id form
 *     would silently hide configuration drift).
 *   • Endpoint 200 → URL rewritten to `/replay/{replayId}`,
 *     `openReplayForGame()` invoked with the normalised payload.
 *
 * Both fetch + viewer wiring are lazy-imported to keep the
 * `?action=replay` cold-launch off the eager lobby chunk.  The
 * replay-launcher + toast modules are W3-/W7-already-emitted lazy
 * chunks shared with other surfaces (post-game modal, leaderboard
 * row, settings drawer), so no new chunk graph is added here.
 */
function dispatchReplay(): void {
  const params = new URLSearchParams(window.location.search);
  const rawId = (params.get('replayId') ?? '').trim();

  if (rawId === '') {
    clearActionParam();
    void showReplayNotFoundToast();
    return;
  }

  // Strip the action + replayId params from the URL synchronously
  // before the network round-trip so a refresh during the fetch
  // doesn't re-fire the shortcut.  The path rewrite below (in the
  // success branch) will set the canonical `/replay/{id}` URL.
  const replayId = rawId;
  try {
    const url = new URL(window.location.href);
    url.searchParams.delete('action');
    url.searchParams.delete('replayId');
    window.history.replaceState(
      window.history.state,
      '',
      url.pathname + (url.searchParams.toString() === '' ? '' : `?${url.searchParams.toString()}`) + url.hash,
    );
  } catch {
    /* legacy browsers — best effort */
  }

  void fetchAndOpenReplay(replayId);
}

async function fetchAndOpenReplay(replayId: string): Promise<void> {
  let resp: Response;
  try {
    resp = await fetch(
      `/api/replays/${encodeURIComponent(replayId)}`,
      {
        credentials: 'same-origin',
        headers: { 'Accept': 'application/json' },
      },
    );
  } catch {
    await showReplayNotFoundToast();
    return;
  }

  if (!resp.ok) {
    // 404 (replay missing) or 5xx (transient backend) — surface the
    // same toast.  We deliberately don't differentiate 404 vs 5xx in
    // the user-visible string; both are "we couldn't load this".
    await showReplayNotFoundToast();
    return;
  }

  let body: unknown;
  try {
    body = await resp.json();
  } catch {
    await showReplayNotFoundToast();
    return;
  }

  // Rewrite the URL to the canonical `/replay/{replayId}` so back/
  // forward + share-link symmetry land at a clean path post-dispatch.
  try {
    const url = new URL(window.location.href);
    url.pathname = `/replay/${encodeURIComponent(replayId)}`;
    window.history.replaceState(window.history.state, '', url.pathname + url.search + url.hash);
  } catch {
    /* ignore */
  }

  // Hand the payload to the existing replay launcher.  The launcher
  // module owns the viewer wiring (`registerReplayLauncher` is called
  // by game-ui.ts at boot); we just normalise the wire shape into
  // the launcher's expected interface and dispatch.
  try {
    const { openReplayPayload } = await import('./replay-launcher');
    openReplayPayload(replayId, body);
  } catch {
    await showReplayNotFoundToast();
  }
}

async function showReplayNotFoundToast(): Promise<void> {
  try {
    const { showToast } = await import('./toast');
    showToast('Replay not found', 'error');
  } catch {
    // Toast module failed to load — fall back to console so the
    // failure isn't completely silent.  Production builds keep the
    // toast chunk eagerly available so this branch is exotic.
    // eslint-disable-next-line no-console
    console.warn('[action-router] replay not found, toast unavailable');
  }
}

/**
 * Top-level entry point — call this once at boot before the game-
 * bootstrap import guard fires.  Returns `true` if a recognized
 * action was handled (caller should skip the game-bootstrap import);
 * returns `false` for the no-action / unrecognized-action case.
 */
export function handlePwaActionFromUrl(): boolean {
  const action = parseActionFromUrl();
  if (action === null) return false;

  switch (action) {
    case 'new-game':
      clearActionParam();
      dispatchNewGame();
      return true;
    case 'spectate':
      dispatchSpectate();
      return true;
    case 'tournament':
      dispatchTournament();
      return true;
    case 'replay':
      dispatchReplay();
      return true;
    default:
      return false;
  }
}
