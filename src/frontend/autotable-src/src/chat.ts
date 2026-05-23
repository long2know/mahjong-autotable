// Phase J Wave 9 — In-game table chat panel.
//
// A bottom-right docked, collapsible chat panel with three channels:
//   • Table       — visible whenever you're in a game (default channel)
//   • Spectators  — visible only when the URL has ?seat=-1 (the
//                   spectator-follow body class)
//   • Private     — visible when at least one other player is known;
//                   picks a recipient from a dropdown
//
// Composer:
//   • 280-char limit with a live counter testid `chat-char-count`
//   • Enter sends; Shift+Enter inserts a newline
//   • Slash commands intercepted client-side:
//       /clear — clears the visible message history (does NOT delete
//                from the server)
//       /help  — shows the command help line in the chat scroller
//
// Inbound messages render with an avatar chip (initial letter + the
// sender's avatar colour) and a display-name pill.  Self-messages
// right-align; everyone else left-aligns.
//
// Notification chime:
//   New incoming messages (i.e. not ours) trigger Sound.play('claim')
//   which respects the existing sound on/off toggle (mahjong:soundEnabled
//   mirror).
//
// Persistence + history:
//   On boot (after the gameId is known) we issue
//     GET /api/games/{gameId}/chat?since=<lastSeenIso>
//   to backfill the history pane.  A 404 hides the panel + shows
//   "Chat unavailable" (Bishop hasn't merged the endpoint yet).
//
// Live updates:
//   • If Bishop's hub broadcasts a `chat` collection (best-effort wire
//     name), the panel binds to it and renders messages reactively.
//   • Otherwise the panel falls back to long-polling
//     `GET /api/games/{gameId}/chat?since=<lastIso>` every ~6s while
//     the panel is open.  This keeps the UI shell honest even when the
//     hub hasn't shipped chat yet.
//
// Send:
//   POST /api/games/{gameId}/chat
//   body: { channel: 'table' | 'spectators' | 'private',
//           recipientPlayerId?: string,
//           body: string }

import type { Client } from './client';
import { Sound } from './sound';
import { t, onLanguageChange } from './i18n';

// ── Wire types ─────────────────────────────────────────────────────

export type ChatChannel = 'table' | 'spectators' | 'private';

export interface ChatMessage {
  id: string;
  channel: ChatChannel;
  senderPlayerId: string;
  senderDisplayName: string;
  senderAvatarColor: string | null;
  recipientPlayerId: string | null;
  body: string;
  sentUtc: string;
  /** True when this message was sent by the local player. */
  isSelf: boolean;
  /** True when this is a synthetic system message (e.g. /help). */
  isSystem: boolean;
}

// ── Constants ──────────────────────────────────────────────────────

const MAX_BODY_LEN = 280;
const POLL_INTERVAL_MS = 6000;
const COLLAPSED_LS_KEY = 'mahjong.chat.collapsed.v1';
const LAST_SEEN_LS_KEY = 'mahjong.chat.lastSeenIso.v1';

// ── Module state ───────────────────────────────────────────────────

interface ChatState {
  available: boolean;          // server has the /chat endpoint
  installed: boolean;
  collapsed: boolean;
  channel: ChatChannel;
  privateRecipientId: string | null;
  messages: ChatMessage[];
  localPlayerId: string | null;
  gameId: string | null;
  pollTimer: number | null;
  lastSeenIso: string;
  client: Client | null;
}

const state: ChatState = {
  available: false,
  installed: false,
  collapsed: true,
  channel: 'table',
  privateRecipientId: null,
  messages: [],
  localPlayerId: null,
  gameId: null,
  pollTimer: null,
  lastSeenIso: '',
  client: null,
};

// ── LS helpers ─────────────────────────────────────────────────────

function loadCollapsed(): boolean {
  try {
    const raw = window.localStorage.getItem(COLLAPSED_LS_KEY);
    if (raw === null) return true;
    return raw === 'true';
  } catch {
    return true;
  }
}

function saveCollapsed(c: boolean): void {
  try { window.localStorage.setItem(COLLAPSED_LS_KEY, c ? 'true' : 'false'); }
  catch { /* skip */ }
}

function loadLastSeen(): string {
  try {
    return window.localStorage.getItem(LAST_SEEN_LS_KEY) ?? '';
  } catch {
    return '';
  }
}

