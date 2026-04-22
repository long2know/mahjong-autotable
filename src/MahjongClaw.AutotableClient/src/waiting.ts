// Waiting overlay for games in "Waiting" state (before start)

import { startGame } from './lobby';
import type { GameState } from './state';

let overlayElement: HTMLElement | null = null;
let currentGameId: string | null = null;
let currentPlayerId: string | null = null;
let isHost = false;

export function showWaitingOverlay(
  gameId: string,
  playerId: string,
  state: GameState
): void {
  if (overlayElement) {
    updateWaitingOverlay(state);
    return;
  }

  currentGameId = gameId;
  currentPlayerId = playerId;

  const hostPlayer = state.players.find(p => p.id === state.hostPlayerId);
  isHost = hostPlayer ? hostPlayer.id === playerId : false;

  overlayElement = document.createElement('div');
  overlayElement.id = 'waiting-overlay';
  overlayElement.innerHTML = `
    <style>
      #waiting-overlay {
        position: fixed;
        top: 0;
        left: 0;
        width: 100vw;
        height: 100vh;
        background: rgba(0, 0, 0, 0.85);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 1000;
      }
      #waiting-overlay .waiting-content {
        background: rgba(30, 30, 30, 0.95);
        border: 2px solid #4a90e2;
        border-radius: 12px;
        padding: 32px;
        max-width: 500px;
        width: 90%;
        box-shadow: 0 8px 32px rgba(0, 0, 0, 0.5);
      }
      #waiting-overlay .waiting-title {
        font-size: 1.5rem;
        font-weight: 600;
        margin: 0 0 8px 0;
        color: #ffffff;
      }
      #waiting-overlay .waiting-subtitle {
        font-size: 0.875rem;
        color: #aaaaaa;
        margin: 0 0 24px 0;
      }
      #waiting-overlay .players-list {
        background: rgba(0, 0, 0, 0.3);
        border: 1px solid #333333;
        border-radius: 8px;
        padding: 16px;
        margin-bottom: 24px;
      }
      #waiting-overlay .players-title {
        font-size: 0.875rem;
        color: #aaaaaa;
        margin: 0 0 12px 0;
        font-weight: 500;
      }
      #waiting-overlay .player-row {
        display: flex;
        justify-content: space-between;
        align-items: center;
        padding: 8px 0;
        border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      }
      #waiting-overlay .player-row:last-child {
        border-bottom: none;
      }
      #waiting-overlay .player-name {
        font-size: 0.9375rem;
        color: #ffffff;
      }
      #waiting-overlay .player-seat {
        font-size: 0.875rem;
        color: #888888;
        font-family: monospace;
      }
      #waiting-overlay .empty-seat {
        color: #555555;
        font-style: italic;
      }
      #waiting-overlay .share-section {
        background: rgba(0, 0, 0, 0.3);
        border: 1px solid #333333;
        border-radius: 8px;
        padding: 16px;
        margin-bottom: 24px;
      }
      #waiting-overlay .share-title {
        font-size: 0.875rem;
        color: #aaaaaa;
        margin: 0 0 8px 0;
        font-weight: 500;
      }
      #waiting-overlay .share-link {
        display: flex;
        gap: 8px;
      }
      #waiting-overlay .share-input {
        flex: 1;
        padding: 8px 12px;
        background: rgba(255, 255, 255, 0.05);
        border: 1px solid #444444;
        border-radius: 6px;
        color: #ffffff;
        font-size: 0.875rem;
        font-family: monospace;
      }
      #waiting-overlay .btn {
        padding: 10px 20px;
        border: none;
        border-radius: 6px;
        font-size: 0.9375rem;
        font-weight: 500;
        cursor: pointer;
        font-family: system-ui, -apple-system, sans-serif;
        transition: all 0.15s;
      }
      #waiting-overlay .btn:disabled {
        opacity: 0.5;
        cursor: not-allowed;
      }
      #waiting-overlay .btn-primary {
        background: #4a90e2;
        color: #ffffff;
        width: 100%;
      }
      #waiting-overlay .btn-primary:hover:not(:disabled) {
        background: #357abd;
      }
      #waiting-overlay .btn-secondary {
        background: rgba(255, 255, 255, 0.1);
        color: #ffffff;
        padding: 8px 16px;
        font-size: 0.875rem;
      }
      #waiting-overlay .btn-secondary:hover:not(:disabled) {
        background: rgba(255, 255, 255, 0.15);
      }
      #waiting-overlay .host-note {
        font-size: 0.8125rem;
        color: #888888;
        text-align: center;
        margin-top: 12px;
      }
      #waiting-overlay .copy-feedback {
        font-size: 0.8125rem;
        color: #5fdc8f;
        margin-top: 8px;
        opacity: 0;
        transition: opacity 0.2s;
      }
      #waiting-overlay .copy-feedback.visible {
        opacity: 1;
      }
    </style>
    <div class="waiting-content">
      <h2 class="waiting-title">Waiting Room</h2>
      <p class="waiting-subtitle">Game will start once all players are ready</p>
      
      <div class="players-list">
        <div class="players-title">Players (0/4)</div>
        <div id="waiting-players-content"></div>
      </div>
      
      <div class="share-section">
        <div class="share-title">Invite Players</div>
        <div class="share-link">
          <input type="text" class="share-input" id="share-link-input" readonly />
          <button class="btn btn-secondary" id="copy-link-btn">Copy</button>
        </div>
        <div class="copy-feedback" id="copy-feedback">Link copied!</div>
      </div>
      
      <button class="btn btn-primary" id="start-game-btn" style="display: none;">Start Game</button>
      <div class="host-note" id="host-note"></div>
    </div>
  `;

  document.body.appendChild(overlayElement);

  const shareInput = document.getElementById('share-link-input') as HTMLInputElement | null;
  if (shareInput && currentGameId) {
    const baseUrl = window.location.origin + window.location.pathname;
    shareInput.value = `${baseUrl}?game=${currentGameId}`;
  }

  const copyBtn = document.getElementById('copy-link-btn');
  if (copyBtn) {
    copyBtn.addEventListener('click', handleCopyLink);
  }

  const startBtn = document.getElementById('start-game-btn');
  if (startBtn) {
    startBtn.addEventListener('click', handleStartGame);
  }

  updateWaitingOverlay(state);
}

