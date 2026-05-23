// Phase J Wave 9 — Per-hand action audit replay tab.
//
// Bishop's Wave-9 endpoint:
//   GET /api/games/{gameId}/audit
//   → 200  { rows: [ { handNumber, turn, seat, source, action,
//                       startedUtc, completedUtc, durationMs,
//                       botScore?, claimDecisionTree? }, ... ] }
//   → 403  caller is not admin (we already feature-detect via
//          /api/auth/me before showing the tab, but treat 403 as
//          "tab unavailable")
//   → 404  endpoint not deployed
//
// Visibility is gated on:
//   1. `/api/auth/me` returning `claims.role === 'admin'` (or
//      `roles` containing `admin` — defensive shape support).
//   2. Bishop's audit endpoint not 404ing.
//
// The tab DOM lives in index.html (`#replay-tab-audit`,
// `#replay-pane-audit`) — initially `display:none` until the
// admin check passes.  This module owns:
//   • the click handler that activates the tab
//   • the fetch + row rendering inside `#replay-audit-table`
//   • the admin probe + showAuditTabIfAdmin() helper

import { t, onLanguageChange } from './i18n';

// ── Types ──────────────────────────────────────────────────────────

export interface AuditRow {
  handNumber: number;
  turn: number;
  seat: number;
  source: 'human' | 'bot' | 'system' | string;
  botTier?: string;
  action: string;
  durationMs: number | null;
  botScore?: number | null;
  claimDecisionTree?: string | null;
}

interface AuditResponse {
  rows: AuditRow[];
}

// ── Module state ───────────────────────────────────────────────────

let installed = false;
let activeGameId: string | null = null;
let isAdmin = false;
let probedAdmin = false;
let currentTab: 'replay' | 'audit' = 'replay';

// ── Admin probe ────────────────────────────────────────────────────

interface MeResponse {
  authenticated?: boolean;
  Authenticated?: boolean;
  claims?: { role?: string; roles?: string[] } | Record<string, unknown>;
  Claims?: { role?: string; roles?: string[] } | Record<string, unknown>;
  role?: string;
  Role?: string;
  roles?: string[];
  Roles?: string[];
}

function isAdminPayload(raw: unknown): boolean {
  if (raw === null || typeof raw !== 'object') return false;
  const me = raw as MeResponse;
  const claims = me.claims ?? me.Claims;
  if (claims !== undefined && claims !== null && typeof claims === 'object') {
    const c = claims as Record<string, unknown>;
    if (typeof c.role === 'string' && c.role.toLowerCase() === 'admin') return true;
    if (Array.isArray(c.roles) && c.roles.some((r) => String(r).toLowerCase() === 'admin')) return true;
  }
  const role = me.role ?? me.Role;
  if (typeof role === 'string' && role.toLowerCase() === 'admin') return true;
  const roles = me.roles ?? me.Roles;
  if (Array.isArray(roles) && roles.some((r) => String(r).toLowerCase() === 'admin')) return true;
  return false;
}

async function probeAdmin(): Promise<boolean> {
  if (probedAdmin) return isAdmin;
  probedAdmin = true;
  try {
    const r = await fetch('/api/auth/me', {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
    });
    if (!r.ok) return false;
    const body = await r.json() as unknown;
    isAdmin = isAdminPayload(body);
    return isAdmin;
  } catch {
    return false;
  }
}

// ── Fetch ──────────────────────────────────────────────────────────

async function fetchAudit(gameId: string): Promise<{ status: number; rows: AuditRow[] }> {
  try {
    const r = await fetch(`/api/games/${encodeURIComponent(gameId)}/audit`, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
    });
    if (r.status === 404 || r.status === 403) {
      return { status: r.status, rows: [] };
    }
    if (!r.ok) return { status: r.status, rows: [] };
    const body = await r.json() as unknown;
    return { status: r.status, rows: normaliseRows(body) };
  } catch {
    return { status: 0, rows: [] };
  }
}

