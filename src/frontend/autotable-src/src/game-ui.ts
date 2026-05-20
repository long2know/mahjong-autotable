import $ from 'jquery';
import { Client } from "./client";
import { World } from "./world";
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
  }

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
    if (this.client.seat === null) {
      (document.querySelector('.seat-buttons')! as HTMLElement).style.display = 'block';
      for (let i = 0; i < 4; i++) {
        const playerId = this.client.seatPlayers[i];
        if (playerId !== null) {
          this.elements.takeSeat[i].style.display = 'none';
          this.elements.kick[i].style.display = '';

          const nick = this.client.nicks.get(playerId) || 'Player';
          const textElement = this.elements.kick[i].querySelector('.btn-progress-text')!;
          textElement.textContent = nick;
        } else {
          this.elements.takeSeat[i].style.display = '';
          this.elements.kick[i].style.display = 'none';
        }
      }
      for (const button of toDisable) {
        button.disabled = true;
      }
    } else {
      (document.querySelector('.seat-buttons')! as HTMLElement).style.display = 'none';
      for (const button of toDisable) {
        button.disabled = false;
      }
    }
    // Phase D — re-evaluate claim button states (selfSeat may have changed)
    // and refresh the bot banner whenever seat ↔ nick mapping shifts.
    this.refreshClaimButtons();
    this.refreshBotBanner();
  }

  private setupDealButton(): void {
    const buttonElement = document.getElementById('deal')! as HTMLButtonElement;

    this.setupProgressButton(buttonElement, 600, () => {
      const dealType = this.elements.dealType.value as DealType;

      // Phase F — fold the latest picker selections into Conditions so the
      // server (or local-relay path) sees the right variant + fives + points
      // + deal-mode on this deal.  defaultsFor() gives us safe per-variant
      // baselines; the picker values then override.
      const overrides = this.collectConditionOverrides();
      this.world.deal(dealType, overrides);
      this.hideSetup();
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
      this.elements.claimCountdown.style.display = 'none';
      return;
    }

    for (const t of allTypes) {
      this.elements.claim[t].disabled = !claim.available.includes(t);
    }
    this.elements.claim.Pass.disabled = false;
    this.elements.claimCountdown.style.display = '';
    this.tickClaimCountdown();
    this.startClaimTimer();
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
    const remainingMs = claim.deadline - Date.now();
    if (remainingMs <= 0) {
      // Auto-pass on expiry.
      this.elements.claimCountdownValue.textContent = '0.0';
      this.sendClaim({ action: 'pass', type: null });
      return;
    }
    this.elements.claimCountdownValue.textContent = (remainingMs / 1000).toFixed(1);
  }

  private sendClaim(action: ClaimAction): void {
    const selfSeat = this.client.seat;
    if (selfSeat === null) return;
    if (!this.activeClaim) return;
    if (action.action === 'claim'
        && !this.activeClaim.available.includes(action.type)) {
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
      this.renderResult(value);
      // @ts-ignore
      $('#result-modal').modal('show');
    }
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

    // Score deltas table.
    const tbody = this.elements.resultScoreBody;
    tbody.innerHTML = '';
    const ordered = [...result.score].sort((a, b) => a.seat - b.seat);
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
    this.elements.diceHud.style.display = 'block';
    if (this.diceHudHandle !== null) {
      window.clearTimeout(this.diceHudHandle);
    }
    this.diceHudHandle = window.setTimeout(() => {
      this.elements.diceHud.style.display = 'none';
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
    // Only show when the local player is seated — pre-seat, no banner.
    if (this.client.seat === null) {
      banner.style.display = 'none';
      return;
    }
    const bots: Array<{ seat: number; nick: string }> = [];
    for (let i = 0; i < 4; i++) {
      if (i === this.client.seat) continue;
      const playerId = this.client.seatPlayers[i];
      if (playerId === null) continue;
      const nick = this.client.nicks.get(playerId);
      if (isBotNick(nick)) {
        bots.push({ seat: i, nick: nick! });
      }
    }
    if (bots.length === 0) {
      banner.style.display = 'none';
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
    banner.style.display = 'block';
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
    // Only the key=0 singleton is the authoritative snapshot.  The string
    // keys ('rollDice' / 'take') are our outbound commands and we don't
    // re-render off them.
    for (const [key,] of entries) {
      if (key !== 0) continue;
      const pickup = this.client.pickup.get(0) ?? null;
      this.renderPickupHud(pickup);
      this.renderRollDiceButton(pickup);
      this.renderBreakMarker(pickup);
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
    hud.style.display = 'block';
  }

  private renderRollDiceButton(pickup: PickupEntry | null): void {
    const btn = this.elements.rollDice;
    if (!pickup) {
      btn.style.display = 'none';
      return;
    }
    // The runtime can spell the phase as either 'RollingDice' or 'rollDice'
    // depending on JSON-serializer settings; accept both.
    const phaseStr = String(pickup.phase ?? '').toLowerCase();
    const isRollPhase = phaseStr === 'rollingdice' || phaseStr === 'rolldice';
    const selfSeat = this.client.seat;
    const isMine = selfSeat !== null && pickup.seatIndex === selfSeat;
    btn.style.display = (isRollPhase && isMine) ? 'flex' : 'none';
  }

  private renderBreakMarker(pickup: PickupEntry | null): void {
    const marker = this.elements.breakMarker;
    if (!pickup || pickup.breakPoint === undefined || pickup.breakPoint === null) {
      marker.style.display = 'none';
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
    marker.style.display = 'block';
  }

}
