# Completion handoff — six lanes and reviewer gates (September 9, 2026)

**Owner:** Ripley. Companion: `sessions/2026-09-09-completion-design-review.md` (Squad runtime key), which contains source evidence, frozen C1–C6, measurable F1–F9, #130 deferral and lockout limitations.

## Dispatch rule

Release these six **verification/new-evidence** scopes now. Do not create implementation merely to occupy an agent. Names are eligible for the stated initial read-only scope, NOT a blanket re-admission to unresolved rejected artifacts. Before a corrective edit, coordinator must recover its exact rejection/author history and grant one eligible author the specific files. When that record is missing, only the revision is held; independent validation and unrelated work proceed.

Every agent: `gpt-6-astra`, `reasoning_effort=max`, `context_tier=long_context`. No factory. Same current checkout, branch `squad/s11-stable-tombstone`, no switches/staging/commits/reset/stash/publish/deploy. Preserve all existing dirty work, including coordinator model config and both authorization files. Mutable Squad state only through runtime tools. No temporary-system-directory operations. Place new evidence under a unique, repo-relative `session-files/completion-proof/2026-09-09/<run-id>/<agent>/`; never overwrite prior findings/screenshots.

## Six parallel lanes

### A — Backend ownership/authorization closure: Spunkmeyer

- **Start:** prove the already-present #163 correction, not rewrite it. Endpoint and test blobs equal PR163 head50abf17; freeze them as protected pre-existing work.
- **Potential write grant, only after eligibility/reproduction:** `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs` and `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableWsSeatAuthorizationTests.cs`. No runtime/rules/persistence/cookie refactor. Additional test files require explicit separate ownership so Vasquez does not collide.
- **Acceptance:** rejected occupied-seat take cannot persist spoofed seats, leak on reconnect or authorize attacker mutation; valid owner control works; spectator/wrong-owner discard/claim/pass/roll/pickup rejected; relay mutation pass-through unchanged.
- **Existing selectors:** `AutotableWsSeatAuthorizationTests`, `SeatOwnershipSpoofTests`, `BishopUatPrivacyContractsTests`, `ChangshaPrivacyProjectorDelegationTests`, identity/startup-validator tests as relevant.
- **Handoff:** exact source/test blobs, command/result, preserved valid-owner controls, independent reviewer requested. Burke is an identity/security review reserve, not a concurrent cookie editor. Drake is runtime-reliability reserve: a fresh runtime regression gets a separate file grant, not endpoint scope creep.

### B — Renderer/physical interaction: Dietrich

- **Potential write boundary:** `src/frontend/autotable-src/src/{setup.ts,slot.ts,world.ts,mouse-ui.ts}` only for a freshly reproduced, eligible correction. `setup-slots.ts` geometry is frozen until Ripley+Vasquez sign the exact contract change. No UI/lobby/harness/dist changes.
- **Acceptance:** long-lived clients survive multi-hand conditions rebuilds; stable hidden-park slot, no orphan references/undefined places/hidden raycast targets; visible owned hand and actionable discard; one physically contiguous perimeter, proper reachable front, both cameras and relay scene behavior.
- **Existing selectors:** `renderer-hidden-park-slot`, `changsha-wall-worldcoord-polyline`, `wall-corner-ring.contract`, relevant existing node hidden-park/slot-owner tests, `view-mode-toggle`.
- **Handoff:** fresh reproduction or explicit green, artifact hashes, scene invariants across hand boundaries. Do not reinterpret a local mesh index as a server privacy leak or change frozen wall order to match stale prose.

### C — Real entry/UI/affordances: Ferro

