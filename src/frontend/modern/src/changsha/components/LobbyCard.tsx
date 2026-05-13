import { useCallback, useState } from 'react';
import {
  Button,
  Card,
  CardHeader,
  Input,
  Label,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Text,
} from '@fluentui/react-components';
import type { SeatIndex } from '../types';

const PLAYER_NAME_KEY = 'mj-autotable:changsha:playerName';
const DEFAULT_NAME = 'Stephen';

interface LobbyCardProps {
  /**
   * Start a new game vs three bots. Implementation must perform the full
   * createGame → fillWithBots → takeSeat → startGame sequence and report
   * any error via the returned promise rejection.
   */
  onPlayVsBots: (playerName: string, userSeat: SeatIndex) => Promise<void>;
  /** Whether the live SignalR connection is ready to issue lobby commands. */
  canStart: boolean;
  /** Optional status hint (e.g., "connecting", "disconnected"). */
  connectionHint?: string;
}

function readPersistedName(): string {
  try {
    const v = localStorage.getItem(PLAYER_NAME_KEY);
    if (v && v.trim().length > 0) return v;
  } catch {
    /* ignore */
  }
  return DEFAULT_NAME;
}

function persistName(name: string): void {
  try {
    localStorage.setItem(PLAYER_NAME_KEY, name);
  } catch {
    /* ignore */
  }
}

export function LobbyCard({ onPlayVsBots, canStart, connectionHint }: LobbyCardProps) {
  const [playerName, setPlayerName] = useState<string>(readPersistedName);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handlePlay = useCallback(async () => {
    if (busy) return;
    const trimmed = playerName.trim();
    if (!trimmed) {
      setError('Enter a display name first.');
      return;
    }
    setError(null);
    setBusy(true);
    persistName(trimmed);
    try {
      await onPlayVsBots(trimmed, 0);
    } catch (err) {
      const msg = err instanceof Error ? err.message : String(err);
      setError(msg);
    } finally {
      setBusy(false);
    }
  }, [busy, onPlayVsBots, playerName]);

  const disabled = busy || !canStart;

  return (
    <Card
      style={{
        padding: 24,
        marginBottom: 16,
        display: 'flex',
        flexDirection: 'column',
        gap: 16,
      }}
    >
      <CardHeader
        header={
          <Text size={500} weight="semibold">
            🀄 Start a Changsha hand
          </Text>
        }
        description={
          <Text size={200}>
            Sit at seat 0 (East). Three bots fill the remaining seats. Server deals,
            rolls dice, and runs the turn loop.
          </Text>
        }
      />

      {error && (
        <MessageBar intent="error">
          <MessageBarBody>
            <MessageBarTitle>Could not start game</MessageBarTitle>
            {error}
          </MessageBarBody>
        </MessageBar>
      )}

      {!canStart && (
        <MessageBar intent="info">
          <MessageBarBody>
            <MessageBarTitle>Waiting for hub</MessageBarTitle>
            {connectionHint
              ? `Connection status: ${connectionHint}.`
              : 'Connecting to the Changsha hub…'}
          </MessageBarBody>
        </MessageBar>
      )}

      <div style={{ display: 'flex', flexDirection: 'column', gap: 6, maxWidth: 280 }}>
        <Label htmlFor="changsha-lobby-name" size="small">
          Your display name
        </Label>
        <Input
          id="changsha-lobby-name"
          value={playerName}
          onChange={(_e, data) => setPlayerName(data.value)}
          placeholder={DEFAULT_NAME}
          disabled={busy}
        />
      </div>

      <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
        <Button
          appearance="primary"
          size="large"
          disabled={disabled}
          onClick={handlePlay}
          icon={busy ? <Spinner size="tiny" /> : undefined}
        >
          {busy ? 'Starting…' : 'Play vs Bots'}
        </Button>
        <Text size={200} style={{ color: '#475569' }}>
          (Game is server-authoritative — dice and deal happen automatically.)
        </Text>
      </div>
    </Card>
  );
}
