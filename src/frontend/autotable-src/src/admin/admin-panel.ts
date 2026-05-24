// Phase K Wave 18 — Hicks (Frontend).
//
// Operator admin panel — single entry point that tabs across the
// three W17 Bishop CRUD surfaces (replay retention, JWKS rotation,
// SignalR retention).  Lazy-loaded behind `?action=admin-panel`
// (see `action-router.ts:dispatchAdminPanel()`).
//
// Wire contract: see `admin-shared.ts` for the auth ladder, the
// `X-Admin-Reason` header convention, and the `AdminSurfaceSpec`
// descriptor shape.  Each surface module ships a const
// `*_SPEC` that this entry consumes uniformly.
//
// Bundle math: vite.config.ts:manualChunks routes everything under
// `src/admin/` into a single `admin-panel.<hash>.js` chunk, with a
// W18 ceiling of ≤ 40 KB.  As more admin surfaces accumulate
// (Phase L cost-dashboard, audit drill-downs, etc.) the chunk
// grows linearly — Hicks W19+ can split if/when the chunk
// approaches the ceiling.

import {
  type AdminSurfaceSpec,
  ADMIN_REASON_HEADER,
  closeAdminOverlay,
  collectFormValues,
  escapeHtml,
  gateAdminFetch,
  mountAdminOverlay,
  promptAdminReason,
  renderAdminFormHtml,
  renderAdminListHtml,
} from './admin-shared';
import { REPLAY_RETENTION_SPEC } from './replay-retention';
import { JWKS_ROTATION_SPEC } from './jwks-rotation';
import { SIGNALR_RETENTION_SPEC } from './signalr-retention';
import { ROTATION_POLICY_BULK_SPEC } from './rotation-policy-bulk';
import { REPLAY_INTEGRITY_AUDIT_SPEC } from './replay-integrity-audit';
import { SWISS_PAIRING_AUDIT_SPEC } from './swiss-pairing-audit';

interface AnySpec extends AdminSurfaceSpec<unknown, unknown> {}

// Type cast: the concrete specs are heterogeneous over their row /
// body types but uniform over the API used by this entry.  We
// erase the generics at the registry level and re-cast at the call
// sites that matter (row parsing returns `unknown`, which the
// per-surface `parseRow` narrows internally — the shared runtime
// never has to know the concrete row type).
//
// Phase K Wave 19 — three Bishop W19 surfaces added at the tail of
// the registry: rotation-policy bulk-update, replay-store integrity
// audit, and tournament Swiss pairing audit log.
const SURFACES: ReadonlyArray<AnySpec> = [
  REPLAY_RETENTION_SPEC as unknown as AnySpec,
  JWKS_ROTATION_SPEC as unknown as AnySpec,
  SIGNALR_RETENTION_SPEC as unknown as AnySpec,
  ROTATION_POLICY_BULK_SPEC as unknown as AnySpec,
  REPLAY_INTEGRITY_AUDIT_SPEC as unknown as AnySpec,
  SWISS_PAIRING_AUDIT_SPEC as unknown as AnySpec,
];

let activeSurfaceIndex = 0;

/**
 * Public mount: invoked by `action-router.ts:dispatchAdminPanel`
 * after the pre-flight admin probe passes.  Idempotent — calling
 * twice rebuilds the overlay rather than stacking it.
 */
export async function openAdminPanel(): Promise<void> {
  const overlay = mountAdminOverlay();
  overlay.innerHTML = renderShellHtml();
  attachShellHandlers(overlay);
  await loadActiveSurface(overlay);
}

function renderShellHtml(): string {
  const tabs = SURFACES.map((s, i) => `
    <button type="button"
            class="admin-panel-tab${i === activeSurfaceIndex ? ' admin-panel-tab-active' : ''}"
            data-testid="admin-panel-tab-${s.id}"
            data-surface-index="${i}">
      ${escapeHtml(s.title)}
    </button>`).join('');
  return `
    <div class="admin-panel-shell" data-testid="admin-panel-shell">
      <header class="admin-panel-header">
        <h1 class="admin-panel-title">Admin · Tenant policies</h1>
        <button type="button"
                class="admin-panel-close"
                data-testid="admin-panel-close"
                aria-label="Close admin panel">×</button>
      </header>
      <nav class="admin-panel-tabs" role="tablist">
        ${tabs}
      </nav>
      <section class="admin-panel-body" data-testid="admin-panel-body">
        <p>Loading…</p>
      </section>
    </div>`;
}

