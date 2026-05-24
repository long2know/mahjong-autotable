// Phase G — Sidebar lobby UI.  Phase H Wave 1 polish layered on top:
// optional seed override, hand-count selector, save-defaults to
// localStorage, and an About / Known Limitations footer link.
//
// Phase J Wave 4 — extends with:
//   • Player chip strip (`#lobby-players-strip`) showing who's already
//     in the active game, with stable colour-keyed chips.
//   • Live seat-preview grid (`#lobby-seat-preview`) so the user can
//     plan a seat take before applying.
//   • Quick Match button (`#lobby-quick-match`) — bypasses the pickers
//     and starts a 3-Medium-bot game with the current variant in one
//     click.
//   • Settings shortcut (`#lobby-open-settings`) — opens the existing
//     settings drawer without closing the lobby panel.
//   • `attachLobbyClient(client)` — deferred-bind helper used from
//     `index.ts` after Game.start() so the chip strip / seat preview
//     can subscribe to the live `seats` + `nicks` collections.
//
// Phase J Wave 5 — Public matchmaking lobby + profile drawer + stats
// surface.  Adds:
//   • Lobby tab strip (`#lobby-my-game-tab`, `#lobby-public-games-tab`)
//     toggling between the existing Quick-Match/seat picker and the
//     new public-games browser.
//   • Public-games pane — list of games from the matchmaking REST
//     endpoint, with per-card Join + an inline "Join Random" button.
//   • Make-public toggle for the host of the current game, with an
//     optional friendly name.
//   • Stats panel rendering the local player's career stats from the
//     profile cache.
//   • Profile drawer hooks — `installProfileDrawer/Toggle` are called
//     here so the lobby is the single place that owns the profile UI's
//     mount lifecycle.
//   • Player chips now prefer the local player's profile displayName
//     + avatarColor over the WS-broadcast nick / djb2 hue fallback.

import type { Client } from './client';
// Phase K Wave 19 — Hicks (bundle-audit §3.4).  `matchmaking` (~7.7 KB
// minified) is now lazy-loaded behind `loadMatchmaking()` /
// `schedulePublicGamesPaneLazyMount()` / `scheduleMakePublicToggleLazy
// Mount()`.  The lobby cold path only pulls the chunk when the user
// first activates the Public-Games tab OR touches the make-public
// toggle in a live game.  All matchmaking call sites in lobby.ts now
// go through the closure-stashed `_matchmakingMod` reference; tab-
// activate transitions delegate poll start/stop to that module via
// the same lazy hook (the public-games tab is the only activation
// surface that needs the polling loop).
import type * as MatchmakingModule from './matchmaking';
import {
  hydrateProfileFromCacheIfAvailable,
  onProfile,
} from './profile';
// Phase K Wave 21 — Hicks (bundle-audit §3.6).  `profile-drawer.ts`
// (~6 KB raw / ~2 KB minified) is now lazy-loaded behind
// `scheduleProfileDrawerLazyMount()` (defined at the bottom of
// this file).  The lobby cold path no longer pays for the legacy
// Wave-5 drawer's DOM-installation graph; the drawer + chip-
// toggle mount on the first `lobby-open-profile` chip
// hover/focus/click, parallel to the W17 §3.2 lazy-mount of
// `./profile-page` and `./settings-drawer`.
import type * as ProfileDrawerModule from './profile-drawer';
// Phase K Wave 23 — Hicks (bundle-audit §3.8).  The W19 lazy
// stats-formatter trigger has migrated to `./lobby-stats-panel.ts`
// (now also lazy), so the `StatsModule` type import is no longer
// needed in the eager bundle.  The W19 comment block above is
// preserved as historical context.
import {
  bootstrapIdentity,
  onIdentity,
  refreshOnboardingVisibility,
} from './identity';
// Phase K Wave 17 — bundle audit §3.2 (Hicks).  Three former eager
// imports — leaderboard, settings-drawer, profile-page — are now lazy-
// loaded behind `scheduleLeaderboardLazyMount` / `scheduleSettings
// DrawerLazyMount` / `scheduleProfilePageLazyMount` (defined at the
// bottom of this file).  Each module is loaded only when its
// activation surface (tab click, gear-icon click, or profile-chip
// click) is first reached.  Module references are stashed in a
// closure so tab-activate transitions and the cross-module
// `mahjong:open-profile-page` custom event can drive the lazy
// modules without re-importing them.  See `docs/frontend-bundle-
// audit.md §3.2` for the W17 delivered-savings table.
import type * as LeaderboardModule from './leaderboard';
import type * as ProfilePageModule from './profile-page';
// Phase K Wave 20 — Hicks (bundle-audit §3.5).  `auth` (~40 KB
// source / ~16-20 KB minified incl. EventEmitter + sign-in modal
// scaffolding + linked-accounts renderer) is now lazy-loaded behind
// `scheduleAuthUiLazyMount()` (defined at the bottom of this file).
// The lobby's eager bootstrap no longer pulls the auth-UI graph;
// `installAuthUi()` mounts on the next idle window after first
// paint, so the lobby cold path renders without waiting on the
// auth chunk.  The `auth-shared` chunk that emerges from the
// dynamic-import boundary is shared with `rule-presets` (W19 lazy)
// which also consumes `getAuthState/onAuth`.  See
// `docs/frontend-bundle-audit.md §3.5` for the W20 delivered-
// savings table.
import type * as AuthModule from './auth';
// Phase K Wave 19 — Hicks (bundle-audit §3.4).  `rule-presets`
// (~12.5 KB minified incl. EventEmitter) is now lazy-loaded behind
// `scheduleRulePresetsUiLazyMount()` (defined at the bottom of this
// file).  The lobby's eager bootstrap inlines a tiny LS-read for the
// selected preset id so the URL builder can still emit `?rulePreset=`
// without dragging in the editor surface; the actual `installRule
// PresetsUi()` call is deferred to `requestIdleCallback` so the
// picker mounts on the next paint window without blocking lobby
// first-paint.
import type * as RulePresetsModule from './rule-presets';
// Phase K Wave 23 — Hicks (bundle-audit §3.8).  `theme.ts`
// (`installDisplayPreferences`) is now lazy-loaded so the lobby
// cold path no longer pulls the LS-loader + matchMedia plumbing.
// We still apply a synchronous best-effort body-class on initLobby
// so the chrome paints with the correct palette immediately (see
// `scheduleDisplayPreferencesLazyMount` below); the full module
// (which adds the matchMedia change-listener) lands on the next
// microtask.
import type * as ThemeModule from './theme';
// Phase K Wave 18 — Hicks (bundle-audit §3.3).  `spectator-follow`
// is now lazy-mounted behind a URL probe (`?seat=-1` or
// `?spectate`) so non-spectator lobby visitors never pull the
// follow-seat helper (~5 KB after minify).  <1 % of sessions
// land on a spectator URL (W18 analytics).
import { setElHidden } from './dom-utils';
//
// The lobby is a small overlay panel anchored top-left of the autotable
// page.  It lets the user pick the Phase F query params
// (variant / dealMode / botCount / botDifficulty) plus the new Phase H
// params (seed / handCount) without editing the URL bar.  Clicking
// "Apply & Start" rebuilds the query string and calls
// window.location.replace(), which restarts the game with the chosen
// settings.  No soft hot-swap is performed — V2 work.
//
// The lobby module is intentionally framework-free (plain TS + DOM) so it
// stays inside the existing autotable bundle and adds zero new deps.  It
// is independent of the Game/Client lifecycle: it only reads URL params
// and navigates.
//
// Resolution priority for the picker pre-population: URL > localStorage
// > hardcoded DEFAULTS.  localStorage is written only when the
// "Save as defaults" checkbox is ticked at Apply time.

type Variant =
  | 'changsha'
  | 'four-player'
  | 'three-player'
  | 'bamboo'
  | 'minefield';

type DealMode = 'manual' | 'auto';
type BotCount = 0 | 3 | 4;
// Phase J Wave 8 — Master tier joins the trio.  Bishop's Wave 8 bot
// difficulty model accepts the new tier; older builds will treat
// Master as Hard server-side (graceful degradation).
type BotDifficulty = 'Easy' | 'Medium' | 'Hard' | 'Master';
// Phase J Wave 2 — Hand counts now include 1 (single-hand sandbox) and the
// default shifts from 8 to 4 to mirror Bishop's runtime default east-wind
// rotation (see Phase J Wave 2 directive §Task 3).
type HandCount = 1 | 4 | 8 | 16 | 32;

// Phase I Wave 4 — seat selection.
//   • null  → no preference (server seats us in the first open chair,
//             matches the pre-Wave-4 default flow).
//   • 0..3  → explicit seat take.  The lobby surfaces these so a
//             returning player can re-claim their wind without juggling
//             URL params, but the bulk of the wave is about -1.
//   • -1    → Spectator.  No seat assignment, server broadcasts only.
//             Pairs with botCount=4 for the all-bots-watch experience
//             that Bishop's backend cap relax enables (the runtime
//             auto-deals once all four seats are bots).
type SeatChoice = -1 | 0 | 1 | 2 | 3 | null;

