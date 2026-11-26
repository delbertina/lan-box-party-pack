using lan_box_party_pack.server.Players;
using lan_box_party_pack.server.Questions;

namespace lan_box_party_pack.server.Game
{
    public class GameManager
    {
        private readonly object _stateLock = new();
        private QuestionSet? _questionSet;
        private readonly HashSet<string> _correctPlayersThisQuestion = [];
        private double _videoTimestampSeconds;
        private DateTimeOffset _lastPlaybackUpdateAt;
        private bool _isVideoPlaying;

        public GameState State { get; private set; } = new();

        public void StartNewGame(IEnumerable<Player> players, QuestionSet questionSet)
        {
            lock (_stateLock)
            {
                if (questionSet.Questions.Count == 0)
                {
                    throw new InvalidOperationException(
                        "The question set contains no questions."
                    );
                }

                _questionSet = questionSet;
                _correctPlayersThisQuestion.Clear();

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
        }

        public void StartQuestion()
        {
            lock (_stateLock)
            {
                EnsurePhase(GamePhase.QuestionIntro);

                var now = DateTimeOffset.UtcNow;

                State.Phase = GamePhase.Playing;
                State.PhaseStartedAt = now;
                State.PhaseEndsAt = now.AddSeconds(
                    State.Settings.GuessingDurationSeconds
                );
                State.PhasePausedAt = null;

                State.RevealedAnswer = null;
                State.RevealedAnswerSubtext = null;

                _videoTimestampSeconds =
                    State.CurrentQuestion!.QuestionVideo.StartTime;

                _lastPlaybackUpdateAt = now;
                _isVideoPlaying = true;
            }
        }

        public void RevealAnswer()
        {
            lock (_stateLock)
            {
                if (State.Phase != GamePhase.Playing &&
                    State.Phase != GamePhase.Overtime)
                {
                    throw new InvalidOperationException(
                        $"Cannot reveal the answer during phase {State.Phase}."
                    );
                }

                RevealAnswerCore(DateTimeOffset.UtcNow);
            }
        }

        public void AdvanceQuestion()
        {
            lock (_stateLock)
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
                    _videoTimestampSeconds = 0;
                    _lastPlaybackUpdateAt = DateTimeOffset.UtcNow;
                    _isVideoPlaying = false;
                    return;
                }

                _videoTimestampSeconds = 0;
                _lastPlaybackUpdateAt = DateTimeOffset.UtcNow;
                _isVideoPlaying = false;
                _correctPlayersThisQuestion.Clear();

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

        public AnswerSubmission SubmitAnswer(Player player, int submittedAnswerOptionId)
        {
            lock (_stateLock)
            {
                if (State.Phase != GamePhase.Playing &&
                    State.Phase != GamePhase.Overtime)
                {
                    throw new InvalidOperationException(
                        "Answers can only be submitted while a question is active."
                    );
                }

                var question = GetCurrentQuestion();

                var isCorrect = submittedAnswerOptionId ==
                    question.CorrectAnswerOptionId;

                var estimatedVideoTimestamp = _videoTimestampSeconds;

                if (_isVideoPlaying)
                {
                    estimatedVideoTimestamp += (
                        DateTimeOffset.UtcNow - _lastPlaybackUpdateAt
                    ).TotalSeconds;
                }

                var submission = new AnswerSubmission
                {
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    PlayerColor = player.Color,
                    AnswerOptionId = submittedAnswerOptionId,
                    IsCorrect = isCorrect,
                    AlreadyAnsweredCorrectly =
                        _correctPlayersThisQuestion.Contains(player.Id),
                    VideoTimestampSeconds = estimatedVideoTimestamp,
                };

                // Ensure the player has a score entry, including players
                // who may have joined after the game started.
                State.Scores.TryAdd(player.Id, 0);

                // Only the first correct answer from each player earns points.
                if (isCorrect && !submission.AlreadyAnsweredCorrectly)
                {
                    _correctPlayersThisQuestion.Add(player.Id);

                    var correctAnswerNumber = _correctPlayersThisQuestion.Count;
                    var configuredPoints = State.Settings.CorrectAnswerPoints;

                    var points = correctAnswerNumber <= configuredPoints.Length
                        ? configuredPoints[correctAnswerNumber - 1]
                        : State.Settings.RemainingCorrectAnswerPoints;

                    points = Math.Max(0, points);

                    submission.CorrectAnswerNumber = correctAnswerNumber;
                    submission.PointsAwarded = points;

                    State.Scores[player.Id] += points;
                }

                return submission;
            }
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

        private void RevealAnswerCore(DateTimeOffset now)
        {
            var question = GetCurrentQuestion();

            State.Phase = GamePhase.Reveal;
            State.PhaseStartedAt = now;
            State.PhaseEndsAt = null;

            State.RevealedAnswer = question.Answer;
            State.RevealedAnswerSubtext = question.AnswerSubtext;
        }

        public void StartOvertime()
        {
            lock (_stateLock)
            {
                EnsurePhase(GamePhase.Playing);

                State.Phase = GamePhase.Overtime;
                State.PhaseStartedAt = DateTimeOffset.UtcNow;
                State.PhaseEndsAt = null;
            }
        }

        public bool UpdatePlayback(double videoTimestampSeconds, bool isPlaying)
        {
            lock (_stateLock)
            {
                if (State.Phase != GamePhase.Playing)
                    return false;

                var now = DateTimeOffset.UtcNow;
                var playbackStateChanged = _isVideoPlaying != isPlaying;

                if (playbackStateChanged)
                {
                    if (!isPlaying)
                    {
                        State.PhasePausedAt = now;
                    }
                    else if (State.PhasePausedAt.HasValue)
                    {
                        var pausedDuration = now - State.PhasePausedAt.Value;

                        if (State.PhaseEndsAt.HasValue)
                        {
                            State.PhaseEndsAt += pausedDuration;
                        }

                        State.PhasePausedAt = null;
                    }
                }

                _videoTimestampSeconds = Math.Max(0, videoTimestampSeconds);
                _lastPlaybackUpdateAt = now;
                _isVideoPlaying = isPlaying;

                return playbackStateChanged;
            }
        }

        public void EndPlayback(double videoTimestampSeconds)
        {
            lock (_stateLock)
            {
                EnsurePhase(GamePhase.Playing);

                _videoTimestampSeconds = Math.Max(0, videoTimestampSeconds);
                _lastPlaybackUpdateAt = DateTimeOffset.UtcNow;
                _isVideoPlaying = false;

                var now = DateTimeOffset.UtcNow;

                State.Phase = GamePhase.Overtime;
                State.PhaseStartedAt = now;
                State.PhaseEndsAt = now.AddSeconds(
                    State.Settings.OvertimeDurationSeconds
                );
                State.PhasePausedAt = null;
            }
        }
    }
}
