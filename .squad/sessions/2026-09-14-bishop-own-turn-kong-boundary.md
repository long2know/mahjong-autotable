# Pending rule-boundary alignment: own-turn Kong provenance

September14,2026. **No rule decision or production edit made by this record. Independent review/live admission remain blocked as appropriate; qualification0.** This records a concrete issue in Bishop's currently delivered ownTurn adapter, not rejection/revision authorization for any frozen artifact.

## Conflict
`ChangshaOwnTurnActions.Available` currently requires LastDrawSeatIndex==seat for ALL ownTurn actions, in addition to active AwaitingDiscard, no claim window and concealed+3*melds==14. Burke's canonical Kong queries/declaration predicates preserve existing active-turn/held-four/existing-Pung legality. Consequently a post-Pung/Chow Kong can be engine-legal but hidden/rejected by the adapter. F07's established actual-own-draw defect specifically concerns SELF-DRAW Hu; it did not establish an additional Kong prohibition. Spec3.5's discard wording and3.4.2's held-four wording must not be silently resolved by an adapter-only policy.

## Proposed alignment for coordinator/Vasquez disposition
Preserve current canonical engine behavior: retain active/no-window/effective14 readiness for the ownTurn surface; determine Hu via canonical CanDeclareSelfDrawWin (which requires authentic own draw); determine concealed/added Kong choices via canonical engine queries without the additional global LastDrawSeatIndex condition. This would remove the adapter's extra restriction rather than add a new rule. If a post-meld Kong prohibition is intended, require explicit disposition and consistent engine/bot/UI behavior, not just a UI/transport gate.

## Ownership / acceptance implications
- Bishop requested concrete alignment from Vasquez and Burke and informed Hicks/Hudson through direct messages.
- Hicks must not infer this prohibition locally; render only the eventual authoritative capability contract.
- The NEW Hudson test `PungWithoutOwnDraw_PreservesDiscardClaimButCannotDeclareOwnKong` currently encodes the adapter's extra restriction. Its result is not independent normative evidence. Owner must reconcile it after disposition; frozen audit/rare-state fixtures remain unchanged.
- Bot self-Hu canonical gate changes remain correct and separate: Pung/Chow must not fabricate self-draw entitlement. No phase/count-only Hu fallback is authorized.
- Current adapter SHA df0cdcfb42f683d1bceceeaf650805e512b71a6dfa1203095125376d2f15983a, path src/backend/src/Mahjong.Autotable.Api/Changsha/ChangshaOwnTurnActions.cs. Domain declarations, base-unit contract and helper API names are unchanged.

Prior completed source/test handoffs are retained as versioned evidence, not release approval or a resolution of this boundary. No production/test/frontend/live8950/container/key/data/branch/commit operations occurred for this coordination record.


## Current disposition: ratified and implemented
The coordinator subsequently ratified preservation of the permissive canonical Kong baseline, with common normal-entry readiness/actor/candidate gates and actual-own-draw required for SELF-HU only. The adapter's global provenance restriction on Kong was removed; normal runtime and legacy-Hub paths were aligned without tightening pure helpers or editing tests. Current coherent267-case selection passes267/267 with no skips/input drift. See `sessions/2026-09-14-bishop-coherent-backend-final.md`. This earlier unresolved record remains historical; policy ratification is complete, but independent source review, actual candidate/browser/Compose and120-game admission remain separate.
