import type { Client } from '../client';
import { getActiveLocale, t, type LocaleTag } from '../i18n';
import type { ThingInfo } from '../types';
import {
  buildPublicBoard,
  type PublicBoardSnapshot,
  type PublicSeat,
  type PublicTile,
  type ReadonlyPublicContext,
  type Seat,
} from '../public-board-model';
import './readable-board.css';

interface BoardCopy {
  title: string;
  publicArea: string;
  you: string;
  right: string;
  opposite: string;
  left: string;
  seat: string;
  active: string;
  melds: string;
  discards: string;
  tiles: string;
  hidden: string;
  unknown: string;
  hiddenFaces: string;
  unknownFaces: string;
  claim: string;
  discard: string;
  waiting: string;
  unavailable: string;
  from: string;
  wall: string;
  wallUnknown: string;
  table: string;
  switchTable: string;
}

const COPY: Record<LocaleTag, BoardCopy> = {
  en: {
    title: 'Readable public table',
    publicArea: 'Public discards and melds for all four seats. Scroll to see more tiles.',
    you: 'You', right: 'Right', opposite: 'Opposite', left: 'Left', seat: 'Seat', active: 'Active',
    melds: 'Melds', discards: 'Discards', tiles: 'tiles', hidden: 'Concealed kong tile — face hidden',
    unknown: 'Public tile — face unavailable', hiddenFaces: 'Faces hidden', unknownFaces: 'Face unavailable',
    claim: 'Current claim', discard: 'Last observed discard',
    waiting: 'Waiting for a discard', unavailable: 'Current claim / last discard unavailable',
    from: 'From', wall: 'Wall', wallUnknown: 'Wall count unavailable',
    table: '3D table', switchTable: 'Switch to the 3D table, keeping your hand controls',
  },
  'zh-Hans': {
    title: '清晰公共牌桌',
    publicArea: '四个座位的弃牌和副露。可滚动查看更多牌。',
    you: '你', right: '右家', opposite: '对家', left: '左家', seat: '座位', active: '当前',
    melds: '副露', discards: '弃牌', tiles: '张牌', hidden: '暗杠牌 — 牌面隐藏',
    unknown: '公共牌 — 牌面不可用', hiddenFaces: '牌面隐藏', unknownFaces: '牌面不可用',
    claim: '当前待认领牌', discard: '最近观察到的弃牌',
    waiting: '等待弃牌', unavailable: '当前待认领牌／最近弃牌不可用',
    from: '来自', wall: '余牌', wallUnknown: '余牌数不可用',
    table: '3D 牌桌', switchTable: '切换到 3D 牌桌，保留手牌操作',
  },
  'zh-Hant': {
    title: '清晰公開牌桌',
    publicArea: '四個座位的棄牌和副露。可捲動查看更多牌。',
    you: '你', right: '右家', opposite: '對家', left: '左家', seat: '座位', active: '目前',
    melds: '副露', discards: '棄牌', tiles: '張牌', hidden: '暗槓牌 — 牌面隱藏',
    unknown: '公開牌 — 牌面無法取得', hiddenFaces: '牌面隱藏', unknownFaces: '牌面無法取得',
    claim: '目前待認領牌', discard: '最近觀察到的棄牌',
    waiting: '等待棄牌', unavailable: '目前待認領牌／最近棄牌無法取得',
    from: '來自', wall: '餘牌', wallUnknown: '餘牌數無法取得',
    table: '3D 牌桌', switchTable: '切換到 3D 牌桌，保留手牌操作',
  },
};

interface SeatView {
  root: HTMLElement;
  heading: HTMLElement;
  active: HTMLElement;
  meldLabel: HTMLElement;
  melds: HTMLElement;
  discardLabel: HTMLElement;
  discards: HTMLElement;
  signature: string;
}

interface SceneState {
  element: HTMLElement;
  inert: boolean;
  inertAttribute: string | null;
  ariaHidden: string | null;
}

const DOCK_INSET_PROPERTY = '--readable-board-dock-inset';

function isSeat(value: unknown): value is Seat {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value < 4;
}

function isFace(value: unknown): value is number {
  return typeof value === 'number' && Number.isInteger(value) && value >= 0 && value < 27;
}

