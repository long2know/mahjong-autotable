# Multiplayer rooms, lobby presence, invitations, and chat

This documents the Changsha backend contract approved on September 17, 2026.
Gameplay remains server-authoritative. The primary transport is `/autotable/ws`;
the existing `/hubs/changsha` SignalR hub also provides lobby metadata and social
delivery. Ordinary chat uses REST and persisted polling history, not a new hub
chat stream.

## Identity and room identifiers

Browsers bootstrap with `POST /api/identity` before opening transports. Existing
signed guests do not need an account to play, read room metadata, use chat, or
exchange invitations. A public player ID is an identifier, never a credential.
Only the signed cookie establishes identity. Legacy unverified hub sessions do
not enter the online roster and cannot subscribe to the lobby or send invites.

`ResolveExistingRoomAsync` returns `RoomReference(RoomId, RuntimeGameId)`:

- Exact, case-sensitive public aliases take precedence.
- Existing runtime GUIDs normalize to the same room and reverse-map to its alias.
- Unaliased native games retain their existing runtime GUID as a join reference.
- Lookups never create a game or binding. Existing recovery failures remain errors.
- Existing alias validation is retained: at most 64 characters, no control
  characters or leading/trailing whitespace; interior whitespace is supported.

HTTP metadata, public cards, matchmaking results, and invitations expose `RoomId`.
Runtime commands, persisted chat keys, and canonical gameplay/claim/turn IDs
continue to use `RuntimeGameId`.

The join URL is constructed from scratch:

```text
/autotable/?gameId=<encoded RoomId>&variant=changsha&join=1
```

No seat hints, creation/configuration overrides, credentials, arbitrary origin,
or fragment are carried into that URL.

## Creation, bot quota, and existing-only admission

A new alias persists its configuration, initial bot seats, and creator claim
before publishing its binding. Bot indexes are selected in ascending order,
excluding the creator's selected human seat. `bots=false` explicitly requests
zero bots. Otherwise a valid `botCount` wins; human quick play defaults to three.
Human creation supports zero through three bots; spectator creation can use four.
The existing replay initialization and `StateJson` bot flags are the quota;
there is no new quota column or recovery format.

Later URLs cannot change the bound bot seats, seed, difficulty, deal mode, base
unit, or hand cap. An aliased room rejects explicit `FillWithBots` with
`room-bot-quota-locked`; unaliased native games retain explicit fill-all behavior.
Aliased-room start requires four genuinely occupied seats: bots or connected
humans. Native explicit seat/control paths retain their legacy behavior;
automatic existing-only human admission still requires Seating.

Forwarding `join=1` to the primary WS means existing-only joining. `NEW` is
rejected; there is no creation fallback. A new human is atomically assigned an
available, non-bot, non-reserved seat in a Seating-phase room. A signed returning
owner can reconnect to a disconnected seat even after play starts. Another live
tab of that identity retains its existing binding: the new tab observes without
private-hand or command entitlement.

Accepted joins retain `JOIN -> JOINED -> UPDATE/seats`. A missing, full, or started
room rejects a new human with one of:

```json
["actionRejected", "current", {
  "action": "join",
  "reason": "room-not-found"
}]
```

The other reasons are `room-full` and `room-not-seating`. The server sends the
entry in an `UPDATE`, then closes with policy violation 1008. Recovery failures
retain their separate existing `action: "room"` errors and do not create a room.

## Metadata and public discovery

`GET /api/games/{gameId}` and `GET /api/games/{gameId}/settings` return the same
minimal `RoomMetadataDto`. Both require a signed identity; neither returns a game
snapshot, hands, connection IDs, or account details.

```typescript
interface RoomMetadataDto {
  gameId: string;
  ownerId: string | null;
  viewerIsOwner: boolean;
  phase: string;
  isPublic: boolean;
  publicName: string | null;
  voiceEnabled: boolean;
  viewerCanManageVoice: boolean;
  botCount: number;
  seatedCount: number;
  openHumanSeats: number;
  canMakePublic: boolean;
  canInvite: boolean;
}
```

Missing identity returns 401, invalid identifiers 400, and unknown rooms 404.
`viewerIsOwner` compares the verified player with the current runtime creator.
`canMakePublic` additionally requires Seating. `canInvite` requires joined
membership, a connected human seat, Seating, an available human seat, and either
a public room or current-creator ownership.

`GET /api/matchmaking/lobby` keeps `{ games: [...] }`, newest first and capped at
50. Existing fields remain; `gameId` is canonical, `seatedCount` counts connected
humans, and `botCount` plus `openHumanSeats` are added. Only public Seating rooms
with a free human seat are listed. Recovery-reserved and bot seats are never
advertised as available.

SignalR metadata RPCs:

- `SetGamePublic(gameId, isPublic, publicName?)` preserves locked creator and
  Seating checks, and returns `{ success: true, gameId, isPublic, publicName }`
  from normalized persisted state.
- `FindJoinableGame(variant?)` returns `{ matched: false }` or
  `{ matched: true, gameId }`. It does not allocate a seat; primary WS admission
  remains authoritative.
