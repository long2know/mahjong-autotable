# Changsha Mahjong Test Scenario Catalog

**Author:** Hudson (Tester, Squad Team)  
**Date:** 2026-04-20  
**Purpose:** Comprehensive test scenarios derived from rule sources to guide unit and integration testing  
**Sources:**
- [S1] https://mahjongpros.com/blogs/how-to-play/beginners-guide-to-changsha-mahjong (PRIMARY)
- [S2] https://baike.baidu.com/en/item/Changsha%20Mahjong/36618
- [S3] https://www.reddit.com/r/Mahjong/comments/xp6crv/... (INACCESSIBLE - verification wall)

**Note:** Reddit source could not be fetched due to verification requirements. Catalog based on S1 and S2.

---

## CAT-A: Tile Set & Wall Construction

### A-01: Tile Set Composition
**Description:** Verify tile set contains exactly 108 tiles: 36 Characters (1-9, 4 each), 36 Dots (1-9, 4 each), 36 Bamboo (1-9, 4 each). No winds, dragons, or flowers.  
**Setup:** Initialize game with standard mahjong tile set.  
**Trigger:** Filter tiles to Changsha-valid subset.  
**Expected:** Exactly 108 tiles remain, all suits 1-9 only.  
**Source:** S1, S2  
**Priority:** P0

### A-02: Wall Segment Construction - Standard Players
**Description:** Each non-dealer player builds wall segment of exactly 26 tiles (13 long × 2 high).  
**Setup:** 108 tiles shuffled face-down.  
**Trigger:** Three non-dealer players build their walls.  
**Expected:** Each segment is 13 stacks × 2 tiles high = 26 tiles.  
**Source:** S1  
**Priority:** P0

### A-03: Wall Segment Construction - Dealer
**Description:** Dealer builds wall segment of exactly 28 tiles (14 long × 2 high).  
**Setup:** 108 tiles shuffled face-down.  
**Trigger:** Dealer builds their wall.  
**Expected:** Dealer segment is 14 stacks × 2 tiles high = 28 tiles.  
**Source:** S1  
**Priority:** P0

### A-04: Wall Formation
**Description:** Four wall segments form a closed square.  
**Setup:** All players have built their wall segments.  
**Trigger:** Maneuver segments into position.  
**Expected:** Walls form contiguous square with no gaps.  
**Source:** S1  
**Priority:** P1

### A-05: Total Wall Tile Count
**Description:** Total tiles in all walls equals 108 (26+26+26+28 for standard 3 players + dealer, or 27×4=108 if all equal).  
**Setup:** All walls constructed.  
**Trigger:** Count all tiles in wall.  
**Expected:** Exactly 108 tiles present.  
**Source:** S1, S2  
**Priority:** P0

---

## CAT-B: Dice Roll & Break Point

### B-01: Dice Roll Range
**Description:** Two dice rolled, each returning value 1-6.  
**Setup:** Game initialized, dealer selected.  
**Trigger:** Dealer rolls two dice.  
**Expected:** Each die shows 1-6, sum is 2-12.  
**Source:** S1, S2  
**Priority:** P0

### B-02: Starting Wall Determination
**Description:** Dice sum determines which player's wall becomes the starting wall, counting counterclockwise from dealer.  
**Setup:** Dealer rolls dice (e.g., sum=7).  
**Trigger:** Count walls counterclockwise: dealer=1, right=2, opposite=3, left=4, repeat.  
**Expected:** Wall at position (sum mod 4) is selected (dealer=1, right=2, opposite=3, left=4).  
**Source:** S1  
**Priority:** P0

### B-03: Break Point Calculation
**Description:** Using the same dice sum, count that many stacks from the right end of the starting wall to find the break point.  
**Setup:** Starting wall identified, dice sum known (e.g., 7).  
**Trigger:** Count 7 stacks from right end of wall segment.  
**Expected:** Break occurs after the 7th stack; tiles after this become draw wall.  
**Source:** S1  
**Priority:** P0

### B-04: Draw Wall Definition
**Description:** Draw wall is the sequence of tiles immediately following the break point, wrapping clockwise around the square.  
**Setup:** Break point determined.  
**Trigger:** Mark start of draw wall.  
**Expected:** Draw wall begins after break point and continues until exhausted.  
**Source:** S1  
**Priority:** P0

---

## CAT-C: Initial Deal

### C-01: Dealer Tile Count After Deal
**Description:** Dealer receives 14 tiles after initial deal (13 + 1 first draw).  
**Setup:** Draw wall identified.  
**Trigger:** Execute initial deal.  
**Expected:** Dealer has 14 tiles.  
**Source:** S1, S2  
**Priority:** P0

### C-02: Non-Dealer Tile Count After Deal
**Description:** Each non-dealer player receives 13 tiles after initial deal.  
**Setup:** Draw wall identified.  
**Trigger:** Execute initial deal.  
**Expected:** Each non-dealer has 13 tiles.  
**Source:** S1, S2  
**Priority:** P0

### C-03: Deal Order - Four Tiles Per Round
**Description:** Tiles dealt in batches of 4 (two stacks), starting with dealer, proceeding counterclockwise.  
**Setup:** Draw wall ready.  
**Trigger:** Deal first round.  
**Expected:** Dealer gets 4, then right player, then opposite, then left; repeat.  
**Source:** S1  
**Priority:** P1

### C-04: Deal Completion - Three Rounds of Four
**Description:** After three rounds of 4 tiles each, all players have 12 tiles.  
**Setup:** Draw wall ready.  
**Trigger:** Complete three dealing rounds.  
**Expected:** All players have 12 tiles.  
**Source:** S1  
**Priority:** P0

### C-05: Final Single Tile Round
**Description:** After 12 tiles dealt, each player draws 1 more tile for total of 13.  
**Setup:** All players at 12 tiles.  
**Trigger:** Each player draws 1 tile counterclockwise.  
**Expected:** All players have 13 tiles.  
**Source:** S1  
**Priority:** P0

