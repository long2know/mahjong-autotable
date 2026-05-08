namespace Mahjong.Autotable.Api.Changsha;

/// <summary>
/// Pure-functional event-sourced state machine for Changsha Mahjong.
/// Transitions: (state, command) → (newState, events[])
/// 
/// States per Vasquez §7:
///   SEATING → ROLLING_DICE → DEALING → AWAITING_DISCARD → AWAITING_CLAIM →
///   SCORING → END_HAND → ROTATING_BANKER → (back to ROLLING_DICE or END_GAME)
/// </summary>
public sealed class ChangshaGameStateMachine
{
    private const int SeatCount = 4;
    private const int TotalHands = 16;
    private const int HandsPerRound = 4;
    private const string RngAlgorithmId = "fisher-yates-changsha-v1";

    // ── Factory ────────────────────────────────────────────────────

    public static (ChangshaGameState State, List<ChangshaEvent> Events) CreateGame(
        int seed,
        int[]? botSeatIndexes = null)
    {
        var state = new ChangshaGameState { Seed = seed };
        var events = new List<ChangshaEvent>();

        var bots = botSeatIndexes ?? [1, 2, 3];
        var botSet = new HashSet<int>(bots);

        for (var i = 0; i < SeatCount; i++)
        {
            state.Seats.Add(new ChangshaSeatState
            {
                SeatIndex = i,
                Wind = (Wind)i,
                PlayerId = botSet.Contains(i) ? $"bot-{i}" : $"human-{i}",
                IsBot = botSet.Contains(i),
                IsDealer = i == 0
            });
            state.Hands.Add(new ChangshaHandState { SeatIndex = i });
            state.CumulativeScores[i] = 0;
        }

        state.DealerSeatIndex = 0;
        state.Phase = ChangshaPhase.Seating;
        events.Add(CreateEvent(state, "game-created", -1, detail: $"seed:{seed}"));

        return (state, events);
    }

    // ── Commands ───────────────────────────────────────────────────

    public static List<ChangshaEvent> StartGame(ChangshaGameState state)
    {
        RequirePhase(state, ChangshaPhase.Seating);
        state.Phase = ChangshaPhase.RollingDice;
        return [CreateEvent(state, "game-started", state.DealerSeatIndex,
            detail: $"dealer:{state.DealerSeatIndex},round:{state.RoundWind}")];
    }

    public static List<ChangshaEvent> RollDice(ChangshaGameState state, IDiceService diceService)
    {
        RequirePhase(state, ChangshaPhase.RollingDice);
        var roll = diceService.Roll();
        state.LastDiceRoll = roll;

        var breakPointService = new BreakPointService();
        state.BreakPoint = breakPointService.ComputeBreakPoint(roll.Sum, state.DealerSeatIndex);

        state.Phase = ChangshaPhase.Dealing;
        return [CreateEvent(state, "dice-rolled", state.DealerSeatIndex,
            detail: $"die1:{roll.Die1},die2:{roll.Die2},sum:{roll.Sum}")];
    }

    public static List<ChangshaEvent> Deal(ChangshaGameState state)
    {
        RequirePhase(state, ChangshaPhase.Dealing);

        // Build wall from seed with break point applied
        var rng = new Random(state.Seed);
        var wall = BuildShuffledWall(rng);

        // Apply break point — reorder wall so drawing starts from break point
        if (state.BreakPoint is not null)
        {
            var bp = state.BreakPoint.Value;
            var reordered = new List<int>(ChangshaDeckBuilder.TotalTiles);
            for (var i = bp.TileIndex; i < wall.Count; i++)
                reordered.Add(wall[i]);
            for (var i = 0; i < bp.TileIndex; i++)
                reordered.Add(wall[i]);
            wall = reordered;
        }

        var dealService = new DealService();
        var dealResult = dealService.Deal(wall, state.DealerSeatIndex);

        for (var i = 0; i < SeatCount; i++)
        {
            state.Hands[i].ConcealedTiles = dealResult.Hands[i];
            state.Hands[i].Melds.Clear();
        }

        state.Wall = dealResult.RemainingWall;
        state.WallDrawIndex = 0;
        state.WallBackIndex = state.Wall.Count - 1;
        state.ActiveSeatIndex = state.DealerSeatIndex;
        state.TurnNumber = 1;
        state.DiscardPile.Clear();
        state.ClaimWindow = null;
        state.CurrentWin = null;
        state.CurrentScore = null;

        state.Phase = ChangshaPhase.AwaitingDiscard;

        var events = new List<ChangshaEvent>
        {
            CreateEvent(state, "tiles-dealt", state.DealerSeatIndex,
                detail: $"wall-remaining:{state.Wall.Count}")
        };

        return events;
    }

