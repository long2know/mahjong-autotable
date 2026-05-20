### 2026-05-19T18:50Z: Phase G backend — bot pickup tick scheduler + privacy-mask slot-parse fix

**By:** Bishop (backend)
**Branch:** `stlong/phase-g-bot-scheduler-lobby` (cut from `main` @ `1e9134a`)
**Inputs:**
- Stephen's Phase G directive ("two tasks, strict file scope").
- Bishop's own Phase F memo (`.squad/decisions/inbox/bishop-phase-f-backend.md`), gotchas §4
  ("Bot pickup ticks NOT YET wired") and "Test bugs surfaced" §2
  ("Pre-existing bug in `FilterEntriesForViewer`").
- Vasquez's `Pickup_PrivacyMask_OpposingHandsHaveFacesStripped` test in
  `ManualPickupAcceptanceTests.cs` (already updated to the correct `EndsWith("@N")`
  convention; this commit makes the production filter match).

## Task 1 — Bot pickup tick scheduler

**Problem statement.** Phase F shipped the manual-pickup state machine and the 3-tier
bot engine but left a user-visible gap: when a bot becomes the active
`state.PickupSeatIndex` during a manual-deal pickup phase, nothing on the server side
fires `TakeTilesFromWallAsync` — so humans can play but bots freeze in pickup.

