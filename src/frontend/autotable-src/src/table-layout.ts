export type TableLayoutMode = 'compact' | null;

export function parseTableLayout(search: string): TableLayoutMode {
  const values = new URLSearchParams(search).getAll('tableLayout');
  return values.length === 1 && values[0] === 'compact' ? 'compact' : null;
}

export interface TableLayoutContext {
  connected: boolean;
  connectionMode: string;
  snapshotGameId: string | null;
  lastGameId: string | null;
  changsha: boolean;
  threeDimensional: boolean;
  phase: string | undefined;
}

export function resolveTableLayout(
  mode: TableLayoutMode, context: TableLayoutContext,
): TableLayoutMode {
  if (mode !== 'compact' || !context.connected || context.connectionMode !== 'changsha'
    || !context.changsha || !context.threeDimensional || !context.lastGameId
    || context.snapshotGameId !== context.lastGameId) return null;
  // An allowlist keeps unknown phases and every manual pickup state canonical.
  return typeof context.phase === 'string' && [
    'awaitingdiscard', 'awaitingclaim', 'declaringkong', 'drawingreplacement',
    'scoring', 'endhand', 'rotatingbanker', 'wallexhausted', 'gamecomplete',
  ].includes(context.phase.toLowerCase()) ? 'compact' : null;
}
