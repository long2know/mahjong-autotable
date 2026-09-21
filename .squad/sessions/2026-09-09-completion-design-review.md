# BEFORE Design Review — completion push, September 9, 2026

**Facilitator:** Ripley. **Requester:** Copilot; project owner remains Stephen Long.
**Decision:** GO for six verification-first lanes; HOLD completion/release claims and any rejected-artifact revision whose author eligibility is unresolved. Detailed write boundaries and integration sequence: `sessions/2026-09-09-completion-lanes.md` (Squad runtime key).

**Authority:** current checkout/branch only; no application implementation by this review, no branch switch, stage, commit, reset, stash, publish, issue closure or deployment. Preserve existing dirty state/config, `.vscode/settings.json`, screenshots/playtest artifacts and authorization edits. Mutable Squad state uses runtime tools. Every agent must use `gpt-6-astra`, max reasoning, long_context; no factory. Two read-only consultations completed: Frost/reviewer-state and Vasquez/rules-contracts.

## 1. Current facts, not another completion assertion

- Verified branch `squad/s11-stable-tombstone`, HEAD `b38e1a943e8804020c7887af3920fba257d1a427` (August 12 stable-pickup-tombstone test correction), preceded by `b1936bc` (#161), `200cad4` (#160), and `3f416a5` (#158 contextual Big Wins).
- The existing dirty `src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs` and `src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableWsSeatAuthorizationTests.cs` are **byte-identical to PR163 head `50abf17547ce8bf61daf629d9bcc03b6c4ef54ed`**. Matching local/GitHub blob IDs: endpoint `50bd1fbbcfaaffc02309341c4bf7da76ac65a545`; tests `81d16d46b0e4ff9a5c18a3a90e80097bc7da9bee`. Diff: endpoint +12/-15; tests +155. Do not duplicate or overwrite this correction.
- GitHub #163 rechecked September 9: OPEN, MERGEABLE/CLEAN; updated `2026-08-13T06:35:37Z`. SQLite/Postgres/SQL Server, migration drift, desktop/mobile E2E, playability, pre-commit, scan and amd64/arm64 runtime checks report SUCCESS at that PR head. No formal GitHub reviews are recorded. The body reports independent security approval, 5,873 backend passes/2 skips, 33 focused passes, corrected-image security 40/40 and evidence hashes 54/54. It explicitly leaves **the final exact signed-image matrix until after merge**. None is a fresh run of today's checkout.
- Strongest inspected browser evidence: `session-files/completion-proof/final-candidate-18089/hudson/HUDSON-UAT-REPORT.md:5–32,36–76,81–137`: August 11 immutable candidate, image `6ed906ae37c2…`, DLL `c039154d…`, served entry `09309beb`; 89 pass, one intentional project skip, zero failures/flakes, workers=1/retries=0. Substantial gameplay already works; that historical image does not certify current bytes.
- Recovery checkpoint #24, session `688ffefb-2daf-43ef-b49d-b196a7a9016b`, `2026-08-13T04:02:15.210Z`, also left the exact-image security matrix incomplete. No broad GitHub scan or redundant subsystem audit was performed.

## 2. Frozen cross-team contracts

**C1 — Mode and authority.** `/autotable/ws` preserves the Changsha-authoritative/non-Changsha-relay switch. Changsha owns wall/deal/turn/claims/scoring/bots/seat entitlement. No Changsha control may disconnect authority and rebuild a local relay table under stale cues. Arbitrary scene updates are not gameplay commands. Spectator/wrong-owner actions cannot mutate state; legitimate owners and relay forwarding still work.

**C2 — Entry, identity and cues.** Preserve New Game/Quick Match fresh IDs and explicit-room reconnect. URL seat is a request, not ownership. Confirmed seat/turn/claim/pickup drives input/private faces; tombstones remove stale UI. Preserve occupied-seat escape and owner reconnect. August 11 Quick Match observed Auto/seat0/three Medium bots; retain explicit URL options and existing entry-path defaults rather than inventing a new global default.

**C3 — Final wall/pickup contract, not obsolete prose.** Absolute-seat stacks `[14,14,13,13]`, capacities `[28,28,26,26]`, dealer origins `[0,28,56,82]`; break wall `(dealer+diceSum-1)%4`, column `Stacks[breakWall]-diceSum`. Cover **44** cases: four dealers × sums 2..12. Top-first layer is `1-(localOrdinal%2)`. Remaining-wall anchor `(dealerOrigin+breakTileIndex+frontDrawn)%108`, where `frontDrawn=108-wallCount-backDrawn`; front draws/back kong replacements leave one middle arc.

**SC4v4:** `pickup.targetSlots` contains exactly ONE public reachable frontier slot; a real press requests **`take{seatIndex,count}` only**, server selects tiles. `batchPreviewSlots` is inert. Wrong/missing/empty/multiple targets, wrong actor, covered lower tile, Auto and post-ceremony wall input fail closed. Reachable means no occupied tile above: the bottom becomes reachable after its top is consumed. No human auto-take; manual ceremony repeats after dealer rotation and terminates with a pickup tombstone. `targetHandles` belongs ONLY to SC2 hidden `things`, NEVER pickup. Do not restore `nextTileSlots`, tile IDs or batch-actionable pickup semantics.

World-coordinate perimeter/corners-allowed behavior and the existing **1.6× pitch corner bound** outrank the stale exact-one-pitch document. Evidence: `docs/rules/changsha-spec.md:86–96`; runtime inbox `bishop-F1-frame-declaration-PA-PB.md` (P-A/P-B), `ripley-wall2-paste-lane-gap-proxy-stale.md` (August 7 16:15, signed 44/44 oracle); current `SlotMapWallGoldenTests.cs:47,56,101,228,398`, `changsha-manual-pickup-endpoint-only.spec.ts:412–993`, `wall-corner-ring.contract.spec.ts:104`. Vasquez recovered these, not a new design pivot.

**C4 — Privacy is wire identity.** Hidden wall/foreign concealed tiles have opaque per-viewer identities; entitled/public tiles retain real IDs. Preserve durable reconnect stability, cross-viewer unlinkability, private faces and rejected unauthorized mutations. Anonymous client back-pool mesh indices are not server tile IDs. Evidence: `BishopUatPrivacyContractsTests.cs:30–123`, `SeatOwnershipSpoofTests.cs:70–163`, `.frost_uat_scratch/ADJUDICATION_privacy_t_index.md`.

**C5 — Rules/scoring.** Preserve SpecPure §5.1 Examples 1–10, contextual Big-Win fixes, zero-sum/dealer/base-unit rules, current claim/rob-kong behavior, manual/auto convergence and 16-hand ceiling. Fan/stacking is display-only by default; HouseRules is opt-in. Seat-level Pass-Hu until next own draw and loose Nine Terminals are accepted baseline, not unfinished semantics to guess. Sources: `docs/known-limitations.md:7–12,58–84,115–155`; `Section51GoldenTests`, `ContextualBigWinScoringTests`, `MaxHandsRotationGoldenTests`, `MissedWinTileSpecificityCharacterizationTests`.

**C6 — Evidence identity.** The backend serves `src/frontend/autotable/`, not a Vite dev server. Freeze source and record actual image/DLL/served-entry hashes at START and END. A friendly buildSha, old screenshot, different rebuilt tree, or a green aggregate cannot substitute. Relevant changes invalidate affected proofs. No added skips, retry-masked success, timeout-only concealment or gameplay injection.

## 3. Measurable definition of finished

All rows must pass on one identified final local candidate. Historical passes guide work, not substitute for it.

| Gate | Required result / existing selectors |
|---|---|
| F1 Entry/ownership | Real Quick Match and New Game acquire only authorized seats and produce playable hands; occupied-seat attempts reveal no owner hand; owner reconnect resumes same room; stale room has genuine escape. `default-game-ux`, `changsha-seat-handoff`, `c5-stale-game-actionable-seat`. |
| F2 Human match | Desktop four-hand human-versus-three-bot real canvas play reaches authoritative GameComplete + visible scoring modal, >=4 hand results, >=2 dealers, both views, zero-sum, no stall/modal storm/errors. Three independent repeat seeds, retries=0. `playability-gate.spec.ts:468–555`. |
| F3 Caps/mobile | Human handCount=1 ends in exactly one hand with modal; 8/16 all-bot runs honor authoritative caps. Desktop/mobile configured coverage passes. Spectator proof is not human-play proof. `playability-gate.spec.ts:557–647`, `changsha-4hand-gamecomplete`. |
| F4 Claims/lifecycle | Genuine Pung/Chow then real-pointer discard advances play; legal Hu/kong/scoring covered by rules/WS tests. Manual ceremony works each hand/rotated dealer; no human auto-take or unsafe re-deal; cues/tombstones correct. `post-meld-discard`, `changsha-manual-deal-orchestration`, `ManualDealPerHandCeremonyTests`, `WsDrivenManualProgressionRegressionTests`. Pass-only match completion alone is insufficient. |
| F5 Physical UI | One real perimeter/frontier and correct top/bottom order; endpoint S1–S13/HUD teardown; private/own faces; no orphan slots or raycast errors; real opened picker readable; phone/tablet controls reachable; both views and relay preserved. `changsha-wall-worldcoord-polyline`, `renderer-hidden-park-slot`, `changsha-human-driven-pickup`, `changsha-responsive-canvas`, `changsha-setup-selector-readable`, mode-policy/chrome, pinned visual project. |
| F6 Rules | Existing literal/golden oracles pass: 44 anchors, reachability, anti-circular literal, §5.1/contextual wins, caps/rotation/Pass-Hu. No invented preset/house rules. |
| F7 Security | Occupied-seat/reconnect regression; spectator/wrong-owner discard/claim/pass/roll/pickup rejected; valid owner and relay controls succeed; privacy/stable signed identity preserved. Independent exact-candidate acceptance. Historical corrected-image 40/40 is not final signed-image proof. |
| F8 Build/local distribution | Targeted checks first, then canonical backend/frontend/desktop/mobile/visual/provider gates: zero unexpected failure/flakes, explicit unchanged skip inventory. Local Production-shaped single image, `/health`, served-bundle parity and persistence/reconnect verified; existing F5/bootstrap remain usable. No deployment implied. |
| F9 Closure | Manifest records source+dirty-diff fingerprint, test hashes, image/DLL/served-entry, commands/devices/seeds/counts/exclusions and evidence. Independent exact-version reviews approve; no current blocking rejection. README/capstone distinguishes fresh proof from historical claims. |

**Completion is not release:** the present authority permits a reviewed local candidate/handoff, not merge/publish/deploy. The previously required post-merge signed-image/security matrix remains a release gate; it is not waived or represented as satisfied locally.

## 4. Current blockers versus stale evidence

| Status | Evidence / action |
|---|---|
| CURRENT integration closure | #163 open; its correction is already in protected dirty files. Endpoint `:629–642,739–747,875–882`; regression `AutotableWsSeatAuthorizationTests.cs:360`. Spunkmeyer validates; coordinator controls later integration. No reimplementation. |
| CURRENT missing acceptance | PR163 expressly postpones exact signed-image matrix. No full-app test/build was executed in this design review. Apone seals candidate; Hudson runs F1–F9; release proof awaits later authority. |
| CURRENT contradictory contract prose | `docs/changsha-wall-perimeter-mapping-contract.md:20,37,43,60,72,81` has bottom-first/nextTileSlots/exact-one-pitch. Vasquez reconciles documentation with final contract/tests, NOT production back to old prose. |
| CURRENT author-eligibility gap | §5 contains incomplete artifact-specific lockout recovery. Coordinator/Ripley must reconcile before corrective edits; read-only verification/new evidence can proceed. |
| HISTORICAL RED | `session-files/completion-proof/uat-200cad4/hudson/findings-uat.md:7–42` reproduces August 7 re-deal corruption. `.frost_uat_scratch/EVIDENCE_exploit_matrix.md` rejects historical :18087 action authorization. Keep regression coverage; do not announce current reproduction without testing corrected bytes. |
| HISTORICAL GREEN | August 11 :18089 report; June `STATE-OF-GAME.md:66–118`; July counts. Preserve provenance, not completion status. |
| STALE test commentary | P0 spec `:526–539` calls #134 current; August 11 report `:139–144` says fixed. Current S11 `:599–703` records several precondition revisions. Validate actual claims/S11, not old prose or assumed flakes. |
| STALE process docs | Wave-4/5 `docs/test-harness-handoff.md` counts/parallelism are historical; actual `xunit.runner.json` disables collection parallelism. Lane-discipline doc says blocking, while current #163 check is OPTIONAL-FOR-NOW. Preserve lane safety; do not alter branch protection. |

## 5. Rejections and author lockouts — no invented clearance

Ripley read own runtime history and individual shared decisions; Frost corroborated. Full `squad_state_read(decisions.md)` returned only a preview plus an oversized 187.5-KB export at a prohibited location, which was NOT opened. **The complete latest lockout roster was not recoverable in bounded form.** Local session history also showed index/data corruption; recovered checkpoints corroborate broad chronology but do not replace exact artifact verdicts. This is an internal coordination prerequisite, not a new product question or permission to bypass state tooling.

| Artifact/cycle | Known record | Required constraint |
|---|---|---|
| claim-key spec | `decisions/inbox/spunkmeyer-claim-key-collision-fix.md` explicitly locks out **Hicks**, names **Spunkmeyer**, requests Hudson re-review of SHA256 `0e44e59f…a8f396`. Current hash is `6d3db9f335fe7b563ecff0b8081cf4fc9d050c03c9f483a45def50f3d3a68564`, a DIFFERENT artifact. | Hicks may not revise/advise this cycle without later exact clearance. No approval transfer by filename or CI status. |
| PR128/WP-F | Ripley July 27 FINAL REJECT locks out **Hudson**, names Bishop runtime revision owner and a non-Hudson gate author. Later complete revision-cycle disposition not recovered. | Hudson may execute frozen product acceptance, not revise/self-approve that rejected gate/helper cycle. Bishop's historical assignment is not blanket current permission. |
| PR155 | Ripley explicitly APPROVES independent Hudson revision `28bf7da`, reversing Ferro's rejection July 27. | That specific cycle is **cleared**; not unrelated August work. |
| S11 stable tombstone | Current test `:599` identifies **Ripley as independent revision owner**; b38e1a9 changes this file only. Current SHA256 `b08174d8a22aaa18cf1100f6d29e12bf5457a3f800fc778427d56b13d17627a2`. | Ripley cannot be sole approving reviewer. Route exact-version review to Frost after author-history check. No new rejection is invented. |
| August wall/endpoint/identity/renderer cycles | Frost recovered historical wall reassignment to Apone (checkpoints 19/20, August 7), not full rejected-author lists or later clearances. Privacy-design approval is separate from action authorization. | Eligibility UNKNOWN pending exact record; no automatic re-admission of Bishop/Hicks/Wierzbowski or any historical author. |
| SC-3 proof | Runtime `Frost-SC3-literal-oracle-verified-test-pending.md`, August 7 16:03: Vasquez (a) signed; physical layer-flip RED still required then. Literal: dealer0/dice5/seed42/break18, fd0=`wall.9.1@0`, 17 frontier keys including49/51. | Historical pending proof is not a current bug. Recover combined sign/mutation record before claiming closure. |

Before any rejected-artifact revision, coordinator records hash, rejected author(s), reviewer verdict and a DIFFERENT revision author through bounded runtime state. Original authors cannot revise/pair/co-author/advise; subsequent rejection adds the next author. Shared GitHub Copilot identity is not agent authorship. Merged/green does not clear lockout.

## 6. Explicit #130 scope decision

**DEFERRED; not required for this completion.** September 9 `gh issue view 130`: OPEN, `release:backlog`, `priority:p2`, `squad:vasquez`, unchanged since July 26. Its contract requires future product/rules direction for eight inert knobs. Preserve shipped plumbing and #131's non-functional UI notice (`rule-presets.spec.ts:135`). Do not activate HandLimit, score caps, chow/kong/washout/seven-pairs flags, timeout, house-rule scoring or strict Nine Terminals. No frontend modernization or unrelated infrastructure expansion is a finish requirement.

## Investigation record

Only runtime reads, two bounded read-only consultations, relevant docs/test-contract inspection, git status/log/diffs/hashes and exact GitHub #130/#163 queries. No application, test, bundle, historical evidence, branch/index, process or deployment changes. No test/build result is claimed freshly executed. Final architecture/sequence and gating handoff continues in `sessions/2026-09-09-completion-lanes.md`.
