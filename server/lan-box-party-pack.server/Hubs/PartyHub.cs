using lan_box_party_pack.server.Game;
using lan_box_party_pack.server.Players;
using lan_box_party_pack.server.Questions;
using Microsoft.AspNetCore.SignalR;
using SignalRSwaggerGen.Attributes;
using SignalRSwaggerGen.Enums;

namespace lan_box_party_pack.server.Hubs
{
    [SignalRHub(path: "/partyHub", autoDiscover: AutoDiscover.MethodsAndParams)]
    public class PartyHub : Hub<IPartyClient>
    {
        private const string RoomGroup = "main-room";

        private readonly PlayerManager _playerManager;
        private readonly PlayerPunishmentManager _punishmentManager;
        private readonly GameManager _gameManager;
        private readonly QuestionRepository _questionRepository;
        private readonly ILogger<PartyHub> _logger;

        public PartyHub(PlayerManager playerManager, PlayerPunishmentManager punishmentManager, GameManager gameManager, QuestionRepository questionRepository, ILogger<PartyHub> logger)
        {
            _playerManager = playerManager;
            _punishmentManager = punishmentManager;
            _gameManager = gameManager;
            _questionRepository = questionRepository;
            _logger = logger;
        }

        [SignalRHidden]
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

        [SignalRMethod(summary: "Subscribe to room and host-screen updates and receive the current room snapshot.")]
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

            await Groups.AddToGroupAsync(Context.ConnectionId, "host-screen");

            await SendRoomSnapshotToCaller();
        }

        [SignalRMethod(summary: "Join or reconnect a player and subscribe to room updates.")]
        public async Task JoinRoom(string playerId, string playerName, string color)
        {
            var remoteIp = GetRemoteIpAddress();

            _logger.LogInformation(
                "JoinRoom requested. ConnectionId: {ConnectionId}, PlayerId: {PlayerId}, PlayerName: {PlayerName}, Color: {Color}, RemoteIp: {RemoteIp}, Room: {Room}",
                Context.ConnectionId,
                playerId,
                playerName,
                color,
                remoteIp ?? "unknown",
                RoomGroup);

            var activePunishment = _punishmentManager.FindActive(playerId, remoteIp);
            if (activePunishment is not null)
            {
                var punishmentDescription = activePunishment.Type == PunishmentType.Ban
                    ? "You are banned from this game."
                    : $"You are timed out until {activePunishment.ExpiresAt:O}.";
                var reasonDescription = string.IsNullOrWhiteSpace(activePunishment.Reason)
                    ? ""
                    : $" Reason: {activePunishment.Reason}";

                _logger.LogWarning(
                    "Blocked join for PlayerId: {PlayerId}, RemoteIp: {RemoteIp}, PunishmentId: {PunishmentId}",
                    playerId,
                    remoteIp ?? "unknown",
                    activePunishment.Id);

                throw new HubException(punishmentDescription + reasonDescription);
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                RoomGroup
            );

            _playerManager.AddOrReconnectPlayer(
                playerId,
                playerName,
                color,
                Context.ConnectionId,
                remoteIp
            );

            await BroadcastRoomSnapshot();

            _logger.LogInformation(
                "JoinRoom completed. ConnectionId: {ConnectionId}, PlayerId: {PlayerId}",
                Context.ConnectionId,
                playerId);
        }

        [SignalRMethod(summary: "Update the calling player's name and color.")]
        public async Task UpdateMyProfile(string playerName, string color)
        {
            var player = _playerManager.GetPlayerByConnection(Context.ConnectionId);
            if (player is null)
            {
                throw new InvalidOperationException(
                    "You must join the room before updating your profile."
                );
            }

            if (string.IsNullOrWhiteSpace(playerName))
            {
                throw new HubException("Player name cannot be empty.");
            }

            var normalizedName = playerName.Trim();
            if (player.NameLocked && !string.Equals(player.Name, normalizedName, StringComparison.Ordinal))
            {
                throw new HubException("Your name has been locked by the host.");
            }

            UpdatePlayerProfileCore(player.Id, normalizedName, color);
            await BroadcastRoomSnapshot();
        }

        [SignalRMethod(summary: "Update a player's name and color from the host screen.")]
        public async Task UpdatePlayerProfile(string playerId, string playerName, string color)
        {
            UpdatePlayerProfileCore(playerId, playerName, color);
            await BroadcastRoomSnapshot();
        }

        [SignalRMethod(summary: "Lock or unlock a player's name from the host screen.")]
        public async Task SetPlayerNameLock(string playerId, bool isLocked)
        {
            var player = _playerManager.SetNameLock(playerId, isLocked);
            if (player is null)
            {
                throw new InvalidOperationException("Player was not found.");
            }

            await BroadcastRoomSnapshot();
        }

