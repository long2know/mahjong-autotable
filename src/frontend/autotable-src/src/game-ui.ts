import $ from 'jquery';
import { Client, GameCompleteEntry } from "./client";
import { readSpectatorFromUrl } from './client-ui';
import { Sound } from './sound';
import { Replay } from './replay';
import { openReplayForGame } from './replay-launcher';
import { World } from "./world";
import { setElHidden, showEl, hideEl } from './dom-utils';
import {
  DealType,
  Conditions,
  ClaimWindowEntry,
  HandResultEntry,
  DiceInfo,
  MatchInfo,
  GameType,
  Fives,
  Points,
  DealMode,
  PickupEntry,
  SoundInfo,
  SoundType,
  ThingInfo,
} from './types';

// Phase D — claim window expiry uses the deadline (epoch ms) that the server
// pushes; CLAIM_TICK_MS controls how often the countdown text re-renders.
const CLAIM_TICK_MS = 100;

// Phase D — dice HUD lifetime (after first-deal roll, before fade-out).
const DICE_HUD_LIFETIME_MS = 3000;

// Phase F — localStorage namespacing.  We store the user's last-chosen
// variant + deal-mode + bot config so a refresh re-applies the same setup.
// Versioned in case the schema needs to change later.
const LS_KEY_PREFIX = 'autotable.phaseF.v1.';
const LS_VARIANT       = LS_KEY_PREFIX + 'variant';
const LS_DEAL_MODE     = LS_KEY_PREFIX + 'dealMode';
const LS_BOT_COUNT     = LS_KEY_PREFIX + 'botCount';
const LS_BOT_DIFFICULTY = LS_KEY_PREFIX + 'botDifficulty';
const LS_FIVES         = LS_KEY_PREFIX + 'fives';
const LS_POINTS        = LS_KEY_PREFIX + 'points';

type ClaimAction = { action: 'claim'; type: 'Pung' | 'Chow' | 'Kong' | 'Hu' }
                 | { action: 'pass'; type: null };

// Phase H Wave 2 + Phase I Wave 1 — V2-rules extensions to the wire-protocol
// `HandResultEntry`.
//
// Bishop's WinDetectionResult / WinResult / ScoreResult contribute these fields:
//   • AllPatterns: every big-win pattern that fires this hand (SevenPairs,
//     AllPungs, FullFlush, NineTerminals, HeavenlyHand, EarthlyHand,
//     LastTileFromWall, LastDiscardCatch, KongReplacementWin — enum-decl order).
//   • IsRobbedKong: shortcut flag for WinMethod.RobbingKong (抢杠胡).
//   • scoreResult { category, basePoints, payments[] }: Phase I Wave 1 — the
//     full Changsha score-multiplier breakdown so the modal can name the
//     multiplier source (×N for N stacked Big-Win patterns).
//
// These ride the existing `result.current` collection.  Until Bishop's
// translator commit lands they will simply be absent from the JSON
// payload — every field below is `?:` so the UI degrades to legacy
// single-pattern rendering with zero runtime errors.
//
// camelCase wire shape (System.Text.Json default is CamelCase per
// AutotableProtocol.cs:JsonNamingPolicy.CamelCase).  We tolerate a
// PascalCase fallback too in case the JSON contract drifts.
interface ScoreResultPayment {
  fromSeatIndex?: number;
  toSeatIndex?: number;
  amount?: number;
  reason?: string;
  // PascalCase fallbacks.
  FromSeatIndex?: number;
  ToSeatIndex?: number;
  Amount?: number;
  Reason?: string;
}

interface ScoreResultExtra {
  category?: string;
  basePoints?: number;
  payments?: ReadonlyArray<ScoreResultPayment>;
  Category?: string;
  BasePoints?: number;
  Payments?: ReadonlyArray<ScoreResultPayment>;
}

interface WinResultExtra {
  allPatterns?: ReadonlyArray<string>;
  method?: string;
  isRobbedKong?: boolean;
  // Phase I Wave 2 — backend `winType` (one of 'selfDraw' / 'discard' /
  // 'robbingKong') + `sourceSeatIndex` for the discarder/declarer.  Both
  // confirmed wire fields on WinResultEntry; optional so a pre-W2 payload
  // degrades to the legacy (no-badge) render.
  winType?: string;
  sourceSeatIndex?: number;
  AllPatterns?: ReadonlyArray<string>;
  Method?: string;
  IsRobbedKong?: boolean;
  WinType?: string;
  SourceSeatIndex?: number;
}

interface ResultExtras {
  pattern?: string;
  method?: string;
  allPatterns?: ReadonlyArray<string>;
  isRobbedKong?: boolean;
  scoreResult?: ScoreResultExtra;
  winResult?: WinResultExtra;
  // Defensive aliases (PascalCase) — only used as a fallback.
  Pattern?: string;
  Method?: string;
  AllPatterns?: ReadonlyArray<string>;
  IsRobbedKong?: boolean;
  ScoreResult?: ScoreResultExtra;
  WinResult?: WinResultExtra;
}

// Friendly display names for each WinPattern enum value.  Standard is the
// baseline non-stacking pattern (per Ripley §2.3) and intentionally absent
// so a vanilla 4-sets-and-a-pair hand renders no chip.
//
// Keys are the camelCase wire vocabulary (see backend WinPatternToWire).
// The lookup is normalised via `normalizePatternKey` so PascalCase variants
// (raw enum ToString) resolve to the same label.  Unknown keys fall back to
// the raw wire string so a forward-compat pattern still renders something.
const PATTERN_LABELS: Readonly<Record<string, string>> = {
  sevenPairs:         '七对 Seven Pairs',
  allPungs:           '碰碰胡 All Pungs',
  fullFlush:          '清一色 Full Flush',
  nineTerminals:      '九幺 Nine Terminals',
  // Phase I Wave 1 — contextual Big Wins (Bishop's branch).
  heavenlyHand:       '天和 Heavenly Hand',
  earthlyHand:        '地和 Earthly Hand',
  lastTileFromWall:   '海底捞月 Last Tile',
  lastDiscardCatch:   '河底捞鱼 Last Discard',
  kongReplacementWin: '杠上开花 Kong Bloom',
};

const PATTERN_CHIP_CLASSES: Readonly<Record<string, string>> = {
  sevenPairs:         'pattern-seven-pairs',
  allPungs:           'pattern-all-pungs',
  fullFlush:          'pattern-full-flush',
  nineTerminals:      'pattern-nine-terminals',
  heavenlyHand:       'pattern-heavenly-hand',
  earthlyHand:        'pattern-earthly-hand',
  lastTileFromWall:   'pattern-last-tile',
  lastDiscardCatch:   'pattern-last-discard',
  kongReplacementWin: 'pattern-kong-bloom',
};

// Phase I Wave 2 — per-chip hover tooltip dictionary.  Each entry pairs the
// 大字 Chinese name with a one-line English description sourced from Bishop's
// pattern spec (Phase H Wave 2 / Phase I Wave 1 §2).  Lookup is keyed by
// the normalised camelCase pattern key (same as PATTERN_LABELS); unknown
// patterns simply skip the tooltip (the chip still renders with the raw
// wire string).
const PATTERN_TOOLTIPS: Readonly<Record<string, { cn: string; en: string }>> = {
  standard:           { cn: '普通胡',       en: 'Standard winning hand: 4 sets + a pair.' },
  sevenPairs:         { cn: '七对',         en: 'Seven distinct pairs (qī duì) — Big Win.' },
  allPungs:           { cn: '碰碰胡',       en: 'All pungs / kongs + a pair — Big Win.' },
  fullFlush:          { cn: '清一色',       en: 'Single suit only (qīng yī sè) — Big Win.' },
  nineTerminals:      { cn: '幺九',         en: 'All tiles are 1s, 9s, or honors — Big Win.' },
  heavenlyHand:       { cn: '天和',         en: 'Dealer self-draws on the initial deal — Big Win.' },
  earthlyHand:        { cn: '地和',         en: 'Non-dealer wins on dealer\'s first discard — Big Win.' },
  lastTileFromWall:   { cn: '海底捞月',     en: 'Self-draw on the very last wall tile (hǎi dǐ lāo yuè) — Big Win.' },
  lastDiscardCatch:   { cn: '河底捞鱼',     en: 'Hu the last discard after wall exhaustion (hé dǐ lāo yú) — Big Win.' },
  kongReplacementWin: { cn: '杠上开花',     en: 'Win on the replacement tile drawn after a kong — Big Win.' },
};

// Phase J Wave 3 → Phase K Wave 4 — pattern ordering moved into
// `./pattern-utils` so the renderer-critical `move-log` + `scene-shell`
// chunks don't drag the full 100 kB game-ui graph in via a comparator
// import.  Re-exported here for the (small) population of external
// callers that imported these helpers via `./game-ui`.
import {
  comparePatterns,
  sortPatterns,
  setPatternDisplayOrder,
  loadPatternOrderingFromApi,
  normalizePatternKey,
} from './pattern-utils';
export {
  comparePatterns,
  sortPatterns,
  setPatternDisplayOrder,
  loadPatternOrderingFromApi,
};

// Phase D — convert a Changsha tile id (0..26 over 3 suits × 9 ranks) to a
// terse glyph the result modal renders, e.g. tile 0 → "1m", tile 14 → "6p".
// Suit order matches setup-deal.ts (m=characters, p=dots, s=bamboo).
function tileLabel(tile: number): { text: string; suit: string } {
  const suits = ['m', 'p', 's'];
  const idx = ((tile % 27) + 27) % 27;
  const suit = suits[Math.floor(idx / 9)];
  const rank = (idx % 9) + 1;
  return { text: `${rank}${suit}`, suit: `suit-${suit}` };
}

// Phase D — Default convention (documented in this PR's inbox drop):
// a seat is treated as a bot when its nick starts with "Bot " (case-sensitive).
// Bishop's seats collection may later carry an explicit `is_bot` flag; this
// fallback keeps the bot banner usable until that field lands.
function isBotNick(nick: string | null | undefined): boolean {
  return !!nick && nick.startsWith('Bot ');
}

// Phase F — variant-badge label and CSS body class selection.  Two-character
// emoji + variant short-name so players never have to open the sidebar to
// confirm what they're playing.
function variantLabel(gameType: GameType): string {
  switch (gameType) {
    case GameType.CHANGSHA:     return '🀄 Changsha';
    case GameType.FOUR_PLAYER:  return '🎴 Riichi 4p';
    case GameType.THREE_PLAYER: return '🎴 Riichi 3p';
    case GameType.BAMBOO:       return '🎋 Bamboo';
    case GameType.MINEFIELD:    return '💣 Minefield';
  }
}

// Phase F — URL params parsed once at page-load and merged with localStorage.
// Priority order: URL > localStorage > Conditions defaults.
interface PhaseFParams {
  variant?:       GameType;
  dealMode?:      DealMode;
  botCount?:      number;
  botDifficulty?: 'easy' | 'medium' | 'hard';
  fives?:         Fives;
  points?:        Points;
}

function parseUrlParams(): PhaseFParams {
  const p = new URLSearchParams(window.location.search);
  const out: PhaseFParams = {};

  const variant = p.get('variant');
  if (variant) {
    const upper = variant.toUpperCase().replace(/-/g, '_');
    if (upper in GameType) {
      out.variant = upper as GameType;
    }
  }

  const dealMode = p.get('dealMode');
  if (dealMode === 'manual' || dealMode === 'auto') {
    out.dealMode = dealMode;
  }

  // Phase F — back-compat: `?bots=true` aliases `?botCount=3`.
  const bots = p.get('bots');
  if (bots === 'true' || bots === '1') {
    out.botCount = 3;
  }
  const botCount = p.get('botCount');
  if (botCount !== null) {
    const n = parseInt(botCount, 10);
    if (!isNaN(n) && n >= 0 && n <= 4) out.botCount = n;
  }

  const diff = p.get('botDifficulty');
  if (diff === 'easy' || diff === 'medium' || diff === 'hard') {
    out.botDifficulty = diff;
  }

  const fives = p.get('fives');
  if (fives === '000' || fives === '111' || fives === '121') {
    out.fives = fives;
  }

  const points = p.get('points');
  if (points === '25' || points === '30' || points === '35' || points === '40' || points === '100') {
    out.points = points;
  }

  return out;
}

// Phase J Wave 7 — Extract `?gameId=...` from the current location so
// the post-game modal's "View Replay" button can fetch the server-side
// replay endpoint when available.  Returns null when absent.
function readGameIdFromLocation(): string | null {
  try {
    const params = new URLSearchParams(window.location.search);
    const gid = params.get('gameId');
    if (gid !== null && gid.trim().length > 0) return gid.trim();
  } catch {
    // ignore — non-browser test contexts
  }
  return null;
}

function readLocalStorage(): PhaseFParams {
  const out: PhaseFParams = {};
  try {
    const v = localStorage.getItem(LS_VARIANT);
    if (v && v in GameType) out.variant = v as GameType;
    const d = localStorage.getItem(LS_DEAL_MODE);
    if (d === 'manual' || d === 'auto') out.dealMode = d;
    const b = localStorage.getItem(LS_BOT_COUNT);
    if (b !== null) {
      const n = parseInt(b, 10);
      if (!isNaN(n) && n >= 0 && n <= 4) out.botCount = n;
    }
    const diff = localStorage.getItem(LS_BOT_DIFFICULTY);
    if (diff === 'easy' || diff === 'medium' || diff === 'hard') out.botDifficulty = diff;
    const f = localStorage.getItem(LS_FIVES);
    if (f === '000' || f === '111' || f === '121') out.fives = f;
    const pts = localStorage.getItem(LS_POINTS);
    if (pts === '25' || pts === '30' || pts === '35' || pts === '40' || pts === '100') {
      out.points = pts;
    }
  } catch {
    // localStorage may be blocked (private mode, etc.) — silently fall through.
  }
  return out;
}

