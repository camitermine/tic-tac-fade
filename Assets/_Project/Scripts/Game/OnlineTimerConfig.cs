namespace TicTacFade.Game
{
    /// <summary>
    /// Online turn-timer parameters (GDD §3.5, §9). They live in Game, not
    /// in Core's GameConfig, because the rules engine never reads them
    /// (CLAUDE.md, architecture rule 6). Edited through GameConfigAsset.
    /// </summary>
    public sealed class OnlineTimerConfig
    {
        public float TurnTimeSeconds { get; }
        public float AbsentTurnTimeSeconds { get; }
        public int MaxConsecutiveTimeouts { get; }

        public OnlineTimerConfig(float turnTimeSeconds, float absentTurnTimeSeconds, int maxConsecutiveTimeouts)
        {
            TurnTimeSeconds = turnTimeSeconds;
            AbsentTurnTimeSeconds = absentTurnTimeSeconds;
            MaxConsecutiveTimeouts = maxConsecutiveTimeouts;
        }

        /// <summary>MVP values from GDD §9: 30 s, 10 s when absent, 3 timeouts.</summary>
        public static OnlineTimerConfig Mvp() => new OnlineTimerConfig(30f, 10f, 3);
    }
}
