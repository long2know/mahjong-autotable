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
import {
  startPolling as startMatchmakingPolling,
  stopPolling as stopMatchmakingPolling,
  isPolling as isMatchmakingPolling,
  getCachedGames,
  getLastError as getMatchmakingError,
  onUpdate as onMatchmakingUpdate,
  refresh as refreshMatchmaking,
  joinRandom,
  setGamePublic,
  navigateToGame,
  type PublicGame,
} from './matchmaking';
import {
  installProfileDrawer,
  installProfileToggle,
  hydrateProfileFromCacheIfAvailable,
  onProfile,
  getProfile,
  type PlayerProfile,
} from './profile';
import { formatStats } from './stats';
import {
  bootstrapIdentity,
  installOnboardingCard,
  onIdentity,
  refreshOnboardingVisibility,
} from './identity';
import {
  installLeaderboardSurface,
  startLeaderboardPolling,
  stopLeaderboardPolling,
} from './leaderboard';
import { installSettingsDrawerV2 } from './settings-drawer';
import { installProfilePage } from './profile-page';
import { installAuthUi } from './auth';
import { installRulePresetsUi, getSelectedPresetId } from './rule-presets';
import { installDisplayPreferences } from './theme';
import { installSpectatorFollow } from './spectator-follow';
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

interface LobbyState {
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

function writeLocalStorageDefaults(state: LobbyState): void {
  try {
    window.localStorage.setItem(LOCAL_STORAGE_KEY, JSON.stringify({
      variant: state.variant,
      dealMode: state.dealMode,
      botCount: state.botCount,
      botDifficulty: state.botDifficulty,
      seed: state.seed,
      handCount: state.handCount,
      seat: state.seat,
    }));
  } catch {
    // localStorage may be disabled (privacy mode, quota); silently ignore
    // because the URL-driven Apply still works.
  }
}

function buildUrl(state: LobbyState): string {
  const p = new URLSearchParams();
  // Phase I Wave 3 — preserve any ?gameId= already on the page URL so
  // a lobby Apply & Start doesn't silently switch the user back to the
  // default game.  client-ui.ts is the source of truth for editing
  // gameId; the lobby just passes it through.
  const currentGameId = new URLSearchParams(window.location.search).get('gameId');
  if (currentGameId !== null && currentGameId !== '') {
    p.set('gameId', currentGameId);
  }
  p.set('variant', state.variant);
  // dealMode is Changsha-only — Riichi variants ignore it.  Emit only
  // when relevant so the URL stays tidy.
  if (state.variant === 'changsha') {
    p.set('dealMode', state.dealMode);
  }
  p.set('botCount', String(state.botCount));
  // botDifficulty is irrelevant when there are no bots — skip it.
  if (state.botCount > 0) {
    p.set('botDifficulty', state.botDifficulty);
  }
  // Phase H Wave 1 — handCount is always emitted (every variant has a
  // hand length); seed is emitted only when explicitly set, so the
  // bare-URL case keeps the existing random-seed behaviour.
  p.set('handCount', String(state.handCount));
  if (state.seed !== null) {
    p.set('seed', String(state.seed));
  }
  // Phase I Wave 4 — emit ?seat= only when the user made an explicit
  // choice (-1 / 0..3).  A null seat (no preference) leaves the param
  // off so the legacy server-picks-a-seat path keeps working unchanged.
  if (state.seat !== null) {
    p.set('seat', String(state.seat));
  }
  // Phase J Wave 8 — emit rulePreset=<id> when the user picked a
  // non-default preset.  The backend ignores the param if Bishop's
  // rule-preset endpoints aren't deployed yet (graceful degradation).
  try {
    const presetId = getSelectedPresetId();
    if (presetId !== '' && presetId !== 'classic-changsha') {
      p.set('rulePreset', presetId);
    }
  } catch { /* rule-presets module not initialised — skip */ }
  return window.location.pathname + '?' + p.toString();
}

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
      seedError.style.display = valid ? 'none' : 'block';
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
      spectatorHint.style.display = isSpectator ? 'block' : 'none';
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
    if (saveDefaultsInput !== null && saveDefaultsInput.checked) {
      writeLocalStorageDefaults(state);
    }
    const url = buildUrl(state);
    // replace() instead of assign() so the browser back-button doesn't
    // bounce the user between game configurations.
    window.location.replace(url);
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
      const url = buildUrl(quick);
      window.location.replace(url);
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
  _renderPlayerChips = renderPlayerChips;
  _renderSeatPreview = renderSeatPreview;
  if (_attachedClient !== null) {
    bindLiveListeners(_attachedClient);
  }

