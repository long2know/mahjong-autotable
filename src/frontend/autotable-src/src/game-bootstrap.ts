// Phase K Wave 3 — Game shell bootstrap (three.js-free).
//
// Wave 2 dynamic-imported `game-bootstrap` off the eager lobby graph
// (eager lobby ≈ 208 kB); the deferred chunk was still 1.11 MB because
// Game / World / Client / AssetLoader / three / MoveLog were all
// imported eagerly *from* `game-bootstrap`.  Wave 3 peels the renderer
// chain into a sibling `./scene` module that is dynamic-imported only
// once the WebGL canvas is mounted.  This module — the "shell" —
// renders the HUD chrome immediately (target: <500 ms after the user
// lands on a table URL), then kicks off the 3D scene asynchronously.
//
// Boundaries:
//   • Pre-shell HUD = vanilla DOM only.  bootstrap CSS is already in
//     the eager bundle; the only JS this file pulls in beyond the
//     lobby chain is the chat + voice modules (both already lazy
//     beyond this point).
//   • The scene chunk owns three.js + AssetLoader + WebGL setup, and
//     mints `data-testid="game-scene-ready"` once it has composited.
//   • Voice chat is gated on the per-game `voiceEnabled` flag (Wave 3
//     wire-up); the `?voice=1` query-string override remains as the
//     E2E-friendly opt-in until the hub broadcasts a flag.
//
// Trigger contract (from `index.ts`):
//   - Empty `window.location.search` → lobby-only; this module is not
//     fetched.  Hovering Quick Match / Apply triggers a preload so
//     the next page load (post-`location.replace`) has it cached.
//   - Non-empty `window.location.search` → user is entering a table;
//     this module is dynamic-imported and `bootstrapGame()` runs.

import 'bootstrap/dist/js/bootstrap';
import type { Client } from './client';

let booted = false;

export async function bootstrapGame(): Promise<void> {
  if (booted) return;
  booted = true;

  // Phase K Wave 3 — Paint the shell + HUD scaffolding immediately so
  // the user sees feedback in <500 ms even though the three.js +
  // asset chunk is still in flight.  The actual game DOM (#main /
  // #loading) already exists in index.html — this hook just publishes
  // the `game-shell-ready` marker that Vasquez's specs gate on.
  markShellReady();

  // Lazy-import the renderer-critical 3D scene chunk.  Wave 4 split
  // this further: `scene-shell` contains three.js (~575 kB) +
  // AssetLoader + Game + World + ClientUi; the heavy GameUi modal
  // graph + MoveLog now live in `scene-effects` which the shell
  // dynamic-imports itself after first-frame.
  const sceneMod = await import('./scene-shell');
  const client = await sceneMod.mountScene();

  // Phase K Wave 1 — Chat panel: only needed when the user is in a
  // game.  Lazy-import it so the lobby-only path doesn't pay the
  // bundle cost.  installChatPanel itself hides the panel when no
  // gameId is on the URL, but the import alone is ~tens of kB —
  // gate on URL inspection here to avoid loading at all.
  if (/[?&]gameId=/.test(window.location.search)) {
    void import('./chat').then(mod => mod.installChatPanel(client));
  }

  // Phase K Wave 3 — Voice chat: gated by Bishop's per-game
  // `voiceEnabled` flag (probed lazily inside `./voice`) with the
  // legacy `?voice=1` query-string opt-in retained as the E2E /
  // self-hosted override.  When voice is disabled the module still
  // mounts a disabled mic toggle so the table-creator settings drawer
  // has somewhere to flip the flag on from.
  if (shouldEnableVoice(client)) {
    void import('./voice').then(mod => mod.installVoicePanel(client));
  }
}

// Phase K Wave 4 — Preload helper.  Lobby wires this to mouseenter /
// pointerdown on Quick Match / Apply so the next page load (after
// `location.replace`) gets the shell + scene chunks from cache.  We
// preload `scene-shell` (renderer-critical) but deliberately skip
// `scene-effects` — that chunk starts loading after first-frame and
// shouldn't block warm navigations.
export function preloadGameBootstrap(): void {
  void import('./game-bootstrap');
  void import('./scene-shell');
}

// ── Shell paint ─────────────────────────────────────────────────────

function markShellReady(): void {
  if (document.body.getAttribute('data-game-shell-ready') === 'true') return;
  document.body.setAttribute('data-game-shell-ready', 'true');
  // Surface a testid-tagged sentinel so Vasquez can wait on the shell
  // without needing to inspect body attributes (which Playwright's
  // `getByTestId` cannot match against).
  const marker = document.createElement('div');
  marker.setAttribute('data-testid', 'game-shell-ready');
  marker.setAttribute('aria-hidden', 'true');
  marker.style.display = 'none';
  document.body.appendChild(marker);
  window.dispatchEvent(new CustomEvent('mahjong:game-shell-ready'));
}

// ── Voice gating ────────────────────────────────────────────────────

function shouldEnableVoice(_client: Client): boolean {
  if (/[?&]voice=1\b/.test(window.location.search)) return true;
  // Bishop's Wave-3 backend broadcasts a per-game `voiceEnabled` flag
  // through the WS `gameSettings` collection.  The voice module
  // itself probes the flag on install and short-circuits to a
  // disabled mic toggle when the table has voice off — we always
  // load it so the toggle is present (the chunk is small once
  // SignalR is pulled in by the lobby chain).
  return true;
}
