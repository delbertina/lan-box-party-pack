namespace lan_box_party_pack.server.Game
{
    public class GameState
    {
        public string? GameSessionId { get; set; }

        public GamePhase Phase { get; set; } = GamePhase.Lobby;

        public int CurrentQuestionIndex { get; set; } = -1;

        public DateTimeOffset? PhaseStartedAt { get; set; }

        public DateTimeOffset? PhaseEndsAt { get; set; }

        public Dictionary<string, int> Scores { get; set; } = new();
    }
}