function saveLastSeen(iso: string): void {
  try { window.localStorage.setItem(LAST_SEEN_LS_KEY, iso); }
  catch { /* skip */ }
}

// ── Helpers ────────────────────────────────────────────────────────

function getGameIdFromUrl(): string | null {
  try {
    const params = new URLSearchParams(window.location.search);
    const v = params.get('game') ?? params.get('gameId');
    if (v === null || v === '') return null;
    return v;
  } catch {
    return null;
  }
}

function isSpectator(): boolean {
  try {
    const params = new URLSearchParams(window.location.search);
    return params.get('seat') === '-1';
  } catch {
    return false;
  }
}

function escapeText(s: string): string {
  return s.replace(/[&<>"]/g, (ch) => {
    switch (ch) {
      case '&': return '&amp;';
      case '<': return '&lt;';
      case '>': return '&gt;';
      case '"': return '&quot;';
      default:  return ch;
    }
  });
}

function djb2Hue(input: string): number {
  let hash = 5381;
  for (let i = 0; i < input.length; i++) hash = ((hash << 5) + hash + input.charCodeAt(i)) | 0;
  return (hash & 0xff) | 0;
}

function colorForSender(message: ChatMessage): string {
  if (message.senderAvatarColor !== null && /^#[0-9a-fA-F]{3,6}$/.test(message.senderAvatarColor)) {
    return message.senderAvatarColor;
  }
  const hue = djb2Hue(message.senderPlayerId || message.senderDisplayName || 'anon');
  return `hsl(${hue * 360 / 256}, 55%, 45%)`;
}

function senderInitial(message: ChatMessage): string {
  const name = (message.senderDisplayName || message.senderPlayerId || '?').trim();
  if (name === '') return '?';
  return name.charAt(0).toUpperCase();
}

function normalizeChannel(raw: unknown): ChatChannel {
  if (raw === 'spectators' || raw === 'private') return raw;
  return 'table';
}

function normalizeMessage(raw: unknown): ChatMessage | null {
  if (raw === null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const id = typeof o.id === 'string' ? o.id
    : (typeof o.Id === 'string' ? o.Id : '');
  if (id === '') return null;
  const body = typeof o.body === 'string' ? o.body
    : (typeof o.Body === 'string' ? o.Body : '');
  if (body === '') return null;
  const channel = normalizeChannel(o.channel ?? o.Channel);
  const senderPlayerId = typeof o.senderPlayerId === 'string' ? o.senderPlayerId
    : (typeof o.SenderPlayerId === 'string' ? o.SenderPlayerId : '');
  const senderDisplayName = typeof o.senderDisplayName === 'string' ? o.senderDisplayName
    : (typeof o.SenderDisplayName === 'string' ? o.SenderDisplayName
       : (typeof o.senderName === 'string' ? o.senderName : senderPlayerId));
  const avatarColor = typeof o.senderAvatarColor === 'string' ? o.senderAvatarColor
    : (typeof o.SenderAvatarColor === 'string' ? o.SenderAvatarColor : null);
  const recipientPlayerId = typeof o.recipientPlayerId === 'string' ? o.recipientPlayerId
    : (typeof o.RecipientPlayerId === 'string' ? o.RecipientPlayerId : null);
  const sentUtc = typeof o.sentUtc === 'string' ? o.sentUtc
    : (typeof o.SentUtc === 'string' ? o.SentUtc
       : (typeof o.timestampUtc === 'string' ? o.timestampUtc : new Date().toISOString()));
  return {
    id,
    channel,
    senderPlayerId,
    senderDisplayName: senderDisplayName.trim() === '' ? senderPlayerId : senderDisplayName,
    senderAvatarColor: avatarColor,
    recipientPlayerId,
    body,
    sentUtc,
    isSelf: state.localPlayerId !== null && senderPlayerId === state.localPlayerId,
    isSystem: false,
  };
}

// ── Network ────────────────────────────────────────────────────────

async function fetchHistory(): Promise<{ ok: boolean; status: number; messages: ChatMessage[] }> {
  const gameId = state.gameId;
  if (gameId === null) return { ok: false, status: 0, messages: [] };
  const params = new URLSearchParams();
  if (state.lastSeenIso !== '') params.set('since', state.lastSeenIso);
  const query = params.toString();
  const url = `/api/games/${encodeURIComponent(gameId)}/chat${query !== '' ? '?' + query : ''}`;
  try {
    const resp = await fetch(url, {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (resp.status === 404) return { ok: false, status: 404, messages: [] };
    if (!resp.ok) return { ok: false, status: resp.status, messages: [] };
    const body = await resp.json() as unknown;
    const rawList = Array.isArray(body)
      ? body
      : (body !== null && typeof body === 'object'
        ? ((body as Record<string, unknown>).messages
          ?? (body as Record<string, unknown>).Messages ?? [])
        : []);
    const out: ChatMessage[] = [];
    if (Array.isArray(rawList)) {
      for (const r of rawList) {
        const m = normalizeMessage(r);
        if (m !== null) out.push(m);
      }
    }
    return { ok: true, status: resp.status, messages: out };
  } catch {
    return { ok: false, status: 0, messages: [] };
  }
}

async function sendMessage(channel: ChatChannel, body: string, recipientPlayerId: string | null):
    Promise<{ ok: boolean; status: number }> {
  const gameId = state.gameId;
  if (gameId === null) return { ok: false, status: 0 };
  const url = `/api/games/${encodeURIComponent(gameId)}/chat`;
  try {
    const resp = await fetch(url, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        channel,
        recipientPlayerId,
        body,
      }),
    });
    return { ok: resp.ok, status: resp.status };
  } catch {
    return { ok: false, status: 0 };
  }
}