// Exported (`export interface`) so the W23 lazy `./lobby-url-io.ts`
// chunk can use it via a type-only import (zero runtime cost).
export interface LobbyState {
  variant: Variant;
  dealMode: DealMode;
  botCount: BotCount;
  botDifficulty: BotDifficulty;
  // Optional seed override: null = let the server pick a random seed
  // (current behavior); otherwise a non-negative 32-bit signed integer
  // that the backend can pass straight to ChangshaGameRuntime.CreateGame.
  seed: number | null;
  // Number of hands per match.  Backend doesn't read this yet (Phase H
  // V2); the param being present in the URL is the lobby contract so the
  // runtime can pick it up later without a frontend redeploy.
  handCount: HandCount;
  // Phase I Wave 4 — seat selection (see SeatChoice).
  seat: SeatChoice;
}

// Hardcoded defaults — the floor of the resolution chain.  Match Phase F's
// backend defaults (variant=changsha, dealMode=manual, botCount=3) plus
// Phase J Wave 2 directive defaults:
//   • botDifficulty = Hard (Phase J Wave 2 settings drawer default)
//   • handCount = 4 (mirrors Bishop's runtime east-wind rotation default)
//   • seed = null (server randomises)
//   • seat = null (server picks, legacy flow)
const DEFAULTS: LobbyState = {
  variant: 'changsha',
  dealMode: 'manual',
  botCount: 3,
  botDifficulty: 'Hard',
  seed: null,
  handCount: 4,
  seat: null,
};

const VARIANTS: ReadonlyArray<Variant> = [
  'changsha', 'four-player', 'three-player', 'bamboo', 'minefield',
];

const HAND_COUNTS: ReadonlyArray<HandCount> = [1, 4, 8, 16, 32];

// localStorage key for persisted lobby defaults.  Versioned so we can
// migrate the shape later without colliding with an old payload.
const LOCAL_STORAGE_KEY = 'mahjong.lobby.defaults';

// Seed is a non-negative 32-bit signed integer so it round-trips through
// .NET's int (Random.Shared.Next bounds).  Bigger seeds are rejected.
const MAX_SEED = 0x7fffffff;

// About link target.  Ripley owns docs/known-limitations.md in parallel
// on this branch.  The backend (Program.cs) only serves /autotable/* as
// static files — it does NOT serve /docs/* — so a relative link to
// /docs/known-limitations.md would 404.  Point at the GitHub copy on the
// repo's main branch instead: it's stable, always available, and
// renders the markdown natively.
const ABOUT_LINK_HREF =
  'https://github.com/long2know/mahjong-autotable/blob/main/docs/known-limitations.md';

function isVariant(v: string): v is Variant {
  return (VARIANTS as ReadonlyArray<string>).indexOf(v) !== -1;
}

function isBotCount(n: number): n is BotCount {
  return n === 0 || n === 3 || n === 4;
}

function isHandCount(n: number): n is HandCount {
  return (HAND_COUNTS as ReadonlyArray<number>).indexOf(n) !== -1;
}

// Phase I Wave 4 — accept -1 / 0 / 1 / 2 / 3 as valid seat selections.
// null is not parsed here; the absence of the URL param maps to null
// (no preference) in resolveInitialState.
function isSeatChoice(n: number): n is Exclude<SeatChoice, null> {
  return n === -1 || n === 0 || n === 1 || n === 2 || n === 3;
}

// Phase I Wave 4 — botCount=4 is only valid when seat=-1 (spectator).
// Bishop's server cap is 0–3 for the seated flow; 0–4 for the spectator
// flow.  We mirror that cap here so the URL we build is always one the
// server will accept, and so a hand-typed URL with botCount=4 but no
// seat=-1 doesn't silently get reified to a 4-bot picker that then
// fails server-side validation.
function clampBotCountForSeat(botCount: BotCount, seat: SeatChoice): BotCount {
  if (seat === -1) return botCount;
  return botCount === 4 ? 3 : botCount;
}

// Coerce a raw seed candidate (string-or-number) to a valid seed or null.
// Empty / blank / non-numeric / out-of-range all collapse to null so the
// server keeps its random-seed behaviour.
function coerceSeed(raw: string | number | null | undefined): number | null {
  if (raw === null || raw === undefined) return null;
  const s = typeof raw === 'string' ? raw.trim() : String(raw);
  if (s === '') return null;
  // Reject anything that isn't an integer literal so we don't silently
  // round 12345.6 or accept "12345abc".
  if (!/^-?\d+$/.test(s)) return null;
  const n = parseInt(s, 10);
  if (isNaN(n)) return null;
  if (n < 0 || n > MAX_SEED) return null;
  return n;
}

function parseUrlState(): Partial<LobbyState> {
  const p = new URLSearchParams(window.location.search);
  const out: Partial<LobbyState> = {};

  // Variant — accept either lowercase-kebab (changsha, four-player) or
  // SCREAMING_SNAKE (FOUR_PLAYER) for back-compat with the existing
  // Phase F parser in game-ui.ts.
  const vRaw = p.get('variant');
  if (vRaw !== null) {
    const v = vRaw.toLowerCase().replace(/_/g, '-');
    if (isVariant(v)) out.variant = v;
  }

  const dm = (p.get('dealMode') ?? '').toLowerCase();
  if (dm === 'manual' || dm === 'auto') out.dealMode = dm;

  // Back-compat: ?bots=true aliases botCount=3 (Phase F game-ui.ts:99–103).
  const bcRaw = p.get('botCount');
  if (bcRaw !== null) {
    const n = parseInt(bcRaw, 10);
    if (!isNaN(n) && isBotCount(n)) out.botCount = n;
  } else if (p.get('bots') === 'true') {
    out.botCount = 3;
  }

  // Difficulty — backend uses PascalCase (Easy/Medium/Hard) per Phase F
  // AutotableConnection.BotDifficulty.  Accept any casing on input.
  const bd = (p.get('botDifficulty') ?? '').toLowerCase();
  if (bd === 'easy') out.botDifficulty = 'Easy';
  else if (bd === 'medium') out.botDifficulty = 'Medium';
  else if (bd === 'hard') out.botDifficulty = 'Hard';
  else if (bd === 'master') out.botDifficulty = 'Master';

  // Phase H Wave 1 — optional seed override and hand count.  Both are
  // ignored silently if malformed so a hand-typed URL doesn't crash the
  // lobby pre-population.
  const seedRaw = p.get('seed');
  if (seedRaw !== null) {
    const seed = coerceSeed(seedRaw);
    if (seed !== null) out.seed = seed;
  }

  const hcRaw = p.get('handCount');
  if (hcRaw !== null) {
    const hc = parseInt(hcRaw, 10);
    if (!isNaN(hc) && isHandCount(hc)) out.handCount = hc;
  }

  // Phase I Wave 4 — seat selection.  Anything outside {-1, 0..3} is
  // ignored silently so a hand-typed URL doesn't crash the pre-population.
  const seatRaw = p.get('seat');
  if (seatRaw !== null) {
    const n = parseInt(seatRaw, 10);
    if (!isNaN(n) && isSeatChoice(n)) out.seat = n;
  }

  return out;
}

// Phase H Wave 1 — read persisted lobby defaults from localStorage.
// Schema is best-effort: any malformed field falls through and the
// hardcoded DEFAULTS take over for that field.  Wrapped in try/catch
// because localStorage access throws under privacy mode + the JSON
// parse can throw on tampered payloads.
function parseLocalStorageState(): Partial<LobbyState> {
  try {
    const raw = window.localStorage.getItem(LOCAL_STORAGE_KEY);
    if (raw === null) return {};
    const j = JSON.parse(raw) as Record<string, unknown>;
    const out: Partial<LobbyState> = {};

    if (typeof j.variant === 'string') {
      const v = j.variant.toLowerCase().replace(/_/g, '-');
      if (isVariant(v)) out.variant = v;
    }
    if (j.dealMode === 'manual' || j.dealMode === 'auto') {
      out.dealMode = j.dealMode;
    }
    if (typeof j.botCount === 'number' && isBotCount(j.botCount)) {
      out.botCount = j.botCount;
    }
    if (j.botDifficulty === 'Easy' || j.botDifficulty === 'Medium' || j.botDifficulty === 'Hard' || j.botDifficulty === 'Master') {
      out.botDifficulty = j.botDifficulty;
    }
    // Seed: stored as number-or-null.  coerceSeed handles both, mapping
    // any out-of-range or non-integer value back to null.
    if (j.seed === null) {
      out.seed = null;
    } else if (typeof j.seed === 'number' || typeof j.seed === 'string') {
      out.seed = coerceSeed(j.seed);
    }
    if (typeof j.handCount === 'number' && isHandCount(j.handCount)) {
      out.handCount = j.handCount;
    }
    if (typeof j.seat === 'number' && isSeatChoice(j.seat)) {
      out.seat = j.seat;
    } else if (j.seat === null) {
      // Explicit null preserves "no preference" through a round-trip.
      out.seat = null;
    }
    return out;
  } catch {
    return {};
  }
}

