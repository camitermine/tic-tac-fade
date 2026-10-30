Online, iteration 2: playable match between two devices.

Goal: when the opponent connects to the room, the match starts on its own on both devices and is played to the result over the network, with rematch. Turn timer, disconnections mid-match and abandonment are left for iteration 3.

Authority model (non-negotiable, see ADR 0002):

The host is the authority. The client never applies its own move: it sends it to the host, the host validates it with RulesEngine.IsLegal and, if it is legal, broadcasts it to both.
On both devices, the GameManager only receives moves confirmed by the host. That way both apply exactly the same sequence.
The opponent is one more IPlayerController (RemotePlayer), which raises MoveChosen when a confirmed move arrives. The GameManager doesn't know there is a network.
After each move, both sides compare the Core's position hash. If they don't match: a clear error log, the match is cut and both go back to the menu with a message. Never keep playing out of sync.

Online game rules (GDD §3.1): the host is always X and the client always O. The first match starts with X; each rematch alternates who starts, without changing the symbols.

Move transport over Netcode for GameObjects, inside TicTacFade.Net. Evaluate RPCs on a NetworkObject versus named messages (CustomMessagingManager), which don't require spawning objects or registering prefabs; pick one and justify it in the plan. Moves (Move) travel over the network, never the board.
The board only accepts input when it is your turn. The HUD says "Tu turno" / "Turno del rival" online (local stays as it is). While the host hasn't confirmed a move, the cell can't be tapped twice.
Online result: the rematch requires both to ask for it; whoever asked sees "Esperando al rival...". If one picks "Menú", the session closes and the other goes back to the menu with the message "El rival salió".
The "Salir" button in the middle of an online match, for now, does the same: it closes the session and both go back to the menu. Counting it as a loss belongs to iteration 3.

Tests:

A test with no real network: two GameManagers connected by a fake in-memory transport, one as host and the other as client, playing a full match. Verify that at the end both states are identical, and that an illegal client move is rejected by the host without either of them changing state.
The existing tests stay green, including local mode unchanged.

Manual verification: with two real devices, full match until a win, rematch (O starts), draw by repetition if it can be forced, and "Menú" from the result on one of the two.

Documentation: ai-log, ADR 0002 with the transport decision, and GDD §5 if something in the network model changes.