- **Potential write boundary:** `src/frontend/autotable-src/src/{client-ui.ts,game-ui.ts,lobby.ts,settings-drawer.ts,style.css}` and `src/frontend/autotable-src/src/ui/variant-picker.ts`. No renderer, shared wire types, backend, test helpers or generated dist.
- **Acceptance:** real Quick Match/New Game seats and deals; explicit room reconnect preserved; occupied room escape operable; no legacy local-deal controls in Changsha; relay controls preserved; claim/discard/pickup cues agree with authoritative state and tombstones; actual opened picker readable; phone/tablet controls reachable; #131 preset warning retained.
- **Existing selectors:** `default-game-ux`, `changsha-seat-handoff`, `changsha-mode-chrome.contract`, `turn-cue.contract`, `changsha-setup-selector-readable`, `changsha-responsive-canvas`, `rule-presets` notice, stale-room escape.
- **Handoff:** real desktop/mobile control evidence, no computed-option-style-only or synthetic-event proxy. Preserve each entry path's current URL/default behavior; no framework migration.

### D — Rules conformance and conflicting docs: Vasquez

- **Potential write boundary:** directly relevant named golden/characterization tests in `src/backend/tests/Mahjong.Autotable.Api.Tests/Changsha/{Scoring,Acceptance}/`, `Autotable/SlotMapWallGoldenTests.cs`, and `docs/changsha-wall-perimeter-mapping-contract.md` with directly related rules/limitations text. No endpoint/runtime source edits, arbitrary test cleanup or #130 implementation. Frost's literal fixture remains read-only pending its recovered disposition.
- **Acceptance:** signed F1/F2 44-case mapping and reachable top/bottom behavior; anti-circular literal oracle (fd0 wall.9.1@0, dealer0/dice5/seed42/break18, 17 keys incl49/51) and actual later mutation-proof/sign status; SpecPure Examples1–10, contextual Big Wins, hand caps/rotation/Pass-Hu. Report missing provenance as missing provenance, not a current code defect.
- **Existing selectors:** `SlotMapWallGoldenTests`, `FrostSc3ReachableWall0LiteralTests`, `Section51GoldenTests`, `ContextualBigWinScoringTests`, `ScoringOptionsCharacterizationTests`, `MaxHandsRotationGoldenTests`, `MissedWinTileSpecificityCharacterizationTests`, `ManualDealPerHandCeremonyTests`, `WsDrivenManualProgressionRegressionTests`.
- **Concrete doc correction:** mapping document's bottom-first formula, nextTileSlots vocabulary and literal one-pitch corners conflict with final contract/current tests. Reconcile prose without reopening the targetSlots/targetHandles dispute or inventing preset semantics.

### E — Independent full-app acceptance: Hudson

- **Write boundary:** new run-specific evidence only. Existing specs/helpers, application source and historical artifacts are read-only. Running frozen tests is allowed; revising or self-approving Hudson's rejected gate/helper cycle is not. A demonstrably invalid test is reassigned to a verified independent author (Drake reserve) under a separate exact-file grant.
- **Acceptance:** F1–F9 matrix from companion decision, same sealed candidate; desktop/mobile as configured, both camera views, real human game/claims/manual pickup, authoritative completion/modal/zero-sum, no UI errors/stalls. Explicitly inventory existing intentional skips. No new skips/retry-only passes.
- **Evidence routing prerequisite:** existing recorders may write legacy directories. Use supported existing artifact-directory overrides or an isolated repo-relative mirror with identical test/helper hashes; do not run a command that overwrites current `playtest-artifacts/` findings. Freeze test hashes before execution. Do not weaken helpers or mutate game state to make a browser scenario pass.
- **Handoff:** commands, hashes, project/device/seed, expected/pass/fail/skip/flaky counts, captured errors and reproduction for each failure; distinguish product, harness and environment using evidence, not assumptions. Adversarial raw-WS checks are separate security evidence, never credited as real-UI gameplay.

### F — Candidate assembly and proof provenance: Apone

