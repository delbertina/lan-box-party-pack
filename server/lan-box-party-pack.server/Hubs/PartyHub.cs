using lan_box_party_pack.server.Game;
using lan_box_party_pack.server.Players;
using Microsoft.AspNetCore.SignalR;

namespace lan_box_party_pack.server.Hubs
{
    public class PartyHub : Hub
    {
        private const string RoomGroup = "main-room";

        private readonly PlayerManager _playerManager;
        private readonly GameManager _gameManager;
        private readonly ILogger<PartyHub> _logger;

        public PartyHub(
            PlayerManager playerManager,
            GameManager gameManager,
            ILogger<PartyHub> logger)
        {
            _playerManager = playerManager;
            _gameManager = gameManager;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();
            var remoteIp = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString() ?? "unknown";

            _logger.LogInformation(
                "SignalR connection established. ConnectionId: {ConnectionId}, RemoteIp: {RemoteIp}, UserAgent: {UserAgent}",
                Context.ConnectionId,
                remoteIp,
                userAgent);

            await base.OnConnectedAsync();
        }

        public async Task WatchRoom()
        {
            _logger.LogInformation(
                "WatchRoom requested. ConnectionId: {ConnectionId}, Room: {Room}",
                Context.ConnectionId,
                RoomGroup);

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RoomGroup
            );

            await SendRoomSnapshotToCaller();
        }

        public async Task JoinRoom(string playerId, string playerName, string color)
        {
            _logger.LogInformation(
                "JoinRoom requested. ConnectionId: {ConnectionId}, PlayerId: {PlayerId}, PlayerName: {PlayerName}, Color: {Color}, Room: {Room}",
                Context.ConnectionId,
                playerId,
                playerName,
                color,
                RoomGroup);

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RoomGroup
            );

            _playerManager.AddOrReconnectPlayer(
                playerId,
                playerName,
                color,
                Context.ConnectionId
            );

            await BroadcastRoomSnapshot();

            _logger.LogInformation(
                "JoinRoom completed. ConnectionId: {ConnectionId}, PlayerId: {PlayerId}",
                Context.ConnectionId,
                playerId);
        }

        private async Task SendRoomSnapshotToCaller()
        {
            var snapshot = CreateRoomSnapshot();

            await Clients.Caller.SendAsync("RoomUpdated", snapshot);
        }

        private async Task BroadcastRoomSnapshot()
        {
            var snapshot = CreateRoomSnapshot();

            await Clients.Group(RoomGroup)
                .SendAsync("RoomUpdated", snapshot);
        }

        private RoomSnapshot CreateRoomSnapshot()
        {
            return new RoomSnapshot
            {
                Players = _playerManager.Players,
                Game = _gameManager.State
            };
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            if (exception is null)
            {
                _logger.LogInformation(
                    "SignalR connection disconnected. ConnectionId: {ConnectionId}",
                    Context.ConnectionId);
            }
            else
            {
                _logger.LogWarning(
                    exception,
                    "SignalR connection disconnected with an error. ConnectionId: {ConnectionId}",
                    Context.ConnectionId);
            }

            _playerManager.MarkDisconnected(Context.ConnectionId);

            await BroadcastRoomSnapshot();

            await base.OnDisconnectedAsync(exception);
        }

        public async Task StartNewGame()
        {
            _logger.LogInformation(
                "StartNewGame requested. ConnectionId: {ConnectionId}",
                Context.ConnectionId);

            _gameManager.StartNewGame(_playerManager.Players);

            await BroadcastRoomSnapshot();
        }
    }
}