    public static List<ChangshaEvent> DrawTile(ChangshaGameState state)
    {
        RequirePhase(state, ChangshaPhase.AwaitingDiscard);

        if (state.Wall.Count == 0)
        {
            state.Phase = ChangshaPhase.WallExhausted;
            return [CreateEvent(state, "wall-exhausted", state.ActiveSeatIndex)];
        }

        var tileId = DrawFromFront(state);
        var hand = GetHand(state, state.ActiveSeatIndex);
        hand.ConcealedTiles.Add(tileId);

        return [CreateEvent(state, "tile-drawn", state.ActiveSeatIndex, tileId: tileId,
            detail: $"wall-remaining:{state.Wall.Count}")];
    }

    public static List<ChangshaEvent> Discard(ChangshaGameState state, int seatIndex, int tileId)
    {
        RequirePhase(state, ChangshaPhase.AwaitingDiscard);
        RequireActiveSeat(state, seatIndex);

        var hand = GetHand(state, seatIndex);
        if (!hand.ConcealedTiles.Remove(tileId))
            throw new InvalidOperationException($"Tile {tileId} not in seat {seatIndex}'s hand.");

        state.DiscardPile.Add(new ChangshaDiscard
        {
            SeatIndex = seatIndex,
            TileId = tileId,
            TurnNumber = state.TurnNumber
        });

        var events = new List<ChangshaEvent>
        {
            CreateEvent(state, "tile-discarded", seatIndex, tileId: tileId)
        };

        // Check for claims
        var adjudicator = new ClaimAdjudicator();
        var opportunities = adjudicator.GetOpportunities(seatIndex, tileId, state.Hands);

        if (opportunities.Count > 0)
        {
            state.ClaimWindow = new ChangshaClaimWindow
            {
                DiscardSeatIndex = seatIndex,
                DiscardTileId = tileId,
                Opportunities = opportunities
            };
            state.Phase = ChangshaPhase.AwaitingClaim;
            events.Add(CreateEvent(state, "claim-window-open", seatIndex, tileId: tileId,
                detail: $"opportunities:{opportunities.Count}"));
        }
        else
        {
            AdvanceToNextPlayer(state, seatIndex);
        }

        state.TurnNumber++;
        return events;
    }

