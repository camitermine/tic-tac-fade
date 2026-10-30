using UnityEngine;

namespace TicTacFade.UI
{
    /// <summary>
    /// App-wide frame rate. Android caps at 30 FPS unless a target is set,
    /// and the art pass brings animations (pulse, dissolve, particles) that
    /// look choppy at 30.
    /// </summary>
    public static class FrameRateBootstrap
    {
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply() => Application.targetFrameRate = TargetFrameRate;
    }
}
