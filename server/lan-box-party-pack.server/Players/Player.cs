namespace lan_box_party_pack.server.Players
{
    public class Player
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string Name { get; set; } = "";

        public string Color { get; set; } = "";

        public bool IsConnected { get; set; } = true;
    }
}