        private void UpdatePlayerProfileCore(string playerId, string playerName, string color)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                throw new InvalidOperationException("Player name cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(color))
            {
                throw new InvalidOperationException("Player color cannot be empty.");
            }

            var player = _playerManager.UpdateProfile(
                playerId,
                playerName.Trim(),
                color
            );

            if (player is null)
            {
                throw new InvalidOperationException("Player was not found.");
            }
        }

        [SignalRMethod(summary: "Adjust a player's score by a signed point amount; scores cannot go below zero.")]
        public async Task AdjustPlayerScore(string playerId, int delta)
        {
            if (delta == 0 || delta is < -100000 or > 100000)
            {
                throw new InvalidOperationException("Score adjustment must be between -100000 and 100000 and cannot be zero.");
            }

            GetPlayerForPunishment(playerId);
            _gameManager.AdjustPlayerScore(playerId, delta);
            await BroadcastRoomSnapshot();
        }

        [SignalRMethod(summary: "Reset a player's score to zero.")]
        public async Task ResetPlayerScore(string playerId)
        {
            GetPlayerForPunishment(playerId);
            _gameManager.ResetPlayerScore(playerId);
            await BroadcastRoomSnapshot();
        }

        [SignalRMethod(summary: "Get players, their observed IP addresses, and in-memory punishments.")]
        public ModerationSnapshot GetModerationState()
        {
            return new ModerationSnapshot
            {
                Players = _playerManager.ModerationPlayers,
                Punishments = _punishmentManager.Punishments
            };
        }

        [SignalRMethod(summary: "Disconnect a player without preventing them from rejoining.")]
        public async Task KickPlayer(string playerId, string reason)
        {
            var player = GetPlayerForPunishment(playerId);
            await DisconnectPlayer(player, FormatModerationMessage("You were kicked from the game.", reason));
        }

        [SignalRMethod(summary: "Disconnect a player and prevent rejoining for a number of minutes.")]
        public async Task TimeoutPlayer(string playerId, int durationMinutes, string reason)
        {
            if (durationMinutes is < 1 or > 10080)
            {
                throw new InvalidOperationException("Timeout duration must be between 1 and 10080 minutes.");
            }

            var player = GetPlayerForPunishment(playerId);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(durationMinutes);
            var ipAddress = GetUniquePlayerIpAddress(playerId);
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "" : reason.Trim();
            _punishmentManager.Add(
                player.Id,
                player.Name,
                ipAddress,
                PunishmentType.Timeout,
                normalizedReason,
                expiresAt
            );

            await DisconnectPlayer(
                player,
                FormatModerationMessage($"You were timed out until {expiresAt:O}.", reason)
            );
        }

        [SignalRMethod(summary: "Permanently ban a player until the punishment is revoked or the server restarts.")]
        public async Task BanPlayer(string playerId, string reason)
        {
            var player = GetPlayerForPunishment(playerId);
            var ipAddress = GetUniquePlayerIpAddress(playerId);
            var normalizedReason = string.IsNullOrWhiteSpace(reason) ? "" : reason.Trim();
            _punishmentManager.Add(
                player.Id,
                player.Name,
                ipAddress,
                PunishmentType.Ban,
                normalizedReason,
                null
            );

            await DisconnectPlayer(
                player,
                FormatModerationMessage("You were banned from the game.", reason)
            );
        }

        [SignalRMethod(summary: "Revoke a timeout or ban by punishment ID.")]
        public async Task RevokePunishment(string punishmentId)
        {
            if (!Guid.TryParse(punishmentId, out var id) || !_punishmentManager.Revoke(id))
            {
                throw new InvalidOperationException("Active punishment was not found.");
            }

            await Clients.Group(RoomGroup).RoomUpdated(CreateRoomSnapshot());
        }

        private Player GetPlayerForPunishment(string playerId)
        {
            return _playerManager.GetPlayer(playerId)
                ?? throw new InvalidOperationException("Player was not found.");
        }

        private string? GetUniquePlayerIpAddress(string playerId)
        {
            var ipAddress = _playerManager.GetIpAddress(playerId);
            return ipAddress is not null &&
                _playerManager.IsIpAddressUniqueToPlayer(playerId, ipAddress)
                    ? ipAddress
                    : null;
        }

