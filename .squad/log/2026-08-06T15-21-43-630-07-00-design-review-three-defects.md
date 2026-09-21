# Design Review ceremony — three live-release defects (START)

- **Ceremony:** Design Review (auto; when=before; time budget=focused)
- **Began:** 2026-08-06T15:21:43.630-07:00
- **Facilitator:** Ripley (Lead)
- **Participants:** Hicks, Ferro, Vasquez, Bishop, Hudson
- **Recorder:** Scribe (background)
- **State backend:** local (`FSStorageProvider`) · **Team root:** `/data/source/mahjong-autotable/.squad`

## Trigger
Fix three defects observed on the live release:
1. **Post-Chow discard deadlock** — after a Chow claim the turn cannot proceed to discard (stuck turn).
2. **Unreadable setup selects** — the setup/lobby `<select>` controls render unreadable.
3. **Incorrect physical wall depletion** — the physical wall depletes in the wrong order/position.

## Agenda (Design Review)
1. Review the task and requirements.
2. Agree on interfaces and contracts between components.
3. Identify risks and edge cases.
4. Assign action items.

## Live investigation in flight (sibling agents)
- **Hudson** — reproduce the live stuck turn (post-Chow deadlock).
- **Hicks** — trace the stuck UI affordance.
- **Vasquez** — audit Changsha turn legality.
- **Ripley** — facilitating; will synthesize participant input into decisions + action items.

## Findings
- **Defect 1 (Hudson, read-only repro on `ddc72e1` / `:18080`) — FAIL, deterministic stuck state.** Stale-gameId-reuse / abandoned-seat lifecycle (NOT a rules bug; fresh gameId is clean). Reconnect binds to the persisted game whose seat 0 sits at `AwaitingDiscard` (14 tiles = 11 concealed + 3 meld ⇒ post-claim) but whose human owner is absent; no bot substitutes the vacated seat, take-seat hidden, Deal/Leave disabled ⇒ permanent deadlock. Seam `Autotable/AutotableWsEndpoint.cs` (`viewerSeat=null` when `botCount=3`, no auto-seat/substitution; `TryAutoDealForSpectatorAsync` only for `IsSpectator && botCount==4`). Confirms 2026-07-27 #153. Evidence: `session-files/completion-proof/stuck-turn/hudson/`.
- **Defect 1 (UI affordance / turn signal):** post-claim the human isn't clearly signaled to discard (Hicks trace + Bishop turn signal).
- **Defect 2:** low-contrast setup `<select>` styling (Ferro).
- **Defect 3:** wall-slot mapping yields wrong depletion order/position (Bishop; cf. #152 translator seam).

## Decisions
1. **Server-authoritative seat lifecycle on JOIN** — on joining a game whose current-turn seat is an abandoned human, substitute a bot / offer takeover-reclaim / surface a clear "seat abandoned" affordance; never silently land a viewer in a stalled persisted game. Reconsider persistent-gameId reuse.
2. **Turn signal is a first-class affordance** — unambiguously signal the human's turn to discard (esp. post-claim): backend signal + frontend affordance.
3. **Wall depletion mapping is a backend-translator contract** (wall-slot mapping owned server-side, not frontend).
4. **Setup selects must meet readable contrast** (frontend styling).

## Action items (assigned; in execution)
- **Bishop (backend prod):** fix turn signal + wall mapping — defects 1 & 3.
- **Ferro (frontend):** fix setup select contrast — defect 2.
- **Hicks (frontend):** trace + fix stuck UI affordance — defect 1.
- **Hudson (tester):** owns the real-UI re-validation gate once fixes land (already reproduced the defect).
- **Vasquez (rules):** audit Changsha turn legality to confirm fixes preserve correctness.

## Status
✅ **Design Review concluded** — facilitated by Ripley. Decisions: 4 | Action items: 5. Recorded to `decisions.md` (§2026-08-06 Design Review block) + this log.
✅ **RESOLVED & SHIPPED (2026-08-06T20:43:57)** — all action items landed via **PR #160 (`c7eea9d`) → main `200cad4`**; signed release healthy as `mahjong-proof-200cad4` on :18083; 0 open PRs. See `log/2026-08-06T20-43-57-115-07-00-defect-wave-complete.md` and the `decisions.md` §2026-08-06 fix/review/COMPLETION blocks.
Scribe did **not** commit mutable state and touched **no** production code. Hudson's ceremony finding is preserved in the decisions block; its inbox file is left in place for the active fix agents and will be swept post-ceremony.
