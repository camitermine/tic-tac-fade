namespace TicTacFade.Game
{
    /// <summary>
    /// Screen-flow states (GDD §4.3). Values are explicit because they are
    /// serialized in the scene (FlowScreenVisibility): new states (lobby,
    /// join by code) must be appended, never inserted, so existing ones keep
    /// their value.
    /// </summary>
    public enum FlowState
    {
        Menu = 0,
        Playing = 1,
        Result = 2,
        Lobby = 3,
        JoinByCode = 4,
    }
}
