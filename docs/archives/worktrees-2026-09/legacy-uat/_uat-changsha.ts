// =============================================================================
//  UAT Changsha harness — Suite A (fresh-state) + Suite B (persisted-failure
//  replay) shared helpers.  Author: Hudson (Tester).
// =============================================================================
//
//  SCOPE / DISCIPLINE
//  ------------------
//  This harness backs the RED-first UAT sub-lane whose acceptance behaviours do
//  NOT depend on the final Bishop/Hicks wire/slot/opaque schemas.  Every game
//  advance goes through a REAL pointer/keyboard gesture or a real button click
//  (reusing tests/e2e/_playability.ts primitives).  We NEVER:
//    • send a WS UPDATE ourselves,
//    • inject into client/world collections,
//    • synthesise DOM to fake a surface,
//    • call emitDiscard/deal directly to advance gameplay.
//
//  Observation is read-only: we look at the SAME `window.game.client` /
//  `window.game.world` collections the shipped renderer consumes, at the real
//  DOM, at the accessibility tree, and at OUTBOUND WebSocket frames (via
//  Playwright's `page.on('websocket')` → `framesent`).  Reading outbound frames
//  is observation of the client's own traffic, not a backdoor.
//
//  Each game gets a FRESH, isolated gameId so a persisted snapshot from a prior
//  run can never taint a fresh-state assertion (Suite A) — and so a specific
//  captured failure can be replayed deterministically (Suite B).
//
//  RED EVIDENCE is machine-readable and written UNDER tests/e2e/uat-artifacts/
//  (a TEST artifact tree — never production, never the served bundle).

import type { Page, WebSocket as PwWebSocket } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// -----------------------------------------------------------------------------
//  Artifact + evidence plumbing
// -----------------------------------------------------------------------------

export const UAT_ARTIFACT_DIR = path.resolve(__dirname, 'uat-artifacts');

export function ensureUatArtifactDir(): string {
  if (!fs.existsSync(UAT_ARTIFACT_DIR)) fs.mkdirSync(UAT_ARTIFACT_DIR, { recursive: true });
  return UAT_ARTIFACT_DIR;
}

export interface RedDiscriminator {
  /** Stable id for the acceptance behaviour under test, e.g. 'B1-repeat-deal'. */
  id: string;
  /** Human summary of the desired (GREEN) behaviour. */
  expected: string;
  /** What baseline actually did (the RED observation). */
  observed: string;
  /** Machine-comparable numbers backing the RED verdict. */
  metrics: Record<string, number | string | boolean | null>;
  /** ISO timestamp + baseline commit for provenance. */
  at: string;
  commit: string;
}

const BASELINE_COMMIT =
  process.env.UAT_BASELINE_COMMIT ?? '200cad420b9225c4660b47f7f49d1711df2c5712';

/**
 * Append a machine-readable RED discriminator to the evidence ledger. This is a
 * TEST artifact — it documents WHY the baseline is RED for a given behaviour so
 * the Suite B replay (and the implementer) can pin the exact target to flip to
 * GREEN. Never written into production / the served bundle.
 */
export function recordRedEvidence(entry: Omit<RedDiscriminator, 'at' | 'commit'>): void {
  ensureUatArtifactDir();
  const file = path.join(UAT_ARTIFACT_DIR, 'red-evidence.json');
  let ledger: RedDiscriminator[] = [];
  if (fs.existsSync(file)) {
    try {
      ledger = JSON.parse(fs.readFileSync(file, 'utf8')) as RedDiscriminator[];
      if (!Array.isArray(ledger)) ledger = [];
    } catch {
      ledger = [];
    }
  }
  const full: RedDiscriminator = { ...entry, at: new Date().toISOString(), commit: BASELINE_COMMIT };
  // De-dupe by id — keep the latest observation.
  ledger = ledger.filter((e) => e.id !== entry.id);
  ledger.push(full);
  fs.writeFileSync(file, JSON.stringify(ledger, null, 2));
}

// -----------------------------------------------------------------------------
//  Fresh isolated game identity
// -----------------------------------------------------------------------------

let seq = 0;
/** A fresh, collision-resistant, isolated gameId for a Suite A fresh-state run. */
export function freshGameId(tag: string): string {
  seq += 1;
  const rid = process.env.UAT_RUN_ID ?? 'local';
  return `uat-${tag}-${rid}-${Date.now().toString(36)}-${seq}`;
}

export interface UatUrlOptions {
  gameId: string;
  variant?: string;
  dealMode?: 'auto' | 'manual';
  botCount?: number;
  botDifficulty?: string;
  /** When omitted, the URL carries NO seat param (a truly unseated connect). */
  seat?: number;
}