function writeLocalStorage(key: string, value: string): void {
  try { localStorage.setItem(key, value); } catch { /* ignore */ }
}

export class GameUi {
  private client: Client;
  private world: World;

  elements: {
    deal: HTMLButtonElement;
    toggleDealer: HTMLButtonElement;
    toggleHonba: HTMLButtonElement;
    takeSeat: Array<HTMLButtonElement>;
    kick: Array<HTMLButtonElement>;
    leaveSeat: HTMLButtonElement;
    toggleSetup: HTMLButtonElement;
    dealType: HTMLSelectElement;
    gameType: HTMLSelectElement;
    dealMode: HTMLSelectElement;
    botCount: HTMLSelectElement;
    botDifficulty: HTMLSelectElement;
    fives: HTMLSelectElement;
    points: HTMLSelectElement;
    resetPoints: HTMLButtonElement;
    setupDesc: HTMLElement;
    claim: {
      Pung: HTMLButtonElement;
      Chow: HTMLButtonElement;
      Kong: HTMLButtonElement;
      Hu: HTMLButtonElement;
      Pass: HTMLButtonElement;
    };
    claimCountdown: HTMLElement;
    claimCountdownValue: HTMLElement;
    resultModal: HTMLElement;
    resultHeadline: HTMLElement;
    resultWinner: HTMLElement;
    resultScoreBody: HTMLTableSectionElement;
    resultHand: HTMLElement;
    resultNext: HTMLButtonElement;
    diceHud: HTMLElement;
    diceHudD1: HTMLElement;
    diceHudD2: HTMLElement;
    diceHudSum: HTMLElement;
    diceHudBreak: HTMLElement;
    botBanner: HTMLElement;
    variantBadge: HTMLElement;
    pickupHud: HTMLElement;
    pickupHudText: HTMLElement;
    pickupTakeBtn: HTMLButtonElement;
    pickupTakeCount: HTMLElement;
    rollDice: HTMLButtonElement;
    breakMarker: HTMLElement;
    // Hicks postfix-verify P0 — turn indicator banner.  See setupTurnBanner
    // for the per-source listener fan-in and refreshTurnBanner for the
    // priority order (claim > pickup > discard).
    turnBanner: HTMLElement;
    // Phase J Wave 1 — Hot-seat swap (Move button + inline picker).
    // Visibility, button states, and click handlers are all owned by
    // setupMoveSeatPicker / refreshMoveSeatPicker below.  The row container
    // toggles inline display=block when connected + Phase == Seating.
    moveSeatRow: HTMLElement;
    moveSeatBtn: HTMLButtonElement;
    moveSeatPanel: HTMLElement;
    moveSeatOptions: Array<HTMLButtonElement>;
    // Phase J Wave 2 — End-of-game summary modal.  Rendered when the
    // runtime pushes `gameComplete["current"]` with the completion flag
    // set.  All children are populated by renderGameComplete; the modal
    // hides itself via Bootstrap on the New Game / Back to Lobby clicks
    // (which then mutate the URL to restart or punt to the lobby).
    gameCompleteModal: HTMLElement;
    gameCompleteHeadline: HTMLElement;
    gameCompleteSubtitle: HTMLElement;
    gameCompleteTotalsBody: HTMLTableSectionElement;
    gameCompleteRecap: HTMLElement;
    gameCompleteNewGameBtn: HTMLButtonElement;
    gameCompleteLobbyBtn: HTMLButtonElement;
    // Phase J Wave 2 — Settings drawer (gear icon + slide-out panel).
    // Persists bot strength, hand count, and auto-deal to localStorage
    // keyed by gameId; Apply rebuilds the URL with the new params and
    // navigates so the runtime picks them up on the next JOIN.
    settingsToggle: HTMLButtonElement;
    settingsDrawer: HTMLElement;
    settingsClose: HTMLButtonElement;
    settingsBotStrength: HTMLSelectElement;
    settingsHandCount: HTMLSelectElement;
    settingsAutoDeal: HTMLInputElement;
    settingsApply: HTMLButtonElement;
    settingsSavedNote: HTMLElement;
    // Phase J Wave 3 — Sound toggle inside the settings drawer.  Persists
    // alongside botStrength / handCount / autoDeal in the same gameId-keyed
    // localStorage payload (see SettingsState below).
    settingsSound: HTMLInputElement;
    // Phase J Wave 3 — End-of-game "View Replay" affordance.  Opens the
    // replay screen (Replay.open) seeded with the gameComplete payload's
    // handHistory (when present).
    gameCompleteReplayBtn: HTMLButtonElement;
  }

  // Phase J Wave 2 — client-side hand history.  We capture each
  // `result.current` UPDATE so the end-of-game modal can render a per-
  // hand recap even when Bishop's runtime doesn't ship a `handHistory`
  // array.  Cleared on every fresh JOIN (the gameComplete tombstone)
  // and on New Game from the modal.
  private handHistory: Array<HandResultEntry> = [];

  // Phase J Wave 2 — guard so re-renders driven by collection updates
  // don't re-open the modal once the user has dismissed it via the
  // New Game or Back to Lobby buttons.
  private gameCompleteShown: boolean = false;

  // Phase J Wave 3 — Replay viewer.  Captures tile movements + per-hand
  // results in real time; opened from the end-of-game modal.
  private replay: Replay;

  // Phase J Wave 3 — sound-event throttle state.  We deduplicate
  // tile-draw signals across an event batch (a 13-tile initial deal
  // would otherwise queue 13 'draw' SFX) and gate every play behind
  // the settings-drawer "Sound" toggle.
  private lastDrawSoundMs: number = 0;
  // Last gameComplete payload (used to seed the replay when the user
  // clicks "View Replay" from the end-of-game modal).
  private lastGameCompletePayload: GameCompleteEntry | null = null;

  // Phase D — claim window state.
  private activeClaim: ClaimWindowEntry | null = null;
  private claimTickHandle: number | null = null;
  private diceHudHandle: number | null = null;

  // Phase F — last parsed Phase F params (URL > localStorage > defaults).
  // The picker UI keeps this in sync so subsequent deals see the latest.
  private phaseF: Required<PhaseFParams>;

  constructor(client: Client, world: World) {
    this.client = client;
    this.world = world;
    this.replay = new Replay(client);

    this.elements = {
      deal: document.getElementById('deal') as HTMLButtonElement,
      toggleDealer: document.getElementById('toggle-dealer') as HTMLButtonElement,
      toggleHonba:  document.getElementById('toggle-honba') as HTMLButtonElement,
      takeSeat: [],
      kick: [],
      leaveSeat: document.getElementById('leave-seat') as HTMLButtonElement,
      toggleSetup: document.getElementById('toggle-setup') as HTMLButtonElement,
      dealType: document.getElementById('deal-type') as HTMLSelectElement,
      gameType:      document.getElementById('game-type')      as HTMLSelectElement,
      dealMode:      document.getElementById('deal-mode')      as HTMLSelectElement,
      botCount:      document.getElementById('bot-count')      as HTMLSelectElement,
      botDifficulty: document.getElementById('bot-difficulty') as HTMLSelectElement,
      fives:         document.getElementById('fives')          as HTMLSelectElement,
      points:        document.getElementById('points')         as HTMLSelectElement,
      resetPoints:   document.getElementById('reset-points')   as HTMLButtonElement,
      setupDesc: document.getElementById('setup-desc') as HTMLElement,
      claim: {
        Pung: document.getElementById('claim-pung') as HTMLButtonElement,
        Chow: document.getElementById('claim-chow') as HTMLButtonElement,
        Kong: document.getElementById('claim-kong') as HTMLButtonElement,
        Hu:   document.getElementById('claim-hu')   as HTMLButtonElement,
        Pass: document.getElementById('claim-pass') as HTMLButtonElement,
      },
      claimCountdown:      document.getElementById('claim-countdown') as HTMLElement,
      claimCountdownValue: document.getElementById('claim-countdown-value') as HTMLElement,
      resultModal:         document.getElementById('result-modal') as HTMLElement,
      resultHeadline:      document.getElementById('result-headline') as HTMLElement,
      resultWinner:        document.getElementById('result-winner') as HTMLElement,
      resultScoreBody:     document.querySelector('#result-score tbody') as HTMLTableSectionElement,
      resultHand:          document.getElementById('result-hand') as HTMLElement,
      resultNext:          document.getElementById('result-next') as HTMLButtonElement,
      diceHud:             document.getElementById('dice-hud') as HTMLElement,
      diceHudD1:           document.getElementById('dice-hud-d1') as HTMLElement,
      diceHudD2:           document.getElementById('dice-hud-d2') as HTMLElement,
      diceHudSum:          document.getElementById('dice-hud-sum') as HTMLElement,
      diceHudBreak:        document.getElementById('dice-hud-break') as HTMLElement,
      botBanner:           document.getElementById('bot-banner') as HTMLElement,
      variantBadge:        document.getElementById('variant-badge') as HTMLElement,
      pickupHud:           document.getElementById('pickup-hud') as HTMLElement,
      pickupHudText:       document.getElementById('pickup-hud-text') as HTMLElement,
      pickupTakeBtn:       document.getElementById('pickup-take-btn') as HTMLButtonElement,
      pickupTakeCount:     document.getElementById('pickup-take-count') as HTMLElement,
      rollDice:            document.getElementById('roll-dice') as HTMLButtonElement,
      breakMarker:         document.getElementById('break-marker') as HTMLElement,
      turnBanner:          document.getElementById('turn-banner') as HTMLElement,
      moveSeatRow:         document.getElementById('move-seat-row') as HTMLElement,
      moveSeatBtn:         document.getElementById('move-seat-btn') as HTMLButtonElement,
      moveSeatPanel:       document.getElementById('move-seat-panel') as HTMLElement,
      moveSeatOptions:     Array.from(
        document.querySelectorAll<HTMLButtonElement>('#move-seat-panel .move-seat-option')),
      gameCompleteModal:        document.getElementById('game-complete-modal') as HTMLElement,
      gameCompleteHeadline:     document.getElementById('game-complete-headline') as HTMLElement,
      gameCompleteSubtitle:     document.getElementById('game-complete-subtitle') as HTMLElement,
      gameCompleteTotalsBody:   document.querySelector('#game-complete-totals tbody') as HTMLTableSectionElement,
      gameCompleteRecap:        document.getElementById('game-complete-recap') as HTMLElement,
      gameCompleteNewGameBtn:   document.getElementById('game-complete-new-game') as HTMLButtonElement,
      gameCompleteLobbyBtn:     document.getElementById('game-complete-lobby') as HTMLButtonElement,
      settingsToggle:           document.getElementById('settings-toggle') as HTMLButtonElement,
      settingsDrawer:           document.getElementById('settings-drawer') as HTMLElement,
      settingsClose:            document.getElementById('settings-close') as HTMLButtonElement,
      settingsBotStrength:      document.getElementById('settings-bot-strength') as HTMLSelectElement,
      settingsHandCount:        document.getElementById('settings-hand-count') as HTMLSelectElement,
      settingsAutoDeal:         document.getElementById('settings-auto-deal') as HTMLInputElement,
      settingsApply:            document.getElementById('settings-apply') as HTMLButtonElement,
      settingsSavedNote:        document.getElementById('settings-saved-note') as HTMLElement,
      settingsSound:            document.getElementById('settings-sound') as HTMLInputElement,
      gameCompleteReplayBtn:    document.getElementById('game-complete-replay') as HTMLButtonElement,
    };
    for (let i = 0; i < 4; i++) {
      this.elements.takeSeat[i] = document.querySelector(
        `.seat-button-${i} .take-seat`) as HTMLButtonElement;

      this.elements.kick[i] = document.querySelector(
        `.seat-button-${i} .kick`) as HTMLButtonElement;
    }

    // Phase F — resolve URL > localStorage > defaults BEFORE wiring the
    // picker so the initial select values match what we'll actually deal.
    this.phaseF = this.resolvePhaseFParams();
    this.applyPhaseFToPickers();
    this.applyVariantBodyClass();
    this.updateVariantBadge();

    this.setupEvents();
    this.setupDealButton();
    this.setupModal();
    this.setupClaimButtons();
    this.setupResultModal();
    this.setupDiceHud();
    this.setupBotBanner();
    this.setupPhaseFPickers();
    this.setupPickupHud();
    this.setupTurnBanner();
    this.setupMoveSeatPicker();
    this.setupGameCompleteModal();
    this.setupSettingsDrawer();
    this.setupSoundEffects();
    this.setupReplay();
    this.setupMobileDrawer();
  }

