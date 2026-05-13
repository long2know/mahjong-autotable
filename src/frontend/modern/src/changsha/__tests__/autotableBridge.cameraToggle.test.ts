/**
 * autotableBridge.cameraToggle.test.ts
 *
 * Phase 5a contract: per spike Default #3 (Open Question §3 → "keep
 * upstream's P keybind, expose a React button"), the receiver script
 * MUST translate an inbound bridge message
 *   { proto: 'changsha-bridge/1', type: 'camera-toggle' }
 * into a synthetic `keydown` on `document` with:
 *   - key  === 'p'
 *   - code === 'KeyP'
 *   - bubbles: true   ← so upstream's window-level listeners receive it
 *
 * The receiver is at:
 *   src/frontend/autotable/changsha-bridge-receiver.js
 *
 * We load it into the jsdom environment via fs.readFileSync + eval so we
 * exercise the actual script, not a re-implementation. Verified against
 * Hicks's Phase 5a commit 1c1bd4a.
 */
import { describe, it, expect, beforeEach, vi } from 'vitest';
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
// then  autotable/  →  src/frontend/autotable/changsha-bridge-receiver.js
const RECEIVER_PATH = path.resolve(
  __dirname,
  '../../../../autotable/changsha-bridge-receiver.js'
);

// Module-level: load the receiver ONCE. Re-loading per-test would stack
// duplicate `message` listeners on window (the receiver registers an
// anonymous handler we can't selectively remove). One load + per-test
// keydown-listener attach/detach gives clean isolation.
let receiverLoaded = false;
function ensureReceiverLoaded(): void {
  if (receiverLoaded) return;
  const source = fs.readFileSync(RECEIVER_PATH, 'utf-8');
  // The receiver is an IIFE that registers a `message` listener on window
  // and creates an overlay div. eval'ing it in the current jsdom context
  // wires it up against this test's window/document.
  // eslint-disable-next-line no-eval
  (0, eval)(source);
  receiverLoaded = true;
}

function postBridgeMessage(data: unknown): void {
  // The receiver listens on the `message` event and reads ev.data —
  // dispatching a MessageEvent directly is the cleanest way to simulate
  // a parent-window postMessage in jsdom.
  window.dispatchEvent(new MessageEvent('message', { data }));
}

describe('autotableBridge receiver — camera-toggle', () => {
  beforeEach(() => {
    ensureReceiverLoaded();
  });

  it(
    'camera-toggle postMessage synthesizes a keydown with key=p / code=KeyP',
    () => {
      const keydownSpy = vi.fn();
      document.addEventListener('keydown', keydownSpy);

      try {
        postBridgeMessage({
          proto: 'changsha-bridge/1',
          type: 'camera-toggle',
        });

        expect(keydownSpy).toHaveBeenCalledTimes(1);
        const ev = keydownSpy.mock.calls[0][0] as KeyboardEvent;
        expect(ev.key).toBe('p');
        expect(ev.code).toBe('KeyP');
      } finally {
        document.removeEventListener('keydown', keydownSpy);
      }
    }
  );

  it(
    'synthesized camera-toggle keydown bubbles (upstream window listeners pick it up)',
    () => {
      const windowSpy = vi.fn();
      // Upstream's three.js controls attach keydown listeners on window.
      // bubbles:true is what lets a document-level dispatch reach those.
      window.addEventListener('keydown', windowSpy);

      try {
        postBridgeMessage({
          proto: 'changsha-bridge/1',
          type: 'camera-toggle',
        });

        expect(windowSpy).toHaveBeenCalledTimes(1);
        const ev = windowSpy.mock.calls[0][0] as KeyboardEvent;
        expect(ev.bubbles).toBe(true);
      } finally {
        window.removeEventListener('keydown', windowSpy);
      }
    }
  );

  // ── Negative control: receiver still ignores messages with wrong proto ──
  // This test does NOT depend on Hicks's camera-toggle wire-up — it just
  // re-confirms the receiver's existing proto-sentinel discipline holds.
  it('ignores camera-toggle messages with wrong proto sentinel', () => {
    const keydownSpy = vi.fn();
    document.addEventListener('keydown', keydownSpy);
    window.addEventListener('keydown', keydownSpy);

    try {
      postBridgeMessage({
        proto: 'someone-else/2',
        type: 'camera-toggle',
      });
      postBridgeMessage({ type: 'camera-toggle' });
      postBridgeMessage('not-an-object');

      expect(keydownSpy).not.toHaveBeenCalled();
    } finally {
      document.removeEventListener('keydown', keydownSpy);
      window.removeEventListener('keydown', keydownSpy);
    }
  });
});
