// Phase K Wave 4 — 3D scene shell chunk.
//
// Wave 3 produced a single 922 kB `scene.<hash>.js` containing
// AssetLoader + Game + World + three + ClientUi + MoveLog + the
// ~100 kB `GameUi` modal/settings/replay graph.  That was the
// renderer-critical chunk a user had to download before they saw
// their first tile.
//
// Wave 4 splits it in two:
//
//   • `scene-shell.<hash>.js` (this file) — three.js + AssetLoader
//     + Game (minus its lazy GameUi field) + World + ClientUi +
//     MainView.  Resolves to a live `Client` so chat / voice can
//     subscribe, and mints `scene-shell-ready` after the first WebGL
//     frame composites.  Also kicks off a parallel dynamic-import of
//     `./scene-effects` so the heavy DOM modals stream in alongside
//     the texture decode round-trips.
//   • `scene-effects.<hash>.js` — GameUi + MoveLog.  See
//     `scene-effects.ts` for the contract.
//
// Surface contract (consumed by `game-bootstrap.ts`):
//   • `mountScene()` — boots Game + Client + asset loader and
//     notifies `attachLobbyClient(client)`.  Resolves to the live
//     `Client` so chat / voice can subscribe.  Side-effects: emits
//     `scene-shell-ready` after first frame, then `scene-effects-ready`
//     once the deferred effects chunk has finished installing.  For
//     back-compat with Wave-3 specs we still emit the legacy
//     `game-scene-ready` marker alongside `scene-shell-ready`.
//   • Exposes `game` + `three` on `window` for the existing
//     debug-only callers.

import { AssetLoader } from './asset-loader';
import { Game } from './game';
import { attachLobbyClient } from './lobby';
import type { Client } from './client';
import { loadPatternOrderingFromApi } from './pattern-utils';
import * as three from 'three';

let mounted = false;

export async function mountScene(): Promise<Client> {
  if (mounted) {
    const existing = (window as unknown as { __mahjongClient?: Client }).__mahjongClient;
    if (existing !== undefined) return existing;
  }
  mounted = true;

  // Phase J Wave 3 — Bishop's canonical pattern display order.  The
  // hardcoded fallback in pattern-utils keeps things rendering if
  // this fetch fails, so it's safely fire-and-forget.  Wave 4 moved
  // this helper from `game-ui.ts` into the small `pattern-utils`
  // module so importing it doesn't drag the GameUi graph back into
  // the shell chunk.
  void loadPatternOrderingFromApi();

  const assetLoader = new AssetLoader();
  await assetLoader.loadAll();

  const game = new Game(assetLoader);
  Object.assign(window, { game, three });
  game.start();

  // Phase K Wave 4 — `client` is now public on Game (was private with
  // a type-widening cast in Wave 3), so we can read it directly.
  const client = game.client;

  // Phase J Wave 4 — wire the lobby's live player chip strip + seat
  // preview to the live Client collections.
  attachLobbyClient(client);

  (window as unknown as { __mahjongClient?: Client }).__mahjongClient = client;

  // Phase K Wave 4 — Defer the shell-ready signal one rAF so the
  // first WebGL frame has actually composited.  We emit both the new
  // `scene-shell-ready` testid and the Wave-3 `game-scene-ready`
  // testid so Vasquez's existing specs keep working without a
  // selectors.md sweep.
  window.requestAnimationFrame(() => {
    markShellReady();
    // Phase K Wave 4 — kick off the heavy DOM-modal chunk in parallel
    // with the first user interactions.  Failures are toast-able
    // (replay / settings drawer just won't open) but should never
    // tear down the renderer, so we swallow + log.
    void import('./scene-effects')
      .then(mod => mod.mountEffects(game, client))
      .catch(err => {
        // eslint-disable-next-line no-console
        console.warn('[scene-shell] failed to mount scene-effects', err);
      });
  });

  return client;
}

function markShellReady(): void {
  if (document.body.getAttribute('data-scene-shell-ready') === 'true') return;
  document.body.setAttribute('data-scene-shell-ready', 'true');
  document.body.setAttribute('data-game-scene-ready', 'true');
  const shellMarker = document.createElement('div');
  shellMarker.setAttribute('data-testid', 'scene-shell-ready');
  shellMarker.setAttribute('aria-hidden', 'true');
  shellMarker.style.display = 'none';
  document.body.appendChild(shellMarker);
  const legacyMarker = document.createElement('div');
  legacyMarker.setAttribute('data-testid', 'game-scene-ready');
  legacyMarker.setAttribute('aria-hidden', 'true');
  legacyMarker.style.display = 'none';
  document.body.appendChild(legacyMarker);
  window.dispatchEvent(new CustomEvent('mahjong:scene-shell-ready'));
  window.dispatchEvent(new CustomEvent('mahjong:game-scene-ready'));
}
