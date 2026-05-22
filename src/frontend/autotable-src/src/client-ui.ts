import { Client } from "./client";
import { Game } from './base-client';


const TITLE_DISCONNECTED = 'Autotable';
const TITLE_CONNECTED = 'Autotable (online)';
const RECONNECT_DELAY = 2000;
const RECONNECT_ATTEMPTS = 15;

// Phase I Wave 3 — Default game routing key.  Mirrors
// AutotableWsEndpoint.DefaultGameId on the backend; used when the URL
// carries no ?gameId= and the user hasn't typed anything into the lobby
// input, so a bare URL keeps the legacy single-game behaviour.
const DEFAULT_GAME_ID = 'changsha-default';

// Phase I Wave 3 — Game ID validation.  Must match the HTML pattern attribute
// on #lobby-gameId so HTML5 form validation and the JS gate agree.  ≤64 chars
// matches Bishop's expected backend cap.
const GAME_ID_MAX_LENGTH = 64;
const GAME_ID_PATTERN = /^[A-Za-z0-9_\-\.]+$/;

// Phase I Wave 4 — sentinel seat value the backend (post-Bishop's cap
// relax) interprets as "spectator: no seat assigned".  Anything in
// 0..3 is a normal seat take; anything else is rejected by the server
// and falls back to the legacy "no preference" flow on our side.
const SPECTATOR_SEAT = -1;

interface GameIdValidation {
  value: string | null;
  error: string | null;
}

function validateGameId(raw: string): GameIdValidation {
  const trimmed = raw.trim();
  if (trimmed === '') {
    return { value: null, error: 'Game ID cannot be empty.' };
  }
  if (trimmed.length > GAME_ID_MAX_LENGTH) {
    return {
      value: null,
      error: `Game ID must be ${GAME_ID_MAX_LENGTH} characters or fewer.`,
    };
  }
  if (!GAME_ID_PATTERN.test(trimmed)) {
    return {
      value: null,
      error: 'Game ID may only contain letters, digits, _, -, and .',
    };
  }
  return { value: trimmed, error: null };
}

// Phase I Wave 4 — exported so game-ui.ts can short-circuit the
// take-seat affordances based on the same URL flag we use to drive the
// body class and the Spectating pill.
export function readSpectatorFromUrl(): boolean {
  const q = new URLSearchParams(window.location.search);
  const raw = q.get('seat');
  if (raw === null) return false;
  const n = parseInt(raw, 10);
  return !isNaN(n) && n === SPECTATOR_SEAT;
}

export class ClientUi {
  url: string;
  client: Client;
  nickElement: HTMLInputElement;
  statusElement: HTMLElement;
  statusTextElement: HTMLElement;
  gameIdInput: HTMLInputElement | null;
  gameIdError: HTMLElement | null;
  currentGameIdElement: HTMLElement | null;
  spectatorPillElement: HTMLElement | null;

  disconnecting = false;
  reconnectAttempts: number = 0;
  reconnectSeat: number | null = null;
  // Phase I Wave 4 — true while the active page URL declares ?seat=-1.
  // Spectator state is page-URL-driven (set on init, refreshed on each
  // connect attempt) so a refresh that lands on the same URL re-joins as
  // spectator without surfacing the take-seat affordances even for the
  // brief moment between page load and WS JOINED.
  spectating: boolean = false;