### C-06: Dealer First Draw
**Description:** Dealer draws 14th tile as their first draw before first discard.  
**Setup:** All players at 13 tiles (dealer included before the extra draw).  
**Trigger:** Dealer draws first tile.  
**Expected:** Dealer has 14 tiles and must discard to start play.  
**Source:** S1  
**Priority:** P0

### C-07: Wall Remainder After Deal
**Description:** After dealing 14+13+13+13=53 tiles, 55 tiles remain in draw wall.  
**Setup:** Initial deal complete.  
**Trigger:** Count remaining wall tiles.  
**Expected:** Exactly 55 tiles remain.  
**Source:** S1, S2  
**Priority:** P1

---

## CAT-D: Turn Flow (Draw / Discard)

### D-01: Turn Draw from Wall
**Description:** Active player draws one tile from draw wall at start of turn.  
**Setup:** Player's turn, wall has tiles.  
**Trigger:** Player draws.  
**Expected:** Player hand increases by 1 tile (13→14 or maintains 14 if dealer after discard).  
**Source:** S1  
**Priority:** P0

### D-02: Turn Discard Reduces Hand
**Description:** After evaluating hand, player discards one tile, ending turn with 13 tiles.  
**Setup:** Player has 14 tiles.  
**Trigger:** Player discards.  
**Expected:** Player hand = 13 tiles, discard placed in public discard area.  
**Source:** S1  
**Priority:** P0

### D-03: Turn Order - Counterclockwise
**Description:** By default, next player is to the right (counterclockwise).  
**Setup:** Player discards without claims.  
**Trigger:** Next turn begins.  
**Expected:** Player to the right becomes active.  
**Source:** S1  
**Priority:** P0

### D-04: Dead Tile Rule
**Description:** If no player claims a discard immediately, tile becomes dead and out of play.  
**Setup:** Tile discarded, no claims within timing window.  
**Trigger:** Next player draws from wall.  
**Expected:** Discarded tile cannot be claimed later, remains in discard area.  
**Source:** S1  
**Priority:** P1

### D-05: Wall Exhaustion - Draw Game
**Description:** If wall is exhausted (last tile drawn) and no one wins, hand ends in draw (流局).  
**Setup:** Only 1 tile remains in wall.  
**Trigger:** Last tile drawn, no win declared.  
**Expected:** Hand ends, no points exchanged, new hand begins.  
**Source:** S1, S2  
**Priority:** P0

### D-06: Chow NOT Allowed Out of Turn
**Description:** Changsha prohibits chow (three-tile straight) except when it completes a win or on player's own turn from prior discard.  
**Setup:** Player to left discards a tile that would complete a chow for non-next player.  
**Trigger:** Player attempts to chow out of turn.  
**Expected:** Chow claim rejected unless it completes a winning hand.  
**Source:** S1 (chow only from player immediately before you)  
**Priority:** P0

### D-07: Chow Allowed from Immediate Prior Player
**Description:** Player may chow a discard from the player immediately before them (to their left) on their turn.  
**Setup:** Player to left discards a tile.  
**Trigger:** Current player claims chow.  
**Expected:** Chow accepted, player reveals meld, discards to continue turn.  
**Source:** S1  
**Priority:** P0

---

## CAT-E: Pung / Kong

### E-01: Pung Interrupts Turn Order
**Description:** Any player can claim a discard for pung (three-of-a-kind) even if not their turn.  
**Setup:** Player holds 2 identical tiles, third is discarded by anyone.  
**Trigger:** Player declares pung.  
**Expected:** Player takes tile, reveals pung, discards, becomes active player.  
**Source:** S1  
**Priority:** P0

### E-02: Concealed Kong from Own Draw
**Description:** Player draws fourth identical tile, can declare concealed kong and draw replacement.  
**Setup:** Player has 3 identical tiles, draws 4th from wall.  
**Trigger:** Player declares concealed kong.  
**Expected:** Kong revealed (outer tiles face-down), player draws replacement tile.  
**Source:** S1, S2  
**Priority:** P0

### E-03: Exposed Kong from Discard
**Description:** Player can claim a discard to form exposed kong (4 identical tiles) and draw replacement.  
**Setup:** Player has 3 identical tiles, 4th is discarded.  
**Trigger:** Player declares kong.  
**Expected:** Kong revealed (all face-up), player draws replacement.  
**Source:** S1  
**Priority:** P0

### E-04: Added Kong (Open Melded Kong)
**Description:** Player who previously ponged can add the 4th matching tile from their own draw.  
**Setup:** Player has exposed pung, draws 4th matching tile on their turn.  
**Trigger:** Player declares added kong.  
**Expected:** Tile added to pung (placed on middle tile), player draws replacement.  
**Source:** S1  
**Priority:** P1

### E-05: Kong Replacement Draw - Dice Option
**Description:** When declaring kong, player can roll dice to determine replacement tile source OR draw from end of wall without rolling.  
**Setup:** Player declares kong.  
**Trigger:** Choose dice roll or default end-of-wall draw.  
**Expected:** If dice: count from end of wall by dice sum. If no dice: take from end.  
**Source:** S1, S2  
**Priority:** P1

### E-06: Kong Replacement Draw - End of Wall Default
**Description:** Without rolling dice, replacement tile drawn from end of wall (last tile working backward).  
**Setup:** Player declares kong, chooses not to roll.  
**Trigger:** Draw replacement.  
**Expected:** Next tile from end of wall drawn.  
**Source:** S1, S2  
**Priority:** P1

### E-07: Kong Replacement Draw - Dice Deterministic
**Description:** If dice rolled for replacement, tile position deterministic from dice value.  
**Setup:** Player declares kong, rolls dice.  
**Trigger:** Dice show sum N.  
**Expected:** Count N stacks from end, draw top tile (or bottom if top unavailable).  
**Source:** S2  
**Priority:** P2

### E-08: Multiple Claims Priority - Win > Pung > Chow
**Description:** If multiple players want a discard, priority: win > pung/kong > chow.  
**Setup:** Tile discarded, multiple claims.  
**Trigger:** Resolve priority.  
**Expected:** Winner takes precedence, then pung, then chow (if applicable).  
**Source:** S1  
**Priority:** P0

