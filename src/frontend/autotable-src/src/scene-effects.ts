// Phase K Wave 4 — Deferred scene effects chunk.
//
// Wave 3 packed three.js + AssetLoader + Game + World + ClientUi +
// MoveLog + the full ~100 kB `GameUi` modal/settings/replay graph
// into a single 922 kB `scene.<hash>.js` chunk that the user paid
// for before they saw their first tile.  Wave 4 splits that into:
//
//   • `scene-shell.<hash>.js`  ← three.js + AssetLoader + Game
//     (minus GameUi/MoveLog) + ClientUi + MainView.  Mints
//     `scene-shell-ready` after the first WebGL frame composites.
//   • `scene-effects.<hash>.js` (this file) ← GameUi + MoveLog +
//     the rest of the heavy DOM glue.  Dynamic-imported by the
//     shell after `scene-shell-ready` fires so the renderer is
//     interactive while these chunks stream in parallel with the
//     tile-texture round-trips.  Mints `scene-effects-ready` once
//     the heavy UI is wired in.
//
// Surface contract (consumed by `scene-shell.ts`):
//   • `mountEffects(game, client)` — installs `GameUi`, mounts the
//     `MoveLog` sidebar, and emits the `scene-effects-ready`
//     marker.  Idempotent — re-entry resolves to the existing
//     install rather than building duplicate listeners.

import { GameUi } from './game-ui';
import { MoveLog } from './move-log';
import type { Game } from './game';
import type { Client } from './client';

let mounted = false;

export async function mountEffects(game: Game, client: Client): Promise<void> {
  if (mounted) {
    markEffectsReady();
    return;
  }
  mounted = true;

  // Phase K Wave 4 — Install the heavy GameUi (result modal, settings
  // drawer, replay viewer, claim window).  Constructor side-effects
  // wire all the DOM listeners; we don't need to hold a reference
  // here once it's mounted.
  game.installGameUi(GameUi);

  // Phase K Wave 4 — Streaming move-log sidebar.  Wave 3 mounted this
  // inside the renderer-critical `scene` chunk; it pulled
  // pattern-utils which previously lived in `game-ui.ts` — both have
  // been demoted to the effects chunk in Wave 4.
  new MoveLog(client).start();

  markEffectsReady();
}

function markEffectsReady(): void {
  if (document.body.getAttribute('data-scene-effects-ready') === 'true') return;
  document.body.setAttribute('data-scene-effects-ready', 'true');
  const marker = document.createElement('div');
  marker.setAttribute('data-testid', 'scene-effects-ready');
  marker.setAttribute('aria-hidden', 'true');
  marker.style.display = 'none';
  document.body.appendChild(marker);
  window.dispatchEvent(new CustomEvent('mahjong:scene-effects-ready'));
}
