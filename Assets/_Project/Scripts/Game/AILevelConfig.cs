namespace TicTacFade.Game
{
    /// <summary>
    /// How one AI level deviates from perfect play (GDD §8.2, ADR 0005).
    /// Edited through GameConfigAsset.
    /// </summary>
    public sealed class AILevelConfig
    {
        /// <summary>Chance, 0..100, of playing a random legal move instead of the best one.</summary>
        public int ErrorPercent { get; }

        /// <summary>An immediate win is always played, error roll or not.</summary>
        public bool AlwaysTakesImmediateWin { get; }

        /// <summary>
        /// A random (error) move never hands the opponent an immediate win
        /// when some move avoids it.
        /// </summary>
        public bool BlocksImmediateLoss { get; }

        public AILevelConfig(int errorPercent, bool alwaysTakesImmediateWin, bool blocksImmediateLoss)
        {
            ErrorPercent = errorPercent;
            AlwaysTakesImmediateWin = alwaysTakesImmediateWin;
            BlocksImmediateLoss = blocksImmediateLoss;
        }
    }
}
