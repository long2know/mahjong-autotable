// HUD action buttons for game interactions
import type { GameState } from './state';
import type { InputEvent } from './input';
import type { CameraController } from './camera';

type ButtonClickListener = (event: InputEvent) => void;

interface MeldOption {
  type: string;
  tileIds: string[];
}

export class HudActions {
  private container: HTMLElement;
  private listener: ButtonClickListener | null = null;
  private currentState: GameState | null = null;
  private playerId: string | null = null;
  private claimTimeout: number | null = null;
  private readonly CLAIM_WINDOW_MS = 5000;

  constructor(containerId: string) {
    const container = document.getElementById(containerId);
    if (!container) {
      throw new Error(`HUD container #${containerId} not found`);
    }
    this.container = container;
    this.render();
  }

  setPlayerId(playerId: string | null): void {
    this.playerId = playerId;
    this.update();
  }

  setState(state: GameState | null): void {
    this.currentState = state;
    this.update();
  }

  onButtonClick(callback: ButtonClickListener): void {
    this.listener = callback;
  }

  private render(): void {
    this.container.innerHTML = `
      <div id="hud-actions" style="
        position: fixed;
        bottom: 20px;
        left: 50%;
        transform: translateX(-50%);
        display: flex;
        gap: 10px;
        background: rgba(0, 0, 0, 0.7);
        padding: 15px 20px;
        border-radius: 8px;
        font-family: monospace;
        font-size: 14px;
      ">
        <button id="btn-draw" disabled style="
          padding: 8px 16px;
          background: #4a90e2;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: bold;
        ">Draw</button>
        
        <button id="btn-chow" disabled style="
          padding: 8px 16px;
          background: #f39c12;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: bold;
          display: none;
        ">Chow</button>
        
        <button id="btn-pung" disabled style="
          padding: 8px 16px;
          background: #e67e22;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: bold;
          display: none;
        ">Pung</button>
        
        <button id="btn-kong" disabled style="
          padding: 8px 16px;
          background: #e74c3c;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: bold;
          display: none;
        ">Kong</button>
        
        <button id="btn-win" disabled style="
          padding: 8px 16px;
          background: #27ae60;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: bold;
          display: none;
        ">Win</button>
        
        <button id="btn-self-win" disabled style="
          padding: 8px 16px;
          background: #2ecc71;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: bold;
        ">Self-Draw Win</button>
        
        <button id="btn-pass" style="
          padding: 8px 16px;
          background: #95a5a6;
          color: white;
          border: none;
          border-radius: 4px;
          cursor: pointer;
          font-weight: bold;
          display: none;
        ">Pass</button>
      </div>
    `;

    // Attach event listeners
    this.attachListeners();
  }

  private attachListeners(): void {
    const drawBtn = document.getElementById('btn-draw');
    const chowBtn = document.getElementById('btn-chow');
    const pungBtn = document.getElementById('btn-pung');
    const kongBtn = document.getElementById('btn-kong');
    const winBtn = document.getElementById('btn-win');
    const selfWinBtn = document.getElementById('btn-self-win');
    const passBtn = document.getElementById('btn-pass');

    if (drawBtn) {
      drawBtn.addEventListener('click', () => this.handleDraw());
    }
    if (chowBtn) {
      chowBtn.addEventListener('click', () => this.handleChow());
    }
    if (pungBtn) {
      pungBtn.addEventListener('click', () => this.handlePung());
    }
    if (kongBtn) {
      kongBtn.addEventListener('click', () => this.handleKong());
    }
    if (winBtn) {
      winBtn.addEventListener('click', () => this.handleWin());
    }
    if (selfWinBtn) {
      selfWinBtn.addEventListener('click', () => this.handleSelfWin());
    }
    if (passBtn) {
      passBtn.addEventListener('click', () => this.handlePass());
    }
  }