export function hideWaitingOverlay(): void {
  if (overlayElement) {
    overlayElement.remove();
    overlayElement = null;
  }
}

export function isVisible(): boolean {
  return overlayElement !== null;
}

function updateWaitingOverlay(state: GameState): void {
  if (!overlayElement) {
    return;
  }

  const playersContent = document.getElementById('waiting-players-content');
  const startBtn = document.getElementById('start-game-btn') as HTMLButtonElement | null;
  const hostNote = document.getElementById('host-note');
  const playersTitle = overlayElement.querySelector('.players-title');

  if (playersTitle) {
    playersTitle.textContent = `Players (${state.players.length}/4)`;
  }

  if (playersContent) {
    const seats = ['East', 'South', 'West', 'North'];
    const playersBySeat = new Map(state.players.map(p => [p.seat, p]));

    playersContent.innerHTML = seats
      .map(seat => {
        const player = playersBySeat.get(seat);
        if (player) {
          return `
            <div class="player-row">
              <span class="player-name">${escapeHtml(player.name)}</span>
              <span class="player-seat">${seat}</span>
            </div>
          `;
        } else {
          return `
            <div class="player-row">
              <span class="player-name empty-seat">Empty</span>
              <span class="player-seat">${seat}</span>
            </div>
          `;
        }
      })
      .join('');
  }

  const hostPlayer = state.players.find(p => p.id === state.hostPlayerId);
  const isCurrentHost = hostPlayer ? hostPlayer.id === currentPlayerId : false;

  if (startBtn) {
    if (isCurrentHost) {
      startBtn.style.display = 'block';
      startBtn.disabled = state.players.length < 1;
    } else {
      startBtn.style.display = 'none';
    }
  }

  if (hostNote) {
    if (isCurrentHost) {
      if (state.players.length < 4) {
        hostNote.textContent = 'You can start with fewer than 4 players (bots will fill empty seats)';
      } else {
        hostNote.textContent = 'All seats filled! Click Start Game to begin';
      }
    } else {
      const hostName = hostPlayer ? hostPlayer.name : 'Host';
      hostNote.textContent = `Waiting for ${hostName} to start the game...`;
    }
  }
}

async function handleStartGame(): Promise<void> {
  if (!currentGameId) {
    return;
  }

  const startBtn = document.getElementById('start-game-btn') as HTMLButtonElement | null;
  if (startBtn) {
    startBtn.disabled = true;
    startBtn.textContent = 'Starting...';
  }

  try {
    await startGame(currentGameId);
  } catch (error) {
    console.error('[Waiting] Failed to start game:', error);

    if (startBtn) {
      startBtn.disabled = false;
      startBtn.textContent = 'Start Game';
    }

    const message = error instanceof Error ? error.message : 'Failed to start game';
    alert(message);
  }
}

function handleCopyLink(): void {
  const shareInput = document.getElementById('share-link-input') as HTMLInputElement | null;
  const feedback = document.getElementById('copy-feedback');

  if (shareInput) {
    shareInput.select();
    document.execCommand('copy');

    if (feedback) {
      feedback.classList.add('visible');
      setTimeout(() => {
        feedback.classList.remove('visible');
      }, 2000);
    }
  }
}

function escapeHtml(text: string): string {
  const div = document.createElement('div');
  div.textContent = text;
  return div.innerHTML;
}
