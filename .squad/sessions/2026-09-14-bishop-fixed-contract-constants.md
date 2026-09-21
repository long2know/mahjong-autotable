# Fixed integration contract constants — supersedes queued EARLY proposals

September14,2026. This records the already implemented/agreed contract, **not source approval, candidate admission or a request for another build**.

- **ownTurn gameId is REQUIRED** in metadata and commands. It is the opaque runtime identity copied from authoritative availability, NOT the URL/WS relay-room alias. Omission rejects; it is not optional.
- Metadata **stateVersion** is copied into command **expectedVersion**, a required nonnegative Int32. Own actions remain hu / concealedKong / addedKong; normal Hu omits tileIds, concealed sends4 exact physical IDs, added sends1.
- **BaseUnit is Int32, default1, inclusive1..11184810**. Shared ChangshaBaseUnit.MaxValue/Validate is authoritative; MaxValue=floor(Int32.MaxValue/(16*12)). The EARLY100000 proposal is superseded and MUST NOT be used.
- BaseUnit query is **baseUnit**; authoritative current-table numeric value is **match.conditions.baseUnit**. No phaseF wire field/object exists. Money is already scaled server-side. Creation latches; later joins/reconnect/config writes do not overwrite it. Missing legacy snapshot field defaults1.
- Legacy SignalR CreateGame keeps its3-argument entry point/default1; CreateGameWithConfig is the explicit-unit RPC. No existing preset/house/cap semantics are activated.
- Additive claim context is separate: metadata gameId/stateVersion, commands gameId/expectedVersion supplied as BOTH-or-NEITHER for legacy compatibility. This does not make ownTurn context optional.

These names/ranges match Burke's finalized contract and Hicks's completed producer-aligned UI. No change to required gameId or BaseUnit range is being made. Any future alteration needs an explicit frontend alignment request BEFORE a candidate pin.

Separate pending gates remain: post-Pung/Chow Kong eligibility disposition, targeted NEW regression completion where recorded, independent exact-source review, and coordinator-controlled candidate/browser/120-game admission. Do not infer acceptance from this constants confirmation or rebuild merely because a delayed EARLY message repeats an obsolete proposal.
