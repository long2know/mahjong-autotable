import { initScene } from './scene';
import { initSignalR, getConfig } from './net';
import { generateTileAtlas } from './tileAtlas';
import { clientState } from './state';

async function main() {
  console.log('Mahjong Claw Autotable Client - Phase 1');

  const container = document.getElementById('canvas-container');
  if (!container) {
    console.error('Canvas container not found');
    return;
  }

  // Initialize tile atlas first (synchronous, procedural)
  console.log('[Main] Generating tile atlas...');
  const tileAtlas = generateTileAtlas();

  // Initialize scene with atlas
  console.log('[Main] Initializing scene...');
  initScene(container, tileAtlas);

  // Initialize networking last (will fetch state and trigger scene rebuild)
  console.log('[Main] Initializing networking...');
  await initSignalR();
  
  // Setup HUD updates
  const hudWall = document.getElementById('hud-wall');
  const hudTurn = document.getElementById('hud-turn');
  const hudYou = document.getElementById('hud-you');
  
  clientState.onStateChanged((state) => {
    if (!state) {
      if (hudWall) hudWall.textContent = '--';
      if (hudTurn) hudTurn.textContent = '--';
      if (hudYou) hudYou.textContent = '--';
      return;
    }
    
    const wallRemaining = state.wallTotal - state.tilesInPlay;
    if (hudWall) {
      hudWall.textContent = wallRemaining.toString();
    }
    
    if (hudTurn) {
      const currentPlayer = state.players.find(p => p.id === state.currentPlayerId);
      if (currentPlayer) {
        hudTurn.textContent = `${currentPlayer.name} (${currentPlayer.seat})`;
      } else {
        hudTurn.textContent = 'Waiting';
      }
    }
    
    if (hudYou) {
      const config = getConfig();
      if (config.playerId) {
        const yourPlayer = state.players.find(p => p.id === config.playerId);
        if (yourPlayer) {
          hudYou.textContent = `${yourPlayer.name} (${yourPlayer.seat})`;
        } else {
          hudYou.textContent = 'Not in game';
        }
      } else {
        hudYou.textContent = 'Spectator';
      }
    }
  });
  
  console.log('[Main] Bootstrap complete. Use ?game=<id>&player=<id> to connect to a game.');
}

main().catch(console.error);