  private setupEvents(): void {
    this.elements.toggleDealer.onclick = () => this.world.toggleDealer();
    // Phase F — restored Riichi honba toggle (no-op for Changsha; the button
    // is hidden by CSS via the `riichi-only` class).
    this.elements.toggleHonba.onclick = () => {
      this.world.toggleHonba();
      this.refreshHonbaLabel();
    };

    this.client.seats.on('update', this.updateSeats.bind(this));
    this.client.nicks.on('update', this.updateSeats.bind(this));
    for (let i = 0; i < 4; i++) {
      this.elements.takeSeat[i].onclick = () => {
        this.client.seats.set(this.client.playerId(), { seat: i });
      };
    }
    for (let i = 0; i < 4; i++) {
      this.setupProgressButton(this.elements.kick[i], 1500, () => {
        const kickedId = this.client.seatPlayers[i];
        if (kickedId !== null) {
          this.client.seats.set(kickedId, { seat: null });
        }
      });
    }
    this.elements.leaveSeat.onclick = () => {
      this.client.seats.set(this.client.playerId(), { seat: null });
    };

    this.client.match.on('update', this.updateSetup.bind(this));
    this.updateSetup();

    // Hack for settings menu
    const doNotClose = ['LABEL', 'SELECT', 'OPTION'];
    for (const menu of Array.from(document.querySelectorAll('.dropdown-menu'))) {
      $(menu.parentElement!).on('hide.bs.dropdown', (e: Event) => {
        // @ts-ignore
        const target: HTMLElement | undefined = e.clickEvent?.target;
        if (target && doNotClose.indexOf(target.tagName) !== -1) {
          e.preventDefault();
        }
      });
    }

    // @ts-ignore
    $('[data-toggle="tooltip"]').tooltip();
  }

  private updateSetup(): void {
    const match = this.client.match.get(0);
    const conditions = match?.conditions ?? Conditions.initial();
    this.elements.setupDesc.textContent = Conditions.describe(conditions);
    // Phase F — keep the badge and honba label in sync with whatever the
    // server-pushed match decided.  Local-deal sets these too via deal().
    this.updateVariantBadge();
    this.refreshHonbaLabel();
  }

  private refreshHonbaLabel(): void {
    const match = this.client.match.get(0);
    const honba = match?.honba ?? 0;
    this.elements.toggleHonba.textContent = `Honba: ${honba}`;
  }

  private updateVariantBadge(): void {
    const match = this.client.match.get(0);
    const conditions = match?.conditions ?? Conditions.initial();
    this.elements.variantBadge.textContent = variantLabel(conditions.gameType);
  }

  private updateSeats(): void {
    const toDisable = [
      this.elements.deal,
      this.elements.toggleDealer,
      this.elements.leaveSeat,
      this.elements.toggleSetup,
    ];
    // Hicks 2026-05-26 — first-play P1 unblock (B3).  When `#deal` is
    // disabled (no seat) we surface the gate in a tooltip so the user
    // knows what to do next instead of guessing.  When enabled we
    // restore the action description.
    const dealEl = this.elements.deal;
    const updateDealTitle = (disabled: boolean): void => {
      dealEl.title = disabled ? 'Take a seat first' : 'Deal a new hand';
      dealEl.setAttribute('aria-label', dealEl.title);
    };
    // Phase I Wave 4 — spectators have no seat and can never take one.
    // Keep the .seat-buttons row hidden + the toDisable buttons disabled
    // so the seat-only affordances don't flash visible between connects.
    // CSS also hides these via body.spectating, but the inline
    // style.display = 'block' below would override a non-!important rule,
    // so we short-circuit here for belt-and-braces correctness.
    const spectating = readSpectatorFromUrl();
    if (spectating) {
      (document.querySelector('.seat-buttons')! as HTMLElement).style.display = 'none';
      for (const button of toDisable) {
        button.disabled = true;
      }
      updateDealTitle(true);
      this.refreshClaimButtons();
      this.refreshBotBanner();
      return;
    }
    if (this.client.seat === null) {
      (document.querySelector('.seat-buttons')! as HTMLElement).style.display = 'block';
      for (let i = 0; i < 4; i++) {
        const playerId = this.client.seatPlayers[i];
        if (playerId !== null) {
          this.elements.takeSeat[i].style.display = 'none';
          showEl(this.elements.kick[i]);

          const nick = this.client.nicks.get(playerId) || 'Player';
          const textElement = this.elements.kick[i].querySelector('.btn-progress-text')!;
          textElement.textContent = nick;
        } else {
          this.elements.takeSeat[i].style.display = '';
          hideEl(this.elements.kick[i]);
        }
      }
      for (const button of toDisable) {
        button.disabled = true;
      }
      updateDealTitle(true);
    } else {
      (document.querySelector('.seat-buttons')! as HTMLElement).style.display = 'none';
      for (const button of toDisable) {
        button.disabled = false;
      }
      updateDealTitle(false);
    }
    // Phase D — re-evaluate claim button states (selfSeat may have changed)
    // and refresh the bot banner whenever seat ↔ nick mapping shifts.
    this.refreshClaimButtons();
    this.refreshBotBanner();
    // Hicks postfix-verify P0 — seat ↔ player binding shifts dictate whose
    // hand we count for the discard cue; re-check the banner here too.
    this.refreshTurnBanner();
  }

  private setupDealButton(): void {
    const buttonElement = document.getElementById('deal')! as HTMLButtonElement;

    // Hicks 2026-05-26 — first-play P1 unblock (B3).  The legacy 600 ms
    // press-and-hold (setupProgressButton) was opaque: users clicked,
    // nothing happened, no toast.  Replaced with a single-click
    // handler.  The `.btn-progress` child is kept so the existing CSS
    // class doesn't break, but its width stays at 0% so the progress
    // ribbon never appears.  Tooltip surfaces the disabled-state
    // reason via the dynamic `title` attr (updated in `updateSeats`).
    const progressElement = buttonElement.querySelector('.btn-progress') as HTMLElement | null;
    if (progressElement !== null) {
      progressElement.style.transitionDuration = '0ms';
      progressElement.style.width = '0%';
    }
    const fireDeal = (): void => {
      const dealType = this.elements.dealType.value as DealType;
      // Phase F — fold the latest picker selections into Conditions so the
      // server (or local-relay path) sees the right variant + fives + points
      // + deal-mode on this deal.  defaultsFor() gives us safe per-variant
      // baselines; the picker values then override.
      const overrides = this.collectConditionOverrides();
      this.world.deal(dealType, overrides);
      this.hideSetup();
      buttonElement.blur();
    };
    buttonElement.onmousedown = null;
    buttonElement.onmouseup = null;
    buttonElement.onmouseleave = null;
    buttonElement.addEventListener('click', () => {
      if (buttonElement.disabled) return;
      fireDeal();
    });
  }

  /**
   * Phase F — read every picker and produce a Conditions delta the world's
   * deal() can merge in.  Pickers that are hidden for the active variant
   * (e.g. `fives` on Changsha) are still read but their effect is harmless
   * because Conditions.defaultsFor() resets those fields per-variant first.
   */
  private collectConditionOverrides(): Partial<Conditions> {
    const gameType = this.elements.gameType.value as GameType;
    const base = Conditions.defaultsFor(gameType);
    base.fives    = this.elements.fives.value as Fives;
    base.points   = this.elements.points.value as Points;
    base.dealMode = this.elements.dealMode.value as DealMode;
    return base;
  }

  private setupProgressButton(
      buttonElement: HTMLButtonElement,
      transitionTime: number, onSuccess: () => void): void {
    const progressElement = buttonElement.querySelector('.btn-progress')! as HTMLElement;

    progressElement.style.transitionDuration = `${transitionTime}ms`;

    let startPressed: number | null = null;
    const waitTime = transitionTime + 0;

    const start = (): void => {
      if (startPressed === null) {
        progressElement.style.width = '100%';
        startPressed = new Date().getTime();
      }
    };

    const cancel = (): void => {
      progressElement.style.width = '0%';
      startPressed = null;
      buttonElement.blur();
    };

    const commit = (): void => {
      const success = startPressed !== null && new Date().getTime() - startPressed > waitTime;
      progressElement.style.width = '0%';
      startPressed = null;
      buttonElement.blur();

      if (success) {
        onSuccess();
      }
    };

    buttonElement.onmousedown = start;
    buttonElement.onmouseup = commit;
    buttonElement.onmouseleave = cancel;
  }

  private hideSetup(): void {
    // @ts-ignore
    $('#setup-group').collapse('hide');
  }

  private setupModal(): void {
    const modalBody = document.querySelector('#viewer-modal .modal-body')!;

    const links = document.querySelectorAll('.show-modal');
    const embeds: Array<HTMLEmbedElement> = [];
    for (let i = 0; i < links.length; i++) {
      const link = links[i] as HTMLAnchorElement;
      const embed = document.createElement('embed');
      embed.type = 'application/pdf';
      embed.src = link.href + '#navpanes=0';
      embed.style.display = 'none';
      modalBody.appendChild(embed);
      embeds.push(embed);
    }

    for (let i = 0; i < links.length; i++) {
      const link = links[i] as HTMLAnchorElement;
      link.addEventListener('click', (e) => {
        for (let j = 0; j < embeds.length; j++) {
          embeds[j].style.display = i == j ? 'block': 'none';
        }

        // @ts-ignore
        $('#viewer-modal').modal();
        e.preventDefault();
      });
    }
  }

  // ---------------------------------------------------------------------
  // Phase D — Claim arc UI.
  //
  //   server pushes claim[seat] = { available, deadline, source, tile }
  //   client enables matching buttons, renders countdown timer,
  //   on click sends claim[selfSeat] = { action, type }, auto-pass on expiry.
  // ---------------------------------------------------------------------

  private setupClaimButtons(): void {
    const buttons: Array<['Pung'|'Chow'|'Kong'|'Hu', HTMLButtonElement]> = [
      ['Pung', this.elements.claim.Pung],
      ['Chow', this.elements.claim.Chow],
      ['Kong', this.elements.claim.Kong],
      ['Hu',   this.elements.claim.Hu],
    ];
    for (const [type, btn] of buttons) {
      btn.onclick = () => this.sendClaim({ action: 'claim', type });
    }
    this.elements.claim.Pass.onclick = () => this.sendClaim({ action: 'pass', type: null });

    this.client.claim.on('update', this.onClaimUpdate.bind(this));
    // initial state
    this.refreshClaimButtons();
  }

  private onClaimUpdate(entries: Array<[string, ClaimWindowEntry | null]>): void {
    // Look at every entry — there can be entries for other seats too. We only
    // surface a claim window when one targets `this.client.seat`.
    const selfSeat = this.client.seat;
    if (selfSeat === null) {
      this.activeClaim = null;
      this.refreshClaimButtons();
      return;
    }
    const selfKey = String(selfSeat);
    for (const [key, value] of entries) {
      if (key !== selfKey) continue;
      this.activeClaim = value ?? null;
    }
    // If no entry in this batch targeted us, fall back to the collection's
    // current state so reconnect / full-sync paths see the latest.
    if (this.activeClaim === null) {
      this.activeClaim = this.client.claim.get(selfKey);
    }
    this.refreshClaimButtons();
  }

  private refreshClaimButtons(): void {
    const claim = this.activeClaim;
    const allTypes: Array<'Pung' | 'Chow' | 'Kong' | 'Hu'> = ['Pung', 'Chow', 'Kong', 'Hu'];

    if (!claim) {
      for (const t of allTypes) {
        this.elements.claim[t].disabled = true;
      }
      this.elements.claim.Pass.disabled = true;
      this.stopClaimTimer();
      hideEl(this.elements.claimCountdown);
      // Hicks postfix-verify P0 — claim window closed; banner may need to
      // fall back to a discard / pickup cue or hide entirely.
      this.refreshTurnBanner();
      return;
    }

    for (const t of allTypes) {
      this.elements.claim[t].disabled = !(Array.isArray(claim.available) && claim.available.includes(t));
    }
    this.elements.claim.Pass.disabled = false;
    showEl(this.elements.claimCountdown);
    this.tickClaimCountdown();
    this.startClaimTimer();
    // Hicks postfix-verify P0 — claim window opened; banner takes priority.
    this.refreshTurnBanner();
  }

  private startClaimTimer(): void {
    this.stopClaimTimer();
    this.claimTickHandle = window.setInterval(
      () => this.tickClaimCountdown(),
      CLAIM_TICK_MS,
    );
  }

  private stopClaimTimer(): void {
    if (this.claimTickHandle !== null) {
      window.clearInterval(this.claimTickHandle);
      this.claimTickHandle = null;
    }
  }

  private tickClaimCountdown(): void {
    const claim = this.activeClaim;
    if (!claim) {
      this.stopClaimTimer();
      return;
    }
    // Frost 2026-05-29 — deadline=0 means "no client-side countdown; server
    // enforces the timeout".  Without this guard the side-panel claim UI
    // immediately auto-passed when the backend (legacy contract) omitted
    // the deadline, hiding the claim window before the player ever saw it.
    if (claim.deadline <= 0) {
      this.elements.claimCountdownValue.textContent = '—';
      // Hicks postfix-verify P0 — keep banner countdown text in sync even
      // when the server is owning the timeout (renders as "—").
      this.updateTurnBannerCountdown(null);
      this.stopClaimTimer();
      return;
    }
    const remainingMs = claim.deadline - Date.now();
    if (remainingMs <= 0) {
      // Auto-pass on expiry.
      this.elements.claimCountdownValue.textContent = '0.0';
      this.updateTurnBannerCountdown(0);
      this.sendClaim({ action: 'pass', type: null });
      return;
    }
    this.elements.claimCountdownValue.textContent = (remainingMs / 1000).toFixed(1);
    this.updateTurnBannerCountdown(remainingMs / 1000);
  }

