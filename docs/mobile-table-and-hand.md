# Mobile table and hand controls

Changsha tables fit the visible screen in portrait, landscape, perspective, and
flat view. Rotating the device refits the default view; intentional zoom and
look-down controls remain independent. The WebGL canvas still fills the screen.
Full-table framing in portrait remains width-limited: the entire square table
cannot also fill a tall phone viewport without cropping.

By default on phones and compact tablets, your concealed tiles stay on the 3D board in
both portrait and landscape. Tap the desired on-board tile when the turn
indicator asks you to discard. There is no separate hand panel or reserved
hand-panel space: the table uses the available canvas, including your hand.
Manual pickup still uses the highlighted wall endpoint or the **Take** button;
the pickup prompt uses the second toolbar row in short landscape viewports
when optional table status is off, instead of reducing the board from below.
Claim choices remain in their existing overlay.

Your owned-hand tile meshes use a display-only **1.7x scale** for legibility;
physical tile IDs and authoritative slot assignments are unchanged. Changsha
also no longer displays the legacy white marker.

## Perspective and flat view

Both existing settings gears expose **Perspective view** and **Hand order**.
In the **original per-game Settings drawer** (with Bot Strength, Hand Count, Auto-Deal
and Sound), these controls are at the top. They apply immediately; do not press
**Apply & Restart** just to change your view or sorting.

The **modern app settings gear** (the square gear) keeps them under **Display**,
which opens first during compact-screen play. Uncheck **Perspective view**
for flat view; check for perspective. The camera changes immediately without
restarting the game. In short landscape viewports the drawer scrolls as needed.

The view choice is saved on this device and restored on reload/reconnect,
without needing to reopen settings. The existing **P** keyboard shortcut and
legacy view checkbox stay synchronized with this preference. Desktop keeps
the same **Settings → Display** location and existing camera framing.

## Stable framing during play

Discards, bot turns, claim countdowns/expiry and metadata refreshes do not resize
the camera's playable region. Table framing responds to viewport/orientation,
intentional view controls and explicitly opened information panels, not changing
status text. Desktop table status uses a stable footer box. Compact claim controls
retain touch-sized actions without reserving a large new hand panel; short
landscape screens place claims in the spare toolbar row.

Reconnects retain the last camera viewpoint separately from server-authoritative
seat and game state, rather than reframing the table from a temporary disconnected
state. Keeping that viewpoint does not grant or change seat ownership.

## Compact-screen overlays

Table status is hidden by default on phones and compact tablets. Enable
**Settings → Display → Show table status on compact screens** to keep the small
human/bot/open-seat summary above the table. Full table details remain in the
lobby.

Move Log and Chat default to collapsed. Their existing buttons open them on demand;
the selected panel is remembered across reloads. Opening one closes the other,
and open panels reserve board space as well as keeping the hand clear: a
scrollable band on portrait screens, or a side panel in landscape. The table
refits around them rather than rendering tiles underneath.
Desktop keeps a separate chat-collapse preference.

On desktop, **× — Collapse chat** closes the expanded chat panel. The **Chat**
header with its upward arrow reopens it; both are keyboard-accessible. Collapsing
keeps messages and remembers the desktop choice across reloads, separately from
mobile's Chat/Move Log preference.

Chat shows the localized selected channel on initial render. Its dropdown has
readable options, stays within the viewport, and supports keyboard selection.
Reopening restores message scrolling through the nested panel so retained
messages are visible, not merely present in the DOM. Drafts, the selected channel,
and the position for reading older messages are preserved; room/private chat and
server-wide online players and invitations keep their existing scopes.

Turn, claim and pickup prompts are never hidden by these preferences. Settings
and game-result dialogs remain explicit overlays.

## Hand results and Continue

After a non-final hand, the result dialog shows the winner (or draw), score
changes, and winning hand. Every human who participated must press **Continue**.
The server holds the actual next deal until all required players confirm;
closing the backdrop or pressing Escape does not acknowledge it.

