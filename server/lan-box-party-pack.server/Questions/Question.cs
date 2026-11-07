namespace lan_box_party_pack.server.Questions
{
    public class Question
    {
        public string Id { get; set; } = "";

        public string VideoId { get; set; } = "";

        public double StartSeconds { get; set; }

        public double EndSeconds { get; set; }

        public string CorrectAnswerId { get; set; } = "";

        public string Category { get; set; } = "";
    }
}
