import 'bootstrap/dist/js/bootstrap';
import { AssetLoader } from './asset-loader';
import { Game } from './game';
import { initLobby } from './lobby';
import { MoveLog } from './move-log';
import { Client } from './client';
import { loadPatternOrderingFromApi } from './game-ui';
import * as three from 'three';

const assetLoader = new AssetLoader();

// Phase G — wire the lobby panel as soon as the script runs.  The lobby is
// independent of the Game lifecycle (it only reads URL params and reloads)
// so the user can adjust settings while assets stream in.
initLobby();

// Phase J Wave 3 — fire-and-forget fetch of Bishop's canonical pattern
// display order (GET /api/changsha/pattern-ordering).  Resolves before
// the first win in practice (the request hits during asset load + first
// connect); on failure the hardcoded fallback in game-ui.ts keeps
// rendering correctly.
void loadPatternOrderingFromApi();

assetLoader.loadAll().then(() => {
  const game = new Game(assetLoader);
  // for debugging
  Object.assign(window, {game, three});
  game.start();

  // Phase I Wave 1 — streaming move-log sidebar.  Mounts into the
  // <aside id="move-log"> placeholder in index.html and subscribes to the
  // existing client collections (match/dice/things/sound/claim/pickup/
  // result).  Client is private on Game, but TypeScript private is purely
  // a compile-time guard; we widen the type so we can hand the same Client
  // singleton to MoveLog without copying it.
  const client = (game as unknown as { client: Client }).client;
  new MoveLog(client).start();
});

