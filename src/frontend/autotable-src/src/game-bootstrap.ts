// Phase K Wave 2 — Game bootstrap.
//
// Owns the eager imports of `Game`, `World`, `Client`, `MoveLog`,
// `AssetLoader`, three.js, the chat panel, the in-game replay capture,
// and Bishop's pattern-ordering fetch.  This module is loaded LAZILY
// from `index.ts` only when the user has crossed the lobby boundary —
// in practice, when the URL carries any query string (the lobby always
// reloads the page with new search params via `window.location.replace`
// before transitioning to a table).
//
// Splitting these out of the eager bundle is the Phase K Wave 2 lobby
// budget win: the eager `autotable-src.<hash>.js` drops below the
// 500 kB ceiling because three.js + the renderer chain are deferred
// until they're actually needed.
//
// Trigger contract (from `index.ts`):
//   - Empty `window.location.search` → lobby-only; this module is not
//     fetched.  Hovering Quick Match / Apply triggers a preload so
//     the next page load (post-`location.replace`) has it cached.
//   - Non-empty `window.location.search` → user is entering a table;
//     this module is dynamic-imported and `bootstrapGame()` runs.
//
// The voice chat module is also wired in here because it depends on
// the live `Client` connection.  It's gated by `?voice=1` (or a
// future per-game `voiceEnabled` flag broadcast by Bishop's hub) so
// the WebRTC code only loads on tables where voice is opted in.

import 'bootstrap/dist/js/bootstrap';
import { AssetLoader } from './asset-loader';
import { Game } from './game';
import { attachLobbyClient } from './lobby';
import { MoveLog } from './move-log';
import { Client } from './client';
import { loadPatternOrderingFromApi } from './game-ui';
import * as three from 'three';

let booted = false;

export async function bootstrapGame(): Promise<void> {
  if (booted) return;
  booted = true;

  // Phase J Wave 3 — Bishop's canonical pattern display order.  The
  // hardcoded fallback in game-ui.ts keeps things rendering if this
  // fetch fails, so it's safely fire-and-forget.
  void loadPatternOrderingFromApi();

  const assetLoader = new AssetLoader();
  await assetLoader.loadAll();

  const game = new Game(assetLoader);
  // for debugging
  Object.assign(window, { game, three });
  game.start();

  // Phase I Wave 1 — streaming move-log sidebar.  Mounts into the
  // <aside id="move-log"> placeholder in index.html and subscribes to
  // the existing client collections.  Client is private on Game, but
  // TypeScript private is purely a compile-time guard; we widen the
  // type so we can hand the same Client singleton to MoveLog without
  // copying it.
  const client = (game as unknown as { client: Client }).client;
  new MoveLog(client).start();

  // Phase J Wave 4 — wire the lobby's live player chip strip + seat
  // preview to the live Client collections now that Game.start() has
  // booted the Client.  initLobby() in index.ts ran pre-asset-load so
  // the Quick Match button + URL-driven pickers were clickable
  // immediately; attachLobbyClient binds the live listeners on top of
  // the already-rendered panel.
  attachLobbyClient(client);

  // Phase K Wave 1 — Chat panel: only needed when the user is in a
  // game.  Lazy-import it so the lobby-only path doesn't pay the
  // bundle cost.  installChatPanel itself hides the panel when no
  // gameId is on the URL, but the import alone is ~tens of kB —
  // gate on URL inspection here to avoid loading at all.
  if (/[?&]gameId=/.test(window.location.search)) {
    void import('./chat').then(mod => mod.installChatPanel(client));
  }

  // Phase K Wave 2 — Voice chat: gated by `?voice=1` until Bishop's
  // hub broadcasts a per-game `voiceEnabled` flag.  When opt-in is
  // present, lazy-import the voice module — it self-mounts a mic
  // toggle + peer status rail and negotiates a WebRTC mesh.
  if (/[?&]voice=1\b/.test(window.location.search)) {
    void import('./voice').then(mod => mod.installVoicePanel(client));
  }
}

// Preload helper — wire to lobby Apply / Quick Match hover so the
// next page load (after location.replace) gets the chunk from cache.
export function preloadGameBootstrap(): void {
  void import('./game-bootstrap');
}
