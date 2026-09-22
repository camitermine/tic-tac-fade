using System;
using System.Collections.Generic;

namespace TicTacFade.Core
{
    /// <summary>
    /// Single entry point to advance the game. The input
    /// <see cref="GameState"/> is never mutated: <see cref="Apply"/> always
    /// returns a new instance together with the events that describe what
    /// happened (CLAUDE.md, architecture rules 3 and 7).
    /// </summary>
    public static class RulesEngine
    {
        /// <summary>
        /// Validates a move without applying it. Public because the UI needs
        /// it for the ghost piece, and online needs it to pre-validate before
        /// sending the move over the network. <see cref="Apply"/> uses it
        /// internally.
        /// </summary>
        public static bool IsLegal(GameState state, Move move, out MoveRejectionReason reason)
        {
            if (state.IsOver)
            {
                reason = MoveRejectionReason.GameAlreadyEnded;
                return false;
            }

            if (move.Player != state.CurrentPlayer)
            {
                reason = MoveRejectionReason.NotPlayersTurn;
                return false;
            }

            if (move.CellIndex < 0 || move.CellIndex >= state.Config.CellCount)
            {
                reason = MoveRejectionReason.CellOutOfRange;
                return false;
            }

            // This already covers the replacement restriction from GDD §3.2:
            // the player's own oldest piece's cell (about to fade) stays
            // occupied until the FIFO clears it AFTER this validation.
            if (state.Cells[move.CellIndex] != Occupant.None)
            {
                reason = MoveRejectionReason.CellOccupied;
                return false;
            }

            reason = MoveRejectionReason.None;
            return true;
        }

        /// <summary>
        /// Resolution order (GDD §3.3): validate → place → FIFO → evaluate
        /// win on the post-FIFO board → if there was no win, evaluate draw
        /// (repetition, then move limit).
        /// </summary>
        public static (GameState State, IReadOnlyList<IGameEvent> Events) Apply(GameState state, Move move)
        {
            if (!IsLegal(state, move, out var rejectionReason))
            {
                return (state, new IGameEvent[] { new MoveRejectedEvent(move, rejectionReason) });
            }

            var config = state.Config;
            var events = new List<IGameEvent>();

            // Place the new piece.
            var cells = new Occupant[config.CellCount];
            for (int i = 0; i < cells.Length; i++)
                cells[i] = state.Cells[i];
            cells[move.CellIndex] = move.Player;

            var ownQueue = new List<int>(state.GetQueue(move.Player)) { move.CellIndex };
            int totalMoves = state.TotalMoves + 1;
            events.Add(new PiecePlacedEvent(move.Player, move.CellIndex));

            // Apply FIFO if the buffer was exceeded.
            if (ownQueue.Count > config.BufferSize)
            {
                int removedCell = ownQueue[0];
                ownQueue.RemoveAt(0);
                cells[removedCell] = Occupant.None;
                events.Add(new PieceFadedEvent(move.Player, removedCell));
            }

            IReadOnlyList<int> queueX = move.Player == Occupant.X ? ownQueue : state.QueueX;
            IReadOnlyList<int> queueO = move.Player == Occupant.O ? ownQueue : state.QueueO;

            // Evaluate the win on the ALREADY post-FIFO board: a line that
            // depended on the just-removed piece doesn't count (GDD §3.3).
            if (WinChecker.TryGetWinningLine(cells, config.BoardSize, config.WinLength, move.Player, out var winningLine))
            {
                events.Add(new GameEndedEvent(move.Player, GameEndReason.Win, winningLine));
                var wonState = new GameState(
                    config, cells, queueX, queueO,
                    currentPlayer: state.CurrentPlayer,
                    totalMoves: totalMoves,
                    isOver: true,
                    winner: move.Player,
                    endReason: GameEndReason.Win,
                    winningLine: winningLine,
                    positionCounts: state.PositionCounts);
                return (wonState, events);
            }

            // No win: evaluate draw. Repetition first, then move limit.
            Occupant nextPlayer = move.Player == Occupant.X ? Occupant.O : Occupant.X;
            var positionKey = PositionKey.Compute(config, queueX, queueO, nextPlayer);
            var positionCounts = new Dictionary<PositionKey, int>(state.PositionCounts);
            positionCounts.TryGetValue(positionKey, out int occurrences);
            occurrences++;
            positionCounts[positionKey] = occurrences;

            if (occurrences >= config.RepetitionLimit)
            {
                events.Add(new GameEndedEvent(Occupant.None, GameEndReason.DrawByRepetition, Array.Empty<int>()));
                return (BuildDrawState(config, cells, queueX, queueO, nextPlayer, totalMoves, GameEndReason.DrawByRepetition, positionCounts), events);
            }

            if (totalMoves >= config.MaxTotalMoves)
            {
                events.Add(new GameEndedEvent(Occupant.None, GameEndReason.DrawByMoveLimit, Array.Empty<int>()));
                return (BuildDrawState(config, cells, queueX, queueO, nextPlayer, totalMoves, GameEndReason.DrawByMoveLimit, positionCounts), events);
            }

            // Match continues: pass the turn.
            var nextState = new GameState(
                config, cells, queueX, queueO,
                currentPlayer: nextPlayer,
                totalMoves: totalMoves,
                isOver: false,
                winner: Occupant.None,
                endReason: null,
                winningLine: Array.Empty<int>(),
                positionCounts: positionCounts);
            return (nextState, events);
        }

        static GameState BuildDrawState(GameConfig config, IReadOnlyList<Occupant> cells, IReadOnlyList<int> queueX,
            IReadOnlyList<int> queueO, Occupant nextPlayer, int totalMoves, GameEndReason reason,
            IReadOnlyDictionary<PositionKey, int> positionCounts)
        {
            return new GameState(
                config, cells, queueX, queueO,
                currentPlayer: nextPlayer,
                totalMoves: totalMoves,
                isOver: true,
                winner: Occupant.None,
                endReason: reason,
                winningLine: Array.Empty<int>(),
                positionCounts: positionCounts);
        }
    }
}