### E-09: Multiple Win Claims Priority - Proximity
**Description:** If multiple players can win from same discard, closest in turn order (counterclockwise from discarder) wins.  
**Setup:** Two+ players claim win from same discard.  
**Trigger:** Resolve winner.  
**Expected:** Player nearest counterclockwise to discarder wins.  
**Source:** S1  
**Priority:** P1

### E-10: Kong Fee Payment (Optional Rule)
**Description:** When kong declared, other three players may pay a fee to the kong declarer.  
**Setup:** Player declares kong (concealed or exposed).  
**Trigger:** Kong revealed.  
**Expected:** If agreed pre-game, other players pay fixed fee.  
**Source:** S2 (noted as optional/common custom)  
**Priority:** P2

---

## CAT-F: Win Patterns

### F-01: Standard Win - Four Sets + Pair (258 General)
**Description:** Win with 4 melds (sets/sequences) + 1 pair of 2, 5, or 8 (Small Win).  
**Setup:** Player has 3 melds + 1 wait.  
**Trigger:** Draw or claim completing tile.  
**Expected:** Hand validated as 4 melds + 258 pair, scored as Small Win.  
**Source:** S1, S2  
**Priority:** P0

### F-02: Seven Pairs (七小对 / Qi Qiao Dui)
**Description:** Win with exactly seven distinct pairs of tiles (Big Win).  
**Setup:** Player holds 6 pairs + 1 wait for 7th pair.  
**Trigger:** Complete 7th pair.  
**Expected:** Hand validated as 7 pairs, scored as Big Win.  
**Source:** S1, S2  
**Priority:** P0

### F-03: Luxurious Seven Pairs (豪华七小对 / Hao Hua Qi Xiao Dui)
**Description:** Win with 5 pairs + 1 set of four identical tiles (Big Win × 2).  
**Setup:** Player has 5 pairs + 4-of-a-kind.  
**Trigger:** Complete winning shape.  
**Expected:** Scored as Big Win × 2 (double multiplier).  
**Source:** S1, S2  
**Priority:** P1

### F-04: All Pungs (碰碰胡 / Peng Peng Hu)
**Description:** Win with 4 pungs/kongs + any pair (Big Win, no 258 requirement).  
**Setup:** Player has 3 pungs + 1 wait.  
**Trigger:** Complete 4th pung or pair.  
**Expected:** Hand validated, scored as Big Win, pair can be any tile.  
**Source:** S1, S2  
**Priority:** P0

### F-05: Full Flush (清一色 / Qing Yi Se)
**Description:** Win with all tiles from single suit (Dots, Bamboo, or Characters), any pattern (Big Win, no 258 requirement).  
**Setup:** Player has hand of single suit only, 1 wait.  
**Trigger:** Complete win.  
**Expected:** Hand validated as pure one suit, scored as Big Win, pair can be any tile.  
**Source:** S1, S2  
**Priority:** P0

### F-06: All Generals (将将胡 / Jiang Jiang Hu / All 258s)
**Description:** Win with all tiles being 2s, 5s, or 8s only (Big Win).  
**Setup:** Player holds only 2, 5, 8 tiles, 1 wait.  
**Trigger:** Complete win.  
**Expected:** Hand validated as all 2/5/8, scored as Big Win.  
**Source:** S1, S2  
**Priority:** P1

### F-07: Full Beggar's Hand (全求人 / Quan Qiu Ren)
**Description:** Player has melded all sets via chow/pung, waits for last tile, must win by claiming discard (Big Win).  
**Setup:** Player has 3+ exposed melds, waits for 1 tile to complete hand.  
**Trigger:** Claim discarded winning tile.  
**Expected:** Cannot win by self-draw, only by discard claim. Scored as Big Win.  
**Source:** S1  
**Priority:** P2

### F-08: Blessing of Heaven (天和 / Tian He)
**Description:** Dealer wins on their initial 14-tile deal without any action (Big Win, 258 pair required).  
**Setup:** Dealer receives initial 14 tiles.  
**Trigger:** Deal completes.  
**Expected:** If dealer's hand is already winning, declare Heavenly Hand, scored as Big Win.  
**Source:** S1, S2  
**Priority:** P1

### F-09: Blessing of Earth (地和 / Di He)
**Description:** Non-dealer wins on first tile drawn, before any exposed melds declared (Big Win, 258 pair required).  
**Setup:** Non-dealer receives 13 tiles, draws first tile.  
**Trigger:** First draw completes winning hand.  
**Expected:** Win declared, scored as Big Win.  
**Source:** S1, S2  
**Priority:** P1

### F-10: Last Tile Win (海底捞月 / Hai Di Lao Yue)
**Description:** Win by drawing the very last tile from wall (Big Win, 258 pair required).  
**Setup:** Only 1 tile remains in wall.  
**Trigger:** Player draws last tile and wins.  
**Expected:** Scored as Big Win.  
**Source:** S1, S2  
**Priority:** P1

### F-11: Last Tile Discard Win (河底撈魚 / He Di Lao Yu)
**Description:** Win by claiming the discard after the last tile was drawn (Big Win, 258 pair required).  
**Setup:** Last tile drawn, player cannot win, discards.  
**Trigger:** Another player claims discard for win.  
**Expected:** Scored as Big Win.  
**Source:** S1, S2  
**Priority:** P1

### F-12: Win After Kong (杠上开花 / Gang Shang Kai Hua)
**Description:** Win by drawing replacement tile immediately after declaring kong (Big Win, 258 pair required).  
**Setup:** Player declares kong, draws replacement.  
**Trigger:** Replacement tile completes win.  
**Expected:** Scored as Big Win.  
**Source:** S1, S2  
**Priority:** P1

### F-13: Kong on Cannon (杠上炮 / Gang Shang Pao)
**Description:** Opponent wins from the discard immediately after you declare kong (Big Win for winner, 258 pair required).  
**Setup:** Player declares kong, draws replacement but cannot win, discards.  
**Trigger:** Another player claims that discard for win.  
**Expected:** Winner scores Big Win, kong declarer pays penalty.  
**Source:** S1, S2  
**Priority:** P1

