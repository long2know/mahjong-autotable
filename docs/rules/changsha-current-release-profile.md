# Current Changsha release profile

> **Adopted scope addendum, not implementation or completion approval.** This
> records the user-selected release scope and the coordinator-adopted
> current-profile Kong disposition. Exact-version implementation, fixture and
> integration evidence remain required. It does not replace or erase the
> historical canonical specification.

## Authority and scope

The user decision `current_changsha_rules`, reported as recorded at
**2026-09-14T04:01:25Z**, is:

> Finish the current Changsha ruleset; keep undefined custom presets disabled and clearly labeled.

The coordinator subsequently adopted the current-profile Kong gate at
**2026-09-14T05:59:12Z**, recorded as
`decisions/inbox/coordinator-current-profile-kong-and-fixture-20260914.md`.
This adoption and its narrow fixture-revision authority are reported by the
coordinator's instruction; they are not inferred from test results.

The binding arithmetic and supported patterns remain the current SpecPure
profile in `changsha-spec.md`, including the section 5.1 examples and section
4.2.2 contextual wins. The signed wall contract remains
`../changsha-wall-perimeter-mapping-contract.md`.

Selecting the current profile does not waive canonical defects, actual human
Hu/Kong/Chow controls, configurable base unit, 120 distinct four-hand matches,
portable Docker proof or independent review. The existing seeded qualification
plan is unchanged by the calendar date.

## Normal own-turn action gate

The server, not the frontend, determines availability. All normal action paths
must establish the authorized active seat, the current game/version,
`AwaitingDiscard`, no open claim or completed game, and **14 effective tiles**
(`concealed count + 3 * meld count`).

The action-specific conditions are different:

| Action | Current-profile condition |
|---|---|
| Self-draw Hu | Valid winning hand and an actual own draw on this turn. `LastDrawSeatIndex` must identify the actor. |
| Concealed Kong | An existing canonical held-four candidate, with the normal action gate above. |
| Added Kong | An existing exposed Pung and its held matching fourth-tile candidate, with the normal action gate above. |

### Adopted Kong rule for current-profile retention

Do not silently extend Hu's new actual-draw requirement into a blanket Kong
restriction. The existing pure Kong candidate/declaration rules and legacy
dedicated runtime behavior admit the appropriate ready post-Pung/Chow state.
The new advertised-action layer must not hide those same candidates merely
because it groups Kong controls with Hu.

This is the adopted **retention rule for this application's current
profile**, not a claim about every regional variant. Section 3.4 defines Kong
operations; section 3.5 preserves the claimant's discard obligation. An
otherwise legal retained Kong may precede that owed discard, complete its
replacement or robbery continuation, and then return to the discard/terminal
flow. Pung/Chow alone does not grant self-draw Hu.

A real pre-draw state at 13 effective tiles, including 7 concealed tiles after
two Chows, is not ready for either action. After a successful Kong replacement,
that real draw may grant Hu according to the normal winning-hand rules.

The pure candidate helpers are not equivalent to complete normal-command
validation: ownership, version, phase and readiness still belong at the shared
server boundary. WS, bot and legacy normal-command paths must agree. Frontend
controls only render advertised options; they must not infer a second local
Kong policy.

Any stricter post-claim Kong variant needs an explicit further decision. The
disabled `AllowConcealedKongPromotion` preset does not choose it. The current
Kong policy is settled. Metadata/runtime alignment and actual post-claim
actions require evidence on an identified source pin; the later passing
witness below does not approve subsequent source changes. Historical
synthetic fixtures were not the authority for the adoption.

### Existing state fields

Keep `LastDrawSeatIndex`, `DiscardsThisHand` and `BaseUnit`. No parallel
`OwnDrawSeatIndex`, `OwnDrawTileId` or second base-unit helper is required.
Never add a synthetic-state or phase-only fallback for self-draw Hu.

### File-specific fixture revision authorities

The coordinator expanded the original narrow grant. The following are explicit
Hudson/different-author authorities, not blanket permission to rebaseline:

