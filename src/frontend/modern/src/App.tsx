import { useCallback, useEffect, useMemo, useState } from 'react';
import { Body1, Button, Card, CardHeader, Text } from '@fluentui/react-components';

type TableSeatType = 'Human' | 'Bot';
type TableTurnPhase = 'AwaitingDiscard' | 'AwaitingClaimResolution' | 'WallExhausted';
type ClaimResolutionDecision = 'pass' | 'take-selected';

type TileSuitTone = 'dots' | 'bamboo' | 'characters' | 'wind' | 'dragon' | 'unknown';

interface TableSeatState {
  seatIndex: number;
  seatType: TableSeatType;
  playerId: string;
}

interface TableSeatHandState {
  seatIndex: number;
  tiles: number[];
}

interface TableSeatViewHandState {
  seatIndex: number;
  tileCount: number;
  tiles: number[] | null;
}

interface TableMeldState {
  claimType: string;
  tileIds: number[];
  claimedFromSeatIndex: number;
  sourceTurnNumber: number;
  sourceActionSequence: number;
}

interface TableSeatMeldState {
  seatIndex: number;
  melds: TableMeldState[];
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

interface TableLastActionState {
  sequence: number;
  seatIndex: number;
  actionType: string;
  tileId: number | null;
  detail: string;
}

interface TableClaimOpportunity {
  seatIndex: number;
  claimType: string;
  priority: number;
}

interface TableClaimWindowState {
  sourceActionSequence: number;
  discardSeatIndex: number;
  discardTileId: number;
  discardTurnNumber: number;
  precedencePolicy: string;
  opportunities: TableClaimOpportunity[];
  selectedOpportunity: TableClaimOpportunity | null;
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
  lastAction: TableLastActionState | null;
  wall: number[];
  seats: TableSeatState[];
  hands: TableSeatHandState[];
  exposedMelds: TableSeatMeldState[];
  discardPile: TableDiscard[];
  claimWindow: TableClaimWindowState | null;
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

interface TableSeatViewState {
  stateVersion: number;
  actionSequence: number;
  activeSeat: number;
  turnNumber: number;
  drawNumber: number;
  phase: TableTurnPhase;
  metadata: TableStateMetadata;
  integrity: TableIntegrityState;
  wallCount: number;
  seats: TableSeatState[];
  hands: TableSeatViewHandState[];
  exposedMelds: TableSeatMeldState[];
  discardPile: TableDiscard[];
  lastAction: TableLastActionState | null;
  claimWindow: TableClaimWindowState | null;
}

interface TableSeatViewDto {
  id: string;
  ruleSet: string;
  viewerSeatIndex: number;
  stateVersion: number;
  createdUtc: string;
  updatedUtc: string;
  lastActionUtc: string | null;
  state: TableSeatViewState;
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

type ClaimResolutionApiResponse = TableDto | { table: TableDto };

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

interface TileFace {
  label: string;
  tone: TileSuitTone;
}

const eventWindowSize = 16;
const humanSeatIndex = 0;

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

function describeTile(tileId: number): TileFace {
  const logical = Math.floor(tileId / 4);

  if (logical < 9) {
    return { label: `${logical + 1}●`, tone: 'dots' };
  }

  if (logical < 18) {
    return { label: `${logical - 8}♣`, tone: 'bamboo' };
  }

  if (logical < 27) {
    return { label: `${logical - 17}萬`, tone: 'characters' };
  }

  if (logical < 31) {
    const winds = ['E', 'S', 'W', 'N'];
    return { label: winds[logical - 27] ?? 'Wind', tone: 'wind' };
  }

  if (logical < 34) {
    const dragons = ['Red', 'Green', 'White'];
    return { label: dragons[logical - 31] ?? 'Dragon', tone: 'dragon' };
  }

  return { label: `#${tileId}`, tone: 'unknown' };
}

function formatStopReason(reason: string): string {
  switch (reason) {
    case 'HumanTurn':
      return 'human turn reached';
    case 'WallExhausted':
      return 'wall exhausted';
    case 'MaxActionsReached':
      return 'continuing bot sequence';
    default:
      return reason;
  }
}

function formatClaimType(claimType: string): string {
  return claimType.toUpperCase();
}

function getResolvedTable(payload: ClaimResolutionApiResponse): TableDto {
  return 'table' in payload ? payload.table : payload;
}

function seatArea(seatIndex: number): 'south' | 'west' | 'north' | 'east' {
  if (seatIndex === 0) {
    return 'south';
  }

  if (seatIndex === 1) {
    return 'west';
  }

  if (seatIndex === 2) {
    return 'north';
  }

  return 'east';
}

export function App() {
  const [healthStatus, setHealthStatus] = useState<'checking' | 'ok' | 'down'>('checking');
  const [table, setTable] = useState<TableDto | null>(null);
  const [tableView, setTableView] = useState<TableSeatViewDto | null>(null);
  const [viewerSeatIndex, setViewerSeatIndex] = useState(humanSeatIndex);
  const [events, setEvents] = useState<TableEventDto[]>([]);
  const [selectedTile, setSelectedTile] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);
  const [statusMessage, setStatusMessage] = useState('Create a table to begin playing.');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const seatMap = useMemo(
    () => new Map((tableView?.state.seats ?? []).map((seat) => [seat.seatIndex, seat])),
    [tableView]
  );

