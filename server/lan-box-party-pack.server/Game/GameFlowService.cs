using lan_box_party_pack.server.Hubs;
using lan_box_party_pack.server.Players;
using Microsoft.AspNetCore.SignalR;

namespace lan_box_party_pack.server.Game;

public class GameFlowService : BackgroundService
{
    private readonly GameManager _gameManager;
    private readonly PlayerManager _playerManager;
    private readonly IHubContext<PartyHub> _hubContext;
    private readonly ILogger<GameFlowService> _logger;

    public GameFlowService(
        GameManager gameManager,
        PlayerManager playerManager,
        IHubContext<PartyHub> hubContext,
        ILogger<GameFlowService> logger)
    {
        _gameManager = gameManager;
        _playerManager = playerManager;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromMilliseconds(100)
        );

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var changed = _gameManager.ProcessExpiredPhase();

                if (!changed)
                    continue;

                var snapshot = new RoomSnapshot
                {
                    Players = _playerManager.Players,
                    Game = _gameManager.State
                };

                await _hubContext.Clients
                    .Group("main-room")
                    .SendAsync("RoomUpdated", snapshot, stoppingToken);

                _logger.LogInformation(
                    "Automatic game phase transition: {Phase}",
                    _gameManager.State.Phase
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error processing automatic game flow."
                );
            }
        }
    }
}