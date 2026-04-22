import { initScene } from './scene';
import { initSignalR, getConfig } from './net';
import { generateTileAtlas } from './tileAtlas';
import { clientState } from './state';
import { createInputHandler } from './input';
import { createHudActions, createViewButton } from './hud';
import * as actions from './actions';
import { globalToastManager } from './toast';
import { gameEndOverlay } from './overlay';
import * as audio from './audio';
import { mountLobby, unmountLobby } from './lobby';
import { showWaitingOverlay, hideWaitingOverlay, isVisible as isWaitingVisible } from './waiting';

async function main() {
  console.log('Mahjong Claw Autotable Client - Phase 5');
  
  // Check for game query param to determine routing
  const params = new URLSearchParams(window.location.search);
  const gameId = params.get('game');
  const playerId = params.get('player');
  
  if (!gameId) {
    // Show lobby if no game ID
    console.log('[Main] No game ID found, showing lobby');
    const lobbyContainer = document.getElementById('canvas-container');
    if (lobbyContainer) {
      mountLobby(lobbyContainer);
    }
    return;
  }
  
  // Game ID present, initialize game scene
  console.log('[Main] Game ID found, initializing game scene');

  const container = document.getElementById('canvas-container');
  if (!container) {
    console.error('Canvas container not found');
    return;
  }

  // Initialize tile atlas first (synchronous, procedural)
  console.log('[Main] Generating tile atlas...');
  const tileAtlas = generateTileAtlas();

  // Initialize scene with camera controller
  console.log('[Main] Initializing scene...');
  const { cameraController, scene } = initScene(container, tileAtlas);

  // Initialize networking last (will fetch state and trigger scene rebuild)
  console.log('[Main] Initializing networking...');
  await initSignalR();
  
  // Setup HUD updates
  const hudWall = document.getElementById('hud-wall');
  const hudTurn = document.getElementById('hud-turn');
  const hudYou = document.getElementById('hud-you');
  
  // Initialize input handler
  console.log('[Main] Initializing input handler...');
  const inputHandler = createInputHandler(cameraController.getActive(), scene, container);
  const config = getConfig();
  inputHandler.setPlayerId(config.playerId);
  inputHandler.setState(clientState.getState());
  
  // Initialize HUD actions
  console.log('[Main] Initializing HUD actions...');
  const hudActions = createHudActions('hud-actions-container');
  hudActions.setPlayerId(config.playerId);
  hudActions.setState(clientState.getState());
  
  // Initialize view button
  console.log('[Main] Initializing view button...');
  const viewButton = createViewButton('hud', cameraController);
  viewButton.setToggleCallback(() => {
    inputHandler.setCamera(cameraController.getActive());
  });
  
  // Keyboard listener for 'p' key
  window.addEventListener('keydown', (event) => {
    if (event.key === 'p' || event.key === 'P') {
      cameraController.toggleMode();
      viewButton.updateLabel();
      inputHandler.setCamera(cameraController.getActive());
      console.log(`[Main] Camera mode: ${cameraController.getMode()}`);
    }
  });
  
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
          if (result.success) {
            globalToastManager.show('Drew a tile');
            audio.play('draw');
          }
          break;
          
        case 'discardRequested':
          console.log('[Main] Discard requested:', event.tileId);
          result = await actions.discardTile(config.gameId, config.playerId, event.tileId);
          if (result.success) {
            globalToastManager.show('Tile discarded');
            audio.play('discard');
          }
          break;
          
        case 'meldRequested':
          console.log('[Main] Meld requested:', event.meldType, event.tileIds);
          result = await actions.declareMeld(config.gameId, config.playerId, event.meldType, event.tileIds);
          if (result.success) {
            globalToastManager.show(`${event.meldType} declared`);
            audio.play('meld');
          }
          break;
          
        case 'winRequested':
          console.log('[Main] Win requested:', event.isSelfDraw ? 'self-draw' : 'discard');
          const state = clientState.getState();
          const discarderPlayerId = event.isSelfDraw ? null : state?.lastDiscardPlayerId ?? null;
          result = await actions.declareWin(config.gameId, config.playerId, event.isSelfDraw, discarderPlayerId);
          if (result.success) {
            globalToastManager.show('Win declared!');
            audio.play('win');
          }
          break;
          
        case 'pass':
          console.log('[Main] Pass');
          // No server call needed, just a UI action
          break;
      }
      
      if (result && !result.success) {
        console.error('[Main] Action failed:', result.error);
        globalToastManager.show(`Error: ${result.error}`);
      }
    } catch (error) {
      console.error('[Main] Action error:', error);
      globalToastManager.show(`Error: ${error instanceof Error ? error.message : 'Unknown error'}`);
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
    
    // Show or hide waiting overlay based on game status
    if (state.status === 'Waiting' && config.gameId && config.playerId) {
      if (!isWaitingVisible()) {
        showWaitingOverlay(config.gameId, config.playerId, state);
      } else {
        // Update the existing waiting overlay
        showWaitingOverlay(config.gameId, config.playerId, state);
      }
    } else if (state.status === 'Active' && isWaitingVisible()) {
      hideWaitingOverlay();
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
    
    // Show game end overlay if game is completed
    if (state.status === 'Completed' && !gameEndOverlay.isVisible()) {
      gameEndOverlay.show(state);
    }
    
    // Update camera if local player seat changed
    if (config.playerId) {
      const player = state.players.find(p => p.id === config.playerId);
      if (player) {
        const seatIndex = ['East', 'South', 'West', 'North'].indexOf(player.seat);
        if (seatIndex !== -1) {
          cameraController.updateSeat(seatIndex);
        }
      }
    }
  });
  
  console.log('[Main] Bootstrap complete.');
}

main().catch(console.error);
