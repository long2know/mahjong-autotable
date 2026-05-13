/**
 * autotableBridge.test.ts
 *
 * Exercises the postMessage bridge between the Changsha React app
 * (parent) and the bundled autotable iframe (child).
 *
 * The bridge's current protocol envelope is { proto: 'changsha-bridge/1',
 * type, ... }. The task brief described { type, version, payload } — the
 * actual code uses `proto` as the version sentinel. Tests assert the
 * **real** envelope and the **real** delivery mechanism (callback, not
 * CustomEvent dispatch — Phase 3 may add that, today there's a single
 * onInbound callback).
 */
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import {
  attachAutotableBridge,
  BRIDGE_PROTOCOL,
  type BridgeInboundMessage,
} from '../autotableBridge';

// ── Mock iframe + contentWindow ───────────────────────────────────────────

interface FakeContentWindow {
  postMessage: ReturnType<typeof vi.fn>;
}

function makeFakeIframe(): {
  iframe: HTMLIFrameElement;
  contentWindow: FakeContentWindow;
} {
  const contentWindow: FakeContentWindow = { postMessage: vi.fn() };
  const iframe = {
    contentWindow: contentWindow as unknown as Window,
  } as unknown as HTMLIFrameElement;
  return { iframe, contentWindow };
}

function fireReady(contentWindow: unknown): void {
  window.dispatchEvent(
    new MessageEvent('message', {
      data: { proto: BRIDGE_PROTOCOL, type: 'ready' },
      source: contentWindow as Window,
    })
  );
}

// ── Tests ─────────────────────────────────────────────────────────────────

describe('autotableBridge / send', () => {
  let consoleErrorSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    consoleErrorSpy = vi.spyOn(console, 'error').mockImplementation(() => undefined);
  });

  afterEach(() => {
    consoleErrorSpy.mockRestore();
  });

  it('queues outbound messages until the iframe posts ready, then flushes them', () => {
    const { iframe, contentWindow } = makeFakeIframe();
    const handle = attachAutotableBridge(iframe);

    handle.send({ type: 'hello', gameId: 'g-1' });
    handle.send({ type: 'phase', phase: 'rollingDice' });

    // Nothing posted yet — bridge waits for ready
    expect(contentWindow.postMessage).not.toHaveBeenCalled();
    expect(handle.isReady).toBe(false);

    fireReady(iframe.contentWindow);

    expect(handle.isReady).toBe(true);
    expect(contentWindow.postMessage).toHaveBeenCalledTimes(2);
    expect(contentWindow.postMessage).toHaveBeenNthCalledWith(
      1,
      { proto: BRIDGE_PROTOCOL, type: 'hello', gameId: 'g-1' },
      '*'
    );
    expect(contentWindow.postMessage).toHaveBeenNthCalledWith(
      2,
      { proto: BRIDGE_PROTOCOL, type: 'phase', phase: 'rollingDice' },
      '*'
    );

    handle.dispose();
  });

  it('posts every message with the proto sentinel after ready', () => {
    const { iframe, contentWindow } = makeFakeIframe();
    const handle = attachAutotableBridge(iframe);
    fireReady(iframe.contentWindow);

    handle.send({ type: 'dice', die1: 3, die2: 4, sum: 7 });
    handle.send({ type: 'tileDiscarded', seatIndex: 0, tileId: 17 });
    handle.send({ type: 'reset' });

    expect(contentWindow.postMessage).toHaveBeenCalledTimes(3);
    for (const call of contentWindow.postMessage.mock.calls) {
      const [msg, targetOrigin] = call as [Record<string, unknown>, string];
      expect(msg.proto).toBe(BRIDGE_PROTOCOL);
      expect(typeof msg.type).toBe('string');
      expect(targetOrigin).toBe('*');
    }

    handle.dispose();
  });
});

describe('autotableBridge / receive', () => {
  it('invokes onInbound only when source === iframe.contentWindow', () => {
    const { iframe } = makeFakeIframe();
    const onInbound = vi.fn();
    const handle = attachAutotableBridge(iframe, onInbound);
    fireReady(iframe.contentWindow);

    // Foreign source — must be ignored
    const foreign = { postMessage: vi.fn() } as unknown as Window;
    window.dispatchEvent(
      new MessageEvent('message', {
        data: { proto: BRIDGE_PROTOCOL, type: 'tileClick', tileId: 9, seatIndex: 0 },
        source: foreign,
      })
    );
    expect(onInbound).not.toHaveBeenCalled();

    // Matching source — delivered
    const tileClick: BridgeInboundMessage = {
      proto: BRIDGE_PROTOCOL,
      type: 'tileClick',
      tileId: 9,
      seatIndex: 0,
    };
    window.dispatchEvent(
      new MessageEvent('message', {
        data: tileClick,
        source: iframe.contentWindow as Window,
      })
    );
    expect(onInbound).toHaveBeenCalledTimes(1);
    expect(onInbound).toHaveBeenCalledWith(tileClick);

    handle.dispose();
  });

  it('drops messages with missing / wrong proto sentinel', () => {
    const { iframe } = makeFakeIframe();
    const onInbound = vi.fn();
    const handle = attachAutotableBridge(iframe, onInbound);

    // Wrong proto
    window.dispatchEvent(
      new MessageEvent('message', {
        data: { proto: 'someone-else/2', type: 'ready' },
        source: iframe.contentWindow as Window,
      })
    );

    // No proto at all
    window.dispatchEvent(
      new MessageEvent('message', {
        data: { type: 'ready' },
        source: iframe.contentWindow as Window,
      })
    );

    // Garbage payload
    window.dispatchEvent(
      new MessageEvent('message', {
        data: 'not-an-object',
        source: iframe.contentWindow as Window,
      })
    );

    expect(handle.isReady).toBe(false);
    expect(onInbound).not.toHaveBeenCalled();

    handle.dispose();
  });

  it('dispose() detaches the window message listener', () => {
    const { iframe } = makeFakeIframe();
    const onInbound = vi.fn();
    const handle = attachAutotableBridge(iframe, onInbound);

    handle.dispose();

    // After dispose, even a well-formed message must not reach the callback
    window.dispatchEvent(
      new MessageEvent('message', {
        data: { proto: BRIDGE_PROTOCOL, type: 'tileClick', tileId: 1, seatIndex: 0 },
        source: iframe.contentWindow as Window,
      })
    );
    expect(onInbound).not.toHaveBeenCalled();
  });
});