  private sendClaim(action: ClaimAction): void {
    const selfSeat = this.client.seat;
    if (selfSeat === null) return;
    if (!this.activeClaim) return;
    if (action.action === 'claim'
        && !(Array.isArray(this.activeClaim.available) && this.activeClaim.available.includes(action.type))) {
      return; // defensive — should not happen because button is disabled
    }
    this.client.claim.set(String(selfSeat), action as any);
    // Locally close the claim window — the server will push a definitive
    // close, but we want immediate button-disable so a player can't
    // double-click claim+pass.
    this.activeClaim = null;
    this.refreshClaimButtons();
  }

  // ---------------------------------------------------------------------
  // Phase D — Scoring panel (result modal).
  //
  //   server pushes result.current = { winner, type, score, hand, nextBanker }
  //   client renders modal, Next Hand button sends match[1] = { action: 'nextHand' }
  // ---------------------------------------------------------------------

  private setupResultModal(): void {
    this.elements.resultNext.onclick = () => {
      // Bishop accepts a sentinel match entry to advance to the next hand.
      // Use key 1 to avoid clobbering key 0 (the live MatchInfo).
      this.client.match.set(1, { action: 'nextHand' } as unknown as MatchInfo);
      // @ts-ignore
      $('#result-modal').modal('hide');
    };
    this.client.result.on('update', this.onResultUpdate.bind(this));
  }

  private onResultUpdate(entries: Array<[string, HandResultEntry | null]>): void {
    for (const [key, value] of entries) {
      if (key !== 'current') continue;
      if (value === null) {
        // @ts-ignore
        $('#result-modal').modal('hide');
        continue;
      }
      // Phase J Wave 2 — accumulate per-hand history client-side so the
      // end-of-game modal can render a recap even when the runtime
      // doesn't push a dedicated `handHistory` array.  Skip duplicates
      // that arise from full-sync replays (key is "current" so we use
      // a structural fingerprint instead of array length).
      this.recordHandResult(value);
      this.renderResult(value);
      // Phase J Wave 3 — Fire the per-hand SFX off the same UPDATE that
      // raised the result modal.  Hu → fanfare, Draw → washout, ZhaHu
      // is left silent (no SFX defined; the modal already conveys it).
      if (value.type === 'Hu') {
        Sound.play('win');
      } else if (value.type === 'Draw') {
        Sound.play('washout');
      }
      // @ts-ignore
      $('#result-modal').modal('show');
    }
  }

  // Phase J Wave 2 — append the latest hand result to the client-side
  // history buffer used by the end-of-game modal.  Deduplicated against
  // the previous entry so connect-time full-syncs (which replay the
  // current `result` entry) don't double-count.
  private recordHandResult(result: HandResultEntry): void {
    const last = this.handHistory[this.handHistory.length - 1];
    if (last && JSON.stringify(last) === JSON.stringify(result)) return;
    this.handHistory.push(result);
  }

  private renderResult(result: HandResultEntry): void {
    let headline: string;
    let winnerLine: string;
    switch (result.type) {
      case 'Hu': {
        headline = '胡!';
        const winnerNick = this.nickForSeat(result.winner) ?? `Seat ${result.winner}`;
        winnerLine = `${winnerNick} 赢了 wins!`;
        break;
      }
      case 'ZhaHu': {
        headline = '诈胡! False Hu';
        const winnerNick = this.nickForSeat(result.winner) ?? `Seat ${result.winner}`;
        winnerLine = `${winnerNick} declared a false win`;
        break;
      }
      case 'Draw':
      default: {
        headline = '流局 Draw';
        winnerLine = 'No winner this hand';
        break;
      }
    }
    this.elements.resultHeadline.textContent = headline;
    this.elements.resultWinner.textContent = winnerLine;

    // Phase I Wave 2 — self-draw / discard pill next to the winner name.
    // RobbingKong stays in the chip strip (rendered by renderResultPatternChips)
    // since it's already the established Wave 2 affordance; the new pill
    // classes give all three a consistent visual language.
    this.renderResultWinTypeBadge(result);

    // Phase H Wave 2 — render stacked-pattern chips + RobbingKong badge.
    this.renderResultPatternChips(result);

    // Phase I Wave 1 — render the score-multiplier breakdown (Base/×N/Total
    // + per-seat payments) underneath the chip strip.  Backward-compat: when
    // the wire payload carries no `scoreResult`, this block hides itself.
    this.renderResultScoreBreakdown(result);

    // Score deltas table.
    const tbody = this.elements.resultScoreBody;
    tbody.innerHTML = '';
    const ordered = [...(result.score ?? [])].sort((a, b) => a.seat - b.seat);
    for (const { seat, delta } of ordered) {
      const tr = document.createElement('tr');
      const tdSeat = document.createElement('td');
      tdSeat.textContent = String(seat);
      const tdNick = document.createElement('td');
      tdNick.textContent = this.nickForSeat(seat) ?? 'Player';
      const tdDelta = document.createElement('td');
      tdDelta.textContent = delta > 0 ? `+${delta}` : String(delta);
      tdDelta.style.color = delta > 0 ? '#9ee69e' : delta < 0 ? '#ff9494' : '#cccccc';
      tr.appendChild(tdSeat);
      tr.appendChild(tdNick);
      tr.appendChild(tdDelta);
      tbody.appendChild(tr);
    }

    // Winning hand tiles.
    const handDiv = this.elements.resultHand;
    handDiv.innerHTML = '';
    for (const tile of result.hand) {
      const { text, suit } = tileLabel(tile);
      const cell = document.createElement('div');
      cell.className = `result-tile ${suit}`;
      cell.textContent = text;
      handDiv.appendChild(cell);
    }
  }

  // Phase I Wave 2 — render a 自摸 / 点炮 pill next to the winning seat name
  // based on the wire `winResult.winType` field (one of 'selfDraw', 'discard',
  // 'robbingKong' — see backend ChangshaToAutotableTranslator.WinMethodToWire).
  //
  // RobbingKong is intentionally NOT rendered here: it stays in the chip
  // strip (see renderResultPatternChips), where the Wave 2 badge already
  // lives.  The pill classes are shared so all three render with
  // consistent styling.
  //
  // No-ops on Draw / ZhaHu / pre-W2 wire payloads where `winType` is absent.
  private renderResultWinTypeBadge(result: HandResultEntry): void {
    const winnerEl = this.elements.resultWinner;
    // Drop any previous-render badge first.  textContent reset in
    // renderResult already wipes children, but be defensive in case the
    // call order changes.
    const stale = winnerEl.querySelector('.result-win-type-pill');
    if (stale) stale.remove();
    if (result.type !== 'Hu') return;

    const extras = result as HandResultEntry & ResultExtras;
    const winType = (
      extras.winResult?.winType ?? extras.WinResult?.WinType ?? ''
    ).toString();
    const sourceSeat =
      extras.winResult?.sourceSeatIndex ?? extras.WinResult?.SourceSeatIndex;

    let cls: string | null = null;
    let label = '';
    switch (winType) {
      case 'selfDraw':
        cls = 'win-type-self-draw';
        label = '自摸 Self-Draw';
        break;
      case 'discard': {
        cls = 'win-type-discard';
        // Name the source seat when the wire payload carries a sane value
        // (0..3 in a 4-player game; negative / NaN means "unknown").
        if (typeof sourceSeat === 'number' && sourceSeat >= 0) {
          const nick = this.nickForSeat(sourceSeat);
          const seatLabel = nick ? `Seat ${sourceSeat} (${nick})` : `Seat ${sourceSeat}`;
          label = `点炮 Discard ← ${seatLabel}`;
        } else {
          label = '点炮 Discard';
        }
        break;
      }
      case 'robbingKong':
        // Render nothing here — the chip-strip badge handles RobbingKong.
        return;
      default:
        return;
    }

    const pill = document.createElement('span');
    pill.className = `result-win-type-pill ${cls}`;
    pill.textContent = label;
    winnerEl.appendChild(pill);
  }

  // Phase H Wave 2 — surface V2 win-detector metadata in the result panel.
  //
  // Two new pieces of info ride the wire when Bishop's WinDetectionResult /
  // WinResult fields populate `result.current`:
  //   1. allPatterns[] — every big-win pattern that fires this hand
  //      (SevenPairs, AllPungs, FullFlush, NineTerminals).  Rendered as a
  //      colour-coded chip strip.  A single-pattern win renders one chip;
  //      a stacked hand (e.g. 清一色 + 碰碰胡) renders the full strip.
  //   2. isRobbedKong / method === 'RobbingKong' — 抢杠胡 badge above the
  //      chips, explaining WHY the win fired (claimed mid-added-kong).
  //
  // Fields are optional — when the backend hasn't pushed them yet we render
  // nothing and the legacy result-modal layout is untouched (chip container
  // stays display:none, so it occupies zero vertical space).
  private renderResultPatternChips(result: HandResultEntry): void {
    const extras = result as HandResultEntry & ResultExtras;
    const allPatterns = extras.allPatterns ?? extras.AllPatterns ?? [];
    const pattern = extras.pattern ?? extras.Pattern;
    const method = extras.method ?? extras.Method;
    const isRobbedKong = extras.isRobbedKong ?? extras.IsRobbedKong
                       ?? method === 'RobbingKong';

    let chipBox = document.getElementById('result-pattern-chips');
    if (!chipBox) {
      // Lazy-create the chip container immediately after #result-winner so
      // we don't need to edit index.html (out of Phase H Wave 2 frontend scope).
      chipBox = document.createElement('div');
      chipBox.id = 'result-pattern-chips';
      const winnerEl = this.elements.resultWinner;
      winnerEl.parentElement?.insertBefore(chipBox, winnerEl.nextSibling);
    }
    chipBox.innerHTML = '';

    // Chips/badge only make sense for a real Hu — drop them on Draw / ZhaHu.
    if (result.type !== 'Hu') {
      chipBox.style.display = 'none';
      return;
    }

    // Robbing-kong badge first — it's the "how" of the win, not a pattern.
    // Phase I Wave 2 — restyled to share the new `.result-win-type-pill`
    // base class so it lines up visually with the self-draw / discard
    // pills rendered next to the winner name.
    if (isRobbedKong) {
      const badge = document.createElement('span');
      badge.className = 'result-win-type-pill win-type-robbing-kong';
      badge.textContent = '抢杠 Robbing the Kong';
      chipBox.appendChild(badge);
    }

    // Pattern chips — prefer AllPatterns when the backend ships it, else
    // fall back to the legacy single Pattern field.  Standard is intentionally
    // skipped: it's the baseline 4-sets-and-a-pair hand, not a stack-worthy
    // big-win pattern (Ripley §2.3).  Match both PascalCase and camelCase
    // spellings of "standard" so either wire vocabulary skips the chip.
    //
    // Phase J Wave 3 — sort the surviving patterns through the canonical
    // PATTERN_DISPLAY_ORDER so HeavenlyHand → EarthlyHand → contextual
    // big-wins → structural big-wins → singleWait, then alphabetical.
    const filteredAllPatterns = sortPatterns(
      Array.from(allPatterns)
        .filter(p => normalizePatternKey(p) !== 'standard')
    );
    const patterns: string[] = filteredAllPatterns.length > 0
      ? filteredAllPatterns
      : (pattern && normalizePatternKey(pattern) !== 'standard' ? [pattern] : []);

    for (const p of patterns) {
      const key = normalizePatternKey(p);
      const label = PATTERN_LABELS[key] ?? p;
      const extraClass = PATTERN_CHIP_CLASSES[key] ?? '';
      const chip = document.createElement('span');
      chip.className = `result-pattern-chip${extraClass ? ' ' + extraClass : ''}`;
      chip.textContent = label;
      // Phase I Wave 2 — append a hover-only tooltip with the Chinese name
      // (大字) + one-line English description.  Pure-CSS reveal:
      // pointer-events:none keeps the tooltip from blocking clicks.
      const tip = PATTERN_TOOLTIPS[key];
      if (tip) {
        const tooltip = document.createElement('div');
        tooltip.className = 'pattern-tooltip';
        const cn = document.createElement('span');
        cn.className = 'pattern-tooltip-cn';
        cn.textContent = tip.cn;
        const en = document.createElement('span');
        en.className = 'pattern-tooltip-en';
        en.textContent = tip.en;
        tooltip.appendChild(cn);
        tooltip.appendChild(en);
        chip.appendChild(tooltip);
      }
      chipBox.appendChild(chip);
    }

    chipBox.style.display = chipBox.childElementCount > 0 ? '' : 'none';
  }

