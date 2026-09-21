# Mobile table and hand controls

Changsha tables fit the visible screen in portrait, landscape, perspective, and
flat view. Rotating the device refits the default view; intentional zoom and
look-down controls remain independent. The WebGL canvas still fills the screen.

On phones and compact tablets, your concealed tiles appear in a touch-sized
hand tray (two rows on narrow screens). This replaces their small 3D hand row;
it is not an extra hand. Tap the desired tile when the turn indicator asks you
to discard.

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
Desktop layout and its separate chat preference are unchanged.

Turn, claim and pickup prompts are never hidden by these preferences. Settings
and game-result dialogs remain explicit overlays.

## Hand results and Continue

After a non-final hand, the result dialog shows the winner (or draw), score
changes, and winning hand. Every human who participated must press **Continue**.
The server holds the actual next deal until all required players confirm;
closing the backdrop or pressing Escape does not acknowledge it.

After you confirm, the dialog can say **Waiting for the other players**.
Reconnect or **Refresh status** restores the server's readiness without sending
an acknowledgement automatically. A rejected acknowledgement stays visible with
its reason; a corrected result can be confirmed explicitly again.

Spectators can **Dismiss** locally and never block play. Bots-only tables may
continue automatically. Final hands keep the existing match summary and
**New Game** action rather than requiring another acknowledgement.

The lobby's collapsed **Build / Version** summary shows the actual server
assembly version once `/health` responds, separately from the loaded UI build.

**Hand order**, also available under **Settings → Display**, is saved on this
device:

- **Suit + rank** (default): characters, circles, then bamboo, in rank order.
  Identical tiles stay adjacent, with a stable physical-tile tie-break.
- **Pairs / triples first**: identical groups of two, three, or four come first,
  followed by remaining singles in suit/rank order. This is presentation only,
  not a claimed meld, suggested play, or scoring hint.

A newly observed drawn tile joins the selected order and gets a blue outline
in the touch tray (a blue tint in the desktop row). Reordering waits until
pointers and claim choices are clear.
The server's hand ownership, physical tile IDs, melds, and claim options do not
change. Spectator/hidden tiles are never sorted. Other (relay) variants retain
their existing manual arrangement.
