// Phase K Wave 23 — Hicks (Frontend).
//
// Operator UI for Bishop's W23 replay-restoration history endpoint:
//
//   GET /api/admin/replays/restoration-history
//     ?tenantId=<id>&sinceIso=<ts>&playerId=<id>
//
// READ-ONLY surface — exposes the history of replay restoration
// attempts triggered by *players* (vs the W21 audit surface which
// shows admin-initiated restores).  Each row is one player's
// request to bring a cold-storage replay back to warm storage,
// including the restoration outcome + cumulative time.  Companions
// the W19 player-initiated request flow; W23 surfaces the
// resulting history for operator audit.
//
// Wire contract:
//   • Auth ladder: 401/403/503 → 200 OK with the listing.
//   • Query params: `tenantId` (optional — empty = global),
//     `sinceIso` (optional — defaults to last 30 days),
//     `playerId` (optional — narrows to a single player).
//   • No X-Admin-Reason required (read-only).

import {
  type AdminSurfaceSpec,
  escapeHtml,
  fmtIso,
} from './admin-shared';

export type RestorationKind =
  | 'restored'
  | 'already-warm'
  | 'cold-miss'
  | 'failed'
  | 'cancelled'
  | 'rate-limited';

interface ReplayRestorationHistoryRow {
  historyId: string;
  tenantId: string;
  replayId: string;
  playerId: string;
  playerName: string;
  outcome: RestorationKind;
  requestedAt: string;
  completedAt?: string;
  durationMs?: number;
  errorDetail?: string;
}

const OUTCOMES: RestorationKind[] = [
  'restored',
  'already-warm',
  'cold-miss',
  'failed',
  'cancelled',
  'rate-limited',
];

function parseRow(raw: unknown): ReplayRestorationHistoryRow | null {
  if (raw === null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const historyId = typeof o.historyId === 'string' ? o.historyId : null;
  const replayId = typeof o.replayId === 'string' ? o.replayId : null;
  const playerId = typeof o.playerId === 'string' ? o.playerId : null;
  const requestedAt = typeof o.requestedAt === 'string' ? o.requestedAt : null;
  if (historyId === null || replayId === null
      || playerId === null || requestedAt === null) return null;
  const outcomeRaw = o.outcome;
  const outcome: RestorationKind = OUTCOMES.includes(outcomeRaw as RestorationKind)
    ? (outcomeRaw as RestorationKind)
    : 'failed';
  return {
    historyId,
    tenantId: typeof o.tenantId === 'string' ? o.tenantId : '',
    replayId,
    playerId,
    playerName: typeof o.playerName === 'string' ? o.playerName : playerId,
    outcome,
    requestedAt,
    completedAt: typeof o.completedAt === 'string' ? o.completedAt : undefined,
    durationMs: typeof o.durationMs === 'number' ? o.durationMs : undefined,
    errorDetail: typeof o.errorDetail === 'string' ? o.errorDetail : undefined,
  };
}

function outcomeClass(o: RestorationKind): string {
  switch (o) {
    case 'restored':       return 'admin-panel-outcome-ok';
    case 'already-warm':   return 'admin-panel-outcome-info';
    case 'cold-miss':
    case 'rate-limited':   return 'admin-panel-outcome-warn';
    case 'failed':
    case 'cancelled':      return 'admin-panel-outcome-err';
  }
}

function fmtDuration(ms: number | undefined): string {
  if (ms === undefined) return '—';
  if (ms < 1000) return `${ms} ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)} s`;
  return `${(ms / 60_000).toFixed(1)} min`;
}

export const REPLAY_RESTORATION_HISTORY_SPEC:
  AdminSurfaceSpec<ReplayRestorationHistoryRow, Record<string, never>> = {
  id: 'replay-restoration-history',
  title: 'Replays · Restoration history (player-initiated)',
  description: 'READ-ONLY view of player-initiated replay '
    + 'restoration requests.  Companions the W21 admin-'
    + 'initiated restoration audit (which shows operator '
    + 'restores).  Defaults to last 30 d.  Use to spot '
    + 'rate-limited players, cold-miss spikes, or repeat-'
    + 'failure patterns.  Audit kind: '
    + 'replays.restoration.history.viewed (informational).',
  endpoint: '/api/admin/replays/restoration-history',
  parseRow,
  rowKey: (r) => r.historyId,
  rowToFormValues: (r) => ({
    tenantId: r.tenantId,
    sinceIso: '',
    playerId: r.playerId,
  }),
  buildBody: () => ({} as Record<string, never>),
  fields: [
    {
      name: 'tenantId',
      label: 'Tenant ID',
      type: 'text',
      required: false,
      placeholder: '(global if blank)',
      help: 'Empty → global view across all tenants.',
    },
    {
      name: 'sinceIso',
      label: 'Since (ISO 8601)',
      type: 'text',
      required: false,
      placeholder: '(last 30 d if blank)',
      help: 'ISO-8601 timestamp.  Empty → last 30 d.',
    },
    {
      name: 'playerId',
      label: 'Player ID',
      type: 'text',
      required: false,
      placeholder: '(all players if blank)',
      help: 'Empty → all players.  Specify to narrow to a '
        + 'single player; useful for rate-limit appeals.',
    },
  ],
  columns: [
    {
      key: 'replayId',
      label: 'Replay',
      render: (r) => ({
        __html: `<code>${escapeHtml(r.replayId)}</code>`,
      }),
    },
    {
      key: 'tenantId',
      label: 'Tenant',
      render: (r) => r.tenantId === ''
        ? ({ __html: '<em class="admin-panel-muted">(global)</em>' })
        : ({ __html: `<code>${escapeHtml(r.tenantId)}</code>` }),
    },
    {
      key: 'playerName',
      label: 'Player',
      render: (r) => ({
        __html: `${escapeHtml(r.playerName)} `
          + `<code class="admin-panel-muted">${escapeHtml(r.playerId)}</code>`,
      }),
    },
    {
      key: 'outcome',
      label: 'Outcome',
      render: (r) => ({
        __html: `<span class="${outcomeClass(r.outcome)}">${escapeHtml(r.outcome)}</span>`,
      }),
    },
    {
      key: 'requestedAt',
      label: 'Requested',
      render: (r) => fmtIso(r.requestedAt),
    },
    {
      key: 'completedAt',
      label: 'Completed',
      render: (r) => fmtIso(r.completedAt),
    },
    {
      key: 'durationMs',
      label: 'Duration',
      render: (r) => fmtDuration(r.durationMs),
    },
    {
      key: 'errorDetail',
      label: 'Error',
      render: (r) => r.errorDetail === undefined
        ? ({ __html: '<span class="admin-panel-muted">—</span>' })
        : ({ __html: `<code class="admin-panel-error">${escapeHtml(r.errorDetail)}</code>` }),
    },
  ],
};
