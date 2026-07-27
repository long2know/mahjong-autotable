// =============================================================================
//  P0 real-UI playability gate — shared harness helpers (#122, Hudson)
// =============================================================================
//
//  ACCEPTANCE DISCIPLINE (binding, per issue #122 + Lead design review C-8):
//
//    The playability GATE may advance the game ONLY through real
//    DOM / canvas / pointer interactions. WS backdoors — `client.update(...)`,
//    the private `events.emit('update', ...)` emitter, direct
//    `world.emitDiscard(id)` / `world.emitTakePickup()` calls, collection
//    injection, or server-state mutation — are FORBIDDEN as a way to make the
//    acceptance test progress or pass. See
//    `.squad/skills/playtest-ws-backdoor/SKILL.md:88-93`.
//
//    Helpers in this file fall into two clearly-separated classes:
//
//      • ADVANCE primitives (takeSeatByClick, clickDeal, discardByPointer,
//        claimByClick) — drive the game EXCLUSIVELY via Playwright
//        `page.mouse.*` / `locator.click()`. These are the only functions the
//        gate uses to move the game forward.
//
//      • OBSERVE primitives (projectTileToCanvas, readSeat, readGameComplete,
//        readResult, readMatch, readMyHandTiles, hasExtraHandTile,
//        readClaimWindow) — READ client/world state to decide *where* to click
//        and *what* to assert. They NEVER emit a WS update. Reading
//        `window.game.*` state is observation, not a backdoor: the projection
//        maths only tells the pointer where a tile is drawn on the canvas; the
//        actual discard is produced by the real `mouse.down()` that the browser
//        routes through `MouseUi.onMouseDown` → `World.onDragStart`.
//
//    If you need a WS backdoor for a UI-plumbing smoke check, put it in a
//    file whose name and header scream "diagnostic" and keep it OUT of the
//    gate — a diagnostic can never satisfy this P0.
//
// =============================================================================

import type { Page, APIRequestContext } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';
import * as crypto from 'crypto';

// The backend serves the *built* bundle from src/frontend/autotable/.
// This spec lives in src/frontend/autotable-src/tests/e2e/, so the built
// dir is three levels up (../../../autotable).
export const BUILT_BUNDLE_DIR = path.resolve(__dirname, '../../../autotable');

// Evidence lands under playtest-artifacts/playability-gate/ (repo root is five
// levels up from this file). Kept next to the diagnostic artifacts so a
// reviewer finds all playtest evidence in one place.
export const ARTIFACT_DIR = path.resolve(
  __dirname,
  '../../../../../playtest-artifacts/playability-gate',
);

export function ensureArtifactDir(): string {
  if (!fs.existsSync(ARTIFACT_DIR)) {
    fs.mkdirSync(ARTIFACT_DIR, { recursive: true });
  }
  return ARTIFACT_DIR;
}

// -----------------------------------------------------------------------------
//  Deterministic game configuration
// -----------------------------------------------------------------------------
//
//  The gate is deterministic in its *inputs*: a fixed gameId, fixed bot
//  difficulty, fixed 4-hand cap, and bounded timeouts. A fully deterministic
//  *wall* additionally needs the backend to honour a `seed` handshake param;
//  today `client-ui.ts buildWsUrl` forwards only gameId/seat/botCount/variant/
//  dealMode/botDifficulty (C-2), so `seed` never reaches the server. We still
//  pass `seed` on the URL so the gate goes fully deterministic for free once
//  the backend plumbs it (handoff logged for WP-A / Bishop).

export interface GameConfig {
  gameId: string;
  seat: number;            // 0..3 — which chair the human takes
  botCount: number;        // 3 → one human + three bots
  botDifficulty: string;   // PascalCase: Easy|Medium|Hard|Master (C-2)
  dealMode: 'manual' | 'auto';
  // Lead decision (2026-07-26): handCount is REAL, not decorative — the server
  // MUST honor 1/4/8/16/32 (MaxHands), so a gate can't be evaded by an impl
  // that ignores it and always plays the default 4.
  handCount: 1 | 4 | 8 | 16 | 32;
  seed: number;            // forward-compatible determinism (see note above)
  variant: string;
}

export function makeConfig(overrides: Partial<GameConfig> = {}): GameConfig {
  // Stable gameId keeps the run reproducible; a short random suffix avoids
  // colliding with a previously-persisted snapshot on a shared backend.
  const suffix = overrides.gameId
    ? ''
    : `-${process.env.PLAYABILITY_RUN_ID ?? 'local'}`;
  return {
    gameId: `playability-gate${suffix}`,
    seat: 0,
    botCount: 3,
    botDifficulty: 'Hard',
    dealMode: 'manual',
    handCount: 4,
    seed: 12345,
    variant: 'changsha',
    ...overrides,
  };
}

export function buildGameUrl(baseURL: string, cfg: GameConfig): string {
  // baseURL is e.g. http://127.0.0.1:8080/autotable/ — append the query.
  const u = new URL(baseURL);
  u.searchParams.set('variant', cfg.variant);
  u.searchParams.set('dealMode', cfg.dealMode);
  u.searchParams.set('botCount', String(cfg.botCount));
  u.searchParams.set('botDifficulty', cfg.botDifficulty);
  u.searchParams.set('handCount', String(cfg.handCount));
  u.searchParams.set('seat', String(cfg.seat));
  u.searchParams.set('seed', String(cfg.seed));
  u.searchParams.set('gameId', cfg.gameId);
  return u.toString();
}