// Resolve the pre-population state for the lobby pickers.
// Priority: URL params > localStorage > DEFAULTS.  URL is the deep-link
// source of truth (a shared URL must reproduce the same game settings);
// localStorage is the user's persistent personal default; DEFAULTS is
// the floor for first-time users.
//
// Phase I Wave 4 — `botCount=4` is only valid when `seat=-1` (Bishop's
// spectator cap relax).  We clamp here so a hand-typed URL with
// `?botCount=4` but no `?seat=-1` doesn't slip a 4 into the picker, and
// so localStorage from a previous spectator session doesn't pollute a
// fresh play session.
function resolveInitialState(): LobbyState {
  const url = parseUrlState();
  const ls = parseLocalStorageState();
  const seat = url.seat !== undefined ? url.seat : (ls.seat !== undefined ? ls.seat : DEFAULTS.seat);
  const rawBotCount = url.botCount ?? ls.botCount ?? DEFAULTS.botCount;
  const botCount = clampBotCountForSeat(rawBotCount, seat);
  return {
    variant: url.variant ?? ls.variant ?? DEFAULTS.variant,
    dealMode: url.dealMode ?? ls.dealMode ?? DEFAULTS.dealMode,
    botCount,
    botDifficulty: url.botDifficulty ?? ls.botDifficulty ?? DEFAULTS.botDifficulty,
    seed: url.seed !== undefined ? url.seed : (ls.seed !== undefined ? ls.seed : DEFAULTS.seed),
    handCount: url.handCount ?? ls.handCount ?? DEFAULTS.handCount,
    seat,
  };
}

// Phase K Wave 23 — Hicks (bundle-audit §3.8).  `writeLocalStorage
// Defaults` + `buildUrl` + `readSelectedPresetIdInline` have moved
// to `./lobby-url-io.ts` so the ~1.5 KB of URL-param serialisation +
// LS persistence ships as a lazy chunk.  Both apply-handlers
// below await `loadLobbyUrlIo()` before redirecting; the click-
// handler flow is unchanged from the user's perspective.

// First-load policy: show the lobby when the user lands on a bare URL
// (no query params at all).  Once the user has applied a setting the URL
// always carries params, so subsequent loads go straight into the game
// and the lobby is only available via the toggle button.
function shouldShowOnLoad(): boolean {
  return window.location.search === '';
}

