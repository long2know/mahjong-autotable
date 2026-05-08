import { useState, useEffect, useCallback } from 'react';
import {
  Dialog,
  DialogSurface,
  DialogTitle,
  DialogBody,
  DialogActions,
  DialogContent,
  Button,
  Text,
} from '@fluentui/react-components';
import type { ChangshaGameState, SeatIndex } from '../types';

interface ClaimPromptModalProps {
  state: ChangshaGameState;
  userSeat: SeatIndex;
  onClaim: (claimType: string | null) => void;
}

const CLAIM_LABELS: Record<string, string> = {
  pung: '碰 Pung',
  kong: '杠 Kong',
  chow: '吃 Chow',
  win: '胡 Win',
};

export function ClaimPromptModal({ state, userSeat, onClaim }: ClaimPromptModalProps) {
  const userClaims =
    state.pendingClaims?.filter((c) => c.seatIndex === userSeat) ?? [];
  const open = state.phase === 'claim-window' && userClaims.length > 0;

  const [countdown, setCountdown] = useState(5);

  useEffect(() => {
    if (!open) {
      setCountdown(5);
      return;
    }
    const interval = setInterval(() => {
      setCountdown((c) => {
        if (c <= 1) {
          onClaim(null); // auto-pass on timeout
          return 5;
        }
        return c - 1;
      });
    }, 1000);
    return () => clearInterval(interval);
  }, [open, onClaim]);

  if (!open) return null;

  return (
    <Dialog open={open} modalType="alert">
      <DialogSurface>
        <DialogTitle>Claim Opportunity</DialogTitle>
        <DialogBody>
          <DialogContent>
            <Text block style={{ marginBottom: 8 }}>
              A tile was discarded. You may claim it:
            </Text>
            <Text size={200} block>
              ⏱ Auto-pass in {countdown}s
            </Text>
          </DialogContent>
        </DialogBody>
        <DialogActions>
          {userClaims.map((claim) => (
            <Button
              key={claim.type}
              appearance="primary"
              onClick={() => onClaim(claim.type)}
            >
              {CLAIM_LABELS[claim.type] ?? claim.type}
            </Button>
          ))}
          <Button appearance="outline" onClick={() => onClaim(null)}>
            过 Pass
          </Button>
        </DialogActions>
      </DialogSurface>
    </Dialog>
  );
}
