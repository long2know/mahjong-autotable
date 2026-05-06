import * as signalR from '@microsoft/signalr';
import { clientState } from './state';
import type { GameState } from './state';
import type {
  JoinGameResponse,
  StartGameResponse,
  DrawTileResponse,
  DiscardTileResponse,
  MeldResponse,
  WinResponse,
} from './types';
import { globalToastManager } from './toast';
import * as audio from './audio';

const API_BASE = import.meta.env.VITE_API_BASE || 'http://localhost:5270';
const HUB_URL = `${API_BASE}/hubs/game`;

let currentGameId: string | null = null;
let currentPlayerId: string | null = null;

export function getConfig(): { gameId: string | null; playerId: string | null } {
  return { gameId: currentGameId, playerId: currentPlayerId };
}

async function fetchGameState(gameId: string, playerId: string | null): Promise<void> {
  const url = playerId
    ? `${API_BASE}/api/game/${gameId}/state?viewerPlayerId=${playerId}`
    : `${API_BASE}/api/game/${gameId}/state`;
  
  try {
    const response = await fetch(url);
    if (!response.ok) {
      throw new Error(`Failed to fetch state: ${response.status} ${response.statusText}`);
    }
    
    const data = await response.json() as GameState;
    clientState.applyFullSnapshot(data);
    console.log('[Net] Game state fetched successfully');
  } catch (error) {
    console.error('[Net] Failed to fetch game state:', error);
    // Retry once after 2 seconds
    setTimeout(async () => {
      try {
        const response = await fetch(url);
        if (response.ok) {
          const data = await response.json() as GameState;
          clientState.applyFullSnapshot(data);
          console.log('[Net] Game state fetched successfully (retry)');
        }
      } catch (retryError) {
        console.error('[Net] Retry failed:', retryError);
      }
    }, 2000);
  }
}