export function initLobby(client?: Client): void {
  if (client !== undefined) {
    _attachedClient = client;
  }
  const panel = document.getElementById('lobby-panel');
  const toggle = document.getElementById('lobby-toggle') as HTMLButtonElement | null;
  if (panel === null || toggle === null) return;

  const apply = document.getElementById('lobby-apply') as HTMLButtonElement | null;
  const close = document.getElementById('lobby-close') as HTMLButtonElement | null;
  if (apply === null || close === null) return;

  const variantInputs = Array.from(
    document.querySelectorAll<HTMLInputElement>('input[name="lobby-variant"]'));
  const dealModeInputs = Array.from(
    document.querySelectorAll<HTMLInputElement>('input[name="lobby-deal-mode"]'));
  const botCountInputs = Array.from(
    document.querySelectorAll<HTMLInputElement>('input[name="lobby-bot-count"]'));
  const botDifficultyInputs = Array.from(
    document.querySelectorAll<HTMLInputElement>('input[name="lobby-bot-difficulty"]'));
  const handCountInputs = Array.from(
    document.querySelectorAll<HTMLInputElement>('input[name="lobby-hand-count"]'));
  // Phase I Wave 4 — seat picker.  Values are "" (Auto, no preference),
  // "-1" (Spectate), or "0".."3" for explicit seats.
  const seatInputs = Array.from(
    document.querySelectorAll<HTMLInputElement>('input[name="lobby-seat"]'));

  const dealModeFieldset =
    document.getElementById('lobby-deal-mode-fieldset');
  const botDifficultyFieldset =
    document.getElementById('lobby-bot-difficulty-fieldset');
  if (dealModeFieldset === null || botDifficultyFieldset === null) return;
  // Phase I Wave 4 — the 4-bot radio (spectator-only) lives inside the
  // existing bot-count fieldset; we toggle it disabled per-seat below.
  const botCount4Input = botCountInputs.find(i => i.value === '4') ?? null;
  const spectatorHint = document.getElementById('lobby-spectator-hint');

  const seedInput =
    document.getElementById('lobby-seed') as HTMLInputElement | null;
  const seedError =
    document.getElementById('lobby-seed-error');
  const saveDefaultsInput =
    document.getElementById('lobby-save-defaults') as HTMLInputElement | null;
  const aboutLink =
    document.getElementById('lobby-about-link') as HTMLAnchorElement | null;

  // Ensure the About link always targets the canonical Known Limitations
  // doc, even if the markup is edited later — keeps the source of truth
  // here in the TS module.
  if (aboutLink !== null) {
    aboutLink.href = ABOUT_LINK_HREF;
    aboutLink.target = '_blank';
    aboutLink.rel = 'noopener noreferrer';
  }

  function readSeedField(): { seed: number | null; raw: string; valid: boolean } {
    if (seedInput === null) return { seed: null, raw: '', valid: true };
    const raw = seedInput.value;
    const trimmed = raw.trim();
    if (trimmed === '') return { seed: null, raw: '', valid: true };
    const seed = coerceSeed(trimmed);
    return { seed, raw: trimmed, valid: seed !== null };
  }

  function refreshSeedValidity(): boolean {
    if (seedInput === null) return true;
    const { valid } = readSeedField();
    seedInput.classList.toggle('lobby-seed-invalid', !valid);
    if (seedError !== null) {
      setElHidden(seedError, valid);
    }
    return valid;
  }

  function readPickers(): LobbyState {
    const v = variantInputs.find(i => i.checked)?.value ?? DEFAULTS.variant;
    const dm = dealModeInputs.find(i => i.checked)?.value ?? DEFAULTS.dealMode;
    const bcRaw = botCountInputs.find(i => i.checked)?.value ?? String(DEFAULTS.botCount);
    const bd = botDifficultyInputs.find(i => i.checked)?.value ?? DEFAULTS.botDifficulty;
    const hcRaw = handCountInputs.find(i => i.checked)?.value ?? String(DEFAULTS.handCount);
    const bcNum = parseInt(bcRaw, 10);
    const hcNum = parseInt(hcRaw, 10);
    const { seed } = readSeedField();
    // Phase I Wave 4 — seat radio: "" = Auto (null), "-1" = Spectate,
    // "0".."3" = explicit seat take.  Default to null when no radio is
    // checked (the picker is keyboard-navigable so a fresh user can leave
    // it on the default Auto radio).
    const seatRaw = seatInputs.find(i => i.checked)?.value ?? '';
    let seat: SeatChoice = null;
    if (seatRaw !== '') {
      const sNum = parseInt(seatRaw, 10);
      if (!isNaN(sNum) && isSeatChoice(sNum)) seat = sNum;
    }
    const botCount: BotCount = isBotCount(bcNum) ? bcNum : DEFAULTS.botCount;
    return {
      variant: isVariant(v) ? v : DEFAULTS.variant,
      dealMode: (dm === 'auto' ? 'auto' : 'manual'),
      botCount: clampBotCountForSeat(botCount, seat),
      botDifficulty: (bd === 'Easy' || bd === 'Medium' || bd === 'Master') ? bd : 'Hard',
      handCount: isHandCount(hcNum) ? hcNum : DEFAULTS.handCount,
      seed,
      seat,
    };
  }

  function writePickers(state: LobbyState): void {
    for (const i of variantInputs) i.checked = (i.value === state.variant);
    for (const i of dealModeInputs) i.checked = (i.value === state.dealMode);
    for (const i of botCountInputs) i.checked = (i.value === String(state.botCount));
    for (const i of botDifficultyInputs) i.checked = (i.value === state.botDifficulty);
    for (const i of handCountInputs) i.checked = (i.value === String(state.handCount));
    // Phase I Wave 4 — seat radio.  Null serialises to "" (the Auto
    // radio's value); -1/0/1/2/3 select the corresponding explicit radio.
    const seatValue = state.seat === null ? '' : String(state.seat);
    for (const i of seatInputs) i.checked = (i.value === seatValue);
    if (seedInput !== null) {
      seedInput.value = state.seed === null ? '' : String(state.seed);
    }
    refreshSeedValidity();
    refreshDisabledStates();
  }

  // Deal mode is Changsha-only; bot difficulty only matters when bots > 0;
  // 4-bot only when seat=-1 (spectator).  Use a CSS class to grey out the
  // fieldset and `disabled` on the inputs so screen readers + keyboard
  // users see the gating too.
  function refreshDisabledStates(): void {
    const s = readPickers();
    const isChangsha = s.variant === 'changsha';
    dealModeFieldset!.classList.toggle('lobby-disabled', !isChangsha);
    for (const i of dealModeInputs) i.disabled = !isChangsha;

    const hasBots = s.botCount > 0;
    botDifficultyFieldset!.classList.toggle('lobby-disabled', !hasBots);
    for (const i of botDifficultyInputs) i.disabled = !hasBots;

    // Phase I Wave 4 — the 4-bot radio is spectator-only; disable when
    // the seat picker isn't on Spectate.  Show the inline hint paragraph
    // only when Spectate is active.
    const isSpectator = s.seat === -1;
    if (botCount4Input !== null) {
      botCount4Input.disabled = !isSpectator;
      botCount4Input.parentElement?.classList.toggle('lobby-radio-disabled', !isSpectator);
    }
    if (spectatorHint !== null) {
      setElHidden(spectatorHint, !isSpectator);
    }
  }

  function showPanel(): void {
    panel!.classList.add('lobby-open');
    document.body.classList.add('lobby-active');
  }

  function hidePanel(): void {
    panel!.classList.remove('lobby-open');
    document.body.classList.remove('lobby-active');
  }

  // Initial population from URL > localStorage > defaults.
  writePickers(resolveInitialState());

  for (const i of variantInputs) i.addEventListener('change', refreshDisabledStates);
  for (const i of botCountInputs) i.addEventListener('change', refreshDisabledStates);
  // Phase I Wave 4 — when the user flips between Spectate and a seated
  // choice, snap botCount to the value that makes the new mode usable:
  //   • → Spectate : pre-select 4 bots so the all-bots-watch flow is one
  //                  click away (overwrites whatever was there — the
  //                  expectation is "spectate = watch four bots").
  //   • → seated   : clamp botCount=4 back down to 3 (the server cap for
  //                  any non-spectator connection).
  for (const i of seatInputs) {
    i.addEventListener('change', () => {
      const seatValue = i.checked ? i.value : '';
      const isSpectator = seatValue === '-1';
      if (isSpectator) {
        if (botCount4Input !== null) botCount4Input.disabled = false;
        for (const bc of botCountInputs) bc.checked = (bc.value === '4');
      } else {
        // Snap botCount=4 down to 3 if it was previously selected.
        const current = botCountInputs.find(b => b.checked)?.value ?? null;
        if (current === '4') {
          for (const bc of botCountInputs) bc.checked = (bc.value === '3');
        }
      }
      refreshDisabledStates();
    });
  }
  if (seedInput !== null) {
    seedInput.addEventListener('input', refreshSeedValidity);
    seedInput.addEventListener('blur', refreshSeedValidity);
  }

  toggle.addEventListener('click', () => {
    if (panel.classList.contains('lobby-open')) {
      hidePanel();
    } else {
      // Re-read URL+localStorage each time the user opens the lobby so the
      // pickers reflect whatever the active game is running with (or the
      // most recently saved defaults if no params).
      writePickers(resolveInitialState());
      showPanel();
    }
  });

  close.addEventListener('click', hidePanel);

  apply.addEventListener('click', () => {
    // Block apply on an invalid seed input so a typo doesn't get
    // silently dropped into a random seed without the user noticing.
    if (!refreshSeedValidity()) {
      seedInput?.focus();
      return;
    }
    const state = readPickers();
    // Phase K Wave 23 — Hicks (bundle-audit §3.8).  Lazy-load
    // `./lobby-url-io` for the URL builder + LS persister.  The
    // import + redirect happens within one user interaction —
    // browsers will not flag this as a navigation interruption.
    void (async () => {
      const mod = await import('./lobby-url-io');
      if (saveDefaultsInput !== null && saveDefaultsInput.checked) {
        mod.writeLocalStorageDefaults(state);
      }
      const url = mod.buildUrl(state);
      // replace() instead of assign() so the browser back-button doesn't
      // bounce the user between game configurations.
      window.location.replace(url);
    })();
  });

  // Phase J Wave 4 — Quick Match handler.  Bypasses the per-picker
  // review and snaps to "3 Medium bots, Auto seat, current variant +
  // dealMode + handCount".  Same exit path as Apply & Start so the
  // existing boot flow + backend validation pick the URL up unchanged.
  const quickMatchBtn = document.getElementById(
    'lobby-quick-match') as HTMLButtonElement | null;
  if (quickMatchBtn !== null) {
    quickMatchBtn.addEventListener('click', () => {
      const current = readPickers();
      const quick: LobbyState = {
        variant: current.variant,
        dealMode: current.dealMode,
        botCount: 3,
        botDifficulty: 'Medium',
        seed: null,
        handCount: current.handCount,
        seat: null,
      };
      // Phase K Wave 23 — same lazy-loaded URL builder.
      void (async () => {
        const mod = await import('./lobby-url-io');
        const url = mod.buildUrl(quick);
        window.location.replace(url);
      })();
    });
  }

  // Phase J Wave 4 — Settings shortcut.  Opens the settings drawer
  // without closing the lobby so a returning player can adjust
  // per-game overrides before clicking Apply.  We dispatch a synthetic
  // click on #settings-toggle so the existing setupSettingsDrawer wiring
  // in game-ui.ts handles the open animation + aria toggling.
  const openSettingsBtn = document.getElementById(
    'lobby-open-settings') as HTMLButtonElement | null;
  if (openSettingsBtn !== null) {
    openSettingsBtn.addEventListener('click', () => {
      const settingsToggle = document.getElementById(
        'settings-toggle') as HTMLButtonElement | null;
      settingsToggle?.click();
    });
  }

  // Phase J Wave 4 — install the renderers, then hand them to
  // attachLobbyClient (if a client is already attached) so the chip
  // strip + seat preview render against the live collections.
  //
  // Phase K Wave 23 — Hicks (bundle-audit §3.8).  The chip-strip +
  // seat-preview renderers are now lazy.  `scheduleLobbyPlayerChips
  // LazyMount` requests the chunk in the next idle window and
  // calls `bindLiveListeners()` once the refs land.
  scheduleLobbyPlayerChipsLazyMount();

  // Phase J Wave 5 — install the profile drawer + open-profile shortcut
  // button, then mount the lobby's new tab strip / public-games pane /
  // make-public toggle / stats panel.  These helpers are defined at the
  // bottom of this file; they are idempotent and bail out if their
  // anchor elements are missing.
  // Phase K Wave 21 — Hicks (bundle-audit §3.6).  `installProfileDrawer`
  // + `installProfileToggle` extracted into `./profile-drawer` and
  // lazy-mounted on first chip activation.  The lobby cold path
  // sheds ~6 KB raw / ~2 KB minified (the drawer's DOM-installation
  // graph) until the user shows intent to open their profile.
  scheduleProfileDrawerLazyMount();
  // Phase J Wave 7 — mount the app-wide settings drawer (separate from
  // the Wave-2 per-game settings panel) and the player profile page
  // overlay.  Both are idempotent and intercept clicks before the
  // legacy Wave-5 drawer handler.
  //
  // Phase K Wave 17 — bundle audit §3.2 (Hicks).  Both surfaces are
  // now lazy-mounted: settings-drawer.ts (~35 KB raw) loads on the
  // first `settings-button` hover/focus/click, and profile-page.ts
  // (~21 KB raw) loads on the first `lobby-open-profile` chip
  // hover/focus/click or `mahjong:open-profile-page` custom event
  // dispatch.  Lobby cold path never pays for either until the user
  // reaches the activation surface.
  scheduleSettingsDrawerLazyMount();
  scheduleProfilePageLazyMount();
  // Phase J Wave 8 — install display-pref body classes (reduced-motion,
  // theme-dark / theme-light) before the auth UI / rule-presets pickers
  // render so the chrome paints with the correct palette immediately.
  //
  // Phase K Wave 23 — Hicks (bundle-audit §3.8).  Lazy-mounted: an
  // inline best-effort sync probe paints the dark/light + reduced-
  // motion classes from a single LS read; the full theme module
  // (with the matchMedia change-listener) lands in a microtask.
  scheduleDisplayPreferencesLazyMount();
  // Phase K Wave 20 — Hicks (bundle-audit §3.5).  `installAuthUi()`
  // is now lazy-mounted behind `scheduleAuthUiLazyMount()` (defined
  // at the bottom of this file).  Auth state subscribers (the
  // header chip, linked-accounts panel) live in `./auth` and the
  // chunk is dropped from the eager bundle; the lobby first-paint
  // renders without waiting for the auth-UI graph.  Sign-in chip
  // appears as soon as the idle-window callback resolves the
  // dynamic import (~1 paint after first paint on a fresh load).
  scheduleAuthUiLazyMount();
  // Phase K Wave 19 — bundle audit §3.4: rule-presets editor surface
  // (~12.5 KB minified) is deferred to the next idle window so the
  // lobby first-paint never blocks on it.  The picker's <select> is
  // present in the static HTML so the user sees the chrome
  // immediately; `installRulePresetsUi()` populates the options +
  // wires the change handler when the chunk lands.
  scheduleRulePresetsUiLazyMount();
  // Phase K Wave 18 — Hicks (bundle-audit §3.3): lazy-mounted on a
  // URL probe.  Spectator-only surface; non-spectator lobby
  // visitors shed the ~5 KB chunk entirely.
  void scheduleSpectatorFollowLazyMount();
  // Phase J Wave 6 — seed profile.ts.current from the localStorage
  // cache so the lobby chip shows the previously-saved displayName
  // *before* the SignalR hub connects (which only happens once the
  // user enters a game).  Without this, a fresh lobby visit after a
  // previous-session onboarding shows the default "Profile" text
  // until the hub round-trip eventually lands.
  hydrateProfileFromCacheIfAvailable();
  // Phase K Wave 23 — Hicks (bundle-audit §3.8).  `installLobbyTabs`
  // is now lazy-loaded so the eager bundle sheds the ~80-LoC body
  // + tab-activation switch.  Tab clicks are eagerly bound by the
  // schedule helper's micro-stub; the full module lands on the
  // next microtask after lobby first-paint.
  scheduleLobbyTabsLazyMount();
  // Phase K Wave 19 — bundle audit §3.4: public-games pane + make-
  // public toggle are now lazy-mounted behind tab/toggle activation.
  // `matchmaking.ts` (~7.7 KB minified incl. polling loop + REST
  // wrappers) is only pulled when the user activates the Public-
  // Games tab OR touches the make-public toggle.  See
  // `schedulePublicGamesPaneLazyMount` / `scheduleMakePublicToggle
  // LazyMount` at the bottom of this file.
  schedulePublicGamesPaneLazyMount();
  scheduleMakePublicToggleLazyMount();
  // Phase K Wave 23 — Hicks (bundle-audit §3.8).  `installLobby
  // StatsPanel` + `renderLobbyStatsPanel` are now lazy-loaded.
  // The chunk lands in a microtask after first-paint; the empty
  // host element is left blank in the interim (the SignalR
  // profile arrives after ~1 RTT anyway, well after the chunk
  // lands).  The same chunk also exports the W23-lazified
  // `installSoundEnabledMirror`.
  scheduleLobbyStatsPanelLazyMount();

  // Phase J Wave 6 — kick off the cookie-bound identity bootstrap +
  // mount the onboarding card + leaderboard surface.  bootstrapIdentity
  // is idempotent (the in-flight POST is deduped), so calling it from
  // both index.ts and here is safe.  installOnboardingCard reads the
  // first-visit hint from the bootstrap result; we also refresh the
  // visibility once the identity arrives in case the identity lands
  // after the lobby is mounted.
  //
  // Phase K Wave 22 — Hicks bundle-audit §3.7.  The installer
  // (~7 KB minified, with the Continue → SignalR UpdateProfile path
  // + 8-swatch preset grid) lives in the lazy `identity-onboarding`
  // chunk.  `refreshOnboardingVisibility` is kept eager because it
  // is a tiny show/hide helper that the lobby also calls on every
  // identity event.  Returning users (LS_KEY_ONBOARDED present) see
  // `shouldShowOnboarding === false`, so we short-circuit BEFORE
  // pulling the chunk to keep the cold path of returning users
  // truly free of the install code.
  void scheduleOnboardingLazyMount();
  // Phase K Wave 17 — bundle audit §3.2 (Hicks).  leaderboard.ts
  // (~27 KB raw) is lazy-loaded on the first activation of the
  // `lobby-leaderboard-tab` button; tab-activate transitions in
  // `installLobbyTabs` delegate poll start/stop to the same lazy
  // module via `loadLeaderboard()`.  Lobby cold path no longer
  // pays for the leaderboard surface or its 30 s poll loop.
  scheduleLeaderboardLazyMount();
  void bootstrapIdentity().then(() => {
    refreshOnboardingVisibility();
  });
  onIdentity(() => {
    refreshOnboardingVisibility();
  });

  // Phase J Wave 6 — sound-toggle localStorage mirror.  The Wave-3
  // settings drawer persists the sound knob inside a JSON-encoded
  // payload at `autotable.phaseJ.v1.settings.*`; the Wave-6 directive
  // also wants a discoverable scalar key (`mahjong:soundEnabled`) so
  // tests and external integrations can flip / read the state without
  // parsing JSON.
  //
  // Phase K Wave 23 — Hicks: lazy-mounted alongside the stats panel
  // (same `./lobby-stats-panel` chunk).  Sched helper triggers the
  // import on `?` or `lobby-stats-panel` host visibility.

  if (shouldShowOnLoad()) showPanel();
}

