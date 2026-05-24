// Phase K Wave 23 — Hicks (Frontend).
//
// Operator UI for Bishop's W23 replay upload-monitor endpoint:
//
//   GET /api/admin/replays/upload-monitor?tenantId=<id>&sinceIso=<ts>
//
// READ-ONLY surface — exposes the replay-pipeline upload-stage
// health window (last 24h by default).  Each row is one replay
// upload attempt with the staged-bytes count, the elapsed
// wall-clock from `started` to `completed`, and the final
// outcome so operators can spot stuck uploads / sudden
// quota-exhaustion events.
//
// Wire contract:
//   • Auth ladder: 401/403/503 → 200 OK with the listing.
//   • Query params: `tenantId` (optional — empty = global),
//     `sinceIso` (optional — defaults to last 24h).
//   • No X-Admin-Reason required (read-only).

import {
  type AdminSurfaceSpec,
  escapeHtml,
  fmtIso,
} from './admin-shared';

export type UploadOutcome =
  | 'pending'
  | 'staging'
  | 'completed'
  | 'failed'
  | 'cancelled'
  | 'quota-exceeded';

interface ReplayUploadMonitorRow {
  uploadId: string;
  tenantId: string;
  replayId: string;
  outcome: UploadOutcome;
  startedAt: string;
  completedAt?: string;
  stagedBytes: number;
  durationMs?: number;
  uploadedBy?: string;
  errorDetail?: string;
}

const OUTCOMES: UploadOutcome[] = [
  'pending',
  'staging',
  'completed',
  'failed',
  'cancelled',
  'quota-exceeded',
];

function parseRow(raw: unknown): ReplayUploadMonitorRow | null {
  if (raw === null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const uploadId = typeof o.uploadId === 'string' ? o.uploadId : null;
  const replayId = typeof o.replayId === 'string' ? o.replayId : null;
  const startedAt = typeof o.startedAt === 'string' ? o.startedAt : null;
  if (uploadId === null || replayId === null || startedAt === null) return null;
  const outcomeRaw = o.outcome;
  const outcome: UploadOutcome = OUTCOMES.includes(outcomeRaw as UploadOutcome)
    ? (outcomeRaw as UploadOutcome)
    : 'pending';
  return {
    uploadId,
    tenantId: typeof o.tenantId === 'string' ? o.tenantId : '',
    replayId,
    outcome,
    startedAt,
    completedAt: typeof o.completedAt === 'string' ? o.completedAt : undefined,
    stagedBytes: typeof o.stagedBytes === 'number' ? o.stagedBytes : 0,
    durationMs: typeof o.durationMs === 'number' ? o.durationMs : undefined,
    uploadedBy: typeof o.uploadedBy === 'string' ? o.uploadedBy : undefined,
    errorDetail: typeof o.errorDetail === 'string' ? o.errorDetail : undefined,
  };
}

function outcomeClass(o: UploadOutcome): string {
  switch (o) {
    case 'completed':       return 'admin-panel-outcome-ok';
    case 'pending':
    case 'staging':         return 'admin-panel-outcome-info';
    case 'cancelled':       return 'admin-panel-outcome-warn';
    case 'failed':
    case 'quota-exceeded':  return 'admin-panel-outcome-err';
  }
}

function fmtBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KiB`;
  if (n < 1024 * 1024 * 1024) return `${(n / 1024 / 1024).toFixed(2)} MiB`;
  return `${(n / 1024 / 1024 / 1024).toFixed(2)} GiB`;
}

function fmtDuration(ms: number | undefined): string {
  if (ms === undefined) return '—';
  if (ms < 1000) return `${ms} ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)} s`;
  return `${(ms / 60_000).toFixed(1)} min`;
}

export const REPLAY_UPLOAD_MONITOR_SPEC:
  AdminSurfaceSpec<ReplayUploadMonitorRow, Record<string, never>> = {
  id: 'replay-upload-monitor',
  title: 'Replays · Upload monitor',
  description: 'READ-ONLY view of recent replay upload attempts.  '
    + 'Use to spot stuck uploads (pending/staging > 5 min), '
    + 'sudden failure spikes, or quota-exhaustion events.  '
    + 'Defaults to last 24 h; pass an ISO timestamp in '
    + 'Since-ISO to widen the window.  Audit kind: '
    + 'replays.upload.monitored (informational).',
  endpoint: '/api/admin/replays/upload-monitor',
  parseRow,
  rowKey: (r) => r.uploadId,
  rowToFormValues: (r) => ({
    tenantId: r.tenantId,
    sinceIso: '',
  }),
  buildBody: () => ({} as Record<string, never>),
  fields: [
    {
      name: 'tenantId',
      label: 'Tenant ID',
      type: 'text',
      required: false,
      placeholder: '(global if blank)',
      help: 'Empty → global view across all tenants.  Specify a '
        + 'tenant to scope the listing.',
    },
    {
      name: 'sinceIso',
      label: 'Since (ISO 8601)',
      type: 'text',
      required: false,
      placeholder: '(last 24h if blank)',
      help: 'ISO-8601 timestamp.  Empty → last 24 h.',
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
      key: 'outcome',
      label: 'Outcome',
      render: (r) => ({
        __html: `<span class="${outcomeClass(r.outcome)}">${escapeHtml(r.outcome)}</span>`,
      }),
    },
    {
      key: 'stagedBytes',
      label: 'Staged',
      render: (r) => fmtBytes(r.stagedBytes),
    },
    {
      key: 'durationMs',
      label: 'Duration',
      render: (r) => fmtDuration(r.durationMs),
    },
    {
      key: 'startedAt',
      label: 'Started',
      render: (r) => fmtIso(r.startedAt),
    },
    {
      key: 'completedAt',
      label: 'Completed',
      render: (r) => fmtIso(r.completedAt),
    },
    {
      key: 'uploadedBy',
      label: 'By',
      render: (r) => r.uploadedBy === undefined
        ? ({ __html: '<span class="admin-panel-muted">—</span>' })
        : escapeHtml(r.uploadedBy),
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
