# Bishop — two bounded runtime partials corrected

September14,2026. **Current-source RED10:8PASS/2FAIL -> GREEN53:53PASS/0FAIL/0skip, no input drift. Independent source review required; qualification0.** Auditor's unchanged10-case fixture executed in both runs; this is not a whole-game release approval.

## Corrections (runtime file only)
1. Accepted exposed Kong with no replacement now dispatches Phase==WallExhausted through the existing terminal-draw handler instead of emitting/scheduling an impossible discard turn. The replacement event is emitted only when Kong resolution actually reaches AwaitingDiscard, so an old held tile is not reported as a replacement. The one-available-back-tile control remains playable at AwaitingDiscard even though Wall.Count becomes0; no wall-count shortcut.
2. Added shared IsOfferedClaim seat/type validation used by human submission, bot proposal processing and queued-response revalidation before commit/scoring. An unoffered bot Hu remains recorded as a proposal for audit but is logged/rejected before entering PendingClaims; the existing claim timer remains active. Invalid queued responses are removed/logged before cancellation/commit, and timeout cleanup then auto-passes unanswered eligible seats normally. Valid bot Pung and ordinary human rejection/pass behavior are preserved.

No automatic false-Hu fee, score-tier/Pass-Hu/preset/house-rule change, role spoofing or broad catch. No engine, domain, frontend or test edits. Existing self-draw runtime/strategy guards and versioned claim context are retained. Post-meld Kong-rule alignment remains separate and was not decided by this patch.

## Source/evidence
Changed: src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs
- New SHA45d043e778a0145d687a2895b42e30420687cf21fdde26fa63cce040be3033a0
- Before SHA5491877bc9138a0ddd860f5072ae38506d35dc236853ffcdeb962a068a241dfe.

Evidence root session-files/qualification/2026-09-12/bishop-actions/runtime-partials-fix/:
- review-manifest.json SHA2c95d131855e1f21d594f9ba019102e79b6bc963aaf69afcbc847559f7b4bc11
- source-delta.patch SHAe3407508a412bd86182d6cc747b0505d80d8bc7529d9cc91e3b33cdaedd5fb89
- results/runtime-partials-red.trx SHA01d0d7bfdfabc081b746f7bd24db3b4a823437caf0ed9959834728e1e5758714:10 executed/8PASS/2FAIL/0skip on actual pre-fix current source.
- results/runtime-partials-green.trx SHAa0461b80e4f01b30cdbc0a01c5a8ba105b98f7d9b698eb3c8b585e78b54cf0ec:53 executed/53PASS/0skip.
- Auditor fixture RuntimeRulePartialsQualificationTests.cs unchanged SHAd684761ec21d8623c77b3b491a420dd7a4a93ed7d0e6a07ddfb7f363823e9997.

GREEN scope:10 auditor cases (both former failures, zero-wall concealed/added/rob-pass, playable last replacement, valid/invalid bot claim, human rejected Hu and unchanged false-Hu controls);26 protected seat authorization cases;9 human claim-wire controls;6 bot Chow continuation;1 human Hu finalization;1 staged invalid-Chow/correct-retry control. Stable source/test inputs pinned in green-inputs.json. All artifacts/obj/results/temp/CLI-home isolated; restore only after retained NETSDK1004; no exclusions or zero-test pass.

No browser/live8950/container/key/data/root-Docker/build-script operation, full suite, Git mutation or extra agent. Previous manifests/failures and baseline adjudication remain retained. Current runtime hash supersedes the prior runtime-only guard pin for future source review; it does not retroactively relabel older evidence. 120-match/cohort/live admission gates remain unchanged.