function attachShellHandlers(overlay: HTMLElement): void {
  overlay.addEventListener('click', (ev: MouseEvent) => {
    const target = ev.target;
    if (!(target instanceof HTMLElement)) return;
    if (target.classList.contains('admin-panel-close')) {
      closeAdminOverlay();
      return;
    }
    if (target.classList.contains('admin-panel-tab')) {
      const idx = Number(target.getAttribute('data-surface-index'));
      if (Number.isInteger(idx) && idx >= 0 && idx < SURFACES.length) {
        activeSurfaceIndex = idx;
        // Re-render tab strip to highlight the active tab.
        const nav = overlay.querySelector('.admin-panel-tabs');
        if (nav !== null) {
          const tabs = nav.querySelectorAll('.admin-panel-tab');
          tabs.forEach((t, i) => {
            t.classList.toggle('admin-panel-tab-active', i === idx);
          });
        }
        void loadActiveSurface(overlay);
      }
    }
  });
}

async function loadActiveSurface(overlay: HTMLElement): Promise<void> {
  const spec = SURFACES[activeSurfaceIndex];
  const body = overlay.querySelector('.admin-panel-body') as HTMLElement | null;
  if (body === null) return;
  body.innerHTML = renderSurfaceLoading(spec);
  const res = await gateAdminFetch(spec.endpoint);
  if (!res.ok) {
    if (res.status === 401) {
      // The gate already initiated the redirect.  Leave the
      // overlay in place — the page will navigate away momentarily.
      return;
    }
    body.innerHTML = renderSurfaceFrame(spec, res.placeholderHtml ?? '');
    return;
  }
  const rows = parseListBody(spec, res.body);
  body.innerHTML = renderSurfaceFrame(spec, renderAdminListHtml(spec, rows));
  attachListHandlers(body, spec, rows);
}

function renderSurfaceLoading(spec: AnySpec): string {
  return renderSurfaceFrame(spec, `
    <p class="admin-panel-loading"
       data-testid="admin-panel-${spec.id}-loading">
      Loading ${escapeHtml(spec.title.toLowerCase())}…
    </p>`);
}

function renderSurfaceFrame(spec: AnySpec, innerHtml: string): string {
  // Phase K Wave 19 — read-only surfaces (e.g. Swiss pairing audit
  // log) omit the Create toolbar button when `spec.fields` is empty.
  // The Edit/Delete row buttons in `renderAdminListHtml` are not
  // emitted for read-only specs either (gated below).
  const isReadOnly = spec.fields.length === 0;
  const createBtn = isReadOnly ? '' : `
        <button type="button"
                class="admin-panel-btn admin-panel-btn-primary"
                data-testid="admin-panel-${spec.id}-create"
                data-action="create">+ Create</button>`;
  return `
    <article class="admin-panel-surface"
             data-testid="admin-panel-surface-${spec.id}">
      <header>
        <h2>${escapeHtml(spec.title)}</h2>
        <p class="admin-panel-description">${escapeHtml(spec.description)}</p>
      </header>
      <div class="admin-panel-toolbar">${createBtn}
        <button type="button"
                class="admin-panel-btn"
                data-testid="admin-panel-${spec.id}-refresh"
                data-action="refresh">Refresh</button>
      </div>
      <div class="admin-panel-surface-body"
           data-testid="admin-panel-${spec.id}-content">
        ${innerHtml}
      </div>
    </article>`;
}

function parseListBody(spec: AnySpec, body: unknown): unknown[] {
  if (body === null || typeof body !== 'object') return [];
  const o = body as { items?: unknown };
  const raw = Array.isArray(o.items) ? o.items : [];
  const out: unknown[] = [];
  for (const r of raw) {
    const parsed = spec.parseRow(r);
    if (parsed !== null) out.push(parsed);
  }
  return out;
}

