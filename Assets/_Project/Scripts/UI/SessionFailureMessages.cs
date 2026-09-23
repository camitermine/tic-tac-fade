using TicTacFade.Game;

namespace TicTacFade.UI
{
    /// <summary>Player-facing text for each <see cref="SessionFailure"/>.</summary>
    public static class SessionFailureMessages
    {
        public const string Connecting = "Conectando...";

        public static string ToText(SessionFailure failure)
        {
            switch (failure)
            {
                case SessionFailure.None: return string.Empty;
                case SessionFailure.InvalidCode: return "Código inválido. Revisalo e intentá de nuevo.";
                case SessionFailure.SessionFull: return "La sala está llena.";
                case SessionFailure.NoConnection: return "Sin conexión a internet.";
                case SessionFailure.ServiceUnavailable: return "El servicio no está disponible. Probá más tarde.";
                case SessionFailure.Timeout: return "La conexión tardó demasiado. Intentá de nuevo.";
                case SessionFailure.NotLinked: return "El juego no está configurado para jugar online.";
                case SessionFailure.SessionClosed: return "La sala se cerró.";
                case SessionFailure.JoinRejected: return "No se pudo unir a la sala. Revisá el código o pedí uno nuevo.";
                case SessionFailure.OpponentLeft: return "El rival salió.";
                case SessionFailure.Desync: return "Se perdió la sincronización con el rival.";
                default: return "No se pudo conectar. Intentá de nuevo.";
            }
        }
    }
}
