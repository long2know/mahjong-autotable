namespace Mahjong.Autotable.Api.Tables;

public static class TableActionErrorCodes
{
    public const string RoundNotActive = "ROUND_NOT_ACTIVE";
    public const string InvalidPhase = "INVALID_PHASE";
    public const string NotActiveSeat = "NOT_ACTIVE_SEAT";
    public const string SeatNotFound = "SEAT_NOT_FOUND";
    public const string TileNotInHand = "TILE_NOT_IN_HAND";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string StateInvariantBroken = "STATE_INVARIANT_BROKEN";
}
