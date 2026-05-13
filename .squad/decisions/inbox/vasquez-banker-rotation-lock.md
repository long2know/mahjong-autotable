# Vasquez — Banker Rotation Canonical Lock (Phase 3)

**Date:** 2026-05-13
**By:** Vasquez (Rules Engineer)
**Branch:** `stlong/changsha-v1-phase3`
**Spec version:** `docs/rules/changsha-spec.md` v1.1 → **v1.2**
**Status:** LOCKED — supersedes v1.1 banker rotation rule.

---

## Decision

**Canonical Changsha banker rotation (v1.2, LOCKED):**

> **The winner of a hand becomes the dealer for the next hand. On washout (wall exhausted with no winner), the current dealer keeps the seat. The hand counter increments regardless.**

There is **no** cyclic seat rotation (`+1 mod 4` or `-1 mod 4`) in v1. The next dealer is determined entirely by the hand outcome:

| Hand outcome | Next `DealerSeatIndex` |
|---|---|
| Winner declared (self-draw 自摸 or discard claim 点炮) | `winnerSeatIndex` (the seat that just won) |
| Washout (wall exhausted, no winner) — 流局 | Unchanged (current dealer keeps seat) |

`HandNumber` increments in both cases.

---

## Why this decision was forced

The v1.1 spec was **internally inconsistent and contradicted every canonical source**:

1. **v1.1 §6.2 text** said "dealer keeps seat on dealer-win; rotate counter-clockwise on non-dealer-win or draw" — a deliberate v1 simplification.
2. **v1.1 §6.2 example** demonstrated `-1 mod 4` (Seat 0 → Seat 3, Seat 3 → Seat 2).
3. **Backend implementation** at `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaStateMachine.cs:458,465` uses `+1 mod 4` — direction-inverted vs. the spec example.
4. **All three canonical sources** say "winner becomes dealer," not cyclic rotation.

So the spec disagreed with itself, the implementation disagreed with the spec example, and *both* disagreed with the canonical rule. Three different behaviors for one rule. Unacceptable for a Phase 3 gate.

---

## Source review (verified 2026-05-13 via web_fetch)

All three canonical sources agree — **winner becomes dealer**:

### MahjongPros (S1 — locked tiebreaker per Stephen, 2026-05-13)
> "In subsequent games the dealer is determined in one of the following orders:
> 1. The winner of the previous game becomes the new dealer.
> 2. In the case of a draw, the player that draws the last tile becomes the dealer.
> 3. If multiple players win simultaneously, the dealer is determined randomly among the winners based on consensus."

### Baidu / Tencent QQ (S2)
> "A. For the first round, the dealer is randomly assigned by the system.
> B. **In subsequent rounds, whoever wins a hand becomes the dealer for the next round.**
> C. If a player takes the bottom tile and no one wins, then that player becomes the dealer for the next round.
> D. If none of the four players want the bottom tile, then the player who has the first option to take the bottom tile in the next round becomes the dealer."

### Reddit (S3 — community overview)
Winner-becomes-dealer (consistent with S1/S2).

**Tiebreaker resolution:** Where S1 and S2 give finer-grained washout rules (last-drawer-becomes-dealer, etc.), v1 simplifies to **"washout keeps the seat"**. Rationale:

- V1 has no concept of "who drew the last tile" exposed in `ChangshaGameState`.
- "Washout keeps seat" is unambiguous, deterministic, and trivial to implement.
- Matches the dominant majority of online digital implementations.
- The finer-grained rule is captured as a documented v2 refinement in §6.2.

---

## Worked example (now in §6.2)

Starting with Seat 0 as dealer:

| Hand | Dealer | Outcome | Next Dealer |
|------|--------|---------|-------------|
| 1 | Seat 0 | Seat 2 wins | **Seat 2** |
| 2 | Seat 2 | Washout | **Seat 2** (unchanged) |
| 3 | Seat 2 | Seat 1 wins | **Seat 1** |
| 4 | Seat 1 | Seat 0 wins | Seat 0 |

This is the canonical sequence Bishop and Hudson should both encode.

---

## Impact on Bishop (Backend) — required change

**File:** `src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaStateMachine.cs`

**Lines 458 and 465** (the `RotateBanker` helper / inline `+1 mod 4` logic):

**Replace** the current `state.DealerSeatIndex = (state.DealerSeatIndex + 1) % 4` (or equivalent rotation arithmetic) with:

```csharp
// Canonical Changsha v1.2 banker rotation (per docs/rules/changsha-spec.md §6.2):
//   - Winner: winner becomes next dealer.
//   - Washout: current dealer keeps the seat.
if (winnerSeatIndex.HasValue)
{
    state.DealerSeatIndex = winnerSeatIndex.Value;
}
// else: washout — leave state.DealerSeatIndex unchanged.

state.HandNumber += 1;
```

**Must NOT do:**
- No `(state.DealerSeatIndex + 1) % 4` cyclic rotation.
- No `(state.DealerSeatIndex - 1 + 4) % 4` cyclic rotation.
- No "dealer keeps seat only when dealer wins" special-case.

**Verification:** A 16-hand replay where Seat 2 wins hand 1, Seat 2 wins hand 2, washout hand 3, Seat 0 wins hand 4 must produce the dealer sequence `[Seat 0, Seat 2, Seat 2, Seat 2, Seat 0]` (initial through post-hand-4).

---

## Impact on Hudson (Tests) — required new coverage

Add at minimum **one parametric test** asserting winner-becomes-dealer across multiple hands. Suggested test name: `BankerRotation_WinnerBecomesNextDealer_AcrossMultipleHands`.

**Test outline:**

```csharp
[Theory]
[InlineData(/* hand-outcome sequence */)]
public void BankerRotation_FollowsCanonicalRule(...)
{
    // Arrange: start a 4-hand game, Seat 0 = initial dealer
    // Act: replay outcomes — hand 1: Seat 2 wins; hand 2: washout; hand 3: Seat 1 wins
    // Assert dealer sequence:
    //   - After hand 1: state.DealerSeatIndex == 2
    //   - After hand 2: state.DealerSeatIndex == 2 (unchanged on washout)
    //   - After hand 3: state.DealerSeatIndex == 1
    // Assert HandNumber increments after every hand.
}
```

Also add **negative assertions** that the legacy `+1 mod 4` and `-1 mod 4` behaviors are gone:
- After a non-dealer-win, the new dealer is **the winner**, not "winner − 1" or "dealer + 1".
- After a dealer-win, the dealer keeps the seat (degenerate case of winner-becomes-dealer).
- After a washout, the dealer is **unchanged**, not "dealer + 1" or "dealer − 1".

---

## Documentation updates applied (this commit)

- `docs/rules/changsha-spec.md` header bumped `v1.1 → v1.2`, dated 2026-05-13, changelog added.
- **§6.2** rewritten — canonical winner-becomes-dealer + washout-keeps-seat, with source quotes, worked example, and explicit implementation contract.
- **§7.2** state-transition table updated: `PAYMENT → ROTATING_DEALER` sets dealer = winner; `WALL_EXHAUSTED → ROTATING_DEALER` leaves dealer unchanged.
- **§9 OQ-10** (Dealer retention) updated to point at the v1.2 canonical rule.
- **§11 assumption #9** updated to v1.2 canonical wording.
- **§12 conformance checklist** "Banker & Game Flow" section rewritten; explicit checkbox forbidding `+1 mod 4` / `-1 mod 4`.
- **§5.2 base unit** clarified: default = 1 (raw values); 10/100 are optional overrides. (Aligns with what `ScoringService` already does and what the v1 conformance audit flagged.)
- **§9 OQ-5** and **§10 OQ-6** updated to reflect default base unit = 1.

---

## Out of scope for this lock

- **§3.3 Claim Priority:** Already correctly states `Hu > Kong = Pung > Chow` with CCW proximity tiebreak. Verified — no change needed. (Note: Bishop's audit flagged that the *implementation* ranks Kong above Pung; that is a backend bug for Bishop to fix, not a spec issue.)
- **§3.6 Missed Win (过胡):** Already in v1 scope (not deferred). Verified — no change needed. (Note: implementation does not enforce it yet; that is a separate Bishop task.)
- **§2.7 Instant Win Check:** Still contradicts §4.3 (instant wins deferred to v2). Documented as a v1.0 legacy hygiene item in the prior conformance audit, non-blocking for this lock.

---

## Decision authority

Per `.squad/decisions.md` 2026-05-13 entry: "When the three reference sources disagree, MahjongPros is the tiebreaker." All three agree on winner-becomes-dealer, so this is the strongest possible consensus — no tiebreaker invocation needed. The v1 washout simplification ("dealer keeps seat") is Vasquez's call as Rules Engineer under the Phase 3 spec-lock charter; Stephen explicitly directed this wording in the Phase 3 task brief.