  // Phase I Wave 1 — score-multiplier breakdown block.
  //
  // Layout (rendered between #result-pattern-chips and #result-score):
  //   🏆 SEAT 2 — BIG WIN                  (or 🎉 SMALL WIN)
  //   Base: 6  Multiplier: ×3 (3 patterns)
  //   Total: 18 to claim
  //   Payments:
  //     Seat 0 → Seat 2: 6
  //     Seat 1 → Seat 2: 6
  //     Seat 3 → Seat 2: 6
  //
  // Sources off the wire:
  //   result.scoreResult.{category, basePoints, payments[]}     (Phase I)
  //   result.allPatterns[] / result.winResult.allPatterns[]     (Phase H W2)
  //
  // The multiplier label is `allPatterns.length` clamped to [1, 3] — matching
  // the backend ScoringService.CalculateScore clamp (see backend
  // Changsha/ScoringService.cs:91-93).  When `allPatterns` is empty we fall
  // back to single-pattern display (no multiplier row).
  //
  // The Base displayed is reverse-derived as `basePoints / multiplier` so the
  // math reads cleanly even though the backend ships the post-multiplier
  // sum as `basePoints` (sum of payments).  This is purely a presentation
  // choice — payments[] is the source of truth and is always shown verbatim.
  private renderResultScoreBreakdown(result: HandResultEntry): void {
    const extras = result as HandResultEntry & ResultExtras;
    const scoreResultRaw = extras.scoreResult ?? extras.ScoreResult;
    const allPatterns =
      extras.allPatterns ?? extras.AllPatterns ??
      extras.winResult?.allPatterns ?? extras.WinResult?.AllPatterns ?? [];

    let block = document.getElementById('result-score-breakdown');
    if (!block) {
      block = document.createElement('div');
      block.id = 'result-score-breakdown';
      const chips = document.getElementById('result-pattern-chips');
      const parent = chips?.parentElement ?? this.elements.resultWinner.parentElement;
      const anchor = chips ?? this.elements.resultWinner;
      parent?.insertBefore(block, anchor.nextSibling);
    }
    block.innerHTML = '';

    // Hide block for non-Hu results or when backend hasn't pushed scoreResult.
    if (result.type !== 'Hu' || !scoreResultRaw) {
      block.style.display = 'none';
      return;
    }

    const categoryRaw = (scoreResultRaw.category ?? scoreResultRaw.Category ?? '').toString();
    const isBig = categoryRaw.toLowerCase().includes('big');
    const basePoints = scoreResultRaw.basePoints ?? scoreResultRaw.BasePoints ?? 0;
    const rawPayments = scoreResultRaw.payments ?? scoreResultRaw.Payments ?? [];

    const patternCount = allPatterns.length;
    const multiplier = isBig ? Math.max(1, Math.min(3, patternCount || 1)) : 1;
    const baseBeforeMult = multiplier > 1 ? Math.floor(basePoints / multiplier) : basePoints;

    const winnerNick = this.nickForSeat(result.winner) ?? `Seat ${result.winner}`;
    const winnerLabel = `Seat ${result.winner}` +
      (this.nickForSeat(result.winner) ? ` (${winnerNick})` : '');

    // Category banner — names the multiplier source ("BIG WIN" / "SMALL WIN").
    const banner = document.createElement('div');
    banner.className = 'result-score-banner ' +
      (isBig ? 'category-big-win' : 'category-small-win');
    banner.textContent = isBig
      ? `🏆 ${winnerLabel} — BIG WIN`
      : `🎉 ${winnerLabel} — SMALL WIN`;
    block.appendChild(banner);

    // Base / Multiplier / Total row.  Single-pattern Big Wins and SmallWins
    // skip the multiplier span so the math line stays terse.
    const calcRow = document.createElement('div');
    calcRow.className = 'result-score-calc';
    const spanBase = document.createElement('span');
    spanBase.innerHTML = `<strong>Base:</strong> ${baseBeforeMult}`;
    calcRow.appendChild(spanBase);
    if (isBig && multiplier > 1) {
      const spanMult = document.createElement('span');
      const patternWord = patternCount === 1 ? 'pattern' : 'patterns';
      spanMult.innerHTML =
        `<strong>Multiplier:</strong> ×${multiplier} ` +
        `<small class="result-score-mult-note">(${patternCount} ${patternWord})</small>`;
      calcRow.appendChild(spanMult);
    }
    const spanTotal = document.createElement('span');
    spanTotal.innerHTML = `<strong>Total:</strong> ${basePoints} to claim`;
    calcRow.appendChild(spanTotal);
    block.appendChild(calcRow);

    // Payments list — one row per seat→seat transfer.  Reasons (e.g.
    // "BigWin-discard-dealer-x3") are tucked into a tooltip so the row stays
    // glanceable.
    if (rawPayments.length > 0) {
      const paymentsBox = document.createElement('div');
      paymentsBox.className = 'result-score-payments';
      const heading = document.createElement('div');
      heading.className = 'result-score-payments-heading';
      heading.textContent = 'Payments:';
      paymentsBox.appendChild(heading);
      for (const p of rawPayments) {
        const from = p.fromSeatIndex ?? p.FromSeatIndex ?? -1;
        const to = p.toSeatIndex ?? p.ToSeatIndex ?? -1;
        const amount = p.amount ?? p.Amount ?? 0;
        const reason = (p.reason ?? p.Reason ?? '').toString();
        const row = document.createElement('div');
        row.className = 'result-score-payment';
        if (reason) row.title = reason;
        row.textContent = `Seat ${from} → Seat ${to}: ${amount}`;
        paymentsBox.appendChild(row);
      }
      block.appendChild(paymentsBox);
    }

    block.style.display = '';
  }

  private nickForSeat(seat: number): string | null {
    const playerId = this.client.seatPlayers[seat];
    if (playerId === null) return null;
    return this.client.nicks.get(playerId) || null;
  }

  // ---------------------------------------------------------------------
  // Phase D — Dice + break-point HUD.
  //
  // Reuses the existing 3D dice render in center.ts (auto-fades after ~1 s)
  // and overlays a small 2D HUD that exposes the computed break-point column
  // — info the 3D dice can't show on its own.  Accepts either Bishop's new
  // {d1,d2,breakPoint} shape or the legacy {dice,state} shape.
  // ---------------------------------------------------------------------

  private setupDiceHud(): void {
    this.client.dice.on('update', this.onDiceUpdate.bind(this));
  }

  private onDiceUpdate(entries: Array<[string | number, DiceInfo | null]>): void {
    for (const [, value] of entries) {
      if (!value) continue;
      const d1 = value.d1 ?? value.dice?.[0];
      const d2 = value.d2 ?? value.dice?.[1];
      if (d1 == null || d2 == null) continue;
      // Skip the "no roll" sentinel that local play emits on non-rolling deals.
      if (value.state === 'ignore' && value.breakPoint === undefined) continue;
      this.showDiceHud(d1, d2, value.breakPoint);
    }
  }

  private showDiceHud(d1: number, d2: number, breakPoint?: number): void {
    const dieFaces = ['⚀', '⚁', '⚂', '⚃', '⚄', '⚅'];
    this.elements.diceHudD1.textContent = dieFaces[Math.max(1, Math.min(6, d1)) - 1];
    this.elements.diceHudD2.textContent = dieFaces[Math.max(1, Math.min(6, d2)) - 1];
    this.elements.diceHudSum.textContent = String(d1 + d2);
    if (breakPoint !== undefined) {
      this.elements.diceHudBreak.textContent = String(breakPoint);
      (this.elements.diceHudBreak.parentElement as HTMLElement).style.display = '';
    } else {
      (this.elements.diceHudBreak.parentElement as HTMLElement).style.display = 'none';
    }
    showEl(this.elements.diceHud);
    if (this.diceHudHandle !== null) {
      window.clearTimeout(this.diceHudHandle);
    }
    this.diceHudHandle = window.setTimeout(() => {
      hideEl(this.elements.diceHud);
      this.diceHudHandle = null;
    }, DICE_HUD_LIFETIME_MS);
  }

  // ---------------------------------------------------------------------
  // Phase D — Bot banner.
  //
  // Bishop's Phase D-backend auto-fills empty seats with bots when a human
  // sits down. We surface that visually so the player understands who they're
  // about to play against. Convention: bot nicks begin with "Bot " (e.g.
  // "Bot Alpha"). Until an explicit `is_bot` flag lands on SeatInfo this is
  // the cheapest reliable signal.
  // ---------------------------------------------------------------------

  private setupBotBanner(): void {
    // Re-render whenever nick/seat membership changes.
    this.client.nicks.on('update', this.refreshBotBanner.bind(this));
    this.refreshBotBanner();
  }

  private refreshBotBanner(): void {
    const banner = this.elements.botBanner;
    // Phase I Wave 4 — for spectators the bot banner IS the HUD: it
    // names which seats are bots and which difficulty.  For seated
    // players we keep the pre-Wave-4 behaviour (banner only shows once
    // the local player is seated).
    const spectating = readSpectatorFromUrl();
    if (!spectating && this.client.seat === null) {
      hideEl(banner);
      return;
    }
    const bots: Array<{ seat: number; nick: string }> = [];
    for (let i = 0; i < 4; i++) {
      if (!spectating && i === this.client.seat) continue;
      const playerId = this.client.seatPlayers[i];
      if (playerId === null) continue;
      const nick = this.client.nicks.get(playerId);
      if (isBotNick(nick)) {
        bots.push({ seat: i, nick: nick! });
      }
    }
    if (bots.length === 0) {
      hideEl(banner);
      return;
    }
    banner.innerHTML = '';
    const title = document.createElement('span');
    title.className = 'bot-banner-title';
    // Phase F — appended difficulty annotation reads the picker, since the
    // backend's bot engine hasn't shipped yet.  Once it has, this should
    // prefer a server-pushed `botDifficulty` from a future seats field.
    const diff = this.phaseF?.botDifficulty ?? 'medium';
    const diffWord = diff.charAt(0).toUpperCase() + diff.slice(1);
    title.textContent = `${bots.length} bot${bots.length === 1 ? '' : 's'} — ${diffWord} · `
      + `seat${bots.length === 1 ? '' : 's'} `
      + bots.map(b => b.seat).join(', ');
    banner.appendChild(title);
    const winds = ['E', 'S', 'W', 'N'];
    for (const { seat, nick } of bots) {
      const row = document.createElement('span');
      row.className = 'bot-row';
      row.textContent = `${nick} (${winds[seat] ?? '?'})`;
      banner.appendChild(row);
    }
    showEl(banner);
  }

  // ---------------------------------------------------------------------
  // Phase F — variant / deal-mode / bot pickers + URL params + localStorage.
  //
  // Pickers update Conditions.defaultsFor(gameType) at deal-time (see
  // setupDealButton).  Variant change requires a reload — the setup machinery
  // recomputes tile catalogues + slot groups at boot from Conditions, so
  // hot-swapping mid-session would leave dangling Things.
  // ---------------------------------------------------------------------

  /** URL > localStorage > Conditions defaults.  Called once in the ctor. */
  private resolvePhaseFParams(): Required<PhaseFParams> {
    const url = parseUrlParams();
    const ls  = readLocalStorage();
    const variant = url.variant ?? ls.variant ?? GameType.CHANGSHA;
    const defaults = Conditions.defaultsFor(variant);
    return {
      variant,
      dealMode:      url.dealMode      ?? ls.dealMode      ?? defaults.dealMode!,
      botCount:      url.botCount      ?? ls.botCount      ?? 0,
      botDifficulty: url.botDifficulty ?? ls.botDifficulty ?? 'medium',
      fives:         url.fives         ?? ls.fives         ?? defaults.fives!,
      points:        url.points        ?? ls.points        ?? defaults.points!,
    };
  }

  /** Apply the resolved Phase F params to the picker UI on boot. */
  private applyPhaseFToPickers(): void {
    this.elements.gameType.value      = this.phaseF.variant;
    this.elements.dealMode.value      = this.phaseF.dealMode;
    this.elements.botCount.value      = String(this.phaseF.botCount);
    this.elements.botDifficulty.value = this.phaseF.botDifficulty;
    this.elements.fives.value         = this.phaseF.fives;
    this.elements.points.value        = this.phaseF.points;
  }

  /**
   * Phase F — body class toggle drives `.changsha-only` / `.riichi-only`
   * visibility in style.css.  Called on boot and on variant-picker change
   * (though the latter recommends a reload before the next deal).
   */
  private applyVariantBodyClass(): void {
    const isChangsha = this.phaseF.variant === GameType.CHANGSHA;
    document.body.classList.toggle('variant-changsha', isChangsha);
    document.body.classList.toggle('variant-riichi',  !isChangsha);
  }

  private setupPhaseFPickers(): void {
    // Variant — page-reload semantics.  Persist immediately, warn the user.
    this.elements.gameType.onchange = () => {
      const newVariant = this.elements.gameType.value as GameType;
      writeLocalStorage(LS_VARIANT, newVariant);
      // Soft warning in the variant badge — full hot-swap is Phase G.
      this.elements.variantBadge.textContent =
        '↻ Reload to change to ' + variantLabel(newVariant);
    };

    this.elements.dealMode.onchange = () => {
      const v = this.elements.dealMode.value as DealMode;
      this.phaseF.dealMode = v;
      writeLocalStorage(LS_DEAL_MODE, v);
    };

    this.elements.botCount.onchange = () => {
      const v = parseInt(this.elements.botCount.value, 10);
      this.phaseF.botCount = v;
      writeLocalStorage(LS_BOT_COUNT, String(v));
      this.refreshBotBanner();
    };

    this.elements.botDifficulty.onchange = () => {
      const v = this.elements.botDifficulty.value as 'easy' | 'medium' | 'hard';
      this.phaseF.botDifficulty = v;
      writeLocalStorage(LS_BOT_DIFFICULTY, v);
      this.refreshBotBanner();
    };

    this.elements.fives.onchange = () => {
      const v = this.elements.fives.value as Fives;
      this.phaseF.fives = v;
      writeLocalStorage(LS_FIVES, v);
    };

    this.elements.points.onchange = () => {
      const v = this.elements.points.value as Points;
      this.phaseF.points = v;
      writeLocalStorage(LS_POINTS, v);
    };

    this.elements.resetPoints.onclick = () => {
      this.world.resetPoints(this.phaseF.points);
    };
  }