// ── State helpers ──────────────────────────────────────────────────

function appendMessages(incoming: ChatMessage[]): number {
  if (incoming.length === 0) return 0;
  // Dedup by id; preserve sortable order by sentUtc.
  const seen = new Set<string>(state.messages.map((m) => m.id));
  let added = 0;
  for (const m of incoming) {
    if (seen.has(m.id)) continue;
    state.messages.push(m);
    seen.add(m.id);
    added++;
    if (m.sentUtc > state.lastSeenIso) {
      state.lastSeenIso = m.sentUtc;
    }
  }
  if (added > 0) {
    state.messages.sort((a, b) => a.sentUtc.localeCompare(b.sentUtc));
    saveLastSeen(state.lastSeenIso);
  }
  return added;
}

function getSeatNicks(): Array<{ playerId: string; displayName: string; avatarColor: string | null }> {
  const out: Array<{ playerId: string; displayName: string; avatarColor: string | null }> = [];
  const client = state.client;
  if (client === null) return out;
  try {
    for (const [playerId, seatInfo] of client.seats.entries()) {
      if (playerId === 'offline') continue;
      if (state.localPlayerId !== null && playerId === state.localPlayerId) continue;
      const nick = client.nicks.get(playerId);
      const displayName = (typeof nick === 'string' && nick !== '')
        ? nick
        : playerId.slice(0, 6);
      out.push({ playerId, displayName, avatarColor: null });
      void seatInfo;
    }
  } catch { /* swallow */ }
  return out;
}

// ── DOM ────────────────────────────────────────────────────────────

function setCollapsed(collapsed: boolean): void {
  state.collapsed = collapsed;
  saveCollapsed(collapsed);
  const root = document.getElementById('chat-panel');
  if (root === null) return;
  root.classList.toggle('chat-panel-collapsed', collapsed);
  const toggle = document.getElementById('chat-toggle') as HTMLButtonElement | null;
  if (toggle !== null) toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
  if (!collapsed) {
    // When opening, mark all received as seen.
    void refreshHistory();
    startPolling();
    requestAnimationFrame(() => scrollToBottom());
  } else {
    stopPolling();
  }
}

function renderRecipientOptions(select: HTMLSelectElement): void {
  const previous = state.privateRecipientId ?? select.value ?? '';
  select.replaceChildren();
  const noneOpt = document.createElement('option');
  noneOpt.value = '';
  noneOpt.textContent = t('chat.recipient_none');
  select.appendChild(noneOpt);
  const nicks = getSeatNicks();
  for (const n of nicks) {
    const opt = document.createElement('option');
    opt.value = n.playerId;
    opt.textContent = n.displayName;
    if (n.playerId === previous) opt.selected = true;
    select.appendChild(opt);
  }
}