        private async Task DisconnectPlayer(Player player, string reason)
        {
            var connectionIds = _playerManager.DisconnectPlayer(player.Id);
            foreach (var connectionId in connectionIds)
            {
                await Groups.RemoveFromGroupAsync(connectionId, RoomGroup);
                await Groups.RemoveFromGroupAsync(connectionId, "host-screen");
                await Clients.Client(connectionId).PlayerDisconnected(reason);
            }

            _logger.LogInformation(
                "Disconnected PlayerId: {PlayerId}, PlayerName: {PlayerName}, ConnectionCount: {ConnectionCount}",
                player.Id,
                player.Name,
                connectionIds.Count);

            await BroadcastRoomSnapshot();
        }

        private static string FormatModerationMessage(string message, string reason)
        {
            return string.IsNullOrWhiteSpace(reason)
                ? message
                : $"{message} Reason: {reason.Trim()}";
        }

        private string? GetRemoteIpAddress()
        {
            var remoteIp = Context.GetHttpContext()?.Connection.RemoteIpAddress;
            if (remoteIp is null)
            {
                return null;
            }

            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            return remoteIp.ToString();
        }

        private async Task SendRoomSnapshotToCaller()
        {
            var snapshot = CreateRoomSnapshot();

            await Clients.Caller.RoomUpdated(snapshot);
        }

        private async Task BroadcastRoomSnapshot()
        {
            var snapshot = CreateRoomSnapshot();

            await Clients.Group(RoomGroup)
                .RoomUpdated(snapshot);
        }

        private RoomSnapshot CreateRoomSnapshot()
        {
            return new RoomSnapshot
            {
                Players = _playerManager.Players,
                Game = _gameManager.State
            };
        }

        [SignalRHidden]
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

        [SignalRMethod(summary: "Start a new game from a question-set JSON filename.")]
        public async Task StartNewGame(string fileName)
        {
            var questionSet = _questionRepository.Load(fileName);

            _gameManager.StartNewGame(
                _playerManager.Players,
                questionSet
            );

            await BroadcastRoomSnapshot();
        }

        [SignalRMethod(summary: "Start the current question.")]
        public async Task StartQuestion()
        {
            _gameManager.StartQuestion();

            await BroadcastRoomSnapshot();
        }

        [SignalRMethod(summary: "Reveal the current question's answer.")]
        public async Task RevealAnswer()
        {
            _gameManager.RevealAnswer();

            await BroadcastRoomSnapshot();
        }

        [SignalRMethod(summary: "Advance to the next question.")]
        public async Task AdvanceQuestion()
        {
            _gameManager.AdvanceQuestion();

            await BroadcastRoomSnapshot();
        }

        [SignalRMethod(summary: "Submit an answer option for the connected player; the player must have joined the room.")]
        public async Task SubmitAnswer(int answerOptionId)
        {
            var player = _playerManager.GetPlayerByConnection(
                Context.ConnectionId
            );

            if (player == null)
            {
                throw new InvalidOperationException(
                    "You must join the room before submitting an answer."
                );
            }

            var submission = _gameManager.SubmitAnswer(player, answerOptionId);

            var answerOption = _questionRepository
                .GetAnswerOptions()
                .FirstOrDefault(option => option.Id == answerOptionId);

            submission.Answer = answerOption?.DisplayName ?? $"Unknown option ({answerOptionId})";

            _logger.LogInformation(
                "Answer from {PlayerName}: Option {AnswerOptionId}, Video timestamp: {VideoTimestampSeconds:F2}s",
                submission.PlayerName,
                submission.AnswerOptionId,
                submission.VideoTimestampSeconds
            );

            await Clients.Caller.AnswerResult(submission);

            await Clients.Group("host-screen")
                .PlayerAnswerSubmitted(submission);
        }

        [SignalRMethod(summary: "Get the available question-set summaries.")]
        public Task<List<QuestionSetSummary>> GetQuestionSets()
        {
            return Task.FromResult(
                _questionRepository.GetAvailableQuestionSets()
            );
        }

        [SignalRMethod(summary: "End playback at the supplied video timestamp.")]
        public async Task EndPlayback(double videoTimestampSeconds)
        {
            _gameManager.EndPlayback(videoTimestampSeconds);

            await BroadcastRoomSnapshot();
        }

        [SignalRMethod(summary: "Update playback timestamp and playing state.")]
        public async Task UpdatePlayback(double videoTimestampSeconds, bool isPlaying)
        {
            var playbackStateChanged = _gameManager.UpdatePlayback(
                videoTimestampSeconds,
                isPlaying
            );

            if (playbackStateChanged)
            {
                await BroadcastRoomSnapshot();
            }
        }

        [SignalRMethod(summary: "Get all configured answer options.")]
        public List<AnswerOption> GetAnswerOptions()
        {
            return _questionRepository.GetAnswerOptions();
        }
    }
}
