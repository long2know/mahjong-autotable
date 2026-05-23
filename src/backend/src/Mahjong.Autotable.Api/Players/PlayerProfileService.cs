using System.Text.RegularExpressions;
using Mahjong.Autotable.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Mahjong.Autotable.Api.Players;

/// <summary>
/// Phase J Wave 5 — service for reading/writing <see cref="PlayerProfile"/>
/// and <see cref="PlayerStats"/>. Singleton-scoped because it is consumed by
/// other singletons (the runtime and the hub); it uses an
/// <see cref="IServiceScopeFactory"/> to open a fresh scoped <see cref="AppDbContext"/>
/// per call. Every DB write is wrapped in a single <c>SaveChangesAsync</c>
/// so callers see all-or-nothing semantics.
/// </summary>
public sealed class PlayerProfileService
{
    private static readonly Regex AvatarColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlayerProfileService> _logger;

    public PlayerProfileService(IServiceScopeFactory scopeFactory, ILogger<PlayerProfileService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Returns a stable default display name keyed off the player id. We
    /// hash the id (FNV-1a) and select from a 16-entry palette so the same
    /// player always sees the same "Player-XYZ" assignment until they pick
    /// their own name. Keeps the lobby readable when nobody has customised
    /// their profile yet.
    /// </summary>
    public static string DefaultDisplayName(string playerId)
    {
        if (string.IsNullOrEmpty(playerId)) return "Player";
        var hash = Fnv1aHash(playerId);
        return $"Player-{(hash & 0xFFFFFF):X6}";
    }

    /// <summary>
    /// Returns a stable default avatar colour keyed off the player id.
    /// Hashed pick from a 16-entry palette so the colour is deterministic
    /// (a reconnect with the same id reproduces the same chip colour).
    /// </summary>
    public static string DefaultAvatarColor(string playerId)
    {
        // Saturated, dark-text-friendly hex palette (HSL spaced ≈22°).
        string[] palette =
        {
            "#E53935", "#D81B60", "#8E24AA", "#5E35B1",
            "#3949AB", "#1E88E5", "#039BE5", "#00ACC1",
            "#00897B", "#43A047", "#7CB342", "#C0CA33",
            "#FDD835", "#FFB300", "#FB8C00", "#F4511E",
        };
        if (string.IsNullOrEmpty(playerId)) return palette[0];
        var hash = Fnv1aHash(playerId);
        return palette[hash % (uint)palette.Length];
    }

    private static uint Fnv1aHash(string s)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            foreach (var c in s)
            {
                hash ^= c;
                hash *= prime;
            }
            return hash;
        }
    }

    /// <summary>
    /// Returns the existing <see cref="PlayerProfile"/> for <paramref name="playerId"/>
    /// or creates one (with default display name + avatar colour) on first
    /// access. Always touches <c>LastSeenAt</c> so the lobby UI can show
    /// "recently online" indicators in a future wave.
    /// </summary>
    public async Task<PlayerProfile> GetOrCreateAsync(string playerId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(playerId)) throw new ArgumentException("playerId required", nameof(playerId));

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.PlayerProfiles.FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
        if (profile is null)
        {
            profile = new PlayerProfile
            {
                PlayerId = playerId,
                DisplayName = DefaultDisplayName(playerId),
                AvatarColor = DefaultAvatarColor(playerId),
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            };
            db.PlayerProfiles.Add(profile);

            // Pair the profile with an empty stats row so subsequent
            // RecordGameCompleted calls don't need a "create on first game"
            // branch — keeps the hot-path single-query.
            db.PlayerStats.Add(new PlayerStats { PlayerId = playerId });
        }
        else
        {
            profile.LastSeenAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return profile;
    }

    /// <summary>
    /// Returns the <see cref="PlayerStats"/> row for the player, creating a
    /// fresh zeroed one if the player has never played (defensive — should
    /// only happen if the profile was created out-of-band).
    /// </summary>
    public async Task<PlayerStats> GetStatsAsync(string playerId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(playerId)) throw new ArgumentException("playerId required", nameof(playerId));

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stats = await db.PlayerStats.FirstOrDefaultAsync(s => s.PlayerId == playerId, ct);
        if (stats is null)
        {
            stats = new PlayerStats { PlayerId = playerId };
            db.PlayerStats.Add(stats);
            await db.SaveChangesAsync(ct);
        }
        return stats;
    }

    /// <summary>
    /// Updates <see cref="PlayerProfile.DisplayName"/> after trimming and
    /// length-validating (1–32 chars, no leading/trailing whitespace).
    /// Throws <see cref="ArgumentException"/> on invalid input — callers
    /// at the hub layer translate this to a <c>HubException</c>.
    /// </summary>
    public async Task<PlayerProfile> UpdateDisplayNameAsync(string playerId, string displayName, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(playerId)) throw new ArgumentException("playerId required", nameof(playerId));
        if (displayName is null) throw new ArgumentException("displayName required", nameof(displayName));
        var trimmed = displayName.Trim();
        if (trimmed.Length is < 1 or > 32)
        {
            throw new ArgumentException("displayName must be 1–32 characters after trimming.", nameof(displayName));
        }
        if (trimmed != displayName)
        {
            // Reject leading/trailing whitespace explicitly so the rejection
            // is visible — the actual stored value is `trimmed`.
            throw new ArgumentException("displayName must not have leading or trailing whitespace.", nameof(displayName));
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.PlayerProfiles.FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
        if (profile is null)
        {
            profile = new PlayerProfile
            {
                PlayerId = playerId,
                DisplayName = trimmed,
                AvatarColor = DefaultAvatarColor(playerId),
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            };
            db.PlayerProfiles.Add(profile);
            db.PlayerStats.Add(new PlayerStats { PlayerId = playerId });
        }
        else
        {
            profile.DisplayName = trimmed;
            profile.LastSeenAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return profile;
    }

    /// <summary>
    /// Updates <see cref="PlayerProfile.AvatarColor"/>. Accepts only
    /// <c>#RRGGBB</c> (lower or upper case). Throws
    /// <see cref="ArgumentException"/> for any other shape.
    /// </summary>
    public async Task<PlayerProfile> UpdateAvatarColorAsync(string playerId, string avatarColor, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(playerId)) throw new ArgumentException("playerId required", nameof(playerId));
        if (string.IsNullOrEmpty(avatarColor) || !AvatarColorPattern.IsMatch(avatarColor))
        {
            throw new ArgumentException("avatarColor must be a #RRGGBB hex string.", nameof(avatarColor));
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.PlayerProfiles.FirstOrDefaultAsync(p => p.PlayerId == playerId, ct);
        if (profile is null)
        {
            profile = new PlayerProfile
            {
                PlayerId = playerId,
                DisplayName = DefaultDisplayName(playerId),
                AvatarColor = avatarColor,
                CreatedAt = DateTime.UtcNow,
                LastSeenAt = DateTime.UtcNow,
            };
            db.PlayerProfiles.Add(profile);
            db.PlayerStats.Add(new PlayerStats { PlayerId = playerId });
        }
        else
        {
            profile.AvatarColor = avatarColor;
            profile.LastSeenAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return profile;
    }

    /// <summary>
    /// Records the completion of a game. <paramref name="finalScores"/> is
    /// keyed by <c>PlayerId</c> (per-seat sums already collapsed by the
    /// caller); <paramref name="winners"/> is the set of <c>PlayerId</c>s
    /// tied at the top score. PlayerIds prefixed with <c>bot-</c> are
    /// filtered (bots have no profile). All updates happen in a single
    /// <c>SaveChangesAsync</c>; DB exceptions are logged and swallowed so a
    /// stats failure can never break game completion.
    /// </summary>
    public async Task RecordGameCompletedAsync(
        IReadOnlyDictionary<string, int> finalScores,
        IReadOnlySet<string> winners,
        CancellationToken ct = default)
    {
        if (finalScores is null || finalScores.Count == 0) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            foreach (var (playerId, score) in finalScores)
            {
                if (string.IsNullOrEmpty(playerId)) continue;
                if (playerId.StartsWith("bot-", StringComparison.Ordinal)) continue;

                var stats = await db.PlayerStats.FirstOrDefaultAsync(s => s.PlayerId == playerId, ct);
                if (stats is null)
                {
                    stats = new PlayerStats { PlayerId = playerId };
                    db.PlayerStats.Add(stats);

                    // Guarantee a profile row exists too so the stats FK
                    // resolves on cascade-aware providers.
                    var profileExists = await db.PlayerProfiles.AnyAsync(p => p.PlayerId == playerId, ct);
                    if (!profileExists)
                    {
                        db.PlayerProfiles.Add(new PlayerProfile
                        {
                            PlayerId = playerId,
                            DisplayName = DefaultDisplayName(playerId),
                            AvatarColor = DefaultAvatarColor(playerId),
                            CreatedAt = now,
                            LastSeenAt = now,
                        });
                    }
                }

                stats.GamesPlayed += 1;
                stats.TotalScore += score;
                if (score > stats.HighestSingleGameScore) stats.HighestSingleGameScore = score;

                if (winners.Contains(playerId))
                {
                    stats.GamesWon += 1;
                    stats.CurrentWinStreak += 1;
                    if (stats.CurrentWinStreak > stats.LongestWinStreak)
                        stats.LongestWinStreak = stats.CurrentWinStreak;
                }
                else
                {
                    stats.CurrentWinStreak = 0;
                }
                stats.LastGameAt = now;
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recording game-completed stats failed; counters may be stale.");
        }
    }
}
