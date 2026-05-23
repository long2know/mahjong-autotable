namespace Mahjong.Autotable.Api.Players;

/// <summary>
/// Phase J Wave 5 — persistent per-player identity. The PK <see cref="PlayerId"/>
/// is the SignalR connection id used by the player at registration time (v1
/// uses ConnectionId as session identity — see Bishop's Wave 5 memo for the
/// reconnect limitation). DisplayName and AvatarColor are user-editable via
/// the <c>UpdateProfile</c> hub RPC.
/// </summary>
public sealed class PlayerProfile
{
    public string PlayerId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string AvatarColor { get; set; } = "#808080";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
