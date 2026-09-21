# Ripley independent entry/seat review — REJECTED revision1

Review requested September9,2026 22:07:21PDT; GPT-6Astra/max/long_context, synchronous/read-only. Author Hicks. Reviewer verified all five supplied hashes unchanged.

## FINAL SOURCE REJECT

Concrete Medium regression in src/frontend/autotable-src/src/game-ui.ts:465: initializing connectionLost from !client.connected() also marks deliberately offline relay tables disconnected. With ?variant=four_player&seat=0 and no gameId, startup never connects; flag stays true throughout local play. refreshTurnBanner exits before relay geometry, so an extra owned tile loses the discard banner/cursor cue. Changsha-only updateSeats guard does not preserve this behavior.

Reviewer executed actual before/current initialization and banner logic against identical offline relay extra-tile state: approved starting bytes emitted discard; new revision emitted an empty cue. New relay regression covers only already-connected client, missing offline relay.

Required independent revision: distinguish intentional offline relay play from a disconnected server session when initializing connection state and add a NEW offline-relay regression without editing protected existing tests.

REASSIGNED AUTHOR: Ferro. Hicks is locked out of authoring, advising or pairing on the rejected five-file entry/seat artifact for this revision cycle. S11 and prior claim-key lockouts remain unchanged. Source, new-image and whole-game acceptance remain pending.

Rejected exact hashes (paths beneath src/frontend/autotable-src/):
- src/client-ui.ts 4aad08d638cad6e6fae5831c6dbfa91cfe546d398dbe02f8983e6784ab23bb1b
- src/game-ui.ts a7a1883ecaad1dda67231378e027159dd971ce5fe6716689a78a09679ad5ee93
- src/lobby.ts 14e287e225417ff907a7cb085da2b3399f89a9b3adae6aebf1862a7d250d7bd3
- src/index.ts d6486e5eb80096c0ffd7c41772e10a371c158e7f3080962e214786bbf9547ddd
- tests/e2e/entry-seat-initialization.spec.ts 9dd8c9785ca777fa5aec2b57169e371b8396053d8cce46198213633f60334feb

Original reviewed patch8b83c8c9ac7a2ab0f654298f2ccae2d17bab07ebb844375fcfba0ad1f48b73cd; original implementation resultsc61c93d782de45c5a42f55cfef8f20e1822c082b80fd49cd0bc59a9f464b2056 remain preserved. No reviewer file/candidate mutation.