### F-14: Robbing the Kong (抢杠胡 / Qiang Gang Hu)
**Description:** Win by claiming the tile another player adds to existing pung to form exposed kong (Big Win, 258 pair required).  
**Setup:** Player A adds 4th tile to their exposed pung.  
**Trigger:** Player B claims that tile for win before replacement draw.  
**Expected:** Player B wins, scored as Big Win. Concealed kongs cannot be robbed.  
**Source:** S1, S2  
**Priority:** P1

### F-15: Starting Hand - Four Joys (四喜 / Si Xi)
**Description:** After initial deal, player has four identical tiles, can declare instant win for Small Win value.  
**Setup:** Player dealt 4 identical tiles in starting hand.  
**Trigger:** Declare instant win.  
**Expected:** Scored as Small Win, game continues (optional rule), no bird catching.  
**Source:** S1, S2  
**Priority:** P2

### F-16: Starting Hand - All Pure (板板胡 / Ban Ban Hu)
**Description:** After initial deal, player has no 2s, 5s, or 8s at all, can declare instant win for Small Win value.  
**Setup:** Player dealt hand with only 1,3,4,6,7,9 tiles.  
**Trigger:** Declare instant win.  
**Expected:** Scored as Small Win, game continues (optional rule), no bird catching.  
**Source:** S1, S2  
**Priority:** P2

### F-17: Starting Hand - Voided Suit (缺一色 / Que Yi Se)
**Description:** After initial deal, player lacks tiles from one of three suits, can declare instant win for Small Win value.  
**Setup:** Player dealt hand missing entire suit (e.g., no Bamboo).  
**Trigger:** Declare instant win.  
**Expected:** Scored as Small Win, game continues (optional rule), no bird catching.  
**Source:** S1, S2  
**Priority:** P2

### F-18: Starting Hand - Six Six Straight (六六顺 / Liu Liu Shun)
**Description:** After initial deal, player has two identical triplets (2 pungs), can declare instant win for Small Win value.  
**Setup:** Player dealt 2 pungs in starting hand.  
**Trigger:** Declare instant win.  
**Expected:** Scored as Small Win, game continues (optional rule), no bird catching.  
**Source:** S1, S2  
**Priority:** P2

### F-19: Starting Hand - Three Same (三同 / San Tong)
**Description:** After initial deal, player has same number from all three suits, each at least a pair.  
**Setup:** Player dealt e.g. 3 Dots pair, 3 Bamboo pair, 3 Characters pair (or more).  
**Trigger:** Declare instant win.  
**Expected:** Scored as Small Win, game continues (optional rule), no bird catching.  
**Source:** S1  
**Priority:** P2

### F-20: Multiple Win (Multiple Players Win Same Tile)
**Description:** Multiple players can win from the same discarded tile simultaneously.  
**Setup:** Tile discarded, 2+ players in ready state.  
**Trigger:** Multiple win declarations.  
**Expected:** All winners receive points from discarder OR resolved by proximity rule (source conflict, see open questions).  
**Source:** S1 (proximity), S2 (multiple win mentioned)  
**Priority:** P1

---

## CAT-G: Scoring

### G-01: Small Win - Non-Dealer Self-Draw Payment
**Description:** Non-dealer wins by self-draw: dealer pays 20, each other player pays 10.  
**Setup:** Non-dealer wins by drawing tile.  
**Trigger:** Score hand.  
**Expected:** Dealer -20, other non-dealers -10 each, winner +40.  
**Source:** S2  
**Priority:** P0

### G-02: Small Win - Dealer Self-Draw Payment
**Description:** Dealer wins by self-draw: each player pays 20.  
**Setup:** Dealer wins by drawing tile.  
**Trigger:** Score hand.  
**Expected:** Each non-dealer -20, dealer +60.  
**Source:** S2  
**Priority:** P0

### G-03: Small Win - Discard Claim Payment (Non-Dealer)
**Description:** Non-dealer wins from discard: discarder pays 10 (or 20 if discarder is dealer).  
**Setup:** Non-dealer wins from someone's discard.  
**Trigger:** Score hand.  
**Expected:** Discarder pays 10 (+10 if dealer), winner receives.  
**Source:** S2  
**Priority:** P0

### G-04: Small Win - Discard Claim Payment (Dealer Winner)
**Description:** Dealer wins from discard: discarder pays 10 + 10 dealer bonus = 20.  
**Setup:** Dealer wins from someone's discard.  
**Trigger:** Score hand.  
**Expected:** Discarder pays 20, dealer receives.  
**Source:** S2  
**Priority:** P0

### G-05: Big Win - Non-Dealer Self-Draw Payment
**Description:** Non-dealer wins Big Win by self-draw: dealer pays 70, each other player pays 60.  
**Setup:** Non-dealer wins Big Win by drawing tile.  
**Trigger:** Score hand.  
**Expected:** Dealer -70, other non-dealers -60 each, winner receives total.  
**Source:** S2  
**Priority:** P0

### G-06: Big Win - Dealer Self-Draw Payment
**Description:** Dealer wins Big Win by self-draw: each player pays 70.  
**Setup:** Dealer wins Big Win by drawing tile.  
**Trigger:** Score hand.  
**Expected:** Each non-dealer -70, dealer +210.  
**Source:** S2  
**Priority:** P0

### G-07: Big Win - Discard Claim Payment (Non-Dealer)
**Description:** Non-dealer wins Big Win from discard: discarder pays 60 (or 70 if discarder is dealer).  
**Setup:** Non-dealer wins Big Win from someone's discard.  
**Trigger:** Score hand.  
**Expected:** Discarder pays 60 (+10 if dealer), winner receives.  
**Source:** S2  
**Priority:** P0

### G-08: Big Win - Discard Claim Payment (Dealer Winner)
**Description:** Dealer wins Big Win from discard: discarder pays 60 + 10 dealer bonus = 70.  
**Setup:** Dealer wins Big Win from someone's discard.  
**Trigger:** Score hand.  
**Expected:** Discarder pays 70, dealer receives.  
**Source:** S2  
**Priority:** P0

