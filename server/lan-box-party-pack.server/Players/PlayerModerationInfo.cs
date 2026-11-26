namespace lan_box_party_pack.server.Players;

public sealed class PlayerModerationInfo
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public bool IsConnected { get; init; }

    public string? IpAddress { get; init; }
}
