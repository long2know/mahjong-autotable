// Lobby UI for game creation and joining

const API_BASE = import.meta.env.VITE_API_BASE || 'http://localhost:5270';

export interface CreateGameRequest {
  hostName?: string;
  mode?: string;
  botSeatCount?: number;
  ruleset?: string;
}

export interface CreateGameResponse {
  id: string;
  status: string;
  round: number;
  createdAt: string;
  hostPlayerId: string | null;
  ruleset: string;
}

export interface JoinGameRequest {
  name: string;
  seat: string;
}

export interface JoinGameResponse {
  id: string;
  gameId: string;
  name: string;
  seat: string;
  score: number;
}

export async function createGame(request: CreateGameRequest): Promise<CreateGameResponse> {
  const response = await fetch(`${API_BASE}/api/game/`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    const message = errorData.message || `Failed to create game: ${response.status}`;
    throw new Error(message);
  }

  return await response.json();
}

export async function joinGame(gameId: string, request: JoinGameRequest): Promise<JoinGameResponse> {
  const response = await fetch(`${API_BASE}/api/game/${gameId}/join`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(request),
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    const message = errorData.message || `Failed to join game: ${response.status}`;
    throw new Error(message);
  }

  return await response.json();
}

export async function startGame(gameId: string): Promise<void> {
  const response = await fetch(`${API_BASE}/api/game/${gameId}/start`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
  });

  if (!response.ok) {
    const errorData = await response.json().catch(() => ({}));
    const message = errorData.message || `Failed to start game: ${response.status}`;
    throw new Error(message);
  }
}

let lobbyContainer: HTMLElement | null = null;

