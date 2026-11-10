using System.Text.Json;

namespace lan_box_party_pack.server.Questions
{
    public class QuestionRepository
    {
        private readonly string _questionsDirectory;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public QuestionRepository(IWebHostEnvironment environment)
        {
            _questionsDirectory = Path.Combine(
                environment.ContentRootPath,
                "Questions"
            );
        }

        public QuestionSet Load(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Invalid question set filename.");
            }

            var path = Path.Combine(_questionsDirectory, fileName);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"Question set not found: {fileName}",
                    path
                );
            }

            var json = File.ReadAllText(path);

            return JsonSerializer.Deserialize<QuestionSet>(
                json,
                _jsonOptions
            ) ?? throw new InvalidOperationException(
                $"Could not load question set: {fileName}"
            );
        }

        public List<QuestionSetSummary> GetAvailableQuestionSets()
        {
            if (!Directory.Exists(_questionsDirectory))
            {
                return [];
            }

            return Directory
                .GetFiles(_questionsDirectory, "*.json")
                .Select(path =>
                {
                    var fileName = Path.GetFileName(path);
                    var questionSet = Load(fileName);

                    return new QuestionSetSummary
                    {
                        Id = questionSet.Id,
                        FileName = fileName,
                        Title = questionSet.Title,
                        Description = questionSet.Description,
                        Rating = questionSet.Rating,
                        QuestionCount = questionSet.Questions.Count
                    };
                })
                .OrderBy(set => set.Title)
                .ToList();
        }
    }
}