function normaliseRows(raw: unknown): AuditRow[] {
  if (raw === null || typeof raw !== 'object') return [];
  const o = raw as Record<string, unknown>;
  const rows = Array.isArray(o.rows) ? o.rows
    : (Array.isArray(o.Rows) ? o.Rows
      : (Array.isArray(raw) ? raw : []));
  const out: AuditRow[] = [];
  for (const r of rows) {
    if (r === null || typeof r !== 'object') continue;
    const row = r as Record<string, unknown>;
    out.push({
      handNumber: typeof row.handNumber === 'number' ? row.handNumber
        : (typeof row.HandNumber === 'number' ? row.HandNumber : 0),
      turn: typeof row.turn === 'number' ? row.turn
        : (typeof row.Turn === 'number' ? row.Turn : 0),
      seat: typeof row.seat === 'number' ? row.seat
        : (typeof row.Seat === 'number' ? row.Seat : -1),
      source: typeof row.source === 'string' ? row.source
        : (typeof row.Source === 'string' ? row.Source : 'system'),
      botTier: typeof row.botTier === 'string' ? row.botTier
        : (typeof row.BotTier === 'string' ? row.BotTier : undefined),
      action: typeof row.action === 'string' ? row.action
        : (typeof row.Action === 'string' ? row.Action : ''),
      durationMs: typeof row.durationMs === 'number' ? row.durationMs
        : (typeof row.DurationMs === 'number' ? row.DurationMs : null),
      botScore: typeof row.botScore === 'number' ? row.botScore
        : (typeof row.BotScore === 'number' ? row.BotScore : null),
      claimDecisionTree: typeof row.claimDecisionTree === 'string'
        ? row.claimDecisionTree
        : (typeof row.ClaimDecisionTree === 'string' ? row.ClaimDecisionTree : null),
    });
  }
  return out;
}

// ── Rendering ──────────────────────────────────────────────────────

function formatDuration(ms: number | null): string {
  if (ms === null || ms === undefined || !isFinite(ms)) return '—';
  if (ms < 1000) return `${Math.round(ms)} ms`;
  return `${(ms / 1000).toFixed(2)} s`;
}

function renderRows(rows: AuditRow[]): void {
  const tableEl = document.getElementById('replay-audit-table');
  const emptyEl = document.getElementById('replay-audit-empty');
  if (tableEl === null || emptyEl === null) return;
  tableEl.replaceChildren();
  emptyEl.textContent = '';

  if (rows.length === 0) {
    emptyEl.textContent = t('replay.audit.empty');
    return;
  }

  // Header row.
  const header = document.createElement('div');
  header.className = 'replay-audit-row replay-audit-header';
  for (const label of [
    '#',
    t('replay.audit.col.source'),
    t('replay.audit.col.action'),
    t('replay.audit.col.duration'),
    t('replay.audit.col.score'),
    t('replay.audit.col.decision'),
  ]) {
    const cell = document.createElement('span');
    cell.textContent = label;
    header.appendChild(cell);
  }
  tableEl.appendChild(header);

  rows.forEach((row, i) => {
    const rowEl = document.createElement('div');
    rowEl.className = 'replay-audit-row';
    rowEl.setAttribute('role', 'row');
    rowEl.setAttribute('data-testid', `replay-audit-row-${i}`);

    const idxCell = document.createElement('span');
    idxCell.textContent = `H${row.handNumber}·T${row.turn}·S${row.seat}`;
    rowEl.appendChild(idxCell);

    const sourceCell = document.createElement('span');
    const src = (row.source || 'system').toLowerCase();
    sourceCell.classList.add(`replay-audit-source-${src}`);
    sourceCell.setAttribute('data-testid', `replay-audit-row-${i}-source`);
    sourceCell.textContent = row.botTier !== undefined && row.botTier !== ''
      ? `${row.source} (${row.botTier})`
      : row.source;
    rowEl.appendChild(sourceCell);

    const actionCell = document.createElement('span');
    actionCell.textContent = row.action;
    rowEl.appendChild(actionCell);

    const durationCell = document.createElement('span');
    durationCell.setAttribute('data-testid', `replay-audit-row-${i}-duration`);
    durationCell.textContent = formatDuration(row.durationMs);
    rowEl.appendChild(durationCell);

    const scoreCell = document.createElement('span');
    scoreCell.setAttribute('data-testid', `replay-audit-row-${i}-score`);
    scoreCell.textContent = row.botScore !== null && row.botScore !== undefined
      ? row.botScore.toFixed(2)
      : '—';
    rowEl.appendChild(scoreCell);

    const decisionCell = document.createElement('span');
    decisionCell.textContent = row.claimDecisionTree ?? '—';
    rowEl.appendChild(decisionCell);

    tableEl.appendChild(rowEl);
  });
}

