# Changsha V1 Spec Lock — Decision Record

**Date:** 2026-05-06  
**Agent:** Vasquez (Rules Engineer)  
**Branch:** `stlong/changsha-v1`

## Summary

Revised `docs/rules/changsha-spec.md` (v1.0 → v1.1) to lock v1 implementation scope per user decisions. All 11 open questions and 8 Hudson test catalog contradictions resolved or deferred.

## V1 Scope Locked

### Tile Set
- **108 tiles:** Characters/Dots/Bamboo 1-9 × 4 each
- **Excluded from v1:** Honors (winds/dragons), flowers, wildcards (including 红中 Red Dragon)

### Winning Patterns (4 Only)
1. **Standard 4-sets-1-pair** with 258 pair rule (Small Win)
2. **七对子 (Seven Pairs)** — Big Win, concealed, any pair
3. **碰碰胡 (All Pungs)** — Big Win, 4 pungs/kongs + any pair
4. **清一色 (Full Flush)** — Big Win, all one suit, any pair

**Win methods:** Self-draw (自摸) and discard claim (点炮) only.

### Scoring (MahjongPros Authoritative)
- **Small Win:** 1/2 (non-dealer/dealer)
- **Big Win self-draw:** 3/4
- **Big Win discard:** 6/7
- Base unit configurable (default: 10 points → 10/20/30/40/60/70 payments)
- Dealer bonus: +1 when dealer is winner or payer

### Claim Priority
- **Multi-claim:** Win > Kong = Pung > Chow
- **Multiple Hu:** Proximity rule (closest counterclockwise wins); simultaneous wins deferred to v2

### Banker Rotation
- **Winner:** Keeps seat if currently dealer; else rotate counter-clockwise
- **Loss/Draw:** Rotate counter-clockwise

### Game Length
- **16 hands:** 4 rounds × 4 hands per round
- Round wind changes every 4 hands

### Chow
- **ALLOWED** (next-seat only) — confirmed by all three sources

## Features Deferred to V2

1. **Instant wins** (Four Joys, Board Hu, Voided Suit, Six Six Straight, San Tong)
2. **Draw-based Big Wins** (Heaven, Earth, Win After Kong, Kong on Cannon, Robbing Kong, Seabed wins)
3. **Hand-based Big Wins** (All Generals, Full Beggar's Hand, Luxury Seven Pairs)
4. **Ready-kong dice roll** (dice-based kong replacement with hand-freezing)
5. **Bird-catching** (扎鸟 post-win multipliers)
6. **Kong micro-payments**
7. **Multiple simultaneous winners** (多家胡)
8. **Seabed tile choice** (pass/draw on last tile)

## Contradictions Resolved

### Hudson's 8 Catalog Contradictions

1. **Bird tile count:** 1 tile standard (MahjongPros singular reading + Baidu/Reddit confirm). **DEFERRED TO V2.**
2. **Multiple Hu resolution:** Proximity rule (closest counterclockwise). **V1 RESOLVED.**
3. **Red Dragon dealer determination:** Source error (Baidu referencing wrong variant). Use proximity or bird tile. **DEFERRED TO V2.**
4. **Instant win continuation:** Game continues (no redeal) per Baidu online rules. **DEFERRED TO V2.**
5. **Kong dice option:** V1 uses back-of-wall only. Dice-if-ready deferred to v2. **V1 RESOLVED.**
6. **Scoring model:** MahjongPros 1/2/3/4/6/7 unit model with configurable base (default 10). **V1 RESOLVED.**
7. **Seven Pairs 258 pair:** "Random eye" exemption (any pair). Big Wins exempt from 258 rule. **V1 RESOLVED.**
8. **Full Beggar chow:** Chow allowed in exposed melds (not a contradiction; chow IS allowed in Changsha). **DEFERRED TO V2.**

### Original Spec Open Questions (11 Items)

All 11 questions from original spec §9 resolved as RESOLVED, DEFERRED-V2, or clarified. No STILL-OPEN questions remain.

## Spec Changes

### Sections Revised
1. **§1 Tile Set:** Explicit 108 count, v1 scope notes, no honors/flowers/wildcards
2. **§3.2 Claiming Discards:** Chow confirmed allowed (v1 scope note)
3. **§3.3 Claim Priority:** Proximity rule locked, multiple Hu deferred to v2
4. **§3.4.4 Ready Kong Dice:** Deferred to v2 with reference note
5. **§4 Winning:** Restructured — v1 patterns only (4 types), v2 deferred list, instant wins excluded
6. **§5 Scoring:** Locked MahjongPros model, 10 worked examples added, bird/kong/multiple-Hu deferred
7. **§6 Game End:** 16-hand lock, banker rotation clarified with examples
8. **§7 State Machine:** Simplified to v1 flow (no instant wins, no bird-catching, no seabed choice)
9. **§9 Open Questions:** All 11 marked RESOLVED or DEFERRED-V2
10. **§10 Hudson Contradictions:** NEW section resolving all 8 contradictions
11. **§11 Assumptions:** Trimmed to v1 scope (10 items, v2 items removed)
12. **§12 V1 Conformance Checklist:** NEW — 60-item build-complete checklist for Bishop/Hudson

### Header Updated
- Version: 1.0 → 1.1 (v1 Locked Scope)
- Status: "Draft — pending product review" → "V1 LOCKED — Implementation-ready baseline"
- Date: 2026-04-22 → 2026-05-06 (updated)

## Deliverables

1. **`docs/rules/changsha-spec.md`** — v1.1, fully locked, no ambiguities
2. **`.squad/decisions/inbox/vasquez-v1-spec-lock.md`** — this decision record
3. **`.squad/agents/vasquez/history.md`** — updated with v1 lock summary

## Ambiguities Remaining

**ZERO.** All rules for v1 scope are unambiguous and implementation-ready. Bishop and Hudson can proceed with confidence using §12 conformance checklist as the acceptance contract.

## Notes for Bishop

- Conformance checklist (§12) is the implementation contract
- All v1 patterns use standard 4+1 or 7-pair validation
- Big Wins exempt from 258 pair rule (any pair allowed for 七对子/碰碰胡/清一色)
- Banker rotation: keep seat on dealer win, rotate CCW otherwise
- No contract file (`changsha-signalr-contract.md`) found at time of spec revision

## Notes for Hudson

- Test catalog contradictions resolved (§10)
- 4 win patterns only: standard 4+1 (258 pair), Seven Pairs, All Pungs, Full Flush
- No instant wins, no seabed, no robbing-kong in v1 scope
- Proximity rule for multiple Hu claims (not simultaneous wins)
- Bird-catching tests should be marked v2-only

---

**Precision:** This spec revision eliminates all ambiguity from v1 scope. The locked contract enables deterministic, testable implementation with clear v2 expansion path.
