// ---------------------------------------------------------------------
// Phase K Wave 23 — Hicks (Frontend) — lobby player-chip + seat-preview
// renderers chunk.
//
// Extracted from `lobby.ts` so the chip-strip + seat-preview DOM
// builders + profile-aware resolvers ship as a lazy
// `lobby-player-chips.<hash>.js` chunk.  The lobby cold path no
// longer pays for the ~3 KB of chip / seat-preview / initials /
// hash-colour code at first paint; the module loads in the next
// idle window via the W23 `scheduleLobbyPlayerChipsLazyMount()`
// helper in `lobby.ts`.
//
// Behaviour: identical to the W22 implementation.  The W23 lazy
// gate is invisible to the chip strip — `bindLiveListeners` no-ops
// until the chunk lands, then re-paints on the next seats/nicks/
// profile tick.  Stale-frame skew is bounded by SignalR's
// SeatChanged / ProfileLoaded debounce (~250 ms cap).
// ---------------------------------------------------------------------

import type { Client } from './client';
import { setElHidden, showEl } from './dom-utils';
import { getProfile } from './profile';

export function renderPlayerChips(client: Client): void {
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
  setElHidden(strip, occupants.length === 0);
}

export function renderSeatPreview(client: Client): void {
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
  showEl(preview);
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

function resolveDisplayName(playerId: string, nick: string): string {
  const profile = getProfile();
  if (profile !== null && profile.playerId === playerId) {
    return profile.displayName;
  }
  if (nick !== '(no nick)' && nick !== '') return nick;
  return nick;
}

function resolveAvatarColor(playerId: string, _nick: string): string {
  const profile = getProfile();
  if (profile !== null && profile.playerId === playerId) {
    return profile.avatarColor;
  }
  return chipColorForPlayer(playerId);
}

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
  return !!nick && /^Bot\b/i.test(nick);
}