| Fixture item | Authorized revision | Required preservation and review |
|---|---|---|
| NEW own-turn WS method `PungWithoutOwnDraw_PreservesDiscardClaimButCannotDeclareOwnKong` | Replace the superseded strict-Kong expectation with an actual legal post-claim Kong positive and a self-Hu negative. | Preserve all other owner, pre-draw, version and privacy assertions; retain old `db327...` source; independently review the exact diff. |
| NEW two-row `HudsonBotMeldContinuationTests` | Change only the obsolete expected `DeclareWin` proposal precondition to the corrected `Discard` proposal. | Actual autonomous discard, next draw, inventory and unchanged 10-second assertions must still execute; retain the old source and failures; independently review the diff. |
| Three non-frozen `PungPromotionToKongTests` malformed setups | Replace the 18-effective, 5-effective and manual-fourth-append setups with conserved actual Pung/draw transitions to 14 effective tiles. | Preserve promotion/replacement assertions and the `1ec8801c...` before-file; independently review the exact changes. |
| Original five non-frozen self-draw setup repairs | Use actual draw preconditions within their existing individual grants. | Preserve their exact assertions and review each authorized diff separately. |
| ONE frozen legacy claim-gap `self-draw` setup | Repair only `CurrentClaimChannel_OmitsOtherwiseLegalOwnTurnActions(action:self-draw)` through a real 13-to-14 `DrawTile` transition with 108 conserved tiles. | Preserve all assertions and all ten original defect discriminators; retain the old source and 424/418/6 result; independently review the narrowly authorized diff. |

The earlier one-case grant is no longer an exhaustive list. Conversely, these
additions do not authorize unrelated frozen changes, original-author
self-rebaselining, engine-guard weakening, skips or result relabeling. Old
424/418/6 and 78/75/3 records remain immutable; actual revised-fixture results
must be recorded separately.

These authorities are time- and file-specific. The coordinator's later
**September 14, 2026, 04:00 PDT** freeze of the Medium bot oracle controlled
until the separately recorded **05:23:23 PDT** release for its one obsolete
proposal expectation. Earlier permission or peer feedback did not override
that freeze. The resulting `7c157143...` revision preserves every other byte,
including the full autonomous progression assertions and 10-second budget;
the original `10f9b705...` source and failures remain archived.

## Established defaults and conflicting descriptions

| Item | Current release disposition | What is not claimed |
|---|---|---|
| Initial dealer | Preserve absolute seat 0 for the first hand; later winner becomes dealer, washout retains dealer. | General random/wind-draw prose is not a new random-dealer feature grant. |
| Match length | Preserve default 4 and the current supported `handCount` options 1/4/8/16, with the 16-hand ceiling. | A four-hand match is not relabeled sixteen hands; no >16 tournament semantics are introduced. |
| URL spelling | `handCount` is the working public parameter. `MaxHands` is a state/internal runtime name. | Historical `?maxHands=` wording is an unsupported/stale alias, not an implemented option or a required new alias for this release. |
| Seven Pairs | Preserve the established ordinary shape behavior, including a quad counting as two pairs. | No luxury premium or newly selected distinct-pairs variant is implemented. |
| Pass-Hu | Preserve the established seat-wide restriction and current expiry behavior. | No tile-specific or alternate reset policy is silently selected. |
| Concealed information | Preserve current concealed-hand/Kong privacy and opaque foreign identities. | Older outer-two-down prose does not authorize newly revealing private ranks. |
| Scoring | SpecPure is default; fans/stacking remain informational for canonical payments. | A tested internal HouseRules capability is not a selectable implemented house profile. |
| Base unit | Canonical creation-time multiplier remains required, default 1; supported numeric input must be validated and settlement exact/zero-sum. | Optional cap or house-scoring interactions are not invented. |

The current implementation's representability bound, `11,184,810`, is a checked
integer-safety bound for the capped SpecPure match, not a custom
`MaxScorePerHand` rule.

## Optional and undefined features remain excluded

Keep all eight custom preset semantics disabled and clearly labeled:
`HandLimit`, `MaxScorePerHand`, `AllowWashout`, `AllowKongRobbing`,
`AllowConcealedKongPromotion`, `AllowSevenPairs`, `AllowChow` and preset
`BotDecisionTimeoutMs`.

