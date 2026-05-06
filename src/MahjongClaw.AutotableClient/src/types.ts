// Placeholder for DTO types matching server contracts

export interface JoinGameResponse {
  playerId: string;
  gameId: string;
  name: string;
  seat: string;
  score: number;
}

export interface StartGameResponse {
  gameId: string;
  status: string;
  dealerId: string;
  currentPlayerId: string;
}

export interface DrawTileResponse {
  tileId: string;
  suit: string;
  rank: number;
  wind?: string;
  dragon?: string;
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
  playerId: string; // First winner for backward compatibility
  changshaWinTypes?: string[];
  isBigWin?: boolean;
  isSmallWin?: boolean;
  scoreDeltas?: Array<{ playerId: string; delta: number; newScore: number }>;
  nextDealerId?: string;
  nextRound?: number;
  birdSeat?: string;
  winners?: Array<{
    playerId: string;
    playerName: string;
    winTypes: string[];
    scoreDelta: number;
    isBigWin: boolean;
    isDealer: boolean;
  }>;
}