// -----------------------------------------------------------------------------
//  PREFLIGHT — served bundle hash === freshly-built source hash
// -----------------------------------------------------------------------------
//
//  The gate must run against the *freshly built* bundle the backend serves
//  (C-8). Vite emits content-addressed filenames (`[name].[hash:8].[ext]`), so
//  if the served index.html references the exact same hashed asset set as the
//  on-disk build AND the served entry bytes hash-match the on-disk entry bytes,
//  the served bundle IS the source build. This is a self-contained scaffold of
//  the WP-D / #119 bundle-diff gate; it can later delegate to that gate.

const HASHED_ASSET_RE = /(?:src|href)="\.?\/?([^"]+\.[0-9a-f]{8}\.(?:js|css))"/g;

export interface BundleHashResult {
  ok: boolean;
  builtIndexExists: boolean;
  builtAssets: string[];
  servedAssets: string[];
  missingOnServer: string[];
  extraOnServer: string[];
  entryChecked: string | null;
  entryBuiltSha: string | null;
  entryServedSha: string | null;
  entryShaMatches: boolean | null;
  reason: string;
}

function extractHashedAssets(html: string): string[] {
  const out = new Set<string>();
  let m: RegExpExecArray | null;
  HASHED_ASSET_RE.lastIndex = 0;
  while ((m = HASHED_ASSET_RE.exec(html)) !== null) {
    // Normalise to the bare hashed filename (drop any leading ./ or path).
    out.add(m[1].replace(/^.*\//, ''));
  }
  return [...out].sort();
}

function sha256(buf: Buffer | string): string {
  return crypto.createHash('sha256').update(buf).digest('hex');
}

/**
 * OBSERVE — compare the served bundle against the freshly-built on-disk bundle.
 * Returns a structured result; the caller decides whether to hard-fail. This
 * function performs NO game interaction.
 */
export async function checkServedBundleMatchesBuild(
  request: APIRequestContext,
  baseURL: string,
): Promise<BundleHashResult> {
  const builtIndexPath = path.join(BUILT_BUNDLE_DIR, 'index.html');
  const builtIndexExists = fs.existsSync(builtIndexPath);

  const result: BundleHashResult = {
    ok: false,
    builtIndexExists,
    builtAssets: [],
    servedAssets: [],
    missingOnServer: [],
    extraOnServer: [],
    entryChecked: null,
    entryBuiltSha: null,
    entryServedSha: null,
    entryShaMatches: null,
    reason: '',
  };

  if (!builtIndexExists) {
    result.reason =
      `No on-disk built bundle at ${builtIndexPath}. Run \`npm run build\` ` +
      `before the gate so the preflight has a source build to diff against.`;
    return result;
  }

  const builtHtml = fs.readFileSync(builtIndexPath, 'utf8');
  result.builtAssets = extractHashedAssets(builtHtml);

  // Fetch the served index.html from the running backend.
  const servedResp = await request.get(baseURL, { failOnStatusCode: false });
  if (!servedResp.ok()) {
    result.reason =
      `Served index.html fetch failed: HTTP ${servedResp.status()} at ${baseURL}. ` +
      `Is the backend running and serving /autotable/?`;
    return result;
  }
  const servedHtml = await servedResp.text();
  result.servedAssets = extractHashedAssets(servedHtml);

  const builtSet = new Set(result.builtAssets);
  const servedSet = new Set(result.servedAssets);
  result.missingOnServer = result.builtAssets.filter((a) => !servedSet.has(a));
  result.extraOnServer = result.servedAssets.filter((a) => !builtSet.has(a));

  // Byte-level confirmation of the main entry bundle (autotable-src.<hash>.js).
  const entry = result.builtAssets.find((a) => /^autotable-src\.[0-9a-f]{8}\.js$/.test(a));
  if (entry) {
    result.entryChecked = entry;
    const entryDisk = path.join(BUILT_BUNDLE_DIR, entry);
    if (fs.existsSync(entryDisk)) {
      result.entryBuiltSha = sha256(fs.readFileSync(entryDisk));
    }
    const entryUrl = new URL(entry, baseURL).toString();
    const entryResp = await request.get(entryUrl, { failOnStatusCode: false });
    if (entryResp.ok()) {
      const body = await entryResp.body();
      result.entryServedSha = sha256(body);
    }
    result.entryShaMatches =
      result.entryBuiltSha !== null &&
      result.entryServedSha !== null &&
      result.entryBuiltSha === result.entryServedSha;
  }

  const assetsMatch =
    result.missingOnServer.length === 0 && result.extraOnServer.length === 0;
  // entryShaMatches may be null when there's no single named entry; in that
  // case fall back to the referenced-asset-set equality (still hash-based).
  const entryOk = result.entryShaMatches !== false;
  result.ok = assetsMatch && entryOk;
  result.reason = result.ok
    ? 'Served bundle matches the freshly-built source bundle (content hashes equal).'
    : `Served bundle differs from the on-disk build. ` +
      `missingOnServer=${JSON.stringify(result.missingOnServer)} ` +
      `extraOnServer=${JSON.stringify(result.extraOnServer)} ` +
      `entrySha(built=${result.entryBuiltSha?.slice(0, 12)} served=${result.entryServedSha?.slice(0, 12)}). ` +
      `Rebuild the frontend and restart the backend against the fresh dist dir.`;
  return result;
}

// -----------------------------------------------------------------------------
//  Overlay defanging — real playability blockers, not test cheats
// -----------------------------------------------------------------------------
//
//  Full-page overlays (tour, magic-link landing, sign-in backdrop) intercept
//  pointer events even while aria-hidden. The lobby panel also re-opens after
//  navigation (W23 UX bug). We hide the overlays via CSS injected before the
//  app boots; this changes nothing about game logic — it just lets a real
//  pointer reach the real buttons a real user would (eventually) also reach.

export async function defangOverlays(page: Page): Promise<void> {
  await page.addInitScript(() => {
    // CSP-clean: no injected <style> element (strict prod CSP forbids
    // style-src inline). Hide overlays via CSSOM property writes (not subject
    // to style-src) + a MutationObserver for ones that mount later.
    const SEL = [
      '#tour-overlay', '#magic-link-landing', '#magic-link-overlay',
      '#signin-modal-backdrop', '.magic-link-landing', '.magic-link-overlay',
      '.signin-modal-backdrop', '[data-testid="tour-overlay"]',
      '[data-testid="signin-modal-backdrop"]',
    ];
    const defang = (): void => {
      for (const sel of SEL) {
        document.querySelectorAll<HTMLElement>(sel).forEach((el) => {
          el.style.display = 'none';
          el.style.pointerEvents = 'none';
          el.style.visibility = 'hidden';
        });
      }
      document.querySelectorAll<HTMLElement>('[aria-hidden="true"]').forEach((el) => {
        el.style.pointerEvents = 'none';
      });
    };
    const start = (): void => {
      defang();
      new MutationObserver(defang).observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeFilter: ['aria-hidden', 'class', 'style'],
      });
    };
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', start);
    } else {
      start();
    }
  });
}

