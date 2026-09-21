# Bishop — current-source replay of reported own-turn overflow disconnect

September14,2026. **5 executed/5 PASS/0skip, no input drift. No production or test changes in this replay.** Independent approval/live qualification remains separate; credit0.

Hudson targeted-04's disconnect is valid PRE-FIX evidence: compiled-start Runtime b8ece9de42c837e13652793674f78fa3cef9c8da7a42458e849bbdb11e615c6c and Endpoint af76306cc7f3b24e651e6625b992e89ac7cedae14ba0ad89c94fbfe80c37e9f8 predate the recorded atomic win+Score preflight and OverflowException handler. Its logs/RED were read and retained, not relabeled.

Current runtime still preflights exact DeclareSelfDrawWin+Score on a private snapshot under the same lock before real mutation/WinDeclared. Current endpoint catches OverflowException, sends actionRejected/current {action:'hu',reason:'score-overflow',requestedSeat:0,ownedSeat:0}, then a corrective full snapshot. Both winner/payer finite zero-sum overflow cases pass with whole-state/version unchanged and a usable socket;3 positive real Hu scaling controls at units1/7/11184810 pass.

Current tested inputs:
- Runtime45d043e778a0145d687a2895b42e30420687cf21fdde26fa63cce040be3033a0
- Endpoint8cd5b64bbb64c39c5239a9b6bb5fd1323c809b00d58e261e0c20af7394161792
- HudsonBaseUnitIntegrationTests.cs8d681f6be0d2501f7e05c952a0d2152d290d867898d9fcf7fbb592bfcd9e141b (current owner version, NOT the9a69cb8d version in targeted-04)
- HudsonOwnTurnWsFixture.cs39531682d5967dce8014c560d95cc52b637f47e74134ac18313055fe036aa4ca.

Evidence: session-files/qualification/2026-09-12/bishop-actions/overflow-replay-current/
- results/overflow-current.trx SHAad63ec9f2b235a5803539236db067fb2f12bd52a39e57629a392883871fa9506
- summary.json SHAf1aa1fded03004c0c3091b5b40dcbf85d1294acc0ece946ec21a3954d0e81390
- inputs.json pins all4 inputs; actual comparison found no drift.

Only the two OwnTurnSettlementOverflow and three AuthenticatedWsSelfHu cases ran. Build/obj/results/temp/CLI-home were separately isolated; restore followed an actual retained NETSDK1004. No full rerun, exclusions, test weakening, live8950/browser/container/key/data/frontend/Git operation, or counted-match claim.