export async function initSignalR(gameId?: string, playerId?: string): Promise<void> {
  // Get config from query params or use defaults
  const params = new URLSearchParams(window.location.search);
  currentGameId = gameId ?? params.get('game') ?? null;
  currentPlayerId = playerId ?? params.get('player') ?? null;
  
  if (!currentGameId) {
    console.warn('[Net] No gameId provided. Use ?game=<id>&player=<id> query params or pass to initSignalR()');
    return;
  }
  
  console.log(`[Net] Initializing for game: ${currentGameId}, player: ${currentPlayerId ?? 'spectator'}`);
  
  // Fetch initial state
  await fetchGameState(currentGameId, currentPlayerId);
  
  console.log(`[Net] Connecting to SignalR hub at ${HUB_URL}...`);

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect()
    .build();

  // Helper to refetch state after events
  const refetchState = async () => {
    if (currentGameId) {
      await fetchGameState(currentGameId, currentPlayerId);
    }
  };

  // Subscribe to game events
  connection.on('PlayerJoined', (data: JoinGameResponse) => {
    console.log('[SignalR] PlayerJoined:', data);
    clientState.applySignalREvent('PlayerJoined', data);
    refetchState();
  });

  connection.on('PlayerJoin', (data: JoinGameResponse) => {
    console.log('[SignalR] PlayerJoin (alias):', data);
    clientState.applySignalREvent('PlayerJoin', data);
    refetchState();
  });

  connection.on('GameStarted', (data: StartGameResponse) => {
    console.log('[SignalR] GameStarted:', data);
    clientState.applySignalREvent('GameStarted', data);
    refetchState();
  });

  connection.on('TileDrawn', (data: DrawTileResponse) => {
    console.log('[SignalR] TileDrawn:', data);
    clientState.applySignalREvent('TileDrawn', data);
    audio.play('draw');
    refetchState();
  });

  connection.on('DrawnTile', (data: DrawTileResponse) => {
    console.log('[SignalR] DrawnTile (alias):', data);
    clientState.applySignalREvent('DrawnTile', data);
    audio.play('draw');
    refetchState();
  });

  connection.on('TileDiscarded', (data: DiscardTileResponse) => {
    console.log('[SignalR] TileDiscarded:', data);
    clientState.applySignalREvent('TileDiscarded', data);
    audio.play('discard');
    refetchState();
  });

  connection.on('DiscardedTile', (data: DiscardTileResponse) => {
    console.log('[SignalR] DiscardedTile (alias):', data);
    clientState.applySignalREvent('DiscardedTile', data);
    audio.play('discard');
    refetchState();
  });

  connection.on('MeldDeclared', (data: MeldResponse) => {
    console.log('[SignalR] MeldDeclared:', data);
    clientState.applySignalREvent('MeldDeclared', data);
    audio.play('meld');
    refetchState();
  });

  connection.on('Meld', (data: MeldResponse) => {
    console.log('[SignalR] Meld (alias):', data);
    clientState.applySignalREvent('Meld', data);
    audio.play('meld');
    refetchState();
  });

  connection.on('GameWon', (data: WinResponse) => {
    console.log('[SignalR] GameWon:', data);
    clientState.applySignalREvent('GameWon', data);
    audio.play('win');
    refetchState();
  });

  connection.on('WinDeclared', (data: WinResponse) => {
    console.log('[SignalR] WinDeclared (alias):', data);
    clientState.applySignalREvent('WinDeclared', data);
    audio.play('win');
    refetchState();
  });

  connection.on('ClaimWindowOpened', (data: any) => {
    console.log('[SignalR] ClaimWindowOpened:', data);
    clientState.applySignalREvent('ClaimWindowOpened', data);
    if (currentPlayerId && data.eligiblePlayerIds && data.eligiblePlayerIds.includes(currentPlayerId)) {
      showClaimPrompt(data);
    }
  });

  connection.on('ClaimSubmitted', (data: any) => {
    console.log('[SignalR] ClaimSubmitted:', data);
    clientState.applySignalREvent('ClaimSubmitted', data);
  });

  connection.on('ClaimResolved', (data: any) => {
    console.log('[SignalR] ClaimResolved:', data);
    clientState.applySignalREvent('ClaimResolved', data);
    hideClaimPrompt();
    refetchState();
  });

  connection.onreconnecting((error) => {
    console.warn('[SignalR] Reconnecting...', error);
  });

  connection.onreconnected(async (connectionId) => {
    console.log('[SignalR] Reconnected:', connectionId);
    // Resync state on reconnection
    await refetchState();
  });

  connection.onclose((error) => {
    console.error('[SignalR] Connection closed:', error);
  });

  try {
    await connection.start();
    console.log('[SignalR] Connected successfully');
    
    // Join the game SignalR group
    if (currentGameId) {
      await connection.invoke('JoinGame', currentGameId);
      console.log(`[SignalR] Joined game group: ${currentGameId}`);
    }
  } catch (error) {
    console.error('[SignalR] Connection failed (API may not be running):', error);
  }
}

let claimPromptElement: HTMLElement | null = null;
let currentClaimData: any = null;

