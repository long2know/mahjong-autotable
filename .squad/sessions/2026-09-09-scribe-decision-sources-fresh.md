# September 9, 2026 — authored inbox source archive (fresh directives)

Runtime-owned preservation by Scribe. The three original authored texts below are retained without changing their meaning; boundary labels identify the original inbox keys. Current outcomes are consolidated in `sessions/2026-09-09-current-focus.md` and `log/2026-09-09-completion-source-review.md`. Apone's assembly plan and intermediate S11 hash below are historical within this push: the later candidate/reviewer session records, not this earlier plan, determine current readiness and exact approval. No final acceptance is implied.

## Source: decisions/inbox/copilot-directive-2026-09-09T15-01-43-0700.md

### 2026-09-09T15:01:43-0700: User directive
**By:** Copilot (via Copilot)
**What:** Use GPT-6 Astra (`gpt-6-astra`) with maximum reasoning (`reasoning_effort: "max"`) for all Squad members, including Scribe and mechanical operations. Persist `defaultModel: "gpt-6-astra"` and `defaultReasoningEffort: "max"` in `.squad/config.json`. Preserve the existing `long_context` preference and no-downgrade policy. This supersedes the June 15, 2026 Claude Opus 4.8 model default.
**Why:** User request — captured for team memory.

## Source: decisions/inbox/Ripley-completion-before-review-freeze-existing-contracts.md

### 2026-09-09T22-29-02: Completion BEFORE review: freeze existing contracts; six verification-first lanes; retain exact-candidate and reviewer gates
**By:** Ripley
**What:** Completion BEFORE review: freeze existing contracts; six verification-first lanes; retain exact-candidate and reviewer gates
**References:** sessions/2026-09-09-completion-design-review.md, sessions/2026-09-09-completion-lanes.md, #130, #163, agents/ripley/history.md, decisions/inbox/spunkmeyer-claim-key-collision-fix.md, docs/changsha-wall-perimeter-mapping-contract.md
**Why:** September 9, 2026 BEFORE Design Review completed in the current checkout without application edits or git/process/deployment mutations. Persisted detailed handoffs: sessions/2026-09-09-completion-design-review.md and sessions/2026-09-09-completion-lanes.md.

DECISION: GO for six bounded verification/new-evidence lanes (Spunkmeyer backend authority; Dietrich renderer; Ferro real UI; Vasquez rules/docs; Hudson full-app acceptance; Apone candidate/provenance). Corrective edits require an actual fresh reproduction, an exact file grant and verified non-locked author. Preserve the two existing dirty authorization files: both are byte-identical to open PR163 head50abf175. No duplicate implementation.

Frozen contracts remain Changsha authority versus relay, confirmed seat ownership, explicit URL/reconnect behavior, SC4v4 ONE public pickup.targetSlots trigger with take{seatIndex,count}, SC2 opaque hidden-things identities, final top-first 44-case wall mapping/corners-allowed geometry, SpecPure scoring and existing hand-limit/Pass-Hu/NineTerminals behavior. Correct obsolete wall documentation rather than reverting working behavior. #130 is explicitly DEFERRED under its existing backlog/product-direction contract; preserve the #131 UI warning and invent no preset semantics.

Finished requires the companion handoff's F1–F9 on the same pinned candidate, including real human full-match/claims/manual pickup, desktop/mobile and both views, no stalls/errors/retry-only green, rules goldens, owner/security controls, actual served bundle/DLL/image identities and independent exact-version review. Prior August11 browser approval and August13 PR-head checks are historical, not a current proof. Final post-merge signed-image/security matrix remains pending; no merge/publish/deploy or issue closure is authorized now.

Reviewer safety: recover exact artifact-specific dispositions before revision. Hicks is explicitly locked out of the claim-key cycle in its last recovered memo; that memo's test hash differs from current. Hudson's PR128 gate-cycle clearance was not recovered; no self-revision of that gate/helper. Ferro's PR155 rejection was explicitly cleared by independent Hudson revision28bf7da. Ripley is the S11 revision author, so Frost/non-author must independently review that artifact. Full latest lockout roster could not be recovered from the oversized runtime ledger export at a prohibited location; do not infer clearance from merged/green status. This uncertainty holds affected edits, not independent verification or unrelated lanes.

Copilot coordinates the shared checkout; Apone seals the final local artifact; Hudson executes frozen full-app acceptance; Ripley owns cross-lane gate except self-authored artifacts. All agents gpt-6-astra/max/long_context; no factory. Two permitted read-only consultations used, no further delegation by this review.

## Source: decisions/inbox/Apone-september-9-local-candidate-uses-pinned-cached-run.md

### 2026-09-09T23-00-02: September 9 local candidate uses pinned cached runtime after reproduced canonical build blockers
**By:** Apone
**What:** September 9 local candidate uses pinned cached runtime after reproduced canonical build blockers
**References:** sessions/2026-09-09-apone-candidate.md, sessions/2026-09-09-completion-lanes.md, sessions/2026-09-09-completion-design-review.md
**Why:** Canonical root Dockerfile c01 build failed at apt-get update with invalid Ubuntu noble InRelease signatures (exit100); signature checks remain enabled and no repo Docker/workflow/package configuration is changed. Canonical backend-build and frontend-build targets then succeeded freshly from current dirty sources: backend image sha256:8de7e13f67c3b16d35de822b2c6e4f0fd7fb089d929680646902720a9940e55e; frontend image sha256:bd5488062d3ae20d81d015abdd8ccced26d847ffaa7e2e64fe2ea1fedae7df65. Evidence-local fallback atop cached runtime sha256:44563d8ce5d1dffc07078674657bcfcc9b31ba5179e400a4ca99d466318416c6 hit Docker BuildKit/vfs cross-image COPY checksum `invalid argument`. Next exact fallback: create unique non-started extraction containers from the two successful build-target images, docker cp their published API and bundle into the isolated Apone evidence directory, hash those artifacts, then use ordinary local-context COPY in a second evidence-only Dockerfile on the same pinned cached runtime. Only old /app and /frontend/autotable inside the NEW image are replaced; no host/shared process/data deletion. This is a local completion candidate with packaging limitations, not a canonical clean/release build. Full failure logs retained. No image will be declared runnable before its new isolated container is started and detailed health/static/hash checks pass.

Source freeze remains intact excluding the existing build-generated dist-size.json metric: effective application inventory SHA256 a38b28e7e5164f65a5c9f263a0fccca8e8dcb331600aed645c2c85df29320a2c (548 files). Protected inputs unchanged. Drake's independent S11 helper revision changed only the test from b08174d8… to 10cf8385… during assembly; both hashes are retained and test acceptance must pin the approved current version. No old test approval is transferred.

Frontend build, bundle-sync and strict tsc are freshly PASS. Existing ESLint is freshly RED (17 errors/10 warnings); frontend production source has no diff from HEAD, and Apone will not autofix another lane's source or waive the gate.
