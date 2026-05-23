// Phase K Wave 2 — Lobby-only entry point.
//
// The eager graph is intentionally lobby-shaped: i18n, sentry, audit,
// tournaments, history, tour, identity, lobby itself.  Game / World /
// Client / three / MoveLog / AssetLoader / chat / voice live in
// `./game-bootstrap` and are dynamic-imported only when the URL has
// query params (Quick Match / Apply / public-game join all reload the
// page via `location.replace(...)`, so the lobby-only entry never
// downloads the renderer chunk for users who just browse the lobby).
//
// Wave-2 bundle target: eager `autotable-src.<hash>.js` < 500 kB.

import { initLobby } from './lobby';
import { applyTokenToUrl, parseRejoinFromUrl } from './reconnect';
import { initSentry } from './sentry';
import { installI18n } from './i18n';
import { installAvatarMigrationModalIfNeeded } from './identity';
import { registerServiceWorker } from './pwa';

// Phase J Wave 9 — install i18n before any other UI install hook so
// chrome paints with the resolved locale (body[lang=…] attribute is
// set immediately, downstream `t()` calls return localized strings).
installI18n();

// Phase K Wave 1 — Audit tab is admin-only and only used inside the
// replay viewer.  Defer loading until the replay tab is opened — the
// dynamic import shrinks the lobby's initial bundle.
void scheduleAuditTabLazyMount();

// Phase K Wave 1 — Tournaments panel: lazy-load on first click of
// the Tournaments tab so the lobby initial bundle stays well under
// Apone's 500 kB budget.
void scheduleTournamentsLazyMount();

// Phase J Wave 10 — install the forced avatar-migration modal.  The
// modal only renders when the persisted avatarColor is still the
// legacy `#808080` sentinel.  Idempotent + listens for late-arriving
// profile updates.
installAvatarMigrationModalIfNeeded();

// Phase K Wave 1 — Match-history export modal.  Self-injects a link
// into the profile page's Recent games section + mounts its own
// modal scaffold.
void scheduleHistoryLazyMount();

// Phase K Wave 1 → Wave 2 — First-visit onboarding tour.  Wave 1 read
// the LS flag only; Wave 2 prefers the server-authoritative
// `/api/players/me/onboarding-status` endpoint so first-launch state
// follows the user across devices.  LS remains the offline fallback.
void scheduleOnboardingTour();

// Phase J Wave 8 — Frontend error reporting.  Sentry only initialises
// when a non-empty DSN is exposed via <meta name="sentry-dsn"> or
// window.__SENTRY_DSN__; with no DSN this is a no-op.
void initSentry();

// Phase K Wave 2 — PWA service worker registration (cache-first for
// static, network-first for /api/*).  Fire-and-forget — failure to
// register doesn't block the lobby.
void registerServiceWorker();

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
    mod.refreshTournamentsPanel?.();
  };
  btn.addEventListener('mouseenter', () => { void load(); }, { once: true });
  btn.addEventListener('focus', () => { void load(); }, { once: true });
  btn.addEventListener('click', () => { void load(); }, { once: true });
}

async function scheduleHistoryLazyMount(): Promise<void> {
  const profile = document.getElementById('profile-page');
  if (profile === null) return;
  let loaded = false;
  const load = async (): Promise<void> => {
    if (loaded) return;
    loaded = true;
    const mod = await import('./history');
    mod.installHistoryModal();
  };
  const obs = new MutationObserver(() => {
    if (profile.getAttribute('aria-hidden') === 'false') {
      void load();
      obs.disconnect();
    }
  });
  obs.observe(profile, { attributes: true, attributeFilter: ['aria-hidden'] });
  const chip = document.getElementById('lobby-open-profile');
  chip?.addEventListener('mouseenter', () => { void load(); }, { once: true });
}

async function scheduleOnboardingTour(): Promise<void> {
  // Wave 2 — server-authoritative first-launch detection.  Load the
  // tour module lazily; the module itself probes
  // `/api/players/me/onboarding-status` and falls back to the LS flag
  // on network failure / 404.  Fast-path: if the LS flag is already
  // set, never even fetch the module.
  let done = false;
  try { done = window.localStorage.getItem('mahjong.tour.completed.v1') === 'true'; }
  catch { /* offline / blocked storage — treat as not done */ }
  if (done) return;
  window.setTimeout(() => {
    void import('./tour').then(mod => mod.installOnboardingTour());
  }, 350);
}

// Phase J Wave 4 — Consume any `?rejoin=<token>` already on the URL
// before the lobby pre-population reads URL params.
const rejoinAtBoot = parseRejoinFromUrl();
if (rejoinAtBoot !== null) {
  applyTokenToUrl(rejoinAtBoot.decoded);
}

// Phase G — wire the lobby panel as soon as the script runs.  The
// lobby is independent of the Game lifecycle (it only reads URL
// params and reloads) so the user can adjust settings before the
// renderer chunk has finished downloading.
initLobby();

// Phase K Wave 2 — Lazy game bootstrap.  Empty search → lobby-only;
// any query string means the user is either entering a table or
// resuming via rejoin token, so we dynamic-import the renderer.
//
// Quick Match / Apply paths reload the page (`location.replace`), so
// they don't need to preload the bootstrap chunk in the lobby tab —
// the new page load will see a non-empty search and pull it.
if (window.location.search !== '') {
  void import('./game-bootstrap').then(mod => mod.bootstrapGame());
}
