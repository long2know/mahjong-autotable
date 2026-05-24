// Phase K Wave 23 — Hicks (Frontend).
//
// Operator UI for Bishop's W23 SignalR groups-dashboard endpoint:
//
//   GET /api/admin/signalr/groups-dashboard
//     ?tenantId=<id>&groupPrefix=<prefix>
//
// READ-ONLY surface — exposes a per-group view of the SignalR
// hub's open groups (table presence rooms, spectator rooms,
// commentary rooms).  Each row is one group with its current
// connection count, the active members' display-names, and the
// last time a message was broadcast.  Operators use this to spot
// runaway group population (post-mortem on a thundering-herd
// disconnect) and to confirm that orphaned groups have been
// reaped by the W19 purge.
//
// Wire contract:
//   • Auth ladder: 401/403/503 → 200 OK with the groups listing.
//   • Query params: `tenantId` (optional — empty = global),
//     `groupPrefix` (optional — defaults to all groups).
//   • No X-Admin-Reason required (read-only).

import {
  type AdminSurfaceSpec,
  escapeHtml,
  fmtIso,
} from './admin-shared';

export type GroupKind =
  | 'table'
  | 'spectator'
  | 'commentary'
  | 'tournament'
  | 'admin'
  | 'unknown';

interface SignalrGroupRow {
  groupName: string;
  tenantId: string;
  kind: GroupKind;
  connectionCount: number;
  memberCount: number;
  sampleMembers: string[];
  lastBroadcastAt?: string;
  createdAt: string;
}

const KINDS: GroupKind[] = [
  'table',
  'spectator',
  'commentary',
  'tournament',
  'admin',
  'unknown',
];

function parseRow(raw: unknown): SignalrGroupRow | null {
  if (raw === null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const groupName = typeof o.groupName === 'string' ? o.groupName : null;
  const createdAt = typeof o.createdAt === 'string' ? o.createdAt : null;
  if (groupName === null || createdAt === null) return null;
  const kindRaw = o.kind;
  const kind: GroupKind = KINDS.includes(kindRaw as GroupKind)
    ? (kindRaw as GroupKind)
    : 'unknown';
  const sample: string[] = Array.isArray(o.sampleMembers)
    ? (o.sampleMembers as unknown[]).filter((x): x is string => typeof x === 'string')
    : [];
  return {
    groupName,
    tenantId: typeof o.tenantId === 'string' ? o.tenantId : '',
    kind,
    connectionCount: typeof o.connectionCount === 'number' ? o.connectionCount : 0,
    memberCount: typeof o.memberCount === 'number' ? o.memberCount : 0,
    sampleMembers: sample,
    lastBroadcastAt: typeof o.lastBroadcastAt === 'string'
      ? o.lastBroadcastAt : undefined,
    createdAt,
  };
}

function kindClass(k: GroupKind): string {
  switch (k) {
    case 'table':
    case 'spectator':
    case 'commentary':  return 'admin-panel-outcome-ok';
    case 'tournament':  return 'admin-panel-outcome-info';
    case 'admin':       return 'admin-panel-outcome-warn';
    case 'unknown':     return 'admin-panel-outcome-err';
  }
}

export const SIGNALR_GROUPS_DASHBOARD_SPEC:
  AdminSurfaceSpec<SignalrGroupRow, Record<string, never>> = {
  id: 'signalr-groups-dashboard',
  title: 'SignalR · Groups dashboard',
  description: 'READ-ONLY view of open SignalR groups.  Each row '
    + 'is one group with its current connection / member count '
    + 'and sample members.  Use to spot runaway populations '
    + '(thundering-herd post-mortem) and to confirm orphaned '
    + 'groups have been reaped by the W19 purge surface.  '
    + 'Audit kind: signalr.groups.dashboard.viewed (informational).',
  endpoint: '/api/admin/signalr/groups-dashboard',
  parseRow,
  rowKey: (r) => r.groupName,
  rowToFormValues: (r) => ({
    tenantId: r.tenantId,
    groupPrefix: '',
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
      name: 'groupPrefix',
      label: 'Group-name prefix',
      type: 'text',
      required: false,
      placeholder: 'e.g. table- / spectator- / commentary-',
      help: 'Empty → all groups.  Filter by prefix to focus on '
        + 'a single group family.',
    },
  ],
  columns: [
    {
      key: 'groupName',
      label: 'Group',
      render: (r) => ({
        __html: `<code>${escapeHtml(r.groupName)}</code>`,
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
      key: 'kind',
      label: 'Kind',
      render: (r) => ({
        __html: `<span class="${kindClass(r.kind)}">${escapeHtml(r.kind)}</span>`,
      }),
    },
    {
      key: 'connectionCount',
      label: 'Connections',
      render: (r) => String(r.connectionCount),
    },
    {
      key: 'memberCount',
      label: 'Members',
      render: (r) => String(r.memberCount),
    },
    {
      key: 'sampleMembers',
      label: 'Sample',
      render: (r) => r.sampleMembers.length === 0
        ? ({ __html: '<span class="admin-panel-muted">—</span>' })
        : ({
            __html: r.sampleMembers
              .slice(0, 5)
              .map((m) => `<code>${escapeHtml(m)}</code>`)
              .join(', '),
          }),
    },
    {
      key: 'createdAt',
      label: 'Created',
      render: (r) => fmtIso(r.createdAt),
    },
    {
      key: 'lastBroadcastAt',
      label: 'Last broadcast',
      render: (r) => fmtIso(r.lastBroadcastAt),
    },
  ],
};
