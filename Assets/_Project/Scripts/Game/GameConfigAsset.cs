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

        [Header("Online turn timer (Game only: the rules engine never reads it)")]
        [SerializeField] float turnTimeSeconds = 30f;
        [SerializeField] float absentTurnTimeSeconds = 10f;
        [SerializeField] int maxConsecutiveTimeouts = 3;

        // Starting values from AIConfig.Default(), to be tuned by playing (GDD §8.2).
        [Header("AI levels (Game only: the rules engine never reads them)")]
        [SerializeField, Range(0, 100)] int easyErrorPercent = 50;
        [SerializeField] bool easyAlwaysTakesImmediateWin = false;
        [SerializeField] bool easyBlocksImmediateLoss = false;
        [SerializeField, Range(0, 100)] int normalErrorPercent = 20;
        [SerializeField] bool normalAlwaysTakesImmediateWin = true;
        [SerializeField] bool normalBlocksImmediateLoss = true;
        [SerializeField, Range(0, 100)] int perfectErrorPercent = 0;
        [SerializeField] bool perfectAlwaysTakesImmediateWin = true;
        [SerializeField] bool perfectBlocksImmediateLoss = true;

        [Header("AI delay before playing, random in [min, max] seconds")]
        [SerializeField] float aiMinDelaySeconds = 0.6f;
        [SerializeField] float aiMaxDelaySeconds = 1.4f;

        public GameConfig ToGameConfig() => new GameConfig(boardSize, bufferSize, winLength, maxTotalMoves, repetitionLimit);

        public OnlineTimerConfig ToOnlineTimerConfig() =>
            new OnlineTimerConfig(turnTimeSeconds, absentTurnTimeSeconds, maxConsecutiveTimeouts);

        public AIConfig ToAIConfig() => new AIConfig(
            new AILevelConfig(easyErrorPercent, easyAlwaysTakesImmediateWin, easyBlocksImmediateLoss),
            new AILevelConfig(normalErrorPercent, normalAlwaysTakesImmediateWin, normalBlocksImmediateLoss),
            new AILevelConfig(perfectErrorPercent, perfectAlwaysTakesImmediateWin, perfectBlocksImmediateLoss),
            aiMinDelaySeconds,
            aiMaxDelaySeconds);
    }
}
