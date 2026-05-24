// Phase K Wave 23 — Hicks (Frontend).
//
// Operator UI for Bishop's W23 audit-log purge endpoint:
//
//   POST /api/admin/audit-log/purge
//   body: { tenantId, olderThanIso, kindFilter?, confirm }
//
// One-way TRAPDOOR surface — purges audit-log rows older than a
// cut-off timestamp.  Companion to the W22 `AUDIT_LOG_SEARCH_SPEC`
// READ surface; the W23 purge complements it with a controlled
// deletion path so operators can free archive storage after legal
// retention windows expire.  The purge is:
//
//   • Irreversible (rows are HARD-DELETED, not soft-deleted).
//   • Mandatory X-Admin-Reason on the request.
//   • Multi-confirm: the form requires the operator to type the
//     literal string `PURGE` into a confirm field.
//
// Wire contract:
//   • Auth ladder: 401/403/503 → 200 OK with the purge manifest.
//   • Body:
//     - `tenantId` (required) — global purge if empty string.
//     - `olderThanIso` (required) — ISO timestamp; rows with
//       `createdAt < olderThanIso` are purged.
//     - `kindFilter` (optional) — narrow to a single audit-kind
//       prefix (e.g. `replays.upload.`).
//     - `confirm` — operator must type `PURGE`.

import {
  type AdminSurfaceSpec,
  escapeHtml,
  fmtIso,
} from './admin-shared';

interface AuditLogPurgeRow {
  // Each "row" is one prior purge manifest; the surface
  // shows historical purges + lets the operator fire a new one.
  purgeId: string;
  tenantId: string;
  olderThanIso: string;
  kindFilter: string;
  purgedRows: number;
  purgedBytes: number;
  startedAt: string;
  completedAt?: string;
  firedBy?: string;
  reason: string;
}

interface AuditLogPurgeBody {
  tenantId: string;
  olderThanIso: string;
  kindFilter: string;
  confirm: string;
  reason: string;
}

function parseRow(raw: unknown): AuditLogPurgeRow | null {
  if (raw === null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const purgeId = typeof o.purgeId === 'string' ? o.purgeId : null;
  const olderThanIso = typeof o.olderThanIso === 'string' ? o.olderThanIso : null;
  const startedAt = typeof o.startedAt === 'string' ? o.startedAt : null;
  if (purgeId === null || olderThanIso === null || startedAt === null) return null;
  return {
    purgeId,
    tenantId: typeof o.tenantId === 'string' ? o.tenantId : '',
    olderThanIso,
    kindFilter: typeof o.kindFilter === 'string' ? o.kindFilter : '',
    purgedRows: typeof o.purgedRows === 'number' ? o.purgedRows : 0,
    purgedBytes: typeof o.purgedBytes === 'number' ? o.purgedBytes : 0,
    startedAt,
    completedAt: typeof o.completedAt === 'string' ? o.completedAt : undefined,
    firedBy: typeof o.firedBy === 'string' ? o.firedBy : undefined,
    reason: typeof o.reason === 'string' ? o.reason : '',
  };
}

function fmtBytes(n: number): string {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KiB`;
  if (n < 1024 * 1024 * 1024) return `${(n / 1024 / 1024).toFixed(2)} MiB`;
  return `${(n / 1024 / 1024 / 1024).toFixed(2)} GiB`;
}

export const AUDIT_LOG_PURGE_UI_SPEC:
  AdminSurfaceSpec<AuditLogPurgeRow, AuditLogPurgeBody> = {
  id: 'audit-log-purge-ui',
  title: 'Audit log · Purge (trapdoor)',
  description: 'One-way HARD-DELETE purge of audit-log rows '
    + 'older than the cut-off.  Use after legal retention '
    + 'windows expire to free archive storage.  Type "PURGE" '
    + 'into the confirm field to enable the submit.  '
    + 'X-Admin-Reason header is mandatory.  Audit kind: '
    + 'governance.audit-log.purge.fired.',
  endpoint: '/api/admin/audit-log/purge',
  parseRow,
  rowKey: (r) => r.purgeId,
  rowToFormValues: (r) => ({
    tenantId: r.tenantId,
    olderThanIso: r.olderThanIso,
    kindFilter: r.kindFilter,
    confirm: '',
    reason: '',
  }),
  buildBody: (v) => {
    const olderThanIso = (v.olderThanIso ?? '').trim();
    if (olderThanIso === '') {
      throw new Error('olderThanIso is required (ISO 8601 timestamp)');
    }
    const confirm = (v.confirm ?? '').trim();
    if (confirm !== 'PURGE') {
      throw new Error('confirm field must be literally PURGE — abort');
    }
    const reason = (v.reason ?? '').trim();
    if (reason === '') {
      throw new Error('purge reason is required');
    }
    return {
      tenantId: (v.tenantId ?? '').trim(),
      olderThanIso,
      kindFilter: (v.kindFilter ?? '').trim(),
      confirm,
      reason,
    };
  },
  fields: [
    {
      name: 'tenantId',
      label: 'Tenant ID',
      type: 'text',
      required: false,
      placeholder: '(global if blank)',
      help: 'Empty → purge across all tenants.  Specifying a '
        + 'tenant scopes the purge to that tenant only.',
    },
    {
      name: 'olderThanIso',
      label: 'Older than (ISO 8601)',
      type: 'text',
      required: true,
      placeholder: '2024-01-01T00:00:00Z',
      help: 'Required.  Rows with createdAt < this timestamp '
        + 'are hard-deleted.  Use the start of the retention '
        + 'cliff (e.g. 7y ago).',
    },
    {
      name: 'kindFilter',
      label: 'Kind-filter prefix',
      type: 'text',
      required: false,
      placeholder: 'e.g. replays.upload.',
      help: 'Empty → all kinds.  A dotted prefix narrows the '
        + 'purge to a single audit-kind family.',
    },
    {
      name: 'confirm',
      label: 'Type PURGE to confirm',
      type: 'text',
      required: true,
      placeholder: 'PURGE',
      help: 'Must equal the literal string "PURGE" (uppercase) '
        + 'before the submit is enabled.  Guards against typo-'
        + 'driven misfires; the purge is irreversible.',
    },
    {
      name: 'reason',
      label: 'Purge reason',
      type: 'text',
      required: true,
      placeholder: 'e.g. retention-cliff-2024Q1',
      help: 'Stamped onto the audit log.  Required.',
    },
  ],
  columns: [
    {
      key: 'purgeId',
      label: 'Purge ID',
      render: (r) => ({
        __html: `<code>${escapeHtml(r.purgeId)}</code>`,
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
      key: 'olderThanIso',
      label: 'Cut-off',
      render: (r) => fmtIso(r.olderThanIso),
    },
    {
      key: 'kindFilter',
      label: 'Kind',
      render: (r) => r.kindFilter === ''
        ? ({ __html: '<em class="admin-panel-muted">(all)</em>' })
        : ({ __html: `<code>${escapeHtml(r.kindFilter)}</code>` }),
    },
    {
      key: 'purgedRows',
      label: 'Rows',
      render: (r) => r.purgedRows.toLocaleString(),
    },
    {
      key: 'purgedBytes',
      label: 'Bytes',
      render: (r) => fmtBytes(r.purgedBytes),
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
      key: 'firedBy',
      label: 'By',
      render: (r) => r.firedBy === undefined
        ? ({ __html: '<span class="admin-panel-muted">—</span>' })
        : escapeHtml(r.firedBy),
    },
    {
      key: 'reason',
      label: 'Reason',
      render: (r) => escapeHtml(r.reason),
    },
  ],
};