    public static List<ChangshaEvent> ResolveClaim(
        ChangshaGameState state,
        int claimingSeatIndex,
        Tables.TableClaimType claimType)
    {
        RequirePhase(state, ChangshaPhase.AwaitingClaim);
        var claimWindow = state.ClaimWindow
            ?? throw new InvalidOperationException("No claim window open.");

        var events = new List<ChangshaEvent>();

        if (claimType == Tables.TableClaimType.Hu)
        {
            return ResolveHuClaim(state, claimingSeatIndex, claimWindow);
        }

        // Remove tile from discard pile
        RemoveLastDiscard(state, claimWindow);

        var hand = GetHand(state, claimingSeatIndex);
        var discardLogical = ChangshaDeckBuilder.GetLogicalTile(claimWindow.DiscardTileId);

        if (claimType == Tables.TableClaimType.Pung)
        {
            var consumed = RemoveMatchingTiles(hand, discardLogical, 2);
            var meldTiles = consumed.Append(claimWindow.DiscardTileId).OrderBy(t => t).ToList();
            hand.Melds.Add(new Meld
            {
                Kind = MeldKind.Pung,
                TileIds = meldTiles,
                ClaimedFromSeatIndex = claimWindow.DiscardSeatIndex
            });
        }
        else if (claimType == Tables.TableClaimType.Kong)
        {
            var consumed = RemoveMatchingTiles(hand, discardLogical, 3);
            var meldTiles = consumed.Append(claimWindow.DiscardTileId).OrderBy(t => t).ToList();
            hand.Melds.Add(new Meld
            {
                Kind = MeldKind.ExposedKong,
                TileIds = meldTiles,
                ClaimedFromSeatIndex = claimWindow.DiscardSeatIndex
            });
        }
        else if (claimType == Tables.TableClaimType.Chow)
        {
            var consumed = RemoveChowTiles(hand, discardLogical);
            var meldTiles = consumed.Append(claimWindow.DiscardTileId).OrderBy(t => t).ToList();
            hand.Melds.Add(new Meld
            {
                Kind = MeldKind.Chow,
                TileIds = meldTiles,
                ClaimedFromSeatIndex = claimWindow.DiscardSeatIndex
            });
        }

        events.Add(CreateEvent(state, "claim-resolved", claimingSeatIndex,
            tileId: claimWindow.DiscardTileId,
            detail: $"type:{claimType}"));

        state.ClaimWindow = null;
        state.ActiveSeatIndex = claimingSeatIndex;

        if (claimType == Tables.TableClaimType.Kong)
        {
            // Kong replacement draw from back of wall
            if (state.Wall.Count > 0)
            {
                var replacementTile = DrawFromBack(state);
                hand.ConcealedTiles.Add(replacementTile);
                events.Add(CreateEvent(state, "kong-replacement-drawn", claimingSeatIndex,
                    tileId: replacementTile));
            }
            else
            {
                state.Phase = ChangshaPhase.WallExhausted;
                events.Add(CreateEvent(state, "wall-exhausted", claimingSeatIndex));
                return events;
            }
        }

        state.Phase = ChangshaPhase.AwaitingDiscard;
        return events;
    }

    public static List<ChangshaEvent> PassClaim(ChangshaGameState state)
    {
        RequirePhase(state, ChangshaPhase.AwaitingClaim);
        var claimWindow = state.ClaimWindow
            ?? throw new InvalidOperationException("No claim window open.");

        state.ClaimWindow = null;
        AdvanceToNextPlayer(state, claimWindow.DiscardSeatIndex);

        return [CreateEvent(state, "claim-passed", claimWindow.DiscardSeatIndex,
            tileId: claimWindow.DiscardTileId)];
    }

    public static List<ChangshaEvent> DeclareSelfDrawWin(ChangshaGameState state, int seatIndex)
    {
        RequirePhase(state, ChangshaPhase.AwaitingDiscard);
        RequireActiveSeat(state, seatIndex);

        var hand = GetHand(state, seatIndex);
        var detector = new ChangshaWinDetector();
        var result = detector.Detect(hand, method: WinMethod.SelfDraw);

        if (!result.IsWin)
            throw new InvalidOperationException("Hand is not a winning hand.");

        state.CurrentWin = new WinResult
        {
            WinningSeatIndex = seatIndex,
            Method = WinMethod.SelfDraw,
            Pattern = result.Pattern!.Value,
            WinningTileId = hand.ConcealedTiles[^1], // last drawn tile
            SourceSeatIndex = seatIndex,
            IsFullFlush = result.IsFullFlush
        };

        state.Phase = ChangshaPhase.Scoring;
        return [CreateEvent(state, "win-declared", seatIndex,
            detail: $"method:selfDraw,pattern:{result.Pattern}")];
    }