### G-09: Simplified Scoring Formula (S1 Model)
**Description:** S1 uses simpler model: Small Win = 1 point, Big Win = 6/7 points (dealer 7, non-dealer 6).  
**Setup:** Any win.  
**Trigger:** Calculate score.  
**Expected:** Small Win pays 1 (dealer receives/pays 2), Big Win pays 6/7 (dealer 7).  
**Source:** S1  
**Priority:** P0

### G-10: Multiple Big Wins Stack
**Description:** If hand qualifies for multiple Big Win patterns, scores stack (added together).  
**Setup:** Player has e.g. Seven Pairs + Heavenly Hand.  
**Trigger:** Calculate score.  
**Expected:** Multiple Big Win bonuses accumulate.  
**Source:** S2  
**Priority:** P1

### G-11: Small Win + Big Win = Big Win
**Description:** If hand has Small Win instant condition + Big Win pattern, score as Big Win only.  
**Setup:** Player has e.g. Four Joys (starting) + completes Big Win later.  
**Trigger:** Calculate score.  
**Expected:** Big Win value replaces Small Win, no stacking.  
**Source:** S2  
**Priority:** P2

### G-12: Multiple Small Wins Stack
**Description:** Multiple starting-hand instant Small Win conditions can accumulate.  
**Setup:** Player has e.g. Four Joys + Six Six Straight at start.  
**Trigger:** Calculate score.  
**Expected:** Multiple Small Win bonuses stack.  
**Source:** S2  
**Priority:** P2

---

## CAT-H: Banker / Round Rotation

### H-01: Initial Dealer - Random Assignment
**Description:** First dealer selected randomly (agreed by players or system random).  
**Setup:** Game start.  
**Trigger:** Determine first dealer.  
**Expected:** Dealer chosen fairly, wind starts at East.  
**Source:** S1, S2  
**Priority:** P0

### H-02: Dealer Rotation - Winner Becomes Dealer
**Description:** Winner of previous hand becomes dealer for next hand.  
**Setup:** Hand completes with winner.  
**Trigger:** Start next hand.  
**Expected:** Previous winner is new dealer.  
**Source:** S1, S2  
**Priority:** P0

### H-03: Dealer Rotation - Draw, Last Tile Drawer Becomes Dealer
**Description:** If hand ends in draw, player who had option to draw last tile (or drew it) becomes dealer.  
**Setup:** Wall exhausted, no winner.  
**Trigger:** Start next hand.  
**Expected:** Last-tile-eligible player becomes dealer.  
**Source:** S1, S2  
**Priority:** P1

### H-04: Dealer Rotation - Multiple Winners, Discarder Becomes Dealer
**Description:** If multiple players win from same discard, the discarder becomes next dealer.  
**Setup:** Multiple win condition.  
**Trigger:** Start next hand.  
**Expected:** Discarder is new dealer.  
**Source:** S2  
**Priority:** P2

### H-05: Dealer Rotation - Multiple Starting Instant Wins
**Description:** If 2+ players have starting instant win hands, player who drew/holds 红中 (Red Dragon) becomes dealer. (NOTE: Changsha has no dragons per tile rules—contradiction, see open questions.)  
**Setup:** Multiple starting instant wins.  
**Trigger:** Determine next dealer.  
**Expected:** Resolution mechanism unclear due to tile set conflict.  
**Source:** S2 (mentions Red Dragon, but Changsha excludes dragons)  
**Priority:** P2 (OPEN QUESTION)

### H-06: Round Wind Progression
**Description:** After 4 hands (one full dealer rotation), round wind changes: East → South → West → North.  
**Setup:** Complete 4 hands.  
**Trigger:** Start 5th hand.  
**Expected:** Round wind advances.  
**Source:** S1  
**Priority:** P1

### H-07: Game End Condition
**Description:** Game ends after all four round winds completed (16 hands total) OR by mutual agreement.  
**Setup:** 16 hands played.  
**Trigger:** Check game end.  
**Expected:** Game concludes, final scores tallied.  
**Source:** Inferred from S1 (round wind progression)  
**Priority:** P2

---

## CAT-I: Capturing the Bird (Zha Niao / 扎鸟)

### I-01: Bird Tiles Drawn After Win
**Description:** After win (excluding starting instant wins), draw one bird tile from wall.  
**Setup:** Player wins normal hand.  
**Trigger:** Draw bird tile.  
**Expected:** One tile drawn from wall as bird.  
**Source:** S1 (says two tiles), S2 (says one tile) — CONFLICT  
**Priority:** P0

### I-02: Bird Tile Number Mapping - Dealer
**Description:** Bird tile number 1, 5, or 9 corresponds to dealer.  
**Setup:** Bird tile drawn.  
**Trigger:** Check tile number.  
**Expected:** 1/5/9 → dealer.  
**Source:** S1  
**Priority:** P0

### I-03: Bird Tile Number Mapping - Right Player
**Description:** Bird tile number 2 or 6 corresponds to player to right of dealer.  
**Setup:** Bird tile drawn.  
**Trigger:** Check tile number.  
**Expected:** 2/6 → right player.  
**Source:** S1  
**Priority:** P0

### I-04: Bird Tile Number Mapping - Opposite Player
**Description:** Bird tile number 3 or 7 corresponds to opposite player from dealer.  
**Setup:** Bird tile drawn.  
**Trigger:** Check tile number.  
**Expected:** 3/7 → opposite player.  
**Source:** S1  
**Priority:** P0

### I-05: Bird Tile Number Mapping - Left Player
**Description:** Bird tile number 4 or 8 corresponds to player to left of dealer.  
**Setup:** Bird tile drawn.  
**Trigger:** Check tile number.  
**Expected:** 4/8 → left player.  
**Source:** S1  
**Priority:** P0

### I-06: Bird Hits Winner - Double Points
**Description:** If bird corresponds to winner, points doubled (self-draw or discard win).  
**Setup:** Winner wins, bird drawn.  
**Trigger:** Bird maps to winner.  
**Expected:** Winner's payout × 2.  
**Source:** S1, S2  
**Priority:** P0