  private update(): void {
    if (!this.currentState || !this.playerId) {
      this.hideAllButtons();
      return;
    }

    const drawBtn = document.getElementById('btn-draw') as HTMLButtonElement | null;
    const chowBtn = document.getElementById('btn-chow') as HTMLButtonElement | null;
    const pungBtn = document.getElementById('btn-pung') as HTMLButtonElement | null;
    const kongBtn = document.getElementById('btn-kong') as HTMLButtonElement | null;
    const winBtn = document.getElementById('btn-win') as HTMLButtonElement | null;
    const selfWinBtn = document.getElementById('btn-self-win') as HTMLButtonElement | null;
    const passBtn = document.getElementById('btn-pass') as HTMLButtonElement | null;

    // Check if it's our turn
    const isOurTurn = this.currentState.currentPlayerId === this.playerId;
    const handCount = this.currentState.viewerHand.length;

    // Draw button: enabled when it's our turn and we need to draw (hand count % 3 == 1)
    const canDraw = isOurTurn && handCount > 0 && handCount % 3 === 1;
    if (drawBtn) {
      drawBtn.disabled = !canDraw;
      drawBtn.style.opacity = canDraw ? '1' : '0.5';
      drawBtn.style.cursor = canDraw ? 'pointer' : 'not-allowed';
    }

    // Self-draw win button: enabled when it's our turn and hand count is right
    const canSelfWin = isOurTurn && handCount > 0 && handCount % 3 === 2;
    if (selfWinBtn) {
      selfWinBtn.disabled = !canSelfWin;
      selfWinBtn.style.opacity = canSelfWin ? '1' : '0.5';
      selfWinBtn.style.cursor = canSelfWin ? 'pointer' : 'not-allowed';
    }

    // Check for claim opportunities (recent discard by another player)
    const hasClaimOpportunity = this.checkClaimOpportunity();
    if (hasClaimOpportunity) {
      this.showClaimButtons();
    } else {
      this.hideClaimButtons();
    }
  }

  private checkClaimOpportunity(): boolean {
    if (!this.currentState || !this.playerId) {
      return false;
    }

    // Check if there's a recent discard and it's not ours
    const lastDiscardPlayerId = this.currentState.lastDiscardPlayerId;
    if (!lastDiscardPlayerId || lastDiscardPlayerId === this.playerId) {
      return false;
    }

    // Check if we already have a claim timeout running
    // If not, start one
    if (!this.claimTimeout) {
      this.claimTimeout = window.setTimeout(() => {
        this.claimTimeout = null;
        this.hideClaimButtons();
      }, this.CLAIM_WINDOW_MS);
    }

    return true;
  }

  private showClaimButtons(): void {
    if (!this.currentState || !this.playerId) {
      return;
    }

    const chowBtn = document.getElementById('btn-chow') as HTMLButtonElement | null;
    const pungBtn = document.getElementById('btn-pung') as HTMLButtonElement | null;
    const kongBtn = document.getElementById('btn-kong') as HTMLButtonElement | null;
    const winBtn = document.getElementById('btn-win') as HTMLButtonElement | null;
    const passBtn = document.getElementById('btn-pass') as HTMLButtonElement | null;

    // Check what melds are possible with the last discard
    const meldOptions = this.calculateMeldOptions();

    if (chowBtn) {
      const hasChow = meldOptions.some(m => m.type === 'Chow');
      chowBtn.style.display = hasChow ? 'block' : 'none';
      chowBtn.disabled = !hasChow;
      chowBtn.style.opacity = hasChow ? '1' : '0.5';
    }

    if (pungBtn) {
      const hasPung = meldOptions.some(m => m.type === 'Pung');
      pungBtn.style.display = hasPung ? 'block' : 'none';
      pungBtn.disabled = !hasPung;
      pungBtn.style.opacity = hasPung ? '1' : '0.5';
    }

    if (kongBtn) {
      const hasKong = meldOptions.some(m => m.type === 'Kong');
      kongBtn.style.display = hasKong ? 'block' : 'none';
      kongBtn.disabled = !hasKong;
      kongBtn.style.opacity = hasKong ? '1' : '0.5';
    }

    if (winBtn) {
      winBtn.style.display = 'block';
      winBtn.disabled = false;
      winBtn.style.opacity = '1';
    }

    if (passBtn) {
      passBtn.style.display = 'block';
    }
  }

