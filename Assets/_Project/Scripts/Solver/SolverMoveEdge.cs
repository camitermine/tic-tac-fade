namespace TicTacFade.Solver
{
    /// <summary>A move in the solver graph: the cell and the index of the next position (-1 = immediate win).</summary>
    internal readonly struct SolverMoveEdge
    {
        public int Cell { get; }
        public int Next { get; }

        public SolverMoveEdge(int cell, int next)
        {
            Cell = cell;
            Next = next;
        }
    }
}
