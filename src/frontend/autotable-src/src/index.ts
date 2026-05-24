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
import { installI18n } from './i18n';
import { registerServiceWorker } from './pwa';

// Phase J Wave 9 — install i18n before any other UI install hook so
// chrome paints with the resolved locale (body[lang=…] attribute is
// set immediately, downstream `t()` calls return localized strings).
installI18n();

// Phase K Wave 16 — bundle audit §3.1 + §3.5 surgery (Hicks).
//
// Three eager imports are now lazy-mounted to shrink
// `autotable-src-eager`:
//
//   • `./action-router`   →  gated on `?action=*` URL param.  The
//     bare-`/` lobby cold path no longer pays the router weight.
//   • `./identity` avatar-migration modal → gated on the legacy
//     `#808080` sentinel being present in LS.  <2 % of sessions
//     trigger the modal (W14 analytics).
//   • `./sentry`           →  gated on
//     `import.meta.env.PROD || localStorage.SENTRY_DEBUG`.  Local
//     dev + any preview deployment without the DSN meta tag now
//     entirely sheds the 342 KB `sentry` chunk.  Production users
//     still pay the chunk after the gate passes + the DSN probe
//     inside `./sentry` succeeds.
//
// See `docs/frontend-bundle-audit.md §3.1 / §3.5` for the W15
// audit reasoning + W16 delivered-savings table.

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
//
// Phase K Wave 16 — Hicks: lazy-mounted.  We probe LS for the legacy
// sentinel synchronously (~1 KB cost in eager bundle); only when
// present do we dynamic-import `./identity` to pull in the modal +
// avatar editor.  See `docs/frontend-bundle-audit.md §3.1`.
void scheduleAvatarMigrationLazyMount();

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
//
// Phase K Wave 16 — Hicks: lazy-mounted (bundle-audit §3.5).  Gate
// on `import.meta.env.PROD || localStorage.SENTRY_DEBUG` so the
// 342 KB sentry chunk never reaches local-dev or any preview
// deployment without a DSN.  Production users with a DSN still get
// the chunk; the inner DSN check inside `./sentry` short-circuits
// without it.
void scheduleSentryLazyMount();

// Phase K Wave 2 — PWA service worker registration (cache-first for
// static, network-first for /api/*).  Fire-and-forget — failure to
// register doesn't block the lobby.
void registerServiceWorker();

// Phase K Wave 6 — Spectator livestream hash route.  `#/spectate/{id}`
// opens a full-screen audio listener for Bishop's HLS livestream.
// The route handler is lightweight (just a hashchange listener) but
// the actual chunk is dynamic-imported so the lobby cold path never
// pays for the viewer.
void scheduleSpectatorRouteLazyMount();

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

async function scheduleSpectatorRouteLazyMount(): Promise<void> {
  // Phase K Wave 6 — Lazy spectator viewer.  We attach a single
  // hashchange listener here that triggers the dynamic import only
  // when the route actually matches `#/spectate/{tableId}`; lobby-
  // only sessions never fetch the spectator chunk.  Fires once at
  // boot too so a deep-link arrival opens the viewer.
  const isSpectateHash = (): boolean => /^#\/spectate\//.test(window.location.hash);
  const load = async (): Promise<void> => {
    try {
      const mod = await import('./spectator-livestream');
      mod.installSpectatorRoute();
    } catch {
      /* never resolved — degrade silently */
    }
  };
  if (isSpectateHash()) {
    void load();
    return;
  }
  const onHash = (): void => {
    if (isSpectateHash()) {
      window.removeEventListener('hashchange', onHash);
      void load();
    }
  };
  window.addEventListener('hashchange', onHash);
}

