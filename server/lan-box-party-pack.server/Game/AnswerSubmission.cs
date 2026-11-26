namespace lan_box_party_pack.server.Game
{
    public class AnswerSubmission
    {
        public string PlayerId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public string PlayerColor { get; set; } = "";
        public string Answer { get; set; } = "";
        public bool IsCorrect { get; set; }

        public int PointsAwarded { get; set; }
        public int? CorrectAnswerNumber { get; set; }
        public bool AlreadyAnsweredCorrectly { get; set; }
        public double? VideoTimestampSeconds { get; set; }
        public int AnswerOptionId { get; set; }
    }
}