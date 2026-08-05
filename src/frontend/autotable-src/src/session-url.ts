// #153 (Ferro) — Default-game URL helpers.
//
// A bare / default navigation (e.g. `/autotable/` or `/autotable/?variant=
// changsha`) carries no concrete `?gameId=`.  Before this module the connect
// path fell back to the shared legacy sentinel `changsha-default`, so every
// fresh visitor JOINed one long-lived room and inherited its stale
// seat/turn/deal state (Hudson's #153 diagnosis: "restart → Take Seat →
// nothing progresses").  It also omitted `dealMode` / `botCount` from the
// handshake, so the runtime silently ran the Manual ceremony while the lobby
// UI implied Auto.
//
// This helper mints a fresh, non-colliding game id and produces an explicit,
// honest "New Game" URL that carries the same defaults the lobby's Apply &
// Start emits (variant / dealMode / botCount / botDifficulty / handCount).
// The connect path redirects a default navigation through this URL so the
// whole page (renderer config display AND the WS handshake) reads one
// consistent, authoritative source of truth.
//
// A URL that already carries a concrete `?gameId=` is a deliberate
// reload / reconnect / shared-room join and is left untouched (creator-wins
// semantics + reconnect are preserved).

// The legacy shared sentinel.  A default navigation must never silently
// resolve to this; only an explicit user-typed / deep-linked gameId may.
export const DEFAULT_GAME_ID = 'changsha-default';

// New-game defaults — kept in lock-step with lobby.ts DEFAULTS so the bare
// Connect path and the lobby's Apply & Start land on identical config.
export const NEW_GAME_DEFAULTS = {
  variant: 'changsha',
  dealMode: 'auto',
  botCount: 3,
  botDifficulty: 'Hard',
  handCount: 4,
} as const;

// Mint a fresh `changsha-<8 hex>` game id.  Prefers crypto.randomUUID, then
// crypto.getRandomValues, then Math.random — never throws on legacy / non-
// secure contexts.  Mirrors lobby.ts mintFreshGameId so both entry points
// produce the same id shape.
export function mintFreshGameId(): string {
  let hex = '';
  try {
    const c = (window as { crypto?: Crypto }).crypto;
    if (c !== undefined && typeof c.randomUUID === 'function') {
      hex = c.randomUUID().replace(/-/g, '').slice(0, 8);
    } else if (c !== undefined && typeof c.getRandomValues === 'function') {
      const buf = new Uint8Array(4);
      c.getRandomValues(buf);
      hex = Array.from(buf, b => b.toString(16).padStart(2, '0')).join('');
    }
  } catch { /* fall through to Math.random fallback */ }
  if (hex === '') {
    hex = Math.floor(Math.random() * 0x100000000).toString(16).padStart(8, '0');
  }
  return `changsha-${hex}`;
}

// Read a concrete, non-empty gameId from a query string.  Returns null when
// the param is absent or blank.  The legacy `changsha-default` sentinel only
// counts as "concrete" when it is literally present in the URL (an explicit
// join), never as an implicit fallback.
export function readConcreteGameId(search: string): string | null {
  const raw = new URLSearchParams(search).get('gameId');
  if (raw === null) return null;
  const trimmed = raw.trim();
  return trimmed === '' ? null : trimmed;
}

// True when the current navigation already targets a concrete game (deliberate
// reload / reconnect / shared-room join).
export function hasConcreteGameId(search: string): boolean {
  return readConcreteGameId(search) !== null;
}

// Build an explicit, honest "New Game" URL for a default navigation: mint a
// fresh gameId and fill in any config params the current URL is missing with
// the shared New Game defaults.  Params the user already set on the URL are
// preserved verbatim so a partial deep-link (e.g. `?dealMode=manual`) keeps
// the user's intent while still gaining a fresh, isolated gameId.
export function buildFreshGameUrl(
  pathname: string,
  search: string,
  gameId: string = mintFreshGameId(),
): string {
  const p = new URLSearchParams(search);
  p.set('gameId', gameId);
  if (!p.has('variant')) p.set('variant', NEW_GAME_DEFAULTS.variant);
  // dealMode is Changsha-only; only stamp it for the Changsha variant so
  // Riichi deep-links stay tidy (they ignore dealMode server-side).
  if (p.get('variant') === 'changsha' && !p.has('dealMode')) {
    p.set('dealMode', NEW_GAME_DEFAULTS.dealMode);
  }
  if (!p.has('botCount')) p.set('botCount', String(NEW_GAME_DEFAULTS.botCount));
  // botDifficulty is meaningless with zero bots — only stamp it when bots
  // will actually fill seats.
  const botCount = Number(p.get('botCount'));
  if (botCount > 0 && !p.has('botDifficulty')) {
    p.set('botDifficulty', NEW_GAME_DEFAULTS.botDifficulty);
  }
  if (!p.has('handCount')) p.set('handCount', String(NEW_GAME_DEFAULTS.handCount));
  return `${pathname}?${p.toString()}`;
}
