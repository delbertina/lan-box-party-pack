using lan_box_party_pack.server.Questions;

namespace lan_box_party_pack.server.Game
{
    public class GameState
    {
        public string? GameSessionId { get; set; }

        public GamePhase Phase { get; set; } = GamePhase.Lobby;

        public int CurrentQuestionIndex { get; set; } = -1;

        public DateTimeOffset? PhaseStartedAt { get; set; }

        public DateTimeOffset? PhaseEndsAt { get; set; }
        public DateTimeOffset? PhasePausedAt { get; set; }

        public Dictionary<string, int> Scores { get; set; } = new();

        public int QuestionSetId { get; set; }

        public string QuestionSetTitle { get; set; } = "";

        public int TotalQuestions { get; set; }

        public QuestionPrompt? CurrentQuestion { get; set; }

        public GameSettings Settings { get; set; } = new();

        // Only populated during the Reveal phase.
        public string? RevealedAnswer { get; set; }

        public string? RevealedAnswerSubtext { get; set; }
    }
}