  // ---------------------------------------------------------------------
  // Phase F — Pickup HUD + roll-dice button.
  //
  // The runtime pushes a singleton `pickup` entry describing the current
  // affordance: who's expected to click, how many tiles, and which phase
  // we're in.  We translate that into:
  //   • "Your turn — pick N tiles" banner (with a Take-N button shortcut)
  //   • "Bot 2 is picking…" banner when another seat is on the clock
  //   • Roll-dice button (only on RollingDice phase + dealer === self)
  // ---------------------------------------------------------------------

  private setupPickupHud(): void {
    this.elements.rollDice.onclick = () => {
      this.world.emitRollDice();
    };
    this.elements.pickupTakeBtn.onclick = () => {
      this.world.emitTakePickup();
    };
    this.client.pickup.on('update', this.onPickupUpdate.bind(this));
  }

  private onPickupUpdate(entries: Array<[string | number, PickupEntry | null]>): void {
    // Only the "current" singleton is the authoritative snapshot.  Outbound
    // command keys ('rollDice' / 'take') round-trip back to us through the
    // collection but carry no inbound state.
    for (const [key,] of entries) {
      if (key !== 'current') continue;
      const pickup = this.client.pickup.get('current') ?? null;
      this.renderPickupHud(pickup);
      this.renderRollDiceButton(pickup);
      this.renderBreakMarker(pickup);
      // Hicks postfix-verify P0 — pickup affordance just shifted; re-check
      // whether the turn-banner should switch to a pickup cue (or back to
      // the discard cue once the pickup chain completes).
      this.refreshTurnBanner();
    }
  }

  private renderPickupHud(pickup: PickupEntry | null): void {
    const hud = this.elements.pickupHud;
    if (!pickup || pickup.count <= 0) {
      hud.style.display = 'none';
      return;
    }
    const selfSeat = this.client.seat;
    const isMine = selfSeat !== null && pickup.seatIndex === selfSeat;
    if (isMine) {
      this.elements.pickupHudText.textContent =
        `Your turn — pick ${pickup.count} tile${pickup.count === 1 ? '' : 's'}`;
      this.elements.pickupTakeCount.textContent = String(pickup.count);
      this.elements.pickupTakeBtn.style.display = '';
    } else {
      const winds = ['E', 'S', 'W', 'N'];
      const seatLabel = winds[pickup.seatIndex] ?? `Seat ${pickup.seatIndex}`;
      this.elements.pickupHudText.textContent =
        `${seatLabel} is picking ${pickup.count} tile${pickup.count === 1 ? '' : 's'}…`;
      this.elements.pickupTakeBtn.style.display = 'none';
    }
    showEl(hud);
  }

  private renderRollDiceButton(pickup: PickupEntry | null): void {
    const btn = this.elements.rollDice;
    if (!pickup) {
      hideEl(btn);
      return;
    }
    // The runtime can spell the phase as either 'RollingDice' or 'rollDice'
    // depending on JSON-serializer settings; accept both.
    const phaseStr = String(pickup.phase ?? '').toLowerCase();
    const isRollPhase = phaseStr === 'rollingdice' || phaseStr === 'rolldice';
    const selfSeat = this.client.seat;
    const isMine = selfSeat !== null && pickup.seatIndex === selfSeat;
    setElHidden(btn, !(isRollPhase && isMine));
  }

  private renderBreakMarker(pickup: PickupEntry | null): void {
    const marker = this.elements.breakMarker;
    if (!pickup || pickup.breakPoint === undefined || pickup.breakPoint === null) {
      hideEl(marker);
      return;
    }
    // Position the marker at one of 4 seat positions × ~18 wall columns.
    // The exact 3D-to-2D projection is owned by world.ts; for the MVP we
    // pin the marker to a small set of seat-local offsets and let the CSS
    // do the rest.  Bishop's authoritative breakPoint is a column index.
    const seat = pickup.seatIndex;
    const col = Math.max(0, Math.min(17, pickup.breakPoint));
    marker.dataset.seat = String(seat);
    marker.dataset.col = String(col);
    showEl(marker);
  }

  // ---------------------------------------------------------------------
  // Hicks postfix-verify P0 — Turn indicator banner.
  //
  // Vasquez surfaced (`vasquez-postfix-verify-1-p0-regression-found-…`)
  // that once seat 0 reaches 14 tiles (post-bot-discard, in-hand draw
  // unresolved) the bundle gives NO visual cue that it's the human's
  // turn to act.  Stephen — and her playtest doppelgänger in
  // playtest-artifacts — sits idle; the game waits forever.
  //
  // This wires a floating top-center banner (`#turn-banner`) with three
  // priority-ordered states:
  //
  //   1. CLAIM  — claim window targets self (highest priority since the
  //               window expires; the player has seconds, not minutes).
  //   2. PICKUP — pickup affordance targets self (post-deal dealer-extra,
  //               manual-pickup chain).
  //   3. DISCARD — my hand has 14 backend-authoritative tiles AND no
  //                pickup / claim window is in flight.
  //
  // The refresh fans in off four existing collection listeners (`seats`,
  // `pickup`, `claim`, `things`) — no new polling loop.  The `things`
  // hook is debounced so a deal batch (18-36 entries) only computes the
  // banner state once per animation frame.
  //
  // We also flip a `body.my-turn-discard` class so CSS can swap the
  // canvas cursor (P1 addition) without re-plumbing the THREE.js hover
  // material path.
  // ---------------------------------------------------------------------
  private turnBannerRafHandle: number | null = null;
  private turnBannerLastState: string = '';
  private setupTurnBanner(): void {
    // `things.update` fires per-batch; we debounce to the next animation
    // frame so a 32-tile deal doesn't recompute 32 times in a row.
    this.client.things.on('update', () => this.scheduleTurnBannerRefresh());
    // Re-evaluate at boot so the banner picks up any state that was
    // already present before our listeners attached (rare but safe).
    this.refreshTurnBanner();
  }

  private scheduleTurnBannerRefresh(): void {
    if (this.turnBannerRafHandle !== null) return;
    this.turnBannerRafHandle = window.requestAnimationFrame(() => {
      this.turnBannerRafHandle = null;
      this.refreshTurnBanner();
    });
  }

  /**
   * Recompute the turn-banner state from the current client/world snapshot
   * and patch the DOM iff the rendered state actually changed.  Stable
   * keying (`turnBannerLastState`) avoids redundant style/class writes that
   * would otherwise interrupt the CSS fade transition.
   */
  private refreshTurnBanner(): void {
    const banner = this.elements.turnBanner;
    if (!banner) return; // defensive — index.html should always carry it

    const selfSeat = this.client.seat;
    const spectating = readSpectatorFromUrl();

    // Spectators / unseated viewers never see a "your turn" cue.
    if (spectating || selfSeat === null) {
      this.applyTurnBannerState('', null, null, null);
      return;
    }

    // Priority 1 — claim window targeting self.
    if (this.activeClaim !== null) {
      const available = Array.isArray(this.activeClaim.available)
        ? this.activeClaim.available.join(' / ')
        : '';
      const text = available
        ? `Claim opportunity — ${available}`
        : 'Claim opportunity';
      this.applyTurnBannerState('claim', text, 'claim', /* show countdown */ true);
      return;
    }

    // Priority 2 — pickup affordance targeting self.
    const pickup = this.client.pickup.get('current') ?? null;
    if (pickup && pickup.count > 0 && pickup.seatIndex === selfSeat) {
      // The phase 'rollDice' state already shows the big dice button;
      // the pickup HUD covers the count.  We still surface a high-
      // contrast top-center cue so the user knows where to look.
      const text = pickup.count === 1
        ? 'Your turn — pick a tile from the wall'
        : `Your turn — pick ${pickup.count} tiles from the wall`;
      this.applyTurnBannerState('pickup', text, 'pickup', null);
      return;
    }

    // Priority 3 — extra hand tile (14 tiles, no pickup/claim in flight).
    // `world.hasExtraHandTile()` already encodes the count-only-real-
    // backend-tiles + skip-extra-preview-slot logic; reuse it so we don't
    // duplicate the discard-gate semantics.
    if (this.world.hasExtraHandTile()) {
      this.applyTurnBannerState(
        'discard',
        'Your turn — click a tile to discard',
        'discard',
        null,
      );
      return;
    }

    this.applyTurnBannerState('', null, null, null);
  }

  /**
   * Patch the banner DOM only when the (kind|text|countdown) tuple has
   * actually changed since the last call.  Toggles `body.my-turn-discard`
   * for the canvas cursor affordance (P1 addition).
   */
  private applyTurnBannerState(
    kind: 'claim' | 'pickup' | 'discard' | '',
    text: string | null,
    cls: 'claim' | 'pickup' | 'discard' | null,
    countdown: boolean | null,
  ): void {
    const banner = this.elements.turnBanner;
    const stateKey = `${kind}|${text ?? ''}|${countdown ? 'c' : '-'}`;
    if (stateKey !== this.turnBannerLastState) {
      this.turnBannerLastState = stateKey;
      banner.classList.remove(
        'turn-banner-claim', 'turn-banner-pickup', 'turn-banner-discard');
      if (kind === '' || text === null) {
        banner.classList.remove('visible');
        banner.textContent = '';
        // Keep `hidden` so the element doesn't accumulate stale aria-live
        // announcements between turns.
        banner.hidden = true;
      } else {
        banner.hidden = false;
        if (cls) banner.classList.add(`turn-banner-${cls}`);
        // Two children so we can update the countdown text in place
        // without re-flowing the label (see updateTurnBannerCountdown).
        banner.textContent = '';
        const label = document.createElement('span');
        label.className = 'turn-banner-text';
        label.textContent = text;
        banner.appendChild(label);
        if (countdown) {
          const cd = document.createElement('span');
          cd.className = 'turn-banner-countdown';
          cd.textContent = '';
          banner.appendChild(cd);
        }
        // Force a reflow so the visible-class transition runs the first
        // time the banner appears after a `hidden` toggle.
        // eslint-disable-next-line @typescript-eslint/no-unused-expressions
        banner.offsetHeight;
        banner.classList.add('visible');
      }
    }
    // Body-class toggle is idempotent; safe to call every refresh.
    document.body.classList.toggle('my-turn-discard', kind === 'discard');
  }

  /**
   * Update only the countdown span inside the banner.  Called from
   * `tickClaimCountdown` (CLAIM_TICK_MS) so the seconds-remaining text
   * stays smooth without recreating the surrounding DOM.
   */
  private updateTurnBannerCountdown(secondsRemaining: number | null): void {
    const cd = this.elements.turnBanner.querySelector(
      '.turn-banner-countdown') as HTMLElement | null;
    if (!cd) return;
    if (secondsRemaining === null) {
      cd.textContent = '—';
    } else {
      cd.textContent = `${secondsRemaining.toFixed(1)}s`;
    }
  }


  // ---------------------------------------------------------------------
  // Phase J Wave 1 — Hot-seat swap.
  //
  // Wires the Move button + inline picker.  The flow:
  //   1. Visibility is gated on connected() AND no active match.  We listen
  //      to `connect` / `disconnect` on the BaseClient and `update` on the
  //      match collection to re-evaluate, and also refresh whenever seats
  //      change so the per-option enabled state stays in sync with who
  //      currently holds which seat.
  //   2. Clicking Move toggles the inline picker panel (CSS-styled flex row
  //      of 5 buttons: Seat 0..3 + Spectate).
  //   3. The current seat's option is disabled; seats held by other players
  //      are disabled.  Spectate is always selectable when visible.
  //   4. Selecting an option performs a soft reconnect: we mutate the page
  //      URL's `?seat=` param via history.replaceState, then call
  //      `client.disconnect()`.  client-ui.ts's existing auto-reconnect
  //      flow (onDisconnect → setTimeout → connect → buildWsUrl) picks up
  //      the new `?seat=` off the URL on the next attempt.  gameId is
  //      preserved by definition (we never touch it).  Reconnect timing is
  //      RECONNECT_DELAY (≈2s) per client-ui.ts:7.
  //
  //   The captured `reconnectSeat` in client-ui.ts:onDisconnect ends up
  //   null after disconnect (perPlayer seats collection clears its entries
  //   before ClientUi.onDisconnect runs and resets client.seat), so the
  //   subsequent connect() won't re-seat us with the OLD seat.  The new
  //   seat comes purely from the URL via buildWsUrl.
  // ---------------------------------------------------------------------
  private setupMoveSeatPicker(): void {
    const btn = this.elements.moveSeatBtn;
    const panel = this.elements.moveSeatPanel;

    btn.onclick = (e: MouseEvent) => {
      e.stopPropagation();
      const open = !panel.hidden;
      setElHidden(panel, open ? true : false);
      if (!open) panel.style.display = 'flex';
      btn.setAttribute('aria-expanded', open ? 'false' : 'true');
      if (!open) this.refreshMoveSeatPicker();
    };

    for (const option of this.elements.moveSeatOptions) {
      option.onclick = (e: MouseEvent) => {
        e.stopPropagation();
        if (option.disabled) return;
        const raw = option.dataset.seat ?? '';
        const target = parseInt(raw, 10);
        if (isNaN(target)) return;
        this.softReconnectWithSeat(target);
      };
    }

    // Click outside closes the picker.  We bind on the document so any
    // mousedown that isn't inside the picker collapses it; using mousedown
    // (not click) so the panel doesn't briefly flash before re-closing
    // when the user clicks the toggle button again.
    document.addEventListener('mousedown', (e: MouseEvent) => {
      if (panel.hidden) return;
      const target = e.target as Node | null;
      if (target && (panel.contains(target) || btn.contains(target))) return;
      hideEl(panel);
      btn.setAttribute('aria-expanded', 'false');
    });

    this.client.on('connect', () => this.refreshMoveSeatVisibility());
    this.client.on('disconnect', () => this.refreshMoveSeatVisibility());
    this.client.match.on('update', () => this.refreshMoveSeatVisibility());
    this.client.seats.on('update', () => this.refreshMoveSeatPicker());

    this.refreshMoveSeatVisibility();
  }

