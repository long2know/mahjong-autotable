# Drake legacy-NEW retry lifecycle handoff — September 15, 2026

Frozen TEST-ONLY revision; independent review required. Exact-final native execution is PARTIAL, not green. Original rejected author Hudson did not revise, advise, pair or co-author.

Root file: src/backend/tests/Mahjong.Autotable.Api.Tests/RulesQualification/HudsonLegacyNewRetryIntegrationTests.cs
SHA256: 432c59d1b8a7e9ee1d5c0ad49d969ec717d8ebc987281f46010f6431f26da57f

Manifest: session-files/qualification/2026-09-12/drake-legacy-new-retry-revision/lifecycle-boundary/final-manifest.json
SHA256: 419560aeda0018b9a6fdeee1de5c4b5e8b258261795a9a61d24056223bebb457
3509-file seal SHA256: 4f427aa01994d11164a6f52a092456f1a20a796f08526a7875e9fe8f5ade2e17

Causal result on the unchanged captured 809e6d6c Runtime / 97d8232c Instance basis: full legacy row/journal survived old-provider disposal unchanged. Replacement startup then committed HydrateAsync -> CreateRecoveredInstance -> PersistRecoveredReplayAsync -> WriteSnapshotAsync. StateJson/StateVersion and every non-write row field stayed identical; UpdatedUtc matched that observed save; journal retained its full prefix and added exactly SnapshotRoundTrip and BindAuthoritativeGameId with unchanged before/after checkpoints. This was not a shutdown flush or a NEW-induced unrelated-row mutation. The fixture now advances its full-row baseline only after those explicit recovery guards, then requires full row/journal isolation through NEW, ordinary JOIN and continued discard. Real alias/RoomKey/runtime/authoritative snapshot/three-field turn correlation remains mandatory.

Actual non-aggregated history: preserved ed894 original 0P/2F; sealed c452/17d 1P/1F; new read-only lifecycle diagnostic 1P/1F with timestamp assertion retained; d50ac8d5 boundary revision 2P/0F, both complete suffixes; final 432c59d1 1P/1F. The sole d50-to-432 source delta expresses the same exact StateJson equality with Assert.Equal, removing the new xUnit2010 warning. No retry followed the final failure.

Final restartBeforeRetry=true PASSED the guarded boundary, literal lost-confirmation NEW retry, no new runtime/row/redeal, ordinary JOIN, discard25 -> next human draw29/v5/inventory108. Final false row FAILED at unchanged CreateUnaliasedLegacyGameAsync TakeSeat line234 under the original shared five-second setup budget, before lifecycle observer construction or retry suffix. Its cause is not established as environmental or product; suffix remains uncredited for this exact final hash. Earlier d50 same-host success is NOT transferred.

All 71 original non-oracle scenario assertion lines and other original scenario lines/multiline arguments preserved except the three authorized identity-oracle calls and restart-boundary statement. Existing transport/state/inventory/persistence/private-projection helper sections unchanged; seeds/config/cookies/budgets unchanged. 356 input bytes stable per capsule; captured API/content-root binding and exact DLL/PDB/TRX hashes included. No owned DB files remain. Protected shared fixture3953, approved5ec boundary helper and approvedbb ClaimMade oracle unchanged. Prior c452/17d sealed packet and both original runs untouched. No production/recovery/controller changes or advice, moving-root builds, other fixture/frontend/bundle/Git/Docker/live/browser actions. Native in-process execution only; qualification0/120.
