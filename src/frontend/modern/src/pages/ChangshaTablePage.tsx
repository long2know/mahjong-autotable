import { useCallback, useEffect, useRef } from 'react';
import {
  Button,
  Card,
  Spinner,
  Text,
  Toast,
  ToastTitle,
  ToastBody,
  Toaster,
  useToastController,
  useId,
} from '@fluentui/react-components';
import {
  useChangshaGame,
  setUseMockOverride,
  shouldUseMock,
} from '../changsha/useChangshaGame';
import {
  ChangshaHud,
  DiceRollModal,
  PlayerHandPanel,
  ClaimPromptModal,
  FanBreakdownPanel,
  LobbyCard,
  OpponentDiscardTrays,
} from '../changsha/components';
import { attachAutotableBridge, diffAndSend, type BridgeHandle } from '../changsha/autotableBridge';
import type { ChangshaGameState, SeatIndex } from '../changsha/types';

const USER_SEAT: SeatIndex = 0;

function ConnectionBanner({
  status,
  isLive,
  lastError,
  onReconnect,
}: {
  status: string;
  isLive: boolean;
  lastError?: { message: string };
  onReconnect: () => void;
}) {
  if (!isLive) {
    return (
      <div
        style={{
          padding: '6px 12px',
          background: '#fef3c7',
          color: '#92400e',
          borderRadius: 6,
          marginBottom: 8,
        }}
      >
        🛠 Mock state — offline UI sandbox. Use the toggle to switch to live.
      </div>
    );
  }
  if (status === 'connecting') {
    return (
      <div
        style={{
          padding: '6px 12px',
          background: '#e0f2fe',
          color: '#075985',
          borderRadius: 6,
          marginBottom: 8,
          display: 'flex',
          alignItems: 'center',
          gap: 8,
        }}
      >
        <Spinner size="extra-tiny" />
        <span>Connecting to Changsha hub…</span>
      </div>
    );
  }
  if (status === 'reconnecting') {
    return (
      <div
        style={{
          padding: '6px 12px',
          background: '#fef3c7',
          color: '#92400e',
          borderRadius: 6,
          marginBottom: 8,
        }}
      >
        🔄 Connection interrupted — reconnecting…
      </div>
    );
  }
  if (status === 'disconnected' || status === 'failed') {
    return (
      <div
        style={{
          padding: '6px 12px',
          background: '#fee2e2',
          color: '#991b1b',
          borderRadius: 6,
          marginBottom: 8,
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 8,
        }}
      >
        <span>⚠ Disconnected{lastError ? ` — ${lastError.message}` : ''}</span>
        <Button size="small" appearance="primary" onClick={onReconnect}>
          Reconnect
        </Button>
      </div>
    );
  }
  return null;
}

function ModeToggle({ isLive }: { isLive: boolean }) {
  const flip = () => {
    setUseMockOverride(isLive ? true : false);
    window.location.reload();
  };
  return (
    <Button size="small" appearance="subtle" onClick={flip}>
      Mode: <strong style={{ marginLeft: 4 }}>{isLive ? 'Live server' : 'Mock state'}</strong>
      &nbsp;(click to switch)
    </Button>
  );
}

function AutotableViewport({ state }: { state: ChangshaGameState }) {
  const iframeRef = useRef<HTMLIFrameElement | null>(null);
  const bridgeRef = useRef<BridgeHandle | null>(null);
  const prevStateRef = useRef<ChangshaGameState | undefined>(undefined);

  useEffect(() => {
    const iframe = iframeRef.current;
    if (!iframe) return;
    const bridge = attachAutotableBridge(iframe, (msg) => {
      // Phase 3: inbound canvas events would be wired to discard / claim here.
      // eslint-disable-next-line no-console
      console.debug('[changsha bridge] inbound', msg);
    });
    bridgeRef.current = bridge;
    return () => {
      bridge.dispose();
      bridgeRef.current = null;
    };
  }, []);

  useEffect(() => {
    const bridge = bridgeRef.current;
    if (!bridge) return;
    diffAndSend(bridge, prevStateRef.current, state);
    prevStateRef.current = state;
  }, [state]);

  return (
    <div
      style={{
        margin: '12px 0',
        position: 'relative',
        borderRadius: 12,
        overflow: 'hidden',
        border: '1px solid #b8c4d1',
        background: '#0b1d2a',
      }}
    >
      <iframe
        ref={iframeRef}
        id="autotable-frame"
        title="Autotable 3D viewport"
        src="/autotable/"
        style={{
          width: '100%',
          height: 480,
          border: 'none',
          display: 'block',
        }}
      />
    </div>
  );
}

