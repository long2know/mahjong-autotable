// Phase K Wave 23 — Hicks (Frontend).
//
// Lazy-loaded extraction of `installLobbyStatsPanel()` +
// `renderLobbyStatsPanel()` + `installSoundEnabledMirror()` from
// `./lobby.ts`.  These three functions are wired during
// `initLobby()` but are not on the critical-paint path (the
// stats panel renders the local profile, which arrives via
// SignalR after the lobby first-paint; the sound mirror is a
// passive LS bookkeeper).  Pulling them into a lazy chunk
// shrinks the eager bundle past the §3.8 ≤ 95 KiB ceiling
// without changing user-visible behaviour beyond a one-frame
// deferral of the initial empty-state stats panel paint.
//
// Why an extracted file: same chunker discipline as
// `./lobby-tabs.ts` — dynamic-importing this module from
// `./lobby.ts` forces rollup to split it into a separate chunk;
// the eager bundle then loses the body of these three
// functions plus their transitive `stats.ts` formatter cost
// (the latter was already lazy at W19 but the trigger probe
// lived in the eager graph).
//
// Dependency injection: `installLobbyStatsPanel` needs access
// to the profile-cache `getProfile` accessor + the `onProfile`
// subscription helper.  Both live in `./profile.ts` which is
// already in the eager bundle, so we statically import them
// here (the import chain `lobby-stats-panel -> profile`
// re-collapses into this lazy chunk via rollup's chunker —
// only `profile`'s NET-new code lands here, which is zero).

import {
  getProfile,
  onProfile,
  type PlayerProfile,
} from './profile';

// ---------------------------------------------------------------------
// Stats panel.
//
// Renders the local player's career stats from the profile cache.
// Empty until the SignalR ProfileLoaded event fires; re-renders on
// every onProfile tick.  The Wave-19 stats formatter chunk is
// dynamic-imported on first populated render so the empty/loading
// state never pulls the chunk.
// ---------------------------------------------------------------------

let _statsFormatterPromise: Promise<typeof import('./stats')> | null = null;

async function loadStatsFormatter(): Promise<typeof import('./stats')> {
  if (_statsFormatterPromise === null) {
    _statsFormatterPromise = import('./stats');
  }
  return _statsFormatterPromise;
}

export function installLobbyStatsPanel(): void {
  renderLobbyStatsPanel();
  // Subscribe directly so updates after install propagate.
  onProfile(() => renderLobbyStatsPanel());
}

export function renderLobbyStatsPanel(): void {
  const host = document.getElementById('lobby-stats-panel');
  if (host === null) return;
  const profile: PlayerProfile | null = getProfile();
  host.replaceChildren();
  if (profile === null) {
    const empty = document.createElement('div');
    empty.className = 'lobby-stats-empty';
    empty.textContent = 'Loading your stats…';
    host.appendChild(empty);
    return;
  }
  // Lazy-load the stats formatter on first populated render.
  void loadStatsFormatter().then((mod) => {
    const fresh = getProfile();
    if (fresh === null) return;
    const currentHost = document.getElementById('lobby-stats-panel');
    if (currentHost === null) return;
    currentHost.replaceChildren();
    const panel = mod.formatStats(fresh.stats);
    const heading = document.createElement('div');
    heading.className = 'lobby-stats-panel-subject';
    heading.textContent = fresh.displayName;
    currentHost.appendChild(heading);
    currentHost.appendChild(panel);
  });
  // Provisional displayName heading shown while the formatter
  // chunk is in flight — keeps the panel from looking inert.
  const heading = document.createElement('div');
  heading.className = 'lobby-stats-panel-subject';
  heading.textContent = profile.displayName;
  host.appendChild(heading);
}

// ---------------------------------------------------------------------
// Sound-toggle localStorage mirror.
//
// Phase J Wave 6 wired a discoverable scalar LS key
// (`mahjong:soundEnabled`) that mirrors the sound knob in the
// Wave-3 settings drawer's JSON-encoded payload.  Mirror is one-
// way (settings → key) with two sync points: boot + change event.
// ---------------------------------------------------------------------

const LS_KEY_SOUND_ENABLED = 'mahjong:soundEnabled';
let soundMirrorInstalled = false;

export function installSoundEnabledMirror(): void {
  if (soundMirrorInstalled) return;
  const checkbox = document.getElementById(
    'settings-sound') as HTMLInputElement | null;
  if (checkbox === null) return;
  soundMirrorInstalled = true;

  const writeKey = (enabled: boolean): void => {
    try {
      window.localStorage.setItem(LS_KEY_SOUND_ENABLED, enabled ? 'true' : 'false');
    } catch {
      /* private mode / quota — skip */
    }
  };

  // Initial mirror.  The checkbox's `checked` property is the source
  // of truth — game-ui.ts seeds it from LS before user interaction.
  writeKey(checkbox.checked);

  checkbox.addEventListener('change', () => writeKey(checkbox.checked));

  // Safety net: re-mirror after the settings drawer toggles open
  // (programmatic state may have changed without firing 'change').
  const drawerToggle = document.getElementById('settings-toggle');
  if (drawerToggle !== null) {
    drawerToggle.addEventListener('click', () => {
      window.setTimeout(() => writeKey(checkbox.checked), 0);
    });
  }
}
