import 'bootstrap/dist/js/bootstrap';
import { AssetLoader } from './asset-loader';
import { Game } from './game';
import { initLobby, attachLobbyClient } from './lobby';
import { MoveLog } from './move-log';
import { Client } from './client';
import { loadPatternOrderingFromApi } from './game-ui';
import { applyTokenToUrl, parseRejoinFromUrl } from './reconnect';
import { initSentry } from './sentry';
import { installI18n } from './i18n';
import { installChatPanel } from './chat';
import { installAuditTab } from './audit';
import * as three from 'three';

// Phase J Wave 9 — install i18n before any other UI install hook so
// chrome paints with the resolved locale (body[lang=…] attribute is
// set immediately, downstream `t()` calls return localized strings).
installI18n();

// Phase J Wave 9 — wire the audit tab in the replay viewer.  Probes
// /api/auth/me to detect admin role; the tab stays hidden for
// non-admin users.  Idempotent.
installAuditTab();

// Phase J Wave 8 — Frontend error reporting.  Sentry only initialises
// when a non-empty DSN is exposed via <meta name="sentry-dsn"> or
// window.__SENTRY_DSN__; with no DSN this is a no-op and no network
// requests are issued.  Fire-and-forget so a slow Sentry boot never
// delays asset load or lobby init.
void initSentry();

const assetLoader = new AssetLoader();

// Phase J Wave 4 — Consume any `?rejoin=<token>` already on the URL
// before the lobby pre-population reads URL params.  If the token is
// valid we stamp `?gameId=…&seat=…` onto the URL (without the rejoin
// param) so the existing Wave-3 routing + Wave-2 seat-take handlers
// pick the rejoin up naturally.  Malformed / expired tokens fall
// through; client-ui.ts surfaces the "session ended" toast later in
// the boot path.
const rejoinAtBoot = parseRejoinFromUrl();
if (rejoinAtBoot !== null) {
  applyTokenToUrl(rejoinAtBoot.decoded);
}

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

  // Phase J Wave 4 — wire the lobby's live player chip strip + seat
  // preview to the live Client collections now that Game.start() has
  // booted the Client.  initLobby() above ran pre-asset-load so the
  // Quick Match button + URL-driven pickers were clickable
  // immediately; attachLobbyClient binds the live listeners on top of
  // the already-rendered panel.
  attachLobbyClient(client);

  // Phase J Wave 9 — install the chat panel after Client.start() so we
  // have access to the seats + nicks collections + the local playerId
  // for self-message detection.  Idempotent; hides itself when no
  // gameId is on the URL (lobby only).
  installChatPanel(client);
});

