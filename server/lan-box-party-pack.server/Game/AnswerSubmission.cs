namespace lan_box_party_pack.server.Game
{
    public class AnswerSubmission
    {
        public string PlayerId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public string PlayerColor { get; set; } = "";
        public string Answer { get; set; } = "";
        public bool IsCorrect { get; set; }
    }
}