### I-07: Bird Hits Discarder - Double Points
**Description:** If bird corresponds to discarder (on discard win), points doubled.  
**Setup:** Winner wins from discard, bird drawn.  
**Trigger:** Bird maps to discarder.  
**Expected:** Winner's payout from discarder × 2.  
**Source:** S1, S2  
**Priority:** P0

### I-08: Bird Hits Winner & Discarder - Triple Points
**Description:** If two birds drawn and both hit winner and/or discarder, points tripled.  
**Setup:** Winner wins, two birds drawn.  
**Trigger:** Both birds map to winner or discarder.  
**Expected:** Winner's payout × 3.  
**Source:** S1  
**Priority:** P1

### I-09: Bird Self-Draw Special Case
**Description:** On self-draw win, if bird hits winner, all losers pay double. If bird hits another player, only that player pays double.  
**Setup:** Self-draw win, bird drawn.  
**Trigger:** Bird maps to player.  
**Expected:** If winner: all pay double. If other: that player pays double only.  
**Source:** S1, S2  
**Priority:** P1

### I-10: Bird Not Drawn for Starting Instant Wins
**Description:** Starting hand instant win conditions do NOT trigger bird drawing.  
**Setup:** Player wins with Four Joys/Ban Ban Hu/etc. at deal.  
**Trigger:** Score hand.  
**Expected:** No bird tile drawn.  
**Source:** S1  
**Priority:** P1

### I-11: Last Tile as Bird
**Description:** If win occurs on the very last tile of wall (sea floor), that tile itself is the bird.  
**Setup:** Last wall tile drawn for win.  
**Trigger:** Determine bird.  
**Expected:** Winning tile's number determines bird position.  
**Source:** S2  
**Priority:** P2

---

## CAT-J: Last Tile & Sea Floor Rules

### J-01: Last Tile Roaming - Offer to Players
**Description:** When only one tile remains (sea floor), players can choose in turn order whether to draw it.  
**Setup:** Wall has 1 tile remaining.  
**Trigger:** Offer to first player in turn order.  
**Expected:** Player can accept or pass; if pass, offer goes to next player.  
**Source:** S1, S2  
**Priority:** P1

### J-02: Last Tile Roaming - All Pass = Draw
**Description:** If all four players pass on last tile, hand ends in draw.  
**Setup:** Last tile offered to all, all pass.  
**Trigger:** Fourth player passes.  
**Expected:** Hand ends, no winner, proceed to dealer determination.  
**Source:** S1  
**Priority:** P1

### J-03: Kong Replacement Negates Sea Floor
**Description:** If last tile taken as kong replacement, sea floor rules do NOT apply; other tiles may be discarded.  
**Setup:** Kong declared when 1 tile remains, replacement drawn.  
**Trigger:** Replacement tile becomes "new last tile."  
**Expected:** Sea floor win condition unavailable, normal play continues until wall truly exhausted.  
**Source:** S1, S2  
**Priority:** P2

### J-04: Sea Floor Win Requires 258 Pair
**Description:** Win from sea floor (last tile draw or discard) requires hand to have 258 pair (unless Big Win exempts).  
**Setup:** Last tile completes win.  
**Trigger:** Validate hand.  
**Expected:** Hand must meet 258 pair rule or be exempted Big Win pattern.  
**Source:** S1, S2  
**Priority:** P1

---

## CAT-K: Edge Cases & Special Rules

### K-01: Missed Win (Furiten / 弃和)
**Description:** If player misses a win opportunity from a discard, they cannot win from ANY discard until after they draw a tile.  
**Setup:** Player could have won from discard but didn't claim.  
**Trigger:** Player passes on winning tile.  
**Expected:** Player blocked from winning by discard until next draw.  
**Source:** S2  
**Priority:** P1

### K-02: Dice-Gated Replacement Draw
**Description:** Player can only roll dice for kong replacement if hand is in ready/waiting state (system-judged).  
**Setup:** Player declares kong but not in ready state.  
**Trigger:** Attempt dice roll.  
**Expected:** Dice roll disallowed, must draw from end of wall.  
**Source:** S2  
**Priority:** P2

### K-03: Forced Discard After Dice Roll Replacement
**Description:** If player rolls dice for replacement, draws tile, and cannot win, they must discard that tile immediately.  
**Setup:** Player in ready state, rolls dice, draws replacement but doesn't win.  
**Trigger:** Evaluate hand.  
**Expected:** Must discard drawn tile, continues until win or discard enables another's win.  
**Source:** S2  
**Priority:** P2

### K-04: Dice Replacement Tile Unavailable - Fallback
**Description:** If dice indicates a stack with no top tile, draw bottom tile of that stack. If last tile only, draw it.  
**Setup:** Dice points to depleted position.  
**Trigger:** Draw replacement.  
**Expected:** System fallback to bottom tile or last tile.  
**Source:** S2  
**Priority:** P2

### K-05: Kong Robbing - Concealed Cannot Be Robbed
**Description:** Concealed kong (4 tiles from hand) cannot be claimed by another player for win. Only exposed kong (added to pung) can be robbed.  
**Setup:** Player declares concealed kong.  
**Trigger:** Another player attempts to rob.  
**Expected:** Robbery disallowed.  
**Source:** S1, S2  
**Priority:** P1

### K-06: Disconnect / Escape Penalty
**Description:** Player who disconnects, escapes, or force-quits penalized 70 points.  
**Setup:** Player disconnects.  
**Trigger:** Session detects disconnect.  
**Expected:** Player loses 70 points. In currency rooms, points redistributed; in other rooms, not redistributed.  
**Source:** S2  
**Priority:** P2

### K-07: Big Win Full Flush + All Pungs Stack
**Description:** If hand is both Full Flush and All Pungs, both Big Win conditions stack (at least 12 points for non-dealer, 14 for dealer, per S1 scoring).  
**Setup:** Hand meets both conditions.  
**Trigger:** Evaluate hand.  
**Expected:** Multiple Big Win multipliers apply.  
**Source:** S1  
**Priority:** P1

