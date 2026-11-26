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
        private readonly GameManager _gameManager;
        private readonly QuestionRepository _questionRepository;
        private readonly ILogger<PartyHub> _logger;

        public PartyHub(PlayerManager playerManager, GameManager gameManager, QuestionRepository questionRepository, ILogger<PartyHub> logger)
        {
            _playerManager = playerManager;
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