function validSnapshot(snapshot: PublicBoardSnapshot, viewer: Seat): boolean {
  const validTile = (tile: PublicTile): boolean => typeof tile.slot === 'string' && tile.slot !== ''
    && (tile.kind === 'face' ? isFace(tile.face)
      : (tile.kind === 'back' || tile.kind === 'unknown') && tile.face === null);
  return snapshot.viewerSeat === viewer && (snapshot.activeSeat === null || isSeat(snapshot.activeSeat))
    && snapshot.seats.length === 4 && new Set(snapshot.seats.map(seat => seat.seat)).size === 4
    && snapshot.seats.every(seat => isSeat(seat.seat) && seat.discards.every(validTile)
      && seat.melds.every(meld => Number.isInteger(meld.group) && meld.group >= 0
        && meld.tiles.length > 0 && meld.tiles.length <= 4 && meld.tiles.every(validTile)));
}

function tileLabel(face: number): string {
  return t('hand.tile', { rank: face % 9 + 1, suit: t(`hand.suit_${Math.floor(face / 9)}`) });
}

function seatLabel(seat: Seat, viewer: Seat, copy: BoardCopy): string {
  const relative = [copy.you, copy.right, copy.opposite, copy.left][(seat - viewer + 4) % 4];
  return `${relative} · ${copy.seat} ${seat}`;
}

function restoreAttribute(element: HTMLElement, name: string, value: string | null): void {
  if (value === null) element.removeAttribute(name);
  else element.setAttribute(name, value);
}

export class ReadableBoardController {
  private root: HTMLElement | null = null;
  private scroller: HTMLElement | null = null;
  private context: HTMLElement | null = null;
  private contextImage: HTMLElement | null = null;
  private contextTitle: HTMLElement | null = null;
  private contextSource: HTMLElement | null = null;
  private contextFaceLabel: HTMLElement | null = null;
  private wall: HTMLElement | null = null;
  private switchButton: HTMLButtonElement | null = null;
  private seats = new Map<Seat, SeatView>();
  private scene: SceneState | null = null;
  private dockInsetStyle: { element: HTMLElement; value: string; priority: string } | null = null;
  private ownerKey: string | null = null;
  private boardSignature = '';
  private contextSignature = '';
  private geometrySignature = '';
  private fits = false;

  constructor(
    private readonly client: Client,
    private readonly publicEntries: () => Iterable<[string | number, ThingInfo]> = () => client.things.entries(),
  ) {
    const deactivate = (): void => this.deactivate();
    this.client.on('joining', deactivate);
    this.client.on('connect', deactivate);
    this.client.on('disconnect', deactivate);
    this.client.on('update', () => {
      if (this.scene !== null && this.owner() === null) this.deactivate();
    });
    window.addEventListener('resize', () => {
      this.geometrySignature = '';
      if (!this.portrait()) this.deactivate();
    });
  }

  refresh(active: boolean, footerInsetPx: number, context: ReadonlyPublicContext): boolean {
    const owner = active ? this.owner() : null;
    const main = document.getElementById('main');
    if (owner === null || main === null || !Number.isFinite(footerInsetPx) || footerInsetPx < 0
      || footerInsetPx >= window.innerHeight || this.panelOpen()) {
      this.deactivate();
      return false;
    }
    const viewer = this.client.seat as Seat;
    let snapshot: PublicBoardSnapshot;
    try {
      snapshot = buildPublicBoard(this.publicEntries(), viewer,
        this.client.turn.get('current')?.activeSeat ?? null);
      if (!validSnapshot(snapshot, viewer)) {
        this.deactivate();
        return false;
      }
    } catch {
      this.deactivate();
      return false;
    }
    this.mount();
    if (owner !== this.ownerKey) {
      this.ownerKey = owner;
      this.boardSignature = this.contextSignature = '';
      this.mountSeats(viewer);
      this.scroller!.scrollTop = 0;
    }
    this.render(snapshot, context, viewer);
    const wasHidden = this.root!.hidden;
    this.root!.hidden = false;
    document.body.classList.add('readable-board-active');
    this.measure(footerInsetPx, wasHidden);
    const style = window.getComputedStyle(this.root!);
    if (!this.root!.isConnected || !this.fits || style.position !== 'fixed' || style.display === 'none'
      || style.visibility === 'hidden' || style.visibility === 'collapse' || Number(style.opacity) === 0) {
      this.deactivate();
      return false;
    }
    this.protectScene(main);
    return true;
  }

