namespace lan_box_party_pack.server.Players;

public enum PunishmentType
{
    Timeout,
    Ban
}

public sealed class PlayerPunishment
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string PlayerId { get; init; } = "";

    public string PlayerName { get; init; } = "";

    public string? IpAddress { get; init; }

    public PunishmentType Type { get; init; }

    public string Reason { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? RevokedAt { get; set; }
}
