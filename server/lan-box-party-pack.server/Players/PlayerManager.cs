using System.Collections.Concurrent;

namespace lan_box_party_pack.server.Players
{
    public class PlayerManager
    {
        private readonly ConcurrentDictionary<string, Player> _players = new();
        private readonly ConcurrentDictionary<string, string> _connections = new();
        private readonly ConcurrentDictionary<string, string> _playerIpAddresses = new();

        public IReadOnlyCollection<Player> Players =>
            _players.Values.ToList();

        public Player AddOrReconnectPlayer(string playerId, string name, string color, string connectionId, string? ipAddress)
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

            if (ipAddress is null)
            {
                _playerIpAddresses.TryRemove(playerId, out _);
            }
            else
            {
                _playerIpAddresses[playerId] = ipAddress;
            }

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

        public Player? UpdateProfile(string playerId, string name, string color)
        {
            if (!_players.TryGetValue(playerId, out var player))
                return null;

            player.Name = name;
            player.Color = color;
            return player;
        }

        public string? GetIpAddress(string playerId) =>
            _playerIpAddresses.GetValueOrDefault(playerId);

        public bool IsIpAddressUniqueToPlayer(string playerId, string ipAddress)
        {
            var matchingPlayers = _playerIpAddresses
                .Where(entry => entry.Value == ipAddress)
                .Select(entry => entry.Key)
                .ToArray();

            return matchingPlayers.Length == 1 && matchingPlayers[0] == playerId;
        }

        public IReadOnlyCollection<PlayerModerationInfo> ModerationPlayers =>
            _players.Values
                .Select(player => new PlayerModerationInfo
                {
                    Id = player.Id,
                    Name = player.Name,
                    IsConnected = player.IsConnected,
                    IpAddress = GetIpAddress(player.Id)
                })
                .ToArray();

        public IReadOnlyCollection<string> DisconnectPlayer(string playerId)
        {
            var connectionIds = _connections
                .Where(connection => connection.Value == playerId)
                .Select(connection => connection.Key)
                .ToArray();

            foreach (var connectionId in connectionIds)
            {
                _connections.TryRemove(connectionId, out _);
            }

            if (_players.TryGetValue(playerId, out var player))
            {
                player.IsConnected = false;
            }

            return connectionIds;
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
            _playerIpAddresses.TryRemove(playerId, out _);
        }
    }
}