async function scheduleAvatarMigrationLazyMount(): Promise<void> {
  // Phase K Wave 16 — bundle-audit §3.1: probe LS for the legacy
  // `#808080` sentinel ourselves so the eager bundle never pulls
  // in the full identity / avatar-editor modal until the migration
  // path is actually relevant.  ~1 KB eager probe vs. ~20 KB modal.
  const LEGACY_AVATAR_COLOR = '#808080';
  const LS_KEY_IDENTITY_CACHE = 'mahjong.identity.cache.v1';
  let raw: string | null = null;
  try { raw = window.localStorage.getItem(LS_KEY_IDENTITY_CACHE); }
  catch { /* private mode / quota — treat as absent */ }
  if (raw === null) return;
  let needsMigration = false;
  try {
    const j = JSON.parse(raw) as { avatarColor?: unknown };
    needsMigration =
      typeof j.avatarColor === 'string'
      && j.avatarColor.toLowerCase() === LEGACY_AVATAR_COLOR;
  } catch { /* corrupt cache — the identity module will repair it */ }
  if (!needsMigration) return;
  try {
    const mod = await import('./identity');
    mod.installAvatarMigrationModalIfNeeded();
  } catch { /* fail-open: never block the lobby on migration */ }
}

async function scheduleSentryLazyMount(): Promise<void> {
  // Phase K Wave 16 — bundle-audit §3.5: gate the entire sentry
  // chunk (342 KB at W15) on PROD env OR explicit local-dev opt-in
  // via `localStorage.SENTRY_DEBUG`.  Dev + DSN-less preview deploys
  // entirely shed the chunk + the inner DSN probe.
  const isProd = (import.meta as { env?: { PROD?: boolean } }).env?.PROD === true;
  let debugFlag = false;
  try { debugFlag = window.localStorage.getItem('SENTRY_DEBUG') === '1'; }
  catch { /* offline / blocked storage — treat as off */ }
  if (!isProd && !debugFlag) return;
  try {
    const mod = await import('./sentry');
    await mod.initSentry();
  } catch { /* fail-open: never block the lobby on Sentry */ }
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

// Phase K Wave 11 — PWA shortcut `?action=*` deep-link routing.
// Must run BEFORE the game-bootstrap import guard below, since the
// guard treats any non-empty search as a game URL.  Returns true if
// a PWA shortcut was handled; we then skip the game-bootstrap path
// so we don't dynamic-import the renderer chunk for a lobby-shaped
// deep link.  See `docs/frontend-routing.md`.
//
// Phase K Wave 16 — Hicks (bundle-audit §3.1): action-router is now
// lazy-mounted.  We sniff `window.location.search` synchronously for
// `action=` and short-circuit to the unhandled state on the bare
// lobby path.  When `action=` IS present, we await the dynamic
// import — the lobby cold path never pays for the ~25 KB router
// graph (W14 / W15 added 6 new keywords inflating it).  The
// game-bootstrap dispatch below is wrapped in an IIFE so we avoid
// top-level `await` (the tsconfig target is ES2017).
void (async (): Promise<void> => {
  const pwaActionHandled = await maybeHandlePwaAction();

  // Phase K Wave 15 — Phase L renderer hello-world spike.  Loads the
  // hand-rolled WebGL2 hello-world ONLY when the URL has
  // `?renderer=webgl2-hello` (or the W16 `?renderer=webgl2-tile-mesh`
  // tile-mesh smoke).  Dev/spike harness — never runs on the lobby
  // cold path.  The dynamic import boundary forces vite to emit
  // `renderer-webgl2.<hash>.js` as its own measurable chunk; see
  // `docs/phase-l-renderer-implementation.md`.
  if (/[?&]renderer=webgl2-(hello|tile-mesh)/.test(window.location.search)) {
    void import('./renderer-webgl2/hello').then((mod) => mod.mount());
  }

  // Phase K Wave 2 — Lazy game bootstrap.  Empty search → lobby-only;
  // any query string means the user is either entering a table or
  // resuming via rejoin token, so we dynamic-import the renderer.
  //
  // Quick Match / Apply paths reload the page (`location.replace`), so
  // they don't need to preload the bootstrap chunk in the lobby tab —
  // the new page load will see a non-empty search and pull it.
  //
  // W11 — the action-router strips its own `?action=*` param before
  // returning true, so `window.location.search` here reflects any
  // non-action params still on the URL (e.g. a rejoin token alongside
  // the shortcut).  Empty search after the strip → lobby-only.
  if (!pwaActionHandled && window.location.search !== '') {
    void import('./game-bootstrap').then(mod => mod.bootstrapGame());
  }
})();

async function maybeHandlePwaAction(): Promise<boolean> {
  if (!/[?&]action=/.test(window.location.search)) return false;
  try {
    const mod = await import('./action-router');
    return mod.handlePwaActionFromUrl();
  } catch {
    return false;
  }
}
