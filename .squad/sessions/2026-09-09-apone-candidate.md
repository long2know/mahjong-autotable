# CURRENT — final all-three-fixes image running; source/runtime proof complete

**Actual final URL:** `http://127.0.0.1:18209/autotable/`
**Authority:** `sessions/2026-09-09-final-live-pin.md`. Coordinator built/launched the final image after reclaiming execution. Apone independently verified it; no duplicate build/launch is needed or authorized during acceptance.

## Exact final runtime
- Container `mahjong-20260909-e2fb89d1-final`, ID `471cf533755bfc442486a7fea5b72c470ec18fb2ec4f2135df2cd15df7096ee0`.
- Image `sha256:d65686a47040e7641775a6c28e185ccd13c3762e4bda877798ce18a89ff424a1`.
- Started September9,2026 at19:17:36 PDT (September10 at02:17:36 UTC).
- DLL `8e3488aa9eac0203d1980919f74b12d174840002f1259e1ce18265c28ef62437`.
- Entry `autotable-src.5ad09744.js`, HTTP SHA256 `29ec3e58429cccc74adde75b37db065d66b4c49a8ff77770e54b55e26558e998`.
- Fresh Apone readback again PASS: Docker healthy, HTTP health/SQLite connected, HTTP index/entry/precache identity, all138 backend +84 frontend packaged files, Production/nonroot/loopback binding. No gameplay operation or browser test by Apone.

## Complete current source/test proof
Latest evidence root `session-files/completion-proof/2026-09-09/apone/`:
- **`final-complete-inputs-1943.json` SHA256 `d47898f3e5cb20f68ba91bc77c44c2b0327d73d4f3bae51705728c27323e6734`**.
- `final-handback-1943-provenance.json` records all scoped source/test hashes and current non-state dirty-diff fingerprint `3f28209ee7fb6be853b73e1fedd84041b15d8bb4da766c08b30db1568043d5bc` (mutable Squad state/evidence excluded).
- All342 backend inputs match approved published-source fingerprint `4dcf45670c20e27f7eb215b76a0834734eb5a6102941dc327d65099b3373e711`.
- All204 frontend inputs were compared against frozen I01 inputs: the ONLY difference is the approved GameUi hydration patch4de6d768…. Final frontend fingerprint `7cf2d9a29de7a2797ae666857eb4fab175dd5b329d9f51405ec2418ba39a76bf`.
- Current913 test/helper files fingerprint `d9b52e39a55fd7f45b9dc0b4476f8ce4d43701667e1b46b082fcf264ede246fa`. Approved UI regression8673cbee…, renderer regression7e2c0b28…, S11cd8a4470… and other named source gates match.
- `final-readback-1943/` retains independent actual final identity/health/all222 file hashes and checked-evidence file; earlier `final-readback-1929/` remains unchanged.
- Git-tracked bundle inventory includes deleted old chunk names; actual packaged/copied frontend count is84 files, not that index-oriented inventory count.

## Actual acceptance results remain scoped to owners
- Coordinator FINAL authority/privacy replay inspected by Apone:17/17 control cases,108 explicit rejections within brute control; cross-room14→0 exposed IDs, no forged seat, attacker mutation rejected, valid owner succeeds. Reference `sessions/2026-09-09-final-authority-proof.md`; Apone cross-reference `final-authority-evidence-crossref-1934.json` SHA25625b61e64….
- Dietrich FINAL renderer lane reports27PASS/4existing mobile skips/0fail/flaky (19browser+8class/source). Real15discards/9pickups/bothviews/hand2 action and1925 physical inventories without representation violations. Final manifestb41eb65f…/lifecycle3395a021… hashes independently checked by Apone. This is partial lifecycle/renderer scope, NOT full GameComplete.
- Coordinator explicitly granted **Ferro** the next FINAL UI browser window after Dietrich closed/released. Hudson follows Ferro's actual release. Apone grants no slots and runs no competing browser/whole-suite jobs.
- Existing18:30 PDT264-pass rules run linked to final342 backend inputs via `final-rules-source-link.json`; no rerun or served-DLL execution claim. Approved docs and S11 source gates remain closed; original C01/interim results are not added/transferred.

## Boundaries / remaining proof
All THREE production source fixes are already independently approved and included. No source-review or assembly HOLD remains. Final source freeze: coordinator/c02-final-source.json; exact review keys for Frost backend, Ripley renderer and Ripley late-UI are authoritative. C01 and C02-I01 stay untouched RED/interim; old queued START/readiness/HOLD messages do not reopen their windows.

This is a REAL isolated local Production single image on a pinned cached runtime, NOT canonical clean/signed/published release packaging. Root Dockerfile failure is diagnosed local `_apt` chmod EPERM on vfs/fuseblk; all four signatures validate, no trust/sandbox bypass or host changes. Final `verify:bundle-sync` exit1 reflects intentionally uncommitted Git-baseline chunk drift and stays reported RED; actual final84-file parity is verified separately. Existing broader lint debt remains disclosed/unwaived, no unrelated cleanup.

Full human-match/UI/caps/claims/manual/S11/provider/persistence/visual and independent final integration outcomes require their actual final evidence; no F1–F9 or release/game-completion assertion is manufactured here. No source/dist/Git/registry/daemon/prune/restart action by Apone during latest verification. Prepared backend and all original provenance remain intact. Stage history: `sessions/2026-09-09-apone-c02.md`; canonical diagnosis: `sessions/2026-09-09-apone-canonical-diagnosis.md`.
