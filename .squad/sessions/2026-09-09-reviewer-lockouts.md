# Exact artifact reviewer state — September 9, 2026

## S11: current rejection; independent revision required

Frost's independent source review REJECTS `src/frontend/autotable-src/tests/e2e/changsha-manual-pickup-endpoint-only.spec.ts` S11 at SHA256 `b08174d8a22aaa18cf1100f6d29e12bf5457a3f800fc778427d56b13d17627a2` (HEAD `b38e1a943e8804020c7887af3920fba257d1a427`). Current author Ripley is locked out of producing or advising the next revision. Drake is the required independent revision owner. Frost must re-review; Hudson executes the approved frozen bytes.

Findings: `ownBatchesPressed` increments without asserting a successful `takePickup()` action; source-level negative control passed with zero pickup clicks. The post-ceremony `realDragWallTile()` result is not required to have a non-null target; a missing wall candidate likewise passed without any wall pointer action. These are reproduced non-vacuity defects in the gate, not yet evidence of a product auto-take bug. Correct the actual-action preconditions without loosening timeouts, retries, skips, stable same-hand tombstone requirements or backend authority.

## Claim-key: current independent source approval

Frost APPROVES `src/frontend/autotable-src/tests/e2e/claim-key-collision.spec.ts` at SHA256 `6d3db9f335fe7b563ecff0b8081cf4fc9d050c03c9f483a45def50f3d3a68564` for source correctness. Historical exact-hash re-review identifies Dietrich as that revision's author. This is not browser execution proof and does not transfer to any other hash. Hicks remains excluded from the historical rejected claim-key revision cycle unless explicit later clearance is recovered.

Preserve all unrelated artifacts. Prior unknown lockouts are not invented failures; a specific rejected-artifact change still requires its exact author history. Current protected endpoint/test files match the existing PR #163 correction and remain untouched.

## S11 independent revision approved for browser execution

Drake produced the S11-only correction (+36/-10) at SHA256 `cd8a447006d670b3fdf72be42fe3dfc980af8c005f7547a3677152ea8b6ef239`. Frost independently re-reviewed that exact hash and issued **APPROVE — source gate only**, with no significant issues. Both old false-pass negative controls now reject, and legitimate controls remain passing. The original author did not revise or advise the correction. The source rejection is resolved by this independent approved revision; Hudson must still execute the frozen exact bytes on the current candidate before product acceptance can be credited. No image rebuild is required for this test-only change.


## 2026-09-09T16:32:32-07:00 — C01 confirmed cross-room privacy blocker; independent revision ownership

Coordinator's actual frozen-script execution CONFIRMS a same-socket cross-room confidentiality defect on C01:14 private tile IDs exposed and a forged neutral seat entry, while attacker mutation remains rejected. This supersedes the prior unexecuted hypothesis; it is an execution RED, not a new Frost source-review verdict. Existing auth/identity/relay positives and108 explicit spectator rejections do not clear F7.

Baseline endpoint SHA256 `df34a10bcb82853cdb0d7328cd581f0252b219d141054481d5a526033875bb99`; C01 image `113c10b315f41483bdd0e1b0879ffdee08399e4f8b2dd91fc37671284ba10461`, DLL `836821f3819d881fc1ee7bc1a480f98b16adf32e7b37e21449b70d04f1b4cec5`. Preserve this immutable RED baseline and inherited PR163 correction.

Coordinator explicitly assigns **independent Burke**, agent `403154cf-209d-4cd8-9b65-c07f661742b9`, the narrow endpoint viewer-context fix and focused regression. Bishop remains excluded from the historical rejected endpoint revision; no blanket re-admission or original-author advice is authorized. Burke implementation is IN PROGRESS with no final hash recorded here. **Frost** independently reviews the resulting exact version; **Apone** then assembles NEW C02; changed-path authority/security plus real-game acceptance must follow. All those downstream results remain PENDING. S11/claim-key source approvals above remain artifact-specific and do not approve this production change or F7.

Source: `sessions/2026-09-09-cross-room-privacy-blocker.md` and coordinator's16:32:32-07:00 update. Redacted evidence: `session-files/completion-proof/2026-09-09/spunkmeyer/c01-coordinator-crossroom-01/cross-room-viewer.json`. No sensitive values are copied into state.


## 2026-09-09T16:41:35-07:00 — exact documentation review APPROVED; unrelated gates unchanged

Independent **Ripley** (code-review sync; gpt-6-astra/max/long_context) issued FINAL APPROVE with no significant issues for **Vasquez's two documentation diffs only**, against HEAD `b38e1a943e8804020c7887af3920fba257d1a427`:
- `docs/changsha-wall-perimeter-mapping-contract.md`: `dc0212bd6791e9b1d220c7ca401e176021b41fe4d4dddb279d3e28601f68dc9e`.
- `docs/rules/changsha-spec.md`: `944ee5f567258ae914012bb7858320d4aec1ca125464daa69efa8796e82f1ebb`.

Source `sessions/2026-09-09-ripley-doc-review.md` is coordinator persistence of a response-only verdict; Ripley did not write that state key. This closes the exact doc-review item, not S11, endpoint, browser, packaging, F7 or game/release completion. No unrelated author lockout is cleared. C01 remains immutable RED; Burke correction/Frost exact review/C02/live acceptance remain pending.

## Additional entry/seat revision1 rejection — September9 22:07PDT review

Ripley SOURCE-REJECTED Hicks's five-file entry/seat artifact (client-ui.ts4aad08…, game-ui.tsa7a188…, lobby.ts14e287…, index.tsd6486…, NEWentry-seat spec9dd8c9…) for confirmed loss of the discard/cursor cue in deliberately offline relay play. Actual before/current execution discriminated the regression. Exact disposition: sessions/2026-09-09-ripley-entry-review-r1.md.

Ferro owns the independent revision as the reviewer required. Hicks may not author, advise or pair on any of this rejected artifact's next version. This is in addition to, not a reversal of, his previous claim-key lockout. Ripley's S11 lockout and all previously recorded dispositions remain intact. Bishop's separately approved relay-origin backend artifact is not rejected and remains frozen.
