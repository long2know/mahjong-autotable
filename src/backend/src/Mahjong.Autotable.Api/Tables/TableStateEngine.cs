namespace Mahjong.Autotable.Api.Tables;

public interface ITableStateEngine
{
    TableGameState CreateInitialState(IReadOnlyCollection<int>? botSeatIndexes = null, int? seed = null);
    void NormalizePersistedState(TableGameState state, int persistedStateVersion);
    TableGameState ReplayFromSeed(TableGameState snapshot);
    ReplayVerificationResult VerifyReplayIntegrity(TableGameState snapshot);
    DiscardActionResult ApplyHumanDiscard(TableGameState state, int seatIndex, int tileId);
    ClaimResolutionResult ResolveClaimWindow(TableGameState state, string decision);
    BotAdvanceResult AdvanceBots(TableGameState state, int maxActions);
    BotAdvanceResult AdvanceBotsUntilHumanTurnOrWallExhausted(TableGameState state);
}

public sealed class DiscardActionResult
{
    public required TableAction DiscardAction { get; init; }
    public required TableAction? DrawAction { get; init; }
}

public sealed class ReplayVerificationResult
{
    public required bool IntegrityMatch { get; init; }
    public required string ExpectedStateHash { get; init; }
    public required string ReplayedStateHash { get; init; }
    public required int ReplayedStateVersion { get; init; }
    public required long ReplayedActionSequence { get; init; }
}

public sealed class ClaimResolutionResult
{
    public required string AppliedDecision { get; init; }
    public required TableAction ResolutionAction { get; init; }
    public required TableAction? DrawAction { get; init; }
}

public sealed class TableStateEngine : ITableStateEngine
{
    private const string ClaimResolvePassActionType = "claim-resolve-pass";
    private const string ClaimResolveTakeSelectedActionType = "claim-resolve-take-selected";
    private const string ClaimTakeSelectedDetailPrefix = "take-selected-v2:";
    private const string ClaimTakeSelectedLegacyDetailPrefix = "take-selected:";
    public const int SeatCount = 4;
    public const int TotalTiles = 136;
    public const string RngAlgorithmId = "fisher-yates-v1";
    public const string ClaimPrecedencePolicy = "hu>kong>pung>chow|next-seat-tiebreak";

    public TableGameState CreateInitialState(IReadOnlyCollection<int>? botSeatIndexes = null, int? seed = null)
    {
        var botSet = NormalizeBots(botSeatIndexes);
        var resolvedSeed = seed ?? Random.Shared.Next(int.MinValue, int.MaxValue);

        var seats = Enumerable.Range(0, SeatCount)
            .Select(index => new TableSeatState
            {
                SeatIndex = index,
                SeatType = botSet.Contains(index) ? TableSeatType.Bot : TableSeatType.Human,
                PlayerId = botSet.Contains(index) ? $"bot-{index}" : $"human-{index}"
            })
            .ToList();

        var wall = CreateShuffledWall(resolvedSeed);
        var hands = Enumerable.Range(0, SeatCount)
            .Select(index => new TableSeatHandState { SeatIndex = index })
            .ToList();
        var exposedMelds = Enumerable.Range(0, SeatCount)
            .Select(index => new TableSeatMeldState { SeatIndex = index })
            .ToList();

        for (var round = 0; round < 13; round++)
        {
            for (var seatIndex = 0; seatIndex < SeatCount; seatIndex++)
            {
                hands[seatIndex].Tiles.Add(DrawFromWall(wall));
            }
        }

        hands[0].Tiles.Add(DrawFromWall(wall));

        var state = new TableGameState
        {
            StateVersion = 1,
            ActionSequence = 0,
            ActiveSeat = 0,
            TurnNumber = 1,
            DrawNumber = 1,
            Metadata = new TableStateMetadata
            {
                Seed = resolvedSeed,
                AlgorithmId = RngAlgorithmId
            },
            Seats = seats,
            Hands = hands,
            ExposedMelds = exposedMelds,
            Wall = wall
        };

        RefreshIntegrity(state);
        return state;
    }

    public void NormalizePersistedState(TableGameState state, int persistedStateVersion)
    {
        state.ExposedMelds ??= [];
        EnsureSeatMeldCollections(state);
        ValidateState(state);
        state.StateVersion = Math.Max(state.StateVersion, persistedStateVersion);
        state.ActionSequence = Math.Max(state.ActionSequence, GetMaxActionSequence(state));
        state.Metadata ??= new TableStateMetadata();
        if (string.IsNullOrWhiteSpace(state.Metadata.AlgorithmId))
        {
            state.Metadata.AlgorithmId = RngAlgorithmId;
        }

        if (state.LastAction is null && state.ActionLog.Count > 0)
        {
            state.LastAction = ToLastAction(state.ActionLog[^1]);
        }

        if (state.ClaimWindow is not null && string.IsNullOrWhiteSpace(state.ClaimWindow.PrecedencePolicy))
        {
            state.ClaimWindow.PrecedencePolicy = ClaimPrecedencePolicy;
        }

        RefreshIntegrity(state);
    }