### K-08: 258 Pair Exemption for Big Win Patterns
**Description:** Most Big Win patterns do NOT require 258 pair ("random eye" allowed), except specific patterns like Heavenly/Earthly/Sea Floor wins.  
**Setup:** Player wins with All Pungs / Full Flush / etc.  
**Trigger:** Validate pair.  
**Expected:** Pair can be any tile (1,3,4,6,7,9 allowed).  
**Source:** S1, S2  
**Priority:** P0

### K-09: Chow in Big Win Patterns
**Description:** Some Big Win patterns (Full Flush) allow chow (sequences). All Pungs explicitly disallows sequences.  
**Setup:** Player completing Full Flush with sequences.  
**Trigger:** Validate hand.  
**Expected:** Chows allowed in Full Flush, not in All Pungs.  
**Source:** S1, S2  
**Priority:** P1

---

## CAT-L: State Machine & Determinism

### L-01: Game State Serialization
**Description:** Full game state (wall, hands, discards, dealer, wind, scores) can be serialized to persistent storage.  
**Setup:** Game in progress.  
**Trigger:** Serialize state.  
**Expected:** State captured completely, no information loss.  
**Source:** Design requirement  
**Priority:** P0

### L-02: Game State Deserialization
**Description:** Serialized game state can be loaded and play resumed exactly.  
**Setup:** Previously serialized state.  
**Trigger:** Deserialize and resume.  
**Expected:** All players, tiles, turn order, wall restored accurately.  
**Source:** Design requirement  
**Priority:** P0

### L-03: Deterministic RNG Seeded Play
**Description:** Given same RNG seed and player actions, game produces identical outcomes.  
**Setup:** Game with fixed seed.  
**Trigger:** Replay actions.  
**Expected:** Wall breaks, deals, draws are identical.  
**Source:** Design requirement (testability)  
**Priority:** P0

### L-04: Concurrent Claim Resolution - Atomic
**Description:** When multiple players claim same discard, resolution is atomic and deterministic (no race conditions).  
**Setup:** Two players click "pung" simultaneously.  
**Trigger:** System resolves.  
**Expected:** Priority rules applied, only one claim succeeds, other rejected cleanly.  
**Source:** Design requirement  
**Priority:** P0

### L-05: Turn Timer Expiration
**Description:** If player does not act within time limit, system auto-discards or auto-passes.  
**Setup:** Player's turn, timer running.  
**Trigger:** Timer expires.  
**Expected:** System makes safe default action (discard random tile or first tile).  
**Source:** Design requirement (online play)  
**Priority:** P1

---

## CAT-M: Bot Behavior & AI

### M-01: Bot Completes Full Hand Without Crash
**Description:** AI bot can play a complete hand from deal to win/draw without errors.  
**Setup:** Game with one+ AI bots.  
**Trigger:** Play full hand.  
**Expected:** Bot discards legally, claims when appropriate, no exceptions thrown.  
**Source:** Design requirement  
**Priority:** P0

### M-02: Bot Never Makes Illegal Moves
**Description:** Bot respects all game rules: doesn't chow out of turn, doesn't discard too many tiles, doesn't claim dead tiles.  
**Setup:** Game with bot.  
**Trigger:** Monitor bot actions over many hands.  
**Expected:** Zero illegal moves detected.  
**Source:** Design requirement  
**Priority:** P0

### M-03: Bot Recognizes Win Conditions
**Description:** Bot correctly identifies when its hand is winning and declares win.  
**Setup:** Bot hand reaches winning shape.  
**Trigger:** Bot's turn or discard claim opportunity.  
**Expected:** Bot declares win, validated successfully.  
**Source:** Design requirement  
**Priority:** P0

### M-04: Bot Makes Plausible Discards
**Description:** Bot discards tiles that reduce wait or improve hand safety (not random/foolish).  
**Setup:** Bot evaluating discard options.  
**Trigger:** Bot selects discard.  
**Expected:** Discard is defensible (e.g., doesn't discard winning tile, doesn't break ready hand carelessly).  
**Source:** Design requirement (playability)  
**Priority:** P1

### M-05: Bot Defensive Play Against Ready Opponents
**Description:** Bot detects when opponents may be ready (by discards/melds) and discards safer tiles.  
**Setup:** Opponent reveals 3 melds, likely waiting.  
**Trigger:** Bot's turn.  
**Expected:** Bot avoids discarding tiles opponent likely needs (e.g., avoids 2/5/8 if opponent has many 2/5/8 melds).  
**Source:** Design requirement (competitive AI)  
**Priority:** P2

---

## CAT-N: API & Integration

### N-01: API Create Game - Valid Configuration
**Description:** API endpoint to create new game validates configuration (player count=4, rule variant=Changsha).  
**Setup:** Client sends create request.  
**Trigger:** API processes request.  
**Expected:** Game created with Changsha rules, 4 players, returns game ID.  
**Source:** Design requirement  
**Priority:** P0

### N-02: API Join Game - Player Authentication
**Description:** Players join game via authenticated API call.  
**Setup:** Game created.  
**Trigger:** Player sends join request with token.  
**Expected:** Player added to game if authenticated and slots available.  
**Source:** Design requirement  
**Priority:** P0

### N-03: API Action Submission - Draw Tile
**Description:** Player submits "draw tile" action via API during their turn.  
**Setup:** Player's turn.  
**Trigger:** API receives draw action.  
**Expected:** Tile drawn, hand updated, response sent.  
**Source:** Design requirement  
**Priority:** P0

### N-04: API Action Submission - Discard Tile
**Description:** Player submits "discard tile X" action via API.  
**Setup:** Player has 14 tiles.  
**Trigger:** API receives discard action.  
**Expected:** Tile discarded, hand updated to 13, other players notified.  
**Source:** Design requirement  
**Priority:** P0

### N-05: API Action Submission - Pung Claim
**Description:** Player submits "pung" claim action when discard occurs.  
**Setup:** Tile discarded, player has 2 matching.  
**Trigger:** API receives pung action within time window.  
**Expected:** Pung validated, claim succeeds, turn transfers.  
**Source:** Design requirement  
**Priority:** P0