- **Write boundary:** sole final generated-bundle writer via existing `npm run build`; new local packaging/manifests/evidence; README/STATE-OF-GAME current-proof refresh after acceptance. Build/workflow changes require a reproduced blocker and a separate grant. Preserve dirty `.vscode/settings.json`; no secret output/commit, registry publish, merge or deployment.
- **Acceptance:** source/dirty-diff fingerprint, test-file hash inventory, one source-locked local Production-shaped image, DLL + served entry/hash matching at START/END, `/health`, persistence/reconnect, bundle parity and canonical provider/build records. F5 and bootstrap remain usable. Do not substitute a custom buildSha label for actual bytes.
- **Existing tooling:** frontend package scripts `build`, `verify:bundle-sync`, `bundle:hash`; documented strict TypeScript command; existing backend solution build/tests; configured Playwright projects and pinned visual environment. No new test/build tool adoption.
- **Handoff:** sealed candidate coordinates/identities and safely isolated running target when authorized, evidence manifest, current-vs-historical distinction. Previously required exact signed-image matrix remains a post-merge release gate; neither local reconstruction nor old :18089 approval satisfies it.

## Shared boundaries and order

1. **Coordinator baseline/eligibility first.** Record current HEAD, dirty-file ownership and exact reviewer dispositions through runtime state; no git manipulation. Initial verification scopes can run concurrently. No author may inspect/edit source assigned to another lane to duplicate its audit.
2. **Targeted first.** Batch related selectors using the same existing runner; one backend invocation for related backend selectors, one Playwright invocation per related group. Use `--workers=1 --retries=0`; do not change xUnit collection parallelism or Playwright's mobile DSF1/visual configuration. Preserve unique game IDs and isolated databases/ports. Never stop someone else's process or access old shared games with mutating probes.
3. **S11/claim-key precise stability.** Current S11 SHA256 `b08174d8a22aaa18cf1100f6d29e12bf5457a3f800fc778427d56b13d17627a2`; current claim-key SHA256 `6d3db9f335fe7b563ecff0b8081cf4fc9d050c03c9f483a45def50f3d3a68564`. Repeat exact-version targeted scenarios. Preserve S11 stable same-hand tombstone and non-vacuous observed-owned-window/real-press behavior; no transient bot-window convergence or arbitrary timeout/skip fix. Claim-key must exercise actual claim windows with non-vacuous outbound observation.
4. **Source freeze, then one build.** Only Apone writes `src/frontend/autotable/` through the existing build. No other Vite rebuild while acceptance runs. Package/lock files, `types.ts`, `_playability.ts`, `_uat_red.ts`, raw-WS helpers, Playwright config, solution/project files and existing snapshots are otherwise frozen. Required shared changes get one named eligible owner and contract review.
5. **Integrated final matrix.** After targeted greens and source freeze, run canonical full backend/frontend/desktop/mobile/provider/visual validation for actual full-app acceptance. Headline human P0 gets three independent repeat seeds with retries0. Existing intentional mobile skips stay explicitly named; do not extrapolate one desktop pass to all mobile claims. Any relevant byte change invalidates affected acceptance and restarts that portion against the new pin.
6. **Truthful finish.** Record independent exact-version approvals, retained evidence hashes and current known limitations. Do not mark BOARD CLEAR from old screenshots, passing counts, PR mergeability, or an unverified signed-image claim. #130 stays deferred. No merge/publish/deploy/issue-close action in this authority.

## Integration and reviewer ownership

- **Shared-checkout integration coordinator:** Copilot. **Final assembly/bundle:** Apone. These are not permissions to commit or merge.
- **Final architectural/cross-lane acceptance gate:** Ripley, except artifacts Ripley authored.
- **Independent real-app execution/acceptance:** Hudson, against frozen production/test artifacts; not self-revision of rejected test work.
- **S11/current canonical-test review:** Frost, after confirming non-authorship in that cycle. Ripley is the documented S11 revision author and cannot be its sole approver.
- **Backend authorization/identity:** Burke or another verified non-author; a cookie author may not approve their own changed cookie artifact. Production authors do not self-approve.
- **Rejection handling:** different eligible revision author; original and subsequent rejected authors remain excluded from revision/advice until an explicit exact-cycle approval. Unknown eligibility means HOLD that edit, not silently route to the default historical owner. Scribe may later fold the two handoffs and decision via runtime state.

No application/test/bundle modifications, builds, test execution or process/deployment operations were performed by this design review. This handoff supplies the bounded execution plan; it does not manufacture a fresh green result.
