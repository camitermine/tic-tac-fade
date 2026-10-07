using System.Collections.Generic;
using TicTacFade.Core;

namespace TicTacFade.Solver
{
    /// <summary>
    /// Result of <see cref="GameSolver.Solve"/>: the value of every position
    /// reachable from the initial one, looked up by <see cref="PositionKey"/>.
    /// Values ignore the draw limits (move cap, repetition): see ADR 0004.
    /// </summary>
    public sealed class SolverTable
    {
        readonly GameConfig _config;
        readonly Dictionary<PositionKey, int> _indexByKey;
        readonly IReadOnlyList<SolverPosition> _positions;
        readonly IReadOnlyList<SolverMoveEdge[]> _moves;
        readonly PositionResult[] _results;

        public PositionKey InitialPosition { get; }
        public PositionResult InitialResult => _results[_indexByKey[InitialPosition]];
        public SolverStats Stats { get; }

        internal SolverTable(GameConfig config, Dictionary<PositionKey, int> indexByKey, IReadOnlyList<SolverPosition> positions,
            IReadOnlyList<SolverMoveEdge[]> moves, PositionResult[] results, PositionKey initialPosition, SolverStats stats)
        {
            _config = config;
            _indexByKey = indexByKey;
            _positions = positions;
            _moves = moves;
            _results = results;
            InitialPosition = initialPosition;
            Stats = stats;
        }

        public bool Contains(PositionKey key) => _indexByKey.ContainsKey(key);

        public bool TryGetResult(PositionKey key, out PositionResult result)
        {
            if (_indexByKey.TryGetValue(key, out int index))
            {
                result = _results[index];
                return true;
            }

            result = default;
            return false;
        }

        /// <summary>Convenience for a live game state (its queues and the player to move).</summary>
        public bool TryGetResult(GameState state, out PositionResult result) =>
            TryGetResult(PositionKey.Compute(_config, state.QueueX, state.QueueO, state.CurrentPlayer), out result);

        /// <summary>Every legal move from a solved position, with its value for the mover. Empty if unknown.</summary>
        public IReadOnlyList<MoveResult> GetMoveResults(PositionKey key)
        {
            if (!_indexByKey.TryGetValue(key, out int index))
                return new MoveResult[0];

            var edges = _moves[index];
            var results = new MoveResult[edges.Length];
            for (int i = 0; i < edges.Length; i++)
            {
                var edge = edges[i];
                results[i] = edge.Next < 0
                    ? new MoveResult(edge.Cell, true, default, new PositionResult(PositionOutcome.Win, 1))
                    : new MoveResult(edge.Cell, false, _positions[edge.Next].Key, _results[edge.Next].SeenByPreviousMover());
            }
            return results;
        }
    }
}