  // Phase J Wave 5 — install the profile drawer + open-profile shortcut
  // button, then mount the lobby's new tab strip / public-games pane /
  // make-public toggle / stats panel.  These helpers are defined at the
  // bottom of this file; they are idempotent and bail out if their
  // anchor elements are missing.
  installProfileDrawer();
  installProfileToggle();
  // Phase J Wave 7 — mount the app-wide settings drawer (separate from
  // the Wave-2 per-game settings panel) and the player profile page
  // overlay.  Both are idempotent and intercept clicks before the
  // legacy Wave-5 drawer handler.
  installSettingsDrawerV2();
  installProfilePage();
  // Phase J Wave 8 — install display-pref body classes (reduced-motion,
  // theme-dark / theme-light) before the auth UI / rule-presets pickers
  // render so the chrome paints with the correct palette immediately.
  installDisplayPreferences();
  // Phase J Wave 8 — sign-in modal + linked-accounts section in the
  // profile drawer; rule preset picker in the lobby + editor tab in
  // the settings drawer; spectator follow-seat helper.
  installAuthUi();
  installRulePresetsUi();
  installSpectatorFollow();
  // Phase J Wave 6 — seed profile.ts.current from the localStorage
  // cache so the lobby chip shows the previously-saved displayName
  // *before* the SignalR hub connects (which only happens once the
  // user enters a game).  Without this, a fresh lobby visit after a
  // previous-session onboarding shows the default "Profile" text
  // until the hub round-trip eventually lands.
  hydrateProfileFromCacheIfAvailable();
  installLobbyTabs();
  installPublicGamesPane();
  installMakePublicToggle();
  installLobbyStatsPanel();

  // Phase J Wave 6 — kick off the cookie-bound identity bootstrap +
  // mount the onboarding card + leaderboard surface.  bootstrapIdentity
  // is idempotent (the in-flight POST is deduped), so calling it from
  // both index.ts and here is safe.  installOnboardingCard reads the
  // first-visit hint from the bootstrap result; we also refresh the
  // visibility once the identity arrives in case the identity lands
  // after the lobby is mounted.
  installOnboardingCard();
  installLeaderboardSurface();
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
  // parsing JSON.  This mirror reads the current state at boot and
  // writes the key on every change of the #settings-sound checkbox.
  installSoundEnabledMirror();

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
  onProfile(() => {
    renderAll();
    renderLobbyStatsPanel();
  });
  renderAll();
}

// Render the lobby's player chip strip from the live `seats` + `nicks`
// collections.  Layout: one chip per occupied seat, plus a "Bot Δ"
// placeholder per missing-but-bot-controlled seat (informational —
// botCount is set per-game, not per-seat, so we use a heuristic of
// "nick starts with 'Bot '" to detect bot seats).
function renderPlayerChips(client: Client): void {
  const strip = document.getElementById('lobby-players-strip');
  const list = document.getElementById('lobby-players-list');
  if (strip === null || list === null) return;

  const occupants: Array<{ playerId: string; nick: string; seat: number | null }> = [];
  for (const [playerId, seatInfo] of client.seats.entries()) {
    if (playerId === 'offline') continue;
    const rawNick = client.nicks.get(playerId);
    const nick = resolveDisplayName(
      playerId, rawNick !== null && rawNick !== undefined ? rawNick : '(no nick)');
    occupants.push({ playerId, nick, seat: seatInfo.seat });
  }
  occupants.sort((a, b) => {
    const sa = a.seat === null ? 99 : a.seat;
    const sb = b.seat === null ? 99 : b.seat;
    return sa - sb;
  });

  list.replaceChildren();
  for (let i = 0; i < occupants.length; i++) {
    list.appendChild(buildPlayerChip(occupants[i], i));
  }
  strip.style.display = occupants.length > 0 ? '' : 'none';
}

