import type { Entry } from '../../server/protocol';
import type { Client } from '../client';
import type { HandTile } from '../hand-sort';
import { t } from '../i18n';
import { resolveMobileLayout, type MobileLayoutMode } from '../mobile-layout';
import type { ReadonlyPublicContext } from '../public-board-model';
import { showToast } from '../toast';
import { GameType, ThingType, type ThingInfo } from '../types';
import type { World } from '../world';
import type { RuleActionControls } from './rule-action-controls';
import './mobile-hand.css';

export interface MobileHandLayout {
  active: boolean;
  insetPx: number;
}

interface DiscardAttempt {
  owner: string;
  seat: number;
  tileId: number;
  needsRecovery: boolean;
}

interface PublicTile {
  id: number;
  face: number;
  seat: number;
}

const CONFIRMATION_TIMEOUT_MS = 15_000;
const MIN_FOOTER_INSET_PX = 208;

function tileLabel(face: number): string {
  return t('hand.tile', { rank: face % 9 + 1, suit: t(`hand.suit_${Math.floor(face / 9)}`) });
}

function physicalId(id: unknown): id is number {
  return typeof id === 'number' && Number.isInteger(id) && id >= 0 && id < 108;
}

export class MobileHandController {
  private root: HTMLElement | null = null;
  private tiles: HTMLElement | null = null;
  private actionsArea: HTMLElement | null = null;
  private context: HTMLElement | null = null;
  private status: HTMLElement | null = null;
  private discard: HTMLButtonElement | null = null;
  private recovery: HTMLButtonElement | null = null;
  private standaloneRecovery: HTMLElement | null = null;
  private mode: MobileLayoutMode = null;
  private selected: { id: number; context: string } | null = null;
  private pending: DiscardAttempt | null = null;
  private emittingDiscard = false;
  private pendingTimer: number | null = null;
  private awaitingSnapshot = false;
  private failure = '';
  private handSignature = '';
  private viewportSignature = '';
  private footerInset = 0;
  private publicRoom: string | null = null;
  private publicSlots = new Map<number, string>();
  private lastDiscard: PublicTile | null = null;
  private moved = new Map<HTMLElement, Comment>();

  constructor(
    private readonly client: Client,
    private readonly world: World,
    private readonly ruleActions: RuleActionControls,
  ) {
    // The same flight locks the restored canvas after resize/off/reconnect.
    this.world.setMobileDiscardGuard(() => this.pending === null || this.emittingDiscard
      || this.client.connectionMode !== 'changsha' || this.world.conditions.gameType !== GameType.CHANGSHA);
    this.client.on('update', (entries, full) => this.observe(entries, full));
    const invalidate = (): void => {
      this.awaitingSnapshot = true;
      this.selected = null;
      if (this.pending !== null) this.pending.needsRecovery = true;
      this.publicRoom = null;
      this.publicSlots.clear();
      this.lastDiscard = null;
      this.deactivate();
    };
    this.client.on('joining', invalidate);
    this.client.on('connect', invalidate);
    this.client.on('disconnect', invalidate);
    if (this.client.connected() && this.client.serverSnapshotGameId !== null) this.observe([], true);
  }

