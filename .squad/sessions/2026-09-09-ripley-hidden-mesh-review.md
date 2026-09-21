# Independent hidden physical-mesh source review

Ripley (code-review, sync; GPT-6 Astra/max/long_context) issued FINAL APPROVE for all three Dietrich-frozen files, no significant issues found. The specialist verdict was response-only; coordinator persists it here.

- `src/frontend/autotable-src/src/world.ts`: SHA256 `fc4d1e1f92fe1a4a337e398df5f016db3b19d3e51333228814b8a3a3324d2344`.
- `src/frontend/autotable-src/src/thing-group.ts`: SHA256 `b323b39d4fcf336c9ad657c23ad74a4fbd295b3c92b601a91d28e1713003e3b1`.
- `src/frontend/autotable-src/tests/e2e/renderer-hidden-mesh-visibility.spec.ts`: SHA256 `7e2c0b28716cccf4db55727128b82343e2455ece3cfc9b9fb26a4b9046a3b5b9`.
- Independently verified review-freeze manifest: SHA256 `a545c574df25ac878c9e26192ddb6177cd2f932ea726b0969a98b465671fafb9`.

Actual C01 browser diagnosis had confirmed108 logically hidden custom meshes still visible and108 nonzero on-table instances. Dietrich's root fix supplies disappearance transitions, clears custom/instance geometry and restores reveals at unchanged cached transforms. Source checks are green and exact new browser cases discriminate against unchanged C01. This approval clears the renderer source prerequisite for Apone's C02 rebuild; it is not fixed-bundle browser or gameplay acceptance.

C01 remains immutable RED. C02 must include both this reviewed renderer correction and Frost-approved Burke endpoint correction. Read-only manual-start classification continues; two subsequent C01 rooms exposed valid RollingDice/dealer0/enabled Roll, so no new runtime source correction is assumed.