// Render the lobby's seat-preview grid from the live `seats` + `nicks`
// collections.  One cell per wind (East/South/West/North); empty cells
// show "Open" and bot-controlled cells show the bot's nick prefixed
// with a 🤖.
function renderSeatPreview(client: Client): void {
  const preview = document.getElementById('lobby-seat-preview');
  if (preview === null) return;

  const occupantBySeat: Array<{ playerId: string; nick: string } | null> =
    [null, null, null, null];
  for (const [playerId, seatInfo] of client.seats.entries()) {
    if (playerId === 'offline') continue;
    if (seatInfo.seat === null) continue;
    if (seatInfo.seat < 0 || seatInfo.seat > 3) continue;
    const rawNick = client.nicks.get(playerId);
    const nick = resolveDisplayName(
      playerId, rawNick !== null && rawNick !== undefined ? rawNick : '(no nick)');
    occupantBySeat[seatInfo.seat] = { playerId, nick };
  }

  for (let seat = 0; seat < 4; seat++) {
    const cell = preview.querySelector<HTMLElement>(
      `.lobby-seat-preview-cell[data-seat="${seat}"]`);
    if (cell === null) continue;
    const occupantEl = cell.querySelector<HTMLElement>(
      '.lobby-seat-preview-occupant');
    if (occupantEl === null) continue;
    const occupant = occupantBySeat[seat];
    cell.classList.toggle('lobby-seat-preview-empty', occupant === null);
    cell.classList.toggle(
      'lobby-seat-preview-bot', occupant !== null && isBotNick(occupant.nick));
    if (occupant === null) {
      occupantEl.textContent = 'Open';
    } else {
      occupantEl.textContent = isBotNick(occupant.nick)
        ? `🤖 ${occupant.nick}`
        : occupant.nick;
    }
  }
  preview.style.display = '';
}

function buildPlayerChip(
  occupant: { playerId: string; nick: string; seat: number | null },
  index: number,
): HTMLElement {
  const chip = document.createElement('div');
  chip.className = 'lobby-player-chip';
  chip.setAttribute('role', 'listitem');
  chip.setAttribute('data-testid', `lobby-player-chip-${index}`);
  if (occupant.seat !== null) {
    chip.setAttribute('data-seat', String(occupant.seat));
  }
  // Phase J Wave 5 — prefer the profile's displayName + avatarColor
  // over the WS-broadcast nick / djb2 hue.  Only the *local* player's
  // profile is available; remote chips fall back to nick + djb2 hash.
  const displayName = resolveDisplayName(occupant.playerId, occupant.nick);
  chip.style.setProperty(
    '--chip-color',
    resolveAvatarColor(occupant.playerId, occupant.nick));

  const avatar = document.createElement('span');
  avatar.className = 'lobby-player-chip-avatar';
  avatar.textContent = initialsFromNick(displayName);

  const nick = document.createElement('span');
  nick.className = 'lobby-player-chip-nick';
  nick.textContent = displayName;

  const seatBadge = document.createElement('span');
  seatBadge.className = 'lobby-player-chip-seat';
  seatBadge.textContent = occupant.seat === null
    ? '👁'
    : String(occupant.seat);

  chip.appendChild(avatar);
  chip.appendChild(nick);
  chip.appendChild(seatBadge);
  return chip;
}

// Phase J Wave 5 — profile-aware display-name resolver.  Returns the
// local player's profile.displayName when their connection-attached
// profile is loaded; otherwise falls back to the WS-broadcast nick
// (which itself is also populated from profile.displayName for the
// local user via client.ts).  Remote players' display names arrive
// only through the WS nicks broadcast — their profile is not exposed
// to the local client (parallel identity).
function resolveDisplayName(playerId: string, nick: string): string {
  const profile = getProfile();
  if (profile !== null && profile.playerId === playerId) {
    return profile.displayName;
  }
  if (nick !== '(no nick)' && nick !== '') return nick;
  return nick;
}