// -----------------------------------------------------------------------------
//  Small logging + wait utilities
// -----------------------------------------------------------------------------

export interface StepLog {
  step: string;
  ok: boolean;
  detail: unknown;
  at: number;
}

export class Recorder {
  readonly steps: StepLog[] = [];
  private readonly t0 = Date.now();

  log(step: string, ok: boolean, detail: unknown = null): void {
    const entry: StepLog = { step, ok, detail, at: Date.now() - this.t0 };
    this.steps.push(entry);
    const flag = ok ? 'OK ' : 'FAIL';
    // eslint-disable-next-line no-console
    console.log(`[gate ${String(entry.at).padStart(6)}ms] ${flag} ${step} :: ${safeJson(detail)}`);
  }

  write(filename: string, extra: Record<string, unknown> = {}): string {
    ensureArtifactDir();
    const p = path.join(ARTIFACT_DIR, filename);
    fs.writeFileSync(p, JSON.stringify({ steps: this.steps, ...extra }, null, 2));
    return p;
  }
}

function safeJson(v: unknown): string {
  try {
    const s = JSON.stringify(v);
    return s && s.length > 300 ? s.slice(0, 300) + '…' : String(s);
  } catch {
    return String(v);
  }
}

export async function snap(page: Page, name: string): Promise<void> {
  ensureArtifactDir();
  await page
    .screenshot({ path: path.join(ARTIFACT_DIR, name), fullPage: true })
    .catch(() => undefined);
}

// -----------------------------------------------------------------------------
//  OBSERVE primitives — read-only client/world state
// -----------------------------------------------------------------------------

export async function waitForGameObject(page: Page, timeoutMs = 30_000): Promise<boolean> {
  return page
    .waitForFunction(
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      () => Boolean((window as any).game?.client && (window as any).game?.world),
      undefined,
      { timeout: timeoutMs },
    )
    .then(() => true)
    .catch(() => false);
}

export async function readConnected(page: Page): Promise<boolean> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    try {
      return typeof g?.client?.connected === 'function'
        ? Boolean(g.client.connected())
        : false;
    } catch {
      return false;
    }
  });
}

export async function readSeat(page: Page): Promise<number | null> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    const s = g?.client?.seat;
    return typeof s === 'number' ? s : null;
  });
}

export interface GameCompleteView {
  present: boolean;
  isComplete: boolean;
  raw: unknown;
}

/** OBSERVE — the server-authoritative gameComplete singleton, if any. */
export async function readGameComplete(page: Page): Promise<GameCompleteView> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    try {
      const cur = g?.client?.gameComplete?.get('current');
      if (cur === null || cur === undefined) {
        return { present: false, isComplete: false, raw: null };
      }
      const isComplete = Boolean(
        cur.isComplete ?? cur.IsComplete ?? cur.isGameComplete ?? cur.IsGameComplete,
      );
      return { present: true, isComplete, raw: cur };
    } catch (e) {
      return { present: false, isComplete: false, raw: String(e) };
    }
  });
}

export interface ResultView {
  present: boolean;
  winner: number | null;
  type: string | null;
  nextBanker: number | null;
  raw: unknown;
}