  private portrait(): boolean {
    return Number.isFinite(window.innerWidth) && Number.isFinite(window.innerHeight)
      && window.innerWidth > 0 && window.innerWidth <= 600 && window.innerHeight > window.innerWidth;
  }

  private owner(): string | null {
    const params = new URLSearchParams(window.location.search);
    const room = params.get('gameId'), seat = this.client.seat, player = this.client.playerId();
    if (!this.portrait() || params.getAll('mobileLayout').length !== 1 || params.get('mobileLayout') !== 'readable'
      || (params.get('variant') ?? 'changsha').toLowerCase() !== 'changsha'
      || params.getAll('gameId').length !== 1 || room === null || room === ''
      || room !== this.client.lastGameId || room !== this.client.serverSnapshotGameId
      || !this.client.connected() || this.client.connectionMode !== 'changsha' || !isSeat(seat)
      || !player || this.client.seatPlayers[seat] !== player
      || document.body.classList.contains('spectating') || document.body.classList.contains('lobby-active')) return null;
    return JSON.stringify([room, player, seat]);
  }

  private panelOpen(): boolean {
    const chat = document.getElementById('chat-panel');
    return (chat !== null && !chat.hidden && !chat.classList.contains('chat-panel-collapsed')
        && window.getComputedStyle(chat).display !== 'none')
      || document.body.classList.contains('move-log-open')
      || document.getElementById('settings-drawer')?.classList.contains('settings-open') === true
      || document.getElementById('settings-drawer-v2')?.classList.contains('settings-drawer-v2-open') === true;
  }

  private mount(): void {
    if (this.root !== null) return;
    const root = document.createElement('section');
    root.id = 'readable-board';
    root.hidden = true;
    root.dataset.mobileLayout = 'readable';
    root.setAttribute('role', 'region');
    const header = document.createElement('header');
    header.className = 'readable-board-header';
    const context = document.createElement('div');
    context.className = 'readable-board-context';
    context.setAttribute('role', 'status');
    context.setAttribute('aria-live', 'polite');
    context.setAttribute('aria-atomic', 'true');
    const image = document.createElement('div');
    image.className = 'readable-board-context-image';
    const description = document.createElement('div');
    description.className = 'readable-board-context-description';
    const title = document.createElement('strong');
    const source = document.createElement('span');
    const faceLabel = document.createElement('span');
    description.append(title, source, faceLabel);
    context.append(image, description);
    const controls = document.createElement('div');
    controls.className = 'readable-board-controls';
    const wall = document.createElement('p');
    wall.className = 'readable-board-wall';
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'readable-board-switch';
    button.addEventListener('click', () => {
      const url = new URL(window.location.href);
      url.searchParams.set('mobileLayout', 'hybrid');
      window.history.replaceState(window.history.state, '', url);
      this.deactivate();
    });
    for (const event of ['keydown', 'keyup']) {
      button.addEventListener(event, event => {
        const key = (event as KeyboardEvent).key;
        if (key === ' ' || key === 'Enter') event.stopPropagation();
      });
    }
    controls.append(wall, button);
    header.append(context, controls);
    const scroller = document.createElement('div');
    scroller.className = 'readable-board-public readable-board-scroll';
    scroller.tabIndex = 0;
    scroller.setAttribute('role', 'region');
    root.append(header, scroller);
    document.body.appendChild(root);
    this.root = root;
    this.scroller = scroller;
    this.context = context;
    this.contextImage = image;
    this.contextTitle = title;
    this.contextSource = source;
    this.contextFaceLabel = faceLabel;
    this.wall = wall;
    this.switchButton = button;
  }