// Phase J Wave 5 — profile-aware avatar-colour resolver.  Same
// precedence as resolveDisplayName: local profile wins, then the
// djb2 hue fallback for remote players (and for the local player
// before the profile arrives from SignalR).
function resolveAvatarColor(playerId: string, _nick: string): string {
  const profile = getProfile();
  if (profile !== null && profile.playerId === playerId) {
    return profile.avatarColor;
  }
  return chipColorForPlayer(playerId);
}

// djb2 hash of the player id → HSL hue for the chip background.
// Lightness + saturation are clamped so every chip is legibly dark
// enough to host white text without per-player tuning.
function chipColorForPlayer(playerId: string): string {
  let hash = 5381;
  for (let i = 0; i < playerId.length; i++) {
    hash = ((hash << 5) + hash) + playerId.charCodeAt(i);
    hash &= 0xffffffff;
  }
  const hue = Math.abs(hash) % 360;
  return `hsl(${hue}, 55%, 38%)`;
}

function initialsFromNick(nick: string): string {
  const trimmed = nick.trim();
  if (trimmed === '') return '?';
  const parts = trimmed.split(/\s+/);
  if (parts.length === 1) return parts[0].charAt(0).toUpperCase();
  return (parts[0].charAt(0) + parts[parts.length - 1].charAt(0)).toUpperCase();
}

function isBotNick(nick: string | null | undefined): boolean {
  // Mirror game-ui.ts's bot detection so both surfaces agree.
  return !!nick && /^Bot\b/i.test(nick);
}

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

function installLobbyTabs(): void {
  const myTab = document.getElementById(
    'lobby-my-game-tab') as HTMLButtonElement | null;
  const pubTab = document.getElementById(
    'lobby-public-games-tab') as HTMLButtonElement | null;
  const lbTab = document.getElementById(
    'lobby-leaderboard-tab') as HTMLButtonElement | null;
  const myPane = document.getElementById('lobby-tab-my-game');
  const pubPane = document.getElementById('lobby-tab-public-games');
  const lbPane = document.getElementById('lobby-tab-leaderboard');
  if (myTab === null || pubTab === null
      || myPane === null || pubPane === null) {
    return;
  }

  const activate = (which: 'my' | 'public' | 'leaderboard'): void => {
    const isMy = which === 'my';
    const isPub = which === 'public';
    const isLb = which === 'leaderboard';
    myTab.classList.toggle('lobby-tab-active', isMy);
    pubTab.classList.toggle('lobby-tab-active', isPub);
    if (lbTab !== null) lbTab.classList.toggle('lobby-tab-active', isLb);
    myTab.setAttribute('aria-selected', isMy ? 'true' : 'false');
    pubTab.setAttribute('aria-selected', isPub ? 'true' : 'false');
    if (lbTab !== null) lbTab.setAttribute('aria-selected', isLb ? 'true' : 'false');
    myPane.style.display = isMy ? '' : 'none';
    pubPane.style.display = isPub ? '' : 'none';
    if (lbPane !== null) lbPane.style.display = isLb ? '' : 'none';

    // Per-tab polling discipline — Apone's rate-limit budget is the
    // motivation here.  Each tab owns one timer; we tear the other
    // tabs' timers down on activate.
    if (isPub) {
      if (!isMatchmakingPolling()) startMatchmakingPolling();
    } else {
      stopMatchmakingPolling();
    }
    if (isLb) {
      startLeaderboardPolling();
    } else {
      stopLeaderboardPolling();
    }
  };

  myTab.addEventListener('click', () => activate('my'));
  pubTab.addEventListener('click', () => activate('public'));
  if (lbTab !== null) {
    lbTab.addEventListener('click', () => activate('leaderboard'));
  }
  // Default: My Game pane visible.
  activate('my');
}