    public TableGameState ReplayFromSeed(TableGameState snapshot)
    {
        ValidateState(snapshot);
        snapshot.Metadata ??= new TableStateMetadata();

        var botSeatIndexes = snapshot.Seats
            .Where(seat => seat.SeatType == TableSeatType.Bot)
            .Select(seat => seat.SeatIndex)
            .ToArray();

        var replay = CreateInitialState(botSeatIndexes, snapshot.Metadata.Seed);
        var actions = snapshot.ActionLog
            .OrderBy(action => action.Sequence);

        foreach (var action in actions)
        {
            if (action.ActionType.Equals("discard", StringComparison.OrdinalIgnoreCase))
            {
                var tileId = action.TileId;
                if (!tileId.HasValue)
                {
                    ThrowInvariant(replay, "Discard action is missing tile id for replay.");
                }

                var replaySeat = GetSeat(replay, action.SeatIndex);
                var requiredSeatType = replaySeat.SeatType == TableSeatType.Human
                    ? TableSeatType.Human
                    : TableSeatType.Bot;
                _ = ApplyDiscard(replay, action.SeatIndex, tileId.GetValueOrDefault(), requiredSeatType);
                continue;
            }

            if (action.ActionType.Equals(ClaimResolvePassActionType, StringComparison.OrdinalIgnoreCase))
            {
                _ = ResolveClaimWindow(replay, TableClaimResolutionDecisionValues.Pass);
                continue;
            }

            if (action.ActionType.Equals(ClaimResolveTakeSelectedActionType, StringComparison.OrdinalIgnoreCase))
            {
                var replayLegacyTakeSelected = IsLegacyTakeSelectedDetail(action.Detail);
                _ = ResolveClaimWindowInternal(
                    replay,
                    TableClaimResolutionDecisionValues.TakeSelected,
                    replayLegacyTakeSelected);
            }
        }

        return replay;
    }

    public ReplayVerificationResult VerifyReplayIntegrity(TableGameState snapshot)
    {
        var replay = ReplayFromSeed(snapshot);
        return new ReplayVerificationResult
        {
            IntegrityMatch = string.Equals(
                snapshot.Integrity.StateHash,
                replay.Integrity.StateHash,
                StringComparison.Ordinal),
            ExpectedStateHash = snapshot.Integrity.StateHash,
            ReplayedStateHash = replay.Integrity.StateHash,
            ReplayedStateVersion = replay.StateVersion,
            ReplayedActionSequence = replay.ActionSequence
        };
    }

    public DiscardActionResult ApplyHumanDiscard(TableGameState state, int seatIndex, int tileId)
        => ApplyDiscard(state, seatIndex, tileId, TableSeatType.Human);

    public ClaimResolutionResult ResolveClaimWindow(TableGameState state, string decision)
    {
        return ResolveClaimWindowInternal(state, decision, replayLegacyTakeSelected: false);
    }

    private static ClaimResolutionResult ResolveClaimWindowInternal(
        TableGameState state,
        string decision,
        bool replayLegacyTakeSelected)
    {
        ValidateState(state);

        if (state.Phase is TableTurnPhase.WallExhausted or TableTurnPhase.RoundComplete)
        {
            ThrowRule(state, TableActionErrorCodes.RoundNotActive, "Round is no longer active.");
        }

        if (state.Phase != TableTurnPhase.AwaitingClaimResolution || state.ClaimWindow is null)
        {
            ThrowRule(state, TableActionErrorCodes.ClaimWindowNotOpen, "No claim window is currently awaiting resolution.");
        }

        var normalizedDecision = NormalizeClaimDecision(state, decision);
        return normalizedDecision switch
        {
            TableClaimResolutionDecisionValues.Pass => ApplyClaimPass(state),
            TableClaimResolutionDecisionValues.TakeSelected => replayLegacyTakeSelected
                ? ApplyClaimTakeSelectedLegacy(state)
                : ApplyClaimTakeSelected(state),
            _ => throw new InvalidOperationException($"Unsupported claim resolution decision '{normalizedDecision}'.")
        };
    }

    public BotAdvanceResult AdvanceBots(TableGameState state, int maxActions)
    {
        ValidateState(state);
        var boundedActions = Math.Max(1, maxActions);
        var actions = new List<TableAction>();

        while (actions.Count < boundedActions)
        {
            if (state.Phase == TableTurnPhase.RoundComplete)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.RoundComplete
                };
            }

