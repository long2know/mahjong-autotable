# Public-room recovery

Changsha WebSocket room names and runtime game IDs are different identifiers.
The server persists their exact association in `AutotableRoomBindings`; it does
not recover a room by matching a seed, owner, hand, timestamp, or similar name.

## Creation and recovery

- Initial room binding and initial `ChangshaGames` snapshot commit in one EF
  `SaveChanges` transaction before the runtime is published.
- `RoomKey` is the lowercase SHA-256 digest of the UTF-8 room name. The original
  `RoomId` is retained and compared ordinally. This preserves case-sensitive room
  identity on providers with case-insensitive database collations.
- The room key is primary; runtime game ID is unique and concurrency-checked.
  The binding intentionally survives a missing runtime row, so recovery fails
  closed rather than treating deletion as permission to create a replacement.
- Changsha `JOIN` and `NEW` recover a known binding before acknowledging the room
  or projecting player-specific state. A recovered room reports `isFirst:false`.
  The first snapshot is the saved runtime state, not a redeal.
- After that snapshot, existing turn, claim-timeout, and bot scheduling resume.
  A ready 14-effective-tile hand is not drawn again; a persisted pre-draw
  13-effective-tile transition receives its required draw.
- Recovered human seats are reserved to their saved identities until a genuine
  reconnect or release. A new transport ID does not make another player's saved
  seat available, and bot filling does not replace reserved humans.
- Base unit, seed, hand limit, deal mode, and resolved bot difficulty remain the
  table's persisted configuration, not a later visitor's URL choices.

Relay variants do not use this recovery path. Their collaborative stores and
traffic are isolated from authoritative Changsha state even when room names
coincide. No frontend runtime-ID/version/private-option wire shape is changed.

New durable rooms require the existing `ChangshaRuntime:PersistSnapshots`
setting to remain enabled. With writes disabled, newly created rooms are
ephemeral and later progress is not durable. Existing stored bindings are still
resolved rather than treated as permission to create a replacement.

## Failure and legacy data

A known binding with missing, invalid, or mismatched saved state never creates a
new runtime. The originating socket receives `actionRejected/current` with
`action:"room"` and a bounded reason, followed by WebSocket close code 1011.
Database/binding failures are not reported as a successful reconnect.

Both startup hydration and on-demand recovery validate the saved state's
four indexed seats/hands, mandatory collection structure, and indexed element
bounds before constructing or publishing a recovered instance. A structurally
corrupt row is logged with a bounded `room-snapshot-invalid` reason and skipped
at startup; it cannot prevent other valid rooms or the backend from starting.
Its snapshot and durable binding remain unchanged, so a later `JOIN` or `NEW`
for that known alias still rejects with close code 1011 rather than redealing.
The check does not reconstruct missing collections, infer a legal hand, or
change game rules. Omitted legacy fields retain their existing domain defaults
when the resulting saved-state structure is valid.

Pre-fix snapshots did not record the public room name. There is no automatic
backfill. Recovering such a name requires an explicitly trusted exact
room-to-runtime mapping; inspecting a matching hand or seed is not sufficient.

An unknown room requested by a player who owns an active recovered runtime with
no durable binding is ambiguous. `JOIN` fails with
`legacy-room-binding-unavailable` instead of silently creating a replacement.
The rejection occurs before `JOINED`, creates no replacement row or binding, and
closes that socket with code 1011. A rejected, unbound socket is not a usable
transport for a later creation command.

### Explicit fresh creation

The existing wire command is `{"type":"NEW"}`. It carries no room name itself:
the normal WebSocket URL supplies the target through `?gameId=`, together with
the usual creation configuration. A deliberate New Game or Quick Match action
must open a connection for its fresh target and actually send `NEW`; minting a
different room name and sending `JOIN` is not equivalent.

After a legacy-ambiguity rejection, the user keeps the same signed identity.
An aligned entry flow can navigate to its fresh target and open a new socket;
it must not rely on sending through the rejected socket or silently turn a
failed recovery into a new game. No additional backend field is required.
Frontend-only navigation intent, if used, must not be mistaken for a server
authorization field.

`NEW` still resolves a known binding before considering creation. Retrying the
same target after a lost acknowledgement therefore restores that bound runtime,
not another table. A known binding with missing or corrupt state still rejects,
even for `NEW`. Explicit creation neither guesses a pre-fix association nor
backfills one; the original unbound legacy runtime remains separate.
`JOINED` or an unbound match-only snapshot alone is not proof that the new
runtime has been created.

### Frontend compatibility and acceptance

A frontend that implements both New Game and Quick Match by navigating to a
fresh room and then sending `JOIN` cannot escape the legacy-ambiguity rejection
for an affected user. This guard is not a backend-only compatible upgrade for
such clients. Their entry transport must be aligned and independently reviewed
before deployment; a callable `NEW` helper that the actual controls never use
does not meet that requirement.

Final browser acceptance must exercise the real New Game and Quick Match
controls after the rejection, with the original signed identity. Capture the
new connection's actual first `NEW` packet and normal seating/start sequence;
confirm that the original rows are unchanged and only the intended fresh table
is created. Also cover same-target retry/reload, ordinary nonlegacy creation,
and known-binding failures. Source or socket doubles alone do not prove that
the controls remain reachable after an early closed connection.

## Schema and rollout

The existing SQLite bootstrap adds the table/index to existing SQLite stores.
Provider-specific EF migrations and model snapshots also include the table for
SQLite, PostgreSQL, and SQL Server. No application data is backfilled by those
migrations.

Review the source/schema and use an isolated store for restart/recreate
acceptance before deployment. Compare the exact runtime ID and progressed
wall/turn/score/configuration, and assert that no extra runtime row was created.
An old binary does not understand these bindings; rolling back to it does not
preserve public-room recovery semantics merely because the new table remains.
