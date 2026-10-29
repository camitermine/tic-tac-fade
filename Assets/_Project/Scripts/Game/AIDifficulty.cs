namespace TicTacFade.Game
{
    /// <summary>
    /// AI levels (GDD §8.2). Values are explicit because a menu button may
    /// serialize them: append new levels, never insert.
    /// </summary>
    public enum AIDifficulty
    {
        Easy = 0,
        Normal = 1,
        Perfect = 2,
    }
}
