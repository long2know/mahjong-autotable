# Swiss-system pairing — FIDE C.04 Dutch variation

Phase K Wave 11 (Bishop). Describes the pairing algorithm used by
Mahjong-Autotable's Swiss-format tournaments and the tiebreaker
stack that converts the per-round results into a final ranking.

Two services jointly own the surface:

| Service | Responsibility | Citation |
|---------|----------------|----------|
| `FideC04SwissPairingService` | Per-round pairings (this doc) | `src/backend/src/Mahjong.Autotable.Api/Tournament/FideC04SwissPairingService.cs` |
| `SwissStandingsService` | Final-standings tiebreaker stack | `src/backend/src/Mahjong.Autotable.Api/Tournament/SwissStandingsService.cs` |

The pairing service is **pure** — no DI dependencies, no clock,
no random source. Identical `(seededPlayers, matchPoints,
priorPairings)` inputs always produce identical output, byte-for-
byte.

## 1. Wave-by-wave history

| Wave | Behaviour | Status |
|------|-----------|--------|
| W-J | `TournamentPairing.SwissFirstRound` — round-1 only, top-half vs bottom-half by seed. | Superseded |
| W10 | `DutchSwissPairingService` — per-round Dutch-style, top-half vs bottom-half per score group + single-swap rematch avoidance + odd-group float-down. | Retained as W10 baseline (contract suite + regression pin) |
| W11 | `FideC04SwissPairingService` — FIDE C.04.1 Dutch-variation with full backtracking + Berger / pre-round Buchholz bracket ordering. **Live binding.** | **Current** |

`DutchSwissPairingService` is not deleted — it remains available
as the W10 contract baseline (the `Phase_K_W10/Bishop/DutchSwissPairingTests`
suite + Vasquez's `BishopW10DutchSwissPairingTests` + the
`Regression/Wave1ThroughKW10RegressionTests` regression pin
all pin its public surface). W11 swaps the live
`ISwissPairingService` DI binding to the FIDE C.04 service; both
implementations conform to the same interface so the swap is
transparent to callers.

## 2. Inputs and outputs

```csharp
IReadOnlyList<TournamentPairing.Pairing> PairNextRound(
    IReadOnlyList<string> seededPlayers,
    IReadOnlyDictionary<string, int> matchPoints,
    IReadOnlyCollection<(string A, string B)> priorPairings);
```

* **`seededPlayers`** — players in their seed order, highest seed
  first (index 0 = top seed). Used as the deterministic tiebreak
  when match-points + pre-round Buchholz are equal.
* **`matchPoints`** — current per-player match-point totals
  entering this round. Missing entries default to zero (round 1).
* **`priorPairings`** — every previously-played pair, as ordered
  `(playerA, playerB)` tuples. The service computes both
  rematch-avoidance and pre-round Buchholz from this list. A
  pair `(p, "__bye__")` records a previous bye and excludes the
  player from the next bye candidate set.

The output is a list of `TournamentPairing.Pairing(P1, P2, _, _)`
records. `P2 == "__bye__"` indicates the round bye — the
service layer interprets this and awards a default-win match.

## 3. Algorithm (FIDE C.04.1 Dutch variation with backtracking)

### 3.1 Score brackets

Players are grouped by match-point total. Brackets are ordered
descending by match-points. Within a bracket, players are ordered
by:

1. Descending **pre-round Buchholz** (sum of opponents' current
   match points) — the FIDE "Berger order" tiebreak. Biases the
   top of each bracket toward the player who has met the
   strongest field so far.
2. Ascending **seed index** — deterministic final tiebreak. We
   don't model colours (mahjong is a 4-player heat abstracted to
   a 2-player pairing surface, so there's no "white/black"
   balance to enforce); seed index plays the role of the FIDE
   colour-balance tiebreak.

### 3.2 Bye assignment

When the total roster is odd, the round bye is **pre-assigned**
to the lowest-ranked player who has not previously received a
bye. FIDE C.04 § "Bye": lowest from the lowest bracket. If every
player has already had a bye, the lowest-ranked player overall
takes it. The bye-recipient is removed from their bracket so
the rest of the algorithm pairs an even number of players.

### 3.3 S1 vs S2 split

For each bracket, the algorithm splits into top-half `S1` and
bottom-half `S2`. The bracket is even by construction (odd
entrants float out before the split — see §3.4). The initial
pairing is `S1[i]` vs `S2[i]` for each `i`.