  // Phase J Wave 1 — Move row is shown only while the WS is connected AND
  // no match has been dealt yet.  `client.connected()` mirrors the WS
  // open state; `match.get(0)` becomes non-null as soon as Deal kicks off
  // the first hand (Bishop's runtime pushes the MatchInfo on dealStart).
  // Between hands the match stays set, so the Move row remains hidden —
  // which is the safer default (no mid-match seat juggling).
  private refreshMoveSeatVisibility(): void {
    const row = this.elements.moveSeatRow;
    const panel = this.elements.moveSeatPanel;
    const connected = this.client.connected();
    const inSeating = this.client.match.get(0) === null;
    const visible = connected && inSeating;
    setElHidden(row, !visible);
    if (!visible) {
      hideEl(panel);
      this.elements.moveSeatBtn.setAttribute('aria-expanded', 'false');
    } else {
      this.refreshMoveSeatPicker();
    }
  }

  // Phase J Wave 1 — Per-option enabled state.  Disables the current
  // seat (no-op move) and seats already held by other players.  The
  // Spectate option (-1) is always selectable when the row is visible.
  private refreshMoveSeatPicker(): void {
    const selfSeat = this.client.seat;
    const spectating = readSpectatorFromUrl();
    for (const option of this.elements.moveSeatOptions) {
      const raw = option.dataset.seat ?? '';
      const seat = parseInt(raw, 10);
      if (isNaN(seat)) {
        option.disabled = true;
        continue;
      }
      if (seat === -1) {
        // Disable Spectate when already spectating.
        option.disabled = spectating;
        continue;
      }
      if (selfSeat !== null && seat === selfSeat) {
        option.disabled = true;
        continue;
      }
      const occupant = this.client.seatPlayers[seat];
      option.disabled = occupant !== null && occupant !== this.client.playerId();
    }
  }

  // Phase J Wave 1 — Soft reconnect with the chosen seat.  We rewrite the
  // page URL's `?seat=` param (preserving the rest of the query, including
  // the sticky `?gameId=` that client-ui.ts:setUrlState pins on every
  // connect) and then close the current WS.  client-ui.ts's existing
  // auto-reconnect kicks in after RECONNECT_DELAY (≈2s) and picks up the
  // new seat off the URL via buildWsUrl().
  private softReconnectWithSeat(seat: number): void {
    if (seat !== -1 && (seat < 0 || seat > 3)) return;

    const url = new URL(window.location.href);
    url.searchParams.set('seat', String(seat));
    history.replaceState(undefined, '', url.pathname + url.search);

    // Close the picker so the next connect lands on a clean HUD.
    hideEl(this.elements.moveSeatPanel);
    this.elements.moveSeatBtn.setAttribute('aria-expanded', 'false');

    // Drop our local seat ahead of the disconnect so the reconnect doesn't
    // accidentally re-claim the OLD seat via client-ui.ts's reconnectSeat
    // capture.  client.seats is perPlayer, so the entry would be cleared
    // on disconnect anyway, but this belt-and-braces avoids the brief
    // window where another listener might read the stale value.
    const playerId = this.client.playerId();
    if (playerId !== 'offline') {
      this.client.seats.set(playerId, { seat: null });
    }

    this.client.disconnect();
  }

  // ---------------------------------------------------------------------
  // Phase J Wave 2 — End-of-game summary modal.
  //
  // Subscribes to the `gameComplete` collection (singleton key="current")
  // that Bishop's runtime emits when MaxHands is exhausted.  Renders a
  // modal showing:
  //   • Per-seat total point delta across the match.  Sourced from the
  //     payload's `totalScores` when present, else derived client-side by
  //     summing the `result.current` updates we observed.
  //   • Hand-by-hand recap.  Sourced from the payload's `handHistory`
  //     when present, else the client-side history.
  //
  // The two action buttons:
  //   • New Game  — bake the current settings-drawer state into the URL
  //                 and reload, so the runtime spins up a fresh game.
  //   • Lobby     — punt the user to the bare URL, where lobby.ts opens
  //                 the panel automatically (shouldShowOnLoad).
  // ---------------------------------------------------------------------
  private setupGameCompleteModal(): void {
    this.elements.gameCompleteNewGameBtn.onclick = () => {
      this.dismissGameCompleteModal();
      this.startNewGameFromSettings();
    };
    this.elements.gameCompleteLobbyBtn.onclick = () => {
      this.dismissGameCompleteModal();
      // Reset URL to bare so the lobby auto-opens (lobby.ts:shouldShowOnLoad).
      window.location.search = '';
    };
    // Phase J Wave 3 — "View Replay" hands the captured tile-move log
    // (+ the runtime's handHistory when present) to the Replay viewer
    // and slides the replay screen open.
    // Phase J Wave 7 — prefer the server replay endpoint when a gameId
    // is available in the URL.  The launcher falls back to an empty
    // payload on 404 so the shell still renders.
    if (this.elements.gameCompleteReplayBtn) {
      this.elements.gameCompleteReplayBtn.onclick = () => {
        this.dismissGameCompleteModal();
        const gameId = readGameIdFromLocation();
        if (gameId !== null) {
          // Fire-and-forget; the launcher dispatches into Replay.openServer
          // (or Replay.open with an empty payload on failure).
          void openReplayForGame(gameId);
          return;
        }
        const serverHistory =
          this.lastGameCompletePayload?.handHistory
          ?? this.lastGameCompletePayload?.HandHistory;
        this.replay.open(serverHistory);
      };
    }
    this.client.gameComplete.on('update', this.onGameCompleteUpdate.bind(this));

    // Also re-evaluate on every new JOIN: clear any stale history (we may
    // be reconnecting to a fresh game) and re-arm the modal-shown guard.
    this.client.on('connect', () => {
      // Drop client-side history on JOIN — full-sync replay will repopulate.
      this.handHistory = [];
      this.gameCompleteShown = false;
    });
  }

  private onGameCompleteUpdate(
    entries: Array<[string, GameCompleteEntry | null]>,
  ): void {
    for (const [key, value] of entries) {
      if (key !== 'current') continue;
      if (value === null) {
        // Tombstone — server cleared the flag (new game starting).  Hide
        // and reset the dismissal guard so the next completion shows.
        this.dismissGameCompleteModal();
        this.gameCompleteShown = false;
        this.lastGameCompletePayload = null;
        continue;
      }
      const isComplete =
        value.isComplete ?? value.IsComplete ??
        value.isGameComplete ?? value.IsGameComplete ?? false;
      if (!isComplete) continue;
      if (this.gameCompleteShown) continue;
      this.gameCompleteShown = true;
      this.lastGameCompletePayload = value;
      this.renderGameComplete(value);
      // Phase J Wave 3 — celebration sting when the match ends.
      Sound.play('gameComplete');
      // @ts-ignore
      $('#game-complete-modal').modal('show');
    }
  }

  private dismissGameCompleteModal(): void {
    // @ts-ignore
    $('#game-complete-modal').modal('hide');
  }

  private renderGameComplete(payload: GameCompleteEntry): void {
    // ── Subtitle: "N hands · East wind" — pulled from the payload's
    // optional MaxHands or our own history length.
    const totalHands = (
      payload.maxHands ?? payload.MaxHands ?? this.handHistory.length
    );
    this.elements.gameCompleteSubtitle.textContent =
      totalHands > 0
        ? `${totalHands}-hand match complete`
        : 'Match complete';

    // ── Per-seat totals ──────────────────────────────────────────────
    const totals = this.computeFinalScores(payload);
    const totalsBody = this.elements.gameCompleteTotalsBody;
    totalsBody.innerHTML = '';
    const winds = ['E 东', 'S 南', 'W 西', 'N 北'];
    const ranked = [...totals.entries()]
      .sort((a, b) => b[1] - a[1]);
    for (const [seat, delta] of ranked) {
      const tr = document.createElement('tr');
      const tdSeat = document.createElement('td');
      tdSeat.textContent = `${seat} (${winds[seat] ?? '?'})`;
      const tdNick = document.createElement('td');
      const nick = this.nickForSeat(seat);
      const isSelf = seat === this.client.seat;
      tdNick.textContent = (nick ?? `Seat ${seat}`) + (isSelf ? ' (You)' : '');
      if (isSelf) tdNick.classList.add('game-complete-self');
      const tdDelta = document.createElement('td');
      tdDelta.textContent = delta > 0 ? `+${delta}` : String(delta);
      tdDelta.style.color =
        delta > 0 ? '#9ee69e' : delta < 0 ? '#ff9494' : '#cccccc';
      tdDelta.style.fontWeight = 'bold';
      tr.appendChild(tdSeat);
      tr.appendChild(tdNick);
      tr.appendChild(tdDelta);
      totalsBody.appendChild(tr);
    }

    // ── Hand-by-hand recap ──────────────────────────────────────────
    const recap = this.elements.gameCompleteRecap;
    recap.innerHTML = '';
    const hands =
      payload.handHistory ?? payload.HandHistory ?? this.handHistory;
    if (hands.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'game-complete-recap-empty';
      empty.textContent = 'No hands recorded.';
      recap.appendChild(empty);
    } else {
      hands.forEach((h, i) => {
        const row = document.createElement('div');
        row.className = 'game-complete-recap-row';
        const label = document.createElement('span');
        label.className = 'game-complete-recap-label';
        label.textContent = `Hand ${i + 1}:`;
        row.appendChild(label);
        const summary = document.createElement('span');
        summary.className = 'game-complete-recap-summary';
        if (h.type === 'Hu') {
          const nick = this.nickForSeat(h.winner) ?? `Seat ${h.winner}`;
          const deltas = [...(h.score ?? [])].sort((a, b) => a.seat - b.seat);
          const deltaText = deltas
            .map(d => (d.delta > 0 ? `+${d.delta}` : String(d.delta)))
            .join(' / ');
          summary.textContent = `${nick} won  (${deltaText})`;
          summary.classList.add('game-complete-recap-hu');
        } else if (h.type === 'ZhaHu') {
          const nick = this.nickForSeat(h.winner) ?? `Seat ${h.winner}`;
          summary.textContent = `${nick} false-Hu`;
          summary.classList.add('game-complete-recap-zha');
        } else {
          summary.textContent = 'Washout 流局';
          summary.classList.add('game-complete-recap-draw');
        }
        row.appendChild(summary);
        recap.appendChild(row);
      });
    }
  }

  // Phase J Wave 2 — Build the per-seat point total map.  Priority:
  // server-pushed `totalScores` → sum of client-side history → zero.
  // Returns a Map<seat, delta> with all four seats represented (even
  // when their delta is zero) so the modal's table is always 4 rows.
  private computeFinalScores(payload: GameCompleteEntry): Map<number, number> {
    const totals = new Map<number, number>();
    for (let i = 0; i < 4; i++) totals.set(i, 0);

    const wireTotals = payload.totalScores ?? payload.TotalScores ?? null;
    if (wireTotals !== null) {
      for (const [k, v] of Object.entries(wireTotals)) {
        const seat = parseInt(k, 10);
        if (!isNaN(seat) && totals.has(seat) && typeof v === 'number') {
          totals.set(seat, v);
        }
      }
      return totals;
    }

    // Fallback: derive from observed hand-by-hand deltas.
    for (const h of this.handHistory) {
      for (const { seat, delta } of h.score ?? []) {
        if (totals.has(seat)) totals.set(seat, (totals.get(seat) ?? 0) + delta);
      }
    }
    return totals;
  }

  // Phase J Wave 2 — Restart the match using the current settings-drawer
  // values.  Reads the drawer (or falls back to URL/localStorage), bakes
  // the params into the page URL, and reloads so the runtime spins up a
  // fresh game with the chosen settings.  Preserves the active gameId so
  // the user stays in the same WS routing pool unless they manually
  // changed it.
  private startNewGameFromSettings(): void {
    const settings = readSettingsState();
    const params = new URLSearchParams(window.location.search);
    // Preserve any existing variant/seat/seed; replace the W2-controlled
    // params (botDifficulty, handCount, dealMode) with the latest drawer
    // values.  botCount stays at whatever the lobby last picked (default 3).
    params.set('botDifficulty', settings.botStrength);
    params.set('handCount', String(settings.handCount));
    params.set('dealMode', settings.autoDeal ? 'auto' : 'manual');
    if (!params.has('variant')) params.set('variant', 'changsha');
    if (!params.has('botCount')) params.set('botCount', '3');
    const url = window.location.pathname + '?' + params.toString();
    window.location.replace(url);
  }

