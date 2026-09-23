namespace TicTacFade.Net
{
    /// <summary>
    /// Format of a Multiplayer Services session (lobby) join code, checked
    /// locally so a malformed code never reaches the network.
    /// Source: the SDK documents it for Relay join codes (Relay/Models/
    /// JoinRequest.cs: "case-insensitive, span six to twelve characters,
    /// composed only of characters in '6789BCDFGHJKLMNPQRTW'"). It does not
    /// document the lobby code, but the real service is consistent with it:
    /// it generated LD7987 and B6DRRR, rejected 'Z' as an invalid character,
    /// and accepted the format of BBBBBB (answering "not found").
    /// </summary>
    public static class JoinCodeFormat
    {
        public const string Alphabet = "6789BCDFGHJKLMNPQRTW";
        public const int MinLength = 6;
        public const int MaxLength = 12;

        /// <summary>Expects a code already trimmed and upper-cased.</summary>
        public static bool IsWellFormed(string code)
        {
            if (code == null || code.Length < MinLength || code.Length > MaxLength)
                return false;

            foreach (var c in code)
            {
                if (Alphabet.IndexOf(c) < 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Safety net for the local rule: checks a code the service just
        /// generated. The risk is asymmetric: if this rule ever becomes
        /// stricter than the service, players can't join real rooms and
        /// nothing else would reveal it. Logs an explicit error when that
        /// happens. Returns whether the code passed.
        /// </summary>
        public static bool CheckGeneratedCode(string code)
        {
            var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
            if (IsWellFormed(normalized))
                return true;

            UnityEngine.Debug.LogError(
                $"Tic-Tac-Fade: the room code format changed: local validation would reject the generated code '{code}'. " +
                $"Update {nameof(JoinCodeFormat)} (alphabet '{Alphabet}', length {MinLength}-{MaxLength}).");
            return false;
        }
    }
}