/** Compose an isolated-game URL against the served `/autotable/` base. */
export function uatGameUrl(base: string, opts: UatUrlOptions): string {
  const u = new URL(base);
  u.searchParams.set('variant', opts.variant ?? 'changsha');
  u.searchParams.set('dealMode', opts.dealMode ?? 'auto');
  u.searchParams.set('botCount', String(opts.botCount ?? 3));
  u.searchParams.set('botDifficulty', opts.botDifficulty ?? 'Hard');
  u.searchParams.set('gameId', opts.gameId);
  if (typeof opts.seat === 'number') u.searchParams.set('seat', String(opts.seat));
  return u.toString();
}

export function resolveBase(baseURL?: string): string {
  return baseURL ?? process.env.E2E_BASE_URL ?? 'http://127.0.0.1:5114/autotable/';
}

// -----------------------------------------------------------------------------
//  Outbound WebSocket capture (observe the client's OWN traffic)
// -----------------------------------------------------------------------------

export interface WsEntry {
  raw: string;
  type: string | null;
  /** Parsed UPDATE entries (each [op, ...args]) when type === 'UPDATE'. */
  entries: unknown[][];
}

export interface WsRecorder {
  frames: WsEntry[];
  clear(): void;
  /** Count of outbound UPDATE frames carrying a TILE-DATA `things` mutation. */
  countThingsMutations(): number;
  /** All tile-data `things` entries seen since the last clear(). */
  thingsMutations(): Array<{ id: number; payload: Record<string, unknown> }>;
}

/**
 * Attach an outbound-frame recorder to the page. This listens to the client's
 * own `ws.send(...)` payloads via Playwright — pure observation, no injection.
 *
 * A "things mutation" is an UPDATE entry shaped `["things", <numericTileId>,
 * <payloadObject>]` — i.e. the client pushing tile placement/hold/flip to the
 * server (the relay behaviour). Collection-registration entries such as
 * `["unique","things","slotName"]` or `["perPlayer","seats",true]` are NOT tile
 * mutations and are excluded.
 */
export function recordOutboundWs(page: Page): WsRecorder {
  const rec: WsRecorder = {
    frames: [],
    clear() {
      this.frames = [];
    },
    countThingsMutations() {
      return this.thingsMutations().length;
    },
    thingsMutations() {
      const out: Array<{ id: number; payload: Record<string, unknown> }> = [];
      for (const f of this.frames) {
        if (f.type !== 'UPDATE') continue;
        for (const e of f.entries) {
          if (Array.isArray(e) && e[0] === 'things' && typeof e[1] === 'number' && e[2] && typeof e[2] === 'object') {
            out.push({ id: e[1] as number, payload: e[2] as Record<string, unknown> });
          }
        }
      }
      return out;
    },
  };
  page.on('websocket', (ws: PwWebSocket) => {
    ws.on('framesent', (frame) => {
      const payload = frame.payload;
      if (typeof payload !== 'string') return;
      let type: string | null = null;
      let entries: unknown[][] = [];
      try {
        const msg = JSON.parse(payload) as { type?: string; entries?: unknown[][] };
        type = typeof msg.type === 'string' ? msg.type : null;
        if (Array.isArray(msg.entries)) entries = msg.entries as unknown[][];
      } catch {
        /* non-JSON frame — keep raw only */
      }
      rec.frames.push({ raw: payload.slice(0, 400), type, entries });
    });
  });
  return rec;
}

// -----------------------------------------------------------------------------
//  Read-only client/world observation
// -----------------------------------------------------------------------------

/** A stable fingerprint of the LOCAL rendered `client.things` collection. */
export async function thingsFingerprint(page: Page): Promise<{ count: number; hash: string }> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const c = (window as any).game?.client;
    const parts: string[] = [];
    if (c?.things) {
      for (const [k, v] of c.things.entries()) {
        const slot = (v && (v.slotName || v.slot?.name)) || '?';
        parts.push(`${k}:${slot}:${v?.rotationIndex ?? ''}:${v?.claimedBy ?? ''}:${v?.typeIndex ?? ''}`);
      }
    }
    parts.sort();
    const s = parts.join('|');
    let h = 0;
    for (let i = 0; i < s.length; i++) h = (h * 31 + s.charCodeAt(i)) | 0;
    return { count: parts.length, hash: String(h >>> 0) };
  });
}

/** Count local hand tiles owned by a seat OTHER than the local seat. */
export async function ownVsOppHand(page: Page): Promise<{ mySeat: number | null; own: number; opp: number; ownRots: number[]; oppRots: number[] }> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    const w = g?.world;
    const c = g?.client;
    const mySeat = typeof c?.seat === 'number' ? c.seat : typeof w?.seat === 'number' ? w.seat : null;
    let own = 0;
    let opp = 0;
    const ownRots = new Set<number>();
    const oppRots = new Set<number>();
    if (w?.things) {
      for (const t of w.things.values()) {
        if (t?.slot?.group !== 'hand') continue;
        if (t.slot.seat === mySeat) {
          own++;
          ownRots.add(t.rotationIndex);
        } else {
          opp++;
          oppRots.add(t.rotationIndex);
        }
      }
    }
    return { mySeat, own, opp, ownRots: [...ownRots], oppRots: [...oppRots] };
  });
}

