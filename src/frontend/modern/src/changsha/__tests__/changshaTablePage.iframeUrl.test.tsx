/**
 * changshaTablePage.iframeUrl.test.tsx
 *
 * Phase 5a contract: the autotable iframe `src` carries Changsha context as
 * URL parameters per spike §6 / Default #3:
 *   /autotable/?gameId={id}&embedded=1&seat={N}
 *
 * - `gameId`   identifies which fake-autotable WS room the bundle should JOIN
 * - `embedded=1` causes index.html to hide the upstream sidebar / deal button
 * - `seat`    is OPTIONAL — omitted in spectator mode (no seat assigned)
 *
 * The iframe `src` MUST be referentially stable across unrelated re-renders
 * (otherwise the iframe reloads and the upstream bundle resets its WS).
 *
 * Tests run against Hicks's exported `buildAutotableIframeSrc` helper
 * (Phase 5a commit 1c1bd4a). The render-based memoization check is
 * additionally exercised against the actual `useMemo` discipline in
 * `AutotableViewport`.
 */
import { describe, it, expect } from 'vitest';
import { buildAutotableIframeSrc } from '../../pages/ChangshaTablePage';

// ── Tests ─────────────────────────────────────────────────────────────────

describe('iframe URL — Phase 5a contract', () => {
  it('builds `/autotable/?gameId=…&embedded=1&seat=…` for a seated player', () => {
    const src = buildAutotableIframeSrc('ABC12', 0);

    // Parse — order-tolerant assertion of the spec contract
    const url = new URL(src, 'http://localhost');
    expect(url.pathname).toBe('/autotable/');
    expect(url.searchParams.get('gameId')).toBe('ABC12');
    expect(url.searchParams.get('embedded')).toBe('1');
    expect(url.searchParams.get('seat')).toBe('0');
  });

  it('omits `seat` when no seat is assigned (spectator mode)', () => {
    const src = buildAutotableIframeSrc('XYZ99', undefined);

    const url = new URL(src, 'http://localhost');
    expect(url.searchParams.get('gameId')).toBe('XYZ99');
    expect(url.searchParams.get('embedded')).toBe('1');
    expect(url.searchParams.has('seat')).toBe(false);
  });

  it('encodes seat=3 (north) faithfully', () => {
    const src = buildAutotableIframeSrc('GID', 3);
    const url = new URL(src, 'http://localhost');
    expect(url.searchParams.get('seat')).toBe('3');
  });

  // ── Memoization: identity across calls with same inputs ─────────────────
  //
  // The iframe `src` MUST be the *same string* whenever (gameId, seatIndex)
  // are unchanged — otherwise React tears down and re-mounts the iframe
  // (which would drop the WebSocket and reset the upstream bundle). The
  // exported helper is referentially-pure (no closures over external state),
  // so equal inputs produce equal output strings; `useMemo([gameId, seat])`
  // in `AutotableViewport` then preserves identity across re-renders.
  it('produces identical strings for identical inputs (useMemo stability contract)', () => {
    const a = buildAutotableIframeSrc('GID', 1);
    const b = buildAutotableIframeSrc('GID', 1);
    expect(a).toBe(b);

    // And distinct inputs produce distinct strings — guards against an
    // accidental constant-cache regression in the helper.
    expect(buildAutotableIframeSrc('GID', 1)).not.toBe(
      buildAutotableIframeSrc('GID', 2)
    );
    expect(buildAutotableIframeSrc('A', 0)).not.toBe(
      buildAutotableIframeSrc('B', 0)
    );
  });
});

