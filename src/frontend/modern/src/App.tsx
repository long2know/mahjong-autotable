import { useCallback, useEffect, useMemo, useState } from 'react';
import { Body1, Button, Card, CardHeader, Text } from '@fluentui/react-components';

type TableSeatType = 'Human' | 'Bot';
type TableTurnPhase = 'AwaitingDiscard' | 'WallExhausted';

interface TableSeatState {
  seatIndex: number;
  seatType: TableSeatType;
  playerId: string;
}

interface TableSeatHandState {
  seatIndex: number;
  tiles: number[];
}

interface TableAction {
  sequence: number;
  actionType: string;
  seatIndex: number;
  turnNumber: number;
  tileId: number | null;
  detail: string;
  stateHash: string;
  occurredUtc: string;
}

interface TableDiscard {
  seatIndex: number;
  tileId: number;
  turnNumber: number;
  occurredUtc: string;
}

interface TableStateMetadata {
  seed: number;
  algorithmId: string;
}

interface TableIntegrityState {
  stateHash: string;
}

interface TableGameState {
  stateVersion: number;
  actionSequence: number;
  activeSeat: number;
  turnNumber: number;
  drawNumber: number;
  phase: TableTurnPhase;
  metadata: TableStateMetadata;
  integrity: TableIntegrityState;
  wall: number[];
  seats: TableSeatState[];
  hands: TableSeatHandState[];
  discardPile: TableDiscard[];
  actionLog: TableAction[];
}

interface TableDto {
  id: string;
  ruleSet: string;
  stateVersion: number;
  createdUtc: string;
  updatedUtc: string;
  lastActionUtc: string | null;
  state: TableGameState;
}

interface AdvanceBotsResponse {
  table: TableDto;
  actions: TableAction[];
  stopReason: string;
}

interface DiscardActionResponse {
  table: TableDto;
  discardAction: TableAction;
  drawAction: TableAction | null;
}

interface ReplayVerificationResponse {
  table: TableDto;
  integrityMatch: boolean;
  expectedStateHash: string;
  replayedStateHash: string;
  replayedStateVersion: number;
  replayedActionSequence: number;
}

interface TableEventDto {
  sequence: number;
  actionType: string;
  seatIndex: number;
  turnNumber: number;
  tileId: number | null;
  detail: string;
  stateVersion: number;
  stateHash: string;
  occurredUtc: string;
  persistedUtc: string;
}

interface TableEventsResponse {
  tableId: string;
  stateVersion: number;
  actionSequence: number;
  events: TableEventDto[];
}

interface ErrorPayload {
  code?: string;
  error?: string;
  message?: string;
}

const eventWindowSize = 16;