  // ---------------------------------------------------------------------
  // Phase J Wave 2 — Settings drawer (gear icon top-right).
  //
  // A tiny per-game settings panel exposing the three knobs the directive
  // calls out:
  //   • Bot Strength (Easy / Medium / Hard, default Hard)
  //   • Hand Count   (1 / 4 / 8 / 16, default 4)
  //   • Auto-Deal    (checkbox, default off — runtime auto-starts hands)
  //
  // Settings are persisted via gameId-keyed localStorage so each WS
  // routing pool keeps its own.  Apply rewrites the URL with the new
  // params and reloads, which is the same pattern the lobby uses.
  // ---------------------------------------------------------------------
  private setupSettingsDrawer(): void {
    // Re-hydrate from localStorage on boot (gameId-keyed; falls back to
    // a global default key when gameId isn't on the URL yet).
    const state = readSettingsState();
    this.elements.settingsBotStrength.value = state.botStrength;
    this.elements.settingsHandCount.value = String(state.handCount);
    this.elements.settingsAutoDeal.checked = state.autoDeal;
    this.elements.settingsSound.checked = state.sound;
    Sound.setMuted(!state.sound);

    this.elements.settingsToggle.onclick = (e: MouseEvent) => {
      e.stopPropagation();
      const open = this.elements.settingsDrawer.classList.contains('settings-open');
      if (open) {
        this.closeSettingsDrawer();
      } else {
        this.openSettingsDrawer();
      }
    };

    this.elements.settingsClose.onclick = () => this.closeSettingsDrawer();

    const onChange = (): void => this.persistSettings();
    this.elements.settingsBotStrength.addEventListener('change', onChange);
    this.elements.settingsHandCount.addEventListener('change', onChange);
    this.elements.settingsAutoDeal.addEventListener('change', onChange);
    this.elements.settingsSound.addEventListener('change', () => {
      Sound.setMuted(!this.elements.settingsSound.checked);
      this.persistSettings();
    });

    this.elements.settingsApply.onclick = () => {
      this.persistSettings();
      this.startNewGameFromSettings();
    };

    // Click outside closes the drawer.  We bind on document mousedown so
    // any click that isn't inside the drawer or on the toggle button
    // collapses it.
    document.addEventListener('mousedown', (e: MouseEvent) => {
      if (!this.elements.settingsDrawer.classList.contains('settings-open')) return;
      const target = e.target as Node | null;
      if (target && (
        this.elements.settingsDrawer.contains(target)
        || this.elements.settingsToggle.contains(target)
      )) return;
      this.closeSettingsDrawer();
    });
  }

  private openSettingsDrawer(): void {
    this.elements.settingsDrawer.classList.add('settings-open');
    this.elements.settingsDrawer.setAttribute('aria-hidden', 'false');
  }

  private closeSettingsDrawer(): void {
    this.elements.settingsDrawer.classList.remove('settings-open');
    this.elements.settingsDrawer.setAttribute('aria-hidden', 'true');
  }

  private persistSettings(): void {
    const state: SettingsState = {
      botStrength: this.elements.settingsBotStrength.value as BotStrength,
      handCount: parseInt(this.elements.settingsHandCount.value, 10) as SettingsHandCount,
      autoDeal: this.elements.settingsAutoDeal.checked,
      sound: this.elements.settingsSound.checked,
    };
    writeSettingsState(state);
    // Flash a "Saved ✓" pill so the user sees the persist landed.
    const note = this.elements.settingsSavedNote;
    note.hidden = false;
    note.style.display = 'inline';
    window.setTimeout(() => {
      hideEl(note);
    }, 1500);
  }

  // ---------------------------------------------------------------------
  // Phase J Wave 3 — Sound-effect wiring.
  //
  // Hooks the synth sound manager (`sound.ts`) into the game-state
  // collections so each gameplay event fires the appropriate SFX:
  //
  //   • discard  — `sound[ * ].type === DISCARD` push (Bishop's ephemeral
  //                 sound collection; emitted on every discard).
  //   • claim    — `claim[ seat ].action === 'claim'` (local or echoed
  //                 remote claim acceptance from the claim collection).
  //   • draw     — `things[ tile ].slot` enters `hand.*` (throttled to
  //                 one SFX per event batch — initial deals shouldn't
  //                 fire 13 clacks).
  //   • win      — fired inline from onResultUpdate (Hu).
  //   • washout  — fired inline from onResultUpdate (Draw).
  //   • gameComplete — fired inline from onGameCompleteUpdate.
  //
  // Browser-autoplay unlock: AudioContext is created on the first user
  // gesture.  We bind a one-shot click listener to the document so the
  // first interaction anywhere unlocks audio; subsequent calls are no-op.
  // ---------------------------------------------------------------------
  private setupSoundEffects(): void {
    // One-shot unlock on the first user gesture.  Use `{ once: true }`
    // semantics manually so we still fire on touch (non-click) starts.
    const unlock = (): void => {
      Sound.unlock();
      document.removeEventListener('click', unlock);
      document.removeEventListener('touchstart', unlock);
      document.removeEventListener('keydown', unlock);
    };
    document.addEventListener('click', unlock, { passive: true });
    document.addEventListener('touchstart', unlock, { passive: true });
    document.addEventListener('keydown', unlock);

    // Discard SFX — Bishop's `sound` collection ships SoundType.DISCARD
    // on every discard.  We piggy-back on the existing fanout instead of
    // adding a parallel `things`-watcher (which would also need
    // discard-side filtering).
    this.client.sound.on('update', this.onSoundForSfx.bind(this));

    // Claim SFX — fires when our local `sendClaim` action round-trips
    // through the claim collection OR another seat's claim acceptance
    // is broadcast.  We detect `{action: 'claim', type: '...'}` shape
    // (vs the open-window `{available: [...]}` shape).
    this.client.claim.on('update', this.onClaimForSfx.bind(this));

    // Draw SFX — detect tile transitions into `hand.*` slots.  Throttled
    // across batches so an initial deal fires one clack, not 13.
    this.client.things.on('update', this.onThingsForSfx.bind(this));
  }

  private onSoundForSfx(entries: Array<[number, SoundInfo | null]>): void {
    for (const [, info] of entries) {
      if (!info) continue;
      if (info.type === SoundType.DISCARD) {
        Sound.play('discard');
        return; // One play per batch — avoids overlapping clacks.
      }
    }
  }

  private onClaimForSfx(entries: Array<[string, ClaimWindowEntry | null]>): void {
    for (const [, value] of entries) {
      if (!value) continue;
      const v = value as unknown as { action?: string; type?: string; available?: unknown };
      if (v.action === 'claim' && v.type) {
        Sound.play('claim');
        return; // Single chime per accepted claim batch.
      }
    }
  }

  private onThingsForSfx(entries: Array<[number, ThingInfo | null]>): void {
    // Find at least one tile transition into hand.* — one SFX per batch.
    // The 200 ms throttle prevents back-to-back draw rounds from
    // sounding like a machine gun (1 clack per round is plenty).
    const now = Date.now();
    if (now - this.lastDrawSoundMs < 200) return;
    let didDraw = false;
    for (const [, info] of entries) {
      if (!info) continue;
      const slot = info.slotName ?? '';
      if (slot.startsWith('hand.')) {
        didDraw = true;
        break;
      }
    }
    if (didDraw) {
      this.lastDrawSoundMs = now;
      Sound.play('draw');
    }
  }

  // ---------------------------------------------------------------------
  // Phase J Wave 3 — Replay viewer wiring.
  //
  // The Replay class owns its own state capture (subscribes to things /
  // result / match) so we only need to start() it on boot.  The viewer
  // is opened from the end-of-game modal's "View Replay" button (see
  // setupGameCompleteModal above) — at which point we hand it the
  // gameComplete payload's `handHistory` (when present) so the dropdown
  // labels match the runtime's view of "Hand 1 / 2 / ...".
  // ---------------------------------------------------------------------
  private setupReplay(): void {
    this.replay.start();
  }

  // Phase J Wave 4 — Mobile move-log drawer.  On tablet/phone widths
  // (≤ 1024 px in style.css) the move-log slides off the right edge of
  // the viewport; this hamburger button toggles a `body.move-log-open`
  // class that brings it back into view.  Clicking outside the open
  // drawer dismisses it so the user can return focus to the table
  // without aiming at the small close icon.
  private setupMobileDrawer(): void {
    const toggle = document.getElementById('move-log-toggle');
    const moveLog = document.getElementById('move-log');
    if (toggle === null || moveLog === null) return;

    const isOpen = (): boolean =>
      document.body.classList.contains('move-log-open');

    const setOpen = (open: boolean): void => {
      document.body.classList.toggle('move-log-open', open);
      toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
    };

    toggle.addEventListener('click', (event: MouseEvent) => {
      event.stopPropagation();
      setOpen(!isOpen());
    });

    // Outside-click dismissal — wired at the document level so any tap
    // outside the drawer + toggle button closes it.
    document.addEventListener('click', (event: MouseEvent) => {
      if (!isOpen()) return;
      const target = event.target as Node | null;
      if (target === null) return;
      if (moveLog.contains(target)) return;
      if (toggle.contains(target)) return;
      setOpen(false);
    });
  }
}

// ---------------------------------------------------------------------
// Phase J Wave 2 — Settings drawer state + localStorage helpers.
//
// Settings live in localStorage under a gameId-keyed key so each WS
// routing pool keeps its own settings.  When the page URL carries no
// gameId yet (boot-time, before the user clicks Connect), we fall back
// to a global default key so the drawer still pre-populates.
// ---------------------------------------------------------------------

type BotStrength = 'Easy' | 'Medium' | 'Hard' | 'Master';
type SettingsHandCount = 1 | 4 | 8 | 16;

interface SettingsState {
  botStrength: BotStrength;
  handCount: SettingsHandCount;
  autoDeal: boolean;
  // Phase J Wave 3 — SFX toggle.  Default: ON.  Persisted in the same
  // gameId-keyed payload as the other settings drawer knobs.
  sound: boolean;
}

const SETTINGS_DEFAULT: SettingsState = {
  botStrength: 'Hard',
  handCount: 4,
  autoDeal: false,
  sound: true,
};

const SETTINGS_LS_PREFIX = 'autotable.phaseJ.v1.settings.';
const SETTINGS_LS_GLOBAL = SETTINGS_LS_PREFIX + 'default';

function settingsLocalStorageKey(): string {
  try {
    const q = new URLSearchParams(window.location.search);
    const gameId = q.get('gameId');
    if (gameId !== null && gameId !== '') {
      return SETTINGS_LS_PREFIX + gameId;
    }
  } catch {
    /* ignore — fall through to global key */
  }
  return SETTINGS_LS_GLOBAL;
}

function readSettingsState(): SettingsState {
  // Priority: URL params > gameId-keyed localStorage > global localStorage
  // > SETTINGS_DEFAULT.  URL wins so a deep-linked match starts with the
  // chosen settings even before localStorage gets touched.
  const out: SettingsState = { ...SETTINGS_DEFAULT };

  const tryLoad = (key: string): Partial<SettingsState> => {
    try {
      const raw = window.localStorage.getItem(key);
      if (raw === null) return {};
      const j = JSON.parse(raw) as Record<string, unknown>;
      const partial: Partial<SettingsState> = {};
      if (j.botStrength === 'Easy' || j.botStrength === 'Medium' || j.botStrength === 'Hard' || j.botStrength === 'Master') {
        partial.botStrength = j.botStrength;
      }
      if (typeof j.handCount === 'number'
          && (j.handCount === 1 || j.handCount === 4
              || j.handCount === 8 || j.handCount === 16)) {
        partial.handCount = j.handCount;
      }
      if (typeof j.autoDeal === 'boolean') {
        partial.autoDeal = j.autoDeal;
      }
      if (typeof j.sound === 'boolean') {
        partial.sound = j.sound;
      }
      return partial;
    } catch {
      return {};
    }
  };

  // Global defaults first (lowest priority).
  Object.assign(out, tryLoad(SETTINGS_LS_GLOBAL));
  // gameId-keyed override.
  const keyed = settingsLocalStorageKey();
  if (keyed !== SETTINGS_LS_GLOBAL) {
    Object.assign(out, tryLoad(keyed));
  }

  // Finally, let URL params override (highest priority — deep-link wins).
  try {
    const q = new URLSearchParams(window.location.search);
    const bd = q.get('botDifficulty');
    if (bd === 'Easy' || bd === 'Medium' || bd === 'Hard' || bd === 'Master') out.botStrength = bd;
    const hcRaw = q.get('handCount');
    if (hcRaw !== null) {
      const n = parseInt(hcRaw, 10);
      if (n === 1 || n === 4 || n === 8 || n === 16) out.handCount = n;
    }
    const dm = q.get('dealMode');
    if (dm === 'auto') out.autoDeal = true;
    else if (dm === 'manual') out.autoDeal = false;
    // Phase J Wave 3 — `?sound=on|off` URL override for SFX.  Deep links
    // can force sound off (e.g. for screencasts) without nuking the
    // user's localStorage preference.
    const sn = q.get('sound');
    if (sn === 'on' || sn === 'true' || sn === '1') out.sound = true;
    else if (sn === 'off' || sn === 'false' || sn === '0') out.sound = false;
  } catch {
    /* ignore */
  }

  return out;
}

function writeSettingsState(state: SettingsState): void {
  const payload = JSON.stringify(state);
  try {
    // Write to BOTH the global key (so a fresh tab without ?gameId= picks
    // these up) and the gameId-keyed key (so the active game's overrides
    // are isolated from other games).
    window.localStorage.setItem(SETTINGS_LS_GLOBAL, payload);
    const keyed = settingsLocalStorageKey();
    if (keyed !== SETTINGS_LS_GLOBAL) {
      window.localStorage.setItem(keyed, payload);
    }
  } catch {
    /* ignore — localStorage may be disabled */
  }
}