  refresh(mode: MobileLayoutMode): MobileHandLayout {
    this.mode = mode;
    const owner = this.owner();
    if (owner !== null && this.pending !== null) {
      if (this.pending.owner !== owner) {
        this.clearPending();
        this.failure = '';
      } else {
        const current = this.world.ownHandPresentation();
        if (this.validHand(current.tiles)) this.confirmPending(current.tiles);
      }
    }
    const resolved = resolveMobileLayout(mode, {
      width: window.innerWidth, height: window.innerHeight,
      changsha: this.client.connectionMode === 'changsha'
        && this.world.conditions.gameType === GameType.CHANGSHA && this.client.connected(),
    });
    if ((resolved !== 'hybrid' && resolved !== 'readable') || owner === null || this.awaitingSnapshot
      || document.body.classList.contains('lobby-active')) {
      this.deactivate();
      return { active: false, insetPx: 0 };
    }
    const presentation = this.world.ownHandPresentation();
    if (!this.validHand(presentation.tiles)) {
      this.deactivate();
      return { active: false, insetPx: 0 };
    }
    const context = this.selectionContext(owner, presentation.tiles);
    if (this.selected !== null && (this.selected.context !== context || !this.ruleActions.canDiscard)) {
      this.selected = null;
    }
    this.confirmPending(presentation.tiles);
    this.mount();
    this.root!.dataset.mobileLayout = resolved;
    this.renderHand(presentation.tiles, presentation.drawn);
    this.moveHud();
    this.renderContext();
    this.renderControls();
    const root = this.root!;
    if (!this.visibleHand()) {
      this.deactivate();
      return { active: false, insetPx: 0 };
    }
    const viewport = `${window.innerWidth}/${window.innerHeight}`;
    if (viewport !== this.viewportSignature) {
      const bounds = root.getBoundingClientRect();
      // The dock is fixed to the full CSS viewport, not the camera's fitted area.
      // Cache only on viewport changes so status/timer text cannot pump the fit.
      this.footerInset = Math.ceil(Math.max(MIN_FOOTER_INSET_PX, bounds.height)
        + Math.max(0, window.innerHeight - bounds.bottom));
      this.viewportSignature = viewport;
    }
    this.world.setOwnHandDomActive(true);
    return { active: true, insetPx: this.footerInset };
  }

  private owner(): string | null {
    const seat = this.client.seat;
    const room = new URLSearchParams(window.location.search).get('gameId');
    if (!this.client.connected() || this.client.connectionMode !== 'changsha'
      || this.world.conditions.gameType !== GameType.CHANGSHA
      || room === null || room !== this.client.lastGameId || room !== this.client.serverSnapshotGameId
      || document.body.classList.contains('spectating') || seat === null
      || !Number.isInteger(seat) || seat < 0 || seat > 3 || this.world.seat !== seat
      || this.client.seatPlayers[seat] !== this.client.playerId()) return null;
    return JSON.stringify([room, this.client.playerId(), seat]);
  }

  private validHand(hand: readonly HandTile[]): boolean {
    if (hand.length > 14 || new Set(hand.map(tile => tile.id)).size !== hand.length) return false;
    const ownSlot = new RegExp(`^hand\\.\\d+@${this.client.seat}$`);
    const authoritative = [...this.client.things.entries()].filter(([, entry]) => ownSlot.test(entry.slotName));
    if (authoritative.length !== hand.length || authoritative.some(([id]) =>
      !physicalId(id) || !hand.some(tile => tile.id === id))) return false;
    const current = [...this.world.things.values()].filter(thing => this.world.ownHandThing(thing.index) === thing);
    return current.length === hand.length && hand.every(tile => {
      const thing = this.world.ownHandThing(tile.id);
      return physicalId(tile.id) && Number.isInteger(tile.face) && tile.face >= 0 && tile.face < 27
        && thing !== null && thing.typeIndex === tile.face && thing.slot.name === tile.slotName;
    });
  }

  private selectionContext(owner: string, hand: readonly HandTile[]): string {
    const own = [...this.client.ownTurn.entries()].find(([seat]) => String(seat) === String(this.client.seat))?.[1];
    const turn = this.client.turn.get('current');
    return JSON.stringify([
      owner, own?.gameId, own?.stateVersion, turn?.phase, turn?.activeSeat, turn?.awaitingDiscard,
      hand.map(tile => [tile.id, tile.face, tile.slotName]).sort((a, b) => Number(a[0]) - Number(b[0])),
    ]);
  }