CRUD/storage and a preview editor may remain, but must not imply that a chosen
preset changes gameplay. Preset-to-game propagation, custom cap allocation and
undefined toggle interactions are **excluded/unimplemented**, not passed
requirements. The active server bot timeout is separate from the inert preset
field.

Table-selectable HouseRules, strict Nine Terminals, optional early washout and
an automatic false-Hu fee trigger are not activated. A normal invalid Hu must
reject safely; the existence of a directly callable fee helper does not adopt
a policy of charging every failed or stale command.

The existing V1 exclusions for birds, opening instant-win flow, Kong
micro-payments, luxury premiums, simultaneous winners and alternate rounds
remain visible. Existing supported contextual wins are not excluded because an
older checklist still says otherwise.

P01-P08 remain recorded questions for future alternatives. The present scope
selects established defaults and excludes undefined preferences; it does not
pretend to have implemented or fully specified those alternatives.

## Evidence and remaining gates

Historical audits and failures remain under
`session-files/qualification/2026-09-12/vasquez/`, including
`rules-matrix-v2.json`, `current-profile-2026-09-14/`,
`runtime-partials-2026-09-14/` and the frozen fixture dispositions.

The adopted post-claim Kong alignment has dedicated **real claim-transition**
controls in `CurrentProfileKongGateParityTests`. They distinguish existing pure
and dedicated-runtime permission from advertised metadata; a failing parity
case is not relabeled as a pass or used to weaken a frozen test.

The **2026-09-14T06:25:55Z** source snapshot produced **17 executed cases:
12 passed, 5 failed, 0 skipped**. Three real post-claim cases (Pung to concealed
Kong, Pung to added Kong, Chow to concealed Kong) completed the existing
dedicated-runtime Kong operation with correct inventory/replacement, while
the advertised options omitted it. Four genuinely fresh-drawn WS Kong
controls passed. This confirms the gate mismatch; it is not proof that the
alignment has been implemented.

Those three failures are distinct from the two independently reproduced
runtime failures: zero-wall exposed-Kong terminal continuation (M06) and
fault-injected unoffered bot-Hu acceptance (A23). Normal invalid-Hu rejection
and the separate helper-only fee control passed.

The retained result is
`session-files/qualification/2026-09-12/vasquez/release-profile-addendum-2026-09-14/results.json`;
TRX SHA256:
`2b785a148b3458613138aafe8e1708f19ff6fd46f993a2af3f3cbadf6b152d74`.
The earlier auditor-fixture compile failure is preserved separately and did
not execute tests. No production source was changed for these controls.

M06 zero-wall exposed-Kong terminal orchestration and A23 unoffered bot-claim
validation require their separate executable discriminators. Presence of a
helper, a schema change, a passing fresh-drawn Kong case, or this document is
not their acceptance evidence.

Only actual results on identified revised hashes can close corresponding
findings. Backend WS fixtures do not substitute for source-approved frontend
controls being exercised in a real browser, and neither substitutes for the
unchanged 120-match and portable-build qualification gates.

The coordinator assigned the Chow prequeue, bot-claim validation and exposed
zero-wall Kong corrections to Bishop. Their later passing executable
discriminators remain separate from Ripley's pure-engine source review; no
overlapping runtime audit or retroactive change to the historical failures
follows.

### Later source-pinned alignment evidence

Bishop's v2 handoff, recorded at **2026-09-14T14:43:55Z**, contains one actual
**269/269 passing** selection. It includes all 45 own-turn/Chow WS cases, the
three real post-claim Kong parity cases, effective-hand controls, runtime
partials, deferred-Chow continuation and both full stock-Medium automatic
claim/discard/next-draw cases. These counts overlap the earlier focused
98-case and 267-case runs; they are not additive.

The unchanged
`PungWithoutOwnDraw_AllowsLegalKongButRejectsSelfHu` case now reaches its full
normal-WS Kong command and exact replacement/inventory assertions, while
still rejecting structurally winning SelfHu without an own draw. This is
passing implementation evidence for the adopted policy on that pin, not
merely a proposed rule or a passing fresh-draw control.