export function mountLobby(container: HTMLElement): void {
  lobbyContainer = container;
  
  // Add lobby mode class to body to hide HUD
  document.body.classList.add('lobby-mode');

  const root = document.createElement('div');
  root.id = 'lobby-root';
  root.innerHTML = `
    <style>
      #lobby-root {
        width: 100vw;
        height: 100vh;
        background: #1a1a1a;
        color: #ffffff;
        font-family: system-ui, -apple-system, sans-serif;
        overflow-y: auto;
        padding: 40px 20px;
        box-sizing: border-box;
      }
      #lobby-root .lobby-container {
        max-width: 1200px;
        margin: 0 auto;
      }
      #lobby-root h1 {
        font-size: 2.5rem;
        font-weight: 600;
        margin: 0 0 8px 0;
        color: #ffffff;
      }
      #lobby-root .subtitle {
        font-size: 1rem;
        color: #aaaaaa;
        margin: 0 0 40px 0;
      }
      #lobby-root .cards-grid {
        display: grid;
        grid-template-columns: 1fr 1fr;
        gap: 24px;
        margin-bottom: 24px;
      }
      @media (max-width: 900px) {
        #lobby-root .cards-grid {
          grid-template-columns: 1fr;
        }
      }
      #lobby-root .card {
        background: rgba(0, 0, 0, 0.5);
        border: 1px solid #333333;
        border-radius: 12px;
        padding: 24px;
      }
      #lobby-root .card-title {
        font-size: 1.25rem;
        font-weight: 600;
        margin: 0 0 8px 0;
      }
      #lobby-root .card-subtitle {
        font-size: 0.875rem;
        color: #aaaaaa;
        margin: 0 0 24px 0;
      }
      #lobby-root .form-group {
        margin-bottom: 16px;
      }
      #lobby-root .form-label {
        display: block;
        font-size: 0.875rem;
        font-weight: 500;
        margin-bottom: 6px;
        color: #cccccc;
      }
      #lobby-root .form-input,
      #lobby-root .form-select {
        width: 100%;
        padding: 10px 12px;
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid #444444;
        border-radius: 6px;
        color: #ffffff;
        font-size: 0.9375rem;
        font-family: inherit;
        box-sizing: border-box;
      }
      #lobby-root .form-input:focus,
      #lobby-root .form-select:focus {
        outline: none;
        border-color: #4a90e2;
      }
      #lobby-root .form-input::placeholder {
        color: #666666;
      }
      #lobby-root .form-row {
        display: grid;
        grid-template-columns: 2fr 1fr;
        gap: 12px;
      }
      #lobby-root .btn {
        padding: 10px 20px;
        border: none;
        border-radius: 6px;
        font-size: 0.9375rem;
        font-weight: 500;
        cursor: pointer;
        font-family: inherit;
        transition: all 0.15s;
      }
      #lobby-root .btn:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
      #lobby-root .btn-primary {
        background: #4a90e2;
        color: #ffffff;
      }
      #lobby-root .btn-primary:hover:not(:disabled) {
        background: #357abd;
      }
      #lobby-root .btn-secondary {
        background: rgba(255, 255, 255, 0.1);
        color: #ffffff;
      }
      #lobby-root .btn-secondary:hover:not(:disabled) {
        background: rgba(255, 255, 255, 0.15);
      }
      #lobby-root .error-message {
        background: rgba(220, 53, 69, 0.2);
        border: 1px solid rgba(220, 53, 69, 0.5);
        border-radius: 6px;
        padding: 12px;
        margin-top: 16px;
        color: #ff6b7a;
        font-size: 0.875rem;
      }
      #lobby-root .success-message {
        background: rgba(40, 167, 69, 0.2);
        border: 1px solid rgba(40, 167, 69, 0.5);
        border-radius: 6px;
        padding: 12px;
        margin-top: 16px;
        color: #5fdc8f;
        font-size: 0.875rem;
      }
      #lobby-root .ruleset-note {
        font-size: 0.8125rem;
        color: #888888;
        margin-top: 6px;
      }
    </style>
    <div class="lobby-container">
      <h1>Mahjong Claw</h1>
      <p class="subtitle">Create or join a game to start playing</p>
      
      <div class="cards-grid">
        <div class="card">
          <h2 class="card-title">Create Game</h2>
          <p class="card-subtitle">Start a new table with bots or wait for human players</p>
          
          <div class="form-group">
            <label class="form-label">Your Name</label>
            <input type="text" class="form-input" id="create-name" placeholder="Enter your name" />
          </div>
          
          <div class="form-group">
            <label class="form-label">Game Mode</label>
            <div class="form-row">
              <select class="form-select" id="create-mode">
                <option value="HumanVsBot">Human vs Bot</option>
                <option value="HumanVsHuman">Human vs Human</option>
              </select>
              <select class="form-select" id="create-bots">
                <option value="3">3 bots</option>
                <option value="2">2 bots</option>
                <option value="1">1 bot</option>
              </select>
            </div>
          </div>
          
          <div class="form-group">
            <label class="form-label">Ruleset</label>
            <input type="text" class="form-input" value="Changsha 258" disabled />
            <div class="ruleset-note">Only Changsha 258 rules supported currently</div>
          </div>
          
          <button class="btn btn-primary" id="create-btn">Create & Enter</button>
          <div id="create-status"></div>
        </div>
        
        <div class="card">
          <h2 class="card-title">Join Game</h2>
          <p class="card-subtitle">Enter a game ID to join an existing table</p>
          
          <div class="form-group">
            <label class="form-label">Game ID</label>
            <input type="text" class="form-input" id="join-game-id" placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" />
          </div>
          
          <div class="form-group">
            <label class="form-label">Your Name</label>
            <input type="text" class="form-input" id="join-name" placeholder="Enter your name" />
          </div>
          
          <div class="form-group">
            <label class="form-label">Seat</label>
            <select class="form-select" id="join-seat">
              <option value="East">East</option>
              <option value="South">South</option>
              <option value="West">West</option>
              <option value="North">North</option>
            </select>
          </div>
          
          <button class="btn btn-primary" id="join-btn">Join</button>
          <div id="join-status"></div>
        </div>
      </div>
    </div>
  `;

  container.appendChild(root);

  setupEventHandlers();
}

export function unmountLobby(): void {
  if (lobbyContainer) {
    const root = document.getElementById('lobby-root');
    if (root) {
      root.remove();
    }
    document.body.classList.remove('lobby-mode');
    lobbyContainer = null;
  }
}