### N-06: API Action Submission - Win Declaration
**Description:** Player submits "win" action when hand is complete.  
**Setup:** Player's hand is winning.  
**Trigger:** API receives win action.  
**Expected:** Hand validated, win confirmed or rejected with reason.  
**Source:** Design requirement  
**Priority:** P0

### N-07: API State Query - Player Hand
**Description:** Player can query current hand state (own tiles, visible melds, discard pile).  
**Setup:** Game in progress.  
**Trigger:** Player queries state.  
**Expected:** Response includes player's tiles (hidden from others), public info.  
**Source:** Design requirement  
**Priority:** P0

### N-08: API State Query - Game Status
**Description:** Any player can query game status (current turn, dealer, wind, scores).  
**Setup:** Game in progress.  
**Trigger:** Player queries status.  
**Expected:** Response includes public game state.  
**Source:** Design requirement  
**Priority:** P0

### N-09: API Event Stream - Real-Time Updates
**Description:** Players receive real-time events (tile drawn by opponent, discard, claim, win) via WebSocket or SSE.  
**Setup:** Player connected to game.  
**Trigger:** Game event occurs.  
**Expected:** Player receives event notification within <500ms.  
**Source:** Design requirement  
**Priority:** P1

### N-10: API Error Handling - Invalid Action
**Description:** API rejects invalid actions (e.g., discard when not your turn) with clear error message.  
**Setup:** Player sends out-of-turn action.  
**Trigger:** API validates action.  
**Expected:** 400/403 response with error: "Not your turn" or similar.  
**Source:** Design requirement  
**Priority:** P0

---

## Summary Statistics

**Total Scenarios:** 80  
**Priority Breakdown:**
- **P0 (Critical):** 47 scenarios — core rules, win validation, scoring, API contracts
- **P1 (High):** 21 scenarios — edge cases, bird mechanics, state recovery
- **P2 (Medium):** 12 scenarios — optional rules, starting instant wins, rare conditions

---

## Open Questions & Contradictions

### OQ-1: Number of Bird Tiles Drawn
**Contradiction:** S1 states "draw two additional tiles" for bird, S2 states "the next tile drawn immediately from the wall is the 'bird'" (singular).  
**Impact:** Fundamental to scoring multiplier calculation.  
**Recommendation:** Clarify with Vasquez which rule applies. If two tiles, need mappings for second tile and cumulative multiplier logic.  
**Priority:** HIGH

### OQ-2: Multiple Win Resolution - Payment Model
**Contradiction:** S1 suggests proximity rule (closest in turn order wins). S2 mentions "Multiple Win" where several players can win off same tile.  
**Impact:** Does one player win (proximity), or do all win simultaneously? If all win, do all receive full payment from discarder, or split?  
**Recommendation:** Define explicit multiple-win resolution: single winner (use proximity) OR all win (define payment splits).  
**Priority:** HIGH

### OQ-3: Dealer Determination After Multiple Starting Instant Wins (Red Dragon Reference)
**Contradiction:** S2 states "player who draws the Red Dragon becomes the dealer" when multiple players have starting instant wins. But Changsha tile set explicitly excludes Red Dragon (红中 / Red Dragon not in 108-tile set per S1 and S2's own tile count).  
**Impact:** Rule is self-contradictory. Likely a translation/adaptation error from another variant.  
**Recommendation:** Remove reference to Red Dragon, or clarify what happens in this rare edge case (e.g., re-deal, or oldest hand by seat order becomes dealer).  
**Priority:** MEDIUM (rare case)

### OQ-4: Starting Hand Instant Wins - Game Continuation
**Contradiction:** S1 and S2 both mention starting instant wins (Four Joys, etc.) can be declared, but also state "the game will continue afterwards" (optional rule). Unclear if hand ends and new hand begins, or if declarer simply collects payment and current hand continues with them still playing.  
**Impact:** Major impact on game flow and implementation. If game continues, declarer still holds tiles and can win again?  
**Recommendation:** Clarify: (A) instant win ends hand, payment made, re-deal; OR (B) instant win is paid, hand continues with tiles kept.  
**Priority:** HIGH

### OQ-5: Kong Replacement Draw - Dice vs. End of Wall
**Contradiction:** S1 says player "can choose one of two options: (1) roll dice to determine from which wall to draw your replacement tile or (2) draw one tile from the end of the wall without rolling dice." S2 adds complexity: dice can only be rolled if hand is in ready state.  
**Impact:** Affects kong strategy and testability. If dice is optional always, simpler. If gated by ready state, more complex validation needed.  
**Recommendation:** Standardize: either (A) dice always optional, OR (B) dice only if ready (S2 model).  
**Priority:** MEDIUM

### OQ-6: Scoring Model - S1 vs. S2
**Contradiction:** S1 uses simplified 1/6/7 point model. S2 uses 10/20/60/70 point model with more granular dealer/non-dealer distinctions.  
**Impact:** Entire scoring system implementation.  
**Recommendation:** Vasquez must choose one model. Recommend S2 model as it's more detailed and closer to traditional Chinese scoring.  
**Priority:** CRITICAL

### OQ-7: Seven Pairs - Can Use 258 Pair or Any Pair?
**Gap:** Both sources say Seven Pairs is Big Win, but don't explicitly state if the pairs must include a 258 pair or if "random eye" exemption applies.  
**Impact:** Hand validation for Seven Pairs.  
**Recommendation:** Assume Big Win exempts 258 pair rule (consistent with All Pungs, Full Flush).  
**Priority:** MEDIUM

### OQ-8: Full Beggar's Hand - Exposed Melds Count
**Contradiction:** S1 says "melded sets through Chow or Pong," but Changsha normally restricts chow. Implies Full Beggar's Hand allows chow calls?  
**Impact:** If Full Beggar's Hand requires all melds exposed via calls, and calls can include chow, need to validate this is allowed.  
**Recommendation:** Clarify if Full Beggar's Hand relaxes chow restrictions or if it's a mistranslation. Likely chows allowed when completing exposed melds.  
**Priority:** LOW (rare hand)

---

## End of Catalog
