namespace lan_box_party_pack.server.Players
{
    public class PlayerState
    {
        public string PlayerId { get; set; } = "";

        public string Name { get; set; } = "";

        public string Color { get; set; } = "";

        public int Score { get; set; }

        public DateTimeOffset? LockedUntil { get; set; }
    }
}