function setupEventHandlers(): void {
  const createBtn = document.getElementById('create-btn') as HTMLButtonElement | null;
  const joinBtn = document.getElementById('join-btn') as HTMLButtonElement | null;
  const createMode = document.getElementById('create-mode') as HTMLSelectElement | null;
  const createBots = document.getElementById('create-bots') as HTMLSelectElement | null;

  if (createMode && createBots) {
    createMode.addEventListener('change', () => {
      createBots.disabled = createMode.value !== 'HumanVsBot';
    });
  }

  if (createBtn) {
    createBtn.addEventListener('click', handleCreate);
  }

  if (joinBtn) {
    joinBtn.addEventListener('click', handleJoin);
  }
}

async function handleCreate(): Promise<void> {
  const nameInput = document.getElementById('create-name') as HTMLInputElement | null;
  const modeSelect = document.getElementById('create-mode') as HTMLSelectElement | null;
  const botsSelect = document.getElementById('create-bots') as HTMLSelectElement | null;
  const statusDiv = document.getElementById('create-status');
  const createBtn = document.getElementById('create-btn') as HTMLButtonElement | null;

  if (!nameInput || !modeSelect || !botsSelect || !statusDiv || !createBtn) {
    return;
  }

  const name = nameInput.value.trim();
  if (!name) {
    showStatus(statusDiv, 'Please enter your name', 'error');
    return;
  }

  const mode = modeSelect.value;
  const botCount = mode === 'HumanVsBot' ? parseInt(botsSelect.value, 10) : 0;

  createBtn.disabled = true;
  showStatus(statusDiv, 'Creating game...', 'info');

  try {
    const result = await createGame({
      hostName: name,
      mode: mode,
      botSeatCount: botCount,
      ruleset: 'Changsha258',
    });

    const playerId = result.hostPlayerId;
    if (!playerId) {
      throw new Error('No player ID returned from server');
    }

    showStatus(statusDiv, `Game created! Redirecting...`, 'success');

    setTimeout(() => {
      window.location.href = `?game=${result.id}&player=${playerId}`;
    }, 500);
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Failed to create game';
    showStatus(statusDiv, message, 'error');
    createBtn.disabled = false;
  }
}

async function handleJoin(): Promise<void> {
  const gameIdInput = document.getElementById('join-game-id') as HTMLInputElement | null;
  const nameInput = document.getElementById('join-name') as HTMLInputElement | null;
  const seatSelect = document.getElementById('join-seat') as HTMLSelectElement | null;
  const statusDiv = document.getElementById('join-status');
  const joinBtn = document.getElementById('join-btn') as HTMLButtonElement | null;

  if (!gameIdInput || !nameInput || !seatSelect || !statusDiv || !joinBtn) {
    return;
  }

  const gameId = gameIdInput.value.trim();
  const name = nameInput.value.trim();
  const seat = seatSelect.value;

  if (!gameId) {
    showStatus(statusDiv, 'Please enter a game ID', 'error');
    return;
  }

  if (!name) {
    showStatus(statusDiv, 'Please enter your name', 'error');
    return;
  }

  joinBtn.disabled = true;
  showStatus(statusDiv, 'Joining game...', 'info');

  try {
    const result = await joinGame(gameId, { name, seat });

    showStatus(statusDiv, `Joined as ${result.seat}! Redirecting...`, 'success');

    setTimeout(() => {
      window.location.href = `?game=${result.gameId}&player=${result.id}`;
    }, 500);
  } catch (error) {
    const message = error instanceof Error ? error.message : 'Failed to join game';
    showStatus(statusDiv, message, 'error');
    joinBtn.disabled = false;
  }
}

function showStatus(container: HTMLElement, message: string, type: 'error' | 'success' | 'info'): void {
  const className = type === 'error' ? 'error-message' : type === 'success' ? 'success-message' : 'success-message';
  container.innerHTML = `<div class="${className}">${message}</div>`;
}
