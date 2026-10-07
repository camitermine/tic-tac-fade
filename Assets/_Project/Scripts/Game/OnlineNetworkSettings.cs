namespace TicTacFade.Game
{
    /// <summary>
    /// Network waits of an online match. Not game rules (GDD §3.5 timers
    /// are in <see cref="OnlineTimerConfig"/>): they only decide how long
    /// to wait for the other device before treating it as gone.
    /// </summary>
    public sealed class OnlineNetworkSettings
    {
        /// <summary>Client: no Confirmed/Rejected for a proposal after this long means the host is gone.</summary>
        public float ProposalResponseTimeoutSeconds { get; }

        /// <summary>"Salir": how long to wait for the ForfeitAck before closing the session anyway.</summary>
        public float ForfeitAckTimeoutSeconds { get; }

        /// <summary>Host: grace after the network reports the client gone, before it loses by abandonment.</summary>
        public float DisconnectGraceSeconds { get; }

        /// <summary>Client: how often Ready is resent until the first StartMatch arrives.</summary>
        public float ReadyResendIntervalSeconds { get; }

        public OnlineNetworkSettings(float proposalResponseTimeoutSeconds, float forfeitAckTimeoutSeconds,
            float disconnectGraceSeconds, float readyResendIntervalSeconds)
        {
            ProposalResponseTimeoutSeconds = proposalResponseTimeoutSeconds;
            ForfeitAckTimeoutSeconds = forfeitAckTimeoutSeconds;
            DisconnectGraceSeconds = disconnectGraceSeconds;
            ReadyResendIntervalSeconds = readyResendIntervalSeconds;
        }

        public static OnlineNetworkSettings Default() => new OnlineNetworkSettings(10f, 2f, 5f, 1f);
    }
}