  constructor(client: Client) {
    this.url = this.getUrl();
    this.client = client;

    this.nickElement = document.getElementById('nick')! as HTMLInputElement;
    this.nickElement.value = localStorage.getItem('autotable.nick') ?? '';

    this.nickElement.onchange = this.onNickChange.bind(this);
    this.nickElement.oninput = this.onNickChange.bind(this);

    this.client.on('connect', this.onConnect.bind(this));
    this.client.on('disconnect', this.onDisconnect.bind(this));
    this.onNickChange();

    const connectButton = document.getElementById('connect')!;
    connectButton.onclick = () => this.connect();
    const disconnectButton = document.getElementById('disconnect')!;
    disconnectButton.onclick = this.disconnect.bind(this);
    const newGameButton = document.getElementById('new-game')!;
    newGameButton.onclick = this.newGame.bind(this);

    this.statusElement = document.getElementById('status') as HTMLElement;
    this.statusTextElement = document.getElementById('status-text') as HTMLElement;

    // Phase I Wave 3 — wire the lobby Game ID input.  Prefill from
    // ?gameId= if present + valid, otherwise the backend default.  Clear
    // any stale error message as the user edits, and re-validate on blur
    // so the red error doesn't linger after the user fixes it.
    this.gameIdInput =
      document.getElementById('lobby-gameId') as HTMLInputElement | null;
    this.gameIdError = document.getElementById('lobby-gameId-error');
    this.currentGameIdElement = document.getElementById('current-game-id');
    this.spectatorPillElement = document.getElementById('spectator-pill');
    if (this.gameIdInput !== null) {
      this.gameIdInput.value = this.readInitialGameId();
      this.gameIdInput.addEventListener('input', () => this.clearGameIdError());
      this.gameIdInput.addEventListener('blur', () => this.refreshGameIdValidity());
      this.gameIdInput.addEventListener('keydown', (event: KeyboardEvent) => {
        if (event.key === 'Enter') {
          event.preventDefault();
          this.connect();
        }
      });
    }

    // Phase I Wave 4 — propagate the page URL's spectator flag onto the
    // body class + pill element BEFORE the first connect.  This keeps
    // the take-seat affordances hidden from the moment the page loads
    // (game-ui.ts updateSeats reads spectatorModeFromUrl()) and ensures
    // the pill is the right initial state on auto-reconnect.
    this.spectating = readSpectatorFromUrl();
    this.applySpectatorClass();
  }

  // Phase I Wave 3 — Read ?gameId= from the URL.  Falls back to the
  // backend default ("changsha-default") when the param is missing or
  // fails validation, so a hand-typed URL with garbage doesn't poison
  // the input prefill.
  private readInitialGameId(): string {
    const q = new URLSearchParams(window.location.search);
    const raw = q.get('gameId');
    if (raw === null) return DEFAULT_GAME_ID;
    const { value } = validateGameId(raw);
    return value ?? DEFAULT_GAME_ID;
  }

  private clearGameIdError(): void {
    if (this.gameIdInput !== null) {
      this.gameIdInput.classList.remove('lobby-error-input');
    }
    if (this.gameIdError !== null) {
      this.gameIdError.style.display = 'none';
    }
  }

  private showGameIdError(message: string): void {
    if (this.gameIdInput !== null) {
      this.gameIdInput.classList.add('lobby-error-input');
    }
    if (this.gameIdError !== null) {
      this.gameIdError.textContent = message;
      this.gameIdError.style.display = 'block';
    }
  }

  private refreshGameIdValidity(): boolean {
    if (this.gameIdInput === null) return true;
    const { value, error } = validateGameId(this.gameIdInput.value);
    if (error !== null) {
      this.showGameIdError(error);
      return false;
    }
    this.clearGameIdError();
    // Normalize the field to the trimmed value so subsequent reads are stable.
    if (value !== null && this.gameIdInput.value !== value) {
      this.gameIdInput.value = value;
    }
    return true;
  }

  // Phase I Wave 3 — Resolve the gameId to use for this connect attempt.
  // Priority: live input value (if present + valid) > URL ?gameId= > default.
  // Returns null when the input is present but invalid so the caller can
  // surface the inline error and abort.
  private resolveGameIdForConnect(): string | null {
    if (this.gameIdInput !== null) {
      const { value, error } = validateGameId(this.gameIdInput.value);
      if (error !== null) {
        this.showGameIdError(error);
        this.gameIdInput.focus();
        return null;
      }
      if (value !== null) {
        this.clearGameIdError();
        if (this.gameIdInput.value !== value) {
          this.gameIdInput.value = value;
        }
        return value;
      }
    }
    // No input element on the page (legacy callers) — fall back to URL > default.
    return this.readInitialGameId();
  }

