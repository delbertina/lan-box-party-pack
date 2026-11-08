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
    }
}
