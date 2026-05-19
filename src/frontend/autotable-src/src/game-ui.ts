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
} from './types';

// Phase D — claim window expiry uses the deadline (epoch ms) that the server
// pushes; CLAIM_TICK_MS controls how often the countdown text re-renders.
const CLAIM_TICK_MS = 100;

// Phase D — dice HUD lifetime (after first-deal roll, before fade-out).
const DICE_HUD_LIFETIME_MS = 3000;

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

export class GameUi {
  private client: Client;
  private world: World;

  elements: {
    deal: HTMLButtonElement;
    toggleDealer: HTMLButtonElement;
    takeSeat: Array<HTMLButtonElement>;
    kick: Array<HTMLButtonElement>;
    leaveSeat: HTMLButtonElement;
    toggleSetup: HTMLButtonElement;
    dealType: HTMLSelectElement;
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
  }

  // Phase D — claim window state.
  private activeClaim: ClaimWindowEntry | null = null;
  private claimTickHandle: number | null = null;
  private diceHudHandle: number | null = null;

  constructor(client: Client, world: World) {
    this.client = client;
    this.world = world;

    this.elements = {
      deal: document.getElementById('deal') as HTMLButtonElement,
      toggleDealer: document.getElementById('toggle-dealer') as HTMLButtonElement,
      takeSeat: [],
      kick: [],
      leaveSeat: document.getElementById('leave-seat') as HTMLButtonElement,
      toggleSetup: document.getElementById('toggle-setup') as HTMLButtonElement,
      dealType: document.getElementById('deal-type') as HTMLSelectElement,
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
    };
    for (let i = 0; i < 4; i++) {
      this.elements.takeSeat[i] = document.querySelector(
        `.seat-button-${i} .take-seat`) as HTMLButtonElement;

      this.elements.kick[i] = document.querySelector(
        `.seat-button-${i} .kick`) as HTMLButtonElement;
    }

    this.setupEvents();
    this.setupDealButton();
    this.setupModal();
    this.setupClaimButtons();
    this.setupResultModal();
    this.setupDiceHud();
    this.setupBotBanner();
  }

  private setupEvents(): void {
    this.elements.toggleDealer.onclick = () => this.world.toggleDealer();

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

      this.world.deal(dealType);
      this.hideSetup();
    });
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
    title.textContent = `Bots filled ${bots.length === 1 ? 'seat' : 'seats'} `
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

}