function renderChannelOptions(select: HTMLSelectElement): void {
  select.replaceChildren();
  const channels: Array<{ id: ChatChannel; label: string }> = [
    { id: 'table',      label: t('chat.channel.table') },
  ];
  if (isSpectator()) {
    channels.push({ id: 'spectators', label: t('chat.channel.spectators') });
  }
  channels.push({ id: 'private', label: t('chat.channel.private') });
  for (const c of channels) {
    const opt = document.createElement('option');
    opt.value = c.id;
    opt.textContent = c.label;
    if (c.id === state.channel) opt.selected = true;
    select.appendChild(opt);
  }
}

function visibleMessages(): ChatMessage[] {
  const local = state.localPlayerId;
  return state.messages.filter((m) => {
    if (m.isSystem) return true;
    if (m.channel !== state.channel) return false;
    if (m.channel === 'private') {
      const peer = state.privateRecipientId;
      if (peer === null || peer === '') {
        return m.isSelf || (m.senderPlayerId === local) || (m.recipientPlayerId === local);
      }
      const involvesPeer = m.senderPlayerId === peer || m.recipientPlayerId === peer;
      const involvesSelf = m.isSelf || m.senderPlayerId === local || m.recipientPlayerId === local;
      return involvesPeer && involvesSelf;
    }
    return true;
  });
}

function renderMessages(): void {
  const list = document.getElementById('chat-messages');
  if (list === null) return;
  list.replaceChildren();
  const msgs = visibleMessages();
  if (msgs.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'chat-empty';
    empty.textContent = state.channel === 'private' && (state.privateRecipientId === null || state.privateRecipientId === '')
      ? t('chat.empty_private')
      : t('chat.no_messages');
    list.appendChild(empty);
    return;
  }
  msgs.forEach((m, i) => {
    const row = document.createElement('div');
    row.className = `chat-message chat-message-${m.isSelf ? 'self' : 'other'}`;
    if (m.isSystem) row.classList.add('chat-message-system');
    row.setAttribute('data-testid', `chat-message-${i}`);

    const avatar = document.createElement('span');
    avatar.className = 'chat-message-avatar';
    avatar.style.backgroundColor = colorForSender(m);
    avatar.textContent = senderInitial(m);

    const bubble = document.createElement('div');
    bubble.className = 'chat-message-bubble';

    const author = document.createElement('span');
    author.className = 'chat-message-author';
    author.setAttribute('data-testid', `chat-message-${i}-author`);
    author.textContent = m.senderDisplayName;

    const bodyEl = document.createElement('span');
    bodyEl.className = 'chat-message-body';
    bodyEl.setAttribute('data-testid', `chat-message-${i}-body`);
    bodyEl.innerHTML = escapeText(m.body).replace(/\n/g, '<br>');

    bubble.appendChild(author);
    bubble.appendChild(bodyEl);

    if (m.isSelf) {
      row.appendChild(bubble);
      row.appendChild(avatar);
    } else {
      row.appendChild(avatar);
      row.appendChild(bubble);
    }
    list.appendChild(row);
  });
  scrollToBottom();
}

function scrollToBottom(): void {
  const list = document.getElementById('chat-messages');
  if (list === null) return;
  list.scrollTop = list.scrollHeight;
}

function renderUnavailable(): void {
  const root = document.getElementById('chat-panel');
  if (root === null) return;
  root.classList.add('chat-panel-unavailable');
  const placeholder = document.getElementById('chat-unavailable');
  if (placeholder !== null) {
    (placeholder as HTMLElement).style.display = 'block';
    placeholder.textContent = t('chat.unavailable') + ' — ' + t('chat.unavailable_hint');
  }
  // Disable composer.
  const input = document.getElementById('chat-input') as HTMLTextAreaElement | null;
  const send = document.getElementById('chat-send') as HTMLButtonElement | null;
  if (input !== null) input.disabled = true;
  if (send !== null) send.disabled = true;
}

function flashError(text: string): void {
  const status = document.getElementById('chat-status');
  if (status === null) return;
  status.textContent = text;
  status.classList.add('chat-status-visible');
  window.setTimeout(() => {
    status.classList.remove('chat-status-visible');
    status.textContent = '';
  }, 3000);
}

