using NUnit.Framework;
using TicTacFade.Game;

namespace TicTacFade.PlayModeTests
{
    /// <summary>
    /// Runs once around every test in this namespace. Silences the online
    /// diagnostic log (<see cref="NetDiagnostics"/>): several tests assert
    /// <c>LogAssert.NoUnexpectedReceived()</c> to catch errors and
    /// exceptions, and that assertion also counts plain info lines.
    /// </summary>
    [SetUpFixture]
    public class PlayModeTestsSetup
    {
        [OneTimeSetUp]
        public void SilenceNetDiagnostics() => NetDiagnostics.Enabled = false;

        [OneTimeTearDown]
        public void RestoreNetDiagnostics() => NetDiagnostics.Enabled = true;
    }
}