### 3.4 Backtracking on rematch

When any `S1[i]` vs `S2[i]` pair is already in `priorPairings`,
the service permutes `S2` in lexicographic seed order looking for
the first permutation that produces zero rematches. The search
is capped at `MaxPermutationsPerBracket = 5040` (= 7!) — the
FIDE handbook acknowledges that a true exhaustive search is
`O(n!)` and a capped backtrack is the practical recommendation.

If no permutation produces a clean pairing, the lowest-ranked
player **floats down** to the next bracket and the algorithm
re-enters with the smaller bracket. Multiple cascading floats
are supported — rare, but happens late in long tournaments where
the rematch graph is dense.

### 3.5 Last-bracket fallback

When the last (lowest) bracket exhausts every permutation with
rematches, the lex-smallest permutation is accepted as the
fallback so no entrant is dropped. This is the FIDE
"unavoidable rematch" tolerance — emitted with the same
deterministic tiebreak so a re-run produces the same pairing.

## 4. Tiebreakers — pairing-time vs standings-time

Two distinct tiebreaker stacks exist; they are **not**
interchangeable.

### 4.1 Pairing-time (`FideC04SwissPairingService`)

Order players **within a score bracket** when deciding who pairs
against whom:

1. Match-points (always identical inside a bracket).
2. Pre-round Buchholz — `ComputePreRoundBuchholz(playerId, …)`.
3. Seed index ascending (lower index = higher seed).

The Sonneborn-Berger pre-round helper
`ComputeSonnebornBerger(playerId, outcomes, matchPoints)` is
exposed for callers that want to feed the FIDE C.04 §B.2
tiebreak; the live pairing path defaults to the Buchholz tiebreak
because the surface only models win/loss/bye, not the explicit
W/D/L outcomes Sonneborn-Berger requires.

### 4.2 Standings-time (`SwissStandingsService`)

Order players in the **final published ranking** at tournament
end:

1. Total wins.
2. Median-Buchholz (drop highest + lowest opponent).
3. Sonneborn-Berger.
4. Cumulative score.
5. PlayerId ordinal.

The standings stack drops the highest + lowest opponent
(Median-Buchholz) which the pairing stack deliberately does not
— a player who happens to face a strong field early in the
tournament should not lose pairing-time priority because of an
outlier opponent.

## 5. Determinism guarantee

The service is hard-asserted-deterministic. The contract pins:

* Same inputs → byte-identical outputs across runs.
* Bracket ordering is stable when ties exist (Berger + seed).
* Bottom-half permutation walk uses lex-ordinal `string` compare
  so the first legal permutation is the same on every host.
* No `DateTime.UtcNow`, no `Random`, no static mutable state.

The Wave-11 contract tests in `Phase_K_W11/Bishop/`
`FideC04SwissPairingFacts.cs` pin all 30+ behaviours including
the bye-rotation invariant, the pre-round Buchholz tiebreak,
and known-correct FIDE handbook examples.

## 6. Known limitations

* **Colour balance not modelled.** Chess Swiss pairing factors in
  the player's white/black sequence so no player gets too many
  consecutive same-colour games. Mahjong has no equivalent
  surface — every heat seats four players and the wind assignment
  is a separate concern handled by the runtime — so the colour-
  balance tiebreak collapses into the seed-index tiebreak.
* **Permutation cap.** The backtrack ceiling
  (`MaxPermutationsPerBracket = 5040`) is generous for realistic
  score groups but a pathological bracket of >7 entrants could
  trigger the float-down path more often than a true exhaustive
  search. The cap mitigates the worst-case `O(n!)` blow-up; the
  fallback (float-down → next bracket) is still FIDE-legal.
* **Player drop-outs mid-tournament.** Not yet modelled. A
  withdrawn player still appears in `seededPlayers` and gets
  paired; the operator surface should remove them via the
  forfeit endpoint
  (`Mahjong.Autotable.Api.Tournament.TournamentForfeitService`).
  The pairing service treats a forfeit as a normal "previously
  played" entry once the forfeit row is recorded.

## 7. References

* FIDE Handbook §C.04.1 — Dutch system rules. The canonical
  spec we implement.
* `docs/bracket-shape.md` — wire-shape details for the
  bracket / pairing surfaces.
* `docs/realtime-resilience.md` — the `TournamentMatchHub`
  broadcaster that publishes pairing results live.
