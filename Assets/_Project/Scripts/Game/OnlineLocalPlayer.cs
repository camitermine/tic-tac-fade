using System;
using TicTacFade.Core;

namespace TicTacFade.Game
{
    /// <summary>
    /// The side a human plays on this device in an online match. A tap does
    /// NOT play the move: it proposes it to the host and waits. Until the
    /// host confirms or rejects it, no more input is accepted, so a cell
    /// can't be sent twice. The move reaches GameManager only through
    /// <see cref="ApplyConfirmed"/>.
    /// </summary>
    public sealed class OnlineLocalPlayer : IPlayerController, IConfirmedMoveReceiver
    {
        readonly Action<Move> _propose;
        bool _awaitingConfirmation;

        public Occupant Player { get; }
        public bool IsLocalHuman => true;
        public bool AcceptsLocalInput => !_awaitingConfirmation;

        public event Action<Move> MoveChosen;
        public event Action AcceptsLocalInputChanged;

        /// <param name="propose">Sends the proposal to the host (or validates it, on the host).</param>
        public OnlineLocalPlayer(Occupant player, Action<Move> propose)
        {
            Player = player;
            _propose = propose;
        }

        public void NotifyTurnStarted(GameState state) { } // waits for a tap

        public void NotifyCellSelected(int cellIndex)
        {
            if (_awaitingConfirmation)
                return;

            SetAwaitingConfirmation(true);
            _propose(new Move(Player, cellIndex));
        }

        public void ApplyConfirmed(Move move)
        {
            SetAwaitingConfirmation(false);
            MoveChosen?.Invoke(move);
        }

        /// <summary>The host rejected the proposal: input is accepted again.</summary>
        public void NotifyRejected() => SetAwaitingConfirmation(false);

        /// <summary>Clears a pending proposal (a new match starts).</summary>
        public void Reset() => SetAwaitingConfirmation(false);

        void SetAwaitingConfirmation(bool awaiting)
        {
            if (_awaitingConfirmation == awaiting) return;
            _awaitingConfirmation = awaiting;
            AcceptsLocalInputChanged?.Invoke();
        }
    }
}
