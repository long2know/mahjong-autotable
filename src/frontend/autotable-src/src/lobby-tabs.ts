// Phase K Wave 23 — Hicks (Frontend).
//
// Lazy-loaded extraction of `installLobbyTabs()` from `./lobby.ts`.
// The lobby's eager bundle was at the §3.8 ≤ 95 KiB ceiling
// after W22; pulling this ~80-LoC function (plus its switch
// helper) into its own chunk drops it out of the eager graph
// without changing any user-visible behaviour.
//
// Why a dependency injection seam: `installLobbyTabs` consults
// the lobby module's `loadMatchmaking` / `_matchmakingMod` and
// `loadLeaderboard` / `_leaderboardMod` private state to decide
// whether to start / stop polling on tab activation.  We can't
// re-import those module-level closures from this lazy chunk
// without re-creating the polling-graph (which would defeat the
// caching).  Solution: the lobby passes those accessors in as a
// `LobbyTabsDeps` parameter; the lazy module remains a pure
// function over the deps + the DOM.

import { setElHidden } from './dom-utils';

export interface MatchmakingPollHandle {
  isPolling(): boolean;
  startPolling(): void;
  stopPolling(): void;
}

export interface LeaderboardPollHandle {
  startLeaderboardPolling(): Promise<void> | void;
  stopLeaderboardPolling(): void;
}

export interface LobbyTabsDeps {
  /** Lazy-load (or return cached) matchmaking module.  Mirrors
   *  `./lobby.ts:loadMatchmaking` — the lobby owns the cache
   *  because the public-games pane + make-public toggle hit the
   *  same module. */
  loadMatchmaking(): Promise<MatchmakingPollHandle>;
  /** Returns the cached matchmaking module if already loaded,
   *  null otherwise.  Used to skip the import on tab-out when
   *  the module hasn't been touched yet. */
  getMatchmakingModule(): MatchmakingPollHandle | null;
  /** Lazy-load (or return cached) leaderboard module. */
  loadLeaderboard(): Promise<LeaderboardPollHandle>;
  /** Returns the cached leaderboard module if loaded, null otherwise. */
  getLeaderboardModule(): LeaderboardPollHandle | null;
}

/**
 * Install the lobby tab strip.  Idempotent — the lobby calls this
 * exactly once during `initLobby()`, but the function is safe to
 * call twice (the second call simply re-binds the click handlers,
 * which is a no-op when the elements are unchanged).
 *
 * Tab activation also starts/stops matchmaking polling so the REST
 * endpoint isn't hammered while the user is on the My-Game tab.
 *
 * Phase K Wave 23 — extracted from `./lobby.ts` to shrink the
 * eager bundle.  See module header for the DI rationale.
 */
export function installLobbyTabs(deps: LobbyTabsDeps): void {
  const myTab = document.getElementById(
    'lobby-my-game-tab') as HTMLButtonElement | null;
  const pubTab = document.getElementById(
    'lobby-public-games-tab') as HTMLButtonElement | null;
  const lbTab = document.getElementById(
    'lobby-leaderboard-tab') as HTMLButtonElement | null;
  // Phase J Wave 10 — Tournaments tab.
  const tournTab = document.getElementById(
    'lobby-tournaments-tab') as HTMLButtonElement | null;
  const myPane = document.getElementById('lobby-tab-my-game');
  const pubPane = document.getElementById('lobby-tab-public-games');
  const lbPane = document.getElementById('lobby-tab-leaderboard');
  const tournPane = document.getElementById('lobby-tab-tournaments');
  if (myTab === null || pubTab === null
      || myPane === null || pubPane === null) {
    return;
  }

  const activate = (which: 'my' | 'public' | 'leaderboard' | 'tournaments'): void => {
    const isMy = which === 'my';
    const isPub = which === 'public';
    const isLb = which === 'leaderboard';
    const isTourn = which === 'tournaments';
    myTab.classList.toggle('lobby-tab-active', isMy);
    pubTab.classList.toggle('lobby-tab-active', isPub);
    if (lbTab !== null) lbTab.classList.toggle('lobby-tab-active', isLb);
    if (tournTab !== null) tournTab.classList.toggle('lobby-tab-active', isTourn);
    myTab.setAttribute('aria-selected', isMy ? 'true' : 'false');
    pubTab.setAttribute('aria-selected', isPub ? 'true' : 'false');
    if (lbTab !== null) lbTab.setAttribute('aria-selected', isLb ? 'true' : 'false');
    if (tournTab !== null) tournTab.setAttribute('aria-selected', isTourn ? 'true' : 'false');
    setElHidden(myPane, !isMy);
    setElHidden(pubPane, !isPub);
    if (lbPane !== null) setElHidden(lbPane, !isLb);
    if (tournPane !== null) setElHidden(tournPane, !isTourn);

    // Per-tab polling discipline — Apone's rate-limit budget is the
    // motivation here.  Each tab owns one timer; we tear the other
    // tabs' timers down on activate.
    if (isPub) {
      void deps.loadMatchmaking().then((mod) => {
        if (!mod.isPolling()) mod.startPolling();
      });
    } else {
      const mm = deps.getMatchmakingModule();
      if (mm !== null) mm.stopPolling();
    }
    if (isLb) {
      void deps.loadLeaderboard().then((m) => m.startLeaderboardPolling());
    } else {
      const lb = deps.getLeaderboardModule();
      if (lb !== null) lb.stopLeaderboardPolling();
    }
  };

  myTab.addEventListener('click', () => activate('my'));
  pubTab.addEventListener('click', () => activate('public'));
  if (lbTab !== null) {
    lbTab.addEventListener('click', () => activate('leaderboard'));
  }
  if (tournTab !== null) {
    tournTab.addEventListener('click', () => activate('tournaments'));
  }
  // Default: My Game pane visible.
  activate('my');
}