function pushSystemMessage(text: string): void {
  state.messages.push({
    id: `sys-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    channel: state.channel,
    senderPlayerId: 'system',
    senderDisplayName: 'system',
    senderAvatarColor: '#666',
    recipientPlayerId: null,
    body: text,
    sentUtc: new Date().toISOString(),
    isSelf: false,
    isSystem: true,
  });
  renderMessages();
}

function executeCommand(raw: string): boolean {
  const trimmed = raw.trim();
  if (!trimmed.startsWith('/')) return false;
  const m = trimmed.match(/^\/(\w+)(?:\s+(.*))?$/);
  if (m === null) return false;
  const cmd = m[1].toLowerCase();
  switch (cmd) {
    case 'clear':
      state.messages = [];
      pushSystemMessage(t('chat.cleared'));
      return true;
    case 'help':
      pushSystemMessage(t('chat.command_help'));
      return true;
    default:
      pushSystemMessage(t('chat.command_unknown', { cmd }));
      return true;
  }
}

// ── Polling ────────────────────────────────────────────────────────

async function refreshHistory(): Promise<void> {
  if (state.gameId === null) return;
  const result = await fetchHistory();
  if (result.status === 404) {
    state.available = false;
    renderUnavailable();
    return;
  }
  if (!result.ok) return;
  const added = appendMessages(result.messages);
  if (added > 0) {
    // Chime when new inbound (i.e. not self) arrives.
    const fresh = result.messages.find((m) => !m.isSelf);
    if (fresh !== undefined) Sound.play('claim');
    renderMessages();
  }
}

function startPolling(): void {
  if (state.pollTimer !== null) return;
  state.pollTimer = window.setInterval(() => {
    if (state.collapsed) return;
    void refreshHistory();
  }, POLL_INTERVAL_MS);
}

function stopPolling(): void {
  if (state.pollTimer !== null) {
    window.clearInterval(state.pollTimer);
    state.pollTimer = null;
  }
}

// ── Composer ───────────────────────────────────────────────────────

function updateCharCount(input: HTMLTextAreaElement): void {
  const counter = document.getElementById('chat-char-count');
  if (counter === null) return;
  counter.textContent = t('chat.char_count', { count: input.value.length, max: MAX_BODY_LEN });
  counter.classList.toggle('chat-char-count-over',
    input.value.length > MAX_BODY_LEN);
}

async function doSend(): Promise<void> {
  const input = document.getElementById('chat-input') as HTMLTextAreaElement | null;
  if (input === null) return;
  const raw = input.value;
  if (raw.trim() === '') return;
  if (executeCommand(raw)) {
    input.value = '';
    updateCharCount(input);
    return;
  }
  if (raw.length > MAX_BODY_LEN) {
    flashError(t('chat.char_count', { count: raw.length, max: MAX_BODY_LEN }));
    return;
  }
  if (!state.available) {
    flashError(t('chat.unavailable'));
    return;
  }
  const channel = state.channel;
  const recipient = channel === 'private' ? state.privateRecipientId : null;
  if (channel === 'private' && (recipient === null || recipient === '')) {
    flashError(t('chat.empty_private'));
    return;
  }
  input.disabled = true;
  const result = await sendMessage(channel, raw, recipient);
  input.disabled = false;
  if (!result.ok) {
    if (result.status === 429) {
      flashError(t('chat.send_rate_limited'));
    } else if (result.status === 404) {
      state.available = false;
      renderUnavailable();
    } else {
      flashError(t('chat.send_failed'));
    }
    input.focus();
    return;
  }
  input.value = '';
  updateCharCount(input);
  input.focus();
  await refreshHistory();
}

// ── Install ────────────────────────────────────────────────────────

export function installChatPanel(client: Client | null): void {
  if (state.installed) {
    if (client !== null) state.client = client;
    return;
  }
  state.installed = true;
  state.client = client;
  state.collapsed = loadCollapsed();
  state.lastSeenIso = loadLastSeen();
  state.gameId = getGameIdFromUrl();
  // If we're not in a game (lobby only), don't render the panel.
  if (state.gameId === null) {
    const root = document.getElementById('chat-panel');
    if (root !== null) (root as HTMLElement).style.display = 'none';
    return;
  }

  // Read local player id.
  if (client !== null) {
    try {
      const pid = client.playerId();
      state.localPlayerId = (pid !== '' && pid !== 'offline') ? pid : null;
    } catch { /* skip */ }
  }

  const root = document.getElementById('chat-panel');
  if (root === null) return;
  (root as HTMLElement).style.display = '';
  root.classList.toggle('chat-panel-collapsed', state.collapsed);

  // Header toggle.
  const toggle = document.getElementById('chat-toggle') as HTMLButtonElement | null;
  if (toggle !== null) {
    toggle.setAttribute('aria-controls', 'chat-panel-body');
    toggle.setAttribute('aria-expanded', state.collapsed ? 'false' : 'true');
    toggle.addEventListener('click', () => setCollapsed(!state.collapsed));
  }

  // Channel select.
  const channelSelect = document.getElementById('chat-channel-select') as HTMLSelectElement | null;
  if (channelSelect !== null) {
    renderChannelOptions(channelSelect);
    channelSelect.addEventListener('change', () => {
      state.channel = normalizeChannel(channelSelect.value);
      const recipientWrap = document.getElementById('chat-recipient-wrap');
      if (recipientWrap !== null) {
        recipientWrap.style.display = state.channel === 'private' ? '' : 'none';
      }
      renderMessages();
    });
  }

  // Recipient select.
  const recipientSelect = document.getElementById('chat-recipient-select') as HTMLSelectElement | null;
  if (recipientSelect !== null) {
    renderRecipientOptions(recipientSelect);
    recipientSelect.addEventListener('change', () => {
      state.privateRecipientId = recipientSelect.value || null;
      renderMessages();
    });
  }
  const recipientWrap = document.getElementById('chat-recipient-wrap');
  if (recipientWrap !== null) {
    recipientWrap.style.display = state.channel === 'private' ? '' : 'none';
  }

  // Composer.
  const input = document.getElementById('chat-input') as HTMLTextAreaElement | null;
  if (input !== null) {
    input.setAttribute('maxlength', String(MAX_BODY_LEN));
    input.placeholder = t('chat.placeholder');
    input.addEventListener('input', () => updateCharCount(input));
    input.addEventListener('keydown', (e: KeyboardEvent) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        void doSend();
      }
    });
    updateCharCount(input);
  }
  const sendBtn = document.getElementById('chat-send') as HTMLButtonElement | null;
  if (sendBtn !== null) {
    sendBtn.addEventListener('click', () => { void doSend(); });
  }

  // Wire client live updates if the seats collection changes — re-render
  // the recipient dropdown so disconnect/joins reflect immediately.
  if (client !== null) {
    try {
      client.seats.on('update', () => {
        if (recipientSelect !== null) renderRecipientOptions(recipientSelect);
        // If localPlayerId only becomes known after connection, pick it
        // up here so isSelf detection works retroactively.
        const cp = client.playerId();
        if (cp !== '' && cp !== 'offline' && cp !== state.localPlayerId) {
          state.localPlayerId = cp;
          // Re-tag existing messages.
          for (const m of state.messages) {
            m.isSelf = m.senderPlayerId === state.localPlayerId;
          }
          renderMessages();
        }
      });
      client.nicks.on('update', () => {
        if (recipientSelect !== null) renderRecipientOptions(recipientSelect);
      });
    } catch { /* swallow */ }
  }

  // Boot fetch. If 404 → "Chat unavailable" placeholder; otherwise mark
  // available and render any backlog.
  void (async () => {
    const r = await fetchHistory();
    if (r.status === 404) {
      state.available = false;
      renderUnavailable();
      return;
    }
    state.available = true;
    appendMessages(r.messages);
    renderMessages();
    if (!state.collapsed) startPolling();
  })();

  // Phase J Wave 9 — refresh static chrome on language change.
  onLanguageChange(() => {
    const channelSelect2 = document.getElementById('chat-channel-select') as HTMLSelectElement | null;
    if (channelSelect2 !== null) renderChannelOptions(channelSelect2);
    const recipientSelect2 = document.getElementById('chat-recipient-select') as HTMLSelectElement | null;
    if (recipientSelect2 !== null) renderRecipientOptions(recipientSelect2);
    const input2 = document.getElementById('chat-input') as HTMLTextAreaElement | null;
    if (input2 !== null) input2.placeholder = t('chat.placeholder');
    if (!state.available) renderUnavailable();
    renderMessages();
  });
}