**Contract (production code; stable names for Vasquez's tests):**

- `ScheduleBotIfNeededAsync(ChangshaGameInstance instance, CancellationToken ct)`:
  - **New branch** — when `ChangshaGameStateMachine.IsPickupPhase(instance.State.Phase)`:
    use `instance.State.PickupSeatIndex` (NOT `ActiveSeatIndex` — the latter holds the
    dealer's seat throughout pickup); if it's a bot seat
    (`instance.State.Seats[pickupSeat].IsBot`), schedule a private
    `RunBotPickupAsync(instance, pickupSeat, instance.LifecycleCts.Token)`.
  - **Unchanged branch** — own-turn discard scheduling (`AwaitingDiscard` + active seat
    is bot → `RunBotTurnAsync`).
- `RunBotPickupAsync(ChangshaGameInstance instance, int seatIndex, CancellationToken ct)`
  (private):
  1. `await Task.Delay(_options.BotPickupDelayMs, ct)` — Phase F's existing knob, default 500 ms.
  2. Acquire `instance.Lock`, re-validate (phase still pickup, picker still
     `seatIndex`, seat still a bot), compute
     `expected = ChangshaGameStateMachine.ExpectedPickupCount(phase)`, release lock.
  3. `await TakeTilesFromWallAsync(instance.GameId, seatIndex, expected, ct)` — which
     re-validates under the state machine and re-invokes `ScheduleBotIfNeededAsync`,
     so the chain self-perpetuates CCW.
- `RollDiceAsync(...)`: now ends with `await ScheduleBotIfNeededAsync(instance, ct)`
  after the `StateChanged?.Invoke(...)` — so when `BeginManualDeal` parks at
  `BreakPointMarked` and the dealer is a bot, the chain starts on its own.
- `TakeTilesFromWallAsync(...)`: still routes to `TryAdvanceAfterDealAsync` when
  `phase == AwaitingDiscard` (turn-loop start path is unchanged), but now also calls
  `ScheduleBotIfNeededAsync` in the still-in-pickup branch so the chain marches
  through the remaining seats.

**Invariants preserved:**
- Bot tick fires ONLY when the active `PickupSeatIndex` is a bot. Human picker → the
  scheduler no-ops, runtime blocks waiting for the UI to send `take`.
- All state mutations stay under `instance.Lock`. The scheduler reads state without
  the lock (racy by design) but the spawned task re-validates under it.
- Cancellation: bot pickup tasks use `instance.LifecycleCts.Token`, the same source
  that backs `RunBotTurnAsync`; `ChangshaGameInstance.DisposeAsync` already cancels
  it on game teardown.
- The auto-deal (`DealMode.Auto`) path is untouched — `StartGameAsync` still runs the
  one-shot `Deal()` and never enters a pickup phase.

## Task 2 — `FilterEntriesForViewer` slot-parse fix

**Problem statement.** The pre-Phase-G implementation extracted the seat from the
substring **between `.` and `@`** in `"hand.{handIdx}@{seat}"`. That substring is the
hand index, not the seat. The effect was double-wrong: viewer's own `hand.1@self`
... `hand.13@self` slots were masked (treated as opposing); opponents'
`hand.0@other` slots were leaked (treated as own).

**Fix.** Slots are now parsed at the **last `@`**, and the privacy mask is universal
(face stripped on any `@`-suffixed foreign slot) while rotation is forced face-down
only on `hand.*` slots:

```csharp
var at = slotName.LastIndexOf('@');
if (at < 0 || at == slotName.Length - 1) { pass-through; continue; }
if (!int.TryParse(slotName.AsSpan(at + 1), out var slotSeat)) { pass-through; continue; }
if (viewerSeat.HasValue && slotSeat == viewerSeat.Value) { keep face-up; continue; }
// Foreign seat: strip face universally; force rotation=2 only for hand.* slots
// (discards/melds/walls keep their public translator-supplied rotation).
var forceHandFaceDown = slotName.StartsWith("hand.", StringComparison.Ordinal);
filtered.Add(new CollectionEntry(entry.Kind, entry.Key, StripFace(je, forceHandFaceDown)));
```

**Asymmetric mask rationale (Vasquez Test 5):** Vasquez's `Filter_HandlesMalformedSlots`
exercises non-hand slots like `weird@foo@1` and asserts implementation-defined latitude on
masking, but the spec calls for universal face-strip on any `@`-suffixed foreign slot.
Tests 2 and 4 lock `rotationIndex == 2` only against `hand.*` slots; non-hand slots
(`discard.*`, `meld.*`, `wall.*`) must keep their translator rotation so discards render
face-up and concealed-kong melds keep their authored face-down pose. The split is
encoded by `forceHandFaceDown`: face-strip is universal, rotation override is hand-only.

**Convention now documented in the method's XML doc:**
> Slot-suffix convention (per `AutotableSlotMap.HandSlot`): hand slots are formatted
> `hand.{handIdx}@{seat}`. The owning seat is the integer AFTER the last `@` — not the
> digit between `.` and `@`, which is the per-seat hand index. Wall / discard / meld
> slots follow the same `{kind}…@{seat}` suffix convention. Slots without `@` carry no
> per-seat privacy semantics and pass through untouched. Slots with an unparseable
> suffix (`trailing@`, `garbled@abc`) also pass through — privacy fails open on
> malformed input.

**Spectator behavior:** `viewerSeat == null` now masks every `@`-suffixed entry (the
`HasValue` short-circuit on equality means no slot can ever match a null viewer).

**Helper rename:** `StripFaceAndForceFaceDown(JsonElement)` → `StripFace(JsonElement,
bool forceHandFaceDown)`. The rotation override is now conditional.

## Verification

- `dotnet build src/backend/Mahjong.Autotable.slnx --nologo` → 0/0, ~6s.
- `dotnet test src/backend/Mahjong.Autotable.slnx --nologo --no-build` → **330/0/9/339**, ~15s.
  Phase F baseline (319/0/9) plus Vasquez's Phase G additions (11 new facts: 6 in
  `BotPickupSchedulerAcceptanceTests`, 5 in `PrivacyMaskAcceptanceTests`) all green.
  Re-ran three times back-to-back; no flakes observed.
- `dotnet test --filter "FullyQualifiedName~PrivacyMaskAcceptanceTests"` → **5/0**, 59 ms.
- `dotnet test --filter "FullyQualifiedName~BotPickupSchedulerAcceptanceTests"` → **6/0**, 9 s.

## Files modified (production only)

- `src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs`:
  `ScheduleBotIfNeededAsync` extended with pickup branch; new private
  `RunBotPickupAsync`; `RollDiceAsync` and `TakeTilesFromWallAsync` invoke
  `ScheduleBotIfNeededAsync` to keep the chain going.
- `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs`:
  `FilterEntriesForViewer` re-parses slot at last `@`; XML doc rewritten to document
  the suffix convention.

## Files NOT touched (file-scope discipline)

- All test files (Vasquez owns them).
- All frontend files (Hicks owns them).
- `Changsha/Bot/*.cs` — engine is intact from Phase F.
- `Changsha/ChangshaStateMachine.cs` — no state-machine changes needed; the pickup
  invariants are already enforced inside `TakeTilesFromWall`.
- `Changsha/ChangshaToAutotableTranslator.cs` — Wave-3 viewerSeat privacy on
  translator output is unchanged.
- `ChangshaRuntimeOptions.cs` — `BotPickupDelayMs` already exists from Phase F (500 ms).

## Remaining follow-ups (Bishop, future sessions, not blocking)

- Add a small standalone unit test for `FilterEntriesForViewer` covering spectator
  + multi-seat hand-entry mix + non-hand `@seat` slot pass-through. Currently only
  the indirect path through `ChangshaToAutotableTranslator` is covered.
- Consider extracting the slot-parse helper (`TryParseHandSeat(string slot, out int seat)`)
  to `AutotableSlotMap` once another consumer needs it; today only the filter parses.

## Handoffs

- **Hicks (frontend):** the bot-pickup auto-tick is now server-driven. UI doesn't need
  a client-side timer for bot pickup seats — `BotPickupDelayMs` paces the chain on the
  server. The `pickup["current"]` entry continues to flow on every transition.
- **Vasquez:** the production contract for `RunBotPickupAsync` is stable as described
  above. Tests that assert "bot completes a 4-tile pickup `BotPickupDelayMs` ms after
  becoming the active picker" should pass; tests that assert "scheduler does not fire
  when picker is human" should also pass (the `IsBot` check is the same one
  `ScheduleBotIfNeededAsync` already enforces for own-turn).