// ---------------------------------------------------------------------
// Phase J Wave 4 — deferred-bind helpers.
//
// initLobby() runs before the Client / Game lifecycle has booted (so
// the Quick Match button is clickable immediately).  attachLobbyClient
// is called from index.ts after Game.start() so the chip strip + seat
// preview can subscribe to the live `seats` + `nicks` collections.
// Module-level state mirrors the renderers + the attached client so a
// second initLobby() call doesn't double-bind listeners.
// ---------------------------------------------------------------------

let _attachedClient: Client | null = null;
let _renderPlayerChips: ((client: Client) => void) | null = null;
let _renderSeatPreview: ((client: Client) => void) | null = null;
let _liveBound: boolean = false;

export function attachLobbyClient(client: Client): void {
  _attachedClient = client;
  if (_renderPlayerChips !== null && _renderSeatPreview !== null) {
    bindLiveListeners(client);
  }
}

function bindLiveListeners(client: Client): void {
  if (_liveBound) return;
  _liveBound = true;
  const renderAll = (): void => {
    _renderPlayerChips?.(client);
    _renderSeatPreview?.(client);
  };
  client.seats.on('update', renderAll);
  client.nicks.on('update', renderAll);
  // Phase J Wave 5 — re-render chips + stats panel when the local
  // profile changes so the displayName + avatarColor override the
  // WS-broadcast nick / djb2 hue immediately.
  //
  // Phase K Wave 23 — `renderLobbyStatsPanel` is now lazy.  When the
  // chunk hasn't landed yet, the call is a no-op; the panel paints
  // on the next onProfile tick after the module arrives.  See
  // `scheduleLobbyStatsPanelLazyMount()` below.
  onProfile(() => {
    renderAll();
    if (_lobbyStatsPanelMod !== null) {
      _lobbyStatsPanelMod.renderLobbyStatsPanel();
    }
  });
  renderAll();
}

// Render the lobby's player chip strip from the live `seats` + `nicks`
// collections.  Phase K Wave 23 — Hicks (bundle-audit §3.8).
// Extracted to `./lobby-player-chips.ts` so the ~3 KB of chip-
// builder + profile-aware resolver code ships as a lazy chunk.
// The lobby cold path goes through `scheduleLobbyPlayerChipsLazy
// Mount` below, which loads the chunk in the next idle window
// and assigns `_renderPlayerChips` / `_renderSeatPreview`.



// ---------------------------------------------------------------------
// Phase J Wave 5 — lobby tab strip.
//
// The lobby panel now has two top-level tabs: "My Game" (the existing
// quick-match / seat-take / settings surface) and "Public Games" (the
// new matchmaking browser).  The strip is rendered in index.html; this
// helper just wires the click handlers and toggles `style.display` of
// the two panes (#lobby-tab-my-game / #lobby-tab-public-games).
//
// Tab activation also starts/stops matchmaking polling so the REST
// endpoint isn't hammered while the user is on the My-Game tab.
// ---------------------------------------------------------------------

// Phase K Wave 23 — Hicks (bundle-audit §3.8).  `installLobbyTabs`
// extracted to `./lobby-tabs.ts`; lobby reaches it via the lazy
// `scheduleLobbyTabsLazyMount()` helper below.

// ---------------------------------------------------------------------
// Phase J Wave 5 — public-games pane.
//
// Phase K Wave 23 — Hicks (bundle-audit §3.8).  Extracted to
// `./lobby-public-games-pane.ts` so the ~4 KB of public-games DOM
// + RPC plumbing ships as a lazy `lobby-public-games-pane.<hash>.js`
// chunk.  The W23 schedule helpers (below) gate the import on
// public-games-tab hover / make-public-toggle hover so the eager
// hot path never pays for code only some users hit.
// ---------------------------------------------------------------------

