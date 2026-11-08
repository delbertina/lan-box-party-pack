namespace lan_box_party_pack.server.Questions
{
    public class QuestionSet
    {
        public int Id { get; set; }

        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public int Rating { get; set; }

        public List<Question> Questions { get; set; } = [];
    }
}
