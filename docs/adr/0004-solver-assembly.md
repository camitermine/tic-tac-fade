# ADR 0004 — Solver in its own assembly and the "no forced win" criterion

**Status:** Accepted
**Date:** 2026-10-07

## Context

The roadmap (GDD §8.1) asks to solve the 3x3 game with buffer 3 by retrograde analysis: find out whether, with perfect play, the initial position is a forced win for whoever starts, for the second player, or for neither, and feed the AI (GDD §8.2). The solver needs the Core's exact rules, but the Core has no reason to know a solver exists. Also, the draw rules (repetition and move cap) depend on the match history, which is not part of a "position".

## Decision

### Assembly
- **`TicTacFade.Solver`**, pure C# with `noEngineReferences: true`, depending only on `TicTacFade.Core`.
  - The Core doesn't depend on the solver.
  - The Core was not modified: the solver uses the public API that already existed (`GameConfig`, `PositionKey.Compute`, `WinChecker`, `Occupant`).
- **Dependency direction:** `Solver → Core`. The future AI (`AIPlayer`, in Game) will depend on `Solver`.
- **Tests:** they go in the existing EditMode assembly (`TicTacFade.Core.Tests`, which adds the reference), without a new test assembly.
- **Report:** an Editor menu (`Tic-Tac-Fade/Solver/Solve MVP and report`, in `Scripts/Editor/SolverReport.cs`) measures and logs the numbers. The solver assembly can't log because it has no Unity.

### State space
- **Position:** both queues with their order (oldest first) plus the player to move. It includes the opening phase with fewer than `BufferSize` pieces. It is exactly what `PositionKey` identifies, which is used as the identity.
- **A winning move ends the match:** in the graph it is an edge to "won", not a node.
  - Only the mover can win, because a move adds a piece of its own and can only remove one of its own.
- **Move generation:** a mirror of `RulesEngine`:
  - free cell; the mover's own oldest piece still occupies its cell during the move, which is the replacement restriction;
  - then, the FIFO;
  - then, the win evaluated on the board already past the FIFO.
  - An EditMode test compares against `RulesEngine.IsLegal` and `RulesEngine.Apply` on **every** reachable position: legality per cell, immediate win and resulting position.
- **Configurations:** the solver accepts a generic `GameConfig`, but rejects with an `ArgumentException` and a clear message when the upper bound on the state space exceeds **5 million**.
  - The bound is the two queues as ordered sequences of distinct cells, up to `BufferSize` each, times the 2 players to move.
  - 3x3 with buffer 3 has a bound of 204,086; 4x4 with buffer 4, on the order of 10^9.
  - It also rejects configurations that don't fit in `PositionKey` (64 bits).

### Result and the "no forced win" criterion
- **Each position is worth `Win`, `Loss` or `NoForcedWin`** for the mover, with the distance in **individual moves (plies)**, the same units as `GameState.TotalMoves` and `MaxTotalMoves`.
  - The winner minimizes the distance and the loser maximizes it.
- **Retrograde:**
  1. A position with a winning move is `Win(1)`.
  2. A predecessor of a `Loss(d)` is `Win(d+1)`.
  3. A position whose moves all lead to `Win` is `Loss(1 + maximum)`.
  4. It is processed by increasing distance, with a FIFO queue and a counter of unresolved moves per position.
- **What is left unresolved at the end is `NoForcedWin`:** neither side can force the win, and with perfect play the game cycles and ends by repetition (or by the cap).
- **Why it holds even though the solver doesn't model repetition or the cap:**
  - **Repetition:** in a forced line (`Win`/`Loss` with optimal play) the distance strictly decreases on each move, so no position repeats within the line. Also, repetition only adds draws: it can't turn into a win something that wasn't one in the unbounded game.
  - **Cap:** a forced win at distance `d` from a position with `TotalMoves = t` only holds if `t + d ≤ MaxTotalMoves`. From the start, the longest forced line from any reachable position is 17 moves, against a cap of 40 (`docs/solver-results.md`). Mid-match it may not hold, and whoever uses the table has to take it into account (see below).

### For the AI iteration (noted, not implemented)
- **Repetition history:** the forced win assumes the path doesn't go through positions that already appeared earlier **in the match**. If a position on the path already came up twice, the third time is a draw (`RepetitionLimit = 3`), and the "forced" line is cut. When picking a move, the AI has to check the real match's repetition history (`GameState` keeps the count), not only the table value.
- **Cap mid-match:** the AI has to compare `TotalMoves + distance` against `MaxTotalMoves`.
- **Computing at startup vs. a precalculated asset.** The final decision is made in the AI iteration, with this criterion:
  - **The computation never runs during a match.**
  - **Measured in the editor (Mono, PC):** 116,074 positions, about 0.5 s (exploration 0.46 s + retrograde 0.06 s) and about 30 MB of managed memory retained by the full table with its graph. On a phone it can take several times longer.
  - **WebGL (roadmap §8):** there are no threads, so a computation of one or several seconds freezes the browser. There, computing at startup implies an explicit loading screen, and even then it is a visible wait.
  - **A compact precalculated table** (position key → result and distance in one byte, without the graph) takes on the order of 1 MB. It loads in milliseconds and doesn't depend on the platform. The risk is that it goes out of sync with the rules; it is mitigated by regenerating it from the same solver and with a test that compares the asset against a computation in EditMode.
  - **Leaning, with these numbers:** a compact precalculated asset for WebGL and mobile. Computing at runtime is acceptable only as an alternative in the editor or in the tests.

## Consequences

**For:**
- The Core didn't change and still doesn't know about the solver.
- The equivalence test guarantees that the solver and the game apply the same rules: if a Core rule changes, that test fails.
- The table answers in O(1) the value of any reachable position and of each of its moves, which is exactly what an AI with noise-based difficulties needs.

**Against:**
- **The solver duplicates the move generation logic** (free cell, FIFO, win) instead of calling `RulesEngine.Apply`. Calling it would force building a `GameState` per move, with the repetition dictionary, and would be much slower. The duplication is covered by the equivalence test.
- **The full table in memory weighs about 30 MB.** For the AI a compact representation will be convenient.
- **The equivalence test walks every position with `RulesEngine`** and takes about 20 s in EditMode.