    public static List<ChangshaEvent> DeclareConcealedKong(ChangshaGameState state, int seatIndex, int logicalTile)
    {
        RequirePhase(state, ChangshaPhase.AwaitingDiscard);
        RequireActiveSeat(state, seatIndex);

        var hand = GetHand(state, seatIndex);
        var matching = hand.ConcealedTiles
            .Where(t => ChangshaDeckBuilder.GetLogicalTile(t) == logicalTile)
            .OrderBy(t => t)
            .ToList();

        if (matching.Count < 4)
            throw new InvalidOperationException("Not enough tiles for concealed kong.");

        var kongTiles = matching.Take(4).ToList();
        foreach (var t in kongTiles)
            hand.ConcealedTiles.Remove(t);

        hand.Melds.Add(new Meld
        {
            Kind = MeldKind.ConcealedKong,
            TileIds = kongTiles
        });

        var events = new List<ChangshaEvent>
        {
            CreateEvent(state, "concealed-kong", seatIndex, detail: $"logical:{logicalTile}")
        };

        // Replacement draw from back of wall
        if (state.Wall.Count > 0)
        {
            var replacementTile = DrawFromBack(state);
            hand.ConcealedTiles.Add(replacementTile);
            events.Add(CreateEvent(state, "kong-replacement-drawn", seatIndex,
                tileId: replacementTile));
        }
        else
        {
            state.Phase = ChangshaPhase.WallExhausted;
            events.Add(CreateEvent(state, "wall-exhausted", seatIndex));
        }

        return events;
    }

    public static List<ChangshaEvent> DeclareAddedKong(ChangshaGameState state, int seatIndex, int tileId)
    {
        RequirePhase(state, ChangshaPhase.AwaitingDiscard);
        RequireActiveSeat(state, seatIndex);

        var hand = GetHand(state, seatIndex);
        if (!hand.ConcealedTiles.Contains(tileId))
            throw new InvalidOperationException($"Tile {tileId} not in hand.");

        var logicalTile = ChangshaDeckBuilder.GetLogicalTile(tileId);
        var existingPung = hand.Melds.FirstOrDefault(m =>
            m.Kind == MeldKind.Pung &&
            m.TileIds.All(t => ChangshaDeckBuilder.GetLogicalTile(t) == logicalTile));

        if (existingPung is null)
            throw new InvalidOperationException("No existing pung to extend.");

        hand.ConcealedTiles.Remove(tileId);
        existingPung.TileIds.Add(tileId);
        existingPung.TileIds.Sort();
        // Upgrade kind to AddedKong — the meld object is mutable so we create a replacement
        var index = hand.Melds.IndexOf(existingPung);
        hand.Melds[index] = new Meld
        {
            Kind = MeldKind.AddedKong,
            TileIds = existingPung.TileIds,
            ClaimedFromSeatIndex = existingPung.ClaimedFromSeatIndex
        };

        var events = new List<ChangshaEvent>
        {
            CreateEvent(state, "added-kong", seatIndex, tileId: tileId)
        };

        // Replacement draw from back of wall
        if (state.Wall.Count > 0)
        {
            var replacementTile = DrawFromBack(state);
            hand.ConcealedTiles.Add(replacementTile);
            events.Add(CreateEvent(state, "kong-replacement-drawn", seatIndex,
                tileId: replacementTile));
        }
        else
        {
            state.Phase = ChangshaPhase.WallExhausted;
            events.Add(CreateEvent(state, "wall-exhausted", seatIndex));
        }

        return events;
    }

    public static List<ChangshaEvent> Score(ChangshaGameState state)
    {
        RequirePhase(state, ChangshaPhase.Scoring);
        if (state.CurrentWin is null)
            throw new InvalidOperationException("No win to score.");

        var scoringService = new ScoringService();
        state.CurrentScore = scoringService.CalculateScore(
            state.CurrentWin, state.DealerSeatIndex, state.CurrentWin.IsFullFlush);

        // Apply payments to cumulative scores
        foreach (var payment in state.CurrentScore.Payments)
        {
            state.CumulativeScores[payment.ToSeatIndex] += payment.Amount;
            state.CumulativeScores[payment.FromSeatIndex] -= payment.Amount;
        }

        state.Phase = ChangshaPhase.EndHand;
        return [CreateEvent(state, "scoring-complete", state.CurrentWin.WinningSeatIndex,
            detail: $"category:{state.CurrentScore.Category}")];
    }

    public static List<ChangshaEvent> HandleWallExhausted(ChangshaGameState state)
    {
        RequirePhase(state, ChangshaPhase.WallExhausted);
        state.Phase = ChangshaPhase.EndHand;
        return [CreateEvent(state, "draw-hand", -1, detail: "wall-exhausted")];
    }

