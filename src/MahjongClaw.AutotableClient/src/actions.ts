// REST API action wrappers for game interactions

const API_BASE = import.meta.env.VITE_API_BASE || 'http://localhost:5270';

export interface ActionResult<T> {
  success: boolean;
  data?: T;
  error?: string;
  status?: number;
}

export interface DrawTileResponse {
  tileId: string;
  suit: string;
  rank: number;
  wind: string | null;
  dragon: string | null;
}

export interface DiscardTileResponse {
  discardId: string;
  tileId: string;
  sequence: number;
}

export interface MeldResponse {
  meldId: string;
  type: string;
  tileIds: string[];
}

export interface WinResponse {
  gameId: string;
  status: string;
  winnerId: string;
}

export async function drawTile(
  gameId: string,
  playerId: string
): Promise<ActionResult<DrawTileResponse>> {
  try {
    const response = await fetch(`${API_BASE}/api/game/${gameId}/draw`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      return {
        success: false,
        error: errorData.message || `HTTP ${response.status}`,
        status: response.status,
      };
    }

    const data = await response.json();
    return { success: true, data };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Network error',
    };
  }
}

export async function discardTile(
  gameId: string,
  playerId: string,
  tileId: string
): Promise<ActionResult<DiscardTileResponse>> {
  try {
    const response = await fetch(`${API_BASE}/api/game/${gameId}/discard`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId, tileId }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      return {
        success: false,
        error: errorData.message || `HTTP ${response.status}`,
        status: response.status,
      };
    }

    const data = await response.json();
    return { success: true, data };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Network error',
    };
  }
}

export async function declareMeld(
  gameId: string,
  playerId: string,
  meldType: string,
  tileIds: string[]
): Promise<ActionResult<MeldResponse>> {
  try {
    const response = await fetch(`${API_BASE}/api/game/${gameId}/meld`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ playerId, type: meldType, tileIds }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      return {
        success: false,
        error: errorData.message || `HTTP ${response.status}`,
        status: response.status,
      };
    }

    const data = await response.json();
    return { success: true, data };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Network error',
    };
  }
}

export async function declareWin(
  gameId: string,
  playerId: string,
  isSelfDraw: boolean,
  discarderPlayerId: string | null
): Promise<ActionResult<WinResponse>> {
  try {
    const response = await fetch(`${API_BASE}/api/game/${gameId}/win`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        playerId,
        isSelfDraw,
        discarderPlayerId,
      }),
    });

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      return {
        success: false,
        error: errorData.message || `HTTP ${response.status}`,
        status: response.status,
      };
    }

    const data = await response.json();
    return { success: true, data };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Network error',
    };
  }
}