// ---------------------------------------------------------------------
// Phase J Wave 5 — public-games pane.
//
// Renders the cached PublicGame list from matchmaking.ts and wires
// the Join Random button.  Re-renders on every onUpdate tick (≤5 s).
// ---------------------------------------------------------------------

function installPublicGamesPane(): void {
  const listEl = document.getElementById('lobby-public-games-list');
  const emptyEl = document.getElementById('lobby-public-games-empty');
  const errorEl = document.getElementById('lobby-public-games-error');
  const joinRandomBtn = document.getElementById(
    'lobby-join-random') as HTMLButtonElement | null;
  if (listEl === null || emptyEl === null
      || errorEl === null || joinRandomBtn === null) {
    return;
  }

  const render = (): void => {
    const games = getCachedGames();
    const err = getMatchmakingError();
    if (err !== null) {
      errorEl.style.display = '';
      errorEl.textContent = `Failed to load public games: ${err}`;
    } else {
      errorEl.style.display = 'none';
      errorEl.textContent = '';
    }
    listEl.replaceChildren();
    if (games.length === 0) {
      emptyEl.style.display = '';
      return;
    }
    emptyEl.style.display = 'none';
    const cap = Math.min(games.length, 50);
    for (let i = 0; i < cap; i++) {
      listEl.appendChild(buildPublicGameCard(games[i], i));
    }
  };

  onMatchmakingUpdate(render);
  // Initial render in case polling fired before the listener landed.
  render();

  joinRandomBtn.addEventListener('click', async () => {
    joinRandomBtn.disabled = true;
    const prevLabel = joinRandomBtn.textContent;
    joinRandomBtn.textContent = 'Joining…';
    try {
      const variant = readUrlVariant();
      const result = await joinRandom(variant);
      if (result !== null) {
        navigateToGame(result.gameId, result.seatIndex);
      } else {
        errorEl.style.display = '';
        errorEl.textContent = 'No public games with free seats right now.';
      }
    } catch (e) {
      errorEl.style.display = '';
      const msg = e instanceof Error ? e.message : String(e);
      errorEl.textContent = `Join Random failed: ${msg}`;
    } finally {
      joinRandomBtn.disabled = false;
      joinRandomBtn.textContent = prevLabel ?? '🎲 Join Random';
    }
    // Force a refresh after the join attempt so the list reflects the
    // new occupancy (or the absence of any games to join).
    void refreshMatchmaking();
  });
}

function buildPublicGameCard(game: PublicGame, index: number): HTMLElement {
  const card = document.createElement('div');
  card.className = 'public-game-card';
  card.setAttribute('role', 'listitem');
  card.setAttribute('data-testid', `lobby-public-game-${index}`);
  card.setAttribute('data-game-id', game.gameId);
  const full = game.seatedCount >= game.maxSeats;
  if (full) card.classList.add('public-game-card-full');

  const name = document.createElement('div');
  name.className = 'public-game-card-name';
  name.setAttribute('data-testid', `lobby-public-game-name-${index}`);
  name.textContent = game.publicName !== null && game.publicName !== ''
    ? game.publicName
    : `${game.creatorDisplayName}'s game`;

  const meta = document.createElement('div');
  meta.className = 'public-game-card-meta';
  const creator = document.createElement('span');
  creator.className = 'public-game-card-meta-creator';
  creator.setAttribute('data-testid', `lobby-public-game-host-${index}`);
  creator.textContent = `Host: ${game.creatorDisplayName}`;
  const seats = document.createElement('span');
  seats.className = 'public-game-card-meta-seats';
  seats.setAttribute('data-testid', `lobby-public-game-seats-${index}`);
  if (full) seats.classList.add('seats-full');
  seats.textContent = `${game.seatedCount} / ${game.maxSeats}`;
  meta.appendChild(creator);
  meta.appendChild(seats);
  if (game.variant !== null && game.variant !== '') {
    const variant = document.createElement('span');
    variant.className = 'public-game-card-meta-variant';
    variant.textContent = `Variant: ${game.variant}`;
    meta.appendChild(variant);
  }

  const join = document.createElement('button');
  join.type = 'button';
  join.className = 'btn btn-primary btn-sm public-game-card-join';
  join.setAttribute('data-testid', `lobby-public-game-join-${index}`);
  join.textContent = full ? 'Full' : 'Join';
  join.disabled = full;
  join.addEventListener('click', () => {
    if (full) return;
    navigateToGame(game.gameId);
  });

  card.appendChild(name);
  card.appendChild(meta);
  card.appendChild(join);
  return card;
}

