# Ripley — Mahjong-Autotable System Audit Report

- Started: 2026-06-04T16:29:04.813Z
- Finished: 2026-06-04T16:31:40.206Z
- Base URL: http://127.0.0.1:8088
- Sqlite path: /tmp/mat-postfix.db

## Totals

- **PASS:** 39
- **FAIL:** 4
- **SKIP:** 0

## Overall Verdict — 🟡 **SHIPPABLE WITH CAVEATS** — surface findings to owning agents.

## Findings by category

### 1-lobby

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `L-1-lobby-render` | PASS | `{"pageErrors":0,"consoleErrors":0,"url":"http://127.0.0.1:8088/autotable/"}` |
| `L-2-variant-switcher` | PASS | `{"options":[{"value":"changsha","label":"Changsha (长沙麻将)","disabled":false},{"value":"four-player","label":"Riichi — 4 player (日本麻将)","disabled":false},{"value":"three-player","label":"Riichi — 3 player","disabled":false},{"value":"bamboo",` |
| `L-3-gameid-prefill` | PASS | `{"gameIdInputPresent":true,"filled":"audit-prefill-1780590547956","echo":"audit-prefill-1780590547956"}` |
| `L-4-take-seat-buttons` | PASS | `{"takeSeatCount":4}` |
| `L-5-quick-match-visible` | PASS | `{"qmVisible":true}` |
| `L-6-connect-button` | PASS | `{"count":1}` |
| `L-7-leave-seat-button` | PASS | `{"count":1}` |
| `L-8-take-seat-0` | PASS | `{"seatIdx":0,"seatTaken":0,"worldSeat":0,"thingsCount":109,"pageErrors":0,"pageErrorMessages":[]}` |
| `L-8-take-seat-1` | PASS | `{"seatIdx":1,"seatTaken":1,"worldSeat":1,"thingsCount":109,"pageErrors":0,"pageErrorMessages":[]}` |
| `L-8-take-seat-2` | PASS | `{"seatIdx":2,"seatTaken":2,"worldSeat":2,"thingsCount":109,"pageErrors":0,"pageErrorMessages":[]}` |
| `L-8-take-seat-3` | PASS | `{"seatIdx":3,"seatTaken":3,"worldSeat":3,"thingsCount":109,"pageErrors":0,"pageErrorMessages":[]}` |
| `L-9-spectator-mode` | PASS | `{"worldSeat":null,"things":109,"hand":53,"wall":35,"discard":20,"pageErrors":0,"pageErrorMessages":[]}` |
| `L-10-leave-seat` | PASS | `{"leaveVisible":true,"seatBefore":0,"seatAfter":null,"playerSeatBefore":{"playerId":"8e898f4a95a8407195ec2c4ed1fcf155","entry":{"seat":0},"clientSeat":0},"playerSeatAfter":{"playerId":"8e898f4a95a8407195ec2c4ed1fcf155","entry":null,"clientS` |
| `L-11-reconnect-after-reload` | PASS | `{"worldSeat":0,"things":109,"connected":true,"pageErrors":0}` |

### 2-variants

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `V-1-changsha-render` | PASS | `{"variant":"changsha","expectedRuntime":"ChangshaRuntime","canvasMounted":true,"thingsCount":109,"wallCount":51,"handCount":52,"connected":true,"pageErrors":0,"pageErrorMessages":[]}` |
| `V-1-changsha-bot-move` | PASS | `{"variant":"changsha","observedMovement":true,"discardAfter":6,"thingsAfter":109,"thingsBefore":109}` |
| `V-2-riichi4-render` | PASS | `{"variant":"riichi4","expectedRuntime":"Relay","canvasMounted":true,"thingsCount":109,"wallCount":55,"handCount":53,"connected":true,"pageErrors":0,"pageErrorMessages":[]}` |
| `V-2-riichi4-bot-move` | FAIL | `{"variant":"riichi4","observedMovement":false,"discardAfter":null,"thingsAfter":null,"thingsBefore":109}` |
| `V-3-riichi3-render` | PASS | `{"variant":"riichi3","expectedRuntime":"Relay","canvasMounted":true,"thingsCount":109,"wallCount":55,"handCount":53,"connected":true,"pageErrors":0,"pageErrorMessages":[]}` |
| `V-3-riichi3-bot-move` | FAIL | `{"variant":"riichi3","observedMovement":false,"discardAfter":null,"thingsAfter":null,"thingsBefore":109}` |
| `V-4-bamboo-render` | PASS | `{"variant":"bamboo","expectedRuntime":"Relay","canvasMounted":true,"thingsCount":67,"wallCount":36,"handCount":0,"connected":true,"pageErrors":0,"pageErrorMessages":[]}` |
| `V-4-bamboo-bot-move` | FAIL | `{"variant":"bamboo","observedMovement":false,"discardAfter":null,"thingsAfter":null,"thingsBefore":67}` |
| `V-5-minefield-render` | PASS | `{"variant":"minefield","expectedRuntime":"Relay","canvasMounted":true,"thingsCount":167,"wallCount":69,"handCount":0,"connected":true,"pageErrors":0,"pageErrorMessages":[]}` |
| `V-5-minefield-bot-move` | FAIL | `{"variant":"minefield","observedMovement":false,"discardAfter":null,"thingsAfter":null,"thingsBefore":167}` |

