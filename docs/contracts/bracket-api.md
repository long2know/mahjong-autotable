# Tournament bracket wire contract

> **Status:** Authoritative as of Phase K Wave 9 (2025-Q4).
> **Owners:** Bishop (backend) is wire-shape authority. Hicks
> (frontend) is renderer authority. Vasquez (test) owns spec
> mocks that exercise this surface.

This document pins the **canonical** JSON shape returned by the
`GET /api/tournaments/{id}` and `GET /api/tournaments/{id}/bracket`
endpoints. The autotable client hard-requires this shape; any
deviation surfaces a visible `bracket-shape-error` banner in the
admin / spectator bracket view and emits a `console.error` so QA
flags it instead of the renderer silently mis-displaying matches.

Why the hard-fail? Through W6→W8 the client tolerated multiple
field-name spellings (`layout` vs `bracketLayout` vs
`doubleElimLayout`; `winners` vs `winnersBracket`; `grand_final`
vs `grandFinal`). That kindness masked a real Bishop contract
drift in W7 that wasn't noticed until Vasquez stood up the W8
specs. W9 retired the tolerance — Bishop ships canonical or the
renderer refuses to draw.

## 1 — `GET /api/tournaments/{id}` (tournament detail)

Top-level response object:

```jsonc
{
  "tournament": { /* TournamentSummary, see types.ts */ },

  // Flat match list — primarily for single-elim / Swiss formats.
  // Double-elim consumers should rely on `layout` (below) and
  // treat `matches` as informational only.
  "matches": [ /* BracketMatch[] */ ],

  // Live standings rows (Swiss / round-robin / tiebreaker).
  "standings": [ /* StandingRow[] */ ],

  // Registered players, in seat order.
  "players": [ /* BracketSlot[] */ ],

  // Double-elim partition — REQUIRED for double-elim format,
  // OMITTED (or null) for single-elim / Swiss.  See §1.1.
  "layout": { /* DoubleElimLayout, see §1.1 */ } | null,

  "viewerRegistered": true | false,
  "viewerCanStart":   true | false
}
```

### 1.1 — `DoubleElimLayout`

```jsonc
{
  "winnersBracket": [ /* BracketMatch[] */ ],
  "losersBracket":  [ /* BracketMatch[] */ ],
  "grandFinal": {
    "match":      /* BracketMatch */ null,
    "resetMatch": /* BracketMatch */ null  // present iff losers
                                            // side wins finals
  }
}
```

**Field-name discipline (W9 canonical):**

| Position             | Canonical key       | NOT accepted               |
|----------------------|---------------------|----------------------------|
| Top-level wrapper    | `layout`            | `bracketLayout`, `doubleElimLayout` |
| Winners side         | `winnersBracket`    | `winners`, `winners_bracket` |
| Losers side          | `losersBracket`     | `losers`,  `losers_bracket`  |
| Finals object        | `grandFinal`        | `grand_final`, `finals`     |
| Finals match         | `grandFinal.match`  | `grandFinal.first`, raw object at `grandFinal` |
| Reset match          | `grandFinal.resetMatch` | `grandFinal.reset`, `grandFinal.reset_match` |

`BracketMatch` fields are normalised by `normalizeMatches` in
`src/frontend/autotable-src/src/tournaments.ts`; the canonical
keys are `id`, `roundNumber`, `matchIndex`, `seedA`, `seedB`,
`winnerSeed`, `status`, `playerA`, `playerB`, `bracketSide`,
`scheduledAtUtc`.

## 2 — `GET /api/tournaments/{id}/bracket` (bracket snapshot)

Bishop's W8 dedicated bracket endpoint. Shape (per
`TournamentController.cs:181-208`):

```jsonc
{
  "winnersBracket": [
    {
      "roundNumber": 1,
      "slots": [
        {
          "matchIndex": 0,
          "seedA": 1,
          "seedB": 8,
          "winnerSeed": 1,        // 0 / null until resolved
          "status": "completed",  // "pending" | "in-progress" | "completed"
          "bracketSide": "winners"
        }
        /* … */
      ]
    }
    /* … */
  ],
  "losersBracket": [ /* BracketRound[] same shape, bracketSide:"losers" */ ],
  "grandFinal": {
    "match":      /* BracketSlot | null */,
    "resetMatch": /* BracketSlot | null */
  }
}
```

The renderer derives `DoubleElimLayout` from this snapshot by
flattening `winnersBracket[].slots[]` into a single
`BracketMatch[]` with `roundNumber` preserved on each match
(round groupings are rebuilt visually from `match.roundNumber`).

## 3 — Renderer behaviour

`DoubleElimRenderer.render(input)`
(`src/frontend/autotable-src/src/bracket-renderer.ts`):

1. If `input.layout === null` and there are no matches → render
   the empty-state notice (`Double-elimination bracket appears
   once the tournament starts.`).
2. If `input.layout === null` and matches are present → render
   `<div data-testid="bracket-shape-error" role="alert">` and
   `console.error` with the canonical-shape reminder.
3. If `input.layout !== null` → render winners column + losers
   column + grand-final row from the layout.

The W6→W8 round-number-sign heuristic (`partitionDoubleElim`,
negative roundNumber = losers side) is retired in W9. The
function survives in `bracket-renderer.ts` for its unit tests
but production code no longer invokes it.

## 4 — Migration / deploy ordering

When Bishop changes a wire field name:

1. Land the **new** canonical name in the controller behind a
   feature flag that emits both old + new keys for one wave.
2. Update this document with the new canonical key.
3. Hicks updates `normalizeDetail` / `normalizeDoubleElimLayout`
   to require only the new key.
4. Vasquez updates spec mocks to ship only the new key.
5. Bishop removes the feature flag (and the old key) in the
   next wave.

Do **not** silently accept multiple spellings across waves.
The W7 drift we paid for in W9 is the cautionary tale.