// Read the lobby's currently-selected variant so Join Random can pass
// it through to the server.  Falls back to undefined (server accepts
// any variant) when the picker can't be read.
function readUrlVariant(): string | undefined {
  const params = new URLSearchParams(window.location.search);
  const v = params.get('variant');
  return v !== null && v !== '' ? v : undefined;
}

// ---------------------------------------------------------------------
// Phase J Wave 5 — Make-my-game-public toggle.
//
// Visible whenever the user is in a live game (gameId is read from
// the URL).  Flipping the toggle invokes Bishop's SignalR
// `SetGamePublic` RPC; the optional friendly name input is sent on
// every toggle-on (omitted when blank).
// ---------------------------------------------------------------------

function installMakePublicToggle(): void {
  const toggle = document.getElementById(
    'lobby-make-public-toggle') as HTMLInputElement | null;
  const nameInput = document.getElementById(
    'lobby-make-public-name') as HTMLInputElement | null;
  const statusEl = document.getElementById('lobby-make-public-status');
  if (toggle === null || nameInput === null || statusEl === null) return;

  const setStatus = (msg: string, isError: boolean): void => {
    statusEl.textContent = msg;
    statusEl.classList.toggle('lobby-make-public-status-error', isError);
  };

  const sync = async (): Promise<void> => {
    const gameId = currentGameId();
    if (gameId === null) {
      setStatus('Not in a live game.', true);
      toggle.checked = false;
      toggle.disabled = true;
      nameInput.disabled = true;
      return;
    }
    toggle.disabled = true;
    nameInput.disabled = !toggle.checked;
    const publicName = toggle.checked && nameInput.value.trim() !== ''
      ? nameInput.value.trim()
      : undefined;
    setStatus(toggle.checked ? 'Publishing…' : 'Unlisting…', false);
    try {
      const result = await setGamePublic(
        { gameId, isPublic: toggle.checked, publicName });
      if (result.success) {
        setStatus(
          result.isPublic
            ? (result.publicName !== null && result.publicName !== ''
                ? `Listed as "${result.publicName}".`
                : 'Listed in the public lobby.')
            : 'Unlisted from the public lobby.',
          false);
      } else {
        setStatus('Server rejected the change.', true);
        toggle.checked = !toggle.checked;
      }
    } catch (e) {
      const msg = e instanceof Error ? e.message : String(e);
      setStatus(`Failed: ${msg}`, true);
      toggle.checked = !toggle.checked;
    } finally {
      toggle.disabled = false;
      nameInput.disabled = !toggle.checked;
    }
  };

  toggle.addEventListener('change', () => { void sync(); });
  // Name changes only matter while the toggle is on; debounce-on-blur
  // re-publishes so the new name lands without a UI ping-pong.
  nameInput.addEventListener('blur', () => {
    if (toggle.checked) void sync();
  });

  // Initial state — only enable if the URL has a game id; otherwise
  // leave the controls disabled and the status descriptive.
  if (currentGameId() === null) {
    toggle.disabled = true;
    nameInput.disabled = true;
    setStatus('Start or join a game to publish it.', false);
  } else {
    nameInput.disabled = !toggle.checked;
    setStatus('', false);
  }
}

// Pull the active game id from the URL.  Mirrors the logic in
// index.ts that bootstraps the Client/Game lifecycle.
function currentGameId(): string | null {
  const params = new URLSearchParams(window.location.search);
  const g = params.get('game');
  if (g === null || g === '') return null;
  return g;
}