- Legacy `JoinRandom(variant?)` still allocates a seat and returns its canonical
  room ID with the existing `seatIndex`.

`POST /api/games/{gameId}/settings/voice` accepts aliases through the same
directory. Its existing account-session owner/admin gate and rate policy remain
unchanged. Signed-guest ownership alone does not grant voice-management access.

## Verified process-local presence

The singleton `LobbyPresenceService` tracks exact namespaced `ws:` and `hub:`
connections. Membership is recorded after actual joins/seat grants, not from
query parameters. Room switches and transport closure remove the corresponding
membership. Closing a metadata hub or duplicate observer does not release a
different transport's seat or transfer/destroy its owner's room.

One roster row represents one verified human identity across rooms, spectators,
tabs, and transports:

```typescript
interface OnlinePlayerDto {
  playerId: string;
  displayName: string;
  avatarColor: string | null;
  canReceiveInvites: boolean;
}
```

Names/colors come from server profiles. The roster has no room, seat, connection,
email, account, hand, or credential fields. Bots, placeholders, offline users,
and unverified fallback identities do not appear. A WS-only player is online
with `canReceiveInvites: false`.

`JoinLobby()` requires a verified cookie or throws `HubException("identity-required")`.
The server joins `lobby` and `lobby-player:<verifiedPlayerId>` before returning:

```typescript
{ revision: number, players: OnlinePlayerDto[], invites: TableInviteDto[] }
```

`LobbyPlayersChanged` sends `{ revision, players }` to the lobby group. Revisions
increase monotonically in one process; each event is a complete authoritative
snapshot. Clients ignore older revisions and reset the comparison on reconnect.
First/last connection, invite-delivery availability, and profile changes publish
updates. Reconnect must invoke `JoinLobby` again. Presence is empty after restart;
there is no database last-seen heuristic or new partition-timeout promise.

## Targeted invitations

`SendTableInvite(gameId, recipientPlayerId)` returns either
`{ success: true, invite: TableInviteDto }` or `{ success: false, reason }`.
Reasons are `identity-required`, `not-allowed`, `room-not-found`,
`room-not-seating`, `room-full`, `recipient-offline`, `self-invite`,
`rate-limited`, and `inbox-full`.

The sender must own a connected human seat and have actual membership. Unlisted
rooms require the current creator; public rooms allow any connected seated human.
The room must still be Seating with an available human seat. The recipient must
be a different verified online identity subscribed through a lobby hub.

```typescript
interface TableInviteDto {
  inviteId: string;
  senderPlayerId: string;
  senderDisplayName: string;
  senderAvatarColor: string | null;
  recipientPlayerId: string;
  gameId: string;
  publicName: string | null;
  joinUrl: string;
  createdUtc: string;
  expiresUtc: string;
}
```

`TableInviteReceived` goes only to `lobby-player:<recipientPlayerId>`. The sender
receives the RPC result, never a room/global broadcast. All recipient tabs may
receive the event and must merge cards by `inviteId`. `JoinLobby` backfills only
that recipient's unexpired inbox.

Invitations expire after five minutes. Pending capacity is 50 per recipient and
1,000 globally, with expired entries pruned before capacity checks. An unexpired
`(sender, recipient, canonical room)` duplicate returns the original ID without
another notification or quota charge. Chat and invitations share the existing
six-sends-per-30-seconds stable-player quota through
`ChatService.TryConsumeSendQuota`.

Invitations are structured in-memory objects, not `ChatMessages` rows, access
credentials, or seat reservations. They can disappear on process restart. A
recipient explicitly choosing Join still goes through ordinary existing-room
admission, and may find that the room has filled or started.

## Ordinary chat compatibility

```text
POST /api/games/{gameId}/chat
  { channel: "table" | "spectators" | "private", recipientPlayerId?: string, body: string }
POST /api/chat/send
  { gameId: string, channel: string, recipientPlayerId?: string, body: string }
GET /api/games/{gameId}/chat?since=<ISO-8601>&limit=<1..200>
```

Both POST routes share signed-identity, actual joined-room membership, channel,
content, and quota validation. `spectator` and `private:<playerId>` remain accepted
legacy channel inputs. Spectator chat requires spectator membership; private
chat requires the recipient to be joined to the same room. Invalid channel or
conflicting recipient encodings do not fall back to table chat.

The new POST returns `ChatMessageDto`; GET returns `{ gameId, messages: [...] }`.

```typescript
interface ChatMessageDto {
  id: string;
  gameId: string;
  channel: "table" | "spectators" | "private";
  senderPlayerId: string;
  senderDisplayName: string;
  senderAvatarColor: string | null;
  recipientPlayerId: string | null;
  body: string;
  sentUtc: string;
  playerId: string; // legacy sender alias
  at: string;       // legacy time alias
}
```

New rows use runtime IDs in the existing table. History includes the bound alias
for pre-fix rows without rewriting them. Exact room/channel/private-access
filters run before the history limit: table history is member-only, spectator
history spectator-only, and private history sender/recipient-only. Results are
chronological, with each stored row returned once. The existing 280-character
limit, content filter, rate limit, and bounded history rules remain in force.