    public static List<ChangshaEvent> RotateBanker(ChangshaGameState state)
    {
        RequirePhase(state, ChangshaPhase.EndHand);

        var events = new List<ChangshaEvent>();
        var previousDealer = state.DealerSeatIndex;
        string reason;

        if (state.CurrentWin is not null)
        {
            if (state.CurrentWin.WinningSeatIndex == state.DealerSeatIndex)
            {
                // Dealer won → dealer keeps seat
                reason = "dealerRetained";
            }
            else
            {
                // Non-dealer won → rotate counterclockwise
                state.DealerSeatIndex = (state.DealerSeatIndex + 1) % SeatCount;
                reason = "winnerRotation";
            }
        }
        else
        {
            // Draw → rotate counterclockwise
            state.DealerSeatIndex = (state.DealerSeatIndex + 1) % SeatCount;
            reason = "drawRotation";
        }

        // Update seat dealer flags
        foreach (var seat in state.Seats)
            seat.IsDealer = seat.SeatIndex == state.DealerSeatIndex;

        events.Add(CreateEvent(state, "banker-rotated", state.DealerSeatIndex,
            detail: $"previous:{previousDealer},reason:{reason}"));

        // Advance hand/round counters
        state.HandNumber++;
        state.HandInRound++;

        if (state.HandInRound > HandsPerRound)
        {
            state.HandInRound = 1;
            state.RoundNumber++;

            if (state.RoundNumber > 4)
            {
                state.Phase = ChangshaPhase.EndGame;
                events.Add(CreateEvent(state, "game-ended", -1,
                    detail: $"hands:{state.HandNumber - 1}"));
                return events;
            }

            state.RoundWind = (Wind)(state.RoundNumber - 1);
            events.Add(CreateEvent(state, "round-changed", -1,
                detail: $"round:{state.RoundNumber},wind:{state.RoundWind}"));
        }

        // Reset for next hand
        state.CurrentWin = null;
        state.CurrentScore = null;
        state.Phase = ChangshaPhase.RollingDice;

        return events;
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static List<ChangshaEvent> ResolveHuClaim(
        ChangshaGameState state,
        int claimingSeatIndex,
        ChangshaClaimWindow claimWindow)
    {
        // Add tile to hand (for scoring display)
        var hand = GetHand(state, claimingSeatIndex);
        RemoveLastDiscard(state, claimWindow);
        hand.ConcealedTiles.Add(claimWindow.DiscardTileId);

        var detector = new ChangshaWinDetector();
        var result = detector.Detect(hand, claimWindow.DiscardTileId, WinMethod.Discard);

        if (!result.IsWin)
            throw new InvalidOperationException("Claimed Hu but hand is not winning.");

        state.CurrentWin = new WinResult
        {
            WinningSeatIndex = claimingSeatIndex,
            Method = WinMethod.Discard,
            Pattern = result.Pattern!.Value,
            WinningTileId = claimWindow.DiscardTileId,
            SourceSeatIndex = claimWindow.DiscardSeatIndex,
            IsFullFlush = result.IsFullFlush
        };

        state.ClaimWindow = null;
        state.ActiveSeatIndex = claimingSeatIndex;
        state.Phase = ChangshaPhase.Scoring;

        return [CreateEvent(state, "win-declared", claimingSeatIndex,
            tileId: claimWindow.DiscardTileId,
            detail: $"method:discard,pattern:{result.Pattern}")];
    }

    private static void AdvanceToNextPlayer(ChangshaGameState state, int currentSeatIndex)
    {
        state.ActiveSeatIndex = (currentSeatIndex + 1) % SeatCount;
        if (state.Wall.Count == 0)
        {
            state.Phase = ChangshaPhase.WallExhausted;
        }
        else
        {
            state.Phase = ChangshaPhase.AwaitingDiscard;
        }
    }

    private static int DrawFromFront(ChangshaGameState state)
    {
        if (state.Wall.Count == 0)
            throw new InvalidOperationException("Cannot draw from empty wall.");
        var tileId = state.Wall[0];
        state.Wall.RemoveAt(0);
        return tileId;
    }

    private static int DrawFromBack(ChangshaGameState state)
    {
        if (state.Wall.Count == 0)
            throw new InvalidOperationException("Cannot draw from empty wall.");
        var last = state.Wall.Count - 1;
        var tileId = state.Wall[last];
        state.Wall.RemoveAt(last);
        return tileId;
    }

    private static ChangshaHandState GetHand(ChangshaGameState state, int seatIndex) =>
        state.Hands.Single(h => h.SeatIndex == seatIndex);

    private static void RequirePhase(ChangshaGameState state, ChangshaPhase expected)
    {
        if (state.Phase != expected)
            throw new InvalidOperationException(
                $"Expected phase {expected} but current phase is {state.Phase}.");
    }

    private static void RequireActiveSeat(ChangshaGameState state, int seatIndex)
    {
        if (state.ActiveSeatIndex != seatIndex)
            throw new InvalidOperationException(
                $"Seat {seatIndex} is not the active seat (active: {state.ActiveSeatIndex}).");
    }

    private static void RemoveLastDiscard(ChangshaGameState state, ChangshaClaimWindow claimWindow)
    {
        var idx = state.DiscardPile.FindLastIndex(d =>
            d.SeatIndex == claimWindow.DiscardSeatIndex &&
            d.TileId == claimWindow.DiscardTileId);
        if (idx >= 0)
            state.DiscardPile.RemoveAt(idx);
    }

    private static List<int> RemoveMatchingTiles(ChangshaHandState hand, int logicalTile, int count)
    {
        var matches = hand.ConcealedTiles
            .Where(t => ChangshaDeckBuilder.GetLogicalTile(t) == logicalTile)
            .OrderBy(t => t)
            .Take(count)
            .ToList();

        if (matches.Count < count)
            throw new InvalidOperationException($"Not enough matching tiles for claim.");

        foreach (var t in matches)
            hand.ConcealedTiles.Remove(t);

        return matches;
    }

    private static List<int> RemoveChowTiles(ChangshaHandState hand, int discardLogical)
    {
        var rank = discardLogical % 9;
        var suitBase = (discardLogical / 9) * 9;

        // Try each possible chow pattern
        var patterns = new List<(int, int)>();
        if (rank >= 2) patterns.Add((discardLogical - 2, discardLogical - 1));
        if (rank >= 1 && rank <= 7) patterns.Add((discardLogical - 1, discardLogical + 1));
        if (rank <= 6) patterns.Add((discardLogical + 1, discardLogical + 2));

        foreach (var (a, b) in patterns)
        {
            var tileA = hand.ConcealedTiles.FirstOrDefault(t => ChangshaDeckBuilder.GetLogicalTile(t) == a, -1);
            var tileB = hand.ConcealedTiles.FirstOrDefault(t => ChangshaDeckBuilder.GetLogicalTile(t) == b, -1);

            if (tileA >= 0 && tileB >= 0)
            {
                hand.ConcealedTiles.Remove(tileA);
                hand.ConcealedTiles.Remove(tileB);
                return [tileA, tileB];
            }
        }

        throw new InvalidOperationException("Cannot find tiles for chow.");
    }

    private static List<int> BuildShuffledWall(Random rng)
    {
        var wall = ChangshaDeckBuilder.Build();
        // Fisher-Yates shuffle
        for (var i = wall.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (wall[i], wall[j]) = (wall[j], wall[i]);
        }
        return wall;
    }

    private static ChangshaEvent CreateEvent(
        ChangshaGameState state,
        string eventType,
        int seatIndex,
        int? tileId = null,
        string detail = "")
    {
        state.EventSequence++;
        state.StateVersion++;
        var evt = new ChangshaEvent
        {
            Sequence = state.EventSequence,
            EventType = eventType,
            SeatIndex = seatIndex,
            TurnNumber = state.TurnNumber,
            TileId = tileId,
            Detail = detail,
            OccurredUtc = DateTime.UtcNow
        };
        state.EventLog.Add(evt);
        return evt;
    }
}
