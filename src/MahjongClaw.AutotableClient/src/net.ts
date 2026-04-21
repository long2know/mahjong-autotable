import * as signalR from '@microsoft/signalr';
import type {
  JoinGameResponse,
  StartGameResponse,
  DrawTileResponse,
  DiscardTileResponse,
  MeldResponse,
  WinResponse,
} from './types';

const HUB_URL = import.meta.env.VITE_HUB_URL || 'http://localhost:5270/hubs/game';

export async function initSignalR(): Promise<void> {
  console.log(`Connecting to SignalR hub at ${HUB_URL}...`);

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(HUB_URL)
    .withAutomaticReconnect()
    .build();

  // Subscribe to game events
  connection.on('PlayerJoined', (data: JoinGameResponse) => {
    console.log('[SignalR] PlayerJoined:', data);
  });

  connection.on('PlayerJoin', (data: JoinGameResponse) => {
    console.log('[SignalR] PlayerJoin (alias):', data);
  });

  connection.on('GameStarted', (data: StartGameResponse) => {
    console.log('[SignalR] GameStarted:', data);
  });

  connection.on('TileDrawn', (data: DrawTileResponse) => {
    console.log('[SignalR] TileDrawn:', data);
  });

  connection.on('DrawnTile', (data: DrawTileResponse) => {
    console.log('[SignalR] DrawnTile (alias):', data);
  });

  connection.on('TileDiscarded', (data: DiscardTileResponse) => {
    console.log('[SignalR] TileDiscarded:', data);
  });

  connection.on('DiscardedTile', (data: DiscardTileResponse) => {
    console.log('[SignalR] DiscardedTile (alias):', data);
  });

  connection.on('MeldDeclared', (data: MeldResponse) => {
    console.log('[SignalR] MeldDeclared:', data);
  });

  connection.on('Meld', (data: MeldResponse) => {
    console.log('[SignalR] Meld (alias):', data);
  });

  connection.on('GameWon', (data: WinResponse) => {
    console.log('[SignalR] GameWon:', data);
  });

  connection.on('WinDeclared', (data: WinResponse) => {
    console.log('[SignalR] WinDeclared (alias):', data);
  });

  connection.onreconnecting((error) => {
    console.warn('[SignalR] Reconnecting...', error);
  });

  connection.onreconnected((connectionId) => {
    console.log('[SignalR] Reconnected:', connectionId);
  });

  connection.onclose((error) => {
    console.error('[SignalR] Connection closed:', error);
  });

  try {
    await connection.start();
    console.log('[SignalR] Connected successfully');
  } catch (error) {
    console.error('[SignalR] Connection failed (API may not be running):', error);
  }
}
