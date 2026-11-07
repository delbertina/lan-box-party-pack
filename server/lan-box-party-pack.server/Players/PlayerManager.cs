using System.Collections.Concurrent;

namespace lan_box_party_pack.server.Players
{
    public class PlayerManager
    {
        private readonly ConcurrentDictionary<string, Player> _players = new();
        private readonly ConcurrentDictionary<string, string> _connections = new();

        public IReadOnlyCollection<Player> Players =>
            _players.Values.ToList();

        public Player AddOrReconnectPlayer(string playerId, string name, string color, string connectionId)
        {
            var player = _players.GetOrAdd(playerId, id => new Player
            {
                Id = id
            });

            // Remove any previous connection associated with this player.
            // This prevents an old connection's disconnect event from
            // marking the newly reconnected player as disconnected.
            foreach (var connection in _connections)
            {
                if (connection.Value == playerId)
                {
                    _connections.TryRemove(connection.Key, out _);
                }
            }

            player.Name = name;
            player.Color = color;
            player.IsConnected = true;

            _connections[connectionId] = playerId;

            return player;
        }

        public Player? GetPlayer(string playerId)
        {
            return _players.GetValueOrDefault(playerId);
        }

        public Player? GetPlayerByConnection(string connectionId)
        {
            if (!_connections.TryGetValue(connectionId, out var playerId))
                return null;

            return GetPlayer(playerId);
        }

        public void MarkDisconnected(string connectionId)
        {
            if (!_connections.TryRemove(connectionId, out var playerId))
                return;

            if (_players.TryGetValue(playerId, out var player))
            {
                player.IsConnected = false;
            }
        }

        public void RemovePlayer(string playerId)
        {
            _players.TryRemove(playerId, out _);
        }
    }
}
