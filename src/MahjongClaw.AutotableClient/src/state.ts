// Client-side game state cache with event-driven updates

export interface TileSummary {
  id: string;
  label: string;
}

export interface PlayerSummary {
  id: string;
  name: string;
  seat: string;
  score: number;
  handCount: number;
}

export interface MeldSummary {
  id: string;
  playerId: string;
  type: string;
  tiles: TileSummary[];
}

export interface DiscardSummary {
  id: string;
  playerId: string;
  tileId: string;
  sequence: number;
  tileLabel: string;
}

export interface GameState {
  id: string;
  status: string;
  currentPlayerId: string | null;
  dealerId: string | null;
  hostPlayerId?: string | null;
  round: number;
  roundWind: string | null;
  ruleset: string;
  wallTotal: number;
  createdAt: string;
  players: PlayerSummary[];
  tilesInPlay: number;
  stateRevision: number;
  melds: MeldSummary[];
  discards: DiscardSummary[];
  viewerHand: TileSummary[];
  turnPhase: string;
  legalActions: string[];
  lastDiscardTileId: string | null;
  lastDiscardPlayerId: string | null;
}

type StateChangeListener = (state: GameState | null) => void;

class ClientState {
  private state: GameState | null = null;
  private listeners: StateChangeListener[] = [];

  getState(): GameState | null {
    return this.state;
  }

  onStateChanged(callback: StateChangeListener): () => void {
    this.listeners.push(callback);
    // Return unsubscribe function
    return () => {
      const index = this.listeners.indexOf(callback);
      if (index !== -1) {
        this.listeners.splice(index, 1);
      }
    };
  }

  applyFullSnapshot(snapshot: GameState): void {
    console.log('[State] Applying full snapshot:', snapshot.id);
    this.state = snapshot;
    this.notifyListeners();
  }

  applySignalREvent(eventName: string, payload: unknown): void {
    console.log(`[State] Applying event: ${eventName}`, payload);
    
    if (!this.state) {
      console.warn('[State] No state to update; ignoring event');
      return;
    }

    // For now, we simply log events. In a full implementation, 
    // we'd parse each event type and update state accordingly.
    // However, the server may send full state updates via SignalR,
    // or we can refetch state after each event.
    // Phase 1 approach: refetch state after events (simple & correct)
    
    // TODO: Implement granular state mutations if server sends deltas
    this.notifyListeners();
  }

  clear(): void {
    this.state = null;
    this.notifyListeners();
  }

  private notifyListeners(): void {
    for (const listener of this.listeners) {
      listener(this.state);
    }
  }
}

export const clientState = new ClientState();
