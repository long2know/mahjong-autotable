import 'bootstrap/dist/js/bootstrap';
import { AssetLoader } from './asset-loader';
import { Game } from './game';
import { initLobby } from './lobby';
import * as three from 'three';

const assetLoader = new AssetLoader();

// Phase G — wire the lobby panel as soon as the script runs.  The lobby is
// independent of the Game lifecycle (it only reads URL params and reloads)
// so the user can adjust settings while assets stream in.
initLobby();

assetLoader.loadAll().then(() => {
  const game = new Game(assetLoader);
  // for debugging
  Object.assign(window, {game, three});
  game.start();
});
