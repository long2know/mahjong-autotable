# Hicks — human rule-action frontend implemented and rebuilt; independent review pending

Recorded September14,2026 UTC under the assigned September12 qualification namespace. This is new own-turn/Chow/base-unit work, not a revision of rejected entry R1. Approved Ferro R2 and prior renderer/hydration behavior are preserved.

## Outcome
- Added owner-only Self-draw Hu, Concealed Kong and Added Kong controls. Availability comes only from Bishop's `ownTurn[seat]` legal metadata, never inferred from phase or concealed tile counts. Sends `ownTurn` dedicated `hu|concealedKong|addedKong` commands with the metadata runtime gameId/expectedVersion and exact four/one physical IDs for Kong, not the broken claim fallback.
- Shared one controller between eager ClientUi, late GameUi and the mobile claim overlay. Multiple Chow choices use a native accessible dialog and transmit the genuinely chosen `chowOptions` pair; a single option is direct. Cancel/Escape/expiry do not manufacture a choice or pass. Owned-seat/connection/full-snapshot/room/phase/completion guards, tombstones, local claim-window generation and pending/rejection handling retract stale UI without duplicate listeners.
- Added creation base unit (server range1..11184810, default1), persisted lobby defaults/URL/WS creation integration and game-defining change detection. Authoritative `match[0].conditions.baseUnit` is displayed in the lobby and reconciles edited existing-room URLs. No frontend scaling; original scoring display is unchanged. No preset/cap/house semantics added.

Selectors and exact command notes: `sessions/2026-09-12-hicks-actions-contract.md`.

## Actual checks / limits
- Existing strict production TypeScript: exit0.
- Existing source-only URL/activation plus approved Ferro R2 controls: **47PASS,0FAIL/0SKIP/0flaky**; no new tests/harnesses authored or changed by Hicks.
- Scoped ESLint: **exit1;14errors/1warning**, same diagnostics as exact pre-grant sources; **zero introduced**. Not all-clean.
- `npm run build` completed; final run `build-02.log` regenerated85files and dist-size via Vite/postbuild. No hand-edited dist.
- **180 protected file hashes unchanged**;21 protected declarations byte-identical including the entire approved R2 GameUi constructor, Roll/Pickup hydration/render methods, existing claim/camera key handler, cold-New-Game binding and teardown, ownership and score rendering.
- No browsers, live8950 changes, backend/container/key/volume operations, dependency/model/config/governance changes, staging/commit/branch/push, or self-approval.
- Current backend Chow metadata has source/tile/deadline/private options but no mandatory server window/version token. UI cancels every observed context/tombstone/actor/option transition and uses a local window generation; optional gameId/stateVersion are sent only if exposed. Backend legacy invalid-claim failures may use the bounded15s no-confirmation notice instead of `actionRejected`. Bishop has the concrete integration follow-up; no silent success or automatic retry.

## Exact source SHA256 (under src/frontend/autotable-src)
```text
index.html b66c64da0369c4c42d3da5a3c43d805fb6c1d9049b73936dd9a6cb5bbe827f6e
src/base-unit.ts d52d4d2b3d86473a300e73ed84b6ed4f0f128e260a15973121ba85740308e7f9
src/client-ui.ts ff8ce31e33fc10bedc381ae08db1facf0fae8f1cca736179c1c4da31e7d6b679
src/client.ts 707cf167c940193d608eab779163807c0fcf72b2002f364b40def4500dd4ccf6
src/game-ui.ts 4170bcf7066358ea1b490bcc845faa03b9c334783ee06a4a6481403502c45673
src/i18n/en.json 7b5f0cf9ccdf99fd826d3da87da47dc417a122ebc065a6780cf66f844076195c
src/i18n/zh-Hans.json 367cca0ed7cadd52d50a9dffb309ff2bd74d9afe4b4301b7507bcee5e99fcc59
src/i18n/zh-Hant.json 8d5d3e94cd394f669f977e1d22bcbf2a2ba8e453fa01c3768a62c1d186a74002
src/lobby.ts 80f0cc490236f55f7affad45f07a5525455264d282ce778436b4cf8c5db7593c
src/session-url.ts 67e84bc29e32ac83ea8a51c9afd1fc59941ffbad5da4c63abce63efd88fd88b2
src/style.css 95ce8457100a1f7c6518d1f0a40c7546e76d806952fefbd1514ca48778e0201b
src/types.ts 5f45d9d29b3c20fc3d40ab89d105b107520da683854fe360bb9acecd99d89990
src/ui/claim-window-overlay.ts 6e09a1308d58fae75f87a8ca530b4e540fda1966f6243813b77fdde9215c15b7
src/ui/rule-action-choice.ts 01765f2446009e83d06a49f8e905ac03c564f7d2ac6a199177df4bec6abd2ca3
src/ui/rule-action-controls.css 4d02cda6e11b1ed16d33d14bb25c5ff31e09e57488fce6062db53b3b4f98fd7b
src/ui/rule-action-controls.ts b8776f45fecd7fe864f305eb2282336b2dad1352762d33276f200f8066d65e54
```

## Evidence / bundle
All evidence: `session-files/qualification/2026-09-12/hicks-actions/`.
- `implementation-results.json` SHA256 **0d6e5b426ae42536962ed357138383a74ebdebef8fe4e3629eb1a99d029539a2**.
- `final-source-and-bundle-manifest.json` SHA256 **3e3e28a383e0c69674a4edca6801c32f32efbf34d9369773c6056a4a6d42981b**:16source hashes, all85generated files, dist-size, exact entry/assets.
- `review-ready.patch` SHA256 **0529a22d6dbf7105d428f312fff0af6880fcdeb0b2e02b686c440e15c8b5a71f**: diff against pre-grant approved dirty sources, not HEAD.
- Entry `autotable-src.d3557f24.js` SHA256 **5e802eac652a9a8b32219cde8b50547f74596516e0b7f2dff8b17c21a758a526**.
- Actions `rule-action-controls.e520b7c0.js` SHA256 **a937955f9106a4309dc9a9e7be7dbdc4f5b3cf62a4673ee41b3f6e1f90eb3854**.

Next required gate is independent source review, then coordinator's reviewed new image and Hudson/Ferro directed integration/browser qualification. This is NOT live completion or whole-game acceptance.

## Superseding final-contract alignment
The detailed final Bishop contract required a versioned pending/recovery and unbound-unit alignment after this handoff. Current source/bundle hashes and evidence are in `sessions/2026-09-12-hicks-actions-contract-alignment.md`. Prior handoff and evidence remain retained; all85 previous built files are preserved under the alignment's `previous-bundle/`. Timeout no longer clears/re-enables an unconfirmed flight; explicit reload recovery is provided instead. Do not pin the older bundle hashes above for the new alignment.
