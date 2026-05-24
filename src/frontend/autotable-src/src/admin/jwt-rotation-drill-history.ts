// Phase K Wave 23 — Hicks (Frontend).
//
// Operator UI for Bishop's W23 JWT rotation-drill history endpoint:
//
//   GET /api/admin/jwt-keys/rotation-drill-history
//     ?tenantId=<id>&sinceIso=<ts>
//
// READ-ONLY companion to the W20 rotation-drill *trigger* surface
// (`./jwt-rotation-drill.ts`).  W20 lets the operator FIRE a drill;
// W23 lets them browse the history of fired drills so they can
// confirm the drill cadence + audit which operator initiated each
// drill + measure the drill-to-completion wall-clock for SLA
// reporting.  Audit kind on each row:
// `governance.jwt-keys.rotation-drill.{started,completed,failed}`.
//
// Wire contract:
//   • Auth ladder: 401/403/503 → 200 OK with the history listing.
//   • Query params: `tenantId` (optional — empty = global),
//     `sinceIso` (optional — defaults to last 90 days).
//   • No X-Admin-Reason required (read-only).

import {
  type AdminSurfaceSpec,
  escapeHtml,
  fmtIso,
} from './admin-shared';

export type DrillOutcome =
  | 'completed'
  | 'failed'
  | 'cancelled'
  | 'in-progress';

interface JwtRotationDrillHistoryRow {
  drillId: string;
  tenantId: string;
  outcome: DrillOutcome;
  startedAt: string;
  completedAt?: string;
  durationMs?: number;
  triggeredBy?: string;
  keyId?: string;
  newKeyId?: string;
  notes?: string;
}

const OUTCOMES: DrillOutcome[] = [
  'completed',
  'failed',
  'cancelled',
  'in-progress',
];

function parseRow(raw: unknown): JwtRotationDrillHistoryRow | null {
  if (raw === null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const drillId = typeof o.drillId === 'string' ? o.drillId : null;
  const startedAt = typeof o.startedAt === 'string' ? o.startedAt : null;
  if (drillId === null || startedAt === null) return null;
  const outcomeRaw = o.outcome;
  const outcome: DrillOutcome = OUTCOMES.includes(outcomeRaw as DrillOutcome)
    ? (outcomeRaw as DrillOutcome)
    : 'in-progress';
  return {
    drillId,
    tenantId: typeof o.tenantId === 'string' ? o.tenantId : '',
    outcome,
    startedAt,
    completedAt: typeof o.completedAt === 'string' ? o.completedAt : undefined,
    durationMs: typeof o.durationMs === 'number' ? o.durationMs : undefined,
    triggeredBy: typeof o.triggeredBy === 'string' ? o.triggeredBy : undefined,
    keyId: typeof o.keyId === 'string' ? o.keyId : undefined,
    newKeyId: typeof o.newKeyId === 'string' ? o.newKeyId : undefined,
    notes: typeof o.notes === 'string' ? o.notes : undefined,
  };
}

function outcomeClass(o: DrillOutcome): string {
  switch (o) {
    case 'completed':   return 'admin-panel-outcome-ok';
    case 'in-progress': return 'admin-panel-outcome-info';
    case 'cancelled':   return 'admin-panel-outcome-warn';
    case 'failed':      return 'admin-panel-outcome-err';
  }
}

function fmtDuration(ms: number | undefined): string {
  if (ms === undefined) return '—';
  if (ms < 1000) return `${ms} ms`;
  if (ms < 60_000) return `${(ms / 1000).toFixed(1)} s`;
  return `${(ms / 60_000).toFixed(1)} min`;
}

export const JWT_ROTATION_DRILL_HISTORY_SPEC:
  AdminSurfaceSpec<JwtRotationDrillHistoryRow, Record<string, never>> = {
  id: 'jwt-rotation-drill-history',
  title: 'JWT keys · Rotation drill history',
  description: 'READ-ONLY history of JWT signing-key rotation '
    + 'drills.  Companions the W20 rotation-drill TRIGGER '
    + 'surface — use this listing to confirm cadence, audit '
    + 'which operator fired each drill, and report drill-to-'
    + 'completion wall-clock for SLA targets.  Defaults to '
    + 'last 90 d.  Audit kind: '
    + 'governance.jwt-keys.rotation-drill.{started,completed,failed}.',
  endpoint: '/api/admin/jwt-keys/rotation-drill-history',
  parseRow,
  rowKey: (r) => r.drillId,
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
      help: 'Empty → global view across all tenants.',
    },
    {
      name: 'sinceIso',
      label: 'Since (ISO 8601)',
      type: 'text',
      required: false,
      placeholder: '(last 90 d if blank)',
      help: 'ISO-8601 timestamp.  Empty → last 90 d.',
    },
  ],
  columns: [
    {
      key: 'drillId',
      label: 'Drill ID',
      render: (r) => ({
        __html: `<code>${escapeHtml(r.drillId)}</code>`,
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
      key: 'durationMs',
      label: 'Duration',
      render: (r) => fmtDuration(r.durationMs),
    },
    {
      key: 'keyId',
      label: 'Old key',
      render: (r) => r.keyId === undefined
        ? ({ __html: '<span class="admin-panel-muted">—</span>' })
        : ({ __html: `<code>${escapeHtml(r.keyId)}</code>` }),
    },
    {
      key: 'newKeyId',
      label: 'New key',
      render: (r) => r.newKeyId === undefined
        ? ({ __html: '<span class="admin-panel-muted">—</span>' })
        : ({ __html: `<code>${escapeHtml(r.newKeyId)}</code>` }),
    },
    {
      key: 'triggeredBy',
      label: 'By',
      render: (r) => r.triggeredBy === undefined
        ? ({ __html: '<span class="admin-panel-muted">—</span>' })
        : escapeHtml(r.triggeredBy),
    },
    {
      key: 'notes',
      label: 'Notes',
      render: (r) => r.notes === undefined
        ? ({ __html: '<span class="admin-panel-muted">—</span>' })
        : escapeHtml(r.notes),
    },
  ],
};