The winning hand uses the same graphical tile faces as the board, with localized
tile names for assistive technology. Tiles retain the server's order and count,
including melds and all four tiles of a kong; your hand-sort preference does not
reorder this result. These tiles are non-interactive, and invalid tile IDs display
an unknown-tile placeholder rather than an invented face.

After you confirm, the dialog can say **Waiting for the other players**.
Reconnect or **Refresh status** restores the server's readiness without sending
an acknowledgement automatically. A rejected acknowledgement stays visible with
its reason; a corrected result can be confirmed explicitly again.

Spectators can **Dismiss** locally and never block play. Bots-only tables may
continue automatically. Final hands keep the existing match summary and
**New Game** action rather than requiring another acknowledgement.

The lobby's collapsed **Build / Version** summary shows the actual server
assembly version once `/health` responds, separately from the loaded UI build.

## Hand order

**Hand order** in the original per-game gear and **Settings → Display → Hand order**
in modern settings share the same device-saved preference, restored on
reload/reconnect. Both choices immediately rearrange your actual on-board hand
without taking space away from the table:

- **Suit + rank** (default): characters, circles, then bamboo, in rank order.
  Identical tiles stay adjacent, with a stable physical-tile tie-break.
- **Pairs / triples first**: identical groups of two, three, or four come first,
  followed by remaining singles in suit/rank order. This is presentation only,
  not a claimed meld, suggested play, or scoring hint.

A newly observed drawn tile joins the selected order and gets a blue tint on
the board. Reordering waits until pointers and claim choices are clear, so a
tile cannot change places during a tap or held press. The visible tile and its
raycast target always use the same sorted position.
The server's hand ownership, physical tile IDs, melds, and claim options do not
change. Spectator/hidden tiles are never sorted. Other (relay) variants retain
their existing manual arrangement.

These descriptions cover the completed implementation; integrated live-browser
acceptance is still pending.

## Opt-in portrait layout experiment

The separate URL flag `mobileLayout=camera` opts into a steeper seated
perspective; `mobileLayout=hybrid` also opts into a two-row concealed hand and
an explicit **Confirm discard** button. `mobileLayout=readable` keeps that hand
and replaces the in-play 3D table with labelled, upright public-tile areas.
The flag must occur exactly once with one of those values. It is preserved by Lobby Apply / Quick Match and is
independent of the existing renderer flags.

The experiment applies only to an actual connected Changsha table in a full
CSS viewport no wider than 600 pixels and taller than it is wide. Default,
desktop, landscape and relay play remain unchanged; flat view and existing
camera controls remain available.

In hybrid mode, releasing a tile tap selects it without sending a command.
Tap it again or press Escape to cancel; only the labelled discard button sends
the selected physical tile. Tiles do not move until the server confirms.
Unconfirmed commands remain locked across reconnect and expose a refresh
action rather than automatically retrying, including after returning to the
on-board hand by resizing or disabling the experiment. In that case, an
independent pending-status panel keeps **Refresh table** available for the same
validated owner until authoritative confirmation or rejection; refreshing never
resends the discard. Manual-deal partial hands are
non-discardable and retain the existing Roll / Take controls.

Existing legal claim and pickup controls, including their original countdown
nodes, move into the action area and return on exit. A current public claim or
an unambiguous observed discard can be enlarged. Continuous same-room snapshots
retain that verified public discard; an initial river snapshot or multiple new
discards do not establish their order. An empty authoritative discard turn says
**Waiting for a discard**, without asserting that it is the first discard.
Otherwise unavailable context is stated explicitly, never guessed from slot
order. Reconnect and room changes clear the observed history. Spectators
never receive a playable concealed DOM hand. If the dock cannot visibly
present the confirmed own hand, the renderer keeps the hand.

### Readable public tiles

