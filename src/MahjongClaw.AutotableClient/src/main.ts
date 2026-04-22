import { initScene } from './scene';
import { initSignalR, getConfig } from './net';
import { generateTileAtlas } from './tileAtlas';
import { clientState } from './state';
import { createInputHandler } from './input';
import { createHudActions } from './hud';
import * as actions from './actions';

async function main() {
  console.log('Mahjong Claw Autotable Client - Phase 2');

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
  const { camera, scene } = initScene(container, tileAtlas);

  // Initialize networking last (will fetch state and trigger scene rebuild)
  console.log('[Main] Initializing networking...');
  await initSignalR();
  
  // Setup HUD updates
  const hudWall = document.getElementById('hud-wall');
  const hudTurn = document.getElementById('hud-turn');
  const hudYou = document.getElementById('hud-you');
  
  // Initialize input handler
  console.log('[Main] Initializing input handler...');
  const inputHandler = createInputHandler(camera, scene, container);
  const config = getConfig();
  inputHandler.setPlayerId(config.playerId);
  inputHandler.setState(clientState.getState());
  
  // Initialize HUD actions
  console.log('[Main] Initializing HUD actions...');
  const hudActions = createHudActions('hud-actions-container');
  hudActions.setPlayerId(config.playerId);
  hudActions.setState(clientState.getState());
  
  // Input locking flag
  let isActionInProgress = false;
  
  // Handle input events (raycasting and button clicks)
  const handleInputEvent = async (event: import('./input').InputEvent) => {
    if (isActionInProgress || !config.gameId || !config.playerId) {
      return;
    }
    
    isActionInProgress = true;
    inputHandler.setEnabled(false);
    
    try {
      let result: actions.ActionResult<unknown> | null = null;
      
      switch (event.type) {
        case 'drawRequested':
          console.log('[Main] Draw requested');
          result = await actions.drawTile(config.gameId, config.playerId);
          break;
          
        case 'discardRequested':
          console.log('[Main] Discard requested:', event.tileId);
          result = await actions.discardTile(config.gameId, config.playerId, event.tileId);
          break;
          
        case 'meldRequested':
          console.log('[Main] Meld requested:', event.meldType, event.tileIds);
          result = await actions.declareMeld(config.gameId, config.playerId, event.meldType, event.tileIds);
          break;
          
        case 'winRequested':
          console.log('[Main] Win requested:', event.isSelfDraw ? 'self-draw' : 'discard');
          const state = clientState.getState();
          const discarderPlayerId = event.isSelfDraw ? null : state?.lastDiscardPlayerId ?? null;
          result = await actions.declareWin(config.gameId, config.playerId, event.isSelfDraw, discarderPlayerId);
          break;
          
        case 'pass':
          console.log('[Main] Pass');
          // No server call needed, just a UI action
          break;
      }
      
      if (result && !result.success) {
        console.error('[Main] Action failed:', result.error);
        alert(`Action failed: ${result.error}`);
      }
    } catch (error) {
      console.error('[Main] Action error:', error);
      alert(`Error: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      isActionInProgress = false;
      inputHandler.setEnabled(true);
    }
  };
  
  inputHandler.onInput(handleInputEvent);
  hudActions.onButtonClick(handleInputEvent);
  
  clientState.onStateChanged((state) => {
    if (!state) {
      if (hudWall) hudWall.textContent = '--';
      if (hudTurn) hudTurn.textContent = '--';
      if (hudYou) hudYou.textContent = '--';
      inputHandler.setState(null);
      hudActions.setState(null);
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
    
    // Update input handler and HUD with new state
    inputHandler.setState(state);
    hudActions.setState(state);
  });
  
  console.log('[Main] Bootstrap complete. Use ?game=<id>&player=<id> to connect to a game.');
}

main().catch(console.error);
