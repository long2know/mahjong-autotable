// Ferro — WP-E / #120 — D-1 protocol-conformance static gate.
//
// Protects the #1 cross-component contract (C-1, the WS **Collection**
// protocol) by statically asserting that the set of collection *kinds* the
// frontend can put on the wire is a subset of what the backend's
// `AutotableWsEndpoint.HandleUpdateAsync` actually dispatches — so a rename
// or a newly-added collection on either side fails CI instead of silently
// half-shipping.
//
// This is a **static** gate: it reads `client.ts` and the backend
// `AutotableWsEndpoint.cs` / `AutotableProtocol.cs` off disk and never
// touches a browser or a running server.  It runs once (chromium project).

import { test, expect } from '@playwright/test';
import * as fs from 'fs';
import * as path from 'path';

// The frozen C-1 kind set (see .squad issue #120 "Locked cross-component
// contracts" and selectors.md).  Adding/removing a collection kind is a
// contract change and MUST update this list in the same PR.
const C1_KINDS = [
  'match', 'seats', 'things', 'nicks', 'mouse', 'sound',
  'dice', 'claim', 'pickup', 'discard', 'result', 'gameComplete',
].sort();

// Server → client only.  The FE receives these; it never sends them as
// commands (C-1: "result / gameComplete are inbound-only").
const INBOUND_ONLY = new Set(['result', 'gameComplete']);

// FE → server game commands.  The backend MUST route each of these through
// an *explicit* case in HandleUpdateAsync — a silent default-passthrough
// would break authoritative play.
const GAME_COMMAND_KINDS = new Set(['seats', 'claim', 'pickup', 'discard', 'match']);

// Cosmetic / meta kinds the backend accepts via its default passthrough
// (mouse / sound / dice / things / nicks).  Kept explicit so a *new* FE
// kind can't quietly slip into the catch-all without a human classifying it.
const PASSTHROUGH_KINDS = new Set(['things', 'nicks', 'mouse', 'sound', 'dice']);

function readRepo(rel: string): string {
  // __dirname = <repo>/src/frontend/autotable-src/tests/e2e
  return fs.readFileSync(path.resolve(__dirname, rel), 'utf-8');
}

/** Every collection kind the FE constructs (`new Collection('<kind>', …)`). */
function feCollectionKinds(): Set<string> {
  const src = readRepo('../../src/client.ts');
  const kinds = new Set<string>();
  for (const m of src.matchAll(/new\s+Collection\(\s*['"]([A-Za-z][A-Za-z0-9]*)['"]/g)) {
    kinds.add(m[1]);
  }
  return kinds;
}

/** Map `ChangshaCollectionKinds.<Name>` → wire string from AutotableProtocol.cs. */
function changshaKindConstants(): Map<string, string> {
  const src = readRepo('../../../../../src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableProtocol.cs');
  const map = new Map<string, string>();
  for (const m of src.matchAll(/public\s+const\s+string\s+(\w+)\s*=\s*"([^"]+)"/g)) {
    map.set(m[1], m[2]);
  }
  return map;
}

/** The kinds HandleUpdateAsync dispatches with an *explicit* `case`. */
function backendExplicitDispatchKinds(): Set<string> {
  const src = readRepo('../../../../../src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs');
  const marker = 'private async Task HandleUpdateAsync(';
  const start = src.indexOf(marker);
  expect(start, 'HandleUpdateAsync not found — endpoint renamed?').toBeGreaterThan(-1);
  // Slice to the next method declaration so we only scan this switch body.
  const rest = src.slice(start + marker.length);
  const nextMethod = rest.search(/\n {4}private\s+(async\s+)?\w/);
  const body = nextMethod > -1 ? rest.slice(0, nextMethod) : rest;

  const consts = changshaKindConstants();
  const kinds = new Set<string>();
  for (const m of body.matchAll(/case\s+"([^"]+)"\s*:/g)) kinds.add(m[1]);
  for (const m of body.matchAll(/case\s+ChangshaCollectionKinds\.(\w+)\s*:/g)) {
    const val = consts.get(m[1]);
    if (val) kinds.add(val);
  }
  return kinds;
}

test.describe('WP-E/#120 — WS Collection protocol conformance (C-1) static gate', () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Static gate runs once (chromium project).');
  });

  test('FE collection kinds exactly match the frozen C-1 contract set', () => {
    const fe = [...feCollectionKinds()].sort();
    expect(
      fe,
      'client.ts collection kinds drifted from the frozen C-1 set — update the ' +
      'contract (C1_KINDS here + selectors.md) and the backend dispatch together.',
    ).toEqual(C1_KINDS);
  });

  test('every FE-sent kind is known to the backend HandleUpdateAsync dispatch', () => {
    const fe = feCollectionKinds();
    const explicit = backendExplicitDispatchKinds();

    // Partition sanity: what the FE can *send* is exactly the game commands
    // plus the cosmetic passthrough set (inbound-only kinds are never sent).
    const feSent = [...fe].filter((k) => !INBOUND_ONLY.has(k)).sort();
    const expectedSent = [...new Set([...GAME_COMMAND_KINDS, ...PASSTHROUGH_KINDS])].sort();
    expect(
      feSent,
      'A FE-sent kind is neither a classified game command nor a cosmetic ' +
      'passthrough kind — classify it before shipping (protects C-1).',
    ).toEqual(expectedSent);

    // The load-bearing check: every game-command kind is *explicitly*
    // dispatched by the backend (not silently relayed via `default`).
    for (const kind of GAME_COMMAND_KINDS) {
      expect(
        explicit.has(kind),
        `Backend HandleUpdateAsync has no explicit case for FE game-command '${kind}' — ` +
        'the FE would send it but the runtime would only see a cosmetic relay.',
      ).toBe(true);
    }

    // Every FE-sent kind must be handled somehow: explicit dispatch OR the
    // documented cosmetic passthrough.  Nothing may fall through unclassified.
    for (const kind of feSent) {
      const handled = explicit.has(kind) || PASSTHROUGH_KINDS.has(kind);
      expect(handled, `FE-sent kind '${kind}' has no backend handling path.`).toBe(true);
    }
  });

  test('inbound-only kinds are never classified as FE→server commands', () => {
    for (const kind of INBOUND_ONLY) {
      expect(
        GAME_COMMAND_KINDS.has(kind),
        `'${kind}' is inbound-only (server → client) and must not be a game command.`,
      ).toBe(false);
    }
    // And the backend must explicitly ignore client 'result' pushes.
    const explicit = backendExplicitDispatchKinds();
    expect(explicit.has('result'), "backend must explicitly handle (ignore) client 'result' pushes").toBe(true);
  });
});
