// Phase K Wave 6 — AI commentary panel.
//
// Renders LLM-generated play-by-play as a scrollable side-panel
// during replay.  The data source is Bishop's W6 stub endpoint
// `GET /api/games/{gameId}/commentary/replay`; until Phase L lands
// the actual LLM bridge, that endpoint returns 404 (or 503) and the
// panel surfaces a "Phase L feature — not yet available" copy so the
// replay viewer keeps working without the panel.
//
// The chunk is lazy-loaded by `replay.ts` only when the replay
// surface opens — the panel never paints on the lobby cold path.
//
// Wire contract:
//   GET /api/games/{gameId}/commentary/replay
//     → 200 { lines: [{ turn, actor, text, timestampUtc }, ...] }
//     → 404 / 503 → render "Phase L feature — not yet available"
//
// Testids exported for Vasquez:
//   • commentary-panel             — panel container
//   • commentary-panel-loading     — loading state
//   • commentary-panel-empty       — empty state
//   • commentary-panel-error       — error state ("Phase L feature…")
//   • commentary-line-{idx}        — each rendered line (idx 0..N-1)

export interface CommentaryLine {
  /** Turn number this line annotates; -1 for narrator / pre-game. */
  turn: number;
  /** Seat 0..3 of the actor, or -1 for narrator / system. */
  actor: number;
  text: string;
  timestampUtc?: string;
}

export interface CommentaryReplayResponse {
  lines: ReadonlyArray<CommentaryLine>;
}

interface PanelState {
  installed: boolean;
  panel: HTMLDivElement | null;
  gameId: string | null;
}

const state: PanelState = {
  installed: false,
  panel: null,
  gameId: null,
};

const PHASE_L_COPY = 'Phase L feature — not yet available.';
const EMPTY_COPY = 'No commentary available for this replay yet.';
const LOADING_COPY = 'Loading commentary…';

/**
 * Mount (or re-use) the panel inside `host` and fetch commentary for
 * `gameId`.  Re-entrant — calling twice replaces the lines.
 */
export async function openCommentaryPanel(host: HTMLElement, gameId: string): Promise<void> {
  ensurePanel(host);
  state.gameId = gameId;
  if (state.panel === null) return;
  renderLoading(state.panel);

  try {
    const resp = await fetch(`/api/games/${encodeURIComponent(gameId)}/commentary/replay`, {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });
    if (resp.status === 404 || resp.status === 503) {
      renderError(state.panel);
      return;
    }
    if (!resp.ok) {
      renderError(state.panel);
      return;
    }
    const raw = (await resp.json()) as unknown;
    const parsed = normalizeResponse(raw);
    if (parsed.lines.length === 0) {
      renderEmpty(state.panel);
      return;
    }
    renderLines(state.panel, parsed.lines);
  } catch {
    // Network failure or backend offline — surface the phase-L copy
    // rather than a hard error; the replay still plays.
    renderError(state.panel);
  }
}

/**
 * Close + tear down the panel.  Idempotent.
 */
export function closeCommentaryPanel(): void {
  if (state.panel !== null && state.panel.parentNode !== null) {
    state.panel.parentNode.removeChild(state.panel);
  }
  state.panel = null;
  state.installed = false;
  state.gameId = null;
}

// ── Internals ───────────────────────────────────────────────────────

function ensurePanel(host: HTMLElement): void {
  if (state.installed && state.panel !== null && state.panel.parentNode === host) return;
  // Re-anchor if host changed (re-entry after close + reopen).
  if (state.panel !== null && state.panel.parentNode !== null) {
    state.panel.parentNode.removeChild(state.panel);
  }
  const panel = document.createElement('div');
  panel.className = 'commentary-panel';
  panel.setAttribute('data-testid', 'commentary-panel');
  panel.setAttribute('role', 'log');
  panel.setAttribute('aria-live', 'polite');
  panel.setAttribute('aria-label', 'AI commentary');

  const header = document.createElement('div');
  header.className = 'commentary-panel-header';
  const title = document.createElement('h3');
  title.className = 'commentary-panel-title';
  title.textContent = 'Commentary';
  header.appendChild(title);
  panel.appendChild(header);

  const body = document.createElement('div');
  body.className = 'commentary-panel-body';
  body.setAttribute('data-commentary-body', '');
  panel.appendChild(body);

  host.appendChild(panel);
  state.panel = panel;
  state.installed = true;
}

