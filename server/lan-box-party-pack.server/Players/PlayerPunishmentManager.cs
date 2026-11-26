namespace lan_box_party_pack.server.Players;

public sealed class PlayerPunishmentManager
{
    private readonly object _lock = new();
    private readonly List<PlayerPunishment> _punishments = [];

    public IReadOnlyCollection<PlayerPunishment> Punishments
    {
        get
        {
            lock (_lock)
            {
                return _punishments.ToArray();
            }
        }
    }

    public PlayerPunishment Add(
        string playerId,
        string playerName,
        string? ipAddress,
        PunishmentType type,
        string reason,
        DateTimeOffset? expiresAt)
    {
        var punishment = new PlayerPunishment
        {
            PlayerId = playerId,
            PlayerName = playerName,
            IpAddress = ipAddress,
            Type = type,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };

        lock (_lock)
        {
            _punishments.Add(punishment);
        }

        return punishment;
    }

    public PlayerPunishment? FindActive(string playerId, string? ipAddress)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            return _punishments
                .Where(punishment =>
                    punishment.RevokedAt is null &&
                    (punishment.ExpiresAt is null || punishment.ExpiresAt > now) &&
                    (punishment.PlayerId == playerId ||
                     (ipAddress is not null && punishment.IpAddress == ipAddress)))
                .OrderByDescending(punishment => punishment.Type == PunishmentType.Ban)
                .ThenByDescending(punishment => punishment.CreatedAt)
                .FirstOrDefault();
        }
    }

    public bool Revoke(Guid punishmentId)
    {
        lock (_lock)
        {
            var punishment = _punishments.FirstOrDefault(item => item.Id == punishmentId);
            if (punishment is null || punishment.RevokedAt is not null)
            {
                return false;
            }

            punishment.RevokedAt = DateTimeOffset.UtcNow;
            return true;
        }
    }
}