// ---------------------------------------------------------------------
// Phase J Wave 5 — lobby stats panel.
//
// Phase K Wave 23 — Hicks (bundle-audit §3.8).  `installLobby
// StatsPanel` + `renderLobbyStatsPanel` are extracted to
// `./lobby-stats-panel.ts` and lazy-mounted via the
// `scheduleLobbyStatsPanelLazyMount()` helper below.  The
// renderLobbyStatsPanel call inside `bindLiveListeners` now goes
// through `_lobbyStatsPanelMod` (which is null until the chunk
// lands; the panel paints on the first onProfile tick after that).
// ---------------------------------------------------------------------

// Phase K Wave 23 — see scheduleLobbyStatsPanelLazyMount below for
// the rest of the W23 stats-panel + sound-mirror plumbing.

// ---------------------------------------------------------------------
// Phase K Wave 17 — bundle audit §3.2 (Hicks).
//
// Three former eager imports now lazy-mount behind interaction-surface
// triggers.  The pattern mirrors W16's §3.1 / §3.5 schedule-helpers in
// `index.ts` (audit / tournaments / history / tour / spectator /
// avatar-migration / sentry / action-router):  each helper installs a
// minimal listener set on the activation element + does the dynamic
// `import()` exactly once on first activation, then calls the
// module's `install…` entry to wire its full handlers.
//
// Module-handle cache survives across `initLobby()` re-invocations so
// the activate-tab transition (which can fire many times) doesn't
// re-import on every switch.  The cache also lets the tab transition
// `stop…Polling` call run only when the module has already loaded —
// no point importing the chunk just to call a no-op stopper.
// ---------------------------------------------------------------------

let _leaderboardMod: typeof LeaderboardModule | null = null;
let _profilePageMod: typeof ProfilePageModule | null = null;
let _profilePageEventQueue: CustomEvent[] = [];

async function loadLeaderboard(): Promise<typeof LeaderboardModule> {
  if (_leaderboardMod === null) {
    _leaderboardMod = await import('./leaderboard');
  }
  return _leaderboardMod;
}

async function loadProfilePage(): Promise<typeof ProfilePageModule> {
  if (_profilePageMod === null) {
    _profilePageMod = await import('./profile-page');
    _profilePageMod.installProfilePage();
    // Drain any open-profile-page events that arrived before the
    // module finished importing.  This handles the rare race where a
    // leaderboard "View" click fires before profile-page.ts has had
    // time to register its own listener.
    for (const ev of _profilePageEventQueue) {
      window.dispatchEvent(ev);
    }
    _profilePageEventQueue = [];
  }
  return _profilePageMod;
}

function scheduleLeaderboardLazyMount(): void {
  const tab = document.getElementById('lobby-leaderboard-tab');
  if (tab === null) return;
  let loaded = false;
  const load = async (): Promise<void> => {
    if (loaded) return;
    loaded = true;
    const mod = await loadLeaderboard();
    mod.installLeaderboardSurface();
  };
  tab.addEventListener('mouseenter', () => { void load(); }, { once: true });
  tab.addEventListener('focus', () => { void load(); }, { once: true });
  tab.addEventListener('click', () => { void load(); }, { once: true });
}

// Phase K Wave 22 — Hicks bundle-audit §3.7.  The onboarding card
// installer (~7 KB minified) lives in the lazy `identity-onboarding`
// chunk.  Returning users have `LS_KEY_ONBOARDED === 'true'` so
// `shouldShowOnboarding()` short-circuits to false BEFORE the
// dynamic-import — those users never pay for the chunk.  First-
// visit users (LS flag absent) lazy-load the installer immediately,
// which mounts the card if the markup is present.
async function scheduleOnboardingLazyMount(): Promise<void> {
  let needed = true;
  try {
    needed = window.localStorage.getItem(
      'mahjong.identity.onboarded.v1') !== 'true';
  } catch { /* private mode — assume needed, fail-open */ }
  if (!needed) return;
  if (document.getElementById('onboarding-card') === null) return;
  try {
    const mod = await import('./identity-onboarding');
    mod.installOnboardingCard();
  } catch { /* fail-open: never block lobby boot on onboarding */ }
}

// Phase K Wave 18 — Hicks (bundle-audit §3.3).  Spectator follow-
// seat helper is only meaningful for spectators (URL ?seat=-1) or
// the deep-link spectator route (?action=spectate / ?spectate).  We
// probe the URL synchronously and only dynamic-import the module
// when the gate matches.  Non-spectator lobby visitors shed the
// ~5 KB chunk entirely.  The module installs the floating
// "Spectator" panel + keyboard shortcuts + show-all-hands toggle.
function scheduleSpectatorFollowLazyMount(): void {
  let isSpectatorPath = false;
  try {
    const s = window.location.search;
    isSpectatorPath = /[?&]seat=-1\b/.test(s) || /[?&](?:spectate|action=spectate)\b/.test(s);
  } catch { /* offline-ish — treat as non-spectator */ }
  if (!isSpectatorPath) return;
  void (async (): Promise<void> => {
    try {
      const mod = await import('./spectator-follow');
      mod.installSpectatorFollow();
    } catch { /* fail-open — follow helper is best-effort */ }
  })();
}

function scheduleSettingsDrawerLazyMount(): void {
  const btn = document.getElementById('settings-button');
  if (btn === null) return;
  let loaded = false;
  // First-click pre-warm: the module's own click handler is attached
  // inside `installSettingsDrawerV2()`, so the very first click only
  // triggers the load.  We re-fire `openDrawer()` post-import so the
  // user's first click also opens the drawer (no double-tap).
  const load = async (opts: { openOnLoad: boolean } = { openOnLoad: false }): Promise<void> => {
    if (loaded) return;
    loaded = true;
    const mod = await import('./settings-drawer');
    mod.installSettingsDrawerV2();
    if (opts.openOnLoad) {
      // The module's openDrawer/closeDrawer are file-private; we get
      // the same effect by dispatching a synthetic click after install.
      const reBtn = document.getElementById('settings-button') as HTMLButtonElement | null;
      // Use the drawer's open class to detect whether the install's
      // own listener already opened it (e.g. fast user double-fired
      // click → install's listener captured the second one).
      const drawer = document.getElementById('settings-drawer-v2');
      const alreadyOpen = drawer?.classList.contains('settings-drawer-v2-open') ?? false;
      if (!alreadyOpen && reBtn !== null) reBtn.click();
    }
  };
  btn.addEventListener('mouseenter', () => { void load(); }, { once: true });
  btn.addEventListener('focus', () => { void load(); }, { once: true });
  btn.addEventListener('click', () => { void load({ openOnLoad: true }); }, { once: true });
}

function scheduleProfilePageLazyMount(): void {
  const chip = document.getElementById('lobby-open-profile');
  // Eager event listener (~30 LoC of eager bytes) so leaderboard
  // "View" clicks queue up correctly even before the profile chip is
  // hovered.  Capture-phase mirrors the original installProfilePage()
  // capture-phase chip listener so we suppress the legacy Wave-5
  // drawer handler the same way.
  let chipPreOpen = false;
  const handleChip = (e: Event): void => {
    e.preventDefault();
    e.stopImmediatePropagation();
    chipPreOpen = true;
    void loadProfilePage().then((mod) => {
      // Re-call openProfilePage() if the install path didn't already
      // open it (install's own chip listener fires only on subsequent
      // clicks since we used `{ once: true }` here).
      mod.openProfilePage();
    });
  };
  if (chip !== null) {
    chip.addEventListener('click', handleChip, { capture: true, once: true });
    chip.addEventListener('mouseenter', () => { void loadProfilePage(); }, { once: true });
    chip.addEventListener('focus', () => { void loadProfilePage(); }, { once: true });
  }

  // Cross-module open event (raised by leaderboard rows + replay
  // viewer).  Queue any events that arrive before the module loads;
  // `loadProfilePage()` drains the queue post-install.
  window.addEventListener('mahjong:open-profile-page', ((e: Event) => {
    if (_profilePageMod !== null) {
      // Module already loaded — its own handler will run; nothing to do.
      return;
    }
    const ce = e as CustomEvent;
    // Clone the event so the dispatch loop receives an event with
    // identical detail (the original is consumed by the listener
    // chain).
    _profilePageEventQueue.push(new CustomEvent(ce.type, { detail: ce.detail }));
    void loadProfilePage();
  }) as EventListener);

  // Avoid an unused-variable warning when the chip is missing.
  void chipPreOpen;
}

// Phase K Wave 21 — Hicks (bundle-audit §3.6).
//
// `profile-drawer.ts` (~6 KB raw / ~2 KB minified) is now lazy-
// loaded behind `scheduleProfileDrawerLazyMount`.  The drawer is
// the legacy Wave-5 side surface; on modern paths it is hidden
// behind `./profile-page` (Wave 7+) which intercepts the
// `lobby-open-profile` chip click in CAPTURE phase, so the
// drawer's chip handler is effectively dormant.  The drawer's
// DOM event handlers (close, save, name input, color picker,
// presets, custom color) still wire up on first activation so
// any third-party flow that calls `openProfileDrawer()`
// programmatically continues to work.
//
// Mount triggers (chip hover / focus / click) parallel the W17
// §3.2 `scheduleProfilePageLazyMount` so both modules load
// together when the user shows intent to open their profile.