export function ChangshaTablePage() {
  const game = useChangshaGame({ userSeat: USER_SEAT });
  const { state, actions, isLive, connectionStatus, lastError, reconnect } = game;
  const toasterId = useId('changsha-toaster');
  const { dispatchToast } = useToastController(toasterId);
  const lastSeenError = useRef<string | undefined>(undefined);

  useEffect(() => {
    if (!lastError) return;
    if (lastSeenError.current === lastError.message) return;
    lastSeenError.current = lastError.message;
    dispatchToast(
      <Toast>
        <ToastTitle>Server error</ToastTitle>
        <ToastBody>{lastError.message}</ToastBody>
      </Toast>,
      { intent: 'error', timeout: 5000 }
    );
  }, [lastError, dispatchToast]);

  const isDevModeToggleVisible = import.meta.env.DEV || shouldUseMock();
  const showLoadingShell = isLive && connectionStatus === 'connecting' && !state.gameId;

  // Lobby is shown when phase is lobby OR there is no active gameId.
  const showLobby = state.phase === 'lobby' || !state.gameId;
  const canStartFromLobby = !isLive || connectionStatus === 'connected';

  const handlePlayVsBots = useCallback(
    async (playerName: string, seat: SeatIndex) => {
      if (!actions.createGame || !actions.fillWithBots || !actions.takeSeat || !actions.startGame) {
        throw new Error('Lobby commands are not available in this mode.');
      }
      const gameId = await actions.createGame({ botSeatIndexes: [1, 2, 3] });
      if (!gameId) throw new Error('Server did not return a game id.');
      await actions.takeSeat(seat, playerName);
      await actions.fillWithBots();
      await actions.startGame();
    },
    [actions]
  );

  return (
    <div style={{ maxWidth: 1080, margin: '0 auto', padding: 16 }}>
      <Toaster toasterId={toasterId} />

      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          marginBottom: 8,
        }}
      >
        <Text size={500} weight="semibold">
          🀄 Changsha Mahjong
        </Text>
        <div style={{ display: 'flex', gap: 8, alignItems: 'center' }}>
          {!showLobby && actions.leaveGame && (
            <Button size="small" appearance="outline" onClick={actions.leaveGame}>
              Leave game
            </Button>
          )}
          <ModeToggle isLive={isLive} />
        </div>
      </div>

      <ConnectionBanner
        status={connectionStatus}
        isLive={isLive}
        lastError={lastError}
        onReconnect={reconnect}
      />

      {showLoadingShell ? (
        <Card
          style={{
            padding: 32,
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            gap: 12,
          }}
        >
          <Spinner size="large" />
          <Text>Establishing connection to Changsha hub…</Text>
        </Card>
      ) : showLobby ? (
        <LobbyCard
          onPlayVsBots={handlePlayVsBots}
          canStart={canStartFromLobby}
          connectionHint={isLive ? connectionStatus : undefined}
        />
      ) : (
        <>
          <ChangshaHud state={state} />

          <AutotableViewport state={state} />

          <OpponentDiscardTrays state={state} userSeat={USER_SEAT} />

          <Card style={{ marginBottom: 16 }}>
            <PlayerHandPanel
              state={state}
              userSeat={USER_SEAT}
              onDiscard={actions.discard}
              onDeclareKong={actions.declareKong}
              onDeclareWin={actions.declareWin}
            />
          </Card>

          <Text size={200} block style={{ textAlign: 'center', marginBottom: 8 }}>
            Phase: <strong>{state.phase}</strong> · Wall: {state.wallRemaining} · Discards:{' '}
            {state.discardPile.length}
            {isLive && (
              <>
                {' · '}
                <em style={{ color: '#475569' }}>connection: {connectionStatus}</em>
              </>
            )}
          </Text>

          <DiceRollModal
            state={state}
            userSeat={USER_SEAT}
            onRoll={actions.rollDice}
            onConfirm={() => {
              actions.confirmDice();
              if (!isLive) {
                setTimeout(() => actions.dealMock(), 400);
              }
            }}
          />
          <ClaimPromptModal
            state={state}
            userSeat={USER_SEAT}
            onClaim={actions.resolveClaim}
          />
          <FanBreakdownPanel state={state} onContinue={actions.continueAfterScoring} />
        </>
      )}

      {isDevModeToggleVisible && !isLive && (
        <Card
          style={{
            position: 'fixed',
            bottom: 16,
            right: 16,
            padding: 12,
            zIndex: 1000,
            maxWidth: 260,
            boxShadow: '0 2px 12px rgba(0,0,0,0.2)',
          }}
        >
          <Text weight="semibold" block style={{ marginBottom: 8 }}>
            🛠 Demo Controls
          </Text>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 6 }}>
            <Button size="small" onClick={actions.rollDice}>
              Simulate Dice Roll
            </Button>
            <Button size="small" onClick={actions.dealMock}>
              Simulate Deal
            </Button>
            <Button size="small" onClick={actions.simulateClaimWindow}>
              Simulate Claim Window
            </Button>
            <Button size="small" onClick={actions.simulateWin}>
              Simulate Win
            </Button>
            <Button size="small" appearance="outline" onClick={actions.resetDemo}>
              Reset Demo
            </Button>
          </div>
        </Card>
      )}
    </div>
  );
}
