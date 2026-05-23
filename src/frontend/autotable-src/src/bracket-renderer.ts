// Phase K Wave 6 — Bracket renderer strategy.
//
// The Wave 1 / Wave 2 / Wave 4 tournaments panel rendered ONLY
// single-elimination brackets (as an interactive SVG) and parked the
// "round-robin / Swiss" rendering on a flat textual list (see
// `buildMatchesList` in `tournaments.ts`).  Wave 6 lifts the renderer
// into a strategy interface so a single host element can dispatch to
// the format-appropriate view:
//
//   • SingleElimRenderer — delegates back to `tournaments.ts`'s
//     existing `buildBracketSvg` so the visual stays identical to
//     Waves 4-5.  This module owns the dispatch wrapper only.
//   • SwissRenderer       — round-by-round W/L/D table.
//   • DoubleElimRenderer  — winners + losers brackets side-by-side
//     plus a highlighted grand-final card.
//
// Format detection is a substring match on `tournament.format`:
//
//   • contains 'double'                              → double-elim
//   • contains 'swiss'                               → swiss
//   • contains 'round-robin' / 'round_robin' / 'rr'  → round-robin (Swiss view)
//   • otherwise (single / bracket / elim / unknown)  → single-elim
//
// Testids exported (consumed by Vasquez):
//   • bracket-format-{format}       — wrapper, one of single-elim |
//     swiss | double-elim | round-robin
//   • bracket-round-{n}             — per-round group (Swiss + losers
//     bracket; n is 1-based round number)
//   • tournament-bracket-svg        — preserved on the single-elim SVG
//   • tournament-grand-final        — double-elim grand-final card

import type { BracketMatch, BracketSlot, StandingsRow } from './tournaments';

export type BracketFormatKey = 'single-elim' | 'swiss' | 'double-elim' | 'round-robin';

export interface BracketRendererInput {
  format: string;
  matches: ReadonlyArray<BracketMatch>;
  standings: ReadonlyArray<StandingsRow>;
  /** Optional registered-players list for sparse Swiss rounds. */
  players?: ReadonlyArray<BracketSlot>;
  tournamentId: string;
  /** Renderer for the single-elim SVG; injected by `tournaments.ts`
   *  so this module stays unaware of the SVG-layout constants. */
  singleElimSvg: (input: BracketRendererInput) => SVGSVGElement;
}

export interface BracketRenderer {
  readonly format: BracketFormatKey;
  render(input: BracketRendererInput): HTMLElement | SVGSVGElement;
}

/** Public dispatch entry point. */
export function pickBracketRenderer(format: string): BracketRenderer {
  const key = resolveFormatKey(format);
  switch (key) {
    case 'double-elim':  return new DoubleElimRenderer();
    case 'swiss':        return new SwissRenderer();
    case 'round-robin':  return new SwissRenderer('round-robin');
    case 'single-elim':
    default:             return new SingleElimRenderer();
  }
}

export function resolveFormatKey(format: string): BracketFormatKey {
  const f = (format ?? '').toLowerCase();
  if (f.includes('double')) return 'double-elim';
  if (f.includes('swiss')) return 'swiss';
  if (f.includes('round-robin') || f.includes('round_robin') || f === 'rr') return 'round-robin';
  return 'single-elim';
}

// ── Single-elim wrapper ─────────────────────────────────────────────

class SingleElimRenderer implements BracketRenderer {
  readonly format: BracketFormatKey = 'single-elim';
  render(input: BracketRendererInput): HTMLElement {
    const wrap = document.createElement('div');
    wrap.className = 'bracket-format-wrap bracket-format-single-elim';
    wrap.setAttribute('data-testid', 'bracket-format-single-elim');
    if (input.matches.length === 0) {
      wrap.appendChild(buildEmptyNotice('No matches yet — start the tournament to seed the bracket.'));
      return wrap;
    }
    wrap.appendChild(input.singleElimSvg(input));
    return wrap;
  }
}

// ── Swiss / round-robin renderer ────────────────────────────────────