  private mountSeats(viewer: Seat): void {
    this.seats.clear();
    this.scroller!.replaceChildren();
    for (const [offset, position] of [[2, 'opposite'], [3, 'left'], [1, 'right'], [0, 'you']] as const) {
      const seat = (viewer + offset) % 4 as Seat;
      const root = document.createElement('section');
      root.className = 'readable-board-seat';
      root.dataset.seat = String(seat);
      root.dataset.position = position;
      const header = document.createElement('div');
      header.className = 'readable-board-seat-heading';
      const heading = document.createElement('h3');
      heading.id = `readable-board-seat-${seat}`;
      const active = document.createElement('span');
      active.className = 'readable-board-seat-active';
      header.append(heading, active);
      root.setAttribute('aria-labelledby', heading.id);
      const meldZone = document.createElement('div');
      meldZone.className = 'readable-board-zone';
      const meldLabel = document.createElement('h4');
      const melds = document.createElement('ul');
      melds.className = 'readable-board-melds';
      meldZone.append(meldLabel, melds);
      const discardZone = document.createElement('div');
      discardZone.className = 'readable-board-zone';
      const discardLabel = document.createElement('h4');
      const discards = document.createElement('ol');
      discards.className = 'readable-board-discards';
      discardZone.append(discardLabel, discards);
      root.append(header, meldZone, discardZone);
      this.scroller!.appendChild(root);
      this.seats.set(seat, { root, heading, active, meldLabel, melds, discardLabel, discards, signature: '' });
    }
  }

  private render(snapshot: PublicBoardSnapshot, supplied: ReadonlyPublicContext, viewer: Seat): void {
    const locale = getActiveLocale(), copy = COPY[locale], scrollTop = this.scroller!.scrollTop;
    const signature = JSON.stringify([locale, snapshot.seats, snapshot.wallCount]);
    const changed = signature !== this.boardSignature;
    if (changed) {
      this.root!.setAttribute('aria-label', copy.title);
      this.root!.dataset.viewerSeat = String(viewer);
      this.scroller!.setAttribute('aria-label', copy.publicArea);
      this.switchButton!.textContent = copy.table;
      this.switchButton!.setAttribute('aria-label', copy.switchTable);
      this.wall!.textContent = Number.isInteger(snapshot.wallCount) && snapshot.wallCount >= 0
        ? `${copy.wall}: ${snapshot.wallCount}` : copy.wallUnknown;
      for (const seat of snapshot.seats) this.renderSeat(seat, viewer, copy, locale);
      this.boardSignature = signature;
    }
    for (const [seat, view] of this.seats) {
      const active = snapshot.activeSeat === seat;
      if (view.root.dataset.active !== String(active)) {
        view.root.dataset.active = String(active);
        view.active.hidden = !active;
      }
    }
    const context: ReadonlyPublicContext = (supplied.kind === 'claim' || supplied.kind === 'discard')
      && (!isSeat(supplied.seat) || !isFace(supplied.face)) ? { kind: 'unavailable' } : supplied;
    const contextSignature = JSON.stringify([locale, context]);
    const contextChanged = contextSignature !== this.contextSignature;
    if (contextChanged) {
      this.context!.dataset.kind = context.kind;
      this.contextImage!.replaceChildren();
      this.contextSource!.textContent = this.contextFaceLabel!.textContent = '';
      delete this.context!.dataset.seat;
      delete this.context!.dataset.face;
      delete this.context!.dataset.publicSlot;
      if (context.kind === 'claim' || context.kind === 'discard') {
        this.contextTitle!.textContent = context.kind === 'claim' ? copy.claim : copy.discard;
        this.contextSource!.textContent = `${copy.from} ${seatLabel(context.seat, viewer, copy)}`;
        this.contextFaceLabel!.textContent = tileLabel(context.face);
        this.context!.dataset.seat = String(context.seat);
        this.context!.dataset.face = String(context.face);
        if (context.slot !== undefined) this.context!.dataset.publicSlot = context.slot;
        const tile = this.tile({ kind: 'face', face: context.face, slot: '' }, copy);
        tile.classList.add('readable-board-context-face');
        this.contextImage!.appendChild(tile);
      } else {
        this.contextTitle!.textContent = context.kind === 'waiting' ? copy.waiting : copy.unavailable;
        const placeholder = document.createElement('span');
        placeholder.className = 'readable-board-context-placeholder';
        placeholder.textContent = context.kind === 'waiting' ? '…' : '?';
        placeholder.setAttribute('aria-hidden', 'true');
        this.contextImage!.appendChild(placeholder);
      }
      this.contextSignature = contextSignature;
    }
    if (changed || contextChanged) {
      for (const tile of this.scroller!.querySelectorAll<HTMLElement>('.readable-board-tile')) {
        const match = (context.kind === 'claim' || context.kind === 'discard') && context.slot !== undefined
          && tile.dataset.publicSlot === context.slot && tile.dataset.face === String(context.face)
          && tile.dataset.publicSeat === String(context.seat);
        tile.classList.toggle('readable-board-context-match', match);
      }
      this.scroller!.scrollTop = scrollTop;
    }
  }

