# Bishop — checked settlement before discard/rob-Kong Hu mutation

September14,2026. **Extension implemented;52 existing focused cases PASS,0skip. NEW dedicated claim-Hu overflow discriminators remain assigned to Hudson; independent review/live admission required, qualification0.** No assertion of those new cases executing is made here.

## Change
The same canonical preflight principle used by own-turn Hu now protects discard/robbing Hu:
- Public ClaimAsync preflights the existing ResolveClaim+Score operations on a private CopyState under lock, after offered-type/actor/version validation and BEFORE queuing Hu. Arithmetic failure propagates without a live tile transfer, win, score, phase/version/event mutation or timer cancellation.
- Bot Hu proposals are checked before queuing; numeric overflow is logged/rejected and the existing window timeout remains active.
- Pending claims are revalidated before final timer cancellation/commit. Unoffered claims and unrepresentable Hu responses are removed/logged, not scored. Timeout performs the same cleanup before its normal missing-response passes, preserving a usable lifecycle.
- WS claim handler narrowly catches OverflowException and sends existing actionRejected/current with action:'claim' (or pass for that handler), reason:'score-overflow', requestedSeat/ownedSeat, followed by corrective full state. No raw exception/credentials or new receipt DTO.

No duplicated scoring algorithm, cap/clamp, automatic false-Hu fee, catch/rollback, Pass-Hu policy, alias-restoration claim, engine/domain/test/frontend edit. Own-turn preflight remains intact. F04 continues to call public SelectChowTiles directly before PendingClaims.

## Current source SHA256
- src/backend/src/Mahjong.Autotable.Api/Changsha/Runtime/ChangshaGameRuntime.cs:96300e95a417e231aa34faed9fa84163f4a573ba9ea56dfea2e1e1186da7eff5
- src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs:624222c8e56bd40b4cd70604828ab45eff3bd80ccef3b39fb4a158bb97a51f95

Evidence root session-files/qualification/2026-09-12/bishop-actions/claim-settlement/:
- review-manifest.json SHAeef471f948291c05654b237fda7906fa4ec839ad9a3e873b40b02c827dacdf03
- source-delta.patch SHA75da1ff0620b14e1293ce6fd67a2b62a5f3e5fe09780c889eed35c0bcd9329f3
- results/claim-settlement-compatibility.trx SHAb8d3c3976c2be83ae85b0709331adbac2c80462c075f90d7093c3a2af9628dde

Actual52PASS:26 protected auth cases,10 bounded runtime partials (including zero-wall/one-last-replacement and bot offered-claim validation),9 human claim-wire controls,5 own-turn overflow/positive scaling controls,1 human Hu finalization,1 deferred invalid-Chow/correct-retry. These are not a substitute for new explicit discard/rob-Kong overflow tests. Hudson was given the exact whole-state/version/tile-transfer/socket/timeout requirements for NEW RulesQualification-only cases.

All artifacts/obj/results/temp/CLI-home isolated; restore only after recorded NETSDK1004. No test exclusions/weakening, full suite, live8950/browser/container/key/data/root-Docker/build-script/Git operation or new agent. Prior handoffs/evidence remain versioned and retained. Separate Kong eligibility alignment and other pending NEW tests are not resolved by this patch; no counted-match or independent approval claim.