  const handMap = useMemo(
    () => new Map((tableView?.state.hands ?? []).map((hand) => [hand.seatIndex, hand])),
    [tableView]
  );

  const meldMap = useMemo(
    () => new Map((tableView?.state.exposedMelds ?? []).map((seatMeld) => [seatMeld.seatIndex, seatMeld.melds])),
    [tableView]
  );

  const sortedSeats = useMemo(
    () => [...(tableView?.state.seats ?? [])].sort((left, right) => left.seatIndex - right.seatIndex),
    [tableView]
  );

  const humanSeat = seatMap.get(humanSeatIndex) ?? null;
  const humanHandTiles = useMemo(
    () => [...(handMap.get(humanSeatIndex)?.tiles ?? [])].sort((left, right) => left - right),
    [handMap]
  );

  const viewerHandTiles = useMemo(
    () => [...(handMap.get(viewerSeatIndex)?.tiles ?? [])].sort((left, right) => left - right),
    [handMap, viewerSeatIndex]
  );

  const canDiscard =
    table !== null &&
    tableView !== null &&
    humanSeat !== null &&
    viewerSeatIndex === humanSeatIndex &&
    tableView.state.activeSeat === humanSeatIndex &&
    tableView.state.phase === 'AwaitingDiscard' &&
    humanHandTiles.length > 0;

  const centerDiscards = useMemo(() => tableView?.state.discardPile.slice(-24) ?? [], [tableView]);
  const claimWindow = tableView?.state.claimWindow ?? null;
  const canResolveClaim =
    table !== null &&
    claimWindow !== null &&
    claimWindow.opportunities.length > 0 &&
    viewerSeatIndex === humanSeatIndex;

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

  const loadSeatView = useCallback(async (tableId: string, seatIndex: number) => {
    const payload = await readJson<TableSeatViewDto>(
      `/api/tables/${tableId}/view?seatIndex=${seatIndex}`
    );
    setTableView(payload);
  }, []);

