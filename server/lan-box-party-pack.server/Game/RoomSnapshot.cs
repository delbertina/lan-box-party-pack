using lan_box_party_pack.server.Players;

namespace lan_box_party_pack.server.Game
{
    public class RoomSnapshot
    {
        public IReadOnlyCollection<Player> Players { get; set; } = [];

        public GameState Game { get; set; } = new();
    }
}