async function readJson<T>(url: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers);
  headers.set('accept', 'application/json');
  if (init?.body && !headers.has('content-type')) {
    headers.set('content-type', 'application/json');
  }

  const response = await fetch(url, { ...init, headers });
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`;
    try {
      const payload = (await response.json()) as ErrorPayload;
      message = payload.message ?? payload.error ?? payload.code ?? message;
    } catch {
      // Keep fallback message.
    }

    throw new Error(message);
  }

  return (await response.json()) as T;
}

export function App() {
  const [healthStatus, setHealthStatus] = useState<'checking' | 'ok' | 'down'>('checking');
  const [table, setTable] = useState<TableDto | null>(null);
  const [events, setEvents] = useState<TableEventDto[]>([]);
  const [selectedTile, setSelectedTile] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [statusMessage, setStatusMessage] = useState('Create a table to start a local round.');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const seatMap = useMemo(
    () => new Map((table?.state.seats ?? []).map((seat) => [seat.seatIndex, seat])),
    [table]
  );
  const handMap = useMemo(
    () => new Map((table?.state.hands ?? []).map((hand) => [hand.seatIndex, hand])),
    [table]
  );

  const sortedSeats = useMemo(
    () => [...(table?.state.seats ?? [])].sort((left, right) => left.seatIndex - right.seatIndex),
    [table]
  );

  const activeSeat = table?.state.activeSeat ?? null;
  const activeSeatInfo = activeSeat === null ? null : seatMap.get(activeSeat) ?? null;
  const activeHand = activeSeat === null ? null : handMap.get(activeSeat) ?? null;
  const sortedActiveTiles = useMemo(
    () => [...(activeHand?.tiles ?? [])].sort((left, right) => left - right),
    [activeHand]
  );

  const canDiscard =
    table !== null &&
    activeSeatInfo !== null &&
    activeSeatInfo.seatType === 'Human' &&
    table.state.phase === 'AwaitingDiscard' &&
    sortedActiveTiles.length > 0;

  const checkHealth = useCallback(async () => {
    try {
      await readJson<{ status: string }>('/api/health');
      setHealthStatus('ok');
    } catch {
      setHealthStatus('down');
    }
  }, []);

  const loadEvents = useCallback(async (tableId: string) => {
    const payload = await readJson<TableEventsResponse>(
      `/api/tables/${tableId}/events?limit=${eventWindowSize}`
    );
    setEvents(payload.events);
  }, []);

  const runOperation = useCallback(
    async (operation: () => Promise<void>) => {
      if (busy) {
        return;
      }

      setBusy(true);
      setErrorMessage(null);
      try {
        await operation();
      } catch (error) {
        const message = error instanceof Error ? error.message : 'Unknown operation error.';
        setErrorMessage(message);
      } finally {
        setBusy(false);
      }
    },
    [busy]
  );

  const createTable = useCallback(async () => {
    await runOperation(async () => {
      const next = await readJson<TableDto>('/api/tables', {
        method: 'POST',
        body: JSON.stringify({ ruleSet: 'changsha' })
      });

      setTable(next);
      await loadEvents(next.id);
      setStatusMessage(`Table ${next.id.slice(0, 8)} created with seed ${next.state.metadata.seed}.`);
    });
  }, [loadEvents, runOperation]);

  const refreshTable = useCallback(async () => {
    if (table === null) {
      return;
    }

    await runOperation(async () => {
      const next = await readJson<TableDto>(`/api/tables/${table.id}`);
      setTable(next);
      await loadEvents(next.id);
      setStatusMessage('State refreshed from backend.');
    });
  }, [loadEvents, runOperation, table]);

  const advanceBots = useCallback(async () => {
    if (table === null) {
      return;
    }

    await runOperation(async () => {
      const payload = await readJson<AdvanceBotsResponse>(`/api/tables/${table.id}/bots/advance`, {
        method: 'POST',
        body: JSON.stringify({ maxActions: 8 })
      });

      setTable(payload.table);
      await loadEvents(payload.table.id);
      setStatusMessage(
        `Advanced bots: ${payload.actions.length} event(s), stop reason = ${payload.stopReason}.`
      );
    });
  }, [loadEvents, runOperation, table]);

  const discardSelectedTile = useCallback(async () => {
    if (table === null || selectedTile === null || !canDiscard) {
      return;
    }

    await runOperation(async () => {
      const payload = await readJson<DiscardActionResponse>(`/api/tables/${table.id}/actions/discard`, {
        method: 'POST',
        body: JSON.stringify({
          seatIndex: table.state.activeSeat,
          tileId: selectedTile,
          expectedStateVersion: table.stateVersion
        })
      });

      setTable(payload.table);
      await loadEvents(payload.table.id);
      setStatusMessage(`Discarded tile ${selectedTile}.`);
    });
  }, [canDiscard, loadEvents, runOperation, selectedTile, table]);

  const verifyReplayStrict = useCallback(async () => {
    if (table === null) {
      return;
    }

    await runOperation(async () => {
      const payload = await readJson<ReplayVerificationResponse>(
        `/api/tables/${table.id}/replay/verify?strict=true`,
        {
          method: 'POST',
          body: JSON.stringify({})
        }
      );

      setTable(payload.table);
      await loadEvents(payload.table.id);
      setStatusMessage(`Replay integrity verified: ${payload.integrityMatch ? 'match' : 'mismatch'}.`);
    });
  }, [loadEvents, runOperation, table]);

  useEffect(() => {
    void checkHealth();
  }, [checkHealth]);

  useEffect(() => {
    if (table === null) {
      setSelectedTile(null);
      return;
    }

    const hand = table.state.hands.find((candidate) => candidate.seatIndex === table.state.activeSeat);
    if (!hand || hand.tiles.length === 0) {
      setSelectedTile(null);
      return;
    }

    if (selectedTile === null || !hand.tiles.includes(selectedTile)) {
      const nextTile = [...hand.tiles].sort((left, right) => left - right)[0] ?? null;
      setSelectedTile(nextTile);
    }
  }, [selectedTile, table]);

  return (
    <main className="app-shell">
      <Card className="panel">
        <CardHeader
          header={<Text weight="semibold">Mahjong Autotable (Modern Control Panel)</Text>}
        />
        <Body1>
          This panel is wired to the .NET backend so you can drive turns, inspect state, and verify
          replay integrity while the full tabletop UI is still in progress.
        </Body1>

        <div className="actions-row">
          <Button appearance="primary" onClick={createTable} disabled={busy}>
            Create table
          </Button>
          <Button onClick={refreshTable} disabled={busy || table === null}>
            Refresh
          </Button>
          <Button onClick={advanceBots} disabled={busy || table === null}>
            Advance bots
          </Button>
          <Button onClick={verifyReplayStrict} disabled={busy || table === null}>
            Verify replay (strict)
          </Button>
        </div>

        <p className={`status-line ${healthStatus}`}>
          Backend status:{' '}
          {healthStatus === 'checking'
            ? 'checking...'
            : healthStatus === 'ok'
              ? 'connected'
              : 'not reachable'}
        </p>
        <p className="status-line">{statusMessage}</p>
        {errorMessage && <p className="error-line">{errorMessage}</p>}
      </Card>

      {table && (
        <>
          <Card className="panel">
            <CardHeader header={<Text weight="semibold">Table {table.id.slice(0, 8)}</Text>} />
            <div className="metrics-grid">
              <div>
                <span>Rule</span>
                <strong>{table.ruleSet}</strong>
              </div>
              <div>
                <span>Phase</span>
                <strong>{table.state.phase}</strong>
              </div>
              <div>
                <span>Active seat</span>
                <strong>{table.state.activeSeat}</strong>
              </div>
              <div>
                <span>Wall remaining</span>
                <strong>{table.state.wall.length}</strong>
              </div>
              <div>
                <span>State version</span>
                <strong>{table.stateVersion}</strong>
              </div>
              <div>
                <span>Action sequence</span>
                <strong>{table.state.actionSequence}</strong>
              </div>
            </div>
          </Card>

          <Card className="panel">
            <CardHeader header={<Text weight="semibold">Seats and hands</Text>} />
            <div className="seats-grid">
              {sortedSeats.map((seat) => {
                const hand = handMap.get(seat.seatIndex);
                const handCount = hand?.tiles.length ?? 0;
                const discardPreview =
                  table.state.discardPile
                    .filter((discard) => discard.seatIndex === seat.seatIndex)
                    .slice(-4)
                    .map((discard) => discard.tileId)
                    .join(', ') || '—';

                return (
                  <section
                    key={seat.seatIndex}
                    className={`seat-card ${seat.seatIndex === table.state.activeSeat ? 'active' : ''}`}
                  >
                    <h3>
                      Seat {seat.seatIndex} · {seat.seatType}
                    </h3>
                    <p>Player: {seat.playerId}</p>
                    <p>Hand count: {handCount}</p>
                    <p>Recent discards: {discardPreview}</p>
                  </section>
                );
              })}
            </div>
          </Card>

          {canDiscard && (
            <Card className="panel">
              <CardHeader header={<Text weight="semibold">Your turn (Seat {activeSeat})</Text>} />
              <Body1>Select a tile and submit discard.</Body1>
              <div className="tile-picker">
                {sortedActiveTiles.map((tileId, index) => (
                  <button
                    key={`${tileId}-${index}-${table.state.actionSequence}`}
                    type="button"
                    className={`tile-pill ${selectedTile === tileId ? 'selected' : ''}`}
                    onClick={() => setSelectedTile(tileId)}
                  >
                    {tileId}
                  </button>
                ))}
              </div>
              <div className="actions-row">
                  <Button
                    appearance="primary"
                    onClick={discardSelectedTile}
                    disabled={busy || selectedTile === null}
                  >
                    Discard selected tile
                  </Button>
              </div>
            </Card>
          )}

          <Card className="panel">
            <CardHeader header={<Text weight="semibold">Recent persisted events</Text>} />
            {events.length === 0 ? (
              <Body1>No persisted events yet.</Body1>
            ) : (
              <ol className="events-list">
                {events.map((event) => (
                  <li key={event.sequence}>
                    #{event.sequence} {event.actionType.toUpperCase()} · seat {event.seatIndex}
                    {event.tileId === null ? '' : ` · tile ${event.tileId}`} · v{event.stateVersion}
                    {' · '}
                    {event.stateHash.slice(0, 12)}
                  </li>
                ))}
              </ol>
            )}
          </Card>
        </>
      )}
    </main>
  );
}
