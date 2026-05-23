// Phase K Wave 3 — 3D scene + asset chunk.
//
// Wave 2 split `game-bootstrap` off the eager lobby graph but the
// renderer chunk was still 1.11 MB because Game / World / AssetLoader /
// three / move-log all lived inside `game-bootstrap.ts`.
//
// Wave 3 peels everything that depends on three.js (and the asset
// pipeline that pulls textures + GLB models) out into this module.
// `game-bootstrap.ts` keeps the lightweight shell + chat + voice
// wiring; the 3D scene is dynamic-imported from there once the WebGL
// canvas (#main) is in the DOM.
//
// Surface contract (consumed by `game-bootstrap.ts`):
//   • `mountScene()` boots `Game` + the live `Client`, runs the asset
//     loader, kicks off `MoveLog`, and notifies the lobby's deferred
//     `attachLobbyClient(client)` binding.  Resolves to the live
//     `Client` so chat / voice can subscribe.
//   • Sets `data-testid="game-scene-ready"` on `<body>` (and dispatches
//     `mahjong:game-scene-ready`) once the renderer has produced its
//     first frame — Vasquez gates its WebGL visual specs on this flag.
//   • Exposes `game` + `three` on `window` for the existing debug-only
//     callers (matches the legacy behaviour that lived in Wave-2
//     `game-bootstrap.ts`).

import { AssetLoader } from './asset-loader';
import { Game } from './game';
import { attachLobbyClient } from './lobby';
import { MoveLog } from './move-log';
import type { Client } from './client';
import { loadPatternOrderingFromApi } from './game-ui';
import * as three from 'three';

let mounted = false;

export async function mountScene(): Promise<Client> {
  if (mounted) {
    // Re-entry: return the already-bound client from window.
    const existing = (window as unknown as { __mahjongClient?: Client }).__mahjongClient;
    if (existing !== undefined) return existing;
  }
  mounted = true;

  // Phase J Wave 3 — Bishop's canonical pattern display order.  The
  // hardcoded fallback in game-ui.ts keeps things rendering if this
  // fetch fails, so it's safely fire-and-forget.
  void loadPatternOrderingFromApi();

  const assetLoader = new AssetLoader();
  await assetLoader.loadAll();

  const game = new Game(assetLoader);
  // for debugging — preserves the Wave-2 surface so anyone poking
  // window.game / window.three from devtools keeps working.
  Object.assign(window, { game, three });
  game.start();

  // Phase I Wave 1 — streaming move-log sidebar.  Client is private on
  // Game, but TypeScript private is purely a compile-time guard; we
  // widen the type so we can hand the same Client singleton to MoveLog
  // without copying it.
  const client = (game as unknown as { client: Client }).client;
  new MoveLog(client).start();

  // Phase J Wave 4 — wire the lobby's live player chip strip + seat
  // preview to the live Client collections now that Game.start() has
  // booted the Client.
  attachLobbyClient(client);

  // Cache the client on window so a second mountScene() call (e.g. an
  // E2E reload that fires through the same module instance) can
  // resolve without rebuilding the renderer.
  (window as unknown as { __mahjongClient?: Client }).__mahjongClient = client;

  // Phase K Wave 3 — Defer the scene-ready signal one rAF so the
  // first WebGL frame has actually composited; Vasquez's specs that
  // wait for `game-scene-ready` only need this guarantee, not a
  // texture-decoded round-trip.
  window.requestAnimationFrame(() => {
    document.body.setAttribute('data-game-scene-ready', 'true');
    const marker = document.createElement('div');
    marker.setAttribute('data-testid', 'game-scene-ready');
    marker.setAttribute('aria-hidden', 'true');
    marker.style.display = 'none';
    document.body.appendChild(marker);
    window.dispatchEvent(new CustomEvent('mahjong:game-scene-ready'));
  });

  return client;
}