  const advanceBotsToHumanTurn = useCallback(async (currentTable: TableDto) => {
    const stillAwaitingDiscard = currentTable.state.phase === 'AwaitingDiscard';
    const isHumanTurn = currentTable.state.activeSeat === humanSeatIndex;
    const wallHasTiles = currentTable.state.wall.length > 0;
    if (!stillAwaitingDiscard || isHumanTurn || !wallHasTiles) {
      const stopReason = !wallHasTiles ? 'WallExhausted' : isHumanTurn ? 'HumanTurn' : currentTable.state.phase;
      return { table: currentTable, totalActions: 0, stopReason };
    }

    const payload = await readJson<AdvanceBotsResponse>(`/api/tables/${currentTable.id}/bots/advance`, {
      method: 'POST',
      body: JSON.stringify({ advanceUntilHumanTurnOrWallExhausted: true })
    });

    return {
      table: payload.table,
      totalActions: payload.actions.length,
      stopReason: payload.stopReason
    };
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
      const createdTable = await readJson<TableDto>('/api/tables', {
        method: 'POST',
        body: JSON.stringify({ ruleSet: 'changsha' })
      });

      const progression = await advanceBotsToHumanTurn(createdTable);
      setTable(progression.table);
      await loadEvents(progression.table.id);
      await loadSeatView(progression.table.id, viewerSeatIndex);

      const botSummary =
        progression.totalActions > 0
          ? ` Bots played ${progression.totalActions} action(s) (${formatStopReason(progression.stopReason)}).`
          : '';
      setStatusMessage(
        `Table ${progression.table.id.slice(0, 8)} ready. ${
          progression.table.state.activeSeat === humanSeatIndex ? 'Your turn.' : 'Waiting on backend state.'
        }${botSummary}`
      );
    });
  }, [advanceBotsToHumanTurn, loadEvents, loadSeatView, runOperation, viewerSeatIndex]);

  const refreshTable = useCallback(async () => {
    if (table === null) {
      return;
    }

    await runOperation(async () => {
      const next = await readJson<TableDto>(`/api/tables/${table.id}`);
      setTable(next);
      await loadEvents(next.id);
      await loadSeatView(next.id, viewerSeatIndex);
      setStatusMessage('Table refreshed from backend.');
    });
  }, [loadEvents, loadSeatView, runOperation, table, viewerSeatIndex]);

  const advanceBotsManual = useCallback(async () => {
    if (table === null) {
      return;
    }

    await runOperation(async () => {
      const progression = await advanceBotsToHumanTurn(table);
      setTable(progression.table);
      await loadEvents(progression.table.id);
      await loadSeatView(progression.table.id, viewerSeatIndex);
      setStatusMessage(
        `Bot progression complete: ${progression.totalActions} action(s), ${formatStopReason(progression.stopReason)}.`
      );
    });
  }, [advanceBotsToHumanTurn, loadEvents, loadSeatView, runOperation, table, viewerSeatIndex]);

