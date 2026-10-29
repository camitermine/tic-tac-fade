namespace TicTacFade.Game
{
    /// <summary>One legal move and its effective value for the AI.</summary>
    public readonly struct AIMoveEvaluation
    {
        public int CellIndex { get; }
        public AIMoveCategory Category { get; }

        /// <summary>Moves until the forced result (Win, WinWithEscape, Loss; 1 for ImmediateWin); 0 otherwise.</summary>
        public int Distance { get; }

        /// <summary>After this move the opponent has a move that wins on the spot.</summary>
        public bool GivesOpponentImmediateWin { get; }

        /// <summary>The position after this move is not in the table (it's out of sync with the rules).</summary>
        public bool MissingFromTable { get; }

        public AIMoveEvaluation(int cellIndex, AIMoveCategory category, int distance, bool givesOpponentImmediateWin,
            bool missingFromTable = false)
        {
            CellIndex = cellIndex;
            Category = category;
            Distance = distance;
            GivesOpponentImmediateWin = givesOpponentImmediateWin;
            MissingFromTable = missingFromTable;
        }

        /// <summary>
        /// Positive when this move is better than <paramref name="other"/>:
        /// category first; then the shorter win or the longer loss.
        /// </summary>
        public int CompareTo(AIMoveEvaluation other)
        {
            if (Category != other.Category)
                return Category.CompareTo(other.Category);

            switch (Category)
            {
                case AIMoveCategory.Win:
                case AIMoveCategory.WinWithEscape:
                    return other.Distance.CompareTo(Distance);
                case AIMoveCategory.Loss:
                    return Distance.CompareTo(other.Distance);
                default:
                    return 0;
            }
        }

        public override string ToString() => $"cell {CellIndex}: {Category}" + (Distance > 0 ? $" in {Distance}" : "");
    }
}
