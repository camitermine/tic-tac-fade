namespace TicTacFade.Game
{
    /// <summary>
    /// AI parameters (GDD §8.2). They live in Game, not in Core's GameConfig,
    /// because the rules engine never reads them (CLAUDE.md, architecture
    /// rule 6). Edited through GameConfigAsset.
    /// </summary>
    public sealed class AIConfig
    {
        public AILevelConfig Easy { get; }
        public AILevelConfig Normal { get; }
        public AILevelConfig Perfect { get; }

        /// <summary>The AI waits a random time in [min, max] before playing; 0 plays at once.</summary>
        public float MinDelaySeconds { get; }
        public float MaxDelaySeconds { get; }

        public AIConfig(AILevelConfig easy, AILevelConfig normal, AILevelConfig perfect, float minDelaySeconds, float maxDelaySeconds)
        {
            Easy = easy;
            Normal = normal;
            Perfect = perfect;
            MinDelaySeconds = minDelaySeconds;
            MaxDelaySeconds = maxDelaySeconds;
        }

        public AILevelConfig For(AIDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy: return Easy;
                case AIDifficulty.Normal: return Normal;
                default: return Perfect;
            }
        }

        /// <summary>Starting values, to be tuned by playing: 50 / 20 / 0 % error, 0.6–1.4 s delay.</summary>
        public static AIConfig Default() => new AIConfig(
            easy: new AILevelConfig(50, alwaysTakesImmediateWin: false, blocksImmediateLoss: false),
            normal: new AILevelConfig(20, alwaysTakesImmediateWin: true, blocksImmediateLoss: true),
            perfect: new AILevelConfig(0, alwaysTakesImmediateWin: true, blocksImmediateLoss: true),
            minDelaySeconds: 0.6f,
            maxDelaySeconds: 1.4f);
    }
}