let _profileDrawerMod: typeof ProfileDrawerModule | null = null;

async function loadProfileDrawer(): Promise<typeof ProfileDrawerModule> {
  if (_profileDrawerMod !== null) return _profileDrawerMod;
  _profileDrawerMod = await import('./profile-drawer');
  // Idempotent — bails out on the second call inside the module.
  _profileDrawerMod.installProfileDrawer();
  _profileDrawerMod.installProfileToggle();
  return _profileDrawerMod;
}

function scheduleProfileDrawerLazyMount(): void {
  const chip = document.getElementById('lobby-open-profile');
  if (chip === null) return;
  chip.addEventListener('mouseenter', () => { void loadProfileDrawer(); }, { once: true });
  chip.addEventListener('focus', () => { void loadProfileDrawer(); }, { once: true });
  chip.addEventListener('click', () => { void loadProfileDrawer(); }, { once: true });
}

// ---------------------------------------------------------------------
// Phase K Wave 19 — bundle audit §3.4 (Hicks).
//
// `matchmaking.ts` and `rule-presets.ts` are now lazy-loaded so the
// lobby cold path never pulls them in.
//
//   • matchmaking (~7.7 KB minified) — only loaded when the user
//     activates the Public-Games tab OR touches the make-public
//     toggle.  Both surfaces are wired by `schedulePublicGamesPane
//     LazyMount` / `scheduleMakePublicToggleLazyMount` below; once
//     either fires, the module is cached on `_matchmakingMod` for
//     the rest of the session.  The tab-activate handler in
//     `installLobbyTabs.activate` consults `_matchmakingMod` directly
//     (start polling on first 'public' activate; stop polling on
//     other-tab activate ONLY IF the module is already loaded —
//     skipping the import for a no-op stopper).
//
//   • rule-presets (~12.5 KB minified incl. its EventEmitter
//     dependency) — only loaded inside a `requestIdleCallback`
//     after lobby first-paint.  The picker `<select>` is in the
//     static HTML; the module populates options + wires the change
//     handler when the chunk lands.  The URL-builder's
//     `getSelectedPresetId` call is replaced by an inline LS read
//     so the URL still emits `?rulePreset=` for non-default
//     selections without dragging in the editor surface.
//
// W18 delivered eager = 156,577 B (+11.5 KB over the §6.3 ceiling
// of 145,000 B).  These two lazifications shed ~20 KB combined —
// see `docs/lh13-soft-pin-rationale.md` §10 for the W19 outcome
// table.
// ---------------------------------------------------------------------

// Phase K Wave 23 — Hicks (bundle-audit §3.8).  `readSelectedPreset
// IdInline` + `RULE_PRESET_LS_KEY` / `RULE_PRESET_DEFAULT_ID`
// constants moved to `./lobby-url-io.ts` (only `buildUrl` used
// them).  rule-presets.ts owns its own canonical copy; both files
// must agree on the LS key by inspection (no shared constants
// module because pulling rule-presets.ts into the eager bundle is
// what bundle-audit §3.4 / §3.8 are trying to avoid).


let _matchmakingMod: typeof MatchmakingModule | null = null;
let _matchmakingLoading: Promise<typeof MatchmakingModule> | null = null;

async function loadMatchmaking(): Promise<typeof MatchmakingModule> {
  if (_matchmakingMod !== null) return _matchmakingMod;
  if (_matchmakingLoading !== null) return _matchmakingLoading;
  _matchmakingLoading = import('./matchmaking').then((m) => {
    _matchmakingMod = m;
    return m;
  });
  return _matchmakingLoading;
}

// Phase K Wave 23 — public-games pane chunk handle (lazy).  Lives in
// `./lobby-public-games-pane.ts`; only loaded when the user hovers
// the public-games tab or interacts with the make-public toggle.
let _publicGamesPaneMod:
  typeof import('./lobby-public-games-pane') | null = null;
let _publicGamesPaneLoading:
  Promise<typeof import('./lobby-public-games-pane')> | null = null;

function loadPublicGamesPane():
  Promise<typeof import('./lobby-public-games-pane')> {
  if (_publicGamesPaneMod !== null) return Promise.resolve(_publicGamesPaneMod);
  if (_publicGamesPaneLoading !== null) return _publicGamesPaneLoading;
  _publicGamesPaneLoading = import('./lobby-public-games-pane').then((m) => {
    _publicGamesPaneMod = m;
    return m;
  });
  return _publicGamesPaneLoading;
}

function schedulePublicGamesPaneLazyMount(): void {
  const pubTab = document.getElementById(
    'lobby-public-games-tab') as HTMLButtonElement | null;
  if (pubTab === null) return;
  let installed = false;
  const install = async (): Promise<void> => {
    if (installed) return;
    installed = true;
    const [mm, mod] = await Promise.all([loadMatchmaking(), loadPublicGamesPane()]);
    mod.installPublicGamesPane(mm);
  };
  pubTab.addEventListener('mouseenter', () => { void install(); }, { once: true });
  pubTab.addEventListener('focus', () => { void install(); }, { once: true });
  pubTab.addEventListener('click', () => { void install(); }, { once: true });
}

function scheduleMakePublicToggleLazyMount(): void {
  const toggle = document.getElementById(
    'lobby-make-public-toggle') as HTMLInputElement | null;
  const nameInput = document.getElementById(
    'lobby-make-public-name') as HTMLInputElement | null;
  if (toggle === null || nameInput === null) return;
  let installed = false;
  const install = async (replayChange: boolean): Promise<void> => {
    if (installed) return;
    installed = true;
    const [mm, mod] = await Promise.all([loadMatchmaking(), loadPublicGamesPane()]);
    mod.installMakePublicToggle(mm);
    if (replayChange) {
      // The user clicked the toggle BEFORE the module was loaded; the
      // checked-state has already flipped (browser default), but the
      // change listener installed by `installMakePublicToggle` did not
      // fire.  Dispatch a synthetic change event so the make-public
      // RPC is invoked exactly once on first activation.
      toggle.dispatchEvent(new Event('change'));
    }
  };
  // Activation surfaces — any hover/focus on the toggle or the name
  // input warms the module; an actual change-click also replays the
  // change event after install so the user's first click is honoured.
  toggle.addEventListener('mouseenter', () => { void install(false); }, { once: true });
  toggle.addEventListener('focus', () => { void install(false); }, { once: true });
  toggle.addEventListener('change', () => { void install(true); }, { once: true });
  nameInput.addEventListener('focus', () => { void install(false); }, { once: true });
}

let _rulePresetsMod: typeof RulePresetsModule | null = null;

function scheduleRulePresetsUiLazyMount(): void {
  if (_rulePresetsMod !== null) return;
  const load = async (): Promise<void> => {
    if (_rulePresetsMod !== null) return;
    try {
      _rulePresetsMod = await import('./rule-presets');
      _rulePresetsMod.installRulePresetsUi();
    } catch { /* fail-open — picker stays inert if the chunk can't load */ }
  };
  // Prefer `requestIdleCallback` so the picker mounts in the next
  // idle window; fall back to a microtask-ish timeout for browsers
  // that lack the API (Safari).
  const ric = (window as { requestIdleCallback?: (cb: () => void, opts?: { timeout: number }) => number }).requestIdleCallback;
  if (typeof ric === 'function') {
    ric(() => { void load(); }, { timeout: 1500 });
  } else {
    window.setTimeout(() => { void load(); }, 0);
  }
}

// Phase K Wave 23 — Hicks (bundle-audit §3.8).  `loadStatsModule`
// has migrated to `./lobby-stats-panel.ts` (which owns the
// formatter trigger now that the panel render is also lazy).
// The eager bundle no longer pays for the stats-loader closure
// nor the `_statsMod`/`_statsLoading` module-level state.

// ── W23 lazy helpers (lobby-tabs + lobby-stats-panel + theme) ────────

import type * as LobbyTabsModule from './lobby-tabs';
import type * as LobbyStatsPanelModule from './lobby-stats-panel';

let _lobbyTabsMod: typeof LobbyTabsModule | null = null;
let _lobbyStatsPanelMod: typeof LobbyStatsPanelModule | null = null;
let _themeMod: typeof ThemeModule | null = null;

