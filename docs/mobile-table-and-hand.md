# Mobile table and hand controls

Changsha tables fit the visible screen in portrait, landscape, perspective, and
flat view. Rotating the device refits the default view; intentional zoom and
look-down controls remain independent. The WebGL canvas still fills the screen.
Full-table framing in portrait remains width-limited: the entire square table
cannot also fill a tall phone viewport without cropping.

On phones and compact tablets, your concealed tiles stay on the 3D board in
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