function attachListHandlers(body: HTMLElement, spec: AnySpec, rows: unknown[]): void {
  const toolbar = body.querySelector(`[data-testid="admin-panel-${spec.id}-create"]`);
  toolbar?.addEventListener('click', () => {
    showForm(body, spec, 'create', {});
  });
  const refresh = body.querySelector(`[data-testid="admin-panel-${spec.id}-refresh"]`);
  refresh?.addEventListener('click', () => {
    const overlay = document.getElementById('admin-panel-overlay');
    if (overlay !== null) void loadActiveSurface(overlay);
  });
  body.querySelectorAll(`[data-testid="admin-panel-${spec.id}-edit"]`).forEach((btn) => {
    btn.addEventListener('click', () => {
      const tenantId = btn.getAttribute('data-tenant-id') ?? '';
      const row = rows.find((r) => spec.rowKey(r) === tenantId);
      if (row === undefined) return;
      const initial = spec.rowToFormValues !== undefined
        ? spec.rowToFormValues(row) : { tenantId };
      showForm(body, spec, 'edit', initial);
    });
  });
  body.querySelectorAll(`[data-testid="admin-panel-${spec.id}-delete"]`).forEach((btn) => {
    btn.addEventListener('click', () => {
      const tenantId = btn.getAttribute('data-tenant-id') ?? '';
      if (tenantId === '') return;
      void deleteRow(spec, tenantId);
    });
  });
}

function showForm(
  body: HTMLElement,
  spec: AnySpec,
  mode: 'create' | 'edit',
  values: Record<string, string>,
): void {
  const content = body.querySelector(`[data-testid="admin-panel-${spec.id}-content"]`) as HTMLElement | null;
  if (content === null) return;
  content.innerHTML = renderAdminFormHtml(spec, mode, values);
  const form = content.querySelector('form') as HTMLFormElement | null;
  if (form === null) return;
  form.addEventListener('submit', (ev: SubmitEvent) => {
    ev.preventDefault();
    const submitted = collectFormValues(form);
    void saveRow(spec, mode, submitted);
  });
  const cancel = content.querySelector(`[data-testid="admin-panel-${spec.id}-cancel"]`);
  cancel?.addEventListener('click', () => {
    const overlay = document.getElementById('admin-panel-overlay');
    if (overlay !== null) void loadActiveSurface(overlay);
  });
}

async function saveRow(
  spec: AnySpec,
  mode: 'create' | 'edit',
  values: Record<string, string>,
): Promise<void> {
  const tenantId = (values.tenantId ?? '').trim();
  if (tenantId === '') {
    // eslint-disable-next-line no-alert
    window.alert('tenantId is required.');
    return;
  }
  const reason = promptAdminReason(mode);
  if (reason === null) {
    // Operator dismissed the prompt — abort the write quietly.  The
    // backend's 400 would surface the same outcome but skipping the
    // RTT keeps the audit log clean.
    return;
  }
  const payload = spec.buildBody(values);
  const url = mode === 'create'
    ? spec.endpoint
    : `${spec.endpoint}/${encodeURIComponent(tenantId)}`;
  const method = mode === 'create' ? 'POST' : 'PUT';
  const res = await gateAdminFetch(url, {
    method,
    headers: {
      'Content-Type': 'application/json',
      [ADMIN_REASON_HEADER]: reason,
    },
    body: JSON.stringify(payload),
  });
  if (!res.ok) {
    // eslint-disable-next-line no-alert
    window.alert(`Save failed (HTTP ${res.status ?? '?'}).  See the panel for detail.`);
    const overlay = document.getElementById('admin-panel-overlay');
    if (overlay !== null) void loadActiveSurface(overlay);
    return;
  }
  // Re-load the list to show the new row.
  const overlay = document.getElementById('admin-panel-overlay');
  if (overlay !== null) void loadActiveSurface(overlay);
}

async function deleteRow(spec: AnySpec, tenantId: string): Promise<void> {
  // eslint-disable-next-line no-alert
  const confirmed = window.confirm(
    `Delete the ${spec.id} policy for "${tenantId}"?  This cannot be undone.`,
  );
  if (!confirmed) return;
  const reason = promptAdminReason('delete');
  if (reason === null) return;
  const res = await gateAdminFetch(
    `${spec.endpoint}/${encodeURIComponent(tenantId)}`,
    {
      method: 'DELETE',
      headers: { [ADMIN_REASON_HEADER]: reason },
    },
  );
  if (!res.ok && res.status !== 204) {
    // eslint-disable-next-line no-alert
    window.alert(`Delete failed (HTTP ${res.status ?? '?'}).`);
  }
  const overlay = document.getElementById('admin-panel-overlay');
  if (overlay !== null) void loadActiveSurface(overlay);
}