  private renderSeat(seat: PublicSeat, viewer: Seat, copy: BoardCopy, locale: LocaleTag): void {
    const view = this.seats.get(seat.seat)!;
    const signature = JSON.stringify([locale, seat]);
    if (signature === view.signature) return;
    view.heading.textContent = seatLabel(seat.seat, viewer, copy);
    view.active.textContent = copy.active;
    view.meldLabel.textContent = `${copy.melds} · ${seat.melds.length}`;
    view.discardLabel.textContent = `${copy.discards} · ${seat.discards.length}${
      seat.discards.some(tile => tile.kind !== 'face') ? ` · ${copy.unknownFaces}` : ''}`;
    view.melds.replaceChildren();
    view.discards.replaceChildren();
    for (const meld of seat.melds) {
      const group = document.createElement('li');
      group.className = 'readable-board-meld';
      group.dataset.group = String(meld.group);
      const note = meld.tiles.some(tile => tile.kind === 'back') ? copy.hiddenFaces
        : meld.tiles.some(tile => tile.kind === 'unknown') ? copy.unknownFaces : '';
      const label = `${meld.tiles.length} ${copy.tiles}${note ? ` · ${note}` : ''}`;
      group.setAttribute('aria-label', label);
      const tiles = document.createElement('div');
      tiles.className = 'readable-board-meld-tiles';
      for (const tile of meld.tiles) tiles.appendChild(this.tile(tile, copy, seat.seat));
      const caption = document.createElement('span');
      caption.className = 'readable-board-meld-caption';
      caption.textContent = label;
      group.append(tiles, caption);
      view.melds.appendChild(group);
    }
    for (const tile of seat.discards) {
      const item = document.createElement('li');
      item.appendChild(this.tile(tile, copy, seat.seat));
      view.discards.appendChild(item);
    }
    view.signature = signature;
  }

  private tile(tile: PublicTile, copy: BoardCopy, seat?: Seat): HTMLElement {
    const image = document.createElement('span');
    image.className = 'readable-board-tile';
    image.dataset.kind = tile.kind;
    if (tile.slot !== '') {
      image.dataset.publicSlot = tile.slot;
      image.dataset.slot = tile.slot;
    }
    if (seat !== undefined) image.dataset.publicSeat = String(seat);
    image.setAttribute('role', 'img');
    if (tile.kind === 'face') {
      image.classList.add('readable-board-face');
      image.dataset.face = String(tile.face);
      image.setAttribute('aria-label', tileLabel(tile.face));
      image.style.backgroundPosition = `${(tile.face % 8) * 100 / 7}% ${Math.floor(tile.face / 8) * 100 / 5.4}%`;
    } else {
      const label = tile.kind === 'back' ? copy.hidden : copy.unknown;
      image.textContent = tile.kind === 'back' ? '▧' : '?';
      image.setAttribute('aria-label', label);
      image.title = label;
    }
    return image;
  }

