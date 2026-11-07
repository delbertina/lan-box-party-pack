using lan_box_party_pack.server.Players;

namespace lan_box_party_pack.server.Game
{
    public class GameManager
    {
        public GameState State { get; private set; } = new();

        public void StartNewGame(IEnumerable<Player> players)
        {
            State = new GameState
            {
                GameSessionId = Guid.NewGuid().ToString(),
                Phase = GamePhase.Lobby,
                CurrentQuestionIndex = -1,
                Scores = players.ToDictionary(
                    player => player.Id,
                    _ => 0
                )
            };
        }
    }
}
