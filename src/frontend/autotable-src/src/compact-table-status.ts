import type { CompactTableCounts } from './compact-table';
import type { ScreenArea } from './table-fit';
import { getActiveLocale, type LocaleTag } from './i18n';

interface StatusCopy {
  title: string;
  seat: string;
  seatShort: string;
  hand: string;
  wall: string;
  discards: string;
  melds: string;
  directions: readonly string[];
}

const COPY: Record<LocaleTag, StatusCopy> = {
  en: {
    title: 'Compact 3D table: concealed hand and wall counts',
    seat: 'Seat', seatShort: 'S', hand: 'Hand', wall: 'Wall', discards: 'Discards', melds: 'Melds',
    directions: ['lower left', 'lower right', 'upper right', 'upper left'],
  },
  'zh-Hans': {
    title: '紧凑 3D 牌桌：手牌与牌墙数量',
    seat: '座位', seatShort: '座', hand: '手牌', wall: '牌墙', discards: '弃牌', melds: '副露',
    directions: ['左下', '右下', '右上', '左上'],
  },
  'zh-Hant': {
    title: '緊湊 3D 牌桌：手牌與牌牆數量',
    seat: '座位', seatShort: '座', hand: '手牌', wall: '牌牆', discards: '棄牌', melds: '副露',
    directions: ['左下', '右下', '右上', '左上'],
  },
};

/** Counts replace only the suppressed backs/wall, never the public 3D tiles. */
export class CompactTableStatus {
  private readonly root: HTMLDivElement;
  private readonly seats: HTMLSpanElement[] = [];
  private readonly wall: HTMLSpanElement;
  private signature = '';

  constructor(parent: HTMLElement) {
    this.root = document.createElement('div');
    this.root.id = 'compact-table-status';
    this.root.setAttribute('aria-label', COPY[getActiveLocale()].title);
    Object.assign(this.root.style, {
      position: 'absolute', display: 'none', alignItems: 'center', justifyContent: 'space-evenly',
      gap: '3px', height: '22px', pointerEvents: 'none', zIndex: '2',
      color: '#e6f6ff', font: '11px/1.2 system-ui, sans-serif', whiteSpace: 'nowrap',
      textShadow: '0 1px 2px #001725',
    });
    for (let seat = 0; seat < 4; seat++) {
      const label = document.createElement('span');
      label.dataset.compactSeat = String(seat);
      this.seats.push(label);
      this.root.appendChild(label);
    }
    this.wall = document.createElement('span');
    this.wall.dataset.compactWall = '';
    this.root.appendChild(this.wall);
    parent.appendChild(this.root);
  }

  update(active: boolean, counts: CompactTableCounts | null, viewSeat: number | null, area: ScreenArea | null): void {
    if (!active || counts === null || area === null) {
      this.root.style.display = 'none';
      this.signature = '';
      document.body.removeAttribute('data-table-layout');
      return;
    }
    const locale = getActiveLocale();
    const copy = COPY[locale];
    const signature = `${locale}/${viewSeat}/${counts.wall}/${area.left}/${area.right}/${area.top}/`
      + counts.seats.map(seat => `${seat.seat}:${seat.hand}:${seat.discards}:${seat.melds}`).join(';');
    if (signature === this.signature) return;
    this.signature = signature;
    document.body.setAttribute('data-table-layout', 'compact');
    this.root.setAttribute('aria-label', copy.title);
    this.root.style.display = 'flex';
    this.root.style.left = `${area.left}px`;
    this.root.style.top = `${Math.max(0, area.top - 24)}px`;
    this.root.style.width = `${Math.max(1, area.right - area.left)}px`;
    for (const seat of counts.seats) {
      const label = this.seats[seat.seat];
      const relative = (seat.seat - (viewSeat ?? 0) + 4) % 4;
      const direction = ['↙', '↘', '↗', '↖'][relative];
      const text = `${copy.seatShort}${seat.seat}${direction} ${copy.hand}${seat.hand}`;
      if (label.textContent !== text) label.textContent = text;
      label.dataset.compactRelative = String(relative);
      label.dataset.compactHandCount = String(seat.hand);
      label.dataset.compactDiscardCount = String(seat.discards);
      label.dataset.compactMeldCount = String(seat.melds);
      label.setAttribute('aria-label',
        `${copy.seat} ${seat.seat}, ${copy.directions[relative]}: ${copy.hand} ${seat.hand}, `
          + `${copy.discards} ${seat.discards}, ${copy.melds} ${seat.melds}`);
    }
    this.wall.textContent = `${copy.wall} ${counts.wall}`;
    this.wall.dataset.compactWallCount = String(counts.wall);
  }
}
