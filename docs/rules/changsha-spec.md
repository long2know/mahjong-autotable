# Changsha Mahjong (长沙麻将) — Canonical Rules Specification

> **Version:** 1.0
> **Author:** Vasquez (Rules Engineer)
> **Date:** 2026-04-22
> **Status:** Draft — pending product review of Open Questions
>
> **Sources cross-referenced:**
> 1. [MahjongPros Beginner's Guide](https://mahjongpros.com/blogs/how-to-play/beginners-guide-to-changsha-mahjong) — PRIMARY
> 2. [Reddit: Better Know a Variant — Changsha Mahjong](https://old.reddit.com/r/Mahjong/comments/xp6crv/) — community rules overview
> 3. [Baidu Encyclopedia — 长沙麻将](https://baike.baidu.com/en/item/Changsha%20Mahjong/36618) — Tencent QQ Game rules

---

## 1. Tile Set

### Composition

Changsha Mahjong uses **only the three numbered suits** — no winds, no dragons, no flowers, no jokers.

| Suit | Chinese | Tiles | Count |
|------|---------|-------|-------|
| Characters (万子) | Wàn | 1万–9万, 4 copies each | 36 |
| Dots / Circles (筒子) | Tǒng | 1筒–9筒, 4 copies each | 36 |
| Bamboo / Strips (条子) | Tiáo | 1条–9条, 4 copies each | 36 |
| **Total** | | | **108** |

### Tile ID Mapping (for engine)

Each physical tile maps to a unique integer ID 0–107:
- IDs 0–35: Characters 1–9 (4 copies each: IDs `[n*4 .. n*4+3]` for logical tile `n`, where `n` = 0–8 → Char 1–9)
- IDs 36–71: Dots 1–9 (logical tiles 9–17)
- IDs 72–107: Bamboo 1–9 (logical tiles 18–26)

Logical tile = `tileId / 4` (0–26). Suit = `logicalTile / 9` (0=Char, 1=Dot, 2=Bamboo). Rank = `logicalTile % 9 + 1` (1–9).

### Key Notes
- **No 红中 (Red Dragon) as wildcard (癞子):** All three sources confirm standard Changsha uses only the 108 suit tiles. Some regional/app variants include a 红中 wildcard, but this is not standard Changsha rules. Our v1 implementation does NOT include wildcards.
- **No honor tiles of any kind** — winds and dragons are fully excluded.

---

## 2. Setup & Deal Flow

### 2.1 Players

**4 players.** Seated at cardinal positions: East (东), South (南), West (西), North (北). Play proceeds **counterclockwise** (East → South → West → North in seating, but turn order is East → right → across → left when viewed from above, i.e., counterclockwise).

### 2.2 Determining the Dealer

- **First game:** Randomly assigned, or players draw wind tiles face-down to determine seating.
- **Subsequent games:** See §6 Dealer Rotation.
- The dealer is referred to as **East (东)** regardless of physical position.

### 2.3 Wall Construction

108 tiles are shuffled face-down. Each player builds one segment of the wall:

- **Total stacks:** 108 tiles / 2 tiles per stack = **54 stacks**
- **Per player:** 54 / 4 = 13.5 → not evenly divisible
- **Practical arrangement:** Two players build walls of **14 stacks** (28 tiles) and two build walls of **13 stacks** (26 tiles), for a total of 54 stacks = 108 tiles.

> **Note for digital implementation:** The wall is an ordered list of 108 tile IDs. Physical wall dimensions are a rendering concern only — the autotable frontend can distribute stacks visually as needed.

### 2.4 Dice Roll (Breaking the Wall) — CRITICAL

1. **The dealer (East) rolls two dice.**
2. **Determine the starting wall:** Starting from the dealer's own wall as position 1, count counterclockwise by the dice sum. The wall at the landing position is the "break wall."
   - Sum 1, 5, 9: Dealer's wall
   - Sum 2, 6, 10: Right of dealer's wall
   - Sum 3, 7, 11: Opposite player's wall
   - Sum 4, 8, 12: Left of dealer's wall
   - (Equivalently: `(sum - 1) % 4` maps to seat index offset from dealer.)
3. **Determine the break point:** From the right end of the identified wall, count stacks equal to the dice sum. The break point is **after** that stack (tiles to the left of the break become the start of the draw wall).
4. **Draw wall direction:** Drawing proceeds counterclockwise from the break point.

### 2.5 Initial Deal

Dealing proceeds from the break point of the draw wall:

| Round | Tiles per player | Method |
|-------|-----------------|--------|
| 1 | 4 tiles (2 stacks) | Starting with dealer, counterclockwise |
| 2 | 4 tiles (2 stacks) | Same order |
| 3 | 4 tiles (2 stacks) | Same order |
| 4 | 1 tile each | Same order |
| Dealer extra | +1 tile to dealer | Dealer only |

**Result:**
- Each non-dealer: **13 tiles**
- Dealer: **14 tiles** (13 + 1 extra draw)
- **Tiles remaining in wall after deal:** 108 − (13 × 3 + 14) = 108 − 53 = **55 tiles**

The dealer's 14th tile counts as their first draw. The dealer discards first.

### 2.6 Dead Wall

**There is no dead wall in Changsha Mahjong.** All tiles in the wall are drawable. Play continues until the last tile (the "seabed tile") is drawn or all players pass on it. (Confirmed by Reddit source and Baidu.)

### 2.7 Instant Win Check (Post-Deal)

Before normal play begins, **each player's starting hand is checked** for instant win conditions (see §4.2). If any player qualifies, they may declare an immediate win. Multiple simultaneous instant wins are allowed.

---

## 3. Turn Flow

### 3.1 Basic Turn Cycle

```
DEALER_DISCARDS → [CLAIM_WINDOW] → NEXT_PLAYER_DRAWS → [WIN_CHECK] → DISCARD → [CLAIM_WINDOW] → ...
```

Each turn:
1. **Active player has 14 tiles** (13 + drawn tile, or 14 after initial deal for dealer).
2. Player checks for self-draw win (自摸). If winning, declares **Hu**.
3. If not winning, player **discards one tile** face-up to the center.
4. **Claim window opens:** All other players may claim the discarded tile (see §3.2).
5. If no claim, the **next player counterclockwise draws** from the wall.
6. Repeat until win or wall exhaustion.

### 3.2 Claiming Discards

| Claim Type | Chinese | Who Can Claim | Requirement |
|-----------|---------|---------------|-------------|
| **Hu (胡)** | 胡 | Any player | Tile completes a winning hand |
| **Kong (杠)** | 杠 | Any player | Player holds 3 matching tiles |
| **Pung (碰)** | 碰 | Any player | Player holds 2 matching tiles |
| **Chow (吃)** | 吃 | Next player in turn order ONLY | Tile completes a sequence in the same suit |

**⚠️ IMPORTANT: Chow IS allowed in Changsha Mahjong.** All three sources confirm this. Chow is restricted to the player whose turn is next (the player immediately counterclockwise from the discarder). The only exception: a chow that completes a winning hand can be claimed by any player (effectively a Hu claim).

### 3.3 Claim Priority

When multiple players want the same discarded tile:

```
Hu > Kong = Pung > Chow
```

- **Hu** always takes precedence.
- **Kong and Pung** take precedence over Chow.
- **Multiple Hu claims:** The player closest counterclockwise from the discarder wins priority. Multiple players CAN win on the same discard (多家胡 — see Scoring).
- **Same priority tie:** Closest player counterclockwise from the discarder wins.

### 3.4 Kong Handling

#### 3.4.1 Exposed Kong (明杠 — from discard)
- Another player discards, you hold 3 matching tiles.
- Declare "Kong," claim the tile, expose all 4 tiles face-up.
- **Draw a replacement tile** from the **back end** of the wall.

#### 3.4.2 Concealed Kong (暗杠)
- You hold all 4 matching tiles in your hand (all drawn, none claimed).
- Declare the kong, expose tiles with outer two face-down (to mark concealed).
- **Draw a replacement tile** from the **back end** of the wall.
- A concealed kong **cannot be robbed.**

#### 3.4.3 Added Kong / Extended Kong (补杠)
- You have an existing exposed pung (3 claimed tiles face-up).
- You draw the 4th matching tile from the wall.
- Declare kong, add the tile on top of the pung.
- **Draw a replacement tile** from the **back end** of the wall.
- An added kong **can be robbed** (see §4.1 Robbing the Kong).

#### 3.4.4 Ready Kong with Dice Roll (Changsha-Specific)

This is a distinctive Changsha mechanic:
- When declaring a kong, if the player is in **tenpai (ready/waiting to win)**, they may choose to **roll the dice** for their replacement tile instead of drawing from the back of the wall.
- The player takes the **lower die value**, counts that many stacks from the back end of the wall (not including the last stack), and draws from that stack.
- **If the player wins** on this replacement tile → scored as a **Big Win** (杠上开花 with dice bonus).
- **If the player cannot win** → their **hand is frozen**: they must discard the replacement tile, and they cannot change their hand for the rest of the game (except to declare another win).
- **If an opponent wins** on the discarded replacement tile → also scored as a **Big Win** (杠上炮).
- The player may **choose not to roll dice** and simply draw from the back of the wall normally.

> **Implementation note:** This mechanic is a significant variant feature. For v1, recommend implementing kong replacement from back-of-wall only, with ready-kong dice roll as a follow-up feature (see Open Questions).

#### 3.4.5 Kong Payment

Per Baidu: When any kong is declared (exposed or concealed), the other three players must each pay a fee to the kong declarer. The amount is agreed upon before the game. This is separate from the final hand scoring.

> **Implementation note:** Kong payment amount is a configurable table parameter.

### 3.5 After a Claim

- After a **Pung or Chow** claim: the claiming player must discard, reducing their hand to 13 tiles in melds + hand.
- After a **Kong** claim: the claiming player draws a replacement tile from the back of the wall, then discards.
- After a **Hu** claim: the round ends.
- After any meld claim: play continues from the **claiming player**, not the next player in sequence.

### 3.6 Missed Win Rule (过胡 / 过水)

Per Baidu: If a player misses a win from a discard (chooses not to claim Hu), they are **prohibited from winning on a discard** until after they draw a tile. This restriction only applies to the specific tile they missed winning on.

---

## 4. Winning (胡)

### 4.1 Standard Winning Hand

A complete hand consists of **14 tiles** arranged as:
- **4 melds** (each a Pung, Kong, or Chow) + **1 pair (eyes/将)**

**The 258 Generals Rule (258将):** In a standard winning hand, the pair **must** be formed by tiles numbered **2, 5, or 8** from any suit. This is the defining feature of Changsha Mahjong (also called "258 Mahjong"). Examples of valid pairs: 2万2万, 5筒5筒, 8条8条.

**Win methods:**
- **Self-draw (自摸, Zìmō):** Win by drawing the completing tile from the wall yourself.
- **Discard claim (点炮, Diǎnpào / 胡):** Win by claiming another player's discarded tile.
- **Robbing the Kong (抢杠胡):** When an opponent attempts to extend a melded pung into an added kong, you may claim the added tile to win. Only applies to added (exposed) kongs, NOT concealed kongs.

### 4.2 Instant Win Conditions (开局胡 — Starting Hand Wins)

These are checked immediately after the deal, **before any discards**. The hand does NOT need to follow the standard 4-melds-and-a-pair structure. Each is scored as a **Small Win by self-draw.**

| Hand | Chinese | Condition |
|------|---------|-----------|
| **Four Joys** | 四喜 (Sì Xǐ) | Starting hand contains 4 identical tiles (a kong) |
| **All Pure / Board Hu** | 板板胡 (Bǎn Bǎn Hú) | Starting hand contains NO tiles numbered 2, 5, or 8 |
| **Voided Suit** | 缺一色 (Quē Yī Sè) | Starting hand is missing tiles from one of the three suits entirely |
| **Six Six Straight** | 六六顺 (Liù Liù Shùn) | Starting hand contains two pungs (two sets of three identical tiles) |

**Notes:**
- Multiple instant win conditions can be met simultaneously and they **stack** (e.g., Four Joys + Six Six Straight = double basic win).
- Multiple players can declare instant wins on the same deal.
- Instant wins are optional (player may choose not to declare and play on).
- **Bird tile bonus is NOT drawn** for instant wins (per MahjongPros).

### 4.3 Small Win Hands

A "Small Win" (小胡) is any standard winning hand where:
- 4 melds + 1 pair of 2/5/8 generals
- None of the Big Win special conditions apply

### 4.4 Big Win Hands

Big wins come in two categories: **draw-based** (based on how the winning tile was obtained) and **hand-based** (based on the tile composition).

#### Draw-Based Big Wins (require 258 pair)

| Hand | Chinese | Condition |
|------|---------|-----------|
| **Blessing of Heaven** | 天和 (Tiān Hé) | Dealer wins on their initial 14-tile hand (before any discard) |
| **Blessing of Earth** | 地和 (Dì Hé) | Non-dealer wins on their first drawn tile, with no open melds declared |
| **Win After Kong** | 杠上开花 (Gāng Shàng Kāi Huā) | Win by drawing a replacement tile after declaring a kong |
| **Kong on Cannon** | 杠上炮 (Gāng Shàng Pào) | Opponent wins on the tile you discard after a kong replacement draw |
| **Robbing the Kong** | 抢杠胡 (Qiǎng Gāng Hú) | Win by claiming the tile an opponent adds to extend a pung into a kong |
| **Last Tile Draw (Haidilao)** | 海底捞月 (Hǎi Dǐ Lāo Yuè) | Win by self-drawing the very last tile in the wall |
| **Last Tile Discard (Haidipao)** | 河底捞鱼 (Hé Dǐ Lāo Yú) | Win by claiming the discard of the player who drew the last wall tile |

#### Hand-Based Big Wins (do NOT require 258 pair — any pair allowed)

| Hand | Chinese | Condition |
|------|---------|-----------|
| **All Pungs** | 碰碰胡 (Pèng Pèng Hú) | 4 pungs/kongs + any pair. No sequences. May be open. |
| **All Generals** | 将将胡 (Jiāng Jiāng Hú) | Every tile in hand is a 2, 5, or 8. Any structure. May be open. |
| **Full Flush** | 清一色 (Qīng Yī Sè) | All tiles from one suit only. Any melds. May be open. |
| **Seven Pairs** | 七对子 (Qī Duì Zi) | Exactly 7 pairs. Must be concealed. |
| **Full Beggar's Hand** | 全求人 (Quán Qiú Rén) | 4 open melds claimed from discards + waiting for last tile to win via discard. |
| **Luxury Seven Pairs** | 豪华七对 (Háo Huá Qī Duì) | 5 pairs + 1 four-of-a-kind (within the 14 tiles). Must be concealed. Scored as **Big Win × 2**. |

### 4.5 Stacking Rules

- **Multiple instant wins stack** additively (e.g., two conditions = double small win).
- **Multiple big wins stack** additively (e.g., Seven Pairs + Last Tile Draw = double big win).
- **Small wins do NOT stack with big wins** — if a hand qualifies for both, it is scored as a big win only.
- **Full Flush compounds** with other big wins: per Baidu, if a hand is Full Flush AND another big win, the score is doubled on top of the big win multiplier.

### 4.6 Seabed Tile Rules (海底牌)

When only **one tile remains** in the wall:
1. The player whose turn it is may **choose to draw or pass** to the next player.
2. If a player **draws and wins** → Big Win (Haidilao 海底捞月).
3. If a player **draws and cannot win** → must discard it. Other players may claim it for a win (Haidipao 河底捞鱼, also Big Win).
4. If all four players **pass** → hand ends in a **draw** (流局).
5. If the last tile was drawn as a **kong replacement tile**, seabed rules do not apply for that hand.

---

## 5. Scoring (番 / Fan)

### 5.1 Payment Structure

Changsha uses a **two-tier scoring system**: Small Win and Big Win.

#### Payment Table (from Reddit/MahjongPros — normalized unit system)

| Scenario | Non-Dealer Pays/Receives | Dealer Pays/Receives |
|----------|-------------------------|---------------------|
| **Small Win** | 1 | 2 |
| **Big Win (self-draw)** | 3 | 4 |
| **Big Win (discard)** | 6 | 7 |

**Payment rules:**
- **Self-draw (自摸):** ALL three opponents pay the winner.
- **Discard (点炮):** Only the **discarder** pays the winner.
- **Dealer bonus:** +1 to payment whenever the dealer is involved (either as payer or winner). This is already reflected in the table above.

#### Alternative Point Values (Baidu/Tencent QQ)

Baidu uses absolute point values (10/20/60/70) rather than unit ratios. For our implementation, we use the **unit ratio system** with a configurable base multiplier.

| Scenario | Base Unit | Dealer Bonus |
|----------|-----------|-------------|
| Small Win Self-draw | Each opponent pays 1 (dealer pays 2) | +1 for dealer |
| Small Win Discard | Discarder pays 1 (2 if dealer involved) | +1 for dealer |
| Big Win Self-draw | Each opponent pays 3 (dealer: 4) | +1 for dealer |
| Big Win Discard | Discarder pays 6 (7 if dealer involved) | +1 for dealer |

### 5.2 Bird Catching (扎鸟 Zhā Niǎo)

After a player wins, the **bird tile mechanic** activates:

1. **Draw the bird tile:** The next tile from the draw wall is revealed (1 tile in standard rules, some variants draw 2).
2. **Map the tile number to a player:**
   - 1, 5, 9 → Dealer (seat 0)
   - 2, 6 → Player to dealer's right (seat 1)
   - 3, 7 → Opposite player (seat 2)
   - 4, 8 → Player to dealer's left (seat 3)
3. **Apply the multiplier:**
   - **Discard win:** If the bird matches the **winner** OR the **discarder** → that player's payment is **doubled**.
   - **Self-draw win:** If the bird matches the **winner** → all opponents pay double. If it matches a **specific opponent** → only that opponent pays double.
4. **Two bird tiles (variant):** If two bird tiles both match the winner/discarder → payment is **tripled**.
5. **No birds for instant wins:** Bird tiles are NOT drawn for starting hand instant win conditions.
6. **Seabed bird:** If the winner won on the last tile, the last tile itself serves as the bird tile (no additional draw needed).

### 5.3 Kong Payments

Kong declarations trigger immediate micro-payments, separate from final hand scoring:
- When any kong is declared (open, concealed, or added), the other three players each pay the kong declarer an agreed-upon amount.
- This payment occurs regardless of who ultimately wins the hand.

### 5.4 Multiple Winners (多家胡)

Multiple players CAN win on the same discarded tile. Each winner is scored and paid independently by the discarder.

---

## 6. Game End & Dealer Rotation

### 6.1 Hand End Conditions

A hand (局) ends when:
1. A player declares **Hu** (win).
2. The wall is **exhausted** and no player can win (流局 — draw).
3. An **instant win** is declared post-deal (game continues after payout, per Baidu; alternatively, a fresh deal starts — see Open Questions).

### 6.2 Dealer Rotation

| Condition | Next Dealer |
|-----------|-------------|
| Single winner | Winner becomes dealer |
| Draw — someone drew last tile | Player who drew the last tile becomes dealer |
| Draw — all passed on seabed tile | Player who had first opportunity to draw the seabed tile |
| Multiple winners on discard | The discarder becomes dealer |
| Multiple instant winners | Bird tile determines dealer (per Reddit) |

### 6.3 Round Structure

- A **round (圈)** consists of enough hands for each player to be dealer once (typically 4 hands, but can be more if the dealer retains the seat by winning).
- Round wind changes after each complete rotation: East → South → West → North.
- A full **game (局)** typically consists of 4 rounds (East round, South round, West round, North round), but this is configurable.

---

## 7. State Machine

### 7.1 States

```
SEATING
  → ROLLING_DICE
    → DEALING
      → CHECKING_INSTANT_WINS
        → IN_HAND (active player has 14 tiles)
          → AWAITING_DISCARD
            → CLAIM_WINDOW_OPEN
              → CLAIM_RESOLUTION
                → [back to IN_HAND for next player or claiming player]
          → DECLARING_KONG
            → DRAWING_REPLACEMENT
              → [back to AWAITING_DISCARD]
        → SCORING
          → BIRD_CATCHING
            → PAYMENT
              → ROTATING_DEALER
                → [back to ROLLING_DICE or END_GAME]
      → WALL_EXHAUSTED (draw)
        → ROTATING_DEALER
          → [back to ROLLING_DICE or END_GAME]
  → END_GAME
```

### 7.2 State Transitions

| From | Trigger | To | Effect |
|------|---------|-----|--------|
| SEATING | All 4 players seated | ROLLING_DICE | Assign dealer (East) |
| ROLLING_DICE | Dice rolled | DEALING | Determine break point; set draw wall pointer |
| DEALING | All tiles dealt | CHECKING_INSTANT_WINS | Dealer has 14, others have 13 |
| CHECKING_INSTANT_WINS | No instant wins (or all resolved) | AWAITING_DISCARD | Active seat = dealer |
| CHECKING_INSTANT_WINS | Instant win declared | SCORING | Score instant win(s), game continues or redeals |
| AWAITING_DISCARD | Player discards tile | CLAIM_WINDOW_OPEN | Remove tile from hand, add to discard pile |
| CLAIM_WINDOW_OPEN | No claims (timeout/all pass) | IN_HAND (next player) | Next player draws from wall |
| CLAIM_WINDOW_OPEN | Hu claimed | SCORING | Round ends, score the win |
| CLAIM_WINDOW_OPEN | Pung/Kong/Chow claimed | IN_HAND (claimer) | Form meld, claimer becomes active |
| IN_HAND | Player draws tile | AWAITING_DISCARD | Add tile to hand (14 tiles) |
| IN_HAND | Self-draw Hu detected | SCORING | Player declares win |
| DECLARING_KONG | Kong declared (any type) | DRAWING_REPLACEMENT | Expose kong, queue replacement draw |
| DRAWING_REPLACEMENT | Tile drawn from back of wall | AWAITING_DISCARD | Player has 14 tiles again |
| DRAWING_REPLACEMENT | Replacement tile wins | SCORING | 杠上开花 — Big Win |
| SCORING | Score calculated | BIRD_CATCHING | Reveal bird tile(s) |
| BIRD_CATCHING | Bird resolved | PAYMENT | Apply multipliers, settle payments |
| PAYMENT | Payments settled | ROTATING_DEALER | Determine next dealer |
| ROTATING_DEALER | Not end of game | ROLLING_DICE | New hand begins |
| ROTATING_DEALER | End of game | END_GAME | Final scores tallied |
| AWAITING_DISCARD (wall=1) | Player draws last tile | SEABED_CHOICE | Player may win, discard, or pass |
| SEABED_CHOICE | Player passes | SEABED_CHOICE (next) | Offer to next player |
| SEABED_CHOICE | All pass | WALL_EXHAUSTED | Hand ends in draw |
| WALL_EXHAUSTED | — | ROTATING_DEALER | Determine next dealer per draw rules |

### 7.3 Concurrency: Claim Window Timing

The claim window must have a **deterministic resolution mechanism**:
1. All players are given the opportunity to claim simultaneously.
2. Claims are prioritized per §3.3.
3. In digital play, a configurable timeout (e.g., 10 seconds) applies. Players who don't respond within the window are assumed to pass.
4. The server resolves the highest-priority claim.

---

## 8. Composability with Autotable's Existing Engine

### 8.1 What Autotable Already Provides (Reusable)

| Component | Status | Notes |
|-----------|--------|-------|
| **Table rendering (3D)** | ✅ Working | pwmarcz/autotable frontend handles tile placement, wall display, hand rendering |
| **Tile movement animations** | ✅ Working | Frontend animates tile draws, discards, claims |
| **WebSocket/SignalR transport** | ✅ Working | Real-time state sync between backend and frontend |
| **Seat management** | ✅ Working | 4-seat model with human/bot distinction |
| **Action logging & replay** | ✅ Working | Ordered event log with state hashing |
| **Claim window framework** | ✅ Working | Priority-based claim resolution with pass/take-selected |
| **Hu detection (basic)** | ⚠️ Partial | Detects 4-melds-plus-pair but does NOT enforce 258 pair rule |
| **Chow detection** | ✅ Working | Already implemented (next-seat only) |
| **Pung/Kong detection** | ✅ Working | Count-based matching |
| **Bot discard selection** | ⚠️ Minimal | Random tile selection, no Changsha strategy |

### 8.2 What Must Change for Changsha

| Component | Current State | Required Change | Priority |
|-----------|--------------|-----------------|----------|
| **Tile count** | 136 (standard set with honors) | **108** (suits only) | 🔴 Critical |
| **Wall construction** | Simple Fisher-Yates over 136 tiles | Fisher-Yates over 108 tiles + dice roll break point | 🔴 Critical |
| **Deal flow** | 13 tiles dealt 1-at-a-time, dealer gets 14th | Batch-of-4 draw (2 stacks) × 3 rounds + 1 each + dealer extra | 🔴 Critical |
| **Hu validation** | Generic 4+1 check on 34 logical tiles | Enforce 258 pair rule + recognize special hands | 🔴 Critical |
| **Instant win detection** | Not implemented | Check 4 conditions post-deal | 🟡 High |
| **Kong replacement draw** | Draws from front of wall | Must draw from **back** of wall | 🔴 Critical |
| **Scoring** | Not implemented | Small/Big win tiers + dealer bonus | 🟡 High |
| **Bird catching** | Not implemented | Post-win bird tile reveal + multiplier | 🟡 High |
| **Seabed tile rules** | Not implemented | Pass/draw choice on last tile | 🟡 High |
| **Ready kong dice roll** | Not implemented | Dice roll for kong replacement (frozen hand on fail) | 🟢 Medium (v2) |
| **Kong payment** | Not implemented | Micro-payments on kong declaration | 🟢 Medium |
| **Dealer rotation** | Not implemented | Winner-becomes-dealer + draw rules | 🟡 High |
| **Special hand recognition** | Not implemented | All Pungs, Full Flush, Seven Pairs, etc. | 🟡 High |
| **Multiple Hu** | Not implemented | Multiple players win on same discard | 🟢 Medium |
| **Missed win rule** | Not implemented | Restrict Hu after passing on a discard | 🟢 Medium |

### 8.3 Recommended Architecture

```
┌─────────────────────────────────────────────────┐
│                   Frontend                       │
│  (autotable 3D renderer — rendering only)        │
│  Receives: projected seat-view state             │
│  Sends: user actions (discard, claim, pass)      │
└──────────────┬──────────────────────┬────────────┘
               │ WebSocket/SignalR    │
┌──────────────▼──────────────────────▼────────────┐
│                   Backend API                     │
│  ┌──────────────────────────────────────────┐    │
│  │  ChangshaRulesEngine (NEW)               │    │
│  │  - Tile set definition (108 tiles)       │    │
│  │  - Dice roll + wall break logic          │    │
│  │  - Deal flow (batch-of-4)                │    │
│  │  - 258 pair validation                   │    │
│  │  - Special hand detection                │    │
│  │  - Scoring calculator                    │    │
│  │  - Bird catching resolver                │    │
│  │  - Seabed tile rules                     │    │
│  │  - Dealer rotation                       │    │
│  └──────────────────────────────────────────┘    │
│  ┌──────────────────────────────────────────┐    │
│  │  TableStateEngine (EXISTING — refactor)  │    │
│  │  - State machine transitions             │    │
│  │  - Action logging & replay               │    │
│  │  - Claim window management               │    │
│  │  - Bot orchestration                     │    │
│  │  - State hashing & integrity             │    │
│  └──────────────────────────────────────────┘    │
│  ┌──────────────────────────────────────────┐    │
│  │  IRuleSet (NEW interface)                │    │
│  │  - GetTileCount() → 108                  │    │
│  │  - IsValidHu(hand) → bool               │    │
│  │  - GetSpecialHands(hand) → list          │    │
│  │  - CalculateScore(win) → score           │    │
│  │  - GetDealSequence() → deal plan         │    │
│  │  - GetKongReplacementSource() → back     │    │
│  └──────────────────────────────────────────┘    │
└──────────────────────────────────────────────────┘
```

**Key principle:** The backend is **authoritative** for all game state. The frontend renders from projected state only. No rules logic runs on the client.

---

## 9. Open Questions

These require explicit product direction before implementation:

1. **Instant win game flow:** When an instant win is declared post-deal, does the game continue with the same tiles (Baidu suggests this for the online version), or is it a fresh deal? MahjongPros is ambiguous. Baidu says "the game will continue afterwards and the tiles are not reshuffled."

2. **Ready kong dice roll scope for v1:** The ready kong mechanic (rolling dice for kong replacement, hand freezing on failure) is unique to Changsha but adds significant state complexity. Should we implement this in v1 or defer to v2?

3. **Bird tile count:** MahjongPros says "draw two additional tiles" as birds. Reddit says "the next tile (the bird tile)" — singular. Baidu says "the next tile drawn immediately from the wall is the bird" — also singular. Standard seems to be 1 bird tile; 2 is a variant. Which should v1 use?

4. **Kong payment amounts:** What is the base amount for kong micro-payments? Is it configurable per table? Default suggestion: 1 unit per opponent per kong.

5. **Scoring unit value:** What is 1 unit worth? Is this configurable per table (e.g., "1 unit = 1 point" or "1 unit = 10 points")? Recommend making the base unit a table configuration parameter.

6. **Big win self-draw vs discard scoring divergence:** Reddit shows different values for big win self-draw (3/4) vs big win discard (6/7). MahjongPros shows flat 6/7 for all big wins. Reddit notes "The scoring for big wins is often streamlined as 6/7 on both self-draw and discard." Which model should v1 use?

7. **Full Flush compounding:** Baidu states Full Flush doubles the score when combined with another big win. Reddit and MahjongPros treat big wins as additive stacks. Should Full Flush be treated as a doubler or as an additional additive big win?

8. **Multiple Hu priority:** When multiple players win on the same discard, Reddit says the discarder pays all winners independently. Confirm: does the discarder pay the full amount to EACH winner?

9. **San Tong (三同) instant win:** MahjongPros lists "Three Same" as an optional instant win condition (same number from all three suits, each in a pair). Should v1 include this?

10. **Dealer retention:** Some variants allow the dealer to retain their position if they win. Our sources focus on winner-becomes-dealer. Confirm: dealer always rotates to the winner, even if the current dealer wins (meaning they keep being dealer)?

11. **Physical wall rendering:** With 108 tiles, the wall doesn't divide evenly into 4 walls of equal length. Should the frontend render walls of 14/14/13/13 stacks, or use a different arrangement?

---

## 10. Assumptions

These assumptions were made where sources were silent or ambiguous:

1. **Turn direction is counterclockwise** when viewed from above (standard Chinese mahjong convention). All three sources reference "to the right" which maps to counterclockwise in standard Chinese seating.

2. **Chow is from the immediate previous player** in turn order (the player who just discarded before you, if it's your natural turn). This aligns with standard Chinese mahjong chow rules and is confirmed by all three sources.

3. **Tile IDs are 0-indexed** with logical tile = `tileId / 4` and copy index = `tileId % 4`. With 108 tiles, IDs run 0–107, logical tiles 0–26.

4. **Dealer is seat index 0** (East position). Non-dealer seats are 1 (South), 2 (West), 3 (North) — following standard Chinese cardinal direction mapping counterclockwise.

5. **Kong replacement tiles come from the back end of the wall** (opposite end from normal draws). When the wall is a list, this means drawing from index 0 while normal draws come from the end.

6. **Concealed kong cannot be robbed** — confirmed by both MahjongPros and Reddit. Only added/extended kongs can be robbed.

7. **Seven Pairs must be concealed** — all seven pairs must come from self-drawn tiles, no open melds.

8. **Luxury Seven Pairs counts as Big Win × 2** — the four-of-a-kind within the seven pairs doubles the big win payout. Only MahjongPros explicitly states this.

9. **For the missed win rule (过胡)**, the restriction is tile-specific: missing a win on a 5万 only prevents you from winning on 5万 from discards until you next draw. You can still win on other tiles or by self-draw.

10. **Multiple instant winners at deal time:** Per MahjongPros, the game continues after instant win payouts (no redeal). Each instant winner is paid independently. Bird tiles are not drawn for instant wins.

11. **Wall shuffling uses the existing Fisher-Yates algorithm** with a seed for deterministic replay. The dice roll only determines the logical break point, not the physical arrangement (which is solely a rendering concern in digital play).

12. **"All Generals" (将将胡) allows any structure** — the only requirement is that every tile in the hand is a 2, 5, or 8. It does not need to conform to 4-melds-and-a-pair (per Baidu: "Every tile in the player's hand is a Two, Five, or Eight").
