namespace lan_box_party_pack.server.Questions
{
    public class Question
    {
        public string QuestionText { get; set; } = "";

        public QuestionVideo QuestionVideo { get; set; } = new();

        public string Answer { get; set; } = "";

        public string AnswerSubtext { get; set; } = "";

        public int CorrectAnswerOptionId { get; set; }
    }
}