// ---------------------------------------------------------------------
// Phase K Wave 23 — Hicks (Frontend) — lobby public-games pane chunk.
//
// Extracted from `lobby.ts` so the public-games list + make-public
// toggle ship as a lazy `lobby-public-games-pane.<hash>.js` chunk.
// The W23 bundle audit (§3.8 of `docs/lh13-soft-pin-rationale.md`)
// tightened the autotable-src-eager ceiling to 95 KiB; moving the
// ~4 KB of public-games DOM/RPC plumbing off the eager hot path
// keeps the lobby cold load under that ceiling.
//
// The two `install…` entries here are activation-surface gated by
// `schedule…LazyMount()` helpers in `lobby.ts`, mirroring the W22
// patterns for matchmaking / leaderboard / profile-page.  This
// module only imports types from `./matchmaking` for the typeof
// `MatchmakingModule` parameter — the actual matchmaking handle is
// passed in from the caller so the dynamic-import cache lives in
// lobby.ts (no duplicate cache, no double-import).
// ---------------------------------------------------------------------

import type * as MatchmakingModule from './matchmaking';
import type { PublicGame } from './matchmaking';
import { showEl, hideEl } from './dom-utils';

/**
 * Mount the lobby public-games list pane (heading + scrollable list +
 * "Join Random" button).  Re-renders on every onUpdate tick from
 * matchmaking.ts (≤ 5 s).  Side-effects only — returns void.
 *
 * Caller passes the loaded matchmaking module so we don't re-import
 * (the lobby's `loadMatchmaking()` cache stays the single source of
 * truth).
 */
export function installPublicGamesPane(mm: typeof MatchmakingModule): void {
  const listEl = document.getElementById('lobby-public-games-list');
  const emptyEl = document.getElementById('lobby-public-games-empty');
  const errorEl = document.getElementById('lobby-public-games-error');
  const joinRandomBtn = document.getElementById(
    'lobby-join-random') as HTMLButtonElement | null;
  if (listEl === null || emptyEl === null
      || errorEl === null || joinRandomBtn === null) {
    return;
  }

  const render = (): void => {
    const games = mm.getCachedGames();
    const err = mm.getLastError();
    if (err !== null) {
      showEl(errorEl);
      errorEl.textContent = `Failed to load public games: ${err}`;
    } else {
      hideEl(errorEl);
      errorEl.textContent = '';
    }
    listEl.replaceChildren();
    if (games.length === 0) {
      showEl(emptyEl);
      return;
    }
    hideEl(emptyEl);
    const cap = Math.min(games.length, 50);
    for (let i = 0; i < cap; i++) {
      listEl.appendChild(buildPublicGameCard(games[i], i, mm.navigateToGame));
    }
  };

  mm.onUpdate(render);
  render();

  joinRandomBtn.addEventListener('click', async () => {
    joinRandomBtn.disabled = true;
    const prevLabel = joinRandomBtn.textContent;
    joinRandomBtn.textContent = 'Joining…';
    try {
      const variant = readUrlVariant();
      const result = await mm.joinRandom(variant);
      if (result !== null) {
        mm.navigateToGame(result.gameId, result.seatIndex);
      } else {
        showEl(errorEl);
        errorEl.textContent = 'No public games with free seats right now.';
      }
    } catch (e) {
      showEl(errorEl);
      const msg = e instanceof Error ? e.message : String(e);
      errorEl.textContent = `Join Random failed: ${msg}`;
    } finally {
      joinRandomBtn.disabled = false;
      joinRandomBtn.textContent = prevLabel ?? '🎲 Join Random';
    }
    void mm.refresh();
  });
}

