# Solver results (3x3, buffer 3)

Results of the retrograde analysis in GDD §8.1. The solver is `TicTacFade.Solver.GameSolver`, and the criterion and decisions are in `docs/adr/0004-solver-assembly.md`. The numbers are reproduced with the Editor menu `Tic-Tac-Fade/Solver/Solve MVP and report`.

- **Configuration:** MVP (`GameConfig.Mvp()`): 3x3 board, buffer 3, line of 3, cap of 40 moves, repetition 3. X starts.
- **Units:** distances are counted in **individual moves (plies)**, the same as `TotalMoves`. "Win in 13" means the match ends on the 13th move counting both players', and that the last one is made by the winner.
- **Date:** 2026-10-07.

## Answer

**With perfect play on both sides, whoever starts has a forced win in 13 moves**: their 7th piece, if the opponent delays as much as possible.

- The forced win requires starting on an **edge** (cells 1, 3, 5 or 7).
- Starting on a **corner or the center**, neither side can force the win.
- By symmetry, the result is the same when O starts (verified: `Win in 13`).

Cell numbering:

```
0 | 1 | 2
3 | 4 | 5
6 | 7 | 8
```

## Independent verification

`tools/verify_solver.py` is a solver written in Python from the GDD rules. It was written by Claude in the project's planning conversation (not the Claude Code agent that implemented the solver in C#), with no access to the C# code. It has no dependencies and takes about 2 s:

```
python3 tools/verify_solver.py
```

Run on 2026-10-07, **it reproduces exactly every number in this document**:
- 116,074 positions and 369,801 moves;
- `Win in 13` in the initial position;
- the result of the 9 openings;
- the Win / Loss / NoForcedWin distribution;
- the maximum distances (17 and 16) and the full distance histogram.

Two independent implementations in different languages agree, on top of the C# solver's EditMode tests (equivalence with `RulesEngine` on every position and consistency of the retrograde pass).

**With O starting (2026-10-29, AI iteration):** the script was extended to also explore the initial position with O starting, and the union of both graphs, which is what the AI table stores (ADR 0005). It prints:
- **133,364 positions in the union** (443,250 moves), the same ones `Config/AI/SolverTable.bytes` has;
- **`Win in 13` for both starting players;**
- that the union gives the same value as the X-only computation on every position they share.

Cami also verified it from the planning conversation, with the same numbers.

**Example of a forced line** (the script rebuilds it: the winner picks the shortest win and the loser the longest defense). It is 13 moves, with no repeated positions:

```
X1 O8 X4 O7 X6 O2 X0 O3 X8 O4 X5 O7 X2   ← X wins on the 13th move
```

## X's first move

| Cell | Type | Result for X |
| :-: | :-- | :-- |
| 0 | corner | No forced win |
| 1 | edge | Win in 13 |
| 2 | corner | No forced win |
| 3 | edge | Win in 13 |
| 4 | center | No forced win |
| 5 | edge | Win in 13 |
| 6 | corner | No forced win |
| 7 | edge | Win in 13 |
| 8 | corner | No forced win |

Counterintuitive compared with classic tic-tac-toe: the center, the best opening without FIFO, forces nothing here.

## Size and performance

Measured in the Editor (Unity 6, Mono, PC). Not yet measured on a device.

| Measure | Value |
| :-- | --: |
| Upper bound on the state space | 204,086 |
| Positions reachable from the start | **116,074** |
| Legal moves (edges, immediate wins included) | 369,801 |
| Total time (first run) | ~0.5 s |
| · forward exploration | ~0.46 s |
| · retrograde analysis | ~0.06 s |
| Managed memory retained by the full table (with the graph) | ~30 MB |

The figure of "about 120 thousand states" the GDD estimated was correct.

## Distribution of results

Over the 116,074 reachable positions, from the point of view of the player to move:

| Result | Positions | % |
| :-- | --: | --: |
| Forced win | 78,613 | 67.7 % |
| Forced loss | 24,268 | 20.9 % |
| No forced win | 13,193 | 11.4 % |

- Longest forced win: 17 moves.
- Longest forced loss: 16 moves.

Positions with a forced win or loss, by distance:

| Distance | Positions | | Distance | Positions |
| --: | --: | :-: | --: | --: |
| 1 | 38,736 | | 10 | 1,504 |
| 2 | 7,520 | | 11 | 1,604 |
| 3 | 14,896 | | 12 | 612 |
| 4 | 7,000 | | 13 | 553 |
| 5 | 13,592 | | 14 | 432 |
| 6 | 3,760 | | 15 | 600 |
| 7 | 6,120 | | 16 | 48 |
| 8 | 3,392 | | 17 | 16 |
| 9 | 2,496 | | | |

Odd distances are wins and even ones are losses, always for the player to move.

## Effect of the 40-move cap

- **From the start of the match, the cap doesn't cut any forced win.** The forced line from the initial position is 13 moves, and the longest from any reachable position is 17. Both are below 40.
- **Mid-match it can matter:** a forced win at distance `d` from a position with `t` moves already made only holds if `t + d ≤ 40`. With `d ≤ 17`, that only happens after move 23. The future AI has to take it into account (ADR 0004).
- **Repetition:** forced lines never repeat positions (the distance goes down on each move), so the repetition rule doesn't cut them. A position that already came up earlier in the real match can cut them (ADR 0004, note for the AI).

## Implications (to be decided, not decided)

- **The game is solved in favor of whoever starts.** With who starts alternating on the rematch (GDD §3.1), a series between two perfect players would always be won by whoever starts each match. Between humans, 13 moves of precision with disappearing pieces is not something played from memory. Still, it is worth keeping in mind for balance and for the design of the AI difficulties.
- **The 40 cap doesn't affect perfect play from the start.** For the "Room options" (GDD §8.3):
  - with a cap of 13 or more, the forced win from the initial position holds;
  - with a cap under 13, that win turns into a draw by cap;
  - mid-match, the effect depends on `t + d` (moves made plus distance).

  Choosing the cap values with data needs a separate analysis: a solver run with the cap as part of the state.