/** OBSERVE — the current hand's server-authoritative result singleton. */
export async function readResult(page: Page): Promise<ResultView> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    try {
      const cur = g?.client?.result?.get('current');
      if (cur === null || cur === undefined) {
        return { present: false, winner: null, type: null, nextBanker: null, raw: null };
      }
      return {
        present: true,
        winner: typeof cur.winner === 'number' ? cur.winner : null,
        type: typeof cur.type === 'string' ? cur.type : null,
        nextBanker: typeof cur.nextBanker === 'number' ? cur.nextBanker : null,
        raw: cur,
      };
    } catch (e) {
      return { present: false, winner: null, type: null, nextBanker: null, raw: String(e) };
    }
  });
}

export interface HandEndRecord {
  winner: number | null;
  type: string | null;
  nextBanker: number | null;
  at: number;
}

export interface HandEndObservation {
  installed: boolean;
  ends: HandEndRecord[];
  clears: number;
}

/**
 * OBSERVE (read-only) — latch every hand end from the real `result` collection
 * `update` event: the SAME signal the shipped bundle's own onResultUpdate
 * (game-ui.ts), move-log, and replay subscribe to. The #132 fix tombstones
 * result['current'] to null on the very next phase after EndHand, so the
 * non-null window is brief and a coarse test-side poll can slip past it; an
 * in-page listener on the applied update stream never does. This ONLY listens —
 * it emits nothing and advances no gameplay, so it is not a backdoor. Install
 * once right after connect (before hand 1 ends) so no hand end is missed.
 */
export async function installHandEndObserver(page: Page): Promise<boolean> {
  return page.evaluate(async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = window as any;
    const deadline = Date.now() + 5_000;
    while (Date.now() < deadline && !w.game?.client?.result) {
      await new Promise((r) => setTimeout(r, 50));
    }
    const result = w.game?.client?.result;
    if (!result || typeof result.on !== 'function') return false;
    if (w.__handEndObs?.installed) return true; // idempotent across re-entry
    const obs = { installed: true, latched: false, ends: [] as unknown[], clears: 0 };
    w.__handEndObs = obs;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    result.on('update', (entries: Array<[string, any]>) => {
      for (const [key, val] of entries) {
        if (key !== 'current') continue;
        const present = val !== null && val !== undefined;
        if (present && !obs.latched) {
          // null -> present transition = one hand end.
          obs.latched = true;
          obs.ends.push({
            winner: typeof val.winner === 'number' ? val.winner : null,
            type: typeof val.type === 'string' ? val.type : null,
            nextBanker: typeof val.nextBanker === 'number' ? val.nextBanker : null,
            at: Date.now(),
          });
        } else if (!present) {
          if (obs.latched) obs.clears++;
          obs.latched = false; // tombstone re-arms the latch for the next hand.
        }
      }
    });
    return true;
  });
}

/** OBSERVE — read back the in-page hand-end latch installed above. */
export async function readHandEndObserver(page: Page): Promise<HandEndObservation> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = window as any;
    const obs = w.__handEndObs;
    if (!obs) return { installed: false, ends: [], clears: 0 };
    // Backup re-arm: the in-page listener latches on each null->present flip and
    // unlatches on the null tombstone, but a single tombstone `update` can be
    // missed under load — which would silently merge two hands into one count.
    // The between-hands null PERSISTS for seconds, so re-arm here whenever the
    // stored result is currently null: a coarse caller poll reliably observes
    // that durable null even if the transient `update` slipped past the listener.
    const cur = w.game?.client?.result?.get('current');
    if (cur === null || cur === undefined) obs.latched = false;
    return {
      installed: true,
      ends: obs.ends.slice(),
      clears: obs.clears,
    };
  });
}

export interface MatchView {
  present: boolean;
  dealer: number | null;
  raw: unknown;
}

/** OBSERVE — the match singleton {dealer, honba, conditions}. */
export async function readMatch(page: Page): Promise<MatchView> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    try {
      const c = g?.client?.match;
      // match is keyed by 0 (numeric) server-side; tolerate both.
      const cur = c?.get(0) ?? c?.get('0');
      if (cur === null || cur === undefined) {
        return { present: false, dealer: null, raw: null };
      }
      return {
        present: true,
        dealer: typeof cur.dealer === 'number' ? cur.dealer : null,
        raw: cur,
      };
    } catch (e) {
      return { present: false, dealer: null, raw: String(e) };
    }
  });
}

export interface MaxHandsView {
  value: number | null;
  source: string;
}

/**
 * OBSERVE — the SERVER-AUTHORITATIVE match length (MaxHands), read from the
 * best available real client state. This is the anti-evasion probe for the
 * "handCount is real, not decorative" decision: a requested non-default
 * handCount MUST be reflected here. Scans (in priority order) the gameComplete
 * payload's maxHands and the match/conditions config. Returns null when the
 * server never surfaces its cap — which itself is a reportable gap (the client
 * must be able to observe the honored MaxHands without a backdoor).
 */