  private measure(footerInsetPx: number, force: boolean): void {
    const visual = window.visualViewport;
    const chat = document.getElementById('chat-panel');
    const chatVisible = chat !== null && !chat.hidden && window.getComputedStyle(chat).display !== 'none';
    const dock = document.getElementById('mobile-hand-dock');
    let dockInset = 0;
    if (dock !== null && !dock.hidden) {
      const style = window.getComputedStyle(dock);
      if (style.display !== 'none' && style.visibility !== 'hidden' && style.visibility !== 'collapse') {
        const bounds = dock.getBoundingClientRect();
        if (bounds.width > 0 && bounds.height > 0 && Number.isFinite(bounds.top)) {
          dockInset = Math.ceil(Math.max(0, window.innerHeight - bounds.top));
        }
      }
    }
    // Keep the camera's viewport-stable inset, but independently track actual
    // dock growth when recovery/HUD controls become part of the DOM layout.
    const dockFooter = Math.max(Math.ceil(footerInsetPx), dockInset);
    const key = JSON.stringify([window.innerWidth, window.innerHeight, Math.ceil(footerInsetPx),
      dockInset,
      visual?.width, visual?.height, visual?.offsetTop, visual?.offsetLeft,
      document.body.classList.contains('mobile-table-status-visible'), chatVisible,
      document.getElementById('turn-banner')?.hidden, document.getElementById('bot-banner')?.hidden,
      document.getElementById('connection-banner')?.hidden]);
    if (!force && key === this.geometrySignature) return;
    if (this.dockInsetStyle === null) {
      this.dockInsetStyle = {
        element: document.body,
        value: document.body.style.getPropertyValue(DOCK_INSET_PROPERTY),
        priority: document.body.style.getPropertyPriority(DOCK_INSET_PROPERTY),
      };
    }
    document.body.style.setProperty(DOCK_INSET_PROPERTY, `${dockFooter}px`);
    let headerBottom = 0;
    for (const id of ['new-game-primary', 'settings-button', 'settings-toggle', 'variant-badge',
      'lobby-toggle', 'turn-banner', 'bot-banner', 'connection-banner']) {
      const element = document.getElementById(id);
      if (element === null || element.hidden) continue;
      const style = window.getComputedStyle(element);
      if (style.display === 'none' || style.visibility === 'hidden') continue;
      const bounds = element.getBoundingClientRect();
      if (bounds.width > 0 && bounds.height > 0 && bounds.top >= 0 && bounds.top < window.innerHeight / 2) {
        // Reserve two banner lines, not the changing countdown's current width.
        headerBottom = Math.max(headerBottom, id === 'turn-banner'
          ? Math.max(bounds.bottom, bounds.top + 64) + 6 : bounds.bottom + 6);
      }
    }
    let footer = dockFooter;
    if (chatVisible && chat !== null) {
      const bounds = chat.getBoundingClientRect();
      if (bounds.width > 0 && bounds.height > 0) footer = Math.max(footer, window.innerHeight - bounds.top);
    }
    this.root!.style.setProperty('--readable-board-header-bottom', `${Math.ceil(headerBottom)}px`);
    this.root!.style.setProperty('--readable-board-footer-inset', `${Math.ceil(footer)}px`);
    const bounds = this.root!.getBoundingClientRect(), pane = this.scroller!.getBoundingClientRect();
    this.fits = bounds.width > 0 && bounds.height >= 160 && pane.height >= 64
      && bounds.left >= 0 && bounds.top >= 0 && bounds.right <= window.innerWidth + 1
      && bounds.bottom <= window.innerHeight + 1;
    this.geometrySignature = key;
  }

  private protectScene(element: HTMLElement): void {
    if (this.scene?.element === element) return;
    this.restoreScene();
    this.scene = {
      element, inert: element.inert, inertAttribute: element.getAttribute('inert'),
      ariaHidden: element.getAttribute('aria-hidden'),
    };
    element.inert = true;
    element.setAttribute('inert', '');
    element.setAttribute('aria-hidden', 'true');
  }

  private restoreScene(): void {
    if (this.scene === null) return;
    const { element, inert, inertAttribute, ariaHidden } = this.scene;
    element.inert = inert;
    restoreAttribute(element, 'inert', inertAttribute);
    restoreAttribute(element, 'aria-hidden', ariaHidden);
    this.scene = null;
  }

  private deactivate(): void {
    const focused = this.root !== null && !this.root.hidden && this.root.contains(document.activeElement);
    if (this.root !== null) this.root.hidden = true;
    document.body.classList.remove('readable-board-active');
    if (this.dockInsetStyle !== null) {
      const { element, value, priority } = this.dockInsetStyle;
      if (value === '') element.style.removeProperty(DOCK_INSET_PROPERTY);
      else element.style.setProperty(DOCK_INSET_PROPERTY, value, priority);
      this.dockInsetStyle = null;
    }
    this.restoreScene();
    this.geometrySignature = '';
    this.fits = false;
    if (focused) document.getElementById('lobby-toggle')?.focus({ preventScroll: true });
  }
}
