// Phase K Wave 5 — Thin scene coordinator (three.js-free).
//
// Wave-3 history:
//
//   Wave 3 produced a single 922 kB `scene.<hash>.js` chunk containing
//   three.js + AssetLoader + Game + World + ClientUi + MoveLog + the
//   ~100 kB GameUi modal/settings/replay graph.  Wave 4 peeled GameUi
//   + MoveLog into `scene-effects` (~60 kB) but `scene-shell` still
//   sat at 886 kB because three.js (~575 kB) and the asset/world
//   graph were imported statically.
//
//   Wave 5 finishes the split: the entire three.js subgraph
//   (AssetLoader + Game + World + MainView + ClientUi + every module
//   that statically imports `from 'three'`) now lives in
//   `./three-renderer`, which `scene-shell` dynamic-imports.  THIS
//   module is the small (~5 kB) coordinator that owns the lifecycle
//   markers; it has no static three.js import, so the
//   `scene-shell.<hash>.js` chunk falls below the Wave-2 <500 kB
//   target the squad has been chasing since Wave 3.
//
// Wave-5 chunk topology:
//
//   • `scene-shell.<hash>.js` (this file) — thin coordinator.
//     Awaits `three-renderer`, wires the live `Client` into the
//     lobby, mints `data-testid="scene-shell-ready"` on the first
//     rAF after the renderer comes up, then dynamic-imports
//     `scene-effects`.
//   • `three-renderer.<hash>.js` — the heavy three.js + WebGL
//     bootstrap.  Mints `data-testid="three-renderer-ready"` once
//     `Game.start()` returns.
//   • `scene-effects.<hash>.js` — GameUi + MoveLog (unchanged from
//     Wave 4).
//
// Surface contract (consumed by `game-bootstrap.ts`):
//   • `mountScene()` — boots the renderer, notifies the lobby with
//     the live `Client`, and emits `scene-shell-ready` after the
//     first frame composites.  Then dynamic-imports `scene-effects`
//     in the background; failures there are logged + swallowed so
//     the renderer stays alive even if the modal graph never lands.
//
// Wave-5 retired the `data-testid="game-scene-ready"` back-compat
// marker that Wave 3 introduced for the Wave-2 spec sweep.  Vasquez's
// Wave-4 specs already gate on `scene-shell-ready`; carrying a second
// marker through Wave 5 just kept dead code in the renderer chunk.

import type { Client } from './client';
import { attachLobbyClient } from './lobby';
import { loadPatternOrderingFromApi } from './pattern-utils';

let mounted = false;

export async function mountScene(): Promise<Client> {
  if (mounted) {
    const existing = (window as unknown as { __mahjongClient?: Client }).__mahjongClient;
    if (existing !== undefined) return existing;
  }
  mounted = true;

  // Phase J Wave 3 — Bishop's canonical pattern display order.  The
  // hardcoded fallback in pattern-utils keeps things rendering if
  // this fetch fails, so it's safely fire-and-forget.  Lives in the
  // small `pattern-utils` module so importing it doesn't drag the
  // GameUi graph back into the shell chunk.
  void loadPatternOrderingFromApi();

  // Phase K Wave 5 — Dynamic-import the heavy three.js subgraph.
  // This is the cut that takes `scene-shell` from 886 kB → ~5 kB
  // and parks the ~575 kB of three.js (plus AssetLoader + Game +
  // World + MainView) in a sibling `three-renderer.<hash>.js` chunk.
  const rendererMod = await import('./three-renderer');
  const { game, client } = await rendererMod.mountThreeRenderer();

  // Phase J Wave 4 — wire the lobby's live player chip strip + seat
  // preview to the live Client collections.
  attachLobbyClient(client);

  (window as unknown as { __mahjongClient?: Client }).__mahjongClient = client;

  // Phase K Wave 4 — Defer the shell-ready signal one rAF so the
  // first WebGL frame has actually composited.  Wave 5 retired the
  // `game-scene-ready` back-compat alias — Vasquez's specs gate on
  // `scene-shell-ready` exclusively now.
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
  const shellMarker = document.createElement('div');
  shellMarker.setAttribute('data-testid', 'scene-shell-ready');
  shellMarker.setAttribute('aria-hidden', 'true');
  shellMarker.style.display = 'none';
  document.body.appendChild(shellMarker);
  window.dispatchEvent(new CustomEvent('mahjong:scene-shell-ready'));
}
