/**
 * autotableBridge.embedded.test.ts
 *
 * Phase 5a contract: when the autotable iframe loads with `?embedded=1`,
 * the upstream sidebar (Connect/Deal/seat picker) MUST be hidden so the
 * user can't trigger the bundled Riichi flow that would override our
 * authoritative Changsha state.
 *
 * Hicks's Phase 5a (commit 1c1bd4a) implements this via an inline
 * `<script>` in `src/frontend/autotable/index.html` plus a CSS rule:
 *   <style id=changsha-embedded-mode>
 *     html[data-changsha-embedded="1"] #sidebar,
 *     html[data-changsha-embedded="1"] .seat-buttons {
 *       display: none !important;
 *     }
 *   </style>
 *   <script>
 *     if (new URLSearchParams(location.search).has('embedded'))
 *       documentElement.setAttribute('data-changsha-embedded','1');
 *   </script>
 *
 * ── Test strategy ───────────────────────────────────────────────────────
 * The runtime path is inline-script-on-iframe-navigation, which is NOT
 * reachable from vitest under jsdom (we don't navigate to bundle HTML in
 * unit tests). Two coverage approaches:
 *  1. **Static fixture check** (active): read index.html from disk and
 *     assert the inline script + CSS rule are present and well-formed.
 *     Guards against accidental removal during future bundle re-mirrors.
 *  2. **Manual e2e** (skipped placeholder): document the manual repro.
 *  3. **Receiver fallback** (skipped placeholder): only used if Hicks
 *     ever moves the logic into the receiver script.
 */
import { describe, it, expect, beforeEach } from 'vitest';
// Node built-ins are available at runtime (vitest runs in Node), but the
// frontend tsconfig intentionally omits @types/node to keep app code
// browser-only. The two ambient declarations below let TS compile this
// test file without pulling node types into the wider build.
// eslint-disable-next-line @typescript-eslint/no-explicit-any
declare const require: (id: string) => any;
declare const __dirname: string;
// eslint-disable-next-line @typescript-eslint/no-require-imports
const fs = require('node:fs');
// eslint-disable-next-line @typescript-eslint/no-require-imports
const path = require('node:path');

// __dirname == src/frontend/modern/src/changsha/__tests__
//   ..(1)=changsha ..(2)=src(modern) ..(3)=modern ..(4)=frontend
const AUTOTABLE_DIR = path.resolve(__dirname, '../../../../autotable');
const RECEIVER_PATH = path.resolve(
  AUTOTABLE_DIR,
  'changsha-bridge-receiver.js'
);
const INDEX_HTML_PATH = path.resolve(AUTOTABLE_DIR, 'index.html');

function readReceiverSource(): string {
  return fs.readFileSync(RECEIVER_PATH, 'utf-8');
}
function readIndexHtml(): string {
  return fs.readFileSync(INDEX_HTML_PATH, 'utf-8');
}

describe('autotableBridge / embedded-mode sidebar hide', () => {
  beforeEach(() => {
    document.body.innerHTML = '';
    document.body.removeAttribute('data-embedded');
    document.body.classList.remove('embedded');
    document.documentElement.removeAttribute('data-changsha-embedded');
  });

  // ── Active: static fixture check on index.html ──────────────────────────
  // The canonical sidebar-hide path is an inline <script> + <style> in
  // src/frontend/autotable/index.html. We can't exercise that script in
  // jsdom (no real iframe navigation), but we CAN parse the file and
  // confirm the contract artifacts are present. This guards against an
  // accidental upstream re-mirror clobbering Hicks's Phase 5a edits.
  it('index.html contains the embedded-mode inline script + CSS rule (Hicks Phase 5a contract)', () => {
    const html = readIndexHtml();

    // CSS rule must hide #sidebar AND .seat-buttons when the data attribute
    // is set on <html>. Regex tolerates whitespace / minification.
    expect(html).toMatch(
      /html\[data-changsha-embedded=["']?1["']?\][^{}]*#sidebar[^{}]*\{[^}]*display\s*:\s*none/i
    );
    expect(html).toMatch(/\.seat-buttons/);

    // Inline script must read URLSearchParams and set the data attribute.
    expect(html).toMatch(/URLSearchParams\s*\(\s*window\.location\.search\s*\)/);
    expect(html).toMatch(/has\(\s*['"]embedded['"]\s*\)/);
    expect(html).toMatch(
      /setAttribute\(\s*['"]data-changsha-embedded['"]\s*,\s*['"]1['"]\s*\)/
    );

    // Sandbox preserved (Default #2): the sidebar div STILL exists in
    // the DOM tree — it's only display:none'd by CSS when embedded.
    expect(html).toMatch(/id=["']?sidebar["']?/);
  });

  it.skip(
    '[MANUAL / INDEX.HTML] ?embedded=1 hides the upstream sidebar — verified manually',
    () => {
      // Manual repro:
      //   1. `npm --prefix src/backend run watch`
      //   2. Open http://localhost:5114/autotable/?embedded=1
      //   3. Expect: no #sidebar visible (no Connect / Deal / seat picker)
      //   4. Open http://localhost:5114/autotable/  (no query)
      //   5. Expect: #sidebar visible (standalone sandbox unchanged)
      //
      // The implementation is an inline <script> in index.html plus a CSS
      // rule on html[data-changsha-embedded="1"]. Inline scripts in
      // index.html cannot be exercised by vitest in jsdom because we
      // don't navigate to the bundle's HTML page in unit tests. The
      // static-fixture test above covers the source-level invariant; this
      // manual step covers the runtime behavior end-to-end.
    }
  );

  it.skip(
    '[NOT IMPLEMENTED — fallback path] receiver applies embedded class when location.search has embedded=1',
    () => {
      // This test exercises an alternative implementation strategy that
      // was NOT chosen: the receiver script reads window.location.search
      // and toggles a class on <body>. Hicks's Phase 5a (commit 1c1bd4a)
      // chose the inline-script-in-index.html path instead. UN-SKIP this
      // test only if a future refactor moves the logic into the receiver.

      // Set up jsdom URL to include embedded=1
      Object.defineProperty(window, 'location', {
        writable: true,
        value: { ...window.location, search: '?embedded=1' },
      });

      const source = readReceiverSource();
      // eslint-disable-next-line no-eval
      (0, eval)(source);

      const hasMarker =
        document.body.hasAttribute('data-embedded') ||
        document.body.classList.contains('embedded');
      expect(hasMarker).toBe(true);
    }
  );

  // ── Negative control: receiver doesn't crash when location.search is empty
  it('receiver loads cleanly when no embedded=1 marker is present', () => {
    // eslint-disable-next-line no-eval
    (0, eval)(readReceiverSource());

    // No body marker should appear (sidebar stays visible in standalone mode)
    expect(document.body.hasAttribute('data-embedded')).toBe(false);
    expect(document.body.classList.contains('embedded')).toBe(false);
  });
});
