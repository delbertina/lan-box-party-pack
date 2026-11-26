namespace lan_box_party_pack.server.Players;

public sealed class ModerationSnapshot
{
    public IReadOnlyCollection<PlayerModerationInfo> Players { get; init; } = [];

    public IReadOnlyCollection<PlayerPunishment> Punishments { get; init; } = [];
}