function activateTab(which: 'replay' | 'audit'): void {
  currentTab = which;
  const replayTab = document.getElementById('replay-tab-replay');
  const auditTab = document.getElementById('replay-tab-audit');
  const replayPane = document.getElementById('replay-pane-replay');
  const auditPane = document.getElementById('replay-pane-audit');
  if (replayTab !== null) {
    replayTab.classList.toggle('replay-tab-active', which === 'replay');
    replayTab.setAttribute('aria-selected', which === 'replay' ? 'true' : 'false');
  }
  if (auditTab !== null) {
    auditTab.classList.toggle('replay-tab-active', which === 'audit');
    auditTab.setAttribute('aria-selected', which === 'audit' ? 'true' : 'false');
  }
  if (replayPane !== null) {
    replayPane.classList.toggle('replay-pane-active', which === 'replay');
    if (which === 'replay') {
      replayPane.removeAttribute('hidden');
    } else {
      replayPane.setAttribute('hidden', '');
    }
  }
  if (auditPane !== null) {
    auditPane.classList.toggle('replay-pane-active', which === 'audit');
    if (which === 'audit') {
      auditPane.removeAttribute('hidden');
    } else {
      auditPane.setAttribute('hidden', '');
    }
  }
}

// ── Public API ─────────────────────────────────────────────────────

/**
 * Wire the audit tab.  Idempotent.  Calls into `probeAdmin()` once
 * and reveals the tab only when the caller is an admin.
 */
export function installAuditTab(): void {
  if (installed) return;
  installed = true;
  const auditTab = document.getElementById('replay-tab-audit') as HTMLButtonElement | null;
  const replayTab = document.getElementById('replay-tab-replay') as HTMLButtonElement | null;
  if (auditTab !== null) {
    auditTab.addEventListener('click', () => {
      activateTab('audit');
      if (activeGameId !== null) {
        void loadAuditForGame(activeGameId);
      }
    });
  }
  if (replayTab !== null) {
    replayTab.addEventListener('click', () => activateTab('replay'));
  }
  void probeAdmin().then((admin) => {
    if (admin && auditTab !== null) {
      auditTab.style.display = '';
    }
  });

  // Phase J Wave 9 — re-render the audit table on language change so
  // column headers + empty/unavailable placeholders reflect the new
  // locale without requiring a navigation.
  onLanguageChange(() => {
    if (activeGameId !== null && currentTab === 'audit') {
      void loadAuditForGame(activeGameId);
    }
  });
}

/**
 * Called by replay.ts (via the `setAuditGameId` hook) when a replay
 * opens — so the Audit tab knows which game's audit rows to fetch.
 * Pre-loads the rows if the tab is already active (so a refresh keeps
 * showing the right data); otherwise loads on first click.
 */
export function setAuditGameId(gameId: string | null): void {
  activeGameId = gameId;
  // Reset to replay tab on new game.
  if (currentTab === 'audit') {
    activateTab('replay');
  }
  // Clear the rows so a stale set doesn't briefly show through.
  const tableEl = document.getElementById('replay-audit-table');
  if (tableEl !== null) tableEl.replaceChildren();
  const emptyEl = document.getElementById('replay-audit-empty');
  if (emptyEl !== null) emptyEl.textContent = '';
}

async function loadAuditForGame(gameId: string): Promise<void> {
  const emptyEl = document.getElementById('replay-audit-empty');
  if (emptyEl !== null) emptyEl.textContent = t('common.loading');
  const result = await fetchAudit(gameId);
  if (result.status === 404 || result.status === 403) {
    if (emptyEl !== null) emptyEl.textContent = t('replay.audit.unavailable');
    return;
  }
  renderRows(result.rows);
}

/** Force a re-probe on auth state changes (e.g. post-login). */
export function refreshAdminStatus(): void {
  probedAdmin = false;
  void probeAdmin().then((admin) => {
    const auditTab = document.getElementById('replay-tab-audit') as HTMLButtonElement | null;
    if (auditTab !== null) {
      auditTab.style.display = admin ? '' : 'none';
    }
  });
}
