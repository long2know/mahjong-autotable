// Phase J Wave 10 — Tournaments tab.
//
// This module owns the Tournaments tab inside the lobby strip.  It
// feature-detects Bishop's tournament endpoints — when the backend is
// not yet deployed (404 on `GET /api/tournaments`) we show a "Coming
// soon" placeholder so the rest of the lobby keeps working.
//
// Wire contract (matches Bishop's draft):
//   GET    /api/tournaments                 → 200 { tournaments: [...] } | 404
//   GET    /api/tournaments/{id}            → 200 { tournament, bracket, standings } | 404
//   POST   /api/tournaments                 → 201 { tournament }
//   POST   /api/tournaments/{id}/register   → 204
//   POST   /api/tournaments/{id}/unregister → 204
//   POST   /api/tournaments/{id}/start      → 204
//
// Until the endpoints land, none of the buttons are wired and the
// list/detail/form remain hidden behind a single placeholder.
// installTournamentsPanel() is idempotent and re-runs the probe on each
// activation so the placeholder can self-heal once the backend ships.

import { setElHidden, showEl, hideEl } from './utils';

interface TournamentSummary {
  id: string;
  name: string;
  format: string;
  status: 'open' | 'running' | 'complete';
  playersRegistered: number;
  maxPlayers: number;
  startsAtUtc?: string | null;
  /** True when the current viewer is registered for this tournament. */
  viewerRegistered?: boolean;
  /** True when the current viewer can start the tournament. */
  viewerCanStart?: boolean;
}

interface TournamentDetail {
  tournament: TournamentSummary;
  bracket: string;
  standings: string;
  /** True when the current viewer is registered for this tournament. */
  viewerRegistered?: boolean;
  /** True when the current viewer can start the tournament. */
  viewerCanStart?: boolean;
}

interface State {
  installed: boolean;
  available: boolean | null; // null = not probed yet
  selected: string | null;
}

const state: State = {
  installed: false,
  available: null,
  selected: null,
};

function $(id: string): HTMLElement | null {
  return document.getElementById(id);
}