### 3-mobile

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `M-1-no-h-overflow` | PASS | `{"docW":375,"innerW":375,"delta":0}` |
| `M-2-touch-target-qm` | PASS | `{"height":44,"width":337}` |
| `M-3-touch-target-picker` | PASS | `{"height":44,"width":327}` |
| `M-4-touch-target-lobby-close` | PASS | `{"height":44,"width":44}` |
| `M-5-sidebar-160px` | PASS | `{"width":160,"cssText":"160px/none"}` |
| `M-6-safe-area-inset` | PASS | `{"lobbyTop":true,"lobbyToggle":true}` |
| `M-7-mobile-page-errors` | PASS | `{"pageErrors":0,"first":[]}` |

### 4-claim

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `C-1-claim-overlay-attached` | PASS | `{"attempts":30,"exists":true,"badgeCount":4,"types":["Pung","Chow","Kong","Hu"],"hasPassButton":true,"hasTimer":true}` |
| `C-2-claim-overlay-visible-on-synthetic` | PASS | `{"synthetic":{"ok":true,"selfSeat":0,"key":"0","payload":{"available":["Pung","Kong","Chow","Hu"],"deadline":1780590694974,"source":1,"tile":5}},"visState":{"visible":true,"innerText":"六Seat 1Claim window7.0s碰PUNGP吃CHOWC杠KONGK胡HUHClick a ch` |
| `C-3-claim-countdown-decrements` | PASS | `{"sequence":["7.0","6.4","5.8","5.2"],"numeric":[7,6.4,5.8,5.2]}` |
| `C-4-legacy-claim-buttons` | PASS | `{"pung":true,"chow":true,"kong":true,"hu":true,"pass":true}` |
| `C-5-pass-button` | PASS | `{"exists":true,"labelText":"跳过PASS"}` |

### 5-win-modal

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `W-1-modal-present` | PASS | `{"modalExists":true}` |
| `W-2-modal-renders-on-synthetic` | PASS | `{"inject":{"ok":true},"state":{"exists":true,"rendered":true,"classList":"modal show","display":"block","totalsRowCount":4,"firstRowText":"0 (E 东)Seat 0 (You)+42","headlineText":"🏆 GAME OVER 🏆","subtitleText":"4-hand match complete"}}` |
| `W-3-modal-can-close` | PASS | `{"hidden":{"ok":true},"afterHide":{"classList":"modal","display":"none"}}` |
| `W-4-fan-section-attached-conditional` | PASS | `{"hasFanSection":false}` |

### 6-db-persistence

| ID | Status | Evidence (short) |
|----|--------|------------------|
| `DB-1-schema` | PASS | `{"sqlitePath":"/tmp/mat-postfix.db","hasPlayerStatsTable":true,"lastGameAtNullable":true,"baselineRows":102,"schemaColumns":["PlayerId:TEXT NN","GamesPlayed:INTEGER NN","GamesWon:INTEGER NN","TotalScore:INTEGER NN","HighestSingleGameScore:I` |
| `DB-2-identity-endpoint` | PASS | `{"status":200,"ok":true}` |
| `DB-3-rowcount-delta` | PASS | `{"baseline":102,"after":102,"grew":false,"error":null}` |

## Failures — full evidence

### V-2-riichi4-bot-move (2-variants)

```json
{
  "variant": "riichi4",
  "observedMovement": false,
  "discardAfter": null,
  "thingsAfter": null,
  "thingsBefore": 109
}
```

### V-3-riichi3-bot-move (2-variants)

```json
{
  "variant": "riichi3",
  "observedMovement": false,
  "discardAfter": null,
  "thingsAfter": null,
  "thingsBefore": 109
}
```

### V-4-bamboo-bot-move (2-variants)

```json
{
  "variant": "bamboo",
  "observedMovement": false,
  "discardAfter": null,
  "thingsAfter": null,
  "thingsBefore": 67
}
```

### V-5-minefield-bot-move (2-variants)

```json
{
  "variant": "minefield",
  "observedMovement": false,
  "discardAfter": null,
  "thingsAfter": null,
  "thingsBefore": 167
}
```
