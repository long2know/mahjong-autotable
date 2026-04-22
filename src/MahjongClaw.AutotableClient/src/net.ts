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
    refetchState();
  });

  connection.on('DrawnTile', (data: DrawTileResponse) => {
    console.log('[SignalR] DrawnTile (alias):', data);
    clientState.applySignalREvent('DrawnTile', data);
    refetchState();
  });

  connection.on('TileDiscarded', (data: DiscardTileResponse) => {
    console.log('[SignalR] TileDiscarded:', data);
    clientState.applySignalREvent('TileDiscarded', data);
    refetchState();
  });

  connection.on('DiscardedTile', (data: DiscardTileResponse) => {
    console.log('[SignalR] DiscardedTile (alias):', data);
    clientState.applySignalREvent('DiscardedTile', data);
    refetchState();
  });

  connection.on('MeldDeclared', (data: MeldResponse) => {
    console.log('[SignalR] MeldDeclared:', data);
    clientState.applySignalREvent('MeldDeclared', data);
    refetchState();
  });

  connection.on('Meld', (data: MeldResponse) => {
    console.log('[SignalR] Meld (alias):', data);
    clientState.applySignalREvent('Meld', data);
    refetchState();
  });

  connection.on('GameWon', (data: WinResponse) => {
    console.log('[SignalR] GameWon:', data);
    clientState.applySignalREvent('GameWon', data);
    refetchState();
  });

  connection.on('WinDeclared', (data: WinResponse) => {
    console.log('[SignalR] WinDeclared (alias):', data);
    clientState.applySignalREvent('WinDeclared', data);
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
