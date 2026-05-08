import type { Tile, Suit } from '../types';

export type TileSize = 'sm' | 'md' | 'lg';

interface TileFaceProps {
  tile?: Tile;
  size?: TileSize;
  faceDown?: boolean;
  highlighted?: boolean;
  onClick?: () => void;
  disabled?: boolean;
  title?: string;
}

const SIZE_DIMS: Record<TileSize, { w: number; h: number; fontScale: number }> = {
  sm: { w: 28, h: 38, fontScale: 0.55 },
  md: { w: 36, h: 50, fontScale: 0.55 },
  lg: { w: 48, h: 66, fontScale: 0.55 },
};

const SUIT_LABEL: Record<Suit, string> = { wan: '萬', tong: '筒', tiao: '条' };

const RANK_HANZI = ['', '一', '二', '三', '四', '五', '六', '七', '八', '九'];

const SUIT_COLOR: Record<Suit, string> = {
  wan: '#b91c1c',   // red
  tong: '#1d4ed8',  // blue
  tiao: '#15803d',  // green
};

/**
 * Render the rank-N pip pattern for the dot (tong) suit using small circles.
 * Patterns roughly follow standard mahjong tile arrangements.
 */
function DotPips({ rank, color, w, h }: { rank: number; color: string; w: number; h: number }) {
  // Layouts: arrays of [col, row] in a 3x3 grid (col 0-2, row 0-2).
  const layouts: Record<number, [number, number][]> = {
    1: [[1, 1]],
    2: [[1, 0], [1, 2]],
    3: [[0, 0], [1, 1], [2, 2]],
    4: [[0, 0], [2, 0], [0, 2], [2, 2]],
    5: [[0, 0], [2, 0], [1, 1], [0, 2], [2, 2]],
    6: [[0, 0], [2, 0], [0, 1], [2, 1], [0, 2], [2, 2]],
    7: [[0, 0], [1, 0], [2, 0], [1, 1], [0, 2], [1, 2], [2, 2]],
    8: [[0, 0], [2, 0], [0, 1], [2, 1], [0, 2], [2, 2], [1, 0], [1, 2]],
    9: [[0, 0], [1, 0], [2, 0], [0, 1], [1, 1], [2, 1], [0, 2], [1, 2], [2, 2]],
  };
  const pts = layouts[rank] ?? [];
  const padX = w * 0.18;
  const padY = h * 0.18;
  const usableW = w - padX * 2;
  const usableH = h - padY * 2;
  const r = Math.min(usableW, usableH) * 0.13;
  return (
    <g>
      {pts.map(([c, row], i) => {
        const cx = padX + (usableW * c) / 2;
        const cy = padY + (usableH * row) / 2;
        return <circle key={i} cx={cx} cy={cy} r={r} fill={color} />;
      })}
    </g>
  );
}

/**
 * Render the rank-N bamboo pattern for the tiao suit as small rectangles.
 * Rank 1 is special (a "bird" — we draw a stylized larger mark).
 */
function BambooSticks({ rank, color, w, h }: { rank: number; color: string; w: number; h: number }) {
  if (rank === 1) {
    // Stylized "1 tiao" — a small bird-like glyph using a circle + lines.
    const cx = w / 2;
    const cy = h / 2;
    const r = Math.min(w, h) * 0.22;
    return (
      <g>
        <circle cx={cx} cy={cy} r={r} fill="none" stroke={color} strokeWidth={r * 0.25} />
        <line x1={cx} y1={cy - r * 1.1} x2={cx} y2={cy + r * 1.1} stroke={color} strokeWidth={r * 0.2} strokeLinecap="round" />
      </g>
    );
  }
  // For ranks 2-9 we draw vertical sticks in a 3-col grid layout.
  const layouts: Record<number, [number, number][]> = {
    2: [[1, 0], [1, 2]],
    3: [[0, 0], [1, 1], [2, 2]],
    4: [[0, 0], [2, 0], [0, 2], [2, 2]],
    5: [[0, 0], [2, 0], [1, 1], [0, 2], [2, 2]],
    6: [[0, 0], [2, 0], [0, 1], [2, 1], [0, 2], [2, 2]],
    7: [[1, 0], [0, 1], [2, 1], [0, 2], [1, 2], [2, 2], [1, 1]],
    8: [[0, 0], [1, 0], [2, 0], [0, 1], [2, 1], [0, 2], [1, 2], [2, 2]],
    9: [[0, 0], [1, 0], [2, 0], [0, 1], [1, 1], [2, 1], [0, 2], [1, 2], [2, 2]],
  };
  const pts = layouts[rank] ?? [];
  const padX = w * 0.2;
  const padY = h * 0.18;
  const usableW = w - padX * 2;
  const usableH = h - padY * 2;
  const stickW = Math.min(usableW, usableH) * 0.18;
  const stickH = Math.min(usableW, usableH) * 0.5;
  return (
    <g>
      {pts.map(([c, row], i) => {
        const cx = padX + (usableW * c) / 2 - stickW / 2;
        const cy = padY + (usableH * row) / 2 - stickH / 2;
        return (
          <rect
            key={i}
            x={cx}
            y={cy}
            width={stickW}
            height={stickH}
            rx={stickW * 0.3}
            fill={color}
          />
        );
      })}
    </g>
  );
}