  const discardSelectedTile = useCallback(async () => {
    if (table === null || selectedTile === null || !canDiscard) {
      return;
    }

    await runOperation(async () => {
      const discardPayload = await readJson<DiscardActionResponse>(`/api/tables/${table.id}/actions/discard`, {
        method: 'POST',
        body: JSON.stringify({
          seatIndex: humanSeatIndex,
          tileId: selectedTile,
          expectedStateVersion: table.stateVersion
        })
      });

      const progression = await advanceBotsToHumanTurn(discardPayload.table);
      setTable(progression.table);
      await loadEvents(progression.table.id);
      await loadSeatView(progression.table.id, viewerSeatIndex);

      setStatusMessage(
        `You discarded ${describeTile(selectedTile).label}. Bots played ${progression.totalActions} action(s) and stopped at ${formatStopReason(progression.stopReason)}.`
      );
    });
  }, [advanceBotsToHumanTurn, canDiscard, loadEvents, loadSeatView, runOperation, selectedTile, table, viewerSeatIndex]);

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
      await loadSeatView(payload.table.id, viewerSeatIndex);
      setStatusMessage(`Replay integrity verified: ${payload.integrityMatch ? 'match' : 'mismatch'}.`);
    });
  }, [loadEvents, loadSeatView, runOperation, table, viewerSeatIndex]);

  const resolveClaimWindow = useCallback(
    async (decision: ClaimResolutionDecision) => {
      if (!canResolveClaim || table === null) {
        return;
      }

      await runOperation(async () => {
        const payload = await readJson<ClaimResolutionApiResponse>(`/api/tables/${table.id}/claims/resolve`, {
          method: 'POST',
          body: JSON.stringify({
            decision,
            expectedStateVersion: table.stateVersion
          })
        });

        const resolvedTable = getResolvedTable(payload);
        const progression = await advanceBotsToHumanTurn(resolvedTable);
        setTable(progression.table);
        await loadEvents(progression.table.id);
        await loadSeatView(progression.table.id, viewerSeatIndex);

        const decisionLabel = decision === 'take-selected' ? 'took selected claim' : 'passed claim window';
        const botSummary =
          progression.totalActions > 0
            ? ` Bots played ${progression.totalActions} action(s) (${formatStopReason(progression.stopReason)}).`
            : '';
        setStatusMessage(`You ${decisionLabel}.${botSummary}`);
      });
    },
    [advanceBotsToHumanTurn, canResolveClaim, loadEvents, loadSeatView, runOperation, table, viewerSeatIndex]
  );

  const changePerspective = useCallback(
    async (nextSeatIndex: number) => {
      setViewerSeatIndex(nextSeatIndex);
      setSelectedTile(null);
      if (table === null) {
        return;
      }

      await runOperation(async () => {
        await loadSeatView(table.id, nextSeatIndex);
        setStatusMessage(
          nextSeatIndex === humanSeatIndex
            ? 'Viewing seat 0 perspective. Gameplay controls enabled.'
            : `Viewing seat ${nextSeatIndex} perspective (read-only).`
        );
      });
    },
    [loadSeatView, runOperation, table]
  );

  useEffect(() => {
    void checkHealth();
  }, [checkHealth]);

  useEffect(() => {
    if (tableView === null) {
      setSelectedTile(null);
      return;
    }

    const availableTiles = handMap.get(humanSeatIndex)?.tiles ?? [];
    if (availableTiles.length === 0) {
      setSelectedTile(null);
      return;
    }

    if (selectedTile === null || !availableTiles.includes(selectedTile)) {
      setSelectedTile([...availableTiles].sort((left, right) => left - right)[0] ?? null);
    }
  }, [handMap, selectedTile, tableView, viewerSeatIndex]);

  return (
    <main className="app-shell">
      <Card className="panel">
        <CardHeader header={<Text weight="semibold">Mahjong Autotable · Playable Modern Table</Text>} />
        <Body1>
          Start a table, click a tile to discard, resolve claim windows when prompted, and bots auto-play until your
          next turn.
        </Body1>

        <div className="actions-row">
          <Button appearance="primary" onClick={createTable} disabled={busy}>
            New table
          </Button>
          <Button onClick={refreshTable} disabled={busy || table === null}>
            Refresh
          </Button>
        </div>
        <div className="perspective-row">
          <label htmlFor="seatPerspective">Perspective seat</label>
          <select
            id="seatPerspective"
            value={viewerSeatIndex}
            onChange={(event) => {
              void changePerspective(Number(event.target.value));
            }}
            disabled={busy}
          >
            {[0, 1, 2, 3].map((seatIndex) => (
              <option key={seatIndex} value={seatIndex}>
                Seat {seatIndex}
              </option>
            ))}
          </select>
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

      {table && tableView && (
        <>
          <Card className="panel">
            <CardHeader header={<Text weight="semibold">Table {table.id.slice(0, 8)}</Text>} />
            <div className="table-layout">
              {sortedSeats.map((seat) => {
                const projectedHand = handMap.get(seat.seatIndex);
                const handCount = projectedHand?.tileCount ?? 0;
                const isViewer = seat.seatIndex === viewerSeatIndex;
                const isHuman = seat.seatType === 'Human' || seat.seatIndex === humanSeatIndex;
                const isActive = seat.seatIndex === tableView.state.activeSeat;
                const seatDiscards = tableView.state.discardPile
                  .filter((discard) => discard.seatIndex === seat.seatIndex)
                  .slice(-5);
                const seatMelds = meldMap.get(seat.seatIndex) ?? [];

                return (
                  <section
                    key={seat.seatIndex}
                    className={`seat-zone area-${seatArea(seat.seatIndex)} ${isActive ? 'active' : ''} ${isViewer ? 'viewer' : ''}`}
                  >
                    <header>
                      <strong>
                        Seat {seat.seatIndex} · {isViewer ? 'Viewer' : isHuman ? 'You' : 'Bot'}
                      </strong>
                      <span>{isActive ? 'Active turn' : 'Waiting'}</span>
                    </header>
                    <p>
                      {projectedHand?.tiles
                        ? `Visible tiles: ${handCount}`
                        : `Concealed tiles: ${handCount}`}
                    </p>
                    <div className="seat-melds">
                      {seatMelds.length === 0 ? (
                        <small>No exposed melds</small>
                      ) : (
                        seatMelds.map((meld, index) => (
                          <div
                            key={`${seat.seatIndex}-${meld.sourceActionSequence}-${meld.claimType}-${index}`}
                            className="seat-meld"
                          >
                            <div className="seat-meld-header">
                              <strong>{formatClaimType(meld.claimType)}</strong>
                              <small>from S{meld.claimedFromSeatIndex}</small>
                            </div>
                            <div className="seat-meld-tiles">
                              {meld.tileIds.map((tileId) => {
                                const face = describeTile(tileId);
                                return (
                                  <span key={`${meld.sourceActionSequence}-${tileId}`} className={`mini-tile ${face.tone}`}>
                                    {face.label}
                                  </span>
                                );
                              })}
                            </div>
                          </div>
                        ))
                      )}
                    </div>
                    <div className="mini-discard-row">
                      {seatDiscards.length === 0 ? (
                        <small>No discards</small>
                      ) : (
                        seatDiscards.map((discard, index) => {
                          const face = describeTile(discard.tileId);
                          return (
                            <span key={`${discard.tileId}-${index}`} className={`mini-tile ${face.tone}`}>
                              {face.label}
                            </span>
                          );
                        })
                      )}
                    </div>
                  </section>
                );
              })}

              <section className="table-center">
                <h3>Center Discards</h3>
                <div className="metrics-grid compact">
                  <div>
                    <span>Phase</span>
                    <strong>{tableView.state.phase}</strong>
                  </div>
                  <div>
                    <span>Turn</span>
                    <strong>{tableView.state.turnNumber}</strong>
                  </div>
                  <div>
                    <span>Active seat</span>
                    <strong>{tableView.state.activeSeat}</strong>
                  </div>
                  <div>
                    <span>Wall</span>
                    <strong>{tableView.state.wallCount}</strong>
                  </div>
                </div>
                <div className="center-discards-grid">
                  {centerDiscards.length === 0 ? (
                    <p className="center-empty">No discards yet.</p>
                  ) : (
                    centerDiscards.map((discard, index) => {
                      const face = describeTile(discard.tileId);
                      return (
                        <div key={`${discard.tileId}-${discard.turnNumber}-${index}`} className="center-discard">
                          <span className={`tile-face ${face.tone}`}>{face.label}</span>
                          <small>S{discard.seatIndex}</small>
                        </div>
                      );
                    })
                  )}
                </div>
              </section>
            </div>
          </Card>

          <Card className="panel">
            <CardHeader header={<Text weight="semibold">Seat {viewerSeatIndex} hand</Text>} />
            <Body1>
              {viewerSeatIndex !== humanSeatIndex
                ? `Read-only perspective for seat ${viewerSeatIndex}. Switch to seat 0 to discard.`
                : canDiscard
                ? 'Select a tile and discard to continue the round.'
                : tableView.state.phase === 'WallExhausted'
                  ? 'Wall exhausted. Start a new table to play again.'
                  : `Waiting for seat ${tableView.state.activeSeat}.`}
            </Body1>
            <div className="human-hand-row">
              {viewerHandTiles.map((tileId) => {
                const face = describeTile(tileId);
                const isSelected = selectedTile === tileId;
                return (
                  <button
                    key={`${tileId}-${tableView.state.actionSequence}`}
                    type="button"
                    className={`tile-face hand-tile ${face.tone} ${isSelected ? 'selected' : ''}`}
                    onClick={() => setSelectedTile(tileId)}
                    disabled={!canDiscard || busy || viewerSeatIndex !== humanSeatIndex}
                  >
                    {face.label}
                  </button>
                );
              })}
            </div>
            <div className="actions-row">
              <Button appearance="primary" onClick={discardSelectedTile} disabled={busy || !canDiscard || selectedTile === null}>
                Discard selected tile
              </Button>
            </div>
          </Card>

          <Card className="panel">
            <CardHeader header={<Text weight="semibold">Claim resolution</Text>} />
            {claimWindow === null || claimWindow.opportunities.length === 0 ? (
              <Body1>No open claim window.</Body1>
            ) : (
              <>
                <Body1>
                  Discard by seat {claimWindow.discardSeatIndex}: {describeTile(claimWindow.discardTileId).label} (turn{' '}
                  {claimWindow.discardTurnNumber}).
                </Body1>
                <p className="claim-policy">
                  Policy: <strong>{claimWindow.precedencePolicy}</strong>
                </p>
                <div className="claim-opportunities">
                  {claimWindow.opportunities.map((opportunity, index) => {
                    const isSelected =
                      claimWindow.selectedOpportunity !== null &&
                      claimWindow.selectedOpportunity.seatIndex === opportunity.seatIndex &&
                      claimWindow.selectedOpportunity.claimType === opportunity.claimType &&
                      claimWindow.selectedOpportunity.priority === opportunity.priority;
                    return (
                      <div
                        key={`${opportunity.seatIndex}-${opportunity.claimType}-${index}`}
                        className={`claim-opportunity ${isSelected ? 'selected' : ''}`}
                      >
                        <span>
                          Seat {opportunity.seatIndex} · {formatClaimType(opportunity.claimType)}
                        </span>
                        <small>Priority {opportunity.priority}</small>
                      </div>
                    );
                  })}
                </div>
                <Body1>
                  Selected winner:{' '}
                  {claimWindow.selectedOpportunity === null
                    ? 'none'
                    : `Seat ${claimWindow.selectedOpportunity.seatIndex} · ${formatClaimType(claimWindow.selectedOpportunity.claimType)}`}
                  .
                </Body1>
              </>
            )}
            <Body1>
              {viewerSeatIndex !== humanSeatIndex
                ? `Read-only perspective for seat ${viewerSeatIndex}. Switch to seat 0 to resolve claims.`
                : claimWindow === null || claimWindow.opportunities.length === 0
                  ? 'No action needed.'
                  : 'Choose pass or take-selected to resolve the current claim window.'}
            </Body1>
            <div className="actions-row">
              <Button
                onClick={() => {
                  void resolveClaimWindow('pass');
                }}
                disabled={busy || !canResolveClaim}
              >
                Pass
              </Button>
              <Button
                appearance="primary"
                onClick={() => {
                  void resolveClaimWindow('take-selected');
                }}
                disabled={busy || !canResolveClaim || claimWindow?.selectedOpportunity === null}
              >
                Take selected
              </Button>
            </div>
          </Card>

          <Card className="panel">
            <CardHeader header={<Text weight="semibold">Advanced tools</Text>} />
            <div className="actions-row">
              <Button onClick={advanceBotsManual} disabled={busy || table === null}>
                Advance bots to next human turn
              </Button>
              <Button onClick={verifyReplayStrict} disabled={busy || table === null}>
                Verify replay (strict)
              </Button>
            </div>
            <details className="advanced-events">
              <summary>Recent persisted events</summary>
              {events.length === 0 ? (
                <Body1>No persisted events yet.</Body1>
              ) : (
                <ol className="events-list">
                  {events.map((event) => (
                    <li key={event.sequence}>
                      #{event.sequence} {event.actionType.toUpperCase()} · seat {event.seatIndex}
                      {event.tileId === null ? '' : ` · tile ${describeTile(event.tileId).label}`} · v
                      {event.stateVersion} · {event.stateHash.slice(0, 12)}
                    </li>
                  ))}
                </ol>
              )}
            </details>
          </Card>
        </>
      )}
    </main>
  );
}
