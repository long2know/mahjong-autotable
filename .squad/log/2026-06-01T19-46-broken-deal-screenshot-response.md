# Session Log: Broken Deal Screenshot Response

**Date:** 2026-06-01
**Triggered by:** Stephen Long (broken-deal screenshot via Copilot)
**HEAD at completion:** `99c1af0` (Frost — column-major wall enumeration)
**Status:** 3 of 4 fixes landed; round-2 Hicks cleanup in flight

---

## What Stephen Saw

Stephen reported "the dealing seems very whacky" with auto-deal game (3 Hard bots, handCount=4):

1. Walls rendered as flat single-row strips (not 2-high stacked bricks)
2. Only ONE tile face-up in front of Seat 0 (expected: dealer's 13 + draw)
3. Gray triangular wedge artifacts at 4 corners
4. Tiny floating "Bot 1/2/3 + Seat 0" label box dead-center on table

---

## What Each Agent Did

| Agent | Commit(s) | Finding | Status |
|-------|-----------|---------|--------|
| **Drake** | `2df2e75` + `5144dc8` | PlayerProfiles.PlayerId UNIQUE race-safe upsert. Concurrent `/api/identity` requests lost the race in SELECT-then-INSERT; 20-parallel-probe fix validated. | ✅ Landed |
| **Vasquez** | `2a9adea` + `edce01d` | Repro spec + state dump. Backend is **90% innocent**: hand tiling correct (14 face-up dealer + 13×3 hidden), wall distribution **broken** (seats 0/1 hoard all 55 tiles; seats 2/3 empty). Three frontend bugs confirmed; one mid-animation shadow. | ✅ Landed |
| **Hicks** | `3560008` | Frontend mitigation: pin `gameType` from URL variant. Backend was hardcoding `FOUR_PLAYER` (Riichi) in conditions block; frontend pin forces CHANGSHA mode. Drops `thingCount` 197→109, dealer hand 1→14 face-up. **Round 2** (corner wedges, center HUD, wall gap) in flight. | ✅ Round 1 Landed |
| **Frost** | `99c1af0` | Backend fix: reorder `AutotableSlotMap.EnumerateWallSlotsInOrder` from seat-major to column-major. Seats 2/3 now get visible 2-high walls. 3 new regression tests added (per-seat distribution balance, 2-layer presence). | ✅ Landed |

---

## Final State (HEAD `99c1af0`)

**Verification checklist:**
- ✅ `thingCount` 197→109 (Riichi ghost tiles purged)
- ✅ Dealer hand 1→14 face-up
- ✅ Per-seat wall tiles balanced across all 4 seats (~13-14 each)
- ✅ Both wall layers (z=2, z=6) present at every seat
- ✅ Backend healthy on `:8088`, fresh DB, zero errors
- ✅ Screenshot post-fix: `playtest-artifacts/screenshots/hicks-deal-fixed-20260601T195953Z.png`

**Residual work (Hicks round 2 in flight):**
- Corner wedges (empty-column wall shadows from row=19 variant-agnostic hardcoding)
- Center HUD position (bot-banner + score panel CSS defaults)
- Wall gap (slot-name ordering in setup-slots.ts for Changsha 14/14/13/13 asymmetry)

---
