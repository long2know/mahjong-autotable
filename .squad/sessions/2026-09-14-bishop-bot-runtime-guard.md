# Bishop — runtime canonical win-proposal guard

September14,2026. **Requested runtime defense implemented; focused existing compatibility35/35 PASS, zero skips. Dedicated invalid-proposal/new post-meld acceptance remains assigned to Hudson. Independent review required; qualification0.**

## Change
Inside RunBotTurnAsync's existing per-game lock, a captured DeclareWin proposal now calls canonical CanDeclareSelfDrawWin. If false, runtime substitutes the SAME deterministic ChangshaBotPolicy.SelectDiscardTile fallback already used for decision timeouts. The replacement BotDecision records the actual Discard/tile/score in LastBotDecisions, appends `runtime: self-draw unavailable; deterministic discard fallback`, and logs an explicit warning. It then follows the normal DiscardAsync path rather than throwing/catching and stalling. No LastDrawSeatIndex, Pass-Hu, hand, role or phase mutation is used to manufacture a win. Legal win proposals and other action kinds are unchanged.

The prior11 canonical win-gate changes in4 stock strategies remain: normal stock policy naturally chooses its own strategy's discard after no-draw Pung/Chow; this runtime defense also handles faulty/custom winning proposals. No Burke engine/scoring/adjudicator/helper, domain, frontend or test edits for this delta. The separate post-meld Kong policy boundary remains pending coordinator/Vasquez disposition and was not changed here.

## Exact source / evidence
Changed file: src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs
- New SHA5491877bc9138a0ddd860f5072ae38506d35dc236853ffcdeb962a068a241dfe
- Input SHAb600cf06d9197c3d55acee1ccdd419df5d6acef77a9c518f40ebd4209f371f65 (includes retained additive claim-context work).

Evidence root: session-files/qualification/2026-09-12/bishop-actions/bot-runtime-guard/
- review-manifest.json SHA3d2597b4b0cefb14a734beb5f8c5afaf6ae909595cf7c31e24bd36a8efc8e7a9
- source-delta.patch SHAa9e64110dae69a6a19d8acc11768e39e2dbfb50efde8d2a75a10c4ec420ce756
- results/bot-runtime-guard-01.trx SHAf3a23a52ec0217e34811b23834565bd3ebfa059a059ba2f40cff225e4b12d240

Actual35 cases passed:4 existing runtime timeout/normal-decision controls,19 reasoning/strategy consistency controls,6 real bot-Chow continuation controls,6 immutable genuine-draw qualification controls. This is not a claim that the newly added invalid-winning-proposal branch has a dedicated executed discriminator. Hudson was asked for that NEW controlled-strategy/real-meld regression plus the stock-policy continuation assertion correction, preserving downstream discard/draw/108-inventory/Pass-Hu assertions and all frozen fixtures.

All build/obj/results/temp/CLI-home isolated here; restore only after retained NETSDK1004. No no-test exit0 pass, full suite, browser, live8950/key/data/container, Docker/root script, staging/commit/push/branch or new-agent operation. Prior source hashes do not cover this new delta; source review/live/cohort gates remain unchanged.
