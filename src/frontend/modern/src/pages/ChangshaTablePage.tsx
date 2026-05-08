import { Button, Card, Text } from '@fluentui/react-components';
import { useChangshaGame } from '../changsha/useChangshaGame';
import {
  ChangshaHud,
  DiceRollModal,
  PlayerHandPanel,
  ClaimPromptModal,
  FanBreakdownPanel,
} from '../changsha/components';
import type { SeatIndex } from '../changsha/types';

const USER_SEAT: SeatIndex = 0;

export function ChangshaTablePage() {
  const { state, actions } = useChangshaGame();

  const isDev = import.meta.env.DEV;

  return (
    <div style={{ maxWidth: 1000, margin: '0 auto', padding: 16 }}>
      {/* Top: HUD */}
      <ChangshaHud state={state} />

      {/* Center: Autotable placeholder (Phase 2 will embed iframe/canvas here) */}
      <div
        style={{
          margin: '16px 0',
          minHeight: 320,
          border: '2px dashed #bbb',
          borderRadius: 12,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          backgroundColor: '#f0f4f8',
        }}
      >
        {/* Phase 2: embed autotable iframe or Three.js canvas here */}
        <Text size={400} style={{ color: '#888' }}>
          🀄 Autotable 3D viewport placeholder — Phase 2
        </Text>
      </div>

      {/* Bottom: Player hand */}
      <Card style={{ marginBottom: 16 }}>
        <PlayerHandPanel state={state} userSeat={USER_SEAT} onDiscard={actions.discard} />
      </Card>

      {/* Phase indicator */}
      <Text size={200} block style={{ textAlign: 'center', marginBottom: 8 }}>
        Phase: <strong>{state.phase}</strong> · Wall: {state.wallRemaining} · Discards:{' '}
        {state.discardPile.length}
      </Text>

      {/* Modals */}
      <DiceRollModal
        state={state}
        userSeat={USER_SEAT}
        onRoll={actions.rollDice}
        onConfirm={() => {
          actions.confirmDice();
          // Auto-deal after confirming dice
          setTimeout(() => actions.dealMock(), 400);
        }}
      />
      <ClaimPromptModal state={state} userSeat={USER_SEAT} onClaim={actions.resolveClaim} />
      <FanBreakdownPanel state={state} onContinue={actions.continueAfterScoring} />

      {/* Dev-only demo controls */}
      {isDev && (
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