function getBody(panel: HTMLDivElement): HTMLDivElement {
  let body = panel.querySelector<HTMLDivElement>('[data-commentary-body]');
  if (body === null) {
    body = document.createElement('div');
    body.className = 'commentary-panel-body';
    body.setAttribute('data-commentary-body', '');
    panel.appendChild(body);
  }
  return body;
}

function renderLoading(panel: HTMLDivElement): void {
  const body = getBody(panel);
  body.replaceChildren();
  const el = document.createElement('div');
  el.className = 'commentary-panel-loading';
  el.setAttribute('data-testid', 'commentary-panel-loading');
  el.setAttribute('role', 'status');
  el.textContent = LOADING_COPY;
  body.appendChild(el);
}

function renderError(panel: HTMLDivElement): void {
  const body = getBody(panel);
  body.replaceChildren();
  const el = document.createElement('div');
  el.className = 'commentary-panel-error';
  el.setAttribute('data-testid', 'commentary-panel-error');
  el.setAttribute('role', 'status');
  el.textContent = PHASE_L_COPY;
  body.appendChild(el);
}

function renderEmpty(panel: HTMLDivElement): void {
  const body = getBody(panel);
  body.replaceChildren();
  const el = document.createElement('div');
  el.className = 'commentary-panel-empty';
  el.setAttribute('data-testid', 'commentary-panel-empty');
  el.textContent = EMPTY_COPY;
  body.appendChild(el);
}

function renderLines(panel: HTMLDivElement, lines: ReadonlyArray<CommentaryLine>): void {
  const body = getBody(panel);
  body.replaceChildren();
  lines.forEach((line, idx) => {
    const row = document.createElement('div');
    row.className = 'commentary-line';
    row.setAttribute('data-testid', `commentary-line-${idx}`);
    row.setAttribute('data-turn', String(line.turn));
    row.setAttribute('data-actor', String(line.actor));

    if (line.actor >= 0) {
      const badge = document.createElement('span');
      badge.className = `commentary-line-actor commentary-line-actor-${line.actor}`;
      badge.textContent = `P${line.actor + 1}`;
      row.appendChild(badge);
    } else {
      const badge = document.createElement('span');
      badge.className = 'commentary-line-actor commentary-line-actor-narrator';
      badge.textContent = '🎙';
      row.appendChild(badge);
    }

    const text = document.createElement('span');
    text.className = 'commentary-line-text';
    text.textContent = line.text;
    row.appendChild(text);
    body.appendChild(row);
  });
}

function normalizeResponse(raw: unknown): CommentaryReplayResponse {
  if (typeof raw !== 'object' || raw === null) return { lines: [] };
  const rec = raw as Record<string, unknown>;
  const linesRaw = rec.lines;
  if (!Array.isArray(linesRaw)) return { lines: [] };
  const lines: CommentaryLine[] = [];
  for (const lr of linesRaw) {
    if (typeof lr !== 'object' || lr === null) continue;
    const r = lr as Record<string, unknown>;
    const text = typeof r.text === 'string' ? r.text : '';
    if (text === '') continue;
    const turn = typeof r.turn === 'number' && Number.isFinite(r.turn) ? r.turn : -1;
    const actor = typeof r.actor === 'number' && Number.isFinite(r.actor) ? r.actor : -1;
    const timestampUtc = typeof r.timestampUtc === 'string' ? r.timestampUtc : undefined;
    lines.push({ turn, actor, text, timestampUtc });
  }
  return { lines };
}
