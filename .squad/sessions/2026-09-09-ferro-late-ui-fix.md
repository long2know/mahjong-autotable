# IMPLEMENTED — late GameUi authoritative HUD hydration

**Author:** Ferro. **Status:** REVIEWER-READY; fixed-image browser GREEN remains pending Apone's independently reviewed C02. Exact source/test freeze unchanged through latest verification, September 9, 2026, 18:59 PDT. All Ferro browsers closed.

## Authorized implementation

- `src/frontend/autotable-src/src/game-ui.ts`: two added lines only, after Roll/Take handlers and the pickup update subscription are initialized in `setupPickupHud()`: an explanatory comment and `this.onPickupUpdate();`. All `this.elements` and class fields exist before this constructor call. Existing current-state rendering reads authoritative pickup, turn phase, dealer and confirmed seat; null state uses the same tombstone teardown. No extra subscription, timer, gameplay command, roll/deal/take, URL/default/preset change, catch broadening or unrelated cleanup.
- NEW `src/frontend/autotable-src/tests/e2e/changsha-late-ui-controls.spec.ts`: three cases per configured project (Chromium actual clicks; mobile-chrome actual taps): (1) normal loading retains Roll and exact first human pickup, (2) original lazy module arrives after real RollingDice/seat0/dealer0, then real Roll advances phase, (3) same-owner same-room reload with an existing unconsumed public pickup frontier before UI arrives hydrates Take without resurrecting Roll. Delayed network route calls `continue()` on original bytes; no DOM/control/game-state injection. Service workers blocked only so this original network delivery is observable. Requires pre-UI authority and `uiInstalled=false`, post-release real UI installation, no auto-take, exactly one `{seatIndex:0,count:4}` frame, and exactly four server-owned tiles.

All inherited GameUi bytes preserved. Existing specs/helpers/config/packages/S11/backend/renderer/dist untouched by Ferro. No build/install/Git staging/commit/switch/restart/publish/deploy.

## Exact fingerprints

| Artifact | SHA256 |
|---|---|
| **Before GameUi** (HEAD capture rechecked, not a checkout/reset) | `50d25b18b7edbb60067e1fcbff6bce27b7fa5ba7f56aadba5219766b07d9cb68` |
| **Final GameUi** | `4de6d7682e7083132cd03f025e5425fa3d10becdac7efa9aa8e8583459f37583` |
| **Final NEW regression** | `8673cbee03e13f37fb474b57fe29523659aa02b18847849599040b2f4d3dc140` |
| Exact review-copy/diff manifest | `cdd85bb637c680462f01938e86a22a46ac12677350d67cf7d874e3cff1025f57` |
| Latest no-new-lint/mobile-RED supplement | `a1787b514b8d888c56c3a622e737a26ce889a953087385bf5e7b5517217601e6` |

Evidence root `session-files/completion-proof/2026-09-09/ferro/`. Reviewer copies and minimal diff: `ui-hydration-review-freeze-r1/`. Exact before-source copy, baseline/current ESLint JSON, strict output and latest hashes: `implementation-verification-02/`. Nineteen other original focused UI/spec/helper/config/package inputs rechecked byte-identical; only granted GameUi changed among that earlier inventory.

## Actual tests and discrimination

**PRE-FIX C01 remains unchanged and RED after editing source — no false deployed-green claim.** Original diagnostic before source edit (`c01-late-ui-roll-01/`, 17:24:18–17:24:30 PDT): normal original module order exposes real Roll and accepted click; delayed original module leaves it hidden after UI installation. Source remained baseline `50d25b18…` for this run.

