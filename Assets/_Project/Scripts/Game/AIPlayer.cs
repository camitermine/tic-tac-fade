using System;
using UnityEngine;
using TicTacFade.Core;
using TicTacFade.Solver;

namespace TicTacFade.Game
{
    /// <summary>
    /// A side played by the AI on this device (GDD §8.2). It decides when its
    /// turn starts and delivers the move after a short random delay through
    /// <see cref="Tick"/>, which its owner calls every frame. With no delay
    /// it plays synchronously, like any autonomous controller. A pending
    /// move is dropped when the match stops (<see cref="NotifyMatchStopped"/>).
    /// </summary>
    public sealed class AIPlayer : IPlayerController
    {
        readonly CompactSolverTable _table;
        readonly AIConfig _config;
        readonly AILevelConfig _level;
        readonly IClock _clock;
        readonly IRandomSource _random;

        bool _hasPendingMove;
        Move _pendingMove;
        double _deadline;
        bool _loggedMissingPosition;

        public Occupant Player { get; }
        public AIDifficulty Difficulty { get; }
        public bool IsLocalHuman => false;
        public bool AcceptsLocalInput => false;

        /// <summary>A move is decided and waiting for its delay.</summary>
        public bool HasPendingMove => _hasPendingMove;

        public event Action<Move> MoveChosen;

        // Never raised: AcceptsLocalInput is constant.
        public event Action AcceptsLocalInputChanged { add { } remove { } }

        public AIPlayer(Occupant player, AIDifficulty difficulty, CompactSolverTable table, AIConfig config, IClock clock,
            IRandomSource random)
        {
            Player = player;
            Difficulty = difficulty;
            _table = table;
            _config = config;
            _level = config.For(difficulty);
            _clock = clock;
            _random = random;
        }

        public void NotifyTurnStarted(GameState state)
        {
            if (state.IsOver || state.CurrentPlayer != Player)
                return;

            var choice = AIMoveChooser.Choose(state, _table, _level, _random);
            LogIfTableOutOfSync(choice);
            var move = new Move(Player, choice.CellIndex);

            double delay = PickDelay();
            if (delay <= 0)
            {
                MoveChosen?.Invoke(move);
                return;
            }

            _pendingMove = move;
            _deadline = _clock.Now + delay;
            _hasPendingMove = true;
        }

        /// <summary>Delivers the pending move once its delay has passed.</summary>
        public void Tick()
        {
            if (!_hasPendingMove || _clock.Now < _deadline)
                return;

            _hasPendingMove = false;
            MoveChosen?.Invoke(_pendingMove);
        }

        public void NotifyMatchStopped() => _hasPendingMove = false;

        public void NotifyCellSelected(int cellIndex) { } // taps never drive the AI

        // Drawn after the move, and only when the range is not a single value.
        double PickDelay()
        {
            float min = _config.MinDelaySeconds;
            float max = _config.MaxDelaySeconds;
            if (max <= min)
                return min;
            return min + (max - min) * _random.Next(1001) / 1000.0;
        }

        void LogIfTableOutOfSync(AIMoveEvaluation choice)
        {
            if (!choice.MissingFromTable || _loggedMissingPosition)
                return;
            _loggedMissingPosition = true;
            Debug.LogError("Tic-Tac-Fade: the AI table is missing a reachable position; it is out of sync with the rules. " +
                "Regenerate it with Tic-Tac-Fade/Solver/Generate AI table.");
        }
    }
}