// ---------------------------------------------------------------------
// Phase J Wave 5 — lobby stats panel.
//
// Renders the local player's career stats from the profile cache.
// Empty until the SignalR ProfileLoaded event fires; re-renders on
// every onProfile tick (the bindLiveListeners wiring above already
// calls renderLobbyStatsPanel() through renderAll).
// ---------------------------------------------------------------------

function installLobbyStatsPanel(): void {
  // Initial paint — onProfile listener (set up in bindLiveListeners)
  // will refresh as updates arrive.
  renderLobbyStatsPanel();
  // Also subscribe directly in case the client isn't attached yet
  // (initLobby runs before Game.start()).
  onProfile(() => renderLobbyStatsPanel());
}

function renderLobbyStatsPanel(): void {
  const host = document.getElementById('lobby-stats-panel');
  if (host === null) return;
  const profile: PlayerProfile | null = getProfile();
  host.replaceChildren();
  if (profile === null) {
    const empty = document.createElement('div');
    empty.className = 'lobby-stats-empty';
    empty.textContent = 'Loading your stats…';
    host.appendChild(empty);
    return;
  }
  const panel = formatStats(profile.stats);
  // Wrap with a heading so the lobby stats section makes it clear who
  // these numbers belong to (matches Bishop's profile.displayName).
  const heading = document.createElement('div');
  heading.className = 'lobby-stats-panel-subject';
  heading.textContent = profile.displayName;
  host.appendChild(heading);
  host.appendChild(panel);
}

// ---------------------------------------------------------------------
// Phase J Wave 6 — sound-toggle localStorage mirror.
//
// The Wave-3 settings drawer persists the sound knob inside a
// JSON-encoded payload at `autotable.phaseJ.v1.settings.*`.  Wave 6
// adds a discoverable scalar key — `mahjong:soundEnabled` — so the
// state can be flipped / read from the outside (Playwright specs,
// browser DevTools, future cross-tab sync) without parsing JSON.
//
// The mirror is one-way (settings → key) with two sync points:
//   • boot — derive the initial key value from the current state of
//     the #settings-sound checkbox.  At boot the checkbox is hydrated
//     from localStorage by game-ui.ts:setupSettingsDrawer() before
//     the user can interact, so this captures the persisted state.
//   • on `change` event — write the new value whenever the user
//     flips the checkbox.
// ---------------------------------------------------------------------

const LS_KEY_SOUND_ENABLED = 'mahjong:soundEnabled';
let soundMirrorInstalled = false;

function installSoundEnabledMirror(): void {
  if (soundMirrorInstalled) return;
  const checkbox = document.getElementById(
    'settings-sound') as HTMLInputElement | null;
  if (checkbox === null) return;
  soundMirrorInstalled = true;

  const writeKey = (enabled: boolean): void => {
    try {
      window.localStorage.setItem(LS_KEY_SOUND_ENABLED, enabled ? 'true' : 'false');
    } catch {
      /* private mode / quota — skip */
    }
  };

  // Initial mirror.  The checkbox's `checked` property is the source of
  // truth — game-ui.ts seeds it from the JSON-encoded settings payload
  // before the user can interact; if it hasn't hydrated yet the default
  // is "on" (matches SETTINGS_DEFAULT.sound).
  writeKey(checkbox.checked);

  checkbox.addEventListener('change', () => writeKey(checkbox.checked));

  // Also rewrite the key on programmatic changes (e.g. game-ui.ts
  // toggling the checkbox from a settings-apply flow).  MutationObserver
  // doesn't fire on .checked changes, so we install a one-shot
  // re-mirror on every settings drawer click event as a safety net.
  const drawerToggle = document.getElementById('settings-toggle');
  if (drawerToggle !== null) {
    drawerToggle.addEventListener('click', () => {
      // After the drawer-toggle handler runs the checkbox state may
      // have been re-hydrated; schedule the mirror for the next tick.
      window.setTimeout(() => writeKey(checkbox.checked), 0);
    });
  }
}
