# Bishop — completed backend action/config integration

**Source implementation complete; final affected-path proof 47/47 PASS, zero skips, no input drift. Independent review and live admission still required. Qualification remains 0.** Work completed September 14, 2026 UTC under the September 12 audit grant. This supersedes the intermediate compilation-blocked handoff; intermediate compiler failures/TRXs remain retained unchanged.

## Implemented boundary
- Ordinary WS `ownTurn` now invokes the dedicated DeclareWinAsync/DeclareKongAsync methods. Discard-window claim Hu/Kong remains separate; never reinterpret stale claim messages or echo raw Changsha commands.
- Under the dedicated runtime lock: revalidate actual cookie-derived seat owner, expected version, and legal current action. Commands copy runtime game identity to prevent cross-room replay when versions coincide. Require AwaitingDiscard, no open claim, LastDrawSeatIndex entitlement, and concealed+3*melds==14; legitimate13/7 pre-draw intermediates remain inactive. Reuse Burke's CanDeclareSelfDrawWin/GetConcealedKongCandidates/GetAddedKongCandidates.
- Per-viewer own-turn availability and Chow partners never enter the shared state store. `things`, `claim`, `ownTurn` are attached from THIS viewer's translation. Nonowners/unavailable seats get ownTurn nulls; runtime->unbound same-socket room switch retracts old actions. Fresh never-bound NEW keeps the existing match-only envelope.
- Zero-wall concealed/added Kong now uses the existing runtime HandleWallExhaustedAsync terminal path.
- Settlement preflight runs the exact engine win+Score on an isolated snapshot under the same lock BEFORE real mutation/WinDeclared. Checked overflow produces explicit `score-overflow` rejection and leaves every state field/version unchanged. No duplicate scoring algorithm, success fallback or partial win.
- Creation BaseUnit defaults1, is latched once and survives snapshots/rotation/hydration. Invalid persisted unit is logged and not hydrated; old JSON defaults1 without fabricated draw entitlement. No preset/cap/house semantics enabled.

## Exact ordinary payload schema
Server only to actual owned human viewer:
`["ownTurn", seatNumber, {gameId:string, stateVersion:int, hu:boolean, concealedKongs:number[][], addedKongs:number[]}]`.
`gameId` is runtime identity, NOT URL relay alias. Concealed choices contain exactly4 physical IDs; added choices each name the fourth held ID. All nonowner/unavailable values are null. Registering the collection ephemeral is supported but privacy/tombstones do not rely on registration.

Client sends one of:
`["ownTurn",seat,{gameId,expectedVersion,action:"hu"}]`
`["ownTurn",seat,{gameId,expectedVersion,action:"concealedKong",tileIds:[a,b,c,d]}]`
`["ownTurn",seat,{gameId,expectedVersion,action:"addedKong",tileIds:[fourth]}]`
Numeric or integer-string seat keys accepted. Wrong actor, malformed choice, missing/future/stale version, stale game and unavailable action reject via existing `actionRejected/current`, followed by corrective viewer snapshot when bound. New reasons include invalid-own-turn-command, stale-game, stale-version, own-turn-not-available, own-turn-actor-rejected and score-overflow.

Claim metadata optionally adds **owner-only** `chowOptions:[[a,b],...]` (two held partner IDs excluding the public discard, one deterministic pair per distinct sequence). Existing `claim {action:"claim",type:"Chow",tileIds:[a,b]}` selects it; omitted tileIds compatibility is retained.

## Base-unit contract
WS creation `?baseUnit=N`: **integer1..11184810**, default1, first creator wins. Invalid/fractional/out-of-range/repeated values close1008. Bound is Burke's unchanged `ChangshaBaseUnit.MaxValue = int.MaxValue/(16*12)`: canonical16 hands ×12 maximum winner units. Checked payment/aggregate arithmetic remains Burke-owned; runtime preflight prevents partial own-turn settlement on corrupted/restored extreme totals.

Runtime CreateGameAsync appends optional `baseUnit=1`; pure CreateGame receives it before first persistence. Translator emits numeric `match.conditions.baseUnit` (no new phaseF wire object). Legacy SignalR CreateGame(ruleSet,botSeatIndexes,seed) keeps THREE arguments/default1. Explicit configuration uses CreateGameWithConfig({ruleSet:"changsha-v1",botSeatIndexes,seed,baseUnit}); unknown ruleSet rejects. Creation response, GameCreated, FullState carry baseUnit. Later valid JOIN/reconnect config cannot mutate a bound multiplier. Domain carriers: BaseUnit:int=1, LastDrawSeatIndex:int?, DiscardsThisHand:int.

