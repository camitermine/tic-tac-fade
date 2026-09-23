using UnityEngine;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// Editor-editable wrapper around Core's pure <see cref="GameConfig"/>
    /// (ADR 0001). Field defaults match the MVP values from GDD §9, so a
    /// freshly created asset starts identical to <c>GameConfig.Mvp()</c>.
    /// Named differently from <see cref="GameConfig"/> on purpose: files
    /// that <c>using TicTacFade.Core;</c> alongside <c>TicTacFade.Game</c>
    /// would get an ambiguous-reference error otherwise (hit once already
    /// with SafeArea in the UI parte 1 work).
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Tic-Tac-Fade/Game Config")]
    public class GameConfigAsset : ScriptableObject
    {
        [SerializeField] int boardSize = 3;
        [SerializeField] int bufferSize = 3;
        [SerializeField] int winLength = 3;
        [SerializeField] int maxTotalMoves = 40;
        [SerializeField] int repetitionLimit = 3;

        public GameConfig ToGameConfig() => new GameConfig(boardSize, bufferSize, winLength, maxTotalMoves, repetitionLimit);
    }
}