  private hideClaimButtons(): void {
    const chowBtn = document.getElementById('btn-chow');
    const pungBtn = document.getElementById('btn-pung');
    const kongBtn = document.getElementById('btn-kong');
    const winBtn = document.getElementById('btn-win');
    const passBtn = document.getElementById('btn-pass');

    if (chowBtn) chowBtn.style.display = 'none';
    if (pungBtn) pungBtn.style.display = 'none';
    if (kongBtn) kongBtn.style.display = 'none';
    if (winBtn) winBtn.style.display = 'none';
    if (passBtn) passBtn.style.display = 'none';

    if (this.claimTimeout) {
      clearTimeout(this.claimTimeout);
      this.claimTimeout = null;
    }
  }

  private hideAllButtons(): void {
    this.hideClaimButtons();
    const drawBtn = document.getElementById('btn-draw') as HTMLButtonElement | null;
    const selfWinBtn = document.getElementById('btn-self-win') as HTMLButtonElement | null;
    if (drawBtn) drawBtn.disabled = true;
    if (selfWinBtn) selfWinBtn.disabled = true;
  }

  private calculateMeldOptions(): MeldOption[] {
    if (!this.currentState || !this.playerId) {
      return [];
    }

    const lastDiscardTileId = this.currentState.lastDiscardTileId;
    if (!lastDiscardTileId) {
      return [];
    }

    // Find the last discard tile label
    const lastDiscard = this.currentState.discards
      .filter(d => d.tileId === lastDiscardTileId)
      .sort((a, b) => b.sequence - a.sequence)[0];
    
    if (!lastDiscard) {
      return [];
    }

    const options: MeldOption[] = [];
    const hand = this.currentState.viewerHand;

    // Parse the discard tile
    const discardInfo = this.parseTileLabel(lastDiscard.tileLabel);
    if (!discardInfo) {
      return options;
    }

    // Check for Pung (3 of same tile)
    const matchingTiles = hand.filter(t => {
      const info = this.parseTileLabel(t.label);
      return info && info.suit === discardInfo.suit && info.rank === discardInfo.rank;
    });

    if (matchingTiles.length >= 2) {
      options.push({
        type: 'Pung',
        tileIds: [...matchingTiles.slice(0, 2).map(t => t.id), lastDiscardTileId],
      });
    }

    // Check for Kong (4 of same tile)
    if (matchingTiles.length >= 3) {
      options.push({
        type: 'Kong',
        tileIds: [...matchingTiles.slice(0, 3).map(t => t.id), lastDiscardTileId],
      });
    }

    // Check for Chow (sequence of 3)
    if (discardInfo.suit !== 'Wind' && discardInfo.suit !== 'Dragon') {
      const chowOptions = this.findChowCombinations(hand, discardInfo);
      options.push(...chowOptions.map(ids => ({
        type: 'Chow',
        tileIds: [...ids, lastDiscardTileId],
      })));
    }

    return options;
  }

  private parseTileLabel(label: string): { suit: string; rank: number } | null {
    const match = label.match(/(\d+)\s+(Bamboo|Character|Dot)/i);
    if (match && match[1] && match[2]) {
      return {
        suit: match[2],
        rank: parseInt(match[1], 10),
      };
    }
    return null;
  }

  private findChowCombinations(hand: { id: string; label: string }[], target: { suit: string; rank: number }): string[][] {
    const sameSuit = hand.filter(t => {
      const info = this.parseTileLabel(t.label);
      return info && info.suit === target.suit;
    });

    const results: string[][] = [];
    
    // Try to form sequences: target-2, target-1, target OR target-1, target, target+1 OR target, target+1, target+2
    const positions = [
      [-2, -1], // need tiles 2 and 1 before target
      [-1, 1],  // need tiles 1 before and 1 after target
      [1, 2],   // need tiles 1 and 2 after target
    ];

    for (const offsets of positions) {
      const offset1 = offsets[0];
      const offset2 = offsets[1];
      if (offset1 === undefined || offset2 === undefined) continue;
      
      const rank1 = target.rank + offset1;
      const rank2 = target.rank + offset2;
      
      const tile1 = sameSuit.find(t => {
        const info = this.parseTileLabel(t.label);
        return info && info.rank === rank1;
      });
      
      const tile2 = sameSuit.find(t => {
        const info = this.parseTileLabel(t.label);
        return info && info.rank === rank2 && t.id !== tile1?.id;
      });

      if (tile1 && tile2) {
        results.push([tile1.id, tile2.id]);
      }
    }

    return results;
  }

