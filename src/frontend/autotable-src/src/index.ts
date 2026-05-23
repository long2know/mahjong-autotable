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
import { installAvatarMigrationModalIfNeeded } from './identity';
import * as three from 'three';

// Phase J Wave 9 — install i18n before any other UI install hook so
// chrome paints with the resolved locale (body[lang=…] attribute is
// set immediately, downstream `t()` calls return localized strings).
installI18n();

// Phase K Wave 1 — Audit tab is admin-only and only used inside the
// replay viewer.  Defer loading until the replay tab is opened — the
// dynamic import shrinks the lobby's initial bundle.  We probe the
// replay tab button if it's present (it is — declared statically in
// index.html) and lazy-mount on hover/focus/click.
void scheduleAuditTabLazyMount();

// Phase K Wave 1 — Tournaments panel: lazy-load on first click of
// the Tournaments tab so the lobby initial bundle stays well under
// Apone's 500 kB budget.  initLobby() / installLobbyTabs() take care
// of the click wiring; we attach our own one-shot listener here that
// resolves the module then forwards to the existing tab handler.
void scheduleTournamentsLazyMount();

// Phase J Wave 10 — install the forced avatar-migration modal.  The
// modal only renders when the persisted avatarColor is still the
// legacy `#808080` sentinel.  Idempotent + listens for late-arriving
// profile updates.
installAvatarMigrationModalIfNeeded();

// Phase K Wave 1 — Match-history export modal.  Self-injects a link
// into the profile page's Recent games section + mounts its own
// modal scaffold.  Dynamic-imported so its preview / CSV pipeline
// only loads when the user opens their profile.
void scheduleHistoryLazyMount();

// Phase K Wave 1 — First-visit onboarding tour.  Reads the
// `mahjong.tour.completed.v1` LS flag — a no-op if the user has
// completed (or skipped) the tour before.  Loaded lazily so returning
// users don't pay the bundle cost.
void scheduleOnboardingTour();

// Phase J Wave 8 — Frontend error reporting.  Sentry only initialises
// when a non-empty DSN is exposed via <meta name="sentry-dsn"> or
// window.__SENTRY_DSN__; with no DSN this is a no-op and no network
// requests are issued.  Fire-and-forget so a slow Sentry boot never
// delays asset load or lobby init.
void initSentry();

async function scheduleAuditTabLazyMount(): Promise<void> {
  const btn = document.getElementById('replay-audit-tab') as HTMLButtonElement | null;
  if (btn === null) return;
  let loaded = false;
  const load = async (): Promise<void> => {
    if (loaded) return;
    loaded = true;
    const mod = await import('./audit');
    mod.installAuditTab();
  };
  btn.addEventListener('mouseenter', () => { void load(); }, { once: true });
  btn.addEventListener('focus', () => { void load(); }, { once: true });
  btn.addEventListener('click', () => { void load(); }, { once: true });
  // Also load when the replay screen becomes visible — a user might
  // open the replay viewer programmatically (e.g. via the
  // "Watch finals" pin from a tournament bracket).
  const replay = document.getElementById('replay-screen');
  if (replay !== null) {
    const obs = new MutationObserver(() => {
      if (!replay.hidden) {
        void load();
        obs.disconnect();
      }
    });
    obs.observe(replay, { attributes: true, attributeFilter: ['hidden'] });
  }
}

async function scheduleTournamentsLazyMount(): Promise<void> {
  const btn = document.getElementById('lobby-tournaments-tab') as HTMLButtonElement | null;
  if (btn === null) return;
  let loaded = false;
  const load = async (): Promise<void> => {
    if (loaded) return;
    loaded = true;
    const mod = await import('./tournaments');
    mod.installTournamentsPanel();
    // The user already clicked the tab to trigger the load — give the
    // freshly-installed panel a kick so it renders the first batch
    // without waiting for the next interaction.
    mod.refreshTournamentsPanel?.();
  };
  btn.addEventListener('mouseenter', () => { void load(); }, { once: true });
  btn.addEventListener('focus', () => { void load(); }, { once: true });
  btn.addEventListener('click', () => { void load(); }, { once: true });
}

async function scheduleHistoryLazyMount(): Promise<void> {
  // The "Match history" link is injected by history.ts itself into the
  // profile-page Recent games section.  Mount the module as soon as
  // the profile page is first opened.
  const profile = document.getElementById('profile-page');
  if (profile === null) return;
  let loaded = false;
  const load = async (): Promise<void> => {
    if (loaded) return;
    loaded = true;
    const mod = await import('./history');
    mod.installHistoryModal();
  };
  // Profile page uses `aria-hidden="true"` toggling.  Watch for that.
  const obs = new MutationObserver(() => {
    if (profile.getAttribute('aria-hidden') === 'false') {
      void load();
      obs.disconnect();
    }
  });
  obs.observe(profile, { attributes: true, attributeFilter: ['aria-hidden'] });
  // Also load on profile chip hover — preloads before the user clicks.
  const chip = document.getElementById('lobby-open-profile');
  chip?.addEventListener('mouseenter', () => { void load(); }, { once: true });
}

async function scheduleOnboardingTour(): Promise<void> {
  let done = true;
  try { done = window.localStorage.getItem('mahjong.tour.completed.v1') === 'true'; }
  catch { done = false; }
  if (done) return;
  // Wait a tick for the lobby chrome to settle so the first spotlight
  // has a target.
  window.setTimeout(() => {
    void import('./tour').then(mod => mod.installOnboardingTour());
  }, 350);
}

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

  // Phase K Wave 1 — Chat panel: only needed when the user is in a
  // game.  Lazy-import it so the lobby-only path doesn't pay the
  // bundle cost.  installChatPanel itself hides the panel when no
  // gameId is on the URL, but the import alone is ~tens of kB —
  // gate on URL inspection here to avoid loading at all.
  if (/[?&]gameId=/.test(window.location.search)) {
    void import('./chat').then(mod => mod.installChatPanel(client));
  }
});

