Vs-AI mode (GDD §8.2), using the solver table (ADR 0004).

Design decisions (Cami):

The human starts the first match against the AI; each rematch alternates, as in the rest of the game (GDD §3.1).
Three levels: Fácil (Easy), Normal and Perfecta (Perfect). All of them start from the solver table. Each level has an error chance: with that chance it picks a random legal move instead of the best one. Starting values in the configuration asset (for example, Fácil 50 %, Normal 20 %, Perfecta 0 %), to be tuned by playing. Normal and Perfecta always take an immediate win if there is one; Fácil can let it pass.
The AI waits a short, variable time before playing (around 1 s), configurable. Zero in the tests.

Phase 1 — the AI, without UI:

Precalculated table as a compact binary asset (position → result and distance), generated from the solver with an Editor menu and referenced by a serialized field, not from Resources. The computation never runs during a match (ADR 0004). A Slow category test regenerates the table and compares it with the asset, so it doesn't get out of sync with the rules.
AIPlayer in the Game layer, implements IPlayerController with IsLocalHuman = false. Update the dependency direction in CLAUDE.md: Game → Solver → Core.
Effective evaluation of each move, not only the raw table value:
a move that leads to a position that already appeared twice in the match is an immediate draw by repetition;
a win that doesn't arrive before MaxTotalMoves counts as a draw, and so does a loss that doesn't arrive before the cap;
when losing, the AI prefers any path to a draw (repetition or cap) over the loss; when winning, it avoids draws by repetition.
The delay must not block or break the GameManager's turn loop: verify that it works with a controller that answers later, and that a pending delay is cancelled if the match ends or the player leaves.

Phase 1 EditMode/PlayMode tests, with injected randomness and clock:

the AI never picks an illegal move;
Perfecta against Perfecta: whoever starts wins, in 13 moves;
Perfecta starting against a random opponent: always wins, over many matches with different seeds;
when losing, Perfecta picks the repetition or the cap when they are available; when winning, it avoids the third repetition;
each level applies its error chance deterministically with the injected randomness.

Stop when phase 1 is done and report before continuing.

Phase 2 — UI:

Menu: “Jugar contra la IA” button → level choice (three buttons) → match. New states at the end of the flow enum, with explicit values, and each one with its “volver” defined.
HUD: “Tu turno” / “Turno de la IA”. No timer, as in local.
Result with rematch (alternating who starts) and back to the menu.
PlayMode test of the full flow: menu → level → match → result → rematch.

Documentation: ADR 0005 (AI: effective evaluation, levels, precalculated asset), GDD §8.2 with the final design, CLAUDE.md, ai-log.