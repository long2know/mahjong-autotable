/**
 * autotableBridge — postMessage bridge between the Changsha React app
 * (parent) and the bundled autotable iframe (child).
 *
 * Phase 2 scope: ONE-WAY (parent → child). Changsha state changes are
 * translated into bridge messages and posted into the iframe. The
 * receiver (src/frontend/autotable/changsha-bridge-receiver.js) maps
 * those into rudimentary scene mutations.
 *
 * Phase 3 will add bidirectional flow (canvas tile clicks → discard /
 * claim commands). The receive direction below is stubbed.
 *
 * See docs/rules/changsha-autotable-bridge.md for the protocol spec.
 */
import type { ChangshaGameState } from './types';

export const BRIDGE_PROTOCOL = 'changsha-bridge/1';

export type BridgeOutboundMessage =
  | { proto: typeof BRIDGE_PROTOCOL; type: 'hello'; gameId: string }
  | { proto: typeof BRIDGE_PROTOCOL; type: 'phase'; phase: ChangshaGameState['phase'] }
  | { proto: typeof BRIDGE_PROTOCOL; type: 'dice'; die1: number; die2: number; sum: number }
  | {
      proto: typeof BRIDGE_PROTOCOL;
      type: 'breakPoint';
      wallIndex: number;
      stackIndex: number;
      tileIndex: number;
    }
  | {
      proto: typeof BRIDGE_PROTOCOL;
      type: 'tilesDealt';
      seatIndex: number;
      tileIds: number[];
      tileCount: number;
      isComplete: boolean;
    }
  | {
      proto: typeof BRIDGE_PROTOCOL;
      type: 'tileDiscarded';
      seatIndex: number;
      tileId: number;
    }
  | {
      proto: typeof BRIDGE_PROTOCOL;
      type: 'claimMade';
      seatIndex: number;
      tileIds: number[];
      meldType: string;
    }
  | { proto: typeof BRIDGE_PROTOCOL; type: 'reset' };

export type BridgeInboundMessage =
  | { proto: typeof BRIDGE_PROTOCOL; type: 'ready' }
  | {
      proto: typeof BRIDGE_PROTOCOL;
      type: 'tileClick';
      tileId: number;
      seatIndex: number;
    }
  | {
      proto: typeof BRIDGE_PROTOCOL;
      type: 'tileDrop';
      tileId: number;
      target: 'discard' | 'meld' | 'wall';
    };

type DistributiveOmit<T, K extends PropertyKey> = T extends unknown ? Omit<T, K> : never;

export interface BridgeHandle {
  send: (msg: DistributiveOmit<BridgeOutboundMessage, 'proto'>) => void;
  dispose: () => void;
  readonly isReady: boolean;
}

export function attachAutotableBridge(
  iframe: HTMLIFrameElement,
  onInbound?: (msg: BridgeInboundMessage) => void
): BridgeHandle {
  let ready = false;
  const queue: BridgeOutboundMessage[] = [];

  const post = (msg: BridgeOutboundMessage) => {
    const target = iframe.contentWindow;
    if (!target) return;
    target.postMessage(msg, '*');
  };

  const flush = () => {
    while (queue.length) {
      const m = queue.shift();
      if (m) post(m);
    }
  };

  const onMessage = (ev: MessageEvent) => {
    if (ev.source !== iframe.contentWindow) return;
    const data = ev.data as Partial<BridgeInboundMessage> | undefined;
    if (!data || data.proto !== BRIDGE_PROTOCOL) return;
    if (data.type === 'ready') {
      ready = true;
      flush();
      return;
    }
    // TODO(phase3): map tileClick/tileDrop to Discard / Claim hub commands.
    if (onInbound) onInbound(data as BridgeInboundMessage);
  };

  window.addEventListener('message', onMessage);

  const send: BridgeHandle['send'] = (partial) => {
    const msg = { proto: BRIDGE_PROTOCOL, ...partial } as BridgeOutboundMessage;
    if (!ready) {
      queue.push(msg);
      return;
    }
    post(msg);
  };

  return {
    send,
    dispose: () => {
      window.removeEventListener('message', onMessage);
    },
    get isReady() {
      return ready;
    },
  };
}

/**
 * Diff two ChangshaGameState snapshots and emit bridge messages for the
 * mutations the receiver cares about.
 */
export function diffAndSend(
  bridge: BridgeHandle,
  prev: ChangshaGameState | undefined,
  next: ChangshaGameState
): void {
  if (!prev || prev.gameId !== next.gameId) {
    bridge.send({ type: 'reset' });
    bridge.send({ type: 'hello', gameId: next.gameId });
  }
  if (!prev || prev.phase !== next.phase) {
    bridge.send({ type: 'phase', phase: next.phase });
  }
  if (next.lastDice && (!prev || prev.lastDice !== next.lastDice)) {
    bridge.send({
      type: 'dice',
      die1: next.lastDice.die1,
      die2: next.lastDice.die2,
      sum: next.lastDice.sum,
    });
  }
  if (next.breakPoint && (!prev || prev.breakPoint !== next.breakPoint)) {
    bridge.send({
      type: 'breakPoint',
      wallIndex: next.breakPoint.wallIndex,
      stackIndex: next.breakPoint.stackIndex,
      tileIndex: next.breakPoint.tileIndex,
    });
  }
  const prevDiscards = prev?.discardPile.length ?? 0;
  if (next.discardPile.length > prevDiscards) {
    for (let i = prevDiscards; i < next.discardPile.length; i++) {
      const t = next.discardPile[i];
      bridge.send({
        type: 'tileDiscarded',
        seatIndex: next.activeSeat ?? 0,
        tileId: t.id,
      });
    }
  }
  if (next.phase === 'dealing' || (prev?.phase === 'dealing' && next.phase === 'awaitingDiscard')) {
    for (const hand of next.hands) {
      const prevHand = prev?.hands.find((h) => h.seatIndex === hand.seatIndex);
      const prevCount = prevHand?.concealed.length ?? 0;
      if (hand.concealed.length > prevCount) {
        bridge.send({
          type: 'tilesDealt',
          seatIndex: hand.seatIndex,
          tileIds: hand.concealed.slice(prevCount).map((t) => t.id),
          tileCount: hand.concealed.length,
          isComplete: next.phase === 'awaitingDiscard',
        });
      }
    }
  }
}