async function probe(): Promise<boolean> {
  try {
    const resp = await fetch('/api/tournaments', {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (resp.status === 404) return false;
    if (!resp.ok) return false;
    return true;
  } catch {
    return false;
  }
}

async function fetchList(): Promise<TournamentSummary[]> {
  try {
    const resp = await fetch('/api/tournaments', {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (!resp.ok) return [];
    const body = await resp.json() as unknown;
    if (Array.isArray(body)) return body as TournamentSummary[];
    if (body !== null && typeof body === 'object'
        && Array.isArray((body as Record<string, unknown>).tournaments)) {
      return (body as Record<string, unknown>).tournaments as TournamentSummary[];
    }
    return [];
  } catch {
    return [];
  }
}

async function fetchDetail(id: string): Promise<TournamentDetail | null> {
  try {
    const resp = await fetch(`/api/tournaments/${encodeURIComponent(id)}`, {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (!resp.ok) return null;
    return await resp.json() as TournamentDetail;
  } catch {
    return null;
  }
}

async function doPost(path: string, body?: unknown): Promise<boolean> {
  try {
    const resp = await fetch(path, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    return resp.ok;
  } catch {
    return false;
  }
}

function renderEmpty(): void {
  const placeholder = $('tournaments-placeholder');
  const list = $('tournament-list');
  const detail = $('tournament-detail');
  const form = $('tournament-create-form');
  if (placeholder !== null) showEl(placeholder);
  if (list !== null) hideEl(list);
  if (detail !== null) hideEl(detail);
  if (form !== null) hideEl(form);
}

function renderListShell(): void {
  const placeholder = $('tournaments-placeholder');
  const list = $('tournament-list');
  const form = $('tournament-create-form');
  if (placeholder !== null) hideEl(placeholder);
  if (list !== null) showEl(list);
  if (form !== null) showEl(form);
}

function renderList(rows: TournamentSummary[]): void {
  const list = $('tournament-list');
  if (list === null) return;
  list.replaceChildren();
  if (rows.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'lobby-tab-tournaments-empty';
    empty.textContent = 'No tournaments yet — create one below.';
    list.appendChild(empty);
    return;
  }
  rows.forEach((row, i) => {
    const card = document.createElement('div');
    card.className = 'tournament-row';
    card.setAttribute('role', 'listitem');
    card.setAttribute('data-testid', `tournament-row-${i}`);
    card.setAttribute('data-tournament-id', row.id);

    const name = document.createElement('div');
    name.className = 'tournament-row-name';
    name.textContent = row.name;

    const meta = document.createElement('div');
    meta.className = 'tournament-row-meta';
    meta.textContent = `${row.format} · ${row.playersRegistered}/${row.maxPlayers} · ${row.status}`;

    // Phase J Wave 10 (Vasquez `tournament-flow.spec.ts`) — surface
    // registration status + register/start buttons inline on each
    // card so the e2e flow doesn't need to descend into the detail
    // view for the happy path.  Clicking the row body still opens
    // the detail; the buttons stopPropagation() so they don't.
    const status = document.createElement('span');
    status.className = 'tournament-registration-status';
    status.setAttribute('data-testid', 'tournament-registration-status');
    status.textContent = row.viewerRegistered === true ? 'Registered'
      : row.status === 'open' ? 'Open'
      : row.status;

    const actions = document.createElement('div');
    actions.className = 'tournament-row-actions';
    const open = row.status === 'open';
    if (open && row.viewerRegistered !== true) {
      const reg = document.createElement('button');
      reg.type = 'button';
      reg.className = 'btn btn-success btn-sm';
      reg.setAttribute('data-testid', 'tournament-register-btn');
      reg.textContent = 'Register';
      reg.addEventListener('click', async (ev) => {
        ev.stopPropagation();
        const ok = await doPost(`/api/tournaments/${encodeURIComponent(row.id)}/register`);
        if (ok) void refreshList();
      });
      actions.appendChild(reg);
    }
    if (open && row.viewerCanStart === true) {
      const startBtn = document.createElement('button');
      startBtn.type = 'button';
      startBtn.className = 'btn btn-warning btn-sm';
      startBtn.setAttribute('data-testid', 'tournament-start-btn');
      startBtn.textContent = 'Start';
      startBtn.addEventListener('click', async (ev) => {
        ev.stopPropagation();
        const ok = await doPost(`/api/tournaments/${encodeURIComponent(row.id)}/start`);
        if (ok) void refreshList();
      });
      actions.appendChild(startBtn);
    }

    card.appendChild(name);
    card.appendChild(meta);
    card.appendChild(status);
    card.appendChild(actions);
    card.addEventListener('click', () => {
      void openDetail(row.id);
    });
    list.appendChild(card);
  });
}

async function openDetail(id: string): Promise<void> {
  state.selected = id;
  const detail = await fetchDetail(id);
  const detailEl = $('tournament-detail');
  const listEl = $('tournament-list');
  const form = $('tournament-create-form');
  if (detail === null || detailEl === null) {
    if (detailEl !== null) hideEl(detailEl);
    return;
  }
  if (listEl !== null) hideEl(listEl);
  if (form !== null) hideEl(form);
  showEl(detailEl);

  const nameEl = $('tournament-detail-name');
  if (nameEl !== null) nameEl.textContent = detail.tournament.name;
  const metaEl = $('tournament-detail-meta');
  if (metaEl !== null) {
    metaEl.textContent = `${detail.tournament.format} · ${detail.tournament.status} · ${detail.tournament.playersRegistered}/${detail.tournament.maxPlayers}`;
  }
  const bracket = $('tournament-bracket');
  if (bracket !== null) bracket.textContent = detail.bracket;
  const standings = $('tournament-standings');
  if (standings !== null) standings.textContent = detail.standings;

  const registerBtn = $('tournament-register') as HTMLButtonElement | null;
  const unregisterBtn = $('tournament-unregister') as HTMLButtonElement | null;
  const startBtn = $('tournament-start') as HTMLButtonElement | null;
  const open = detail.tournament.status === 'open';
  if (registerBtn !== null) setElHidden(registerBtn, !open || detail.viewerRegistered === true);
  if (unregisterBtn !== null) setElHidden(unregisterBtn, !open || detail.viewerRegistered !== true);
  if (startBtn !== null) setElHidden(startBtn, !open || detail.viewerCanStart !== true);
}

async function refreshList(): Promise<void> {
  const rows = await fetchList();
  renderListShell();
  renderList(rows);
}

function wireDetailButtons(): void {
  const back = $('tournament-detail-back');
  if (back !== null) {
    back.addEventListener('click', () => {
      const detailEl = $('tournament-detail');
      if (detailEl !== null) hideEl(detailEl);
      void refreshList();
    });
  }
  const register = $('tournament-register');
  if (register !== null) {
    register.addEventListener('click', async () => {
      if (state.selected === null) return;
      const ok = await doPost(`/api/tournaments/${encodeURIComponent(state.selected)}/register`);
      if (ok) void openDetail(state.selected);
    });
  }
  const unregister = $('tournament-unregister');
  if (unregister !== null) {
    unregister.addEventListener('click', async () => {
      if (state.selected === null) return;
      const ok = await doPost(`/api/tournaments/${encodeURIComponent(state.selected)}/unregister`);
      if (ok) void openDetail(state.selected);
    });
  }
  const start = $('tournament-start');
  if (start !== null) {
    start.addEventListener('click', async () => {
      if (state.selected === null) return;
      const ok = await doPost(`/api/tournaments/${encodeURIComponent(state.selected)}/start`);
      if (ok) void openDetail(state.selected);
    });
  }
}

function wireCreateForm(): void {
  const form = $('tournament-create-form') as HTMLFormElement | null;
  if (form === null) return;
  form.addEventListener('submit', async (e: SubmitEvent) => {
    e.preventDefault();
    const name = ($('tournament-create-name') as HTMLInputElement | null)?.value.trim() ?? '';
    const format = ($('tournament-create-format') as HTMLSelectElement | null)?.value ?? 'round-robin';
    const maxRaw = ($('tournament-create-max-players') as HTMLInputElement | null)?.value ?? '8';
    const maxPlayers = Math.max(4, Math.min(64, parseInt(maxRaw, 10) || 8));
    if (name === '') return;
    const ok = await doPost('/api/tournaments', { name, format, maxPlayers });
    if (ok) {
      ($('tournament-create-name') as HTMLInputElement | null)?.removeAttribute('value');
      const input = $('tournament-create-name') as HTMLInputElement | null;
      if (input !== null) input.value = '';
      void refreshList();
    }
  });
}

export function installTournamentsPanel(): void {
  if (state.installed) return;
  state.installed = true;
  wireDetailButtons();
  wireCreateForm();
  void probe().then((available) => {
    state.available = available;
    if (!available) {
      renderEmpty();
      return;
    }
    void refreshList();
  });
}

/** Called by lobby tab activation so the panel can refresh on each
 *  Tournaments-tab click.  No-op when the endpoint is unavailable. */
export function refreshTournamentsPanel(): void {
  if (!state.installed) return;
  if (state.available !== true) {
    // Re-probe in case the backend just came up.
    void probe().then((available) => {
      state.available = available;
      if (!available) {
        renderEmpty();
        return;
      }
      void refreshList();
    });
    return;
  }
  void refreshList();
}
