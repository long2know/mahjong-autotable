# Hicks — final-contract alignment implemented and rebuilt (September14,2026 UTC)

This version supersedes the pending/retry description and bundle hashes in the earlier Hicks action handoff. It aligns the NEW action feature to Bishop's detailed final transport contract; it does not revise rejected entry R1 or the locked claim-key artifact. Prior handoffs/evidence remain intact, including all85 previously built files retained under `contract-alignment-01/previous-bundle/`.

## Concrete alignment
- Own-turn requests stay in flight on equal/lower-version availability. Pending retires only on authoritative advancement/retraction, rejection or context change. Numeric ownTurn adapter/command keys; runtime gameId is copied verbatim and expectedVersion remains mandatory.
- Claim commands use legacy STRING seat keys. Unsupported optional claim game/version fields were removed. Client-clock deadline expiry is not treated as an arbitration receipt; pending follows actual window/tombstone/phase/context changes.
- The15s notice does NOT clear pending or re-enable unconfirmed commands. It exposes `#rule-action-recovery` and `#rule-action-reload` (matching testids). Reload is a real explicit page reload, never an automatic resend. Long claim arbitration is labeled as waiting/possibly awaiting other players, not falsely reported as successful or necessarily a transport failure.
- Current-table baseUnit/URL reconciliation requires runtime turn metadata. The documented match-only/unbound placeholder unit1 no longer erases the requested creation unit. Bound rooms still display/use authoritative match.conditions.baseUnit; range/default unchanged1..11184810/default1.

Existing own-turn and chooser selectors remain unchanged. No frontend draw/hand-count legality validator, house rules, presets or payout caps were introduced.

## Actual validation and preservation
Strict production TypeScript exit0. Existing source-only URL/activation + approved R2 controls **47PASS/0FAIL/0SKIP/0flaky**. Scoped lint on the four TS paths changed by this alignment: **0errors/0warnings**; the previously recorded unrelated14errors/1warning elsewhere remain unchanged. Existing `npm run build` exit0;85 generated files, no hand-edited dist. No browser/integration/game qualification was executed by Hicks.

**180 protected file hashes and21 protected declarations remain unchanged**, including the full approved R2 GameUi constructor, Roll/Pickup hydration/rendering, key handler, cold binding/ownership and score rendering. No protected/new harness or test edit, backend edit, live8950/container/key/volume operation, dependency/model/config change or Git mutation.

## Changed alignment source SHA256
```text
index.html d786a7552bdea7f8e3a14576019958cecf5ff9c99ead9d2e2ce04be7249813fd
src/client.ts b26d5996258a96dc9a0e3b25ad0023a2e5b054613866d88367bcc8dbd9e8662f
src/i18n/en.json 4b5122fb6782bb0b1e81bb4c5c84b7fcc810a9158f4da21c98cc282261ae31da
src/i18n/zh-Hans.json e7da1b19ae105140e5aeeefd5643a11fe572d321926fcd18b01a82570d12798d
src/i18n/zh-Hant.json 78497c14b5588f6825033917f336457490c30431d82cb438f184296f41b9ce41
src/lobby.ts 42b3c1ac2faf401f4fdd223516801df66f8ee0db1b8e49b668523e28a659b0da
src/types.ts 51dd4becc1a8ec6aea132023f957fbf2465c3e1d425760af79bdfb3a06a150f1
src/ui/rule-action-controls.ts 54cc63e9c8c06b0120a9d9098a860124b48361bcab159aee65940e12d3defd32
```
Paths above are beneath `src/frontend/autotable-src/`. Other feature source files retain their earlier hashes; the new report lists all16 feature sources and all85 generated files.

## Versioned evidence and bundle
Directory: `session-files/qualification/2026-09-12/hicks-actions/contract-alignment-01/`.
- `implementation-results.json` SHA256 **fcb054603aded8ae7b266ba079ae994b2165a445252178d684dca9edf8ea9981**: full hashes/results/limits.
- `alignment.patch` SHA256 **6fc26e29cd185cc45ebab4ad1f4ef06ebfc2ec40fdc291063cf71d4d56d9b7f6**: eight-file delta against the previously completed action implementation.
- `review-ready.patch` SHA256 **4c4429a34655dbf9231fa7de2d2e7b1a6923eddee556311402901a331f7b613d**: complete action-feature source delta against pre-grant approved sources.
- Entry `autotable-src.d83ba589.js` SHA256 **5704c1d4da0fa2bd15ddb1809be7392055f3b5a61d66674d5c5080eec2140fa4**.
- Actions `rule-action-controls.8e86de1a.js` SHA256 **161cde4ab890800dc464927cbc8e75a3f0eb93d1cd56d7c77448168b8353bb4d**.

Required next gates: independent source review, then Hudson/Ferro directed behavior/browser qualification on the coordinator's reviewed new candidate. Legacy claim receipt limitations and backend post-Pung/Chow Kong disposition remain explicit shared-backend/rules considerations; no UI-only workaround or qualification credit is claimed.

## Explicit follow-up grant: versioned claim context
The approved alignment bytes above were preserved immutably before the separately authorized `contract-claim-context-02/` delta. Current new source/bundle hashes and red/green source controls are in `sessions/2026-09-14-hicks-claim-context.md`. Only claim-context binding and one new source-control spec were changed; the new bytes require independent re-review and are on HOLD. The prior alignment approval remains historical and is not approval of this additive delta.
