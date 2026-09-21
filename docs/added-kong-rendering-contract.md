# Added-Kong rendering contract

This is a presentation contract, not a change to Kong eligibility, robbing,
replacement draws, scoring, or concealed-tile privacy.

## Slot distinction

The existing `ThingInfo` tuple and fields are unchanged. Ordinary
`meld.{meldIndex}.{tileIndex}@{seat}` slots with tile indices `0..3` retain
their flat positions for Pung, Chow, direct exposed Kong, concealed Kong,
and relay layouts.

Only a **committed `MeldKind.AddedKong`** emits the additional
`meld.{meldIndex}.4@{seat}` slot. It places one physical tile above the
middle Pung slot (`.1`) at the same x/y centre and one `Size.TILE.z`
layer higher. The base three remain in `.0`, `.1`, `.2`; `.3` is empty.
There are still exactly four public physical IDs and one public meld group.
Slot `.4` is not a fifth tile or another meld.

The upper slot requires the base middle slot, but is not part of the flat
push/shift chain and does not add an `up` link to the base. Consequently an
ordinary Pung does not acquire wall-bottom shading. Existing rotations and
claim-source information are preserved; no concealed faces are exposed.

## Physical identity and full snapshots

The rules engine already records the committed `added-kong` event with
the actual added tile ID, then stores a sorted meld tile list. The translator
uses that event in the current hand to put the actual added copy in `.4`
and leave the original three Pung IDs in their existing slots. It does not
assume that list index 3 is the newly added copy.

The event log is part of the persisted state, so a reconnect/FULL projection
has the same layout for every viewer. Old hand events are ignored after
`tiles-dealt` or `manual-deal-begun`. Imported/historyless legacy added melds
use the highest physical ID as a deterministic display copy; all four IDs
remain present and no rule/ownership state is rewritten. Malformed added
melds with other than four distinct physical IDs fail explicitly.

An uncommitted robbing window still contains a Pung and the fourth tile in
the declarer's concealed hand: it emits no upper tile. A robbed Kong never
publishes a committed upper tile. Only a successful immediate promotion or
the actual all-pass continuation uses `.4`.

## Compatibility and consumers

All legacy flat slot names remain valid; `AutotableSlotMap.MeldSlot` still
accepts only `0..3`. `AddedKongSlot` is a separate producer helper and the
new upper geometry is Changsha-only. No new DTO, private identity, drag
state, or synthetic hold is used.

The changed server and fingerprinted frontend are a paired deployment.
An old cached renderer does not know `.4` and must refresh to the matching
bundle; forward compatibility of such an old asset is not claimed. Updated
clients continue to accept legacy flat tuples. Qualification consumers must
recognize `.4` within the existing numeric meld group and count four actual
members, rather than demand contiguous occupied indices `0..3`. Shared
protocol/UI harness changes require their own owner and review.

## Evidence boundary

`AddedKongPlacementTests` exercises conserved unit fixtures through real
draw/candidate/promotion, all-pass and robbed branches, serialized FULL
snapshots, all four owners and all added-copy identities. The real-GLB CPU
geometry tests cover all 16 seat/meld positions. These source tests are not
native/browser or current-image acceptance. A newly admitted candidate still
needs the real action-to-render/reconnect proof on a normally functioning host.