  // Phase I Wave 4 — Build the WS connection URL with ?gameId= appended,
  // plus the spectator/seat/botCount query params parsed off the page
  // URL.  Bishop's WS endpoint reads these from
  // context.Request.Query at connect time (AutotableWsEndpoint.cs:174 for
  // seat, :192 for botCount), so they have to ride the WS URL — the page
  // URL alone doesn't reach the server.
  //
  // We forward both seat and botCount so:
  //   • Spectator (?seat=-1) actually reaches the backend as a "no seat"
  //     connection — the seat-take auto-fill is suppressed and the runtime
  //     auto-deals when bots fill the remaining seats.
  //   • The lobby's chosen botCount actually drives the backend's
  //     auto-bot-fill on this connection (previously the WS URL only
  //     carried gameId, so botCount was forever the server default).
  private buildWsUrl(gameId: string): string {
    const params = new URLSearchParams();
    params.set('gameId', gameId);
    const pageQuery = new URLSearchParams(window.location.search);
    const seatRaw = pageQuery.get('seat');
    if (seatRaw !== null) {
      const seatNum = parseInt(seatRaw, 10);
      if (!isNaN(seatNum) && (seatNum === SPECTATOR_SEAT || (seatNum >= 0 && seatNum <= 3))) {
        params.set('seat', String(seatNum));
      }
    }
    const botCountRaw = pageQuery.get('botCount');
    if (botCountRaw !== null) {
      const bc = parseInt(botCountRaw, 10);
      if (!isNaN(bc) && bc >= 0 && bc <= 4) {
        params.set('botCount', String(bc));
      }
    }
    const separator = this.url.indexOf('?') >= 0 ? '&' : '?';
    return `${this.url}${separator}${params.toString()}`;
  }

  getUrlState(): string | null {
    const q = new URLSearchParams(window.location.search);
    return q.get('gameId');
  }

  setUrlState(gameId: string | null): void {
    const query = window.location.search;
    const q = new URLSearchParams(query);
    if (gameId !== null) {
      q.set('gameId', gameId);
    } else {
      q.delete('gameId');
    }
    const newQuery = q.toString();
    if (newQuery !== query.substring(1)) {
      // Phase I Wave 3 — replaceState (not pushState) so refresh re-joins
      // the same game without polluting browser history with every
      // connect attempt.  The previous pushState pre-Wave-3 was wrong
      // for the same reason but rarely triggered (only the server-issued
      // gameId hit this path).
      history.replaceState(undefined, '', newQuery ? '?' + newQuery : "");
    }
  }

  start(): void {
    if (this.getUrlState() !== null) {
      // If connecting right on page load, start from empty seat
      // (to prevent sudden change)
      this.client.seats.set(this.client.playerId(), { seat: null });

      this.connect();
    }
  }

  getUrl(): string {
    // @ts-ignore
    const env = process.env.NODE_ENV;

    if (env !== 'production') {
      return 'ws://localhost:1235';
    }

    let path = window.location.pathname;
    path = path.substring(1, path.lastIndexOf('/')+1);
    const wsProtocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    const wsHost = window.location.host;
    const wsPath = path + 'ws';
    return `${wsProtocol}//${wsHost}/${wsPath}`;
  }

  onNickChange(): void {
    if (this.client.connected()) {
      this.client.nicks.set(this.client.playerId(), this.nickElement.value);
    }
    localStorage.setItem('autotable.nick', this.nickElement.value);
  }

