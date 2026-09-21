# Changsha manual-deal CI reds — fresh-head reproduction (Bishop)

CI: run 31576171218 / job 94048669279 (`e2e`), head 3c2a624 — job **cancelled** at
60m36 (Run Playwright smoke 07:59:05→08:57:45, hit `timeout-minutes: 60`). Four
deterministic-looking reds observed before cancel (all 3 CI attempts):

| # | cell | assertion | CI |
|---|------|-----------|----|
| 1 | chromium `manual-deal-ceremony.spec.ts:89` | `totalDiscards>0` (real-pointer discard registers) | RED |
| 2 | mobile `changsha-human-driven-pickup.spec.ts:97` | `sawAnyDesignation` (pickup.targetSlots shipped) | RED + 150s screenshot timeout |
| 3 | mobile `changsha-manual-deal-orchestration.spec.ts:33` | `presses>=1` (non-dealer not RollingDice-stalled) | RED + 150s evaluate timeout |
| 4 | mobile `changsha-manual-pickup-endpoint-only.spec.ts:385` | `seen.length>0` (pickup window targets my seat) | RED |

## Build provenance (fresh HEAD, NOT the frozen :18089/:18088 candidates)
- HEAD 672f6aa; product tree **byte-identical** to CI head 3c2a624
  (`git diff 3c2a624 672f6aa -- src/ ':(exclude).github'` = empty).
- Canonical `docker build` **BLOCKED** at Dockerfile Stage 3
  (`mcr.microsoft.com/dotnet/aspnet:10.0` → `apt-get update` GPG "invalid signature";
  environmental, HEAD-independent). Deviation: used the established local UAT path —
  `npm run build` (node20+Vite, same as Docker Stage 1) + `dotnet run -c Release`
  serving `/autotable/` (same published Release assembly Stage 2/3 runs), Production env,
  per-run JWT key, isolated port 8092, isolated SQLite DB, BUILD_SHA=HEAD.
- Proof served == HEAD: `/health` buildSha=672f6aa; served `index.html` sha256
  == on-disk fresh build (37796c25…); ContentRoot resolves bundle to `src/frontend/autotable`.

## Result on fresh HEAD (workers=1 retries=0, CI-like headless)
- **#2/#3/#4 mobile: PASS** (first run 43.5s–2.1m; re-confirmed on clean build). Do NOT reproduce.
- **#1 chromium: PASS 18/20 sequential-healthy; FAILS only under saturation.**
  - Runs: 2 early FAIL (~52s, while docker-probe + trace-unzip ran concurrently);
    then diag(1)+loop(5)+underload(1)+tally(10)=17 PASS (~21s); 6× parallel(workers=6): 5 PASS/1 FAIL;
    final clean build: PASS (21.3s). Fail rate ≈ 3/26, all under contention.

## Root cause = timing/contention, NOT product defect
Diagnostic logging temporarily added to `AutotableWsEndpoint.TryHandleDiscardActionAsync`
(reverted; backend now byte-clean at HEAD) proved: across 23 diag runs, **23 discard
frames arrived, 23 ACCEPTED, 0 REJECTED**. Server-side snapshots (SQLite) confirm the
discard path works; one parallel-fail game shows a bot **chow-claim `[2,4,10]` from seat 0**
(`RemoveLastDiscard` moves the discard into the claimer's meld). So the failing cell's
discard DID register, then was legitimately claimed / snapshot-lagged before the client
read `cli.things` — a load-fragile client-side read, not a lost discard.

Committed HEAD backend already contains every referenced fix:
- `ChangshaGameRuntime.StartGameAsync` manual branch schedules the bot-dealer hand-1 roll
  (`ScheduleBotIfNeededAsync`, line ~843/1661) — kills the "#3 RollingDice stall".
- `ChangshaToAutotableTranslator.ApplyPickupTargetSlots` ships `pickup.targetSlots`
  (len-1 exposed-front slot) — satisfies #2/#4 designation.

## Determination → STEP E
All four cells pass on a fresh HEAD build in a healthy environment. The four CI reds are
**CI-environment sensitivity**: the `e2e` job is a 474×2-test, single-worker
(`workers: CI?1`) suite that **cancelled at the 60-min cap** — a resource-saturated runner
starves the real-UI/canvas/timing-sensitive ceremony+discard tests to red (mirrored locally
only under deliberate 6× parallel saturation). No backend/runtime/test defect in Bishop's lane.
No product edit. `e2e-playwright.yml` worker-parallelism / budget is **Apone's** lane (untouched).

## Residual risk (route to Hudson/Hicks — NOT weakened here)
`manual-deal-ceremony.spec.ts:303` (`totalDiscards>0`) is load-fragile: a fast bot claim of
the dealer's discard, or a client snapshot lag under saturation, can zero the count post-discard.
Hardening (e.g. also accept "discard observed then claimed", or await snapshot settle) is a
frontend/test-lane change — must NOT be done by weakening the assertion, adding sleeps, or retries.

## CI-serves-fresh-bytes cross-verification (Ferro ask — RESOLVED)
The general `e2e` job lacks the playability-gate bundle-hash preflight, so Ferro asked to
confirm CI served the fresh 3c2a624 bundle, not stale pre-fix bytes. Verified from the CI
job log (e2e-job-94048669279.log):
- CI Docker Stage-1 Vite emitted **`autotable-src.09309beb.js`** + **`game-bootstrap.dceff56f.js`**
  and **49/49** content-hashed `.js` chunks — ALL byte-identical filenames to the committed
  HEAD dist (Vite content-hash ⇒ identical name = identical bytes). (The only 2 "misses" in a
  first pass were a regex artifact dropping the capital in `zh-Hans.fac38604.js` /
  `zh-Hant.2ac4da8b.js`, both present with identical hashes.)
- Served entry hash: `src/frontend/autotable/autotable-src.09309beb.js`
  sha256 = `790a5bef6f5a2a7cf764fa69914601b549815155660b44d26996f58d444e191a`
  (== my fresh `npm run build`, == committed dist, == candidate :18089 per Ferro).
- CI checkout ref included PR head `3c2a6247…`; BUILD_SHA arg `0de8b6d…` is the
  `pull_request` MERGE-ref commit (expected for pull_request events), whose frontend tree
  produced the identical bundle → no divergent/stale bytes.

⇒ The four `e2e` reds occurred on the CORRECT fresh bundle. Conclusively environment/timing
(saturated, cancelled 60-min single-worker job), not stale bytes and not a product defect.

## First broken transition (per cell)
- #1 chromium: NONE lost — real-pointer discard registered server-side (23/23 diag frames
  ACCEPTED); transient client `totalDiscards=0` from a bot claim of the dealer's discard
  (`RemoveLastDiscard`→meld) / snapshot lag under load. Load-fragile client read.
- #2/#3/#4 mobile: NONE on fresh head (pass). Per Ferro they share a mobile
  seat-handoff/ceremony-start timing surface that only trips under CI saturation; translator
  `targetSlots` shape + `StartGame` bot scheduling are present and correct.

## Recommended remediation (route to Hudson/Hicks + Apone — NOT Bishop's lane)
Spec-local authoritative-condition polling + per-project budgets, RETAINING the real-pointer /
server-authoritative assertions. No sleeps, retries, weakened assertions, or production bypasses.
Workflow worker-parallelism/budget for `e2e-playwright.yml` = Apone.
