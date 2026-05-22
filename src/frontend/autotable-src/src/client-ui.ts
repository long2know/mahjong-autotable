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

export class ClientUi {
  url: string;
  client: Client;
  nickElement: HTMLInputElement;
  statusElement: HTMLElement;
  statusTextElement: HTMLElement;
  gameIdInput: HTMLInputElement | null;
  gameIdError: HTMLElement | null;
  currentGameIdElement: HTMLElement | null;

  disconnecting = false;
  reconnectAttempts: number = 0;
  reconnectSeat: number | null = null;

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

  // Phase I Wave 3 — Build the WS connection URL with ?gameId= appended.
  // Preserves any other query params already on the base URL (currently
  // none — getUrl() returns a bare path — but defensive against future
  // additions).
  private buildWsUrl(gameId: string): string {
    const separator = this.url.indexOf('?') >= 0 ? '&' : '?';
    return `${this.url}${separator}gameId=${encodeURIComponent(gameId)}`;
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

    if (this.reconnectSeat !== null) {
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
