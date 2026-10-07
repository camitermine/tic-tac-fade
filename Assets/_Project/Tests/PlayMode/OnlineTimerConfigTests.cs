using NUnit.Framework;
using UnityEngine;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// The online timer parameters live in Game (CLAUDE.md rule 6), so their
    /// GDD §9 check lives here and not with Core's GameConfig tests.
    /// </summary>
    public class OnlineTimerConfigTests
    {
        [Test]
        public void Mvp_MatchesGdd9Parameters()
        {
            AssertGdd9(OnlineTimerConfig.Mvp());
        }

        [Test]
        public void FreshGameConfigAsset_MatchesGdd9Parameters()
        {
            var asset = ScriptableObject.CreateInstance<GameConfigAsset>();
            try
            {
                AssertGdd9(asset.ToOnlineTimerConfig());
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        static void AssertGdd9(OnlineTimerConfig config)
        {
            Assert.AreEqual(30f, config.TurnTimeSeconds, "TurnTime (GDD §9).");
            Assert.AreEqual(10f, config.AbsentTurnTimeSeconds, "AbsentTurnTime (GDD §9).");
            Assert.AreEqual(3, config.MaxConsecutiveTimeouts, "MaxConsecutiveTimeouts (GDD §9).");
        }
    }
}
