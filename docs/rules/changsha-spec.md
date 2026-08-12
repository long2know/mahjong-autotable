# Changsha Mahjong (长沙麻将) — Canonical Rules Specification

> **Version:** 1.3 (v1 Locked Scope — Special-Context Big Wins documented)
> **Author:** Vasquez (Rules Engineer)
> **Date:** 2026-05-24 (Updated from 2026-05-13)
> **Status:** V1 LOCKED — Implementation-ready baseline
>
> **Changelog:**
> - **v1.3 (2026-05-24):** Documented special-context Big Wins now shipping in v1 (Phase H Wave 2 + Phase I Wave 1): 天和, 地和, 海底捞月, 河底捞鱼, 杠上开花, 抢杠胡. Moved them out of §4.3 (Deferred to V2) into a new §4.2.2 (Special-Context Big Wins) with engine hooks. Bishop, Phase J Wave 7 spec-drift sweep.
> - **v1.2 (2026-05-13):** Banker rotation now canonical winner-becomes-dealer per MahjongPros (was simplified `+1 mod 4` in v1.1).
> - **v1.1 (2026-05-06):** V1 scope locked. Open questions resolved. Hudson's 8 test catalog contradictions addressed.
> - **v1.0 (2026-04-22):** Initial spec.
>
> **Sources cross-referenced:**
> 1. [MahjongPros Beginner's Guide](https://mahjongpros.com/blogs/how-to-play/beginners-guide-to-changsha-mahjong) — PRIMARY (AUTHORITATIVE)
> 2. [Reddit: Better Know a Variant — Changsha Mahjong](https://old.reddit.com/r/Mahjong/comments/xp6crv/) — community rules overview
> 3. [Baidu Encyclopedia — 长沙麻将](https://baike.baidu.com/en/item/Changsha%20Mahjong/36618) — Tencent QQ Game rules
>
> **V1 Scope:** This revision locks the v1 implementation scope per user decisions (2026-05-06, refined 2026-05-13). Features deferred to v2 are explicitly marked.

---

## 1. Tile Set

### Composition

**V1 SCOPE:** Changsha Mahjong uses **exactly 108 tiles** — only the three numbered suits. NO winds, NO dragons, NO flowers, NO jokers, NO wildcards.

| Suit | Chinese | Tiles | Count |
|------|---------|-------|-------|
| Characters (万子) | Wàn | 1万, 2万, 3万, 4万, 5万, 6万, 7万, 8万, 9万 (4 copies each) | 36 |
| Dots / Circles (筒子) | Tǒng | 1筒, 2筒, 3筒, 4筒, 5筒, 6筒, 7筒, 8筒, 9筒 (4 copies each) | 36 |
| Bamboo / Strips (条子) | Tiáo | 1条, 2条, 3条, 4条, 5条, 6条, 7条, 8条, 9条 (4 copies each) | 36 |
| **Total** | | | **108** |

### Tile ID Mapping (for engine)

Each physical tile maps to a unique integer ID 0–107:
- IDs 0–35: Characters 1–9 (4 copies each: IDs `[n*4 .. n*4+3]` for logical tile `n`, where `n` = 0–8 → Char 1–9)
- IDs 36–71: Dots 1–9 (logical tiles 9–17)
- IDs 72–107: Bamboo 1–9 (logical tiles 18–26)

Logical tile = `tileId / 4` (0–26). Suit = `logicalTile / 9` (0=Char, 1=Dot, 2=Bamboo). Rank = `logicalTile % 9 + 1` (1–9).

### Key Notes
- **No 红中 (Red Dragon) wildcard (癞子) in v1:** All three sources confirm standard Changsha uses only the 108 suit tiles. Some regional/app variants include a 红中 wildcard, but this is NOT standard Changsha and is explicitly excluded from v1.
- **No honor tiles of any kind** — winds and dragons are fully excluded from v1.
- **No flowers, seasons, or bonus tiles** — v1 uses ONLY the 108 numbered suit tiles.

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

#### 2.4.1 Canonical wall-index mapping (pinned)

The steps above are made precise here to remove any intra-wall-direction ambiguity. All indices are **seat-absolute** — they do **not** rotate with the dealer (the E/S/W/N winds are dealer-relative labels only).

- **Frame (fixed):** each seat owns a fixed wall of `StacksPerSeat = [14, 14, 13, 13]` stacks by **absolute seat index** (seats 0,1 → 14 stacks / 28 tiles; seats 2,3 → 13 stacks / 26 tiles). 54 stacks, 108 tiles. No dealer rotation.
- **Break wall (step 2):** `B = (dealer + (sum − 1) mod 4) mod 4` — the absolute seat whose wall is broken.
- **Break stack (step 3):** the **sum-th stack counted from wall B's RIGHT end**, where the "right end" is the end toward the next seat counterclockwise (**seat B+1**). In render columns (0-based from the wall's **left/origin** end) this is `col = StacksPerSeat[B] − sum` — for `sum ∈ [2,12]`, `col ∈ [1,12] ⊂ [0, StacksPerSeat[B]−1]`. The break stack itself is the first tile drawn (`Wall[0]`).
- **Layer (F2 top-first):** the break frontier and every subsequent draw take the **TOP** tile of a stack first; the occluded bottom becomes reachable only after its top is drawn (the wall-perimeter reachability invariant — reachable ⇔ up-link empty, not merely `layer == 1`).
- **Draw direction (step 4):** from the break, drawing proceeds by **increasing column** (from the left/origin end toward the right/col-max end) within wall B, then **wraps counterclockwise to seat B+1**, forming one contiguous perimeter arc `B → B+1 → B+2 → B+3`.

> The physical render must realize this as a single counterclockwise perimeter run: wall B's col-max end abuts seat B+1's wall (handedness), and depletion is top-before-bottom. This mapping is the rules-owned reference for the F1 dice-anchor oracle and is mirrored in `docs/changsha-wall-perimeter-mapping-contract.md`.

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

**V1 SCOPE:** Chow IS ALLOWED in Changsha Mahjong (next-seat only). This is confirmed by all three authoritative sources.

| Claim Type | Chinese | Who Can Claim | Requirement |
|-----------|---------|---------------|-------------|
| **Hu (胡)** | 胡 | Any player | Tile completes a winning hand |
| **Kong (杠)** | 杠 | Any player | Player holds 3 matching tiles |
| **Pung (碰)** | 碰 | Any player | Player holds 2 matching tiles |
| **Chow (吃)** | 吃 | Next player in turn order ONLY | Tile completes a sequence in the same suit |

**Chow restriction:** Chow is restricted to the player whose turn is next (the player immediately counterclockwise from the discarder). Exception: a chow that completes a winning hand can be claimed by any player (effectively a Hu claim).

### 3.3 Claim Priority

**V1 SCOPE:** Multi-claim priority is locked per user decision.

When multiple players want the same discarded tile:

```
Hu > Kong = Pung > Chow
```

- **Hu** always takes precedence.
- **Kong and Pung** take precedence over Chow (same priority tier).
- **Multiple Hu claims:** Closest player counterclockwise from the discarder wins priority. **V1 IMPLEMENTATION:** Only one winner per discard (proximity rule). Multiple simultaneous wins are deferred to v2.
- **Same priority tie (Kong/Pung):** Closest player counterclockwise from the discarder wins.

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

**V1 SCOPE:** DEFERRED TO V2.

This is a distinctive Changsha mechanic where a player in tenpai (ready) may roll dice to select their kong replacement tile from a specific position in the wall, with hand-freezing penalties on failure. This adds significant state complexity and is excluded from v1.

**For v2 reference:**
- When declaring a kong in tenpai, player may choose to roll dice for replacement tile.
- Take lower die value, count from back of wall, draw from that stack.
- Win on replacement → Big Win (杠上开花 with dice bonus).
- Cannot win → hand frozen, must discard replacement tile, cannot change hand further.
- Opponent wins on discarded replacement → Big Win (杠上炮).

> **V1 Implementation:** Kong replacement always draws from back of wall (no dice option).

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

> **⚠️ Open question — tile-specific vs. seat-level lockout (surfaced by #117, NOT resolved).**
> The paragraph above is internally contradictory: "prohibited from winning on **a discard** until after they draw a tile" reads as a **blanket seat-level** lockout, while "only applies to the **specific tile** they missed" reads as a **tile-specific** lockout. The two clauses disagree about whether a seat that passed on winning tile *A* may still claim Hu on a *different* winning tile *B* before their next draw.
>
> **Current implementation (V1): seat-level.** `ChangshaGameState.MissedWinSeats` is a `HashSet<int>` of *seats* (not `(seat, tile)` pairs), so a flagged seat is blocked from Hu on **any** tile until their next own draw — including a different winning tile the tile-specific clause would allow. The lockout decays on the seat's next self-draw (`ChangshaGameStateMachine.DrawTile`) per Baidu §过水 ("until your next draw"), and clears every new hand (`Deal`).
>
> This behaviour is pinned by `MissedWinTileSpecificityCharacterizationTests` (blocks-different-tile + decay-on-draw) so switching to tile-specific semantics would be a deliberate, test-visible change. **Awaiting product direction** on which reading is canonical for Changsha before any code change.

---

## 4. Winning (胡)

**V1 SCOPE:** V1 supports the following winning patterns:
1. **Standard 4-sets-1-pair with 258 pair rule** (Small Win baseline)
2. **七对子 (Seven Pairs)** — Big Win
3. **碰碰胡 (All Pungs)** — Big Win
4. **清一色 (Full Flush)** — Big Win
5. **九幺 (Nine Terminals)** — Big Win (loose default — see §4.2.1)
6. **Special-context Big Wins** (Phase H Wave 2 / Phase I Wave 1): 天和 (Heavenly Hand), 地和 (Earthly Hand), 海底捞月 (Last Tile from Wall), 河底捞鱼 (Last Discard Catch), 杠上开花 (Win on Kong Replacement), and 抢杠胡 (Robbing the Added Kong). These are **contextual flags layered on top of a valid standard / Big Win shape** rather than independent hand patterns — they upgrade an otherwise-valid hand to Big Win pricing when the runtime context matches. See §4.4.

The remaining instant-win and special hand patterns (四喜, 板板胡, 缺一色, 六六顺, 三同, 杠上炮, 将将胡, 全求人, 豪华七对) are deferred to v2.

Win methods in v1:
- **自摸 (Self-draw):** Win by drawing the completing tile from the wall yourself.
- **点炮 (Discard claim):** Win by claiming another player's discarded tile.

### 4.1 Standard Winning Hand

A complete hand consists of **14 tiles** arranged as:
- **4 melds** (each a Pung, Kong, or Chow) + **1 pair (eyes/将)**

**The 258 Generals Rule (258将):** In a standard winning hand, the pair **must** be formed by tiles numbered **2, 5, or 8** from any suit. This is the defining feature of Changsha Mahjong (also called "258 Mahjong"). Examples of valid pairs: 2万2万, 5筒5筒, 8条8条.

This is the **Small Win** baseline pattern.

### 4.2 Big Win Hands (V1 Supported)

The following Big Win patterns are supported in v1. All of these **do NOT require a 258 pair** — any pair is allowed (confirmed by sources: "random eye" exemption for Big Wins).

| Hand | Chinese | Condition |
|------|---------|-----------|
| **All Pungs** | 碰碰胡 (Pèng Pèng Hú) | 4 pungs/kongs + any pair. No sequences. May be open. |
| **Full Flush** | 清一色 (Qīng Yī Sè) | All tiles from one suit only. Any melds (sequences allowed). May be open. |
| **Seven Pairs** | 七对子 (Qī Duì Zi) | Exactly 7 pairs. Must be concealed. |
| **Nine Terminals** | 九幺 / 九门十三幺 (Jiǔ-Yāo) | Every tile is rank 1 or rank 9 (any suit). All six distinct terminal tiles (1万 9万 1筒 9筒 1条 9条) present at least once. **Loose default** — see note below. |

#### 4.2.1 Nine Terminals — Strict vs Loose Default (Phase J Wave 4)

There are two readings of 九幺 in the wild:

- **Loose** (v1 default): every tile is rank 1 or 9 *and* all six distinct terminals appear at least once. No structural 4-sets-plus-pair requirement. The hand is recognised as a Big Win on rank-bounds + six-distinct alone (analogous to the classical 十三幺 / ThirteenOrphans shape, which bypasses decomposition by convention).
- **Strict** (not implemented in v1; reserved for a future game-options flag): every tile is rank 1 or 9 *and* the hand decomposes as 4 valid sets + 1 pair *and* all six terminals appear.

**Decision:** v1 ships the **loose** definition. Rationale:

- Matches the descriptions of 九幺 on **MahjongPros** ("Changsha Mahjong patterns") and the **Baidu Baike** entry for 长沙麻将 (section 牌型 / hand patterns), both of which frame the pattern in terms of rank-bounds + six-distinct without a strict structural clause.
- Consistent with Changsha's "random eye" exemption for Big Wins (see §4.2 intro) — Big Win shapes do not require the conventional 258-pair eye.
- Accessible to casual players and streamers; tightening to strict 4+1 over the 108-tile Changsha deck (24 physical terminal tiles total across all four copies of each of the six logical terminals) makes the pattern effectively unreachable, contradicting source descriptions that frame 九幺 as "rare but achievable".

A future tournament option (e.g. `gameOptions.nineTerminalsStrict = true`) could add the strict variant without breaking v1 behaviour; the loose path remains the default. No options surface is implemented in this wave — the door is simply left open in `Changsha/WinDetector.cs::CheckNineTerminals` doc comments.

**Source code:** `Changsha/WinDetector.cs::CheckNineTerminals` implements the loose check; `WinPatternTests.NineTerminals_RankBoundsOnly` pins the binding semantic.

### 4.2.2 Special-Context Big Wins (Phase H Wave 2 / Phase I Wave 1)

These six patterns are **contextual upgrades** rather than structural hand shapes — the engine evaluates the underlying hand for a standard or Big Win shape and **then** sets a contextual flag on the resulting `WinResult` if the runtime state matches the triggering condition. Pricing follows the Big Win schedule (3/4 self-draw, 6/7 discard) per §5.

| Pattern | Chinese | Condition | Engine Hook |
|---------|---------|-----------|-------------|
| **Heavenly Hand** | 天和 (tiān-hé) | Dealer wins by self-draw on their initial 14-tile hand with no intervening discards or claims. | `WinResult.IsHeavenlyHand` |
| **Earthly Hand** | 地和 (dì-hé) | Non-dealer wins by claiming Hu on the dealer's first discard (no intervening draws/claims). | `WinResult.IsEarthlyHand` |
| **Last Tile from Wall** | 海底捞月 (hǎi-dǐ-lāo-yuè) | Self-draw win on the very last tile drawn from the wall (wall is empty after the draw). | `WinResult.IsLastTileFromWall` |
| **Last Discard Catch** | 河底捞鱼 (hé-dǐ-lāo-yú) | Discard-claim win on the final discard after the wall is exhausted. | `WinResult.IsLastDiscardCatch` |
| **Kong Replacement Win** | 杠上开花 (gàng-shàng-kāi-huā) | Self-draw win on a tile drawn as a kong replacement (immediately after declaring concealed / added / exposed kong). | `WinResult.IsKongReplacementWin` |
| **Robbing the Added Kong** | 抢杠胡 (qiǎng-gàng-hú) | Win by claiming the fourth tile of an opponent's just-declared **added** kong (4-tile kong upgrade). Concealed kongs cannot be robbed. | `WinResult.IsRobbedKong` |

**Source code:** detection lives in `Changsha/ChangshaStateMachine.cs` (`SetSpecialContextFlags` helper at ~line 610) and `Changsha/WinDetector.cs`. Robbing-the-kong claim plumbing is in `Changsha/ClaimAdjudicator.cs` (Phase H Wave 2). Display ordering is pinned in `Changsha/Patterns/ChangshaPatternOrdering.cs`.

### 4.3 Patterns Deferred to V2

The following patterns from the canonical sources are **explicitly excluded from v1** and deferred to v2:

**Instant Win Conditions (开局胡 — Starting Hand Wins):**
- 四喜 (Four Joys) — starting hand contains a kong
- 板板胡 (All Pure / Board Hu) — no 2/5/8 tiles
- 缺一色 (Voided Suit) — missing one suit entirely
- 六六顺 (Six Six Straight) — two pungs in starting hand
- 三同 (Three Same) — same number from all three suits in pairs (optional variant)

**Draw-Based Big Wins (remaining deferred items):**
- 杠上炮 (Kong on Cannon) — opponent wins on your post-kong discard

> **Note:** The other draw-/context-based Big Wins (天和, 地和, 杠上开花, 抢杠胡, 海底捞月, 河底捞鱼) shipped in Phase H Wave 2 (抢杠胡) and Phase I Wave 1 (the remaining five). See §4.2.2 for current contracts and engine hooks.

**Hand-Based Big Wins:**
- 将将胡 (All Generals) — every tile is 2/5/8
- 全求人 (Full Beggar's Hand) — 4 open melds + win via discard
- 豪华七对 (Luxury Seven Pairs) — 5 pairs + 1 four-of-a-kind

**Rationale:** The remaining deferred patterns add significant complexity (instant win flow, seabed tile rules, dependent discard interrupts on already-completed kongs, frozen hand states) and are deferred to maintain v1 delivery focus on core gameplay loop. The special-context wins migrated to v1 (§4.2.2) were a natural follow-on once the runtime exposed the necessary game-state flags.

---

## 5. Scoring (番 / Fan)

**V1 SCOPE:** Two-tier scoring system locked per user decision. Bird-catching deferred to v2.

### 5.1 Payment Structure

Changsha uses a **two-tier scoring system**: Small Win and Big Win.

#### V1 Payment Table (Authoritative — MahjongPros Source)

| Win Type | Method | Non-Dealer Payment | Dealer Payment |
|----------|--------|-------------------|----------------|
| **Small Win** | Self-draw (自摸) | Each opponent pays 1 | Each opponent pays 2 |
| **Small Win** | Discard (点炮) | Discarder pays 1 | Discarder pays 2 |
| **Big Win** | Self-draw (自摸) | Each opponent pays 3 | Each opponent pays 4 |
| **Big Win** | Discard (点炮) | Discarder pays 6 | Discarder pays 7 |

**Payment rules:**
- **Self-draw (自摸):** ALL three opponents pay the winner.
- **Discard (点炮):** Only the **discarder** pays the winner.
- **Dealer bonus:** +1 to payment whenever the dealer is involved as winner or payer.

#### V1 Worked Examples

**Example 1: Small Win by Self-Draw (Non-Dealer Winner)**
- Non-dealer wins by self-draw
- Payment: Each of the 3 opponents pays 1 unit
- If one opponent is dealer: that opponent pays 2 units (dealer bonus)
- Total received: 1 + 1 + 2 = 4 units (or 1 + 1 + 1 = 3 if no dealer among losers)

**Example 2: Small Win by Self-Draw (Dealer Winner)**
- Dealer wins by self-draw
- Payment: Each opponent pays 2 units (dealer as winner bonus)
- Total received: 2 + 2 + 2 = 6 units

**Example 3: Small Win by Discard (Non-Dealer Winner from Non-Dealer)**
- Non-dealer wins by claiming non-dealer's discard
- Payment: Discarder pays 1 unit
- Total received: 1 unit

**Example 4: Small Win by Discard (Non-Dealer Winner from Dealer)**
- Non-dealer wins by claiming dealer's discard
- Payment: Dealer pays 2 units (dealer as payer bonus)
- Total received: 2 units

**Example 5: Small Win by Discard (Dealer Winner)**
- Dealer wins by claiming any discard
- Payment: Discarder pays 2 units (dealer as winner bonus)
- Total received: 2 units

**Example 6: Big Win by Self-Draw (Non-Dealer Winner)**
- Non-dealer wins All Pungs by self-draw
- Payment: Each opponent pays 3 units; dealer pays 4
- Total received: 3 + 3 + 4 = 10 units

**Example 7: Big Win by Self-Draw (Dealer Winner)**
- Dealer wins Seven Pairs by self-draw
- Payment: Each opponent pays 4 units (dealer as winner bonus)
- Total received: 4 + 4 + 4 = 12 units

**Example 8: Big Win by Discard (Non-Dealer Winner from Non-Dealer)**
- Non-dealer wins Full Flush by claiming non-dealer's discard
- Payment: Discarder pays 6 units
- Total received: 6 units

**Example 9: Big Win by Discard (Non-Dealer Winner from Dealer)**
- Non-dealer wins Seven Pairs by claiming dealer's discard
- Payment: Dealer pays 7 units (dealer as payer bonus)
- Total received: 7 units

**Example 10: Big Win by Discard (Dealer Winner)**
- Dealer wins All Pungs by claiming any discard
- Payment: Discarder pays 7 units (dealer as winner bonus)
- Total received: 7 units

### 5.2 Base Unit Configuration

The payment unit values (1, 2, 3, 4, 6, 7) are multipliers on a configurable **base unit**. The base unit is a table-level configuration parameter.

**Default:** base unit = **1** — payments are emitted as the raw unit values listed in §5.1 (1/2/3/4/6/7). This is the simplest configuration and matches the v1 implementation default.

Alternative configurations:
- Base unit = 10 → payments of 10/20/30/40/60/70 (matches Baidu/Tencent display model)
- Base unit = 100 → high-stakes play

V1 implementation must accept base unit as a table creation parameter, default = 1.

### 5.3 Features Deferred to V2

**Bird Catching (扎鸟 Zhā Niǎo):** Post-win bird tile mechanics where the next tile from the wall is revealed, mapped to a player by rank (1/5/9→dealer, 2/6→right, 3/7→opposite, 4/8→left), and used to apply payment multipliers (double/triple). Deferred to v2 due to:
- Source contradiction on bird count (1 vs. 2 tiles)
- Complexity in payment multiplier chaining
- Non-critical to core gameplay loop

**Contradiction resolution (for v2):** MahjongPros is authoritative — **1 bird tile** is standard (per Reddit and Baidu confirmation). Two bird tiles is a regional variant.

**Kong Payments:** Micro-payments on kong declaration (each opponent pays kong declarer immediately, separate from hand scoring). Deferred to v2 as a secondary scoring mechanic.

**Multiple Winners (多家胡):** Multiple players winning on the same discard. V1 uses proximity rule (closest counterclockwise wins). V2 may implement simultaneous multiple wins with independent payments.

### 5.4 Fan Catalog & Big-Win Stacking — Query-Only (Non-Scoring) in V1

**The binding V1 payment computation is EXACTLY the two-tier table in §5.1 — nothing more.** A winning hand pays the Small/Big-Win unit values (1/2 · 3/4 · 6/7) with the +1 dealer bonus. There is **no fan (番) table** and **no big-win stacking multiplier** in the authoritative payout.

The engine ships two *supplementary* mechanisms that are **display/query-only** with respect to the money (introduced pre-#117, gated to non-scoring by #117):

- **Fan catalog** (`Changsha/Scoring/FanCalculator.cs`): a 14-entry catalog (自摸, 清一色, 七对, 碰碰胡, 门清, 杠上开花, 海底捞月, 河底捞鱼, 抢杠, 天和, 地和, 九幺, plus two variant-gated honor fans). It is evaluated on every win and surfaced on `ScoreResult.Fans` / `FanPoints` (and the WS/SignalR `fans[]` wire field) so the frontend can render an informational fan breakdown. It does **not** alter payments.
- **Big-win stacking multiplier** (`Changsha/ScoringService.cs`): a `×Clamp(AllPatterns.Count,1,3)` multiplier that can be applied when a hand satisfies multiple Big-Win shapes (e.g. 碰碰胡 + 清一色). `AllPatterns` is detected and surfaced, but the multiplier is **not** applied to the default payout.

Both are gated behind `Changsha/Scoring/ChangshaScoringOptions.cs`:

| Mode | Default? | Payments | Fan catalog | Stacking |
|------|----------|----------|-------------|----------|
| `SpecPure` | **✅ yes** | §5.1 table verbatim | surfaced, **not** folded into payments | ×1 (off) |
| `HouseRules` | no (opt-in) | §5.1 base **+** fan bonus folded per payment | folded into payments (`"fan:…"` reason rows) | ×Clamp(count,1,3) |

The default `SpecPure` keeps the live payout equal to §5.1 (frozen by `Section51GoldenTests`, Examples 1-10). `HouseRules` is retained for a possible future tournament option and is pinned by characterization tests (`ScoringOptionsCharacterizationTests`, `FanCatalogIntegrationTests`) so its magnitudes are not lost.

> **⚠️ Open question (surfaced by #117, NOT resolved):** *Should canonical Changsha score a fan (番) catalog + big-win stacking at all, or is the two-tier §5.1 table the complete authoritative model?* The MahjongPros/Baidu sources §5.1 is drawn from describe only the two-tier table; the fan catalog was an engine extension. Until Stephen rules on this, V1 ships spec-pure (§5.1 only) and treats the fan/stacking layer as query-only display metadata. **This section deliberately does NOT bless the code's former fan/stacking payout numbers as canonical.**

---

## 6. Game End & Dealer Rotation

**V1 SCOPE:** 16 hands per game (4 rounds × 4 hands). Banker rotation locked per user decision.

### 6.1 Hand End Conditions

A hand (局) ends when:
1. A player declares **Hu** (win) — either by self-draw or discard claim.
2. The wall is **exhausted** and no player can win (流局 — draw).

### 6.2 Banker Rotation (V1 Canonical Rule)

**V1 LOCKED DECISION (v1.2):** Banker rotation follows the canonical Changsha rule confirmed by all three authoritative sources: **the winner of a hand becomes the dealer for the next hand.** On washout (wall exhausted with no winner), the dealer keeps the seat. The hand counter increments regardless.

| Condition | Next Dealer | Hand Counter |
|-----------|-------------|--------------|
| Winner declared (self-draw 自摸 or discard claim 点炮) | **Winner becomes next dealer** — set `DealerSeatIndex = winnerSeatIndex` | +1 |
| Washout (wall exhausted, no winner) — 流局 | **Current dealer keeps the seat** — `DealerSeatIndex` unchanged | +1 |

**Source consensus (all three agree):**
- **MahjongPros (S1, authoritative):** *"The winner of the previous game becomes the new dealer."*
- **Baidu/Tencent (S2):** *"In subsequent rounds, whoever wins a hand becomes the dealer for the next round."*
- **Reddit (S3):** Winner-becomes-dealer (community rules overview).

**Worked example (starting with Seat 0 as dealer):**
- Hand 1: dealer = Seat 0. Seat 2 wins → **Hand 2: dealer = Seat 2.**
- Hand 2: dealer = Seat 2. Washout (wall exhausted, no winner) → **Hand 3: dealer = Seat 2 (unchanged).**
- Hand 3: dealer = Seat 2. Seat 1 wins → **Hand 4: dealer = Seat 1.**
- Hand 4: dealer = Seat 1. Seat 0 wins → Hand 5: dealer = Seat 0.

**Implementation contract:**
- On hand-end with a winner: `state.DealerSeatIndex = winnerSeatIndex` (no cyclic rotation).
- On hand-end without a winner (washout): leave `state.DealerSeatIndex` unchanged.
- Increment `state.HandNumber` in both cases.
- **No `+1 mod 4` and no `-1 mod 4` arithmetic.** There is no seat-cyclic rotation in canonical Changsha.

**Note on the v1.1 simplification (now superseded):** v1.1 said "dealer keeps seat on dealer win, otherwise rotate counter-clockwise." That was a v1 simplification which contradicted all three canonical sources and the spec's own internal example. v1.2 removes it. The actual Changsha rule is simpler: the winner is the next dealer; only a washout leaves the seat unchanged.

**Note on draw-handling nuance (deferred to v2):** Both MahjongPros and Baidu describe a finer-grained washout rule (e.g., MahjongPros: *"the player that draws the last tile becomes the dealer"*; Baidu: *"if a player takes the bottom tile and no one wins, that player becomes the dealer"*). V1 simplifies these to **"washout keeps the dealer seat"** because v1 has no concept of "who drew the last tile" and the simpler rule is unambiguous, deterministic, and consistent with the dominant majority of online digital implementations. The fine-grained washout-dealer rule is captured as a v2 refinement.

### 6.3 Round Structure (V1 Game Length)

**V1 LOCKED:** A game consists of **4 rounds × 4 hands = 16 hands total**.

- **Round 1 (East Round):** Hands 1-4
- **Round 2 (South Round):** Hands 5-8
- **Round 3 (West Round):** Hands 9-12
- **Round 4 (North Round):** Hands 13-16

Round wind changes every 4 hands regardless of dealer retention. **Note:** In traditional play, a round ends when each player has been dealer once. V1 uses a fixed 4-hands-per-round structure for simplicity.

**Alternative interpretation (for v2 consideration):** If implementing "true round" semantics, a round ends when each player has been dealer at least once (which may take more than 4 hands if the dealer retains the seat by winning). V1 uses the simpler fixed-length model.

---

## 7. State Machine

**V1 SCOPE:** State machine simplified to exclude instant wins, bird-catching, seabed choice, and robbing-the-kong.

### 7.1 States (V1)

```
SEATING
  → ROLLING_DICE
    → DEALING
      → IN_HAND (active player has 14 tiles)
        → AWAITING_DISCARD
          → CLAIM_WINDOW_OPEN
            → CLAIM_RESOLUTION
              → [back to IN_HAND for next player or claiming player]
        → DECLARING_KONG
          → DRAWING_REPLACEMENT
            → [back to AWAITING_DISCARD]
      → SCORING (win declared)
        → PAYMENT
          → ROTATING_DEALER
            → [back to ROLLING_DICE or END_GAME]
      → WALL_EXHAUSTED (draw)
        → ROTATING_DEALER
          → [back to ROLLING_DICE or END_GAME]
  → END_GAME
```

### 7.2 State Transitions (V1)

| From | Trigger | To | Effect |
|------|---------|-----|--------|
| SEATING | All 4 players seated | ROLLING_DICE | Assign dealer (East) |
| ROLLING_DICE | Dice rolled | DEALING | Determine break point; set draw wall pointer |
| DEALING | All tiles dealt | IN_HAND (dealer) | Dealer has 14, others have 13; dealer is active |
| IN_HAND | Player draws tile | AWAITING_DISCARD | Add tile to hand (14 tiles) |
| AWAITING_DISCARD | Player discards tile | CLAIM_WINDOW_OPEN | Remove tile from hand, add to discard pile |
| CLAIM_WINDOW_OPEN | No claims (timeout/all pass) | IN_HAND (next player) | Next player counterclockwise draws from wall |
| CLAIM_WINDOW_OPEN | Hu claimed | SCORING | Round ends, score the win |
| CLAIM_WINDOW_OPEN | Pung/Kong/Chow claimed | IN_HAND (claimer) | Form meld, claimer becomes active |
| IN_HAND | Self-draw Hu detected | SCORING | Player declares win |
| DECLARING_KONG | Kong declared (any type) | DRAWING_REPLACEMENT | Expose kong, queue replacement draw |
| DRAWING_REPLACEMENT | Tile drawn from back of wall | AWAITING_DISCARD | Player has 14 tiles again |
| SCORING | Score calculated | PAYMENT | Settle payments between players |
| PAYMENT | Payments settled | ROTATING_DEALER | Set next dealer = winner seat (canonical v1.2 rule) |
| ROTATING_DEALER | Not end of game (< 16 hands) | ROLLING_DICE | New hand begins; increment HandNumber |
| ROTATING_DEALER | End of game (16 hands complete) | END_GAME | Final scores tallied |
| IN_HAND (wall empty) | Last tile drawn | WALL_EXHAUSTED | Hand ends in draw (流局) |
| WALL_EXHAUSTED | — | ROTATING_DEALER | Dealer keeps seat (canonical v1.2 washout rule); increment HandNumber |

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

## 9. Open Questions — V1 Resolution Status

All open questions from the original spec have been resolved or deferred for v1 implementation.

### RESOLVED (V1 Decisions Locked)

1. **Instant win game flow** — **DEFERRED TO V2.** All instant win patterns excluded from v1.

2. **Ready kong dice roll scope for v1** — **DEFERRED TO V2.** Kong replacement always draws from back of wall in v1 (no dice option).

3. **Bird tile count** — **RESOLVED for v2:** 1 bird tile is standard (MahjongPros authoritative, confirmed by Reddit/Baidu). **DEFERRED TO V2:** Bird-catching excluded from v1.

4. **Kong payment amounts** — **DEFERRED TO V2.** Kong micro-payments excluded from v1.

5. **Scoring unit value** — **RESOLVED:** Base unit is a configurable table parameter. **Default base unit = 1** (payments emitted as raw unit values 1/2/3/4/6/7). Tables may override to base unit = 10 to display Baidu/Tencent-style 10/20/30/40/60/70 totals, or higher for high-stakes play. V1 implementation accepts base unit as table creation config; ScoringService default is 1.

6. **Big win self-draw vs discard scoring divergence** — **RESOLVED:** V1 uses MahjongPros model (authoritative): Small Win 1/2, Big Win self-draw 3/4, Big Win discard 6/7.

7. **Full Flush compounding** — **RESOLVED for v2:** In v1, Full Flush is a Big Win (single tier). Stacking/compounding with other Big Wins deferred to v2 when multiple special patterns are supported.

8. **Multiple Hu priority** — **RESOLVED:** V1 uses proximity rule — only one winner per discard (closest counterclockwise). Multiple simultaneous wins deferred to v2.

9. **San Tong (三同) instant win** — **DEFERRED TO V2.** All instant wins excluded from v1.

10. **Dealer retention** — **RESOLVED (v1.2, 2026-05-13):** **Winner of a hand becomes the next dealer.** On washout, the current dealer keeps the seat. Hand counter increments regardless. This matches all three canonical sources (MahjongPros, Baidu, Reddit) and supersedes the v1.1 simplification (`+1 CCW on non-dealer-win`). See §6.2 for full contract and worked example.

11. **Physical wall rendering** — **IMPLEMENTATION NOTE, NOT A RULE:** With 108 tiles, frontend can render walls as 14/14/13/13 stacks or use any balanced distribution. This is a presentation concern, not a gameplay rule. Backend wall is a flat 108-tile ordered list.

### ADDITIONAL RESOLUTIONS (Hudson's Contradictions)

See §10 below for detailed contradiction resolutions.

---

## 10. Hudson's Test Catalog Contradictions — Resolution

Hudson identified 8 source contradictions in the test catalog. All are resolved below per v1 decisions, with MahjongPros as the authoritative source.

### OQ-1: Number of Bird Tiles Drawn
**Contradiction:** S1 (MahjongPros) states "draw two additional tiles" for bird. S2 (Baidu) states "the next tile drawn immediately from the wall is the 'bird'" (singular).
**V1 RESOLUTION:** DEFERRED TO V2. Bird-catching excluded from v1.
**V2 DECISION:** **1 bird tile** is standard (S2 and Reddit confirm singular). Two bird tiles is a regional variant. MahjongPros' "two tiles" reference may be a translation ambiguity or regional variant. Standard Changsha uses 1 bird tile.

### OQ-2: Multiple Win Resolution - Payment Model
**Contradiction:** S1 suggests proximity rule (closest in turn order wins). S2 mentions "Multiple Win" where several players can win off same tile.
**V1 RESOLUTION:** **RESOLVED.** V1 uses **proximity rule** — only one winner per discard (closest player counterclockwise from discarder). Multiple simultaneous wins deferred to v2.
**Rationale:** Simplifies v1 state machine and payment flow. Proximity rule is unambiguous and consistent with standard mahjong claim priority.

### OQ-3: Dealer Determination After Multiple Starting Instant Wins (Red Dragon Reference)
**Contradiction:** S2 states "player who draws the Red Dragon becomes the dealer" when multiple players have starting instant wins. But Changsha tile set explicitly excludes Red Dragon (红中).
**V1 RESOLUTION:** DEFERRED TO V2. All instant wins excluded from v1.
**V2 DECISION:** This is a **source error** (Baidu referencing a different variant). For v2, use seat proximity rule: closest player counterclockwise from current dealer becomes next dealer, or use bird tile to determine dealer (per Reddit alternate method).

### OQ-4: Starting Hand Instant Wins - Game Continuation
**Contradiction:** S1 and S2 mention starting instant wins can be declared, and S2 says "the game will continue afterwards." Unclear if hand ends and redeals, or payment made and hand continues.
**V1 RESOLUTION:** DEFERRED TO V2. All instant wins excluded from v1.
**V2 DECISION:** Per S2 (Baidu) online rules, **game continues** after instant win payout (no redeal). Winner keeps tiles and may win again. This is the standard online implementation for flow efficiency.

### OQ-5: Kong Replacement Draw - Dice vs. End of Wall
**Contradiction:** S1 says player can choose dice roll or draw from end of wall. S2 adds dice can only be rolled if hand is in ready state.
**V1 RESOLUTION:** **RESOLVED.** Kong replacement always draws from **back of wall** (no dice option in v1). Ready-kong dice mechanic deferred to v2.
**V2 DECISION:** Implement S2 model (dice only if ready) as it adds strategic depth and risk/reward to kong declarations.

### OQ-6: Scoring Model - S1 vs. S2
**Contradiction:** S1 uses simplified 1/6/7 point model. S2 uses 10/20/60/70 point model.
**V1 RESOLUTION:** **RESOLVED.** V1 uses **S1 (MahjongPros) unit model: 1/2/3/4/6/7** with configurable base unit multiplier. **Default base unit = 1** (payments emitted as raw unit values). Tables may override to 10 to match S2's 10/20/30/40/60/70 display, or higher for high-stakes play.
**Rationale:** MahjongPros is authoritative. Unit model is more flexible and allows easy table configuration. Default = 1 keeps the implementation and test fixtures simple; display-scale multipliers are a presentation concern.

### OQ-7: Seven Pairs - Can Use 258 Pair or Any Pair?
**Gap:** Sources don't explicitly state if Seven Pairs requires a 258 pair.
**V1 RESOLUTION:** **RESOLVED.** Seven Pairs is a Big Win and uses the **"random eye" exemption** — any pair is allowed (no 258 requirement). This is consistent with All Pungs and Full Flush (both Big Wins exempt from 258 pair rule per sources).
**Rationale:** S1 and S2 both categorize Seven Pairs as a Big Win and state hand-based Big Wins do not require 258 pair. No exception mentioned for Seven Pairs.

### OQ-8: Full Beggar's Hand - Exposed Melds Count
**Contradiction:** S1 says "melded sets through Chow or Pong," but Changsha normally restricts chow. Implies Full Beggar's Hand allows chow calls.
**V1 RESOLUTION:** DEFERRED TO V2. Full Beggar's Hand excluded from v1 (not in 4 supported hand patterns).
**V2 DECISION:** Full Beggar's Hand does allow chow when forming exposed melds (explicitly stated in S1). The "next-seat only" restriction still applies to chow claims. This is NOT a contradiction — chow is allowed in Changsha, and Full Beggar's Hand requires all melds to be exposed (including chows).

---

## 11. Assumptions (V1 Scope)

These assumptions were made where sources were silent or ambiguous. Trimmed to v1-relevant items.

1. **Turn direction is counterclockwise** when viewed from above (standard Chinese mahjong convention). All three sources reference "to the right" which maps to counterclockwise in standard Chinese seating.

2. **Chow is from the immediate previous player** in turn order (the player who just discarded before you, if it's your natural turn). This aligns with standard Chinese mahjong chow rules and is confirmed by all three sources.

3. **Tile IDs are 0-indexed** with logical tile = `tileId / 4` and copy index = `tileId % 4`. With 108 tiles, IDs run 0–107, logical tiles 0–26.

4. **Dealer is seat index 0** (East position). Non-dealer seats are 1 (South), 2 (West), 3 (North) — following standard Chinese cardinal direction mapping counterclockwise.

5. **Kong replacement tiles come from the back end of the wall** (opposite end from normal draws). When the wall is a list, this means drawing from index 0 while normal draws come from the end (or vice versa depending on implementation choice).

6. **Concealed kong cannot be robbed** — confirmed by both MahjongPros and Reddit. Only added/extended kongs can be robbed. (Robbing-the-kong deferred to v2.)

7. **Seven Pairs must be concealed** — all seven pairs must come from self-drawn tiles, no open melds.

8. **Wall shuffling uses Fisher-Yates algorithm** with a seed for deterministic replay. The dice roll only determines the logical break point, not the physical arrangement (which is solely a rendering concern in digital play).

9. **Banker rotation rule** (v1.2 canonical, 2026-05-13): **Winner of a hand becomes the next dealer.** On washout (wall exhausted with no winner), the dealer keeps the seat. Hand counter increments after every hand. There is no cyclic `+1` or `-1` seat rotation in v1.

10. **Game length** (v1 locked): 16 hands total (4 rounds × 4 hands per round). Round wind changes every 4 hands.

---

## 12. V1 Conformance Checklist

This checklist confirms full v1 implementation. Bishop and Hudson use this as build-complete signal.

### Tile Set & Wall
- [ ] Tile set: exactly 108 tiles (Characters 1-9 ×4, Dots 1-9 ×4, Bamboo 1-9 ×4)
- [ ] No honors (winds/dragons), no flowers, no wildcards in tile set
- [ ] Dice roll (2d6) determines break point in wall
- [ ] Deal flow: 3 rounds of 4 tiles + 1 round of 1 tile + dealer's extra tile (dealer: 14, others: 13)
- [ ] Wall is drawable to last tile (no dead wall)

### Turn Flow
- [ ] Counterclockwise turn order
- [ ] Draw → evaluate → discard cycle (14 tiles in hand when active)
- [ ] Chow allowed (next-seat only)
- [ ] Pung/Kong allowed (any player)
- [ ] Claim priority: Hu > Kong = Pung > Chow (proximity breaks ties)
- [ ] Kong replacement draws from back of wall (not dice-based in v1)
- [ ] Concealed kong, exposed kong, added kong all supported

### Winning Patterns (V1 Only)
- [ ] Standard 4-sets-1-pair with 258 pair rule (Small Win)
- [ ] 七对子 (Seven Pairs) — concealed, any pair (Big Win)
- [ ] 碰碰胡 (All Pungs) — 4 pungs/kongs + any pair (Big Win)
- [ ] 清一色 (Full Flush) — all one suit, any pair (Big Win)
- [ ] Win methods: self-draw (自摸) and discard claim (点炮)
- [ ] NO instant wins, NO robbing-the-kong, NO seabed special wins in v1

### Scoring
- [ ] Small Win self-draw: each opponent pays 1 (dealer pays 2)
- [ ] Small Win discard: discarder pays 1 (dealer pays 2 if involved)
- [ ] Big Win self-draw: each opponent pays 3 (dealer pays 4)
- [ ] Big Win discard: discarder pays 6 (dealer pays 7 if involved)
- [ ] Base unit configurable at table creation (default base unit = 1; payments emitted as raw 1/2/3/4/6/7)
- [ ] Dealer bonus (+1) applied when dealer is winner or payer
- [ ] NO bird-catching, NO kong micro-payments in v1

### Banker & Game Flow
- [ ] Winner of a hand becomes the next dealer (canonical v1.2 rule, per MahjongPros / Baidu / Reddit)
- [ ] Washout (wall exhausted, no winner) → dealer keeps the seat (no rotation)
- [ ] Hand counter increments after every hand (win or washout)
- [ ] No `+1 mod 4` or `-1 mod 4` cyclic rotation logic anywhere in v1
- [ ] Game length: 16 hands (4 rounds × 4 hands)
- [ ] Round wind changes every 4 hands
- [ ] Only one winner per discard (proximity rule; no multiple simultaneous wins in v1)

### State Machine
- [ ] States: SEATING → ROLLING_DICE → DEALING → IN_HAND → AWAITING_DISCARD → CLAIM_WINDOW_OPEN → SCORING → PAYMENT → ROTATING_DEALER → [loop or END_GAME]
- [ ] NO CHECKING_INSTANT_WINS, NO BIRD_CATCHING, NO SEABED_CHOICE states in v1
- [ ] Deterministic: seeded RNG, ordered action log, reproducible replay

### Implementation Quality
- [ ] Hu validation enforces 258 pair for standard wins
- [ ] Big Win patterns exempt from 258 pair rule
- [ ] Seat-scoped state projection (no privileged bot information leaks)
- [ ] Authoritative backend (frontend renders from projected state only)
- [ ] Claim window timeout with deterministic resolution
- [ ] Bot and human actions use same validation pipeline

---

**V1 COMPLETE** when all checkboxes above are validated. This is the locked contract for Bishop (backend) and Hudson (test framework).
