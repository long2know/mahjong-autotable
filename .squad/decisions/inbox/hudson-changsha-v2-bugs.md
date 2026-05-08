# Hudson — Bishop bug findings (Changsha v1 Phase 2)

Discovered while wiring CAT-G ScoringTests against shipped `ScoringService`.
**Production code intentionally NOT modified** by Hudson; tests assert spec-correct
behavior and will go GREEN automatically once Bishop applies these fixes.

## Bug 1 — Small Win self-draw uses flat base, ignores dealer involvement

**File:** `src/backend/src/Mahjong.Autotable.Api/Changsha/ScoringService.cs`
**Symptom:** Non-dealer self-draws Standard hand → every other seat (including
dealer) pays a flat 2 each. Spec §5.1 says non-dealer pays 1, dealer pays 2.

**Root cause:** `CalculateSelfDrawPayments` uses a single constant
`SmallWinSelfDrawBase = 2` for the SmallWin branch, with no
`dealerInvolved ? base+1 : base` adjustment (the BigWin branch has it).

**Spec excerpt (§5.1):**
> Small Win — Self draw: each non-winner pays 1; dealer (if not the winner) pays 2.

**Failing test:** `ScoringTests.SmallWin_NonDealerSelfDraw_DealerPays2_OthersPay1`
> Expected: dealer-seat-payment 2, other-non-dealer-payment 1.
> Actual: every payment is 2.

**Suggested fix:** apply the same dealer-involved branching used for BigWin
self-draw. Add a `SmallWinSelfDrawDealer = 2`, keep `SmallWinSelfDrawBase = 1`.

```csharp
// inside CalculateSelfDrawPayments, SmallWin branch:
amount = (dealerInvolved ? SmallWinSelfDrawDealer : SmallWinSelfDrawBase);
```

---

## Bug 2 — Full Flush silently doubles the Big Win payment

**File:** `src/backend/src/Mahjong.Autotable.Api/Changsha/ScoringService.cs`
**Symptom:** A Full Flush Big Win pays double the standard Big Win amount
(12 instead of 6 from a non-dealer discarder; 14 instead of 7 if dealer involved).

**Root cause:** Line ~49 of `CalculateScore`:
```csharp
var flushMultiplier = isFullFlush && category == ScoreCategory.BigWin ? 2 : 1;
```
Both `Calculate*Payments` helpers then multiply by this.

**Spec excerpt (§5.1, locked v1):**
> Big Win categories (AllPungs, SevenPairs, FullFlush) all pay the same flat amount.
> No stacking or doubling in v1; multiplier extensions deferred to v2.

**Failing tests:**
- `ScoringTests.FullFlush_BigWin_SingleTier_NoDoubling` (Expected 6, Actual 12)
- (`SmallWin_NonDealerSelfDraw_...` is bug #1, not this)

**Suggested fix:** remove the `flushMultiplier` entirely; replace the two call
sites with the unmultiplied amount.

```csharp
// drop:
//   var flushMultiplier = isFullFlush && category == ScoreCategory.BigWin ? 2 : 1;
// callers no longer multiply by flushMultiplier.
```

---

## Verification once fixed

After both fixes, `dotnet test --filter Category=Changsha` should report
**70 passed, 0 failed, 7 skipped, 77 total.**