function buildPublicGameCard(
  game: PublicGame,
  index: number,
  navigate: (gameId: string, seatIndex?: number) => void,
): HTMLElement {
  const card = document.createElement('div');
  card.className = 'public-game-card';
  card.setAttribute('role', 'listitem');
  card.setAttribute('data-testid', `lobby-public-game-${index}`);
  card.setAttribute('data-game-id', game.gameId);
  const full = game.seatedCount >= game.maxSeats;
  if (full) card.classList.add('public-game-card-full');

  const name = document.createElement('div');
  name.className = 'public-game-card-name';
  name.setAttribute('data-testid', `lobby-public-game-name-${index}`);
  name.textContent = game.publicName !== null && game.publicName !== ''
    ? game.publicName
    : `${game.creatorDisplayName}'s game`;

  const meta = document.createElement('div');
  meta.className = 'public-game-card-meta';
  const creator = document.createElement('span');
  creator.className = 'public-game-card-meta-creator';
  creator.setAttribute('data-testid', `lobby-public-game-host-${index}`);
  creator.textContent = `Host: ${game.creatorDisplayName}`;
  const seats = document.createElement('span');
  seats.className = 'public-game-card-meta-seats';
  seats.setAttribute('data-testid', `lobby-public-game-seats-${index}`);
  if (full) seats.classList.add('seats-full');
  seats.textContent = `${game.seatedCount} / ${game.maxSeats}`;
  meta.appendChild(creator);
  meta.appendChild(seats);
  if (game.variant !== null && game.variant !== '') {
    const variant = document.createElement('span');
    variant.className = 'public-game-card-meta-variant';
    variant.textContent = `Variant: ${game.variant}`;
    meta.appendChild(variant);
  }

  const join = document.createElement('button');
  join.type = 'button';
  join.className = 'btn btn-primary btn-sm public-game-card-join';
  join.setAttribute('data-testid', `lobby-public-game-join-${index}`);
  join.textContent = full ? 'Full' : 'Join';
  join.disabled = full;
  join.addEventListener('click', () => {
    if (full) return;
    navigate(game.gameId);
  });

  card.appendChild(name);
  card.appendChild(meta);
  card.appendChild(join);
  return card;
}

function readUrlVariant(): string | undefined {
  const params = new URLSearchParams(window.location.search);
  const v = params.get('variant');
  return v !== null && v !== '' ? v : undefined;
}

function currentGameId(): string | null {
  const params = new URLSearchParams(window.location.search);
  const g = params.get('game');
  if (g === null || g === '') return null;
  return g;
}

/**
 * Mount the lobby "Make my game public" toggle + name input pair.
 * Activates only when the URL has a `game=…` parameter (i.e. the
 * user is in a live game).  Otherwise the controls stay disabled
 * with a descriptive status message.
 */
export function installMakePublicToggle(mm: typeof MatchmakingModule): void {
  const toggle = document.getElementById(
    'lobby-make-public-toggle') as HTMLInputElement | null;
  const nameInput = document.getElementById(
    'lobby-make-public-name') as HTMLInputElement | null;
  const statusEl = document.getElementById('lobby-make-public-status');
  if (toggle === null || nameInput === null || statusEl === null) return;

  const setStatus = (msg: string, isError: boolean): void => {
    statusEl.textContent = msg;
    statusEl.classList.toggle('lobby-make-public-status-error', isError);
  };

  const sync = async (): Promise<void> => {
    const gameId = currentGameId();
    if (gameId === null) {
      setStatus('Not in a live game.', true);
      toggle.checked = false;
      toggle.disabled = true;
      nameInput.disabled = true;
      return;
    }
    toggle.disabled = true;
    nameInput.disabled = !toggle.checked;
    const publicName = toggle.checked && nameInput.value.trim() !== ''
      ? nameInput.value.trim()
      : undefined;
    setStatus(toggle.checked ? 'Publishing…' : 'Unlisting…', false);
    try {
      const result = await mm.setGamePublic(
        { gameId, isPublic: toggle.checked, publicName });
      if (result.success) {
        setStatus(
          result.isPublic
            ? (result.publicName !== null && result.publicName !== ''
                ? `Listed as "${result.publicName}".`
                : 'Listed in the public lobby.')
            : 'Unlisted from the public lobby.',
          false);
      } else {
        setStatus('Server rejected the change.', true);
        toggle.checked = !toggle.checked;
      }
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setStatus(`Failed: ${msg}`, true);
      toggle.checked = !toggle.checked;
    } finally {
      toggle.disabled = false;
      nameInput.disabled = !toggle.checked;
    }
  };

  toggle.addEventListener('change', () => { void sync(); });
  nameInput.addEventListener('blur', () => {
    if (toggle.checked) void sync();
  });

  if (currentGameId() === null) {
    toggle.disabled = true;
    nameInput.disabled = true;
    setStatus('Start or join a game to publish it.', false);
  } else {
    nameInput.disabled = !toggle.checked;
    setStatus('', false);
  }
}
