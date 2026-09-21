# Bishop — canonical bot own-win gates

**Source correction implemented; NEW full post-meld acceptance remains blocked by its obsolete pre-fix strategy assertion. Independent review required; qualification0.** September14,2026.

Burke's requested domain declarations were already saved at ChangshaDomain.cs:494/497: LastDrawSeatIndex:int? (null default), DiscardsThisHand:int (0 default), plus BaseUnit:int=1 at373. Domain remains byte-identical SHA f6979f0afe10b4c7c6521b15f2cd74fe903f76ef1a6f1f178b8ad3b081787bff; no duplicate declarations or domain changes were needed.

## Actual source correction
Shape-only bot win gates could choose DeclareWin after Pung/Chow even though the canonical engine correctly rejected absent own-draw entitlement; runtime then logged the rejection rather than making the intended discard. Corrected all11 decision gates in Easy/Medium/Hard/Master to call ChangshaGameStateMachine.CanDeclareSelfDrawWin. Turn-start, self-draw and explainable/unified decision entry points are consistent (Master self-draw already delegates Hard). A denied own win now naturally reaches the SAME strategy's existing Kong/discard/wait branch. No arbitrary runtime discard fallback, tier downgrade, role/hand mutation, ranking change or phase/count-only fallback. No changes to ordinary discard claims or Pass-Hu clearing.

Only4 strategy files changed under src/backend/src/Mahjong.Autotable.Api/Changsha/Bot/:
- EasyStrategy.cs SHA042c9cf69624e1eafc07c18d96eece74d08a7bb50e577c5f79d84c1e0f663da0
- MediumStrategy.cs SHAd1f8e0d54f1cba4fbd7121e0b978d4ce19eedd08a0837b0a698d509e5d8e8657
- HardStrategy.cs SHA3a40c2eadba9ed0936d1b8d9ba40be4ee900ff348db8f158dc81275991747e44
- MasterStrategy.cs SHA9d6ddbde2ef34a08df1d3e438995a042f679134edbe6da9ff4be6c3da7e06302

Burke's engine/scoring/adjudicator/pure scoring helper untouched. No frontend, test, live8950, Docker/root script, staging/commit/branch or agent-spawn operation.

## Actual validation and remaining acceptance
Two targeted executions each ran33 cases:31PASS/2FAIL/0skip. Passing: BotDecisionReasoningTests19/19 (all difficulty identity/decision/reasoning consistency), existing real BotChowAdvancesTests6/6, immutable genuine-draw qualification controls6/6.

Both NEW HudsonBotMeldContinuationTests failures occur at the pre-fix assertion `Assert.Equal(DeclareWin, proposed.Action.Type)`: actual corrected Medium strategy returns Discard. The fixture already establishes real runtime Pung/Chow, conserved108 IDs,11 concealed+meld, null draw entitlement, detector.IsWin=true and canonical.CanDeclareSelfDrawWin=false. It stops BEFORE its final runtime advancement assertions. Thus this proves the decision now differs as intended, NOT full completion of the new continuation case. Hudson was notified to update HIS NEW post-fix assertion/diagnostic and preserve all downstream actual discard/draw/Pass-Hu/inventory assertions; I did not change his test or add a production alias/fallback to satisfy its old expectation. Requested all4-tier genuine-draw/post-meld coverage remains his new test scope. A bounded wait/retry still encountered the same stale assertion.

Evidence root session-files/qualification/2026-09-12/bishop-actions/bot-provenance/:
- review-manifest.json SHA906bf3b5a860e0b940ff0b4d05b458b9145af40d426df93be338aa637ed349cb
- source-delta.patch SHAfffe30ea6b85add7e891d88c12ccef8ce111168d982088905189e0208a43e113
- results/bot-provenance-01.trx SHA4ca3be450acfabfcd938699953a88de0e155d35f1c4e029cc960ef52842017be
- results/bot-provenance-02.trx SHA6a7d29efb06721659db1e3da62bb4015f8e2332aca1c109fb9364a6529429848
- Hudson NEW fixture at capture SHA10f9b705e4a000c56ff30ef13cfe737571c32930145fc5d75b67b8b0e78d332d.

All .NET artifacts/temp/results isolated here; restore only after retained NETSDK1004. No full suite or changed frozen synthetic fixtures. The separate versioned-claim follow-up remains in sessions/2026-09-14-bishop-claim-context.md; prior own-turn/base-unit evidence remains retained. No independent approval or120-match qualification claim.
