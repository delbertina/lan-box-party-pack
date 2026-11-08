using lan_box_party_pack.server.Players;
using lan_box_party_pack.server.Questions;

namespace lan_box_party_pack.server.Game
{
    public class GameManager
    {
        private QuestionSet? _questionSet;

        public GameState State { get; private set; } = new();

        public void StartNewGame(
            IEnumerable<Player> players,
            QuestionSet questionSet)
        {
            if (questionSet.Questions.Count == 0)
            {
                throw new InvalidOperationException(
                    "The question set contains no questions."
                );
            }

            _questionSet = questionSet;

            State = new GameState
            {
                GameSessionId = Guid.NewGuid().ToString(),
                Phase = GamePhase.QuestionIntro,
                CurrentQuestionIndex = 0,

                QuestionSetId = questionSet.Id,
                QuestionSetTitle = questionSet.Title,
                TotalQuestions = questionSet.Questions.Count,

                Scores = players.ToDictionary(
                    player => player.Id,
                    _ => 0
                ),

                CurrentQuestion = CreateQuestionPrompt(
                    questionSet.Questions[0]
                )
            };
        }

        public void StartQuestion()
        {
            EnsurePhase(GamePhase.QuestionIntro);

            State.Phase = GamePhase.Playing;
            State.PhaseStartedAt = DateTimeOffset.UtcNow;
            State.PhaseEndsAt = State.PhaseStartedAt.Value.AddSeconds(
                State.Settings.GuessingDurationSeconds
            );

            State.RevealedAnswer = null;
            State.RevealedAnswerSubtext = null;
        }

        public void RevealAnswer()
        {
            EnsurePhase(GamePhase.Playing);

            var question = GetCurrentQuestion();

            State.Phase = GamePhase.Reveal;
            State.PhaseStartedAt = DateTimeOffset.UtcNow;
            State.PhaseEndsAt = null;

            State.RevealedAnswer = question.Answer;
            State.RevealedAnswerSubtext = question.AnswerSubtext;
        }

        public void AdvanceQuestion()
        {
            EnsurePhase(GamePhase.Reveal);

            var nextIndex = State.CurrentQuestionIndex + 1;

            if (_questionSet == null)
            {
                throw new InvalidOperationException(
                    "No question set is loaded."
                );
            }

            if (nextIndex >= _questionSet.Questions.Count)
            {
                State.Phase = GamePhase.Results;
                State.PhaseStartedAt = DateTimeOffset.UtcNow;
                State.PhaseEndsAt = null;
                State.CurrentQuestion = null;
                return;
            }

            State.CurrentQuestionIndex = nextIndex;
            State.CurrentQuestion = CreateQuestionPrompt(
                _questionSet.Questions[nextIndex]
            );

            State.Phase = GamePhase.QuestionIntro;
            State.PhaseStartedAt = DateTimeOffset.UtcNow;
            State.PhaseEndsAt = null;

            State.RevealedAnswer = null;
            State.RevealedAnswerSubtext = null;
        }

        private Question GetCurrentQuestion()
        {
            if (_questionSet == null ||
                State.CurrentQuestionIndex < 0 ||
                State.CurrentQuestionIndex >= _questionSet.Questions.Count)
            {
                throw new InvalidOperationException(
                    "There is no current question."
                );
            }

            return _questionSet.Questions[State.CurrentQuestionIndex];
        }

        private static QuestionPrompt CreateQuestionPrompt(Question question)
        {
            return new QuestionPrompt
            {
                QuestionText = question.QuestionText,
                QuestionVideo = question.QuestionVideo
            };
        }

        private void EnsurePhase(GamePhase expectedPhase)
        {
            if (State.Phase != expectedPhase)
            {
                throw new InvalidOperationException(
                    $"Cannot perform this action during phase {State.Phase}."
                );
            }
        }

        public AnswerSubmission SubmitAnswer(Player player, string submittedAnswer)
        {
            if (State.Phase != GamePhase.Playing &&
                State.Phase != GamePhase.Overtime)
            {
                throw new InvalidOperationException(
                    "Answers can only be submitted while a question is active."
                );
            }

            var question = GetCurrentQuestion();

            var isCorrect = NormalizeAnswer(submittedAnswer) ==
                            NormalizeAnswer(question.Answer);

            return new AnswerSubmission
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                PlayerColor = player.Color,
                Answer = submittedAnswer,
                IsCorrect = isCorrect
            };
        }

        private static string NormalizeAnswer(string answer)
        {
            return new string(
                answer
                    .Where(char.IsLetterOrDigit)
                    .Select(char.ToLowerInvariant)
                    .ToArray()
            );
        }
    }
}