/** Read the streaming move-log sidebar text (a stale-cue surface). */
export async function readMoveLogText(page: Page): Promise<string> {
  return page.evaluate(() => {
    const el = document.getElementById('move-log');
    return el ? (el.textContent || '').replace(/\s+/g, ' ').trim() : '';
  });
}

export interface ControlProbe {
  id: string;
  present: boolean;
  visible: boolean;
  disabled: boolean | null;
  ariaHidden: string | null;
  inAccessibilityTree: boolean;
  focusable: boolean;
}

/**
 * Probe a control's real accessibility posture: present, visible, disabled,
 * aria-hidden, whether it appears in the accessibility tree (by accessible
 * name/role), and whether it is keyboard-focusable. Used to assert that a
 * legacy relay control is genuinely gone from the a11y tree — not merely
 * visually hidden — for a Changsha game.
 */
export async function probeControl(page: Page, id: string): Promise<ControlProbe> {
  return page.evaluate((elId: string) => {
    const el = document.getElementById(elId) as HTMLElement | null;
    if (!el) {
      return { id: elId, present: false, visible: false, disabled: null, ariaHidden: null, inAccessibilityTree: false, focusable: false };
    }
    const cs = getComputedStyle(el);
    const visible = cs.display !== 'none' && cs.visibility !== 'hidden' && Number(cs.opacity) !== 0;
    // A node is excluded from the a11y tree if it (or an ancestor) is
    // display:none / visibility:hidden / aria-hidden="true".
    let hiddenFromA11y = false;
    let node: HTMLElement | null = el;
    while (node) {
      const ncs = getComputedStyle(node);
      if (ncs.display === 'none' || ncs.visibility === 'hidden' || node.getAttribute('aria-hidden') === 'true') {
        hiddenFromA11y = true;
        break;
      }
      node = node.parentElement;
    }
    const disabled = (el as HTMLButtonElement | HTMLInputElement | HTMLSelectElement).disabled ?? null;
    const focusable = visible && !disabled && el.tabIndex >= 0;
    return {
      id: elId,
      present: true,
      visible,
      disabled,
      ariaHidden: el.getAttribute('aria-hidden'),
      inAccessibilityTree: !hiddenFromA11y,
      focusable,
    };
  }, id);
}

/**
 * Collect every accessible label exposed by the live accessibility tree that
 * MATCHES a forbidden pattern (e.g. Riichi / four_player / 4p). Uses
 * Playwright's real ARIA snapshot (`locator.ariaSnapshot()`) — NOT a computed
 * <option> proxy read off a closed <select>, and NOT raw DOM text. Returns the
 * offending lines from the ARIA YAML.
 */
export async function accessibilityLabelsMatching(page: Page, pattern: RegExp): Promise<string[]> {
  const yaml = await page.locator('body').ariaSnapshot().catch(() => '');
  const hits: string[] = [];
  for (const line of yaml.split('\n')) {
    if (pattern.test(line)) hits.push(line.trim());
  }
  return hits;
}

export interface SelectA11y {
  id: string;
  present: boolean;
  /** A real accessible name (aria-label / aria-labelledby / <label for>) — NOT
   *  the disabled first-<option> pseudo-label. */
  accessibleName: string | null;
  optionCount: number;
  focusable: boolean;
  fontSizePx: number;
}

/**
 * Probe a <select>'s ACTUAL accessibility posture — the opened/real control, not
 * a computed option proxy. `accessibleName` is null unless the element carries a
 * genuine accessible name; a disabled placeholder first option ("Bots:") does
 * NOT count. This is the anti-"computed-option-only proxy" probe.
 */
export async function probeSelectA11y(page: Page, id: string): Promise<SelectA11y> {
  return page.evaluate((sid: string) => {
    const el = document.getElementById(sid) as HTMLSelectElement | null;
    if (!el) return { id: sid, present: false, accessibleName: null, optionCount: 0, focusable: false, fontSizePx: 0 };
    const aria = el.getAttribute('aria-label');
    const labelledby = el.getAttribute('aria-labelledby');
    let labelledbyText: string | null = null;
    if (labelledby) {
      const ref = document.getElementById(labelledby);
      labelledbyText = ref ? (ref.textContent || '').trim() || null : null;
    }
    const labelFor = document.querySelector(`label[for="${sid}"]`);
    const labelForText = labelFor ? (labelFor.textContent || '').trim() || null : null;
    const accessibleName = (aria && aria.trim()) || labelledbyText || labelForText || null;
    const cs = getComputedStyle(el);
    return {
      id: sid,
      present: true,
      accessibleName,
      optionCount: el.options.length,
      focusable: el.tabIndex >= 0 && !el.disabled,
      fontSizePx: parseFloat(cs.fontSize) || 0,
    };
  }, id);
}