function showClaimPrompt(data: any): void {
  currentClaimData = data;
  
  if (claimPromptElement) {
    return; // Already showing
  }

  claimPromptElement = document.createElement('div');
  claimPromptElement.id = 'claim-prompt';
  claimPromptElement.style.cssText = `
    position: fixed;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    background: rgba(20, 20, 20, 0.98);
    padding: 30px;
    border-radius: 12px;
    border: 3px solid #e2a54a;
    box-shadow: 0 8px 24px rgba(226, 165, 74, 0.5);
    z-index: 3000;
    font-family: monospace;
    color: white;
    min-width: 300px;
  `;

  const title = document.createElement('h3');
  title.textContent = 'Claim Discard?';
  title.style.cssText = `
    margin: 0 0 20px 0;
    font-size: 24px;
    color: #e2a54a;
    text-align: center;
  `;
  claimPromptElement.appendChild(title);

  const timeout = document.createElement('div');
  timeout.textContent = `Timeout: ${data.timeoutMs / 1000}s`;
  timeout.style.cssText = `
    margin: 0 0 20px 0;
    font-size: 14px;
    color: #aaa;
    text-align: center;
  `;
  claimPromptElement.appendChild(timeout);

  const buttonContainer = document.createElement('div');
  buttonContainer.style.cssText = `
    display: flex;
    flex-direction: column;
    gap: 10px;
  `;

  const buttonStyle = `
    padding: 12px 20px;
    font-size: 16px;
    font-family: monospace;
    cursor: pointer;
    border: 2px solid #666;
    border-radius: 6px;
    background: rgba(255, 255, 255, 0.1);
    color: white;
    transition: all 0.2s;
  `;

  const winButton = document.createElement('button');
  winButton.textContent = '🀄 Win';
  winButton.style.cssText = buttonStyle + 'border-color: #4a90e2; color: #4a90e2;';
  winButton.onmouseover = () => { winButton.style.background = 'rgba(74, 144, 226, 0.3)'; };
  winButton.onmouseout = () => { winButton.style.background = 'rgba(255, 255, 255, 0.1)'; };
  winButton.onclick = () => submitClaim('Win');
  buttonContainer.appendChild(winButton);

  const pungButton = document.createElement('button');
  pungButton.textContent = '🀇 Pung';
  pungButton.style.cssText = buttonStyle + 'border-color: #a54ae2; color: #a54ae2;';
  pungButton.onmouseover = () => { pungButton.style.background = 'rgba(165, 74, 226, 0.3)'; };
  pungButton.onmouseout = () => { pungButton.style.background = 'rgba(255, 255, 255, 0.1)'; };
  pungButton.onclick = () => submitClaim('Pung');
  buttonContainer.appendChild(pungButton);

  const kongButton = document.createElement('button');
  kongButton.textContent = '🀫 Kong';
  kongButton.style.cssText = buttonStyle + 'border-color: #e2a54a; color: #e2a54a;';
  kongButton.onmouseover = () => { kongButton.style.background = 'rgba(226, 165, 74, 0.3)'; };
  kongButton.onmouseout = () => { kongButton.style.background = 'rgba(255, 255, 255, 0.1)'; };
  kongButton.onclick = () => submitClaim('ExposedKong');
  buttonContainer.appendChild(kongButton);

  const chowButton = document.createElement('button');
  chowButton.textContent = '🀐 Chow';
  chowButton.style.cssText = buttonStyle + 'border-color: #4ae290; color: #4ae290;';
  chowButton.onmouseover = () => { chowButton.style.background = 'rgba(74, 226, 144, 0.3)'; };
  chowButton.onmouseout = () => { chowButton.style.background = 'rgba(255, 255, 255, 0.1)'; };
  chowButton.onclick = () => submitClaim('Chow');
  buttonContainer.appendChild(chowButton);

  const passButton = document.createElement('button');
  passButton.textContent = '❌ Pass';
  passButton.style.cssText = buttonStyle + 'border-color: #e24a4a; color: #e24a4a;';
  passButton.onmouseover = () => { passButton.style.background = 'rgba(226, 74, 74, 0.3)'; };
  passButton.onmouseout = () => { passButton.style.background = 'rgba(255, 255, 255, 0.1)'; };
  passButton.onclick = () => submitClaim('Pass');
  buttonContainer.appendChild(passButton);

  claimPromptElement.appendChild(buttonContainer);
  document.body.appendChild(claimPromptElement);
}

function hideClaimPrompt(): void {
  if (claimPromptElement) {
    claimPromptElement.remove();
    claimPromptElement = null;
  }
  currentClaimData = null;
}

async function submitClaim(claimType: string): Promise<void> {
  if (!currentGameId || !currentPlayerId || !currentClaimData) {
    return;
  }

  hideClaimPrompt();

  try {
    const response = await fetch(`${API_BASE}/api/game/${currentGameId}/claim`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        playerId: currentPlayerId,
        claimType: claimType,
        meldTileIds: null,
        winContext: null
      })
    });

    if (!response.ok) {
      console.error(`[Net] Claim submission failed: ${response.status}`);
      const errorData = await response.json().catch(() => ({ message: 'Unknown error' }));
      globalToastManager.show(`Claim failed: ${errorData.message || response.statusText}`);
    } else {
      console.log(`[Net] Claim submitted: ${claimType}`);
    }
  } catch (error) {
    console.error('[Net] Failed to submit claim:', error);
    globalToastManager.show('Failed to submit claim');
  }
}