export async function readMaxHands(page: Page): Promise<MaxHandsView> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const num = (v: any): number | null =>
      typeof v === 'number' && Number.isFinite(v) && v > 0 ? v : null;
    try {
      // 1) gameComplete payload (server-authoritative, present on completion).
      const gc = g?.client?.gameComplete?.get('current');
      if (gc) {
        const v = num(gc.maxHands) ?? num(gc.MaxHands);
        if (v !== null) return { value: v, source: 'gameComplete' };
      }
      // 2) match config / conditions (server game config, if surfaced there).
      const m = g?.client?.match?.get(0) ?? g?.client?.match?.get('0');
      if (m) {
        const v =
          num(m.maxHands) ??
          num(m.MaxHands) ??
          num(m.conditions?.maxHands) ??
          num(m.conditions?.MaxHands) ??
          num(m.conditions?.hands);
        if (v !== null) return { value: v, source: 'match' };
      }
      return { value: null, source: 'none' };
    } catch (e) {
      return { value: null, source: String(e) };
    }
  });
}

/** OBSERVE — does the local seat currently hold a 14th tile (must discard)? */
export async function hasExtraHandTile(page: Page): Promise<boolean> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = (window as any).game?.world;
    try {
      return typeof w?.hasExtraHandTile === 'function'
        ? Boolean(w.hasExtraHandTile())
        : false;
    } catch {
      return false;
    }
  });
}

export interface ClaimView {
  open: boolean;
  available: string[];
  raw: unknown;
}

/** OBSERVE — is there a claim window targeting the local seat, and which types? */
export async function readClaimWindow(page: Page): Promise<ClaimView> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    try {
      const seat = g?.client?.seat;
      if (typeof seat !== 'number') return { open: false, available: [], raw: null };
      const cur = g?.client?.claim?.get(String(seat));
      if (cur === null || cur === undefined) return { open: false, available: [], raw: null };
      const available = Array.isArray(cur.available) ? cur.available.slice() : [];
      return { open: true, available, raw: cur };
    } catch (e) {
      return { open: false, available: [], raw: String(e) };
    }
  });
}

/** OBSERVE — id list of the local seat's own concealed hand tiles. */
export async function readMyHandTiles(page: Page): Promise<number[]> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = (window as any).game?.world;
    if (!w || typeof w.toSelect !== 'function') return [];
    const seat = w.seat;
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return w
      .toSelect()
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      .filter((s: any) => {
        const t = w.things.get(s.id);
        return (
          t &&
          t.slot?.group === 'hand' &&
          t.slot?.seat === seat &&
          !String(t.slot?.name ?? '').startsWith('hand.extra@')
        );
      })
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      .map((s: any) => s.id as number);
  });
}

/** OBSERVE — total discard-pile size (across seats) for progress detection. */
export async function readDiscardCount(page: Page): Promise<number> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = (window as any).game?.world;
    if (!w || !w.things) return 0;
    let n = 0;
    for (const t of w.things.values()) {
      if (t?.slot?.group === 'discard') n++;
    }
    return n;
  });
}

export interface TileScreenPos {
  ok: boolean;
  reason?: string;
  clientX: number;
  clientY: number;
}

/**
 * OBSERVE — project a hand tile's 3D world position to canvas pixel
 * coordinates using the live camera. This tells the pointer WHERE to click;
 * it does not itself discard anything. Mirrors the projection proven in
 * playtest-full-game-integration.spec.mjs.
 */
export async function projectTileToCanvas(page: Page, tileId: number): Promise<TileScreenPos> {
  return page.evaluate((id: number) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    const w = g?.world;
    if (!w) return { ok: false, reason: 'no world', clientX: 0, clientY: 0 };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const sel = w.toSelect().find((s: any) => s.id === id);
    if (!sel) return { ok: false, reason: `tile ${id} not selectable`, clientX: 0, clientY: 0 };
    const camera = g?.mainView?.camera;
    if (!camera) return { ok: false, reason: 'no mainView.camera', clientX: 0, clientY: 0 };
    try {
      if (camera.parent) camera.parent.updateMatrixWorld(true);
      camera.updateMatrixWorld(true);
      if (camera.matrixWorldInverse?.copy) {
        camera.matrixWorldInverse.copy(camera.matrixWorld).invert();
      }
    } catch {
      /* best effort */
    }
    const pos = { x: sel.position.x, y: sel.position.y, z: sel.position.z };
    const mw = camera.matrixWorldInverse.elements;
    const vx = mw[0] * pos.x + mw[4] * pos.y + mw[8] * pos.z + mw[12];
    const vy = mw[1] * pos.x + mw[5] * pos.y + mw[9] * pos.z + mw[13];
    const vz = mw[2] * pos.x + mw[6] * pos.y + mw[10] * pos.z + mw[14];
    const vw = mw[3] * pos.x + mw[7] * pos.y + mw[11] * pos.z + mw[15];
    const pm = camera.projectionMatrix.elements;
    const cx = pm[0] * vx + pm[4] * vy + pm[8] * vz + pm[12] * vw;
    const cy = pm[1] * vx + pm[5] * vy + pm[9] * vz + pm[13] * vw;
    const cw = pm[3] * vx + pm[7] * vy + pm[11] * vz + pm[15] * vw;
    const ndcX = cx / cw;
    const ndcY = cy / cw;
    const main = document.getElementById('main');
    if (!main) return { ok: false, reason: 'no #main canvas', clientX: 0, clientY: 0 };
    const rect = main.getBoundingClientRect();
    return {
      ok: true,
      clientX: rect.left + (ndcX + 1) * 0.5 * rect.width,
      clientY: rect.top + (1 - ndcY) * 0.5 * rect.height,
    };
  }, tileId);
}