class SwissRenderer implements BracketRenderer {
  readonly format: BracketFormatKey;
  constructor(format: BracketFormatKey = 'swiss') {
    this.format = format;
  }
  render(input: BracketRendererInput): HTMLElement {
    const wrap = document.createElement('div');
    wrap.className = `bracket-format-wrap bracket-format-${this.format}`;
    wrap.setAttribute('data-testid', `bracket-format-${this.format}`);
    if (input.matches.length === 0) {
      wrap.appendChild(buildEmptyNotice(
        this.format === 'round-robin'
          ? 'Round-robin schedule lands when the tournament starts.'
          : 'Swiss pairings appear once the first round is published.'));
      return wrap;
    }

    const byRound = groupByRound(input.matches);
    for (const [round, rows] of byRound) {
      const group = document.createElement('div');
      group.className = `bracket-${this.format}-round`;
      group.setAttribute('data-testid', `bracket-round-${round}`);
      group.setAttribute('data-round', String(round));

      const title = document.createElement('h5');
      title.className = `bracket-${this.format}-round-title`;
      title.textContent = `Round ${round}`;
      group.appendChild(title);

      const table = document.createElement('table');
      table.className = 'bracket-swiss-table';
      const head = document.createElement('thead');
      const hr = document.createElement('tr');
      for (const label of ['Player 1', 'Player 2', 'Result']) {
        const th = document.createElement('th');
        th.scope = 'col';
        th.textContent = label;
        hr.appendChild(th);
      }
      head.appendChild(hr);
      table.appendChild(head);

      const body = document.createElement('tbody');
      for (const m of rows) {
        const tr = document.createElement('tr');
        tr.className = 'bracket-swiss-row';
        tr.setAttribute('data-testid', `bracket-match-${m.round}-${m.matchIndex}`);
        tr.setAttribute('data-match-id', m.id);

        const td1 = document.createElement('td');
        td1.className = 'bracket-swiss-p1';
        td1.textContent = displayName(m.player1);
        tr.appendChild(td1);

        const td2 = document.createElement('td');
        td2.className = 'bracket-swiss-p2';
        td2.textContent = displayName(m.player2);
        tr.appendChild(td2);

        const tdR = document.createElement('td');
        tdR.className = `bracket-swiss-result bracket-swiss-result-${m.status}`;
        tdR.textContent = describeSwissResult(m);
        tr.appendChild(tdR);
        body.appendChild(tr);
      }
      table.appendChild(body);
      group.appendChild(table);
      wrap.appendChild(group);
    }
    return wrap;
  }
}

// ── Double-elim renderer ────────────────────────────────────────────

class DoubleElimRenderer implements BracketRenderer {
  readonly format: BracketFormatKey = 'double-elim';
  render(input: BracketRendererInput): HTMLElement {
    const wrap = document.createElement('div');
    wrap.className = 'bracket-format-wrap bracket-format-double-elim';
    wrap.setAttribute('data-testid', 'bracket-format-double-elim');
    if (input.matches.length === 0) {
      wrap.appendChild(buildEmptyNotice(
        'Double-elimination bracket appears once the tournament starts.'));
      return wrap;
    }

    const { winners, losers, grandFinal } = partitionDoubleElim(input.matches);

    const layout = document.createElement('div');
    layout.className = 'bracket-double-elim-layout';

    layout.appendChild(buildBracketColumn(
      'Winners bracket',
      'winners',
      winners,
    ));
    layout.appendChild(buildBracketColumn(
      'Losers bracket',
      'losers',
      losers,
    ));

    if (grandFinal !== null) {
      const card = document.createElement('div');
      card.className = 'bracket-double-elim-grand-final';
      card.setAttribute('data-testid', 'tournament-grand-final');
      const title = document.createElement('h5');
      title.textContent = '🏆 Grand final';
      card.appendChild(title);

      const row = document.createElement('div');
      row.className = 'bracket-double-elim-grand-final-row';
      row.setAttribute('data-match-id', grandFinal.id);
      row.textContent = `${displayName(grandFinal.player1)} vs ${displayName(grandFinal.player2)} — ${describeSwissResult(grandFinal)}`;
      card.appendChild(row);
      layout.appendChild(card);
    }
    wrap.appendChild(layout);
    return wrap;
  }
}

// ── Helpers ─────────────────────────────────────────────────────────

function buildEmptyNotice(text: string): HTMLDivElement {
  const div = document.createElement('div');
  div.className = 'tournament-bracket-empty';
  div.textContent = text;
  return div;
}

function buildBracketColumn(
  title: string,
  kind: 'winners' | 'losers',
  matches: ReadonlyArray<BracketMatch>,
): HTMLDivElement {
  const col = document.createElement('div');
  col.className = `bracket-double-elim-column bracket-double-elim-${kind}`;
  col.setAttribute('data-testid', `bracket-double-elim-${kind}`);

  const h = document.createElement('h5');
  h.textContent = title;
  col.appendChild(h);

  if (matches.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'bracket-double-elim-empty';
    empty.textContent = kind === 'winners'
      ? 'Winners bracket pairings pending.'
      : 'No eliminations yet.';
    col.appendChild(empty);
    return col;
  }

  const byRound = groupByRound(matches);
  for (const [round, rows] of byRound) {
    const group = document.createElement('div');
    group.className = `bracket-double-elim-round bracket-double-elim-round-${round}`;
    group.setAttribute('data-testid', `bracket-round-${round}`);
    group.setAttribute('data-round', String(round));

    const title = document.createElement('div');
    title.className = 'bracket-double-elim-round-title';
    title.textContent = `Round ${round}`;
    group.appendChild(title);

    const table = document.createElement('table');
    table.className = 'bracket-double-elim-table';
    const body = document.createElement('tbody');
    for (const m of rows) {
      const tr = document.createElement('tr');
      tr.className = 'bracket-double-elim-row';
      tr.setAttribute('data-testid', `bracket-match-${m.round}-${m.matchIndex}`);
      tr.setAttribute('data-match-id', m.id);
      const td1 = document.createElement('td');
      td1.textContent = displayName(m.player1);
      tr.appendChild(td1);
      const td2 = document.createElement('td');
      td2.textContent = displayName(m.player2);
      tr.appendChild(td2);
      const tdR = document.createElement('td');
      tdR.textContent = describeSwissResult(m);
      tdR.className = `bracket-match-${m.status}`;
      tr.appendChild(tdR);
      body.appendChild(tr);
    }
    table.appendChild(body);
    group.appendChild(table);
    col.appendChild(group);
  }
  return col;
}

