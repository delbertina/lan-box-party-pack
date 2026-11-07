namespace lan_box_party_pack.server.Players
{
    public class PlayerManager
    {
        private readonly Dictionary<string, Player> _players = new();

        public IReadOnlyCollection<Player> Players => _players.Values.ToList();

        public Player AddPlayer(string name, string color)
        {
            var player = new Player
            {
                Name = name,
                Color = color
            };

            _players.Add(player.Id, player);

            return player;
        }

        public Player? GetPlayer(string playerId)
        {
            return _players.GetValueOrDefault(playerId);
        }

        public void RemovePlayer(string playerId)
        {
            _players.Remove(playerId);
        }
    }
}