// -----------------------------------------------------------------------------
//  ADVANCE primitives — the ONLY functions allowed to move the game forward,
//  all via real Playwright pointer / click events.
// -----------------------------------------------------------------------------

/** ADVANCE — close the lobby panel and any onboarding, via real clicks. */
export async function dismissLobbyAndTour(page: Page): Promise<void> {
  for (const sel of ['#tour-skip', '#onboarding-skip', '#lobby-close']) {
    const el = page.locator(sel);
    if (await el.first().isVisible().catch(() => false)) {
      await el.first().click({ force: true, timeout: 3000 }).catch(() => undefined);
      await page.waitForTimeout(300);
    }
  }
}

/**
 * ADVANCE — ensure the WS is connected. The client auto-connects on load when
 * a gameId is present; if it hasn't after a beat, click the real #connect
 * button (the affordance a user would use).
 */
export async function ensureConnected(page: Page, timeoutMs = 20_000): Promise<boolean> {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    if (await readConnected(page)) return true;
    const connect = page.locator('#connect');
    if (await connect.first().isVisible().catch(() => false)) {
      await connect.first().click({ timeout: 3000 }).catch(() => undefined);
    }
    await page.waitForTimeout(500);
  }
  return readConnected(page);
}

/**
 * ADVANCE — take a seat by clicking the real .take-seat button for the chair
 * matching cfg.seat (falls back to the first visible one). Returns the seat the
 * server assigned (read back from client.seat).
 */
export async function takeSeatByClick(page: Page, seat: number): Promise<number | null> {
  const preferred = page.locator(`.seat-button-${seat} .take-seat`);
  if (await preferred.first().isVisible().catch(() => false)) {
    await preferred.first().click({ timeout: 5000 }).catch(() => undefined);
  } else {
    const anySeat = page.locator('.take-seat');
    const count = await anySeat.count();
    for (let i = 0; i < count; i++) {
      if (await anySeat.nth(i).isVisible().catch(() => false)) {
        await anySeat.nth(i).click({ timeout: 5000 }).catch(() => undefined);
        break;
      }
    }
  }
  await page.waitForTimeout(1500);
  return readSeat(page);
}

/**
 * ADVANCE — press the real #deal button. The current bundle binds a plain
 * `click` handler (game-ui.ts setupDealButton → world.deal), which then
 * client-drives the manual pickup ceremony. A single real click is the whole
 * gesture; we do NOT synthesise anything further.
 */
export async function clickDeal(page: Page): Promise<boolean> {
  const deal = page.locator('#deal');
  if (!(await deal.first().isVisible().catch(() => false))) return false;
  await deal.first().click({ timeout: 5000 }).catch(() => undefined);
  await page.waitForTimeout(500);
  return true;
}

export interface PickupView {
  present: boolean;
  phase: string | null;
  seatIndex: number | null;
  count: number | null;
  raw: unknown;
}

/** OBSERVE — the current manual-deal pickup cursor (phase/seat/count). */
export async function readPickup(page: Page): Promise<PickupView> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const g = (window as any).game;
    try {
      const cur = g?.client?.pickup?.get('current');
      if (cur === null || cur === undefined) {
        return { present: false, phase: null, seatIndex: null, count: null, raw: null };
      }
      return {
        present: true,
        phase: typeof cur.phase === 'string' ? cur.phase : null,
        seatIndex: typeof cur.seatIndex === 'number' ? cur.seatIndex : null,
        count: typeof cur.count === 'number' ? cur.count : null,
        raw: cur,
      };
    } catch (e) {
      return { present: false, phase: null, seatIndex: null, count: null, raw: String(e) };
    }
  });
}

export interface PlayableHandResult {
  playable: boolean;
  myHandCount: number;
  lastPickup: PickupView;
  elapsedMs: number;
}

/**
 * OBSERVE — wait until the local seat holds a playable 14th tile
 * (hasExtraHandTile), i.e. the manual deal ceremony delivered a hand and it's
 * our turn to discard. Returns the outcome plus the last pickup cursor so a
 * stall can be reported precisely (e.g. stuck in DealerExtra). Performs NO
 * game interaction — the ceremony is client-auto-driven after clickDeal.
 */
export async function waitForPlayableHand(
  page: Page,
  timeoutMs = 45_000,
): Promise<PlayableHandResult> {
  const t0 = Date.now();
  let lastPickup = await readPickup(page);
  while (Date.now() - t0 < timeoutMs) {
    if (await hasExtraHandTile(page)) {
      return {
        playable: true,
        myHandCount: (await readMyHandTiles(page)).length,
        lastPickup: await readPickup(page),
        elapsedMs: Date.now() - t0,
      };
    }
    lastPickup = await readPickup(page);
    await page.waitForTimeout(1000);
  }
  return {
    playable: false,
    myHandCount: (await readMyHandTiles(page)).length,
    lastPickup,
    elapsedMs: Date.now() - t0,
  };
}

export interface DiscardOutcome {
  ok: boolean;
  tileId: number | null;
  reason: string;
  discardBefore: number;
  discardAfter: number;
}

