# Project Context

- **Owner:** Stephen Long
- **Project:** Changsha-first Mahjong game built from pwmarcz/autotable, with expanded Chinese rules planned
- **Stack:** .NET 10 backend, EF Core + SQLite initially, optional React + Fluent UI 9 + TypeScript + Vite frontend modernization, single-image Docker deployment
- **Created:** 2026-04-20

## Learnings (summarized 2026-07-27T01-56-23-811-07-00)

> Full history (43670 B, 59 entries) preserved verbatim in `history-archive.md`. Most-recent entries retained below.

📌 Production-readiness audit (2026-06-03): 54/59 system gates, 16/16 prodready gates, caught leave-seat blocker — committed `ea36eb2`.

---

## 2026-06-04 — Docker single-image deploy + STATE-OF-GAME capstone — summarized 2026-08-07T09-40

> Long-form preserved verbatim in `history-archive.md` (folded 2026-08-07T09-40). Compact:

📌 Docker single-image deploy proof (2026-06-04): `/health` 200, real 4-bot Changsha game running in-container with 0 page errors; non-root UID 1000 + `/data` SQLite volume + healthcheck; README augmented with verified build/run + F5 commands (fixed stale `src/frontend/modern` refs). Flags: amd64-only image; JWT signing key must be operator-set for restart-survival.

📌 STATE-OF-GAME.md capstone (2026-06-04): 18-gate evidence table / 300 lines / 27 evidence paths `ls`-verified / zero gaps; docs-only, strict lane discipline.

---

## 2026-07-26 — Design Review ceremony ("address everything" completion)

### Directive
Auto-triggered Design Review. Stephen ordered the whole team to fan out and finish the multi-domain game with full testing ("Yeah … address everything").

### Method
Convened Bishop/Frost/Vasquez/Hicks/Ferro/Hudson/Apone as READ-ONLY inspectors (claude-opus-4.8, max effort, long-context). Gathered fresh HEAD (e9e349b) baseline myself first — did NOT trust the stale 2026-06-19 "all green" claims.

### Verified baseline (fresh)
Backend build PASS; backend tests 5330 pass / 0 fail / 2 skip; frontend tsc PASS; frontend eslint 25 errors but NOT CI-gated; committed bundle in sync; Docker image + F5 contracts hold; Postgres overlay compose config VALID (the `******` I saw was tool-masking of `Password=`, not a malformed string — verified via `docker compose config`).

### Findings (2 P0 + 10 P1)
- P0: no real-UI full-game acceptance gate (all Hu/gameComplete evidence is WS-backdoor); real-UI human connect flow recorded "user stranded".
- P1 highlights: manual deal ceremony runs first hand only then auto-deals (ChangshaGameRuntime.StartNextHandOrEndAsync:1756-1765); live scoring adds fan-bonus + stacking multiplier diverging from locked spec §5.1 (Standard-258 self-draw pays 2 not 1); SQLite migrations not applied at runtime (EnsureCreated drift); SQL Server never integration-tested; docker-compose.yml quickstart crash-loops (Production + no JWT key + no .env.example — Lead-confirmed); Production CSP would break Sentry/HLS + WASM-if-strict; flat/perspective not pixel-gated.

### Deliverable
Full ceremony summary at decisions/inbox/ripley-completion-design-review.md — evidence-based gap inventory, 8 locked cross-component contracts, 7 non-overlapping work packages (WP-A..G), 16-row acceptance matrix, parallel-launch plan, termination condition. squad_decide recorded.

### Sign-off items surfaced for Stephen
(a) scoring numbers — codify fan+stacking into spec §5 vs revert spec-pure; (b) §3.6 missed-win tile-specific vs seat-level; (c) SQL Server real integration vs architecture-only.

### Self-critique
- Did NOT run a live game myself — deliberately (ceremony is design-review, not implementation); the playability P0 is precisely that no one has proven a real-UI full game, so the fix is a new gate (WP-F), not another ad-hoc backdoor playtest.
- The scoring §5 divergence is the sharpest risk: 5330 backend tests are green because the tests were updated to the fan-inclusive numbers, but no test pins spec §5.1 Examples 1-10 — green tests masked a spec-vs-code drift. Lesson: conformance gates must pin the SPEC, not the current code output.

📌 Design Review 2026-07-26: backend/persistence solid on HEAD (5330/0/2); 2 P0 + 10 P1 gaps; 7 work packages assigned with non-overlapping ownership; contracts C-1..C-8 locked.

