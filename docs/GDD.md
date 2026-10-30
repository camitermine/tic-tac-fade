# GDD: Tic-Tac-Fade

**Version:** 0.14
**Status:** v0.2.0 — MVP plus play against the AI
**Date:** October 2026
**Source of truth:** this file (`docs/GDD.md`). Any external mirror (Google Docs) is secondary.

---

## 1. Version history

| Version | Date | Description |
| :-- | :-- | :-- |
| 0.1 | Sept 2026 | First draft: core gameplay, FIFO logic, turn loop and roadmap. |
| 0.2 | Sept 2026 | Moved to the repo. Draw rule (repetition + cap), turn timer with absent mode, general definition of "life", indicators for both players, legal-move invariant, MVP scope. |
| 0.3 | Sept 2026 | Final visual language for the life 1 indicator (opaque badge tinted by owner) and for the ghost piece (minimum-opacity mark on the player's own piece the FIFO would remove, different from the passive critical mark). |
| 0.4 | Sept 2026 | Visual direction for the art pass (§6): neon on a dark background, X cyan/O magenta, white shapes tinted in code with glow through URP Bloom, dissolve by shader + particles. Technical constraint: Bloom requires the Canvas in Screen Space - Camera, not Overlay. |
| 0.5 | Sept 2026 | Explicit rule for who starts a match started from the menu (§3.1). Full screen flow with MVP / later scope and the decision of "Crear sala" without a configuration screen (§4.3). |
| 0.6 | Sept 2026 | Online: host always X and client always O, rematch alternates who starts (§3.1). Moves confirmed by the host, match cut on a different hash, rematch requested by both and "El rival salió" (§5). |
| 0.7 | Sept 2026 | Abandonment by 3 timeouts confirmed; the automatic move never wins and the 3rd timeout doesn't play; online "Salir" is a loss (§3.5). Client disconnection with a 5 s grace, host drop and move without answer (§5). |
| 0.8 | Oct 2026 | Message for the opponent's different version (§5); the online match start goes through a Ready handshake. |
| 0.9 | Oct 2026 | Roadmap: "Room options" after the vs-AI mode (§8). Indicators yes/no (makes the "memory mode" of §4.1 concrete), turn time 10/20/30 s and move cap according to the solver; "Normal" and "Difícil" (Hard) as presets; the configuration is chosen by the host and travels to the client. |
| 0.10 | Oct 2026 | Solver solved (§8.1): with perfect play, whoever starts has a forced win in 13 moves (plies) if they open on an edge; corner or center don't force a win. 116,074 reachable positions; the cap of 40 doesn't cut any forced line from the start. Verified by an independent solver in Python (`tools/verify_solver.py`). |
| 0.11 | Oct 2026 | Vs-AI mode (§8.2): levels Fácil (Easy) / Normal / Perfecta (Perfect) on the solver table, evaluation with repetition history and cap, delay before playing, the human starts the first match and the rematch alternates. Flow: Menu → level choice → Match → Result (§4.3). |
| 0.12 | Oct 2026 | Documentation translated from Spanish to English, with no change of content; the game's text stays in Spanish. Roadmap: "Localization" item (§8). |
| 0.13 | Oct 2026 | Status: the game is v0.2.0, the MVP plus play against the AI (§8.2). The GDD version stays independent of the game version. |
| 0.14 | Oct 2026 | Art pass started (§6): neon palette, glow from URP Bloom through a Screen Space - Camera canvas and an HDR UI shader, applied to the board and pieces. |

---

## 2. Overview

### 2.1 Concept

*Tic-Tac-Fade* is a tactical reinterpretation of Tic-Tac-Toe. Each player has a limit of **3 active pieces** on a 3x3 board. When the 4th piece is placed, that player's oldest one disappears. The board never fills up, so there is no draw by blocking: the game becomes a duel of anticipation and control of space.

### 2.2 Key features

- **No draws by blocking.** The board never saturates. Draws only happen by repetition or by the move cap (see 3.4).
- **Reading the board.** Each move requires anticipating which cell will be freed, both your own and the opponent's.
- **Quick matches.** Goal: 1 to 2 minutes per match.
- **Modular base.** Parameterized rules (board size, buffer size, line length) to scale to 4x4, vs AI and roguelike modifiers.

### 2.3 Genre, platforms and audience

- **Genre:** turn-based strategy / competitive tactical puzzle.
- **MVP platform:** Android, distributed as an `.apk`, portrait orientation.
- **Future platforms:** Web (WebGL, for the portfolio and itch.io) and PC.
- **Audience:** players looking for short competitive matches with tactical depth.

---

## 3. Mechanics (core gameplay)

### 3.1 Board and turns

- **Board:** 3x3 in the MVP. Parameterizable (`BoardSize`).
- **Players:** 2 (Player 1: X, Player 2: O).
- **Turn:** alternating, one action per turn: place a piece on a free cell.
- **Who starts:** in the first match, the host (X). On each rematch who starts alternates.
  - **Match from the menu (v0.5):** a match started from the main menu is a first match: X starts. The alternation only applies to consecutive rematches; going back to the menu (from the result or leaving a match in progress) resets it.
  - **Online (v0.6):** the host is always X and the client always O. The room's first match starts with X; each rematch alternates who starts, without changing the symbols.

### 3.2 FIFO logic

Each player has a FIFO queue with a maximum size of `BufferSize` (3 in the MVP).

1. **Opening phase:** while the queue is not full, pieces are placed normally.
2. **Fading phase:** with the queue full, placing a new piece removes the active player's oldest one.
3. **Replacement restriction:** the new piece can't be placed on the cell occupied by the piece that is about to disappear. The cell has to be free **before** the move.

### 3.3 Win condition

- **Alignment:** whoever has `WinLength` pieces (3 in the MVP) aligned horizontally, vertically or diagonally wins.
- **Resolution order of a move:**
  1. Validate the move.
  2. Place the new piece.
  3. Apply FIFO (remove the oldest if the queue was exceeded).
  4. Evaluate the win.
  5. Evaluate the draw (3.4).
- Consequence: a line that depended on the just-removed piece does **not** count as a win.

### 3.4 Draws

The game can cycle indefinitely between two players who defend well. There are two cut-off rules:

- **Repetition:** if the same **position** appears for the 3rd time, the match ends in a draw.
  - A position is: both players' queues **with their order** + whose turn it is.
  - Two visually identical boards with queues in a different order are **different** positions, because the game's future is different.
- **Move cap:** on reaching `MaxTotalMoves` total moves (adding both players; starting value: 40), the match ends in a draw. It is a safety net.

### 3.5 Turn timer and absent mode (online)

- **Normal timer:** `TurnTime` = 30 s per turn.
- **Timeout:** if the timer reaches 0, a **random legal move** is played automatically for that player, and the turn passes to the opponent. This gives a player with connection problems some margin to come back.
  - **Automatic move (v0.7):** it is picked at random among the legal moves that **don't win** the match; if every legal move wins, any of them. Reason: an absent player must not be able to win without having played.
- **Absent mode:** from a timeout on, the player is marked as *absent* and their following turns last `AbsentTurnTime` = 10 s.
- **Return:** as soon as the absent player makes a move of their own, they stop being absent and go back to the normal timer. The player's own move also resets the count of consecutive timeouts.
- **Abandonment (confirmed, v0.7):** `MaxConsecutiveTimeouts` = 3 consecutive timeouts count as a loss by abandonment. The third timeout does **not** play an automatic move: the match ends directly.
- **"Salir" during an online match (v0.7):** it counts as a loss by abandonment for whoever leaves. The one who stays sees "Ganaste: el rival abandonó".
- **Authority:** the host keeps the timer and generates the random move. That move is transmitted like any other, so both clients stay in sync. The client only shows the countdown the host announces.
- **Configuration:** these parameters are not used by the board rules, so they live in the Game layer (not in the Core's `GameConfig`) and are edited in the same `GameConfigAsset`.

### 3.6 Invariant: there is always a legal move

With 3x3 and buffer 3 there are at most 6 occupied cells, so at least 3 are always free. **Any future rule or modifier must preserve this invariant.** If a variant breaks it (anchor pieces, buffer 4 in 3x3, etc.), what happens when a player has no legal move has to be defined first.

### 3.7 Controls

- **Mobile (touch):** the first tap selects a cell and shows the *ghost piece* (preview of the result). A second tap on the same cell confirms. Tapping another cell changes the selection.
  - **Result preview (v0.3):** the *ghost piece* doesn't show only the new piece. If the active player's queue is full, it also marks their own oldest piece (the one the FIFO would remove with that move) with a drop to minimum opacity. This mark is different from the passive critical mark of life 1 (§4.1): the latter can be visible on either player's piece at any time and doesn't identify which one goes with the move being previewed now.
- **PC (future):** hover previews and click confirms.

---

## 4. UI / UX

### 4.1 Piece life

**Definition:** `life = BufferSize − (number of pieces that player placed after this one)`.

- Life 1 = disappears on its owner's **next** move (when the queue is full).
- The definition also works in the opening phase and with buffers of another size.

Representation, applied to **both players'** pieces:

| Life | Appearance |
| :-- | :-- |
| 3 (new) | Full opacity, defined edges. |
| 2 | Medium opacity, slow pulse. |
| 1 (critical) | An "expiring" outline or icon **on top of** the transparency. Don't rely only on opacity, because on screens in the sun it can't be told apart. |

**MVP implementation (v0.3):** the life 1 icon is an opaque badge (not another transparency layer) in the corner of the piece, **tinted with the owner's color** — not a fixed color shared between players, because with both queues full there can be one badge of each player visible at the same time and they have to be told apart.

**MVP decision:** full visual aids on. A mode without aids ("memory mode") remains as a possible future modifier.

### 4.2 Match HUD

- Turn indicator, with who plays and who each one is.
- Turn timer, with a different visual state in absent mode.
- "Absent" indicator on the corresponding player.
- Winning line highlight.
- Result message: win, loss or draw, stating the reason (repetition, cap, abandonment, disconnection).

### 4.3 Screen flow

```
Main menu
  ├─ Jugar local       → Match (local) → Result → Revancha | Menú
  ├─ Jugar contra la IA → Level choice (Fácil, Normal, Perfecta, volver)
  │                         └─ level → Match (vs AI) → Result → Revancha | Menú
  ├─ Crear sala        → Waiting room (shows the code, copy button, cancel)
  │                         └─ when the opponent joins → Match (online) → Result → Revancha | Menú
  └─ Unirse con código → Code entry screen (text field, join, cancel)
                            └─ success → Waiting room → Match (online)
```

- Every screen has a back or cancel action that returns to the previous screen.
- **MVP:** menu, local match, result, create room, waiting room, join with code.
- **After the MVP:** room configuration (name and options), public rooms and room list, settings screen, profile, level and news.
- **Design decision:** "Crear sala" goes straight to the waiting room, with no intermediate configuration screen. The room name would only make sense with public rooms, which are outside the MVP.

---

## 5. Online (MVP)

- **Technology:** Unity Multiplayer Services (Sessions), with Relay and room codes. See `docs/adr/0002-online-multiplayer-services.md`.
- **Model:** host-client, 2 players per room. The host is authoritative: it validates the moves and keeps the timer.
- **Sync:** **moves** (`Move`) are transmitted, not the full board. Both clients apply the same Core rules.
  - **(v0.6)** Each move, the host's too, is validated by the host before being applied. Both devices apply only moves confirmed by the host, in the same order. An illegal move is rejected and changes nothing.
  - **(v0.6)** After each move both compare the position hash. If they don't match, the match is cut and both go back to the menu with the message "Se perdió la sincronización con el rival". Never keep playing out of sync.
- **Online result (v0.6):** the rematch requires both to ask for it; whoever asked sees "Esperando al rival...". If one picks "Menú" from the result, the session closes and the other goes back to the menu with "El rival salió".
- **"Salir" during the match (v0.7):** it is a loss by abandonment for whoever leaves (§3.5). Whoever leaves sees "Saliendo..." until the other device confirms it received the notice (at most 2 s) and goes back to the menu. The one who stays sees "Ganaste: el rival abandonó", with no rematch possible.
- **Disconnections (v0.7):** there is no reconnection in the MVP. Two cases are told apart:
  - **Opponent disconnected:** if the network reports that the **client** dropped, the host gives it a short 5 s grace in case it was an instant cut, and then the client loses by abandonment ("Ganaste: el rival abandonó"). The 3 timeouts are not waited for: nobody is coming back.
  - **Connected opponent who doesn't play:** this is the case of the timeouts and absent mode (§3.5), up to abandonment by 3 consecutive timeouts.
  - If the **host** drops, the client goes back to the menu with "Se perdió la conexión con el rival".
  - If a client move gets no answer from the host in 10 s, the input is unlocked and it is treated as a host drop.
  - The 5 s grace and the 10 s wait are network parameters of the Game layer, not game rules.
- **Different version (v0.8):** if the two devices have versions of the game that don't understand each other, neither starts the match and both go back to the menu with "El rival tiene otra versión del juego".

---

## 6. Audiovisual design (post-MVP)

- **Visual direction (decided):** neon on a dark background. X in cyan, O in magenta. The sprites are **white** shapes, tinted in code (the same pattern the placeholders already use: player color applied at runtime, never baked into the sprite). The glow is handled by **URP Bloom** on those white shapes, not a glow hand-painted in the texture.
  - **Palette (v0.14):** X cyan `#22E6F0`, O magenta `#FF3BD4`, background `#0B1026`. The grid glows cyan, like the X.
  - **Glow (implemented, v0.14):** the glow comes from URP Bloom, never from the sprite. The game Canvas renders in **Screen Space - Camera** (Bloom doesn't reach an Overlay canvas), and a UI shader lifts only the neon elements above 1 so a Bloom threshold of 1 leaves text and buttons out. Validated on the phone at 60 FPS. See `docs/adr/0006-neon-canvas-and-hdr-ui.md`.
  - **Art sources:** white SVGs with round caps in `Assets/_Project/Art/Neon/Source/`, imported as sprites; no glow and no color in the file. So far the board and pieces use them; the menu and HUD are next.
- **Animations:** the piece disintegrates with a **dissolve shader** plus particles when it fades; a luminous stroke on the winning line.
- **Planned SFX:** placing a piece, piece entering life 1, piece dissolving, win, timer warning.
- Placeholders are allowed in the MVP.

---

## 7. MVP scope

**In:**

- 3x3 rules with FIFO, win and draws, implemented in the Core and covered by tests.
- Local mode on one device.
- Online 1v1: create room, show the code, join with code.
- Turn timer with absent mode.
- Life indicators for both players and *ghost piece*.
- Highlighted winning line and result screen with rematch (alternating who starts).
- Disconnection handling.
- Minimal menu.
- Android build in portrait.

**Out:** final art, music, AI, roguelike, 4x4, user accounts, matchmaking, PC/Web.

---

## 8. Post-MVP roadmap

1. **Solver by retrograde analysis.** About 120 thousand states in 3x3 with buffer 3. It answers whether the game is a forced win for the first player or a draw with perfect play, and feeds the AI.
   - **Solved (2026-10-07):** with perfect play, **whoever starts has a forced win in 13 moves** (plies).
     - The forced win requires opening on an edge (1, 3, 5 or 7). Opening on a corner or the center, neither side can force it.
     - There are 116,074 reachable positions.
     - The cap of 40 doesn't cut any forced line from the start: the longest, from any reachable position, is 17.
     - Details in `docs/solver-results.md`; criterion in `docs/adr/0004-solver-assembly.md`.
2. **Vs-AI mode**, with difficulties based on the solver plus noise.
   - **Implemented (v0.11).** Technical criterion in `docs/adr/0005-ai-player.md`.
   - **Who plays what:** the human is X and the AI is O. The human starts the first match; each rematch alternates who starts, as in the rest of the game (§3.1). Going back to the menu resets the alternation.
   - **Levels:** Fácil, Normal and Perfecta. All of them start from the solver table.
     - Each level has an **error chance**: with that chance it plays a random legal move instead of the best one. Starting values: Fácil 50 %, Normal 20 %, Perfecta 0 %.
     - **Immediate win:** Normal and Perfecta always take it; Fácil can let it pass.
     - **Immediate loss:** in Normal and Perfecta, the random move of an error never leaves the opponent an immediate win if some move avoids it; Fácil can leave it open.
     - The four values of each level are tuned in the configuration asset, by playing.
   - **Best move:** the AI doesn't use only the table value; it corrects it with the real match.
     - A move that leads to a position that already appeared twice is a draw by repetition (§3.4).
     - A forced win or loss that doesn't arrive before `MaxTotalMoves` counts as a draw.
     - Order of preference: immediate win; forced win in which the opponent has no reply that draws by repetition; forced win with that reply; keep playing with no forced win for anyone; immediate draw; loss (the longest).
     - So, **when losing** it prefers any draw over the loss, and **when winning** it avoids the repetition. Between draws it prefers keeping the match alive, so the human can make a mistake.
     - The AI only looks at the opponent's next reply; nothing deeper is analysed.
   - **Pace:** the AI waits a short, variable time before playing (between 0.6 and 1.4 s, configurable). There is no turn timer, as in local.
   - **HUD:** "Tu turno" / "Turno de la IA". The result says "¡Ganaste!", "Ganó la IA" or the draw with its reason.
   - **Salir** during the match goes back to the menu with no result, as in local.
   - **AI unavailable:** if the AI data is missing or doesn't correspond to the game rules, "Jugar contra la IA" shows "La IA no está disponible en esta versión del juego" in the menu and doesn't move on.
3. **Room options.** Independent options the host picks when creating the room:
   - **Fade indicators:** yes / no.
   - **Turn time:** 10 / 20 / 30 s.
   - **Move cap:** values to be defined with the solver results (item 1).
   - **Presets:** "Normal" and "Difícil" are presets that configure those options, not modes with their own rules.
   - **No indicators** means hiding all the information about which piece goes. This makes concrete the "memory mode" planned in §4.1. Hidden:
     - the opacity by life;
     - the life 1 badge;
     - the *ghost piece* mark on the player's own piece;
     - the piece counters;
     - the fade warning.
   - **Sync:** the host picks and the configuration travels to the client in the protocol. The move cap is a Core rule, so it has to be identical on both sides.
4. **WebGL build** on itch.io.
5. **4x4 board** and `BufferSize` / `WinLength` variants.
6. **Art and audio.**
7. **Roguelike:**
   - Special pieces: heavy (last longer), bomb, anchor.
   - Relics and passives: variable buffer, altering the fade order, stealing turns.
   - PvE with bosses and cells with hazards.
   - Sudden death as an alternative to the draw.
8. **Localization:** English, Spanish and Portuguese through a localization asset (evaluate the official Unity Localization package). Meanwhile, the game's texts stay in Spanish.

---

## 9. Parameters (starting values)

| Parameter | Value | Note |
| :-- | :-- | :-- |
| `BoardSize` | 3 | NxN board. |
| `BufferSize` | 3 | Active pieces per player. |
| `WinLength` | 3 | Pieces in a line to win. |
| `MaxTotalMoves` | 40 | Cap on total moves. |
| `RepetitionLimit` | 3 | Occurrences of a position to declare a draw. |
| `TurnTime` | 30 s | Normal turn duration. |
| `AbsentTurnTime` | 10 s | Turn duration in absent mode. |
| `MaxConsecutiveTimeouts` | 3 | Consecutive timeouts for abandonment (confirmed in v0.7). |
