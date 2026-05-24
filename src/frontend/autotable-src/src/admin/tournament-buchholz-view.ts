// Phase K Wave 23 — Hicks (Frontend).
//
// Operator UI for Bishop's W23 Buchholz-score viewer endpoint:
//
//   GET /api/admin/tournaments/buchholz?tournamentId=<id>&round=<n>
//
// READ-ONLY surface — exposes the Buchholz tie-breaker scoring
// table for an in-progress or completed Swiss tournament so
// directors can audit how the tie-breaker resolved final
// standings.  Buchholz score is `Σ opponents' final scores`; the
// "Median Buchholz" variant drops the highest + lowest opponent
// scores before summing.  Both numbers are surfaced so the
// director can spot anomalies before they trigger a manual
// rerun of the standings job.
//
// Wire contract:
//   • Auth ladder: 401/403/503 → 200 OK with the standings table.
//   • Query params: `tournamentId` (required), `round` (optional —
//     defaults to current/latest).
//   • No X-Admin-Reason required (read-only).

import {
  type AdminSurfaceSpec,
  escapeHtml,
  fmtIso,
} from './admin-shared';

interface TournamentBuchholzRow {
  /** Composite key:  `${tournamentId}|${round}|${playerId}`. */
  rowId: string;
  tournamentId: string;
  round: number;
  playerId: string;
  playerName: string;
  rank: number;
  matchPoints: number;
  buchholz: number;
  medianBuchholz: number;
  cumulativeOpponents: number;
  recordedAt: string;
}

function parseRow(raw: unknown): TournamentBuchholzRow | null {
  if (raw === null || typeof raw !== 'object') return null;
  const o = raw as Record<string, unknown>;
  const tournamentId = typeof o.tournamentId === 'string' ? o.tournamentId : null;
  const playerId = typeof o.playerId === 'string' ? o.playerId : null;
  const round = typeof o.round === 'number' ? o.round : null;
  if (tournamentId === null || playerId === null || round === null) return null;
  return {
    rowId: `${tournamentId}|${round}|${playerId}`,
    tournamentId,
    round,
    playerId,
    playerName: typeof o.playerName === 'string' ? o.playerName : playerId,
    rank: typeof o.rank === 'number' ? o.rank : 0,
    matchPoints: typeof o.matchPoints === 'number' ? o.matchPoints : 0,
    buchholz: typeof o.buchholz === 'number' ? o.buchholz : 0,
    medianBuchholz: typeof o.medianBuchholz === 'number' ? o.medianBuchholz : 0,
    cumulativeOpponents: typeof o.cumulativeOpponents === 'number'
      ? o.cumulativeOpponents : 0,
    recordedAt: typeof o.recordedAt === 'string' ? o.recordedAt : '',
  };
}

function fmtScore(n: number): string {
  return n.toFixed(1);
}

export const TOURNAMENT_BUCHHOLZ_VIEW_SPEC:
  AdminSurfaceSpec<TournamentBuchholzRow, Record<string, never>> = {
  id: 'tournament-buchholz-view',
  title: 'Tournaments · Buchholz standings',
  description: 'READ-ONLY view of the Buchholz tie-breaker table '
    + 'for a Swiss tournament.  Buchholz = Σ opponents'
    + ' final match-points; Median Buchholz drops highest + '
    + 'lowest before summing.  Use to audit how a tie-breaker '
    + 'resolved final standings before triggering a manual '
    + 'standings rerun.  Audit kind: '
    + 'tournaments.buchholz.viewed (informational).',
  endpoint: '/api/admin/tournaments/buchholz',
  parseRow,
  rowKey: (r) => r.rowId,
  rowToFormValues: (r) => ({
    tournamentId: r.tournamentId,
    round: String(r.round),
  }),
  buildBody: (v) => {
    const tournamentId = (v.tournamentId ?? '').trim();
    if (tournamentId === '') {
      throw new Error('tournamentId is required');
    }
    return {} as Record<string, never>;
  },
  fields: [
    {
      name: 'tournamentId',
      label: 'Tournament ID',
      type: 'text',
      required: true,
      primaryKey: true,
      placeholder: 'tourn-2026-spring-swiss-01',
      help: 'Required.  The tournament whose Buchholz table to view.',
    },
    {
      name: 'round',
      label: 'Round number',
      type: 'number',
      required: false,
      integer: true,
      min: 1,
      max: 99,
      placeholder: '(latest if blank)',
      help: 'Empty → latest completed round.  Specify a round '
        + 'number to view the historical table at the end of '
        + 'that round.',
    },
  ],
  columns: [
    {
      key: 'rank',
      label: 'Rank',
      render: (r) => ({
        __html: `<strong>${escapeHtml(String(r.rank))}</strong>`,
      }),
    },
    {
      key: 'playerName',
      label: 'Player',
      render: (r) => ({
        __html: `${escapeHtml(r.playerName)} `
          + `<code class="admin-panel-muted">${escapeHtml(r.playerId)}</code>`,
      }),
    },
    {
      key: 'matchPoints',
      label: 'MP',
      render: (r) => fmtScore(r.matchPoints),
    },
    {
      key: 'buchholz',
      label: 'Buchholz',
      render: (r) => fmtScore(r.buchholz),
    },
    {
      key: 'medianBuchholz',
      label: 'Median Buchholz',
      render: (r) => fmtScore(r.medianBuchholz),
    },
    {
      key: 'cumulativeOpponents',
      label: 'Cum. opp.',
      render: (r) => fmtScore(r.cumulativeOpponents),
    },
    {
      key: 'round',
      label: 'Round',
      render: (r) => String(r.round),
    },
    {
      key: 'recordedAt',
      label: 'Recorded',
      render: (r) => fmtIso(r.recordedAt),
    },
  ],
};
