# Frost independent relay-origin source review

Requested September9,2026 22:00:05PDT. Reviewer Frost, GPT-6Astra/max/long_context, synchronous read-only gate. Author Bishop.

## FINAL SOURCE APPROVE

No significant issues found. The origin receives only accepted relay entries as non-full updates, preserving its submitted hand rotations. Peer filtering and the approved Changsha authorization, private-hand projection and reconnect paths remain unchanged.

Approved exact hashes:
- src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableWsEndpoint.cs: de4152b7d79b5fcbb74018fff63fcc203cf37f05c6162b9141b2f0ed0944a197
- src/backend/src/Mahjong.Autotable.Api/Autotable/AutotableGameState.cs: e99370fe2a4f5b1093838959405e3a4ba98bf491d4dea2a8151f49794fb66902
- src/backend/tests/Mahjong.Autotable.Api.Tests/Autotable/AutotableWsRelayTests.cs: 52b8655ae0194c4a2db07227da70c4663194807696b120d236de8b230859c8c5
- Preserved authorization tests: a3ad5f00cff2f9bd913011170193457baf881f7b472debaf6da98e3f268381c3

Exact baseline-relative patch SHA7a20edeeb8e550f9aa610754bd942577774ec0bcd6b6f8292e86dc21e79c1e9d. Author validation manifest f45b8d9c60bbe34f5b5922da4ca7e28a41e12e0bd9fc03b73b0c4af6d8bc5a4d retains12actualRED cases and46actualGREEN cases with0skips.

Scope: source approval only. Served-image/UI/whole-game acceptance remains pending; immutable18209 still contains the old relay defect. No reviewer source, state, browser, build or candidate mutation. Coordinator may prepare isolated new backend publication while Hicks completes UI source and Hudson uses old immutable18209.