function groupByRound(matches: ReadonlyArray<BracketMatch>): Map<number, BracketMatch[]> {
  const out = new Map<number, BracketMatch[]>();
  for (const m of matches) {
    const list = out.get(m.round) ?? [];
    list.push(m);
    out.set(m.round, list);
  }
  for (const list of out.values()) {
    list.sort((a, b) => a.matchIndex - b.matchIndex);
  }
  return new Map(Array.from(out.entries()).sort((a, b) => a[0] - b[0]));
}

function displayName(slot: BracketSlot | null): string {
  if (slot === null || slot === undefined) return 'TBD';
  const name = slot.displayName;
  return name === '' || name === undefined || name === null ? 'TBD' : name;
}

function describeSwissResult(m: BracketMatch): string {
  if (m.status === 'complete') {
    const s1 = m.score1 ?? null;
    const s2 = m.score2 ?? null;
    if (s1 !== null && s2 !== null) {
      if (s1 > s2) return `${displayName(m.player1)} won (${s1}-${s2})`;
      if (s2 > s1) return `${displayName(m.player2)} won (${s2}-${s1})`;
      return `Draw (${s1}-${s2})`;
    }
    if (m.winnerPlayerId !== null && m.winnerPlayerId !== '') {
      const winner = m.player1?.playerId === m.winnerPlayerId ? m.player1 : m.player2;
      return `${displayName(winner)} won`;
    }
    return 'Complete';
  }
  if (m.status === 'in-progress') return 'In progress';
  return 'Pending';
}

interface PartitionedMatches {
  winners: BracketMatch[];
  losers: BracketMatch[];
  grandFinal: BracketMatch | null;
}

function partitionDoubleElim(matches: ReadonlyArray<BracketMatch>): PartitionedMatches {
  // Bishop's double-elim wire shape carries a `bracket` discriminator
  // on each match (`'winners' | 'losers' | 'grand-final'`); we read it
  // off `(m as any).bracket` defensively so the renderer keeps
  // working if the field isn't yet populated.  When it's missing we
  // fall back to heuristics: positive rounds go to winners, negative
  // rounds to losers (a common convention in challonge-style data),
  // and the highest-round single match becomes the grand final.
  const winners: BracketMatch[] = [];
  const losers: BracketMatch[] = [];
  let grandFinal: BracketMatch | null = null;
  for (const m of matches) {
    const bracketTag = (m as unknown as { bracket?: string }).bracket;
    if (typeof bracketTag === 'string') {
      const tag = bracketTag.toLowerCase();
      if (tag === 'grand-final' || tag === 'grand_final' || tag === 'final') {
        grandFinal = m;
        continue;
      }
      if (tag === 'losers' || tag === 'loser' || tag === 'lower') {
        losers.push(m);
        continue;
      }
      if (tag === 'winners' || tag === 'winner' || tag === 'upper') {
        winners.push(m);
        continue;
      }
    }
    if (m.round < 0) {
      losers.push(m);
    } else {
      winners.push(m);
    }
  }
  if (grandFinal === null) {
    // Heuristic: the final match in the winners bracket (after
    // ordering by round desc, matchIndex desc) where both players
    // are filled is treated as grand final if it sits a round above
    // all other matches.
    const sorted = winners.slice().sort((a, b) => b.round - a.round || b.matchIndex - a.matchIndex);
    if (sorted.length > 0) {
      const top = sorted[0];
      const others = sorted.slice(1);
      const isolated = others.every(o => o.round < top.round);
      if (isolated && top.player1 !== null && top.player2 !== null) {
        grandFinal = top;
        winners.splice(winners.indexOf(top), 1);
      }
    }
  }
  // Normalize losers' rounds to positive ascending for display.
  const allLosersNegative = losers.length > 0 && losers.every(m => m.round < 0);
  if (allLosersNegative) {
    for (const m of losers) {
      (m as { round: number }).round = Math.abs(m.round);
    }
  }
  return { winners, losers, grandFinal };
}