The readable experiment shows the opposite player, left and right players,
and your public tiles in seat-labelled areas. Each area separates discards
from exposed melds, including the fourth tile of a kong. Tile faces come only
from the current server-filtered public snapshot; concealed tiles remain
backs or explicit unknown markers. The remaining wall is a count, not a
face-revealing wall display.

The current claim or verified observed discard is enlarged above the public
tiles. It shares the hybrid hand's existing public-history validation:
rejoining a populated river does not invent which tile was discarded last.
Existing claim countdowns and validated actions remain in the hand dock.

Crowded late-hand information scrolls in one public-board region rather than
shrinking every face. All four players are available without switching tabs;
not every late-hand tile is promised to fit on one phone screen. The hand and
actions stay outside that scroll region. Manual dealing keeps the existing 3D
ceremony and partial hand, then switches to readable public tiles when play
begins. Rotating to landscape or leaving the experiment restores the table.
The **3D table** button switches back to the hybrid table without restarting
the room. Perspective settings affect that 3D view, not the upright public
tiles.

For a fresh local preview, open:

```text
http://127.0.0.1:5114/autotable/?mobileLayout=readable&variant=changsha&dealMode=auto&botCount=3&botDifficulty=Easy&handCount=1&seed=12345&seat=0
```

Open **Lobby**, skip the optional Welcome card if shown, and choose
**Apply & Start**. This creates your own private room. A bare
`?mobileLayout=readable` URL is only an offline scene and cannot enable the
confirmed-player hand or public-board experiment.
New rooms start with empty discard areas; crowded comparison captures show a
later position reached by playing the real hand, not a prefilled preview.

## Opt-in compact 3D table

`tableLayout=compact` is a separate, in-play 3D presentation experiment for
Changsha on both portrait phones and desktop. It is independent of
`mobileLayout` and renderer flighting. The flag must occur exactly once with
the exact value `compact`; Lobby Apply preserves it.

The goal is to reduce the gutters between public discard and exposed-set
areas while increasing the rendered tile faces. Only display placement,
scale and framing change: the server's slots, tile IDs, rules, concealed
information and legal commands remain authoritative. Default and relay
tables retain their existing presentation, and manual dealing keeps its
ordinary wall and pickup ceremony.

Public tiles use a uniform 2x body scale. Four separate, seat-relative
discard rivers stay in the centre, inside the real two-level wall. Exposed
sets sit beside their player's concealed hand outside the wall, not in the
discard area. The actual remaining wall tiles and opponents' concealed
backs stay visible; counts supplement them rather than replacing them.
Empty discard grids remain visible before the first discard, without
inventing any discarded tiles.

The felt, shadows, camera bounds and picking volumes follow the same display
placement. The presentation preserves all four kong bodies, including the
raised fourth tile of an added kong, and never turns concealed tiles face-up.
Space for the full local hand and four ordinary flat kongs is reserved so
draws, discards, hover, selection and countdowns cannot pump the camera.
A wider sideways-set footprint is latched after pointer and claim release;
extreme capacity can therefore reduce the tile-size gain. Compact camera
baselines favor public faces while existing intentional camera controls
remain available. Readability is measured with the whole table visible,
not by hiding walls or other players.

Combine it with `mobileLayout=hybrid` to keep the two-row hand on narrow
portrait screens. Desktop retains its on-board hand. The separate
`mobileLayout=readable` public-tile dashboard remains available and does not
turn into this 3D experiment.

```text
http://127.0.0.1:5114/autotable/?tableLayout=compact&mobileLayout=hybrid&variant=changsha&dealMode=auto&botCount=3&botDifficulty=Easy&handCount=1&seed=12345&seat=0
```

Use **Lobby → Apply & Start** to create your own private room. A new room
starts with no discards; later-hand comparisons are reached through normal
play, not prefilled board state.

While a portrait hand dock is active, the PWA install pill is withheld so
it cannot cover **Confirm discard** or claim controls. Installation remains
available from the lobby, where the hand dock is inactive.