  private handleDraw(): void {
    if (!this.listener) return;
    this.listener({ type: 'drawRequested' });
  }

  private handleChow(): void {
    if (!this.listener || !this.currentState) return;
    
    const options = this.calculateMeldOptions().filter(m => m.type === 'Chow');
    if (options.length === 0) return;
    
    // Pick the first (lowest) chow option
    const option = options[0];
    if (!option) return;
    
    this.listener({
      type: 'meldRequested',
      meldType: 'Chow',
      tileIds: option.tileIds,
    });
    
    this.hideClaimButtons();
  }

  private handlePung(): void {
    if (!this.listener || !this.currentState) return;
    
    const options = this.calculateMeldOptions().filter(m => m.type === 'Pung');
    if (options.length === 0) return;
    
    const option = options[0];
    if (!option) return;
    
    this.listener({
      type: 'meldRequested',
      meldType: 'Pung',
      tileIds: option.tileIds,
    });
    
    this.hideClaimButtons();
  }

  private handleKong(): void {
    if (!this.listener || !this.currentState) return;
    
    const options = this.calculateMeldOptions().filter(m => m.type === 'Kong');
    if (options.length === 0) return;
    
    const option = options[0];
    if (!option) return;
    
    this.listener({
      type: 'meldRequested',
      meldType: 'Kong',
      tileIds: option.tileIds,
    });
    
    this.hideClaimButtons();
  }

  private handleWin(): void {
    if (!this.listener) return;
    
    this.listener({
      type: 'winRequested',
      isSelfDraw: false,
    });
    
    this.hideClaimButtons();
  }

  private handleSelfWin(): void {
    if (!this.listener) return;
    
    this.listener({
      type: 'winRequested',
      isSelfDraw: true,
    });
  }

  private handlePass(): void {
    if (!this.listener) return;
    
    this.listener({ type: 'pass' });
    this.hideClaimButtons();
  }
}

export function createHudActions(containerId: string): HudActions {
  return new HudActions(containerId);
}

// View mode button
export class ViewButton {
  private container: HTMLElement;
  private button: HTMLButtonElement;
  private cameraController: CameraController;
  private onToggle: (() => void) | null = null;

  constructor(containerId: string, cameraController: CameraController) {
    const container = document.getElementById(containerId);
    if (!container) {
      throw new Error(`HUD container #${containerId} not found`);
    }
    this.container = container;
    this.cameraController = cameraController;

    // Create button
    this.button = document.createElement('button');
    this.button.id = 'btn-view';
    this.button.style.cssText = `
      position: fixed;
      top: 10px;
      right: 230px;
      padding: 8px 16px;
      background: rgba(74, 144, 226, 0.9);
      color: white;
      border: none;
      border-radius: 4px;
      cursor: pointer;
      font-weight: bold;
      font-family: monospace;
      font-size: 14px;
      z-index: 101;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
    `;
    this.updateLabel();
    
    this.button.addEventListener('click', () => {
      this.cameraController.toggleMode();
      this.updateLabel();
      if (this.onToggle) {
        this.onToggle();
      }
      console.log(`[ViewButton] Mode: ${this.cameraController.getMode()}`);
    });

    document.body.appendChild(this.button);
  }

  setToggleCallback(callback: () => void): void {
    this.onToggle = callback;
  }

  updateLabel(): void {
    const mode = this.cameraController.getMode();
    this.button.textContent = mode === 'flat' ? 'View: Flat' : 'View: Perspective';
  }
}

export function createViewButton(containerId: string, cameraController: CameraController): ViewButton {
  return new ViewButton(containerId, cameraController);
}
