namespace lan_box_party_pack.server.Game
{
    public class GameManager
    {
        public GameState State { get; private set; } = new();

        public void StartNewGame()
        {
            State = new GameState
            {
                GameSessionId = Guid.NewGuid().ToString(),
                Phase = GamePhase.Lobby,
                CurrentQuestionIndex = -1
            };
        }
    }
}
