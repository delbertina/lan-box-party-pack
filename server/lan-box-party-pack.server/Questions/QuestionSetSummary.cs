namespace lan_box_party_pack.server.Questions;

public class QuestionSetSummary
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Rating { get; set; }
    public int QuestionCount { get; set; }
}