/**
 * ADVANCE — discard exactly the way a human does: hover a hand tile so the
 * raycaster sets world.hovered, then press the left mouse button on the #main
 * canvas. MouseUi.onMouseDown → World.onDragStart consumes the press and, when
 * hasExtraHandTile() is true, emits the discard. NO direct emitDiscard call.
 */
export async function discardByPointer(page: Page): Promise<DiscardOutcome> {
  const discardBefore = await readDiscardCount(page);
  const tiles = await readMyHandTiles(page);
  if (tiles.length === 0) {
    return {
      ok: false, tileId: null, reason: 'no own-hand tiles selectable',
      discardBefore, discardAfter: discardBefore,
    };
  }
  // Try from the middle of the rack outward — edge tiles can miss on sub-pixel
  // projection boundaries.
  const mid = Math.floor(tiles.length / 2);
  const order: number[] = [mid];
  for (let off = 1; order.length < Math.min(6, tiles.length); off++) {
    if (mid + off < tiles.length) order.push(mid + off);
    if (mid - off >= 0 && order.length < 6) order.push(mid - off);
  }

  for (const idx of order) {
    const tileId = tiles[idx];
    const proj = await projectTileToCanvas(page, tileId);
    if (!proj.ok) continue;
    // Real hover, then real press+release on the canvas.
    await page.mouse.move(proj.clientX, proj.clientY, { steps: 8 });
    await page.waitForTimeout(120);
    await page.mouse.down();
    await page.waitForTimeout(90);
    await page.mouse.up();
    await page.waitForTimeout(1200);
    const discardAfter = await readDiscardCount(page);
    if (discardAfter > discardBefore) {
      return { ok: true, tileId, reason: 'discard pile grew', discardBefore, discardAfter };
    }
  }
  const discardAfter = await readDiscardCount(page);
  return {
    ok: false, tileId: null,
    reason: 'pointer discard did not grow the discard pile (see world.onDragStart/emitDiscard drift)',
    discardBefore, discardAfter,
  };
}

/**
 * ADVANCE — respond to a claim window with a real button click. Prefers a real
 * Hu (win) when offered, otherwise passes. Returns which button was clicked.
 *
 * #137: click the ON-TOP claim overlay (`.ferro-claim-*`) — the primary claim UI
 * a real player sees and clicks — rather than the side-panel `#claim-*` buttons.
 * The 720px-wide bottom-center overlay (z-index 1080, pointer-events:auto while a
 * window is open) overlaps the side-panel controls, so a click aimed at the
 * occluded `#claim-pass` / `#claim-hu` is intermittently hit-tested onto an
 * overlay MELD badge and commits an accidental Pung/Chow — which then floods the
 * page with uncaught errors and wedges the hand. The overlay's Hu badge and Pass
 * button are the top layer (Pass sits in its own grid column, never under a meld
 * badge), so they receive the click unambiguously. Side-panel is a defensive
 * fallback only (e.g. if the overlay isn't mounted).
 */
export async function claimByClick(page: Page): Promise<string | null> {
  const claim = await readClaimWindow(page);
  if (!claim.open) return null;
  const wantHu = claim.available.includes('Hu');
  if (wantHu) {
    // Overlay Hu badge (lit + enabled only when Hu is available), else side-panel.
    const huBadge = page.locator('.ferro-claim-badge-hu');
    if (await huBadge.first().isEnabled().catch(() => false)) {
      await huBadge.first().click({ timeout: 3000 }).catch(() => undefined);
      await page.waitForTimeout(400);
      return 'Hu';
    }
    const sideHu = page.locator('#claim-hu');
    if (await sideHu.first().isEnabled().catch(() => false)) {
      await sideHu.first().click({ timeout: 3000 }).catch(() => undefined);
      await page.waitForTimeout(400);
      return 'Hu';
    }
  }
  // Decline via the overlay Pass button (its own column — never under a meld badge).
  const overlayPass = page.locator('.ferro-claim-pass');
  if (await overlayPass.first().isVisible().catch(() => false)) {
    await overlayPass.first().click({ timeout: 3000 }).catch(() => undefined);
    await page.waitForTimeout(400);
    return 'Pass';
  }
  const sidePass = page.locator('#claim-pass');
  if (await sidePass.first().isEnabled().catch(() => false)) {
    await sidePass.first().click({ timeout: 3000 }).catch(() => undefined);
    await page.waitForTimeout(400);
    return 'Pass';
  }
  return null;
}

/** OBSERVE — the live camera kind ('perspective' | 'orthographic' | null). */
export async function readCameraType(page: Page): Promise<'perspective' | 'orthographic' | null> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const cam = (window as any).game?.mainView?.camera;
    if (!cam) return null;
    if (cam.isPerspectiveCamera === true) return 'perspective';
    if (cam.isOrthographicCamera === true) return 'orthographic';
    return null;
  });
}

/**
 * ADVANCE (view only) — toggle the flat/perspective view the way a user does:
 * a real 'p' keypress (game.ts onKeyDown → MainView.setPerspective). Returns the
 * camera kind after the toggle. Does not affect gameplay state.
 */