  private mount(): void {
    this.removeStandaloneRecovery();
    if (this.root !== null) return;
    const root = document.createElement('section');
    root.id = 'mobile-hand-dock';
    root.dataset.mobileLayout = 'hybrid';
    root.setAttribute('role', 'region');
    root.setAttribute('aria-label', 'Your hand — select a tile, then confirm discard');
    const context = document.createElement('div');
    context.className = 'mobile-hand-context';
    const status = this.createStatus();
    const tiles = document.createElement('div');
    tiles.className = 'mobile-hand-tiles';
    tiles.setAttribute('aria-label', 'Your concealed tiles');
    const actions = document.createElement('div');
    actions.className = 'mobile-hand-actions';
    const discard = document.createElement('button');
    discard.type = 'button';
    discard.className = 'mobile-hand-discard';
    discard.addEventListener('click', () => this.confirmDiscard());
    const recovery = this.createRecovery();
    actions.append(discard, recovery);
    root.append(context, status, tiles, actions);
    const activationKey = (event: KeyboardEvent): void => {
      const target = event.target as HTMLElement | null;
      if ((event.key === ' ' || event.key === 'Enter')
        && (target?.classList.contains('mobile-hand-tile')
          || target?.classList.contains('mobile-hand-discard')
          || target?.classList.contains('mobile-hand-recovery'))) event.stopPropagation();
    };
    root.addEventListener('keydown', event => {
      if (event.key === 'Escape' && this.pending === null && this.selected !== null) {
        event.preventDefault();
        event.stopPropagation();
        this.selected = null;
        this.failure = '';
        this.refresh(this.mode);
      }
      activationKey(event);
    });
    root.addEventListener('keyup', activationKey);
    document.body.appendChild(root);
    this.root = root;
    this.tiles = tiles;
    this.context = context;
    this.status = status;
    this.actionsArea = actions;
    this.discard = discard;
    this.recovery = recovery;
  }

  private createStatus(): HTMLElement {
    const status = document.createElement('p');
    status.className = 'mobile-hand-status';
    status.setAttribute('role', 'status');
    status.setAttribute('aria-live', 'polite');
    return status;
  }

  private createRecovery(): HTMLButtonElement {
    const recovery = document.createElement('button');
    recovery.type = 'button';
    recovery.className = 'mobile-hand-recovery';
    recovery.textContent = 'Refresh table';
    recovery.setAttribute('aria-label', 'Refresh table without resending the discard');
    recovery.addEventListener('click', () => {
      const owner = this.owner();
      if (owner === null || this.awaitingSnapshot || !document.body.contains(recovery)) return;
      if (this.pending !== null ? this.pending.owner === owner : this.failure !== '') window.location.reload();
    });
    const activationKey = (event: KeyboardEvent): void => {
      if (event.key === ' ' || event.key === 'Enter') event.stopPropagation();
    };
    recovery.addEventListener('keydown', activationKey);
    recovery.addEventListener('keyup', activationKey);
    return recovery;
  }

  private pendingMessage(): string {
    if (this.failure !== '') return `${this.failure}. Refresh table; this discard will not be resent.`;
    return this.pending?.needsRecovery
      ? 'Confirmation unavailable. Refresh status; this discard will not be resent.'
      : 'Discard sent. Waiting for the server; tiles stay in place until confirmed.';
  }

  private removeStandaloneRecovery(): void {
    this.standaloneRecovery?.remove();
    this.standaloneRecovery = null;
  }

  private renderStandaloneRecovery(): void {
    if (this.pending === null || this.awaitingSnapshot || this.owner() !== this.pending.owner) {
      this.removeStandaloneRecovery();
      return;
    }
    if (this.standaloneRecovery === null) {
      const root = document.createElement('section');
      root.id = 'mobile-discard-recovery';
      root.setAttribute('role', 'region');
      root.setAttribute('aria-label', 'Pending discard recovery');
      root.append(this.createStatus(), this.createRecovery());
      document.body.appendChild(root);
      this.standaloneRecovery = root;
    }
    const status = this.standaloneRecovery.querySelector<HTMLElement>('.mobile-hand-status')!;
    const message = this.pendingMessage();
    if (status.textContent !== message) status.textContent = message;
    const root = this.standaloneRecovery;
    root.style.top = '';
    root.style.maxHeight = '';
    const bounds = root.getBoundingClientRect();
    let top = bounds.top;
    for (const id of ['own-turn-actions', 'rule-action-recovery']) {
      const panel = document.getElementById(id);
      if (panel === null || panel.hidden) continue;
      const style = window.getComputedStyle(panel);
      if (style.display === 'none' || style.visibility === 'hidden') continue;
      const rect = panel.getBoundingClientRect();
      if (rect.width > 0 && rect.height > 0 && rect.left < bounds.right && rect.right > bounds.left
        && rect.top < top + bounds.height && rect.bottom > top) top = rect.bottom + 8;
    }
    if (top > bounds.top) {
      root.style.top = `${top}px`;
      root.style.maxHeight = `calc(100dvh - ${top + 16}px - env(safe-area-inset-bottom, 0px))`;
    }
  }