## FINAL Reviewer Gate — PR #128 / WP-F #122 → REJECT (2026-07-27)

**Target:** merged main `ab3687d` (squash of #128; head `cffc3e4`). Post-merge ship gate — Hudson authored, Ralph merged before my review. Verdict comment (COMMENTED; shared identity blocks REQUEST_CHANGES): PR #128 issuecomment-5088676618.

**REJECT — required real-UI acceptance RED on integrated main:**
- `playability-gate`: FAILURE; `e2e`: FAILURE (281 pass / 246 skip / 1 fail). Both fail the SAME headline `playability-gate.spec.ts:437` 4-hand human-vs-bots test — **stalls in hand 1, handEnds=0, dealers=[0], realClaimsAttempted=0**, across all 6 attempts (2 jobs × initial+2 retries). Tripped `expect(run.stalled).toBe(false)` :504.
- Root cause: PR head `cffc3e4` lacked #135 (merge-base `06c164c`/#133); integrated `ab3687d` = `8aaa37b`(#135)+#128. Same `seed=4100` (backend honors `?seed=` :270-277) → pre-#135 completed 4 hands, post-#135 integrated stack **regresses the Pass-only human flow**. PR's "Pass-only unaffected" (line 503) FALSIFIED on main.

**Verified solid (preserve):** no backdoors (real pointer/click/keyboard only; evaluate write-audit empty; 6 backdoor `.mjs` neutered); #133+#135 green; **independently re-ran `HumanClaimWireContractTests`@ab3687d → 8/8 pass** (human Pung/Chow/Kong/Hu accepted — exercised, not avoided, at WS layer; DOM `claimByClick` only Hu/Pass); handCount hc1/hc8/hc16 exact + zeroSum=0; bundle-hash preflight green; both views.

**Strict lockout:** Hudson locked out. **Bishop (WP-A runtime) = named revision owner** for the hand-1 wedge regression; Hicks (WP-D world.ts) + Ferro (WP-E) as needed; WP-F gate-hardening + real DOM human-meld-claim assertion by a non-Hudson agent. Corrective PR must make `e2e`+`playability-gate` green on the NEW integrated main before BOARD CLEAR.

📌 Team update (2026-07-27T01-56-23-811-07-00): Your rejection retrospective (deferred from the prior turn) is now merged to decisions.md. You issued the #128 FINAL reviewer-gate REJECT on integrated main `ab3687d` and named Bishop revision owner; a corrective PR is required before BOARD CLEAR. You are facilitating; the reviewer-gate batch #123-#129 is otherwise all-approved. — recorded by Scribe (decisions.md §2026-07-27).

📌 Team update (2026-07-27T02-51-38-764-07-00): You **APPROVED PR #141** (Apone; container-scan-remediation files an issue only when findings>0, Closes #140) — workflow-only, fail-closed (the blocking gate is `container-scan.yml`, untouched). Merge once #137 is green on main or per the P0-exception policy. You flagged a docs gap (`secrets-scanning.md` §4.1) -> Apone closed it (commit `b664f74`) for your head-confirm. — recorded by Scribe (decisions.md §2026-07-27).


## Reviewer Gate — PR #155 / #153 frontend → REJECT (2026-07-27)

**Target:** PR #155 (author Ferro/long2know), head `7589fa9d146f2694e7cff8b98b1fc7bb8d768ca2` (full SHA resolved from `7589fa9`; mergeable/CLEAN). Frontend half of #153 "honest default-game UX". Read-only review; verdict comment (COMMENTED — shared identity blocks formal approve): https://github.com/long2know/mahjong-autotable/pull/155#issuecomment-5097268757

**REJECT — the PR's own new headline test is FLAKY at the exact head.** `e2e` job `90110567918` (run `30306084042`) is green ONLY via Playwright retry: `1 flaky`, `316 passed`. The flake is `tests/e2e/dealer-turn-affordance.spec.ts:122` (`:69:7 honest default flow … bots respond`), failed first attempt: "no bot responded after the human discard (turn appeared frozen)" — `Expected > 2, Received 2`, 45s timeout. It reproduces the exact #153 "frozen bots" symptom on first run and is the PR's headline proof for dealer-discard→bot-response; retry-masked green is not deterministic-enough-for-CI and not genuinely green.

**Verified solid (preserve):** head/config parity `NEW_GAME_DEFAULTS` == lobby `DEFAULTS` (auto/3/Hard/4); NO location.replace loop (buildFreshGameUrl always stamps non-empty minted gameId → readConcreteGameId non-null on reload → funnel never re-fires; cross-checked start() getUrlState guard); bare-URL opens New Game lobby with BLANK game-id field (no silent changsha-default); concrete gameId joined verbatim + no re-mint on reload (creator-wins/reconnect); legacy ?gameId/dealMode/botCount links honored; ?seat= funnels to fresh id preserving seat; changsha-default only joinable when literally typed; crypto→getRandomValues→Math.random fallback safe for PWA/non-secure; hash-drop matches pre-existing lobby.buildUrl (spectate #/spectate/ route independent — not a regression); disjoint from #154 (#152 HUD/CSS) and complementary to backend #156; NO backdoors (default-game-ux asserts URL/DOM/WS-handshake bytes; dealer spec advances via _playability real pointer/click only).

**Strict lockout:** Ferro locked out. **Revision owner: Hudson** (Tester; owns _playability harness + authored #153 diagnosis) to make the bot-response acceptance deterministic (assert an authoritative turn-advanced/bot-seat signal, not a fixed 45s discard-count budget; harden real-pointer discard registration). **Hicks** (Frontend Dev) as needed for turn-banner/client-ui production tweaks; Ferro's frontend logic otherwise sound and retained. Corrective PR must land `e2e` + `playability-gate` green with **0 flaky** at the new head before merge. Do not merge.

## FINAL Reviewer Gate — PR #155 / #153 frontend → APPROVE (2026-07-27)

**Target:** PR #155 integrated head `28bf7da4e11e39961ccce689e756c94ca36f688e` (revision author Hudson; Ferro locked out). Strict read-only re-review reversing my `7589fa9` REJECT (#issuecomment-5097268757). Verdict comment: https://github.com/long2know/mahjong-autotable/pull/155#issuecomment-5099221315

**APPROVE — my flaky-headline blocker is fixed at the true seam, not masked.**
- `7589fa9..1dc32dd` (test-only, no src/bundle): read-only `readBotActivity` + `installHandEndObserver`/`readHandEndObserver` replace the invalid `readDiscardCount()>discardAfter`. Bounded, baseline-relative, covers non-local discard / non-local meld (claim churn = the flake seam) / hand-end. Authoritative world.things+client.result; local seat excluded (mySeat=client.seat asserted ===0) ⇒ can't pass from human's own discard/stale/baseline race.
- Human real-pointer discard (discardByPointer, real hover+mouse.down/up, no emit) proven accepted BEFORE the bot poll; timeout 45s→30s (no inflation); no retries/skip/mutation/client.update/dispatch/emit/hooks.
- Exact-head CI e2e (job 90155842320): **329 passed / 267 skipped / 0 flaky / 0 failed** with retries:2 configured ⇒ first-attempt pass. e2e + playability-gate + bundle-sync + view/visual + pre-commit + gitleaks + Trivy + Lighthouse all green. Meets my "0 flaky at new head" bar.
- `1dc32dd..28bf7da`: real merge (parents 1dc32dd+c7d0cbe; c7d0cbe #154 + d82430b #156 both ancestors); source semantically disjoint (zero source overlap); every #155 source byte-identical across merge; dist clean rebuild (only index.html + manifest + style hash 0092dca8→01bf3260); bundle-sync green.
- URL/session solids reconfirmed unchanged (default-game-ux.spec). #155 adds ZERO backend source; head backend == main c7d0cbe (inherits #156 + Issue153 lifecycle tests). **Issue #153 closable on merge.**

No revision owner needed (approval). Do not merge from reviewer seat; merge per team policy.

📌 Record note (2026-08-07T09-40, by Scribe): You FORMALLY RETRACTED your 08:39 `targetHandles` pickup request and CONFIRMED (B) single-trigger-slot `pickup.targetSlots` (SC-4 v4) as the SOLE canonical pickup key — explicitly affirming the Scribe integrity guard that held the record to `targetSlots` through the ~13-pivot churn. Recorded on decisions.md §2026-08-07T09-06 (v4 FROZEN; your 08:39 request added to SUPERSEDED/VOID). Permanent split: `targetHandles` = SC-2/G19 opaque per-viewer hidden-`things` keys ONLY, never the pickup key.