function WanFace({ rank, color, w, h }: { rank: number; color: string; w: number; h: number }) {
  const numFontSize = h * 0.32;
  const charFontSize = h * 0.28;
  return (
    <g>
      <text
        x={w / 2}
        y={h * 0.42}
        textAnchor="middle"
        fontSize={numFontSize}
        fontWeight={700}
        fill={color}
        fontFamily="serif"
      >
        {RANK_HANZI[rank]}
      </text>
      <text
        x={w / 2}
        y={h * 0.85}
        textAnchor="middle"
        fontSize={charFontSize}
        fontWeight={700}
        fill={color}
        fontFamily="serif"
      >
        萬
      </text>
    </g>
  );
}

export function TileFace({
  tile,
  size = 'md',
  faceDown,
  highlighted,
  onClick,
  disabled,
  title,
}: TileFaceProps) {
  const { w, h } = SIZE_DIMS[size];
  const baseFill = '#fffdf5';
  const backFill = '#5d6a78';
  const borderColor = highlighted ? '#f59e0b' : '#9ca3af';

  const interactive = Boolean(onClick) && !disabled;
  const computedTitle =
    title ??
    (tile ? `${tile.rank}${SUIT_LABEL[tile.suit]}` : faceDown ? 'face-down' : '');

  const content = (() => {
    if (faceDown || !tile) {
      return (
        <g>
          <rect x={0} y={0} width={w} height={h} rx={w * 0.12} fill={backFill} />
          <rect
            x={w * 0.15}
            y={h * 0.15}
            width={w * 0.7}
            height={h * 0.7}
            rx={w * 0.06}
            fill="none"
            stroke="#94a3b8"
            strokeWidth={1.5}
            strokeDasharray="3 3"
          />
        </g>
      );
    }
    const color = SUIT_COLOR[tile.suit];
    return (
      <g>
        <rect x={0} y={0} width={w} height={h} rx={w * 0.12} fill={baseFill} />
        {tile.suit === 'wan' && <WanFace rank={tile.rank} color={color} w={w} h={h} />}
        {tile.suit === 'tong' && <DotPips rank={tile.rank} color={color} w={w} h={h} />}
        {tile.suit === 'tiao' && <BambooSticks rank={tile.rank} color={color} w={w} h={h} />}
      </g>
    );
  })();

  const svg = (
    <svg
      width={w}
      height={h}
      viewBox={`0 0 ${w} ${h}`}
      style={{ display: 'block' }}
      role="img"
      aria-label={computedTitle}
    >
      {content}
      <rect
        x={0.5}
        y={0.5}
        width={w - 1}
        height={h - 1}
        rx={w * 0.12}
        fill="none"
        stroke={borderColor}
        strokeWidth={highlighted ? 3 : 1.25}
      />
      {highlighted && (
        <rect
          x={1.5}
          y={1.5}
          width={w - 3}
          height={h - 3}
          rx={w * 0.1}
          fill="none"
          stroke="#fbbf24"
          strokeWidth={1}
          opacity={0.6}
        />
      )}
    </svg>
  );

  if (!onClick) {
    return (
      <span
        title={computedTitle}
        className={highlighted ? 'changsha-tile changsha-tile-claim' : 'changsha-tile'}
        style={{ display: 'inline-block', lineHeight: 0 }}
      >
        {svg}
      </span>
    );
  }
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled}
      title={computedTitle}
      className={highlighted ? 'changsha-tile changsha-tile-claim' : 'changsha-tile'}
      style={{
        background: 'transparent',
        border: 'none',
        padding: 0,
        margin: 0,
        cursor: interactive ? 'pointer' : 'default',
        opacity: disabled ? 0.6 : 1,
        lineHeight: 0,
      }}
    >
      {svg}
    </button>
  );
}