After source edit, NEW exact canonical regression was run against still-immutable pre-fix C01:
- Chromium, 18:35:33–18:36:17 PDT,44.50s: **1 PASS (normal controls),2 RED (hidden late Roll, hidden late cached pickup HUD),0 skip/0 flaky/retries0**. `c01-late-ui-regression-red-01/results.json`, SHA256 `78ea9436c409d9c15be83bdf4ebee09ec6ba208f1fb4b7e9d220e10e4b78abb6`.
- Mobile-chrome,18:58:57–18:59:19 PDT,21.77s: **1 PASS (real touch Roll+take),2 RED (same late Roll/HUD defects),0 skip/0 flaky/retries0**. `c01-late-ui-mobile-red-01/results.json`, SHA256 `3519bc6ee0d7ffe909f5ac44c725e3fe2e6e5850e8e2b0866be0c4ced85b36e8`.
- Existing turn/tombstone + mode-chrome source contracts after hydration patch: **39 PASS**,0 fail/skip/flaky, workers1/retries0. These overlap earlier134 source passes and must not be added as unique coverage.
- Focused strict app+test TypeScript: **PASS**, repeated after final exact file freeze.
- New test ESLint: **0 errors/0 warnings**. Baseline GameUi13 errors and current GameUi13 errors have identical rule/severity/message/column and source-line locations after accounting for the two inserted lines. Broader current named UI subset remains baseline **14 errors/1 warning**, no new findings. Both ESLint commands intentionally exit1 due unchanged existing debt; no waiver/autofix. `implementation-verification-02/lint-comparison.json` records asserted equality; baseline/current JSON retained.
- `git diff --check` on granted production diff PASS;19 other original UI inputs unchanged.

## Reproducible commands

From root, choose a NEW `OUT=session-files/completion-proof/2026-09-09/ferro/<run>` and create `"$OUT/runtime"`; resolve it absolute before changing directory.

`E2E_BASE_URL=http://127.0.0.1:18190/autotable/ TMPDIR="$ABS_OUT/runtime" PLAYWRIGHT_JSON_OUTPUT_NAME="$ABS_OUT/results.json" npm run e2e -- changsha-late-ui-controls.spec.ts --project=chromium --workers=1 --retries=0 --output="$ABS_OUT/test-results" --reporter=json`

Run from `src/frontend/autotable-src/`; replace project with `mobile-chrome` and use a different output directory for touch proof. Against C01, the two late cases MUST stay RED. Against reviewed C02, all three must pass without retries/skip/timeout changes.

`npx --no-install tsc --noEmit --strict --target es6 --module esnext --moduleResolution bundler --types vite/client,node --lib DOM,DOM.Iterable,es6,es2017 src/game-ui.ts tests/e2e/changsha-late-ui-controls.spec.ts`

`ESLINT_USE_FLAT_CONFIG=false npx --no-install eslint src/client-ui.ts src/game-ui.ts src/lobby.ts src/settings-drawer.ts src/ui/variant-picker.ts src/ui/dark-listbox.ts tests/e2e/changsha-late-ui-controls.spec.ts --format=json`

## Candidate integrity and reviewer handoff

Each C01 regression has HTTP START/END pins and actual END container/DLL check. Unchanged image `sha256:113c10b315f41483bdd0e1b0879ffdee08399e4f8b2dd91fc37671284ba10461`, DLL `836821f3819d881fc1ee7bc1a480f98b16adf32e7b37e21449b70d04f1b4cec5`, index `37796c25f581a1e49b26179718bccdc70148e0618997cb4ce5c6cec67cbed6eb`, entry09309beb actual hash `790a5bef6f5a2a7cf764fa69914601b549815155660b44d26996f58d444e191a`. C01 remains separately security/renderer RED.

Independent reviewer must inspect exact final two hashes and retained red/control evidence. Apone alone builds/serves final C02 including reviewed backend/renderer/UI changes; Ferro then executes fixed-image new regression plus real entry/opened picker/phone/tablet/manual controls after Dietrich releases the browser slot. **No C02 fix-green, independent approval, final usability or release claim is made yet.** Other history/context remains at `sessions/2026-09-09-ferro-late-ui-roll-blocker.md`; current compact lane handoff at `sessions/2026-09-09-ferro-ui.md`.

## Independent approval received — supersedes review-pending wording

Consumed coordinator-persisted **`sessions/2026-09-09-ripley-late-ui-review.md`**: independent Ripley FINAL APPROVE — SOURCE ONLY, no significant issues, for the EXACT unchanged GameUi `4de6d768…37583` and NEW spec `8673cbee…dc140`. No revision or hash change followed. Apone was sent the actual review reference and full hashes immediately; all three known production source gates are now approved. Final full corrected C02 assembly—not a new source gate—is next. Fixed-image/browser acceptance remains pending and must not be inferred from source approval. All source author/evidence/final-fingerprint records above remain valid; no self-approval or release claim.
