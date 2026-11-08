namespace lan_box_party_pack.server.Game
{
    public class GameSettings
    {
        public int GuessingDurationSeconds { get; set; } = 15;

        public int OvertimeDurationSeconds { get; set; } = 5;

        public int IncorrectGuessLockoutMilliseconds { get; set; } = 2000;
    }
}