  onConnect(game: Game): void {
    this.setStatus(null);
    document.getElementById('server')!.classList.add('connected');
    // Phase I Wave 3 — mirror the .connected toggle onto the lobby Game
    // ID row so its .server-disconnected / .server-connected children
    // swap visibility (the existing #server descendant selectors at
    // style.css:87-88 don't reach the row, which is a sibling).
    document.getElementById('lobby-gameId-row')?.classList.add('connected');
    this.onNickChange();
    this.setUrlState(game.gameId);
    document.getElementsByTagName('title')[0].innerText = TITLE_CONNECTED;

    // Phase I Wave 3 — surface the active game id in the sidebar so
    // the user always knows which game they're in.  The element is
    // gated visible via the .connected class toggle above.
    if (this.currentGameIdElement !== null) {
      this.currentGameIdElement.textContent = game.gameId;
    }
    // Clear any stale validation error that might still be visible
    // (defensive: connect normally blocks on an invalid input, but if
    // the field changed between attempts we don't want a red border
    // lingering after a successful join).
    this.clearGameIdError();

    // Phase I Wave 4 — re-evaluate spectator mode against the post-connect
    // URL (it can change between attempts when the lobby's Apply &
    // Start lands on a fresh URL, or the user edits ?seat= by hand).
    this.spectating = readSpectatorFromUrl();
    this.applySpectatorClass();

    // Phase I Wave 4 — spectators never reconnect into a seat.  The
    // reconnectSeat is captured pre-disconnect; for spectators that
    // capture is always null (no seat was taken), but we belt-and-brace
    // it here so a stray reconnectSeat from a prior seated session
    // doesn't accidentally seat the spectator.
    if (this.spectating) {
      this.reconnectSeat = null;
    } else if (this.reconnectSeat !== null) {
      this.client.seats.set(this.client.playerId(), { seat: this.reconnectSeat });
    }
  }

  onDisconnect(game: Game | null): void {
    document.getElementById('server')!.classList.remove('connected');
    document.getElementById('lobby-gameId-row')?.classList.remove('connected');
    document.getElementsByTagName('title')[0].innerText = TITLE_DISCONNECTED;

    if (game && !this.disconnecting) {
      this.reconnectSeat = this.client.seat;
      setTimeout(
        () => this.connect(RECONNECT_ATTEMPTS, this.client.seat ?? undefined),
        RECONNECT_DELAY
      );
      this.setStatus('Trying to reconnect...');
    } else if (!game && this.reconnectAttempts > 0) {
      setTimeout(
        () => this.connect(this.reconnectAttempts - 1, this.reconnectSeat ?? undefined),
        RECONNECT_DELAY);
    } else {
      (document.getElementById('connect')! as HTMLButtonElement).disabled = false;
      if (!this.disconnecting) {
        this.setStatus('Failed to connect.');
      }
    }
  }

  // Phase I Wave 4 — mirror the URL-derived spectator state onto the body
  // class + pill element.  Centralised so we can call it from the ctor
  // (before first connect), onConnect (after a URL transition), and
  // disconnect (so the body class stays in sync with the URL).
  private applySpectatorClass(): void {
    document.body.classList.toggle('spectating', this.spectating);
    if (this.spectatorPillElement !== null) {
      this.spectatorPillElement.classList.toggle('visible', this.spectating);
    }
  }

  setStatus(status: string | null): void {
    if (status !== null) {
      this.statusElement.style.display = 'block';
      this.statusTextElement.innerText = status;
    } else {
      this.statusElement.style.display = 'none';
    }
  }

  connect(reconnectAttempts?: number, reconnectSeat?: number): void {
    if (this.client.connected()) {
      return;
    }

    // Phase I Wave 3 — resolve the gameId from the live input (or URL /
    // default fallback), validate, and bake it into both the WS URL
    // (where Bishop's lifted AutotableWsEndpoint parses it as the
    // routing key) and the page URL (so refresh re-joins the same
    // game).  Validation failures show inline and abort the connect.
    const gameId = this.resolveGameIdForConnect();
    if (gameId === null) {
      return;
    }
    this.setUrlState(gameId);

    // Phase I Wave 4 — re-evaluate spectator mode every connect attempt
    // (the URL can shift between attempts when Apply & Start lands on a
    // fresh URL, or the user hand-edits ?seat=).
    this.spectating = readSpectatorFromUrl();
    this.applySpectatorClass();

    (document.getElementById('connect')! as HTMLButtonElement).disabled = true;
    this.reconnectSeat = null;
    const wsUrl = this.buildWsUrl(gameId);
    const existing = this.getUrlState();
    if (existing !== null) {
      this.client.join(wsUrl, gameId);
    } else {
      this.client.new(wsUrl);
    }
    this.reconnectAttempts = reconnectAttempts ?? 0;
    this.reconnectSeat = reconnectSeat ?? null;
  }

  disconnect(): void {
    this.disconnecting = true;
    this.client.disconnect();
    // this.setUrlState(null);
  }

  newGame(): void {
    window.location.search = '';
  }
}