export async function pressViewToggle(page: Page): Promise<'perspective' | 'orthographic' | null> {
  // Ensure a game-level element holds focus (not a text input, which game.ts
  // ignores) without clicking the canvas (which would select/discard a tile).
  await page.evaluate(() => {
    const el = document.activeElement as HTMLElement | null;
    if (el && el.tagName === 'INPUT') el.blur();
  });
  await page.keyboard.press('p');
  await page.waitForTimeout(300);
  return readCameraType(page);
}

/** OBSERVE — server-authoritative per-seat totals at completion (for zero-sum). */
export async function readTotalScores(page: Page): Promise<Record<string, number> | null> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const gc = (window as any).game?.client?.gameComplete?.get('current');
    if (!gc) return null;
    const raw = gc.totalScores ?? gc.TotalScores;
    if (raw === null || raw === undefined) return null;
    const out: Record<string, number> = {};
    for (const k of Object.keys(raw)) {
      const v = Number(raw[k]);
      if (Number.isFinite(v)) out[String(k)] = v;
    }
    return out;
  });
}

/** OBSERVE — does the runtime currently expect the local seat to pick up? */
export async function readIsMyPickupTurn(page: Page): Promise<boolean> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = (window as any).game?.world;
    return typeof w?.isMyPickupTurn === 'function' ? Boolean(w.isMyPickupTurn()) : false;
  });
}

/** OBSERVE — count of the local seat's backend-authoritative concealed hand tiles. */
export async function countMyHandTiles(page: Page): Promise<number> {
  return page.evaluate(() => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const w = (window as any).game?.world;
    if (!w || !w.things) return 0;
    const seat = w.seat;
    let n = 0;
    for (const t of w.things.values()) {
      if (
        t?.slot?.group === 'hand' &&
        t.slot?.seat === seat &&
        t.slot?.thing === t &&
        !String(t.slot?.name ?? '').startsWith('hand.extra@')
      ) {
        n++;
      }
    }
    return n;
  });
}

export interface PickupOutcome {
  ok: boolean;
  reason: string;
  handBefore: number;
  handAfter: number;
}

/**
 * ADVANCE — the human's manual pickup: when the runtime expects our seat to
 * draw, the pickup HUD shows "Your turn — pick N tiles" with a real Take-N
 * button (#pickup-take-btn, game-ui.ts). Click it — that is the human
 * affordance (its onclick calls world.emitTakePickup). driveManualDealChain
 * auto-drives hand 1; this real click drives hands 2..N.
 */
export async function takePickup(page: Page): Promise<PickupOutcome> {
  const handBefore = await countMyHandTiles(page);
  if (!(await readIsMyPickupTurn(page))) {
    return { ok: false, reason: 'not my pickup turn', handBefore, handAfter: handBefore };
  }
  const btn = page.locator('#pickup-take-btn');
  const deadline = Date.now() + 6000;
  while (Date.now() < deadline) {
    const vis = await btn.first().isVisible().catch(() => false);
    const en = vis && (await btn.first().isEnabled().catch(() => false));
    if (vis && en) {
      await btn.first().click({ timeout: 3000 }).catch(() => undefined);
      await page.waitForTimeout(700);
      const handAfter = await countMyHandTiles(page);
      const stillMyTurn = await readIsMyPickupTurn(page);
      if (handAfter > handBefore || !stillMyTurn) {
        return { ok: true, reason: 'take button clicked', handBefore, handAfter };
      }
      // Still our turn with no change — the HUD may be mid-refresh; loop.
    }
    await page.waitForTimeout(350);
  }
  return { ok: false, reason: 'take button not actionable', handBefore, handAfter: await countMyHandTiles(page) };
}

/** OBSERVE — is the per-hand #result-modal (scoring panel) visible? */
export async function isResultModalVisible(page: Page): Promise<boolean> {
  return page.evaluate(() => {
    const el = document.getElementById('result-modal');
    if (!el) return false;
    const s = window.getComputedStyle(el);
    return (el.classList.contains('show') || s.display === 'block') && s.visibility !== 'hidden';
  });
}

/**
 * ADVANCE — click the real "Next Hand" (#result-next) button in the per-hand
 * result modal to proceed to the next hand (sends match[1]={action:'nextHand'}
 * through the normal UI path — NOT a backdoor). Waits for it to be actionable.
 */
export async function clickNextHand(page: Page, timeoutMs = 8000): Promise<boolean> {
  const btn = page.locator('#result-next');
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const vis = await btn.first().isVisible().catch(() => false);
    const en = vis && (await btn.first().isEnabled().catch(() => false));
    if (vis && en) {
      await btn.first().click({ timeout: 3000 }).catch(() => undefined);
      await page.waitForTimeout(600);
      return true;
    }
    await page.waitForTimeout(400);
  }
  return false;
}

export async function isGameCompleteModalVisible(page: Page): Promise<boolean> {
  const modal = page.locator('#game-complete-modal');
  if ((await modal.count()) === 0) return false;
  // Bootstrap toggles display:block + .show; check both computed visibility
  // and the .show class so a mid-transition modal still counts.
  return page.evaluate(() => {
    const el = document.getElementById('game-complete-modal');
    if (!el) return false;
    const style = window.getComputedStyle(el);
    const shown = el.classList.contains('show') || style.display === 'block';
    return shown && style.visibility !== 'hidden' && style.display !== 'none';
  });
}