The identified runtime is `14b12539...`, the availability adapter is
`eee17c46...`, and the pure engine remains `0c8b838b...`. Full hashes and case
identities are in
`session-files/qualification/2026-09-12/bishop-actions/backend-final-current-profile-v2/final-manifest.json`
(SHA256 `ca114063c56eab4ce2349c7590e5ce641bbbb95dac79543854446c4541f97990`).
Its main TRX SHA256 is
`5bb9c984a816f43c7359cc012fa5c3d81f107207a01ed030d8e99f9ac5e4148c`.

Vasquez admitted that pin after all 54 listed source/dependency/test hashes
matched, then prepared the final isolated rules replay. Before dependency
restore or test execution, `ChangshaGameRuntime.PublicRooms.cs` changed from
`c3d77392...` to `948237a6...`, and the related recovery document also changed.
The source-integrity gate stopped the attempt: **zero tests executed**, no
restore started, and no owner changes were reverted. The expected initial
missing-assets preflight is not a test failure or a passing replay.

The admitted manifest, before/after inputs and blocked-attempt record are
preserved under
`session-files/qualification/2026-09-12/vasquez/final-coherent-rules-2026-09-14/`.
The owner's 269-case evidence remains valid for its recorded source. That
initial independent attempt required a refreshed coherent owner pin; its
zero-test outcome remains unchanged.

### Independent revalidation on the refreshed cut

Bishop's **2026-09-14T15:38:06Z** handoff supplied that refreshed cut, including
public-room partial `948237a6...`. Vasquez matched its 55 selected input
hashes and all 349 retained backend source copies before starting a separate
isolated attempt.

The actual independent run on **September 14, 2026, 09:24:15-09:27:53 PDT**
executed **653 cases: 653 passed, 0 failed, 0 skipped, no retries**. All 1,021
enumerated source/project/configuration input hashes and the file set stayed
unchanged. No test or production source was edited.

This one selection includes:

- The nine originally requested selectors: 264 cases, including the unchanged
  read-only literal wall oracle.
- All 132 dealer/dice/hand manual-auto convergence combinations and all ten
  original defect discriminators.
- Current normal-WS Hu/Kong/Chow and post-claim Kong/false-SelfHu controls,
  three real post-claim parity cases, and 26 effective-hand eligibility cases.
- All 31 BaseUnit controls, 17 claim-context cases, ten runtime partials and
  the complete deferred-Chow rejection/continuation case.
- Both full stock-Medium automatic claim/discard/next-draw cases, the separate
  twelve four-tier draw/own-turn controls, and the precisely reviewed fixture
  repairs. The four-tier controlled-claim boundary remains explicit.

The tested runtime is `14b12539...`, adapter `eee17c46...`, public-room partial
`948237a6...`, and pure engine `0c8b838b...`. Full pins, actual per-selector
counts, case results, source integrity and qualification limits are in
`session-files/qualification/2026-09-12/vasquez/final-coherent-rules-2026-09-14/run-02/verified-summary.json`.
The actual TRX SHA256 is
`60fcd8b74699eb646fff1a0f092144cd829b97a233fec324d0ec16b40bb6f140`.

This closes the requested bounded replay on that cut, not all 109
requirements or every source/caller path. In particular, D10 world geometry,
A22 added-Kong stack presentation, D17 full-state-hash replay and actual
production-entry/pointer acceptance are not established by this .NET run.
Whole-addition source/schema review, normal-container launch, portable-image
acceptance and the specified four-hand cohort remain separate. UI
r6/shared3.7/protocolv7 qualification changes are not production feature proof.

The approved pure-engine pin is StateMachine
`0c8b838b71746d4e772c651bd278a7d883b650871849f71c6cbfbe780286636f`.
Any further rules replay must use an identified coherent backend/test freeze
that remains matched through execution.
Source approval of pure engine or frontend files is not runtime, fixture,
image, portable-build or 120-match approval.
