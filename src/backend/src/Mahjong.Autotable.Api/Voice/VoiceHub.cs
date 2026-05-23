using Mahjong.Autotable.Api.Data;
using Mahjong.Autotable.Api.Data.Entities;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace Mahjong.Autotable.Api.Voice;

// Phase K Wave 2 — Bishop (backend). SignalR signalling hub for WebRTC
// peer-mesh voice chat. The hub never relays media; it only carries the
// SDP offer/answer + ICE candidate handshake messages and announces peer
// join/leave on a per-table SignalR group. The actual RTP/RTCP flows
// browser-to-browser through STUN/TURN. Mesh topology only — max 4
// peers per table per VoiceOptions.MaxPeersPerTable.
//
// Auth: not enforced at hub level (mesh signalling). Future Wave 3 may
// wrap per-table membership against the AuthCookieService — for Wave 2
// the production code keeps the hub open and tracks who joined via the
// audit table.
public sealed class VoiceHub : Hub
{
    public const int MeshPeerCeiling = 4;
    public const int SignallingRatePerSecond = 30;

    private readonly VoiceOptions _options;
    private readonly VoiceRateLimiter _rateLimiter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VoiceHub> _logger;

    public VoiceHub(
        IOptions<VoiceOptions> options,
        VoiceRateLimiter rateLimiter,
        IServiceScopeFactory scopeFactory,
        ILogger<VoiceHub> logger)
    {
        _options = options.Value;
        _rateLimiter = rateLimiter;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task JoinVoice(string tableId)
    {
        if (string.IsNullOrWhiteSpace(tableId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tableId));
        await Clients.OthersInGroup(GroupName(tableId)).SendAsync("PeerJoined", Context.ConnectionId);
        await AuditAsync(tableId, ReconnectAuditEntry.KindVoiceJoin);
    }

    public async Task LeaveVoice(string tableId)
    {
        if (string.IsNullOrWhiteSpace(tableId)) return;
        await Clients.OthersInGroup(GroupName(tableId)).SendAsync("PeerLeft", Context.ConnectionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tableId));
        await AuditAsync(tableId, ReconnectAuditEntry.KindVoiceLeave);
    }

    public async Task RelayOffer(string targetConnectionId, string sdp)
    {
        if (!Throttle()) return;
        if (string.IsNullOrWhiteSpace(targetConnectionId)) return;
        await Clients.Client(targetConnectionId).SendAsync("OfferReceived", Context.ConnectionId, sdp);
    }

    public async Task RelayAnswer(string targetConnectionId, string sdp)
    {
        if (!Throttle()) return;
        if (string.IsNullOrWhiteSpace(targetConnectionId)) return;
        await Clients.Client(targetConnectionId).SendAsync("AnswerReceived", Context.ConnectionId, sdp);
    }

    public async Task RelayIceCandidate(string targetConnectionId, string candidate)
    {
        if (!Throttle()) return;
        if (string.IsNullOrWhiteSpace(targetConnectionId)) return;
        await Clients.Client(targetConnectionId).SendAsync("IceCandidateReceived", Context.ConnectionId, candidate);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _rateLimiter.Forget(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    private bool Throttle() => _rateLimiter.TryConsume(Context.ConnectionId);

    private static string GroupName(string tableId) => $"voice:{tableId}";

    private async Task AuditAsync(string tableId, string kind)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ReconnectAuditEntries.Add(new ReconnectAuditEntry
            {
                Id = Guid.NewGuid(),
                PlayerId = Context.ConnectionId,
                At = DateTime.UtcNow,
                Kind = kind,
                Detail = tableId,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "VoiceHub audit write failed for {Kind}", kind);
        }
    }
}
