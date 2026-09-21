# Final all-three-fixes candidate IS NOW RUNNING

Captured September 10, 2026 UTC (evening September 9 PDT). This supersedes all prior C01/C02-I01 readiness and pending-source-approval messages. Coordinator reclaimed execution and built/launched the final image from approved source and the final freshly regenerated frontend.

- URL: http://127.0.0.1:18209/autotable/
- WebSocket: ws://127.0.0.1:18209/autotable/ws
- Container: mahjong-20260909-e2fb89d1-final
- Container ID: 471cf533755bfc442486a7fea5b72c470ec18fb2ec4f2135df2cd15df7096ee0
- Image: sha256:d65686a47040e7641775a6c28e185ccd13c3762e4bda877798ce18a89ff424a1
- DLL SHA256: 8e3488aa9eac0203d1980919f74b12d174840002f1259e1ce18265c28ef62437
- Served entry: autotable-src.5ad09744.js
- Served entry SHA256: 29ec3e58429cccc74adde75b37db065d66b4c49a8ff77770e54b55e26558e998
- Status: running; Docker healthy; /health DB connected; /autotable/ HTTP200.
- Existing identity verifier confirmed all 138 backend +84 frontend packaged files and HTTP entry/index/precache bytes against sealed build outputs.
- All 342 approved backend input files were unchanged. GameUi4de6d…, Worldfc4d…, ThingGroupb323…, UI regression8673…, renderer regression7e2c…, S11cd8a… matched approved source hashes.

Evidence: session-files/completion-proof/2026-09-09/coordinator/c02-final-{source,local-artifacts,launch,identity}.json and c02-final-first-live-proof-{identity.json,evidence.sha256,container-files.sha256,health.json}.

Read-only pin verification command (unique label and lane-owned output directory required):
`python3 session-files/completion-proof/2026-09-09/coordinator/verify-live-candidate.py c02-final <unique-label> <lane-output>`

Packaging: real isolated single Production image atop exact pinned cached runtime; not clean canonical/signed/published release. Startup's first HTTP request reset before readiness; subsequent healthy checks and independent full-byte verifier succeeded. This is not a gameplay pass yet.

Execution queue: Dietrich final affected renderer window -> Ferro actual late-hydration/UI/mobile window -> Hudson final human-match acceptance. Coordinator runs frozen backend replays separately. Keep final/C01/I01 containers immutable; nobody rebuilds dist or starts another image while acceptance runs. No more interim/readiness announcements; only actual results, new blockers or slot transfer.

## Working-tree bundle guard disposition

The existing `npm run verify:bundle-sync` command exited1 because its implementation compares generated dist files with the Git baseline/index and reports the intentionally uncommitted changed/new chunks. It did NOT perform another build (that requires --build). This is expected pre-commit bundle drift, not an assertion that the running image uses old source. No staging/commit was performed to make the guard green. The final source was built successfully; all84 copied frontend files and all138 backend files match their sealed outputs in the healthy final container. The scoped source/docs diff whitespace check was clean. Preserve this distinction in final integration disposition; do not report the Git-baseline guard as a pass.

## Final renderer window COMPLETE; Ferro granted browser slot

Dietrich's actual final-pin manifest is `session-files/completion-proof/2026-09-09/dietrich/final-r1/manifest.json`, SHA256 b41eb65f2e91cb470ea88a021c4f652660a115ecdb9f753c650cc956052a1d76 (independently checked by coordinator). Same final image/dll/entry at START and END.

-27PASS:19 served-browser cases +8 actual-class/source cases; four established mobile view-mode skips, no failures/flakes, workers1/retries0.
-15 verified genuine discards,9pickups, both views, actual hand2discard.
-1925physical samples with0hidden-visible/missing/double-mesh violations.
-All relevant approved source inputs unchanged; browsers closed and slot released.

This closes the scoped final-image renderer gate, not full human-match acceptance. Coordinator explicitly granted Ferro next execution of late-UI/mobile/entry/manual focused window. Hudson remains the subsequent sole heavy browser owner after Ferro releases. Old C01/I01 readiness broadcasts are superseded; no new image/build is necessary.
