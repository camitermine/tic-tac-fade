# Tic-Tac-Fade v0.2.0 — Play against the AI

Tic-Tac-Fade can now be played solo: a new mode lets you face an AI
opponent built on a complete solution of the game.

## What's new

- **Play against the AI** with three levels: Fácil (Easy), Normal and
  Perfecta (Perfect). Every level reads a precomputed solution of the
  game; lower levels make deliberate mistakes.
- **The game is solved.** With perfect play, the first player wins in
  13 moves, but only by opening on an edge square. Corners and the
  centre — the best opening in classic tic-tac-toe — force nothing.
  The solver explores all 133,364 reachable positions, and an
  independent Python implementation reproduces every figure. See
  [docs/solver-results.md](https://github.com/camitermine/tic-tac-fade/blob/main/docs/solver-results.md).
- The Perfect AI also uses the match history: when losing it steers
  towards a draw by repetition or by the move cap, and when winning it
  avoids lines the opponent could escape by repetition.
- Rematches against the AI alternate who starts, as in every mode.
  When the Perfect AI starts, it cannot be beaten.

## How to install

Download `TicTacFade-v0.2.0.apk` on an Android device and open it. You
may need to allow installing apps from unknown sources. For online
play, both players need this same version.

## Known limitations

- Android only, portrait. Placeholder art and no sound yet.
- In-game text is in Spanish. Localization into English, Spanish and
  Portuguese is planned.
- No reconnection: if the connection drops during an online match,
  the match ends.

## How it was made

Developed iteratively with AI assistance, with human review at every
step. The full process is documented in
[`docs/ai-log.md`](https://github.com/camitermine/tic-tac-fade/blob/main/docs/ai-log.md), and architecture decisions in
[`docs/adr/`](https://github.com/camitermine/tic-tac-fade/tree/main/docs/adr).