import { useEffect, useMemo, useState } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  Text,
  Radio,
  RadioGroup,
} from '@fluentui/react-components';
import type { ChangshaGameState, ClaimType, SeatIndex } from '../types';
import { computeChowCombos, tileFromId, tileLabel, type ChowCombo } from '../tileUtils';
import { TileFace } from './TileFace';

interface ClaimPromptModalProps {
  state: ChangshaGameState;
  userSeat: SeatIndex;
  /**
   * Submit a claim. For chow, the second argument carries the EXACT two
   * concealed tile ids forming the chow with the discard. The discard
   * itself is NOT included — backend already knows it. Pass null to pass.
   */
  onClaim: (claimType: string | null, tileIds?: number[]) => void;
}

const CLAIM_LABELS: Record<ClaimType, string> = {
  pung: '碰 Pung',
  kong: '杠 Kong',
  chow: '吃 Chow',
  hu: '🏆 Win!',
};

/**
 * Stable ordering for claim buttons: hu first (highest value), then kong,
 * pung, chow. Pass is always rendered last.
 */
const CLAIM_PRIORITY: Record<ClaimType, number> = {
  hu: 0,
  kong: 1,
  pung: 2,
  chow: 3,
};

export function ClaimPromptModal({ state, userSeat, onClaim }: ClaimPromptModalProps) {
  const userClaims = useMemo(
    () => state.pendingClaims?.filter((c) => c.seatIndex === userSeat) ?? [],
    [state.pendingClaims, userSeat]
  );
  const open = state.phase === 'awaitingClaim' && userClaims.length > 0;

  const discardTile = useMemo(() => {
    if (state.discardPile.length === 0) return undefined;
    return state.discardPile[state.discardPile.length - 1];
  }, [state.discardPile]);

  const hand = useMemo(
    () => state.hands.find((h) => h.seatIndex === userSeat),
    [state.hands, userSeat]
  );

  // Compute chow combos client-side (server doesn't send them per-pair in
  // ClaimWindowOpen). Empty if no chow claim is offered or discard missing.
  const chowOffered = userClaims.some((c) => c.type === 'chow');
  const chowCombos: ChowCombo[] = useMemo(() => {
    if (!chowOffered || !discardTile || !hand) return [];
    return computeChowCombos(hand.concealed, discardTile);
  }, [chowOffered, discardTile, hand]);

  const sortedClaims = useMemo(
    () =>
      [...userClaims].sort(
        (a, b) => (CLAIM_PRIORITY[a.type] ?? 99) - (CLAIM_PRIORITY[b.type] ?? 99)
      ),
    [userClaims]
  );

  const [countdown, setCountdown] = useState(5);
  const [chowSelection, setChowSelection] = useState<number>(0);

  // Reset selections each time the modal opens.
  useEffect(() => {
    if (open) {
      setCountdown(5);
      setChowSelection(0);
    }
  }, [open, discardTile?.id]);

  // Countdown timer (auto-pass).
  useEffect(() => {
    if (!open) return;
    const interval = setInterval(() => {
      setCountdown((c) => {
        if (c <= 1) {
          onClaim(null);
          return 5;
        }
        return c - 1;
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [open, onClaim]);

  if (!open || !discardTile) return null;

  const handleClaim = (type: ClaimType) => {
    if (type === 'chow') {
      const combo = chowCombos[chowSelection];
      if (!combo || combo.tileIds.length !== 2) {
        // Validation guard — never send an empty chow.
        return;
      }
      onClaim('chow', [combo.tileIds[0], combo.tileIds[1]]);
      return;
    }
    onClaim(type);
  };

  return (
    <Dialog open={open} modalType="alert">
      <DialogSurface>
        <DialogTitle>Claim Opportunity</DialogTitle>
        <DialogBody>
          <DialogContent>
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: 12,
                marginBottom: 12,
              }}
            >
              <Text>Discarded tile:</Text>
              <TileFace tile={discardTile} size="md" highlighted />
              <Text size={200}>{tileLabel(discardTile)}</Text>
            </div>

            {chowOffered && chowCombos.length > 0 && (
              <div
                style={{
                  marginTop: 12,
                  padding: 8,
                  background: '#f1f5f9',
                  borderRadius: 6,
                }}
              >
                <Text size={200} weight="semibold" block style={{ marginBottom: 6 }}>
                  Choose chow combo:
                </Text>
                <RadioGroup
                  value={String(chowSelection)}
                  onChange={(_e, data) => setChowSelection(Number(data.value))}
                  layout="vertical"
                >
                  {chowCombos.map((c, i) => (
                    <Radio
                      key={`${c.tileIds[0]}-${c.tileIds[1]}`}
                      value={String(i)}
                      label={
                        <div style={{ display: 'inline-flex', gap: 4, alignItems: 'center' }}>
                          <TileFace tile={c.tiles[0]} size="sm" />
                          <TileFace tile={c.tiles[1]} size="sm" />
                          <TileFace tile={discardTile} size="sm" highlighted />
                          <Text size={200}>
                            ({tileLabel(c.tiles[0])} {tileLabel(c.tiles[1])} +{' '}
                            {tileLabel(discardTile)})
                          </Text>
                        </div>
                      }
                    />
                  ))}
                </RadioGroup>
              </div>
            )}

            <Text size={200} block style={{ marginTop: 12 }}>
              ⏱ Auto-pass in {countdown}s
            </Text>
          </DialogContent>
        </DialogBody>
        <DialogActions>
          {sortedClaims.map((claim) => {
            const disabled = claim.type === 'chow' && chowCombos.length === 0;
            return (
              <Button
                key={claim.type}
                appearance="primary"
                onClick={() => handleClaim(claim.type)}
                disabled={disabled}
                style={
                  claim.type === 'hu' ? { backgroundColor: '#dc2626' } : undefined
                }
              >
                {CLAIM_LABELS[claim.type] ?? claim.type}
              </Button>
            );
          })}
          <Button appearance="outline" onClick={() => onClaim(null)}>
            过 Pass
          </Button>
        </DialogActions>
      </DialogSurface>
    </Dialog>
  );
}

// Re-export so consumers can import directly from this module if needed.
export { tileFromId };
