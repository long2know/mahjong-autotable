// Game end overlay showing winner and settlement

import type { GameState, TileSummary } from './state';

export class GameEndOverlay {
  private container: HTMLElement | null = null;
  private visible = false;

  show(state: GameState): void {
    if (this.visible) {
      return;
    }

    const winner = state.players.find(p => p.id === state.currentPlayerId);
    if (!winner && state.status !== 'Completed') {
      // No clear winner data; don't show overlay
      return;
    }

    this.visible = true;

    // Create overlay container
    this.container = document.createElement('div');
    this.container.id = 'game-end-overlay';
    this.container.style.cssText = `
      position: fixed;
      top: 0;
      left: 0;
      width: 100%;
      height: 100%;
      background: rgba(0, 0, 0, 0.8);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 2000;
      font-family: monospace;
    `;

    // Create content box
    const contentBox = document.createElement('div');
    contentBox.style.cssText = `
      background: rgba(20, 20, 20, 0.95);
      padding: 40px;
      border-radius: 12px;
      border: 2px solid #4a90e2;
      box-shadow: 0 8px 16px rgba(0, 0, 0, 0.5);
      max-width: 600px;
      color: white;
    `;

    // Title
    const title = document.createElement('h2');
    title.textContent = winner ? `Winner: ${winner.name}` : 'Game Complete';
    title.style.cssText = `
      margin: 0 0 20px 0;
      font-size: 28px;
      color: #4a90e2;
      text-align: center;
    `;
    contentBox.appendChild(title);

    // Winner info
    if (winner) {
      const infoDiv = document.createElement('div');
      infoDiv.style.cssText = `
        margin: 20px 0;
        padding: 15px;
        background: rgba(255, 255, 255, 0.05);
        border-radius: 6px;
      `;

      const seatInfo = document.createElement('p');
      seatInfo.textContent = `Seat: ${winner.seat}`;
      seatInfo.style.cssText = 'margin: 5px 0; font-size: 16px;';
      infoDiv.appendChild(seatInfo);

      const scoreInfo = document.createElement('p');
      scoreInfo.textContent = `Score: ${winner.score}`;
      scoreInfo.style.cssText = 'margin: 5px 0; font-size: 16px;';
      infoDiv.appendChild(scoreInfo);

      contentBox.appendChild(infoDiv);
    }

    // Winning hand (if viewer is winner or spectator can see all)
    if (winner && state.viewerHand && state.viewerHand.length > 0) {
      const handDiv = document.createElement('div');
      handDiv.style.cssText = `
        margin: 20px 0;
        padding: 15px;
        background: rgba(255, 255, 255, 0.05);
        border-radius: 6px;
      `;

      const handTitle = document.createElement('h3');
      handTitle.textContent = 'Winning Hand:';
      handTitle.style.cssText = `
        margin: 0 0 10px 0;
        font-size: 18px;
        color: #f39c12;
      `;
      handDiv.appendChild(handTitle);

      const tileList = document.createElement('div');
      tileList.style.cssText = `
        display: flex;
        flex-wrap: wrap;
        gap: 8px;
      `;

      // Show melds
      const playerMelds = state.melds.filter(m => m.playerId === winner.id);
      for (const meld of playerMelds) {
        const meldContainer = document.createElement('div');
        meldContainer.style.cssText = `
          display: flex;
          gap: 4px;
          padding: 5px;
          background: rgba(255, 255, 255, 0.1);
          border-radius: 4px;
        `;

        for (const tile of meld.tiles) {
          const tileSpan = this.createTileSpan(tile);
          meldContainer.appendChild(tileSpan);
        }

        tileList.appendChild(meldContainer);
      }

      // Show concealed tiles
      if (state.viewerHand.length > 0) {
        const concealedContainer = document.createElement('div');
        concealedContainer.style.cssText = `
          display: flex;
          gap: 4px;
          padding: 5px;
          background: rgba(255, 255, 255, 0.08);
          border-radius: 4px;
        `;

        for (const tile of state.viewerHand) {
          const tileSpan = this.createTileSpan(tile);
          concealedContainer.appendChild(tileSpan);
        }

        tileList.appendChild(concealedContainer);
      }

      handDiv.appendChild(tileList);
      contentBox.appendChild(handDiv);
    }

    // Close button
    const closeButton = document.createElement('button');
    closeButton.textContent = 'Close';
    closeButton.style.cssText = `
      display: block;
      margin: 20px auto 0 auto;
      padding: 10px 30px;
      background: #4a90e2;
      color: white;
      border: none;
      border-radius: 6px;
      font-size: 16px;
      font-weight: bold;
      cursor: pointer;
      transition: background 0.2s;
    `;
    closeButton.addEventListener('mouseenter', () => {
      closeButton.style.background = '#3a7bc8';
    });
    closeButton.addEventListener('mouseleave', () => {
      closeButton.style.background = '#4a90e2';
    });
    closeButton.addEventListener('click', () => {
      this.hide();
    });

    contentBox.appendChild(closeButton);
    this.container.appendChild(contentBox);
    document.body.appendChild(this.container);
  }

  private createTileSpan(tile: TileSummary): HTMLSpanElement {
    const span = document.createElement('span');
    span.textContent = tile.label;
    span.style.cssText = `
      padding: 4px 8px;
      background: rgba(255, 255, 255, 0.15);
      border-radius: 3px;
      font-size: 14px;
      white-space: nowrap;
    `;
    return span;
  }

  hide(): void {
    if (!this.visible || !this.container) {
      return;
    }

    if (this.container.parentNode === document.body) {
      document.body.removeChild(this.container);
    }

    this.container = null;
    this.visible = false;
  }

  isVisible(): boolean {
    return this.visible;
  }
}

export const gameEndOverlay = new GameEndOverlay();