## Final changed production SHA256
Relative to `src/backend/src/Mahjong.Autotable.Api/`:
- `Changsha/ChangshaDomain.cs`: f6979f0afe10b4c7c6521b15f2cd74fe903f76ef1a6f1f178b8ad3b081787bff
- `Changsha/ChangshaOwnTurnActions.cs` NEW: df0cdcfb42f683d1bceceeaf650805e512b71a6dfa1203095125376d2f15983a
- `Changsha/ChangshaCreateGameOptions.cs` NEW: c8e8d23daa7bb49df1988523c9033e35cecd1e6261e8a2bbbdc8567cc084a903
- `Changsha/Runtime/ChangshaGameRuntime.cs`: 3766ec17b93f8dd8f2eff2534465c510659aca8837d5c32dd84162c2d0e83a9e
- `Changsha/ChangshaHub.cs`: 7065c9dbccf043d5012fb94c01f4250ebae71ac1c129a5ccc4bdb3b1c615a1c4
- `Autotable/AutotableProtocol.cs`: 23d57d4e33c0505357efa9760c347f693b94f1d58633f7c101cf3f27b6e68048
- `Autotable/AutotableWsEndpoint.cs`: 889f4d81d89a1519a0d1511fe0afe027e38907987709e89cd3fa7afb3b26cfb1
- `Autotable/ChangshaToAutotableTranslator.cs`: 711fd631788af4201e5ab7681524e0d7abfb079563c2975d450ea5f59efd83aa

No Bishop edits to Burke's state machine/scoring/adjudicator/base-unit helper, Hudson's new tests, frozen audit/E2E fixtures, frontend, Docker/root build scripts, or live8950/key/data. No staging/commit/push/branch change or added agents.

## Actual proof and evidence
Root: `session-files/qualification/2026-09-12/bishop-actions/`.

1. Initial existing scope98 executed:97PASS/1FAIL/0skip. Fresh-NEW envelope-count failure corrected in source; it passes in subsequent runs. All inherited authorization controls passed.
2. After Hudson corrected his NEW fixture's ClaimType assertion, combined scope **175 executed:173PASS/2FAIL/0skip**. Own-turn integration45/45, creation/config/scaling/restart29/31, existing99/99. The two failures were the real own-turn settlement overflow/partial-mutation gap, subsequently fixed by runtime preflight and explicit rejection.
3. **Final changed-path scope47 executed:47PASS/0FAIL/0skip**, including both former overflow failures,8 authenticated real-draw WS Hu cases,3 base-unit payment scales,26 protected auth/cross-room tests,6 immutable real-draw controls, fresh NEW and legacy SignalR reconnect. Source/test inputs stayed byte-identical during this run. All175 distinct cases' latest outcomes are passing across versioned runs; **do not present this as175 re-executed after the final two-file fix**.

Positive real-WS cases in the combined run include all seats' self-draw Hu, meld-adjusted11/8 hands, exact concealed/added Kong replacement, Hu-only rob window, private explicit Chow choice, owner reconnect, and creator unit latching/restart/legacy RPC. Negatives include pre-draw13/7, no-own-draw Pung, stale/forged/other-room commands, invalid physical choices, spectators and private metadata across room switches. Tests use actual signed identity and ASP.NET WS processing, not mock-only method assertions. No browser/mass-game credit.

Final evidence:
- `final-review-manifest.json` SHA **cf56c6cceb3bd2562fc672624e799e44d753e2e72ad7f3771eb558954fb1107d** (source/dependency/test hashes, all case outcomes, pinned inputs, protected-method checks).
- `source-delta-final.patch` SHA f3842c27a9b44e2b4c981b4f4aade0bd0ecdd431f5cb1f9a0018a848fa8c64ce (against preserved pre-grant dirty inputs).
- `results/actions-final-focused-02.trx` SHA f07d301430e0ef577fc9056e0139b7a743b3b97a688c095589bfe68685963d3f (173/2).
- `results/actions-settlement-final-01.trx` SHA 5082bc2122a00e27bc8252b9b80f378d80cbfb249f4521ee37cb6e5b19755650 (47/47).
- `actions-settlement-final-01-inputs.json` SHA a677b8a81aebbfcbee45420a50ec3895f645f05badc0bec3d7cbd5af9286fe9a.
- Tested API DLL SHA ff79a0b9c4a2641e0302e3c626d8823f22d3831c43d9a5c02c7271131d8637cb.

Hudson test source hashes: OwnTurnWsIntegrationTests=db3276e046dcad57bfd059e1c90ddcaaa85691b7c8dbbd62384a639c11f3f980; OwnTurnWsFixture=39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca; BaseUnitIntegrationTests=9a69cb8d26215dd9b4c85608c18cd11ad48ab3639636dde1b0d1bbc06ea6ba48. Protected seat-auth test remains a3ad5f00cff2f9bd913011170193457baf881f7b472debaf6da98e3f268381c3; frozen own-draw audit remains dc5401e73240727bb95b6bed1efc8a39feb68de4caa3c9649f3d430395f04a5e. Critical auth/destination-room methods are byte-identical to pre-grant source; occupied-seat handler differs only by BaseUnit creation arguments/wrapping. Relay echo fix retained.

All commands used the existing test project with isolated --artifacts-path/results/temp/CLI-home. Restore occurred only after explicit retained NETSDK1004. No zero-TRX or exit0-no-tests pass, test exclusions, provider/all-suite runs. **Remaining gate:** independent exact-source review and newly admitted live UI/qualification execution, not another source/compiler blocker. No frontend rebuild or live8950 access occurred;120-match qualification remains0.