            if (state.Phase == TableTurnPhase.AwaitingClaimResolution)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.ClaimResolutionRequired
                };
            }

            if (state.Phase == TableTurnPhase.WallExhausted)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.WallExhausted
                };
            }

            var seat = GetSeat(state, state.ActiveSeat);
            if (seat.SeatType == TableSeatType.Human)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.HumanTurn
                };
            }

            var requiredActions = state.Wall.Count > 0 ? 2 : 1;
            if (actions.Count + requiredActions > boundedActions)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.MaxActionsReached
                };
            }

            var hand = GetHandForSeat(state, seat.SeatIndex);
            var tileId = SelectBotDiscardTile(hand.Tiles);
            var result = ApplyDiscard(state, seat.SeatIndex, tileId, TableSeatType.Bot);
            actions.Add(result.DiscardAction);
            if (result.DrawAction is not null)
            {
                actions.Add(result.DrawAction);
            }

            if (state.Phase == TableTurnPhase.WallExhausted)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.WallExhausted
                };
            }
        }

        return new BotAdvanceResult
        {
            Actions = actions,
            StopReason = BotAdvanceStopReason.MaxActionsReached
        };
    }

    public BotAdvanceResult AdvanceBotsUntilHumanTurnOrWallExhausted(TableGameState state)
    {
        ValidateState(state);
        var actions = new List<TableAction>();

        while (true)
        {
            if (state.Phase == TableTurnPhase.RoundComplete)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.RoundComplete
                };
            }

            if (state.Phase == TableTurnPhase.AwaitingClaimResolution)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.ClaimResolutionRequired
                };
            }

            if (state.Phase == TableTurnPhase.WallExhausted)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.WallExhausted
                };
            }

            var seat = GetSeat(state, state.ActiveSeat);
            if (seat.SeatType == TableSeatType.Human)
            {
                return new BotAdvanceResult
                {
                    Actions = actions,
                    StopReason = BotAdvanceStopReason.HumanTurn
                };
            }

            var hand = GetHandForSeat(state, seat.SeatIndex);
            var tileId = SelectBotDiscardTile(hand.Tiles);
            var result = ApplyDiscard(state, seat.SeatIndex, tileId, TableSeatType.Bot);
            actions.Add(result.DiscardAction);
            if (result.DrawAction is not null)
            {
                actions.Add(result.DrawAction);
            }
        }
    }

    private static HashSet<int> NormalizeBots(IReadOnlyCollection<int>? botSeatIndexes)
    {
        if (botSeatIndexes is null || botSeatIndexes.Count == 0)
        {
            return [1, 2, 3];
        }

        if (botSeatIndexes.Count >= SeatCount)
        {
            throw new ArgumentException("At least one human seat is required.", nameof(botSeatIndexes));
        }

        var set = new HashSet<int>(botSeatIndexes);
        if (set.Count != botSeatIndexes.Count)
        {
            throw new ArgumentException("Bot seat indexes must be unique.", nameof(botSeatIndexes));
        }

        if (set.Any(index => index is < 0 or >= SeatCount))
        {
            throw new ArgumentException("Bot seat indexes must be between 0 and 3.", nameof(botSeatIndexes));
        }

        return set;
    }

    private static void EnsureSeatMeldCollections(TableGameState state)
    {
        var existingSeatIndexes = state.ExposedMelds
            .Select(seatMelds => seatMelds.SeatIndex)
            .ToHashSet();

        foreach (var seatIndex in Enumerable.Range(0, SeatCount))
        {
            if (existingSeatIndexes.Contains(seatIndex))
            {
                continue;
            }

            state.ExposedMelds.Add(new TableSeatMeldState
            {
                SeatIndex = seatIndex
            });
        }
    }

    private static void ValidateState(TableGameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Seats.Count != SeatCount)
        {
            ThrowInvariant(state, "Table state must contain exactly four seats.");
        }

        if (state.ActiveSeat is < 0 or >= SeatCount)
        {
            ThrowInvariant(state, "Active seat must be between 0 and 3.");
        }

        var expectedSeatIndexes = Enumerable.Range(0, SeatCount);
        var seatIndexes = state.Seats.Select(seat => seat.SeatIndex).OrderBy(index => index).ToArray();
        if (!seatIndexes.SequenceEqual(expectedSeatIndexes))
        {
            ThrowInvariant(state, "Seat indexes must map to 0-3 exactly once.");
        }

        if (state.Hands.Count != SeatCount)
        {
            ThrowInvariant(state, "Table state must contain exactly four hands.");
        }

        var handSeatIndexes = state.Hands.Select(hand => hand.SeatIndex).OrderBy(index => index).ToArray();
        if (!handSeatIndexes.SequenceEqual(expectedSeatIndexes))
        {
            ThrowInvariant(state, "Hand seat indexes must map to 0-3 exactly once.");
        }

        state.ExposedMelds ??= [];
        var meldSeatIndexes = state.ExposedMelds.Select(seatMelds => seatMelds.SeatIndex).ToArray();
        if (meldSeatIndexes.Any(index => index is < 0 or >= SeatCount))
        {
            ThrowInvariant(state, "Meld seat indexes must be between 0 and 3.");
        }

        if (meldSeatIndexes.Length != meldSeatIndexes.Distinct().Count())
        {
            ThrowInvariant(state, "Meld seat indexes must be unique.");
        }

        if (state.Phase == TableTurnPhase.RoundComplete && state.Win is null)
        {
            ThrowInvariant(state, "Round-complete phase requires win metadata.");
        }

        if (state.Phase != TableTurnPhase.RoundComplete && state.Win is not null)
        {
            ThrowInvariant(state, "Win metadata is only valid during round-complete phase.");
        }

        if (state.Win is not null && (state.Win.WinningSeatIndex is < 0 or >= SeatCount || state.Win.SourceSeatIndex is < 0 or >= SeatCount))
        {
            ThrowInvariant(state, "Win metadata seat indexes must be between 0 and 3.");
        }

        var meldTileCount = state.ExposedMelds.Sum(seatMelds => seatMelds.Melds.Sum(meld => meld.TileIds.Count));
        var trackedTiles = state.Wall.Count + state.Hands.Sum(hand => hand.Tiles.Count) + state.DiscardPile.Count + meldTileCount;
        if (trackedTiles != TotalTiles)
        {
            ThrowInvariant(state, "Tile conservation invariant failed.");
        }
    }

    private static DiscardActionResult ApplyDiscard(
        TableGameState state,
        int seatIndex,
        int tileId,
        TableSeatType requiredSeatType)
    {
        ValidateState(state);

        if (state.Phase is TableTurnPhase.WallExhausted or TableTurnPhase.RoundComplete)
        {
            ThrowRule(state, TableActionErrorCodes.RoundNotActive, "Round is no longer active.");
        }

        if (state.Phase != TableTurnPhase.AwaitingDiscard)
        {
            ThrowRule(state, TableActionErrorCodes.InvalidPhase, "Discards are only allowed while awaiting discard.");
        }

        if (seatIndex != state.ActiveSeat)
        {
            ThrowRule(state, TableActionErrorCodes.NotActiveSeat, "Only the active seat can discard.");
        }

        var seat = GetSeat(state, seatIndex);
        if (seat.SeatType != requiredSeatType)
        {
            ThrowRule(state, TableActionErrorCodes.NotActiveSeat, $"Seat {seatIndex} is not a {requiredSeatType} seat.");
        }

        var hand = GetHandForSeat(state, seatIndex);
        if (!hand.Tiles.Remove(tileId))
        {
            ThrowRule(state, TableActionErrorCodes.TileNotInHand, "Tile is not in the active seat hand.");
        }

        state.Win = null;

        var discardAction = AppendAction(
            state,
            seatIndex,
            state.TurnNumber,
            "discard",
            tileId,
            $"tile-{tileId}");

        state.DiscardPile.Add(new TableDiscard
        {
            SeatIndex = seatIndex,
            TileId = tileId,
            TurnNumber = state.TurnNumber,
            OccurredUtc = discardAction.OccurredUtc
        });
        state.ClaimWindow = BuildClaimWindowState(
            state,
            seatIndex,
            tileId,
            state.TurnNumber,
            discardAction.Sequence);
        state.TurnNumber++;

        if (state.ClaimWindow is not null)
        {
            state.Phase = TableTurnPhase.AwaitingClaimResolution;
            RefreshIntegrity(state);
            discardAction.StateHash = state.Integrity.StateHash;
            return new DiscardActionResult
            {
                DiscardAction = discardAction,
                DrawAction = null
            };
        }

        var canDraw = PreparePostDiscardContinuation(state, seatIndex);
        RefreshIntegrity(state);
        discardAction.StateHash = state.Integrity.StateHash;
        if (!canDraw)
        {
            return new DiscardActionResult
            {
                DiscardAction = discardAction,
                DrawAction = null
            };
        }

        var drawAction = DrawForActiveSeat(state);
        RefreshIntegrity(state);
        if (drawAction is not null)
        {
            drawAction.StateHash = state.Integrity.StateHash;
        }

        return new DiscardActionResult
        {
            DiscardAction = discardAction,
            DrawAction = drawAction
        };
    }

    private static TableClaimWindowState? BuildClaimWindowState(
        TableGameState state,
        int discardSeatIndex,
        int discardTileId,
        int discardTurnNumber,
        long sourceActionSequence)
    {
        var opportunities = GetClaimOpportunities(state, discardSeatIndex, discardTileId)
            .ToList();

        if (opportunities.Count == 0)
        {
            return null;
        }

        return new TableClaimWindowState
        {
            SourceActionSequence = sourceActionSequence,
            DiscardSeatIndex = discardSeatIndex,
            DiscardTileId = discardTileId,
            DiscardTurnNumber = discardTurnNumber,
            PrecedencePolicy = ClaimPrecedencePolicy,
            Opportunities = opportunities,
            SelectedOpportunity = SelectClaimOpportunity(opportunities, discardSeatIndex)
        };
    }

    private static IEnumerable<TableClaimOpportunity> GetClaimOpportunities(
        TableGameState state,
        int discardSeatIndex,
        int discardTileId)
    {
        var discardLogical = discardTileId / 4;
        foreach (var seat in state.Seats)
        {
            if (seat.SeatIndex == discardSeatIndex)
            {
                continue;
            }

            var hand = GetHandForSeat(state, seat.SeatIndex);
            var logicalTiles = hand.Tiles.Select(tile => tile / 4).ToList();
            var matchingCount = logicalTiles.Count(tile => tile == discardLogical);

            if (IsHuCandidate(logicalTiles, discardLogical))
            {
                yield return new TableClaimOpportunity
                {
                    SeatIndex = seat.SeatIndex,
                    ClaimType = TableClaimType.Hu,
                    Priority = GetClaimPriority(TableClaimType.Hu)
                };
            }

            if (matchingCount >= 3)
            {
                yield return new TableClaimOpportunity
                {
                    SeatIndex = seat.SeatIndex,
                    ClaimType = TableClaimType.Kong,
                    Priority = GetClaimPriority(TableClaimType.Kong)
                };
            }
            else if (matchingCount >= 2)
            {
                yield return new TableClaimOpportunity
                {
                    SeatIndex = seat.SeatIndex,
                    ClaimType = TableClaimType.Pung,
                    Priority = GetClaimPriority(TableClaimType.Pung)
                };
            }

            if (IsNextSeat(discardSeatIndex, seat.SeatIndex) && IsChowCandidate(logicalTiles, discardLogical))
            {
                yield return new TableClaimOpportunity
                {
                    SeatIndex = seat.SeatIndex,
                    ClaimType = TableClaimType.Chow,
                    Priority = GetClaimPriority(TableClaimType.Chow)
                };
            }
        }
    }

    private static TableClaimOpportunity? SelectClaimOpportunity(
        IReadOnlyCollection<TableClaimOpportunity> opportunities,
        int discardSeatIndex) =>
        opportunities
            .OrderByDescending(opportunity => opportunity.Priority)
            .ThenBy(opportunity => GetRelativeDistance(discardSeatIndex, opportunity.SeatIndex))
            .ThenBy(opportunity => opportunity.SeatIndex)
            .ThenBy(opportunity => opportunity.ClaimType)
            .FirstOrDefault();

    private static int GetClaimPriority(TableClaimType claimType) =>
        claimType switch
        {
            TableClaimType.Hu => 4,
            TableClaimType.Kong => 3,
            TableClaimType.Pung => 2,
            TableClaimType.Chow => 1,
            _ => 0
        };

    private static bool IsNextSeat(int discardSeatIndex, int seatIndex) =>
        (discardSeatIndex + 1) % SeatCount == seatIndex;

    private static int GetRelativeDistance(int discardSeatIndex, int seatIndex) =>
        (seatIndex - discardSeatIndex + SeatCount) % SeatCount;

    private static bool IsChowCandidate(IReadOnlyCollection<int> logicalTiles, int discardLogical)
    {
        if (discardLogical is < 0 or >= 27)
        {
            return false;
        }

        var suitOffset = discardLogical / 9;
        var rank = discardLogical % 9;
        var suitedTiles = logicalTiles
            .Where(tile => tile / 9 == suitOffset)
            .Select(tile => tile % 9)
            .ToHashSet();

        var hasLowerRun = rank >= 2 && suitedTiles.Contains(rank - 2) && suitedTiles.Contains(rank - 1);
        var hasMiddleRun = rank >= 1 && rank <= 7 && suitedTiles.Contains(rank - 1) && suitedTiles.Contains(rank + 1);
        var hasUpperRun = rank <= 6 && suitedTiles.Contains(rank + 1) && suitedTiles.Contains(rank + 2);
        return hasLowerRun || hasMiddleRun || hasUpperRun;
    }

    private static bool IsHuCandidate(IReadOnlyCollection<int> logicalTiles, int discardLogical)
    {
        if (discardLogical < 0 || discardLogical >= 34)
        {
            return false;
        }

        var counts = new int[34];
        foreach (var logicalTile in logicalTiles)
        {
            if (logicalTile < 0 || logicalTile >= counts.Length)
            {
                return false;
            }

            counts[logicalTile]++;
        }

        counts[discardLogical]++;
        if (counts.Sum() % 3 != 2)
        {
            return false;
        }

        return IsWinningHand(counts);
    }

    private static bool IsWinningHand(int[] counts)
    {
        for (var pairLogical = 0; pairLogical < counts.Length; pairLogical++)
        {
            if (counts[pairLogical] < 2)
            {
                continue;
            }

            counts[pairLogical] -= 2;
            var winning = CanFormMelds(counts);
            counts[pairLogical] += 2;
            if (winning)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanFormMelds(int[] counts)
    {
        var logical = Array.FindIndex(counts, count => count > 0);
        if (logical < 0)
        {
            return true;
        }

        if (counts[logical] >= 3)
        {
            counts[logical] -= 3;
            var tripletSuccess = CanFormMelds(counts);
            counts[logical] += 3;
            if (tripletSuccess)
            {
                return true;
            }
        }

        var canFormSequence = logical < 27 && logical % 9 <= 6 && counts[logical + 1] > 0 && counts[logical + 2] > 0;
        if (!canFormSequence)
        {
            return false;
        }

        counts[logical]--;
        counts[logical + 1]--;
        counts[logical + 2]--;
        var sequenceSuccess = CanFormMelds(counts);
        counts[logical]++;
        counts[logical + 1]++;
        counts[logical + 2]++;
        return sequenceSuccess;
    }

    private static bool IsLegacyTakeSelectedDetail(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return true;
        }

        return detail.StartsWith(ClaimTakeSelectedLegacyDetailPrefix, StringComparison.OrdinalIgnoreCase)
            && !detail.StartsWith(ClaimTakeSelectedDetailPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeClaimDecision(TableGameState state, string decision)
    {
        if (string.IsNullOrWhiteSpace(decision))
        {
            ThrowRule(
                state,
                TableActionErrorCodes.InvalidClaimDecision,
                $"Claim decision must be one of: {TableClaimResolutionDecisionValues.Pass}, {TableClaimResolutionDecisionValues.TakeSelected}.");
        }

        var normalized = decision.Trim().ToLowerInvariant();
        if (normalized is TableClaimResolutionDecisionValues.Pass or TableClaimResolutionDecisionValues.TakeSelected)
        {
            return normalized;
        }

        ThrowRule(
            state,
            TableActionErrorCodes.InvalidClaimDecision,
            $"Claim decision must be one of: {TableClaimResolutionDecisionValues.Pass}, {TableClaimResolutionDecisionValues.TakeSelected}.");
        return string.Empty;
    }

    private static ClaimResolutionResult ApplyClaimPass(TableGameState state)
    {
        var claimWindow = state.ClaimWindow!;
        var resolutionAction = AppendAction(
            state,
            claimWindow.DiscardSeatIndex,
            claimWindow.DiscardTurnNumber,
            ClaimResolvePassActionType,
            claimWindow.DiscardTileId,
            TableClaimResolutionDecisionValues.Pass);

        state.ClaimWindow = null;
        state.Win = null;
        var canDraw = PreparePostDiscardContinuation(state, claimWindow.DiscardSeatIndex);
        RefreshIntegrity(state);
        resolutionAction.StateHash = state.Integrity.StateHash;
        if (!canDraw)
        {
            return new ClaimResolutionResult
            {
                AppliedDecision = TableClaimResolutionDecisionValues.Pass,
                ResolutionAction = resolutionAction,
                DrawAction = null
            };
        }

        var drawAction = DrawForActiveSeat(state);
        RefreshIntegrity(state);
        if (drawAction is not null)
        {
            drawAction.StateHash = state.Integrity.StateHash;
        }

        return new ClaimResolutionResult
        {
            AppliedDecision = TableClaimResolutionDecisionValues.Pass,
            ResolutionAction = resolutionAction,
            DrawAction = drawAction
        };
    }

    private static ClaimResolutionResult ApplyClaimTakeSelected(TableGameState state)
    {
        var claimWindow = state.ClaimWindow!;
        if (claimWindow.SelectedOpportunity is null)
        {
            ThrowRule(state, TableActionErrorCodes.ClaimSelectionUnavailable, "No selected claim opportunity is available.");
        }

        var selected = claimWindow.SelectedOpportunity!;
        if (selected.ClaimType == TableClaimType.Hu)
        {
            return ApplyClaimTakeSelectedHu(state, claimWindow, selected);
        }

        RemoveClaimedDiscard(state, claimWindow);

        var claimantHand = GetHandForSeat(state, selected.SeatIndex);
        var consumedTiles = ConsumeMeldTilesFromHand(state, claimantHand, claimWindow.DiscardTileId, selected.ClaimType);
        var meldTileIds = consumedTiles
            .Append(claimWindow.DiscardTileId)
            .OrderBy(tileId => tileId / 4)
            .ThenBy(tileId => tileId)
            .ToList();

        var seatMelds = GetOrCreateSeatMelds(state, selected.SeatIndex);
        seatMelds.Melds.Add(new TableMeldState
        {
            ClaimType = selected.ClaimType,
            TileIds = meldTileIds,
            ClaimedFromSeatIndex = claimWindow.DiscardSeatIndex,
            SourceTurnNumber = claimWindow.DiscardTurnNumber,
            SourceActionSequence = claimWindow.SourceActionSequence
        });

        var resolutionAction = AppendAction(
            state,
            selected.SeatIndex,
            claimWindow.DiscardTurnNumber,
            ClaimResolveTakeSelectedActionType,
            claimWindow.DiscardTileId,
            $"{ClaimTakeSelectedDetailPrefix}{selected.SeatIndex}:{selected.ClaimType.ToString().ToLowerInvariant()}");

        state.ClaimWindow = null;
        state.Win = null;
        state.ActiveSeat = selected.SeatIndex;
        state.Phase = TableTurnPhase.AwaitingDiscard;
        RefreshIntegrity(state);
        resolutionAction.StateHash = state.Integrity.StateHash;

        TableAction? drawAction = null;
        if (selected.ClaimType == TableClaimType.Kong)
        {
            if (state.Wall.Count == 0)
            {
                state.Phase = TableTurnPhase.WallExhausted;
                RefreshIntegrity(state);
                resolutionAction.StateHash = state.Integrity.StateHash;
            }
            else
            {
                drawAction = DrawForActiveSeat(state);
                RefreshIntegrity(state);
                drawAction.StateHash = state.Integrity.StateHash;
            }
        }

        return new ClaimResolutionResult
        {
            AppliedDecision = TableClaimResolutionDecisionValues.TakeSelected,
            ResolutionAction = resolutionAction,
            DrawAction = drawAction
        };
    }

    private static ClaimResolutionResult ApplyClaimTakeSelectedHu(
        TableGameState state,
        TableClaimWindowState claimWindow,
        TableClaimOpportunity selected)
    {
        RemoveClaimedDiscard(state, claimWindow);

        var claimantHand = GetHandForSeat(state, selected.SeatIndex);
        claimantHand.Tiles.Add(claimWindow.DiscardTileId);

        var resolutionAction = AppendAction(
            state,
            selected.SeatIndex,
            claimWindow.DiscardTurnNumber,
            ClaimResolveTakeSelectedActionType,
            claimWindow.DiscardTileId,
            $"{ClaimTakeSelectedDetailPrefix}{selected.SeatIndex}:{selected.ClaimType.ToString().ToLowerInvariant()}");

        state.ClaimWindow = null;
        state.ActiveSeat = selected.SeatIndex;
        state.Phase = TableTurnPhase.RoundComplete;
        state.Win = new TableWinState
        {
            WinningSeatIndex = selected.SeatIndex,
            WinningClaimType = selected.ClaimType,
            WinningTileId = claimWindow.DiscardTileId,
            SourceSeatIndex = claimWindow.DiscardSeatIndex,
            SourceTurnNumber = claimWindow.DiscardTurnNumber,
            SourceActionSequence = claimWindow.SourceActionSequence
        };
        RefreshIntegrity(state);
        resolutionAction.StateHash = state.Integrity.StateHash;

        return new ClaimResolutionResult
        {
            AppliedDecision = TableClaimResolutionDecisionValues.TakeSelected,
            ResolutionAction = resolutionAction,
            DrawAction = null
        };
    }

    private static ClaimResolutionResult ApplyClaimTakeSelectedLegacy(TableGameState state)
    {
        var claimWindow = state.ClaimWindow!;
        if (claimWindow.SelectedOpportunity is null)
        {
            ThrowRule(state, TableActionErrorCodes.ClaimSelectionUnavailable, "No selected claim opportunity is available.");
        }

        RemoveClaimedDiscard(state, claimWindow);

        var selected = claimWindow.SelectedOpportunity!;
        var claimantHand = GetHandForSeat(state, selected.SeatIndex);
        claimantHand.Tiles.Add(claimWindow.DiscardTileId);

        var resolutionAction = AppendAction(
            state,
            selected.SeatIndex,
            claimWindow.DiscardTurnNumber,
            ClaimResolveTakeSelectedActionType,
            claimWindow.DiscardTileId,
            $"{TableClaimResolutionDecisionValues.TakeSelected}:{selected.SeatIndex}:{selected.ClaimType.ToString().ToLowerInvariant()}");

        state.ClaimWindow = null;
        state.Win = null;
        state.ActiveSeat = selected.SeatIndex;
        state.Phase = TableTurnPhase.AwaitingDiscard;
        RefreshIntegrity(state);
        resolutionAction.StateHash = state.Integrity.StateHash;

        return new ClaimResolutionResult
        {
            AppliedDecision = TableClaimResolutionDecisionValues.TakeSelected,
            ResolutionAction = resolutionAction,
            DrawAction = null
        };
    }

    private static IReadOnlyList<int> ConsumeMeldTilesFromHand(
        TableGameState state,
        TableSeatHandState claimantHand,
        int discardTileId,
        TableClaimType claimType)
    {
        var discardLogical = discardTileId / 4;
        return claimType switch
        {
            TableClaimType.Pung => RemoveMatchingTilesForClaim(state, claimantHand, discardLogical, 2, "pung"),
            TableClaimType.Kong => RemoveMatchingTilesForClaim(state, claimantHand, discardLogical, 3, "kong"),
            TableClaimType.Chow => RemoveChowTilesForClaim(state, claimantHand, discardLogical),
            TableClaimType.Hu => ThrowUnsupportedHuClaim(state),
            _ => ThrowUnknownClaimType(state, claimType)
        };
    }

    private static IReadOnlyList<int> RemoveMatchingTilesForClaim(
        TableGameState state,
        TableSeatHandState claimantHand,
        int discardLogical,
        int count,
        string claimName)
    {
        var matches = claimantHand.Tiles
            .Where(tileId => tileId / 4 == discardLogical)
            .OrderBy(tileId => tileId)
            .Take(count)
            .ToList();
        if (matches.Count != count)
        {
            ThrowInvariant(state, $"Seat {claimantHand.SeatIndex} cannot satisfy {claimName} claim for tile {discardLogical}.");
        }

        foreach (var tileId in matches)
        {
            claimantHand.Tiles.Remove(tileId);
        }

        return matches;
    }

    private static IReadOnlyList<int> RemoveChowTilesForClaim(
        TableGameState state,
        TableSeatHandState claimantHand,
        int discardLogical)
    {
        foreach (var (leftLogical, rightLogical) in GetChowPairCandidates(discardLogical))
        {
            if (TryTakeChowTilePair(claimantHand, leftLogical, rightLogical, out var tiles))
            {
                return tiles;
            }
        }

        ThrowInvariant(state, $"Seat {claimantHand.SeatIndex} cannot satisfy chow claim for tile {discardLogical}.");
        return [];
    }

    private static bool TryTakeChowTilePair(
        TableSeatHandState claimantHand,
        int leftLogical,
        int rightLogical,
        out IReadOnlyList<int> tiles)
    {
        var leftTileId = claimantHand.Tiles
            .Where(tileId => tileId / 4 == leftLogical)
            .OrderBy(tileId => tileId)
            .FirstOrDefault(-1);
        if (leftTileId < 0)
        {
            tiles = [];
            return false;
        }

        var rightTileId = claimantHand.Tiles
            .Where(tileId => tileId / 4 == rightLogical)
            .OrderBy(tileId => tileId)
            .FirstOrDefault(-1);
        if (rightTileId < 0)
        {
            tiles = [];
            return false;
        }

        claimantHand.Tiles.Remove(leftTileId);
        claimantHand.Tiles.Remove(rightTileId);
        tiles = leftTileId <= rightTileId
            ? [leftTileId, rightTileId]
            : [rightTileId, leftTileId];
        return true;
    }

    private static IEnumerable<(int LeftLogical, int RightLogical)> GetChowPairCandidates(int discardLogical)
    {
        var rank = discardLogical % 9;
        if (rank >= 2)
        {
            yield return (discardLogical - 2, discardLogical - 1);
        }

        if (rank >= 1 && rank <= 7)
        {
            yield return (discardLogical - 1, discardLogical + 1);
        }

        if (rank <= 6)
        {
            yield return (discardLogical + 1, discardLogical + 2);
        }
    }

    private static IReadOnlyList<int> ThrowUnsupportedHuClaim(TableGameState state)
    {
        ThrowRule(state, TableActionErrorCodes.ClaimSelectionUnavailable, "Hu claim resolution is not yet implemented.");
        return [];
    }

    private static IReadOnlyList<int> ThrowUnknownClaimType(TableGameState state, TableClaimType claimType)
    {
        ThrowInvariant(state, $"Unknown claim type '{claimType}'.");
        return [];
    }

    private static TableSeatMeldState GetOrCreateSeatMelds(TableGameState state, int seatIndex)
    {
        var seatMelds = state.ExposedMelds.SingleOrDefault(current => current.SeatIndex == seatIndex);
        if (seatMelds is not null)
        {
            return seatMelds;
        }

        var created = new TableSeatMeldState
        {
            SeatIndex = seatIndex
        };
        state.ExposedMelds.Add(created);
        return created;
    }

    private static void RemoveClaimedDiscard(TableGameState state, TableClaimWindowState claimWindow)
    {
        var discardIndex = state.DiscardPile.FindLastIndex(discard =>
            discard.SeatIndex == claimWindow.DiscardSeatIndex &&
            discard.TileId == claimWindow.DiscardTileId &&
            discard.TurnNumber == claimWindow.DiscardTurnNumber);

        if (discardIndex < 0)
        {
            ThrowInvariant(state, "Claimed discard is missing from discard pile.");
        }

        state.DiscardPile.RemoveAt(discardIndex);
    }

    private static bool PreparePostDiscardContinuation(TableGameState state, int discardSeatIndex)
    {
        state.Win = null;
        state.ActiveSeat = (discardSeatIndex + 1) % SeatCount;
        if (state.Wall.Count == 0)
        {
            state.Phase = TableTurnPhase.WallExhausted;
            return false;
        }

        state.Phase = TableTurnPhase.AwaitingDiscard;
        return true;
    }

    private static TableAction DrawForActiveSeat(TableGameState state)
    {
        var drawSeat = state.ActiveSeat;
        var drawTileId = DrawFromWall(state.Wall);
        var nextHand = GetHandForSeat(state, drawSeat);
        nextHand.Tiles.Add(drawTileId);
        state.DrawNumber++;

        return AppendAction(
            state,
            drawSeat,
            state.TurnNumber,
            "draw",
            drawTileId,
            $"tile-{drawTileId}");
    }

    private static TableSeatState GetSeat(TableGameState state, int seatIndex)
    {
        var seat = state.Seats.SingleOrDefault(currentSeat => currentSeat.SeatIndex == seatIndex);
        if (seat is null)
        {
            throw new TableRuleException(
                TableActionErrorCodes.SeatNotFound,
                $"Seat {seatIndex} does not exist.",
                state.StateVersion,
                state.ActionSequence);
        }

        return seat;
    }

    private static TableSeatHandState GetHandForSeat(TableGameState state, int seatIndex)
    {
        var hand = state.Hands.SingleOrDefault(currentHand => currentHand.SeatIndex == seatIndex);
        if (hand is null)
        {
            throw new TableRuleException(
                TableActionErrorCodes.StateInvariantBroken,
                $"Seat {seatIndex} hand is missing.",
                state.StateVersion,
                state.ActionSequence);
        }

        return hand;
    }

    private static List<int> CreateShuffledWall(int seed)
    {
        var wall = Enumerable.Range(0, TotalTiles).ToList();
        var random = new Random(seed);

        for (var index = wall.Count - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (wall[index], wall[swapIndex]) = (wall[swapIndex], wall[index]);
        }

        return wall;
    }

    private static int DrawFromWall(List<int> wall)
    {
        if (wall.Count == 0)
        {
            throw new InvalidOperationException("Cannot draw from an empty wall.");
        }

        var lastIndex = wall.Count - 1;
        var tileId = wall[lastIndex];
        wall.RemoveAt(lastIndex);
        return tileId;
    }

    private static int SelectBotDiscardTile(IReadOnlyList<int> tiles)
    {
        if (tiles.Count == 0)
        {
            throw new InvalidOperationException("Bot hand cannot be empty.");
        }

        var logicalCounts = tiles
            .GroupBy(tileId => tileId / 4)
            .ToDictionary(group => group.Key, group => group.Count());

        return tiles
            .OrderBy(tileId => ComputeBotTileKeepScore(tileId, logicalCounts))
            .ThenByDescending(tileId => tileId)
            .First();
    }

    private static int ComputeBotTileKeepScore(int tileId, IReadOnlyDictionary<int, int> logicalCounts)
    {
        var logicalTile = tileId / 4;
        var keepScore = 0;

        if (logicalCounts.TryGetValue(logicalTile, out var duplicateCount) && duplicateCount > 1)
        {
            keepScore += (duplicateCount - 1) * 6;
        }

        if (logicalTile >= 27)
        {
            return keepScore;
        }

        var rank = logicalTile % 9;
        if (rank > 0 && logicalCounts.ContainsKey(logicalTile - 1))
        {
            keepScore += 3;
        }

        if (rank < 8 && logicalCounts.ContainsKey(logicalTile + 1))
        {
            keepScore += 3;
        }

        if (rank > 1 && logicalCounts.ContainsKey(logicalTile - 2))
        {
            keepScore += 1;
        }

        if (rank < 7 && logicalCounts.ContainsKey(logicalTile + 2))
        {
            keepScore += 1;
        }

        return keepScore;
    }

    private static TableAction AppendAction(
        TableGameState state,
        int seatIndex,
        int turnNumber,
        string actionType,
        int? tileId,
        string detail)
    {
        var action = new TableAction
        {
            Sequence = ++state.ActionSequence,
            SeatIndex = seatIndex,
            TurnNumber = turnNumber,
            ActionType = actionType,
            TileId = tileId,
            Detail = detail,
            OccurredUtc = DateTime.UtcNow
        };

        state.ActionLog.Add(action);
        state.LastAction = ToLastAction(action);
        state.StateVersion++;
        return action;
    }

    private static TableLastActionState ToLastAction(TableAction action) =>
        new()
        {
            Sequence = action.Sequence,
            SeatIndex = action.SeatIndex,
            ActionType = action.ActionType,
            TileId = action.TileId,
            Detail = action.Detail
        };

    private static long GetMaxActionSequence(TableGameState state) =>
        state.ActionLog.Count == 0 ? 0 : state.ActionLog.Max(action => action.Sequence);

    private static void RefreshIntegrity(TableGameState state)
    {
        state.Integrity ??= new TableIntegrityState();
        state.Integrity.StateHash = TableStateHasher.Compute(state);
    }

    private static void ThrowInvariant(TableGameState state, string message) =>
        ThrowRule(state, TableActionErrorCodes.StateInvariantBroken, message);

    private static void ThrowRule(TableGameState state, string code, string message) =>
        throw new TableRuleException(code, message, state.StateVersion, state.ActionSequence);
}