  private face(face: number): HTMLElement {
    const image = document.createElement('span');
    image.className = 'mobile-hand-tile-face';
    image.setAttribute('aria-hidden', 'true');
    image.style.backgroundPosition = `${(face % 8) * 100 / 7}% ${Math.floor(face / 8) * 100 / 5.4}%`;
    return image;
  }

  private renderHand(hand: readonly HandTile[], drawn: number | null): void {
    const signature = JSON.stringify([hand.map(tile => [tile.id, tile.face]), drawn]);
    if (signature !== this.handSignature) {
      const ordered = [...hand.filter(tile => tile.id !== drawn), ...hand.filter(tile => tile.id === drawn)];
      this.tiles!.replaceChildren();
      for (const tile of ordered) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'mobile-hand-tile';
        button.dataset.tileId = String(tile.id);
        button.dataset.face = String(tile.face);
        button.dataset.drawn = String(tile.id === drawn);
        if (tile.id === drawn) button.classList.add('mobile-hand-drawn');
        button.setAttribute('aria-label', `${tileLabel(tile.face)}${tile.id === drawn ? ', drawn tile' : ''}; tap to select, tap again to cancel`);
        button.appendChild(this.face(tile.face));
        button.addEventListener('click', () => this.select(tile.id));
        this.tiles!.appendChild(button);
      }
      this.handSignature = signature;
    }
    for (const button of this.tiles!.querySelectorAll<HTMLButtonElement>('.mobile-hand-tile')) {
      const id = Number(button.dataset.tileId);
      button.setAttribute('aria-pressed', String(this.selected?.id === id));
      button.dataset.pending = String(this.pending?.tileId === id);
      button.disabled = !this.ruleActions.canDiscard || this.pending !== null;
    }
    this.tiles!.setAttribute('aria-label', hand.length === 0 ? 'No concealed tiles dealt yet'
      : `${hand.length} concealed tiles; seven tiles per row`);
  }

  private select(id: number): void {
    const owner = this.owner();
    if (owner === null || this.pending !== null || !this.ruleActions.canDiscard
      || this.world.ownHandThing(id) === null || !this.refresh(this.mode).active) return;
    const hand = this.world.ownHandPresentation().tiles;
    this.selected = this.selected?.id === id ? null : { id, context: this.selectionContext(owner, hand) };
    this.failure = '';
    this.refresh(this.mode);
  }

  private confirmDiscard(): void {
    const selected = this.selected;
    if (selected === null || this.pending !== null || !this.refresh(this.mode).active
      || this.selected !== selected || !this.ruleActions.canDiscard) return;
    const owner = this.owner();
    const hand = this.world.ownHandPresentation().tiles;
    const thing = this.world.ownHandThing(selected.id);
    if (owner === null || thing === null || this.selectionContext(owner, hand) !== selected.context
      || !hand.some(tile => tile.id === selected.id)) {
      this.selected = null;
      this.failure = 'That tile is no longer available. Select a current tile.';
      this.refresh(this.mode);
      return;
    }
    this.pending = { owner, seat: this.client.seat!, tileId: selected.id, needsRecovery: false };
    this.selected = null;
    this.failure = '';
    this.refresh(this.mode);
    this.emittingDiscard = true;
    try {
      // Never use emitDiscard(number): that compatibility route permits unknown IDs.
      if (!this.ruleActions.canDiscard || this.world.ownHandThing(selected.id) !== thing
        || !this.world.emitDiscard(thing)) {
        this.clearPending();
        this.failure = 'Discard was not sent. Select a current tile and try again.';
      } else if (this.pending !== null) {
        this.pendingTimer = window.setTimeout(() => {
          this.pendingTimer = null;
          if (this.pending !== null) this.pending.needsRecovery = true;
          this.refresh(this.mode);
        }, CONFIRMATION_TIMEOUT_MS);
      }
    } catch (error) {
      if (this.pending !== null) this.pending.needsRecovery = true;
      this.failure = `Discard confirmation unavailable: ${error instanceof Error ? error.message : String(error)}`;
      showToast(this.failure, 'error');
    } finally {
      this.emittingDiscard = false;
    }
    this.refresh(this.mode);
  }

  private confirmPending(hand: readonly HandTile[]): void {
    if (this.pending === null || this.awaitingSnapshot || this.owner() !== this.pending.owner) return;
    if (!hand.some(tile => tile.id === this.pending!.tileId)
      && this.world.ownHandThing(this.pending.tileId) === null) {
      this.clearPending();
      this.failure = '';
    }
  }

  private renderControls(): void {
    const tile = this.selected === null ? null
      : this.world.ownHandPresentation().tiles.find(item => item.id === this.selected!.id);
    const ready = tile !== null && tile !== undefined && this.ruleActions.canDiscard && this.pending === null;
    this.discard!.disabled = !ready;
    this.discard!.textContent = this.pending !== null ? 'Waiting for server…'
      : ready ? `Discard ${tileLabel(tile.face)}` : 'Select a tile to discard';
    this.discard!.setAttribute('aria-label', this.pending !== null ? 'Discard pending server confirmation'
      : ready ? `Confirm discard of ${tileLabel(tile.face)}` : 'Select a tile, then confirm discard');
    if (tile !== null && tile !== undefined) this.discard!.dataset.tileId = String(tile.id);
    else delete this.discard!.dataset.tileId;
    this.recovery!.hidden = !(this.pending?.needsRecovery || this.failure !== '');
    this.root!.setAttribute('aria-busy', String(this.pending !== null));
    const message = this.pending !== null ? this.pendingMessage() : this.failure
      || (ready ? `${tileLabel(tile.face)} selected. Tap it again or press Escape to cancel.`
        : this.ruleActions.canDiscard ? 'Tap a tile to select. Only Confirm discard sends a command.'
          : 'Waiting for server actions. Manual pickup does not permit discarding.');
    if (this.status!.textContent !== message) this.status!.textContent = message;
  }

  private publicTile(id: number, allowMeld = false): PublicTile | null {
    if (!physicalId(id)) return null;
    const thing = this.world.things.get(id);
    const entry = this.client.things.get(id);
    if (thing === undefined || entry === null || entry.face === null
      || (entry.face !== undefined && (!Number.isInteger(entry.face) || entry.face !== thing.typeIndex))
      || thing.hidden
      || thing.hiddenHandle !== null || thing.type !== ThingType.TILE || thing.slot.thing !== thing
      || entry.slotName !== thing.slot.name || this.world.slots.get(entry.slotName) !== thing.slot
      || thing.claimedBy !== null || (entry.claimedBy ?? null) !== null
      || thing.shiftSlot !== null || (entry.shiftSlotName ?? null) !== null
      || entry.rotationIndex !== thing.rotationIndex
      || (entry.rotationIndex !== 0 && entry.rotationIndex !== 1)
      || (thing.slot.group !== 'discard' && !(allowMeld && thing.slot.group === 'meld'))
      || thing.slot.seat === null || !Number.isInteger(thing.typeIndex)
      || thing.typeIndex < 0 || thing.typeIndex >= 27) return null;
    return { id, face: thing.typeIndex, seat: thing.slot.seat };
  }

  get publicBoardEntries(): Iterable<[string | number, ThingInfo]> {
    if (this.owner() === null || this.awaitingSnapshot) return [];
    const entries: Array<[string | number, ThingInfo]> = [];
    for (const [id, entry] of this.client.things.entries()) {
      const publicSlot = /^(discard|meld)\.\d+\.\d+@[0-3]$/.test(entry.slotName);
      let projection = entry;
      if (typeof id === 'number' && publicSlot) {
        if (entry.rotationIndex === 2 || entry.rotationIndex === 3) {
          projection = { ...entry, face: null };
        } else if (entry.face !== null) {
          const tile = this.publicTile(id, true);
          // Legacy frames omit face on entitled IDs; use the reconciled public Thing, never ID arithmetic.
          projection = { ...entry, face: tile?.face };
        }
      }
      entries.push([id, projection]);
    }
    return entries;
  }

  get publicTileContext(): ReadonlyPublicContext {
    if (this.owner() === null || this.awaitingSnapshot) return { kind: 'unavailable' };
    const claim = this.ruleActions.claim;
    const claimed = claim !== null && physicalId(claim.tile) ? this.publicTile(claim.tile, true) : null;
    const observed = this.lastDiscard !== null ? this.publicTile(this.lastDiscard.id) : null;
    const last = observed !== null && observed.face === this.lastDiscard?.face
      && observed.seat === this.lastDiscard?.seat ? observed : null;
    const tile = claimed !== null && claimed.seat === claim?.source ? claimed : last;
    if (tile !== null && (tile.seat === 0 || tile.seat === 1 || tile.seat === 2 || tile.seat === 3)) {
      return {
        kind: tile === claimed ? 'claim' : 'discard', seat: tile.seat, face: tile.face,
        slot: this.client.things.get(tile.id)?.slotName,
      };
    }
    const turn = this.client.turn.get('current');
    const waiting = turn?.phase?.toLowerCase() === 'awaitingdiscard' && turn.awaitingDiscard === true
      && turn.activeSeat !== null && Number.isInteger(turn.activeSeat) && turn.activeSeat >= 0 && turn.activeSeat < 4
      && ![...this.client.things.entries()].some(([, entry]) => entry.slotName.startsWith('discard.'));
    return { kind: waiting ? 'waiting' : 'unavailable' };
  }

  private renderContext(): void {
    const context = this.publicTileContext;
    const tile = context.kind === 'claim' || context.kind === 'discard' ? context : null;
    const text = tile === null ? context.kind === 'waiting'
      ? 'Waiting for a discard' : 'Current claim / last discard unavailable'
      : `${tile.kind === 'claim' ? 'Current claim' : 'Last observed discard'}: ${tileLabel(tile.face)} · Seat ${tile.seat}`;
    if (this.context!.dataset.context === text) return;
    this.context!.dataset.context = text;
    this.context!.replaceChildren();
    if (tile !== null) {
      const image = this.face(tile.face);
      image.classList.add('mobile-hand-context-tile');
      if (tile.slot !== undefined) image.dataset.publicSlot = tile.slot;
      const id = tile.kind === 'claim' ? this.ruleActions.claim?.tile : this.lastDiscard?.id;
      if (physicalId(id)) image.dataset.tileId = String(id);
      this.context!.appendChild(image);
    }
    const label = document.createElement('span');
    label.className = 'mobile-hand-context-label';
    label.textContent = text;
    this.context!.appendChild(label);
  }

  private observe(entries: Entry[], full: boolean): void {
    if (full) this.awaitingSnapshot = false;
    const room = this.client.connected() && this.client.serverSnapshotGameId === this.client.lastGameId
      ? this.client.serverSnapshotGameId : null;
    if (room === null || room !== this.publicRoom || this.client.connectionMode !== 'changsha') {
      this.publicSlots.clear();
      this.lastDiscard = null;
      this.publicRoom = room;
      for (const [id, entry] of this.client.things.entries()) {
        if (physicalId(id) && entry !== null) this.publicSlots.set(id, entry.slotName);
      }
    } else {
      const discards: Array<PublicTile | null> = [];
      // FULL is a replacement collection, not a break in this room's observed history.
      const changed = full ? [...this.client.things.entries()].map(([id]) => id)
        : entries.filter(([kind]) => kind === 'things').map(([, id]) => id);
      for (const id of changed) {
        if (!physicalId(id)) continue;
        const entry = this.client.things.get(id);
        const previous = this.publicSlots.get(id);
        if (entry !== null && entry.slotName.startsWith('discard.')
          && (previous === undefined || !previous.startsWith('discard.'))) discards.push(this.publicTile(id));
        if (entry === null) this.publicSlots.delete(id);
        else this.publicSlots.set(id, entry.slotName);
      }
      if (full) {
        for (const id of this.publicSlots.keys()) {
          if (this.client.things.get(id) === null) this.publicSlots.delete(id);
        }
      }
      if (discards.length > 0) this.lastDiscard = discards.length === 1 ? discards[0] : null;
    }
    const claim = room !== null && this.client.connectionMode === 'changsha' ? this.ruleActions.claim : null;
    const claimed = claim !== null && physicalId(claim.tile) ? this.publicTile(claim.tile) : null;
    if (claimed !== null && claimed.seat === claim?.source) this.lastDiscard = claimed;
    if (this.lastDiscard !== null) {
      const tile = this.publicTile(this.lastDiscard.id);
      if (tile === null || tile.face !== this.lastDiscard.face || tile.seat !== this.lastDiscard.seat) {
        this.lastDiscard = null;
      }
    }
    for (const [kind, key, value] of entries) {
      if (kind !== 'actionRejected' || key !== 'current' || value === null || this.pending === null) continue;
      const rejection = this.client.actionRejected.get('current');
      if (rejection?.action === 'discard' && rejection.requestedSeat === this.pending.seat
        && this.owner() === this.pending.owner) {
        this.clearPending();
        this.selected = null;
        this.failure = `Discard rejected: ${rejection.reason}. Check the current hand before trying again.`;
        showToast(this.failure, 'error');
      }
    }
    this.refresh(this.mode);
  }

  private moveHud(): void {
    const selectors = ['.ferro-claim-overlay', '#pickup-hud', '#roll-dice', '#own-turn-actions', '#rule-action-recovery'];
    for (const selector of selectors) {
      const element = document.querySelector<HTMLElement>(selector);
      if (element === null || this.moved.has(element) || element.parentNode === null) continue;
      const anchor = document.createComment('mobile-hand original HUD position');
      element.parentNode.insertBefore(anchor, element);
      this.moved.set(element, anchor);
      this.actionsArea!.appendChild(element);
    }
  }

  private visibleHand(): boolean {
    const root = this.root!;
    const style = window.getComputedStyle(root);
    const bounds = root.getBoundingClientRect();
    if (style.position !== 'fixed' || style.display === 'none' || style.visibility === 'hidden'
      || bounds.width <= 0 || bounds.height <= 0 || bounds.left < 0 || bounds.top < 0
      || bounds.right > window.innerWidth + 1 || bounds.bottom > window.innerHeight + 1) return false;
    for (const tile of this.tiles!.querySelectorAll<HTMLElement>('.mobile-hand-tile')) {
      const rect = tile.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0 || rect.left < 0 || rect.top < 0
        || rect.right > window.innerWidth + 1 || rect.bottom > window.innerHeight + 1) return false;
    }
    return true;
  }

  private clearPending(): void {
    this.pending = null;
    if (this.pendingTimer !== null) window.clearTimeout(this.pendingTimer);
    this.pendingTimer = null;
  }

  private deactivate(): void {
    this.world.setOwnHandDomActive(false);
    this.selected = null;
    for (const [element, anchor] of this.moved) {
      if (anchor.parentNode !== null && this.root?.contains(element)) {
        anchor.parentNode.insertBefore(element, anchor.nextSibling);
      }
      anchor.remove();
    }
    this.moved.clear();
    this.root?.remove();
    this.root = this.tiles = this.context = this.status = this.actionsArea = null;
    this.discard = this.recovery = null;
    this.handSignature = '';
    this.renderStandaloneRecovery();
  }
}