function scheduleLobbyTabsLazyMount(): void {
  // Phase K Wave 23 — Hicks (bundle-audit §3.8).  Lazy-mount the
  // tab-strip installer.  The schedule helper triggers on the
  // earliest of:  microtask after lobby first-paint, or any
  // explicit click/hover/focus on the four lobby tab buttons (to
  // avoid a perceptible flash if the microtask is starved by
  // long-running auth-UI work).
  if (_lobbyTabsMod !== null) return;
  const tabs = [
    'lobby-my-game-tab',
    'lobby-public-games-tab',
    'lobby-leaderboard-tab',
    'lobby-tournaments-tab',
  ];
  const load = async (): Promise<void> => {
    if (_lobbyTabsMod !== null) return;
    try {
      _lobbyTabsMod = await import('./lobby-tabs');
      _lobbyTabsMod.installLobbyTabs({
        loadMatchmaking: loadMatchmaking,
        getMatchmakingModule: (): LobbyTabsModule.MatchmakingPollHandle | null =>
          _matchmakingMod === null ? null : _matchmakingMod,
        loadLeaderboard: loadLeaderboard,
        getLeaderboardModule: (): LobbyTabsModule.LeaderboardPollHandle | null =>
          _leaderboardMod === null ? null : _leaderboardMod,
      });
    } catch { /* fail-open — tabs degrade to default visible state */ }
  };
  // Prefer requestIdleCallback so the installer mounts in the next
  // idle window; fall back to setTimeout for Safari.
  const ric = (window as { requestIdleCallback?: (cb: () => void, opts?: { timeout: number }) => number }).requestIdleCallback;
  if (typeof ric === 'function') {
    ric(() => { void load(); }, { timeout: 800 });
  } else {
    window.setTimeout(() => { void load(); }, 0);
  }
  // Belt-and-braces: any pointer activity on a tab button triggers
  // the load too, so we never miss the user's first click.
  for (const id of tabs) {
    const btn = document.getElementById(id);
    if (btn === null) continue;
    btn.addEventListener('mouseenter', () => { void load(); }, { once: true });
    btn.addEventListener('focus', () => { void load(); }, { once: true });
    btn.addEventListener('click', () => { void load(); }, { once: true });
  }
}

function scheduleLobbyStatsPanelLazyMount(): void {
  // Phase K Wave 23 — Hicks (bundle-audit §3.8).  Lazy-mount the
  // stats panel + sound-mirror.  Triggers on the next idle window
  // (the chunk lands well before the SignalR ProfileLoaded payload
  // arrives, so the panel still paints with profile data on the
  // first onProfile tick).
  if (_lobbyStatsPanelMod !== null) return;
  const load = async (): Promise<void> => {
    if (_lobbyStatsPanelMod !== null) return;
    try {
      _lobbyStatsPanelMod = await import('./lobby-stats-panel');
      _lobbyStatsPanelMod.installLobbyStatsPanel();
      _lobbyStatsPanelMod.installSoundEnabledMirror();
    } catch { /* fail-open — panel stays empty until next initLobby */ }
  };
  const ric = (window as { requestIdleCallback?: (cb: () => void, opts?: { timeout: number }) => number }).requestIdleCallback;
  if (typeof ric === 'function') {
    ric(() => { void load(); }, { timeout: 1500 });
  } else {
    window.setTimeout(() => { void load(); }, 0);
  }
}

function scheduleDisplayPreferencesLazyMount(): void {
  // Phase K Wave 23 — Hicks (bundle-audit §3.8).  Apply a best-
  // effort synchronous body-class probe so the chrome paints with
  // the correct theme palette immediately (no FOUC), then load
  // the full `./theme` module (with the matchMedia change-
  // listener) in a microtask.
  if (_themeMod !== null) return;
  // Inline sync paint — mirrors `./theme.ts:apply()` for the
  // user-overridable cases.  Pure LS read + matchMedia probe; no
  // module deps.
  try {
    const raw = window.localStorage.getItem('mahjong.display.v1');
    let motion: 'auto' | 'reduced' | 'full' = 'auto';
    let theme: 'auto' | 'light' | 'dark' = 'auto';
    if (raw !== null) {
      const j = JSON.parse(raw) as { motion?: string; theme?: string };
      if (j.motion === 'reduced' || j.motion === 'full') motion = j.motion;
      if (j.theme === 'light' || j.theme === 'dark') theme = j.theme;
    }
    const body = document.body;
    if (body !== null) {
      const osReduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
      const osDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      const reduced = motion === 'reduced' || (motion === 'auto' && osReduced);
      const dark = theme === 'dark' || (theme === 'auto' && osDark);
      const light = theme === 'light' || (theme === 'auto' && !osDark);
      body.classList.toggle('reduced-motion', reduced);
      body.classList.toggle('full-motion', motion === 'full');
      body.classList.toggle('theme-dark', dark);
      body.classList.toggle('theme-light', light);
    }
  } catch { /* swallow — full module will retry on land */ }
  const load = async (): Promise<void> => {
    if (_themeMod !== null) return;
    try {
      _themeMod = await import('./theme');
      _themeMod.installDisplayPreferences();
    } catch { /* fail-open — body classes already set above */ }
  };
  // Microtask is enough — the inline probe has already painted.
  window.setTimeout(() => { void load(); }, 0);
}

// Phase K Wave 20 — bundle audit §3.5: auth-UI lazy mount.
//
// The `./auth` module (~16-20 KB minified after rollup's chunker
// applies its tree-shake to the lobby-static import graph) was the
// last >10 KB eager dep blocking the W20 ≤135 KB target for
// `autotable-src-eager`.  We defer `installAuthUi()` to the next
// idle window so the lobby first-paint never waits on the auth-UI
// chunk download/parse.
//
// User-visible effect: the sign-in header chip ("Sign in" button)
// appears one paint frame later than it did at W19.  Anonymous
// lobby visitors never interact with auth before that frame; the
// degraded UX is bounded to ~1 frame (~16 ms on a fresh load,
// since rollup's content-hashed chunks are pre-cached after first
// visit).
//
// Cross-module impact: `rule-presets.ts` (already lazy as of W19)
// statically imports `getAuthState/onAuth` from `./auth`.  When
// both consumers go dynamic, rollup either emits a shared chunk
// for `./auth` OR collapses it into whichever lazy chunk loads
// first.  Either way, the eager bundle no longer carries it.  See
// `docs/frontend-bundle-audit.md §3.5` for the W20 measurement.
let _authMod: typeof AuthModule | null = null;

function scheduleAuthUiLazyMount(): void {
  if (_authMod !== null) return;
  const load = async (): Promise<void> => {
    if (_authMod !== null) return;
    try {
      _authMod = await import('./auth');
      _authMod.installAuthUi();
    } catch { /* fail-open — anonymous lobby still functional */ }
  };
  // Prefer `requestIdleCallback` so the auth chip mounts in the
  // next idle window; fall back to a microtask-ish timeout for
  // browsers that lack the API (Safari).
  const ric = (window as { requestIdleCallback?: (cb: () => void, opts?: { timeout: number }) => number }).requestIdleCallback;
  if (typeof ric === 'function') {
    ric(() => { void load(); }, { timeout: 1500 });
  } else {
    window.setTimeout(() => { void load(); }, 0);
  }
}

// ── W23 lazy helpers (lobby-player-chips) ─────────────────────────────

import type * as LobbyPlayerChipsModule from './lobby-player-chips';

let _lobbyPlayerChipsMod: typeof LobbyPlayerChipsModule | null = null;

function scheduleLobbyPlayerChipsLazyMount(): void {
  // Phase K Wave 23 — Hicks (bundle-audit §3.8).  Lazy-mount the
  // chip-strip + seat-preview renderers.  These render the live
  // `seats` + `nicks` collections, so we must load the chunk
  // before the bind-live listeners can fire.  We do this in the
  // next idle window (or microtask) so the lobby cold path doesn't
  // pay for the ~3 KB on first paint.
  if (_lobbyPlayerChipsMod !== null) return;
  const load = async (): Promise<void> => {
    if (_lobbyPlayerChipsMod !== null) return;
    try {
      _lobbyPlayerChipsMod = await import('./lobby-player-chips');
      _renderPlayerChips = _lobbyPlayerChipsMod.renderPlayerChips;
      _renderSeatPreview = _lobbyPlayerChipsMod.renderSeatPreview;
      if (_attachedClient !== null) {
        bindLiveListeners(_attachedClient);
        // Force a paint now that the renderers are wired — the live
        // listener bind no-ops if it ran before (idempotent), but
        // the chip strip needs an explicit render-now since the
        // seats/nicks tick may have fired before the chunk landed.
        _lobbyPlayerChipsMod.renderPlayerChips(_attachedClient);
        _lobbyPlayerChipsMod.renderSeatPreview(_attachedClient);
      }
    } catch { /* fail-open — chip strip stays empty if chunk fails */ }
  };
  const ric = (window as { requestIdleCallback?: (cb: () => void, opts?: { timeout: number }) => number }).requestIdleCallback;
  if (typeof ric === 'function') {
    ric(() => { void load(); }, { timeout: 1000 });
  } else {
    window.setTimeout(() => { void load(); }, 0);
  }
}
