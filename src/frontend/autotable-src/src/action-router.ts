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

const SUPPORTED_ACTIONS = new Set([
  'new-game', 'spectate', 'tournament', 'tournaments',
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
 */
function dispatchSpectate(): void {
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
    default:
      return false;
  }
}
