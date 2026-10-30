Menu and screen flow, with a complete local match.

Goal: the game stops starting straight on the board and gets the flow from GDD §4.3: Main menu → Match → Result → (Rematch | Menu). The online room options don't exist yet; for now the menu only has "Jugar local" and is ready to add them.

Flow state machine in the Game layer, with the states Menu, Playing and Result. No screen logic inside GameManager: it keeps dealing only with the match.
Menu screen: title and "Jugar local" button. Ready for two more buttons (create room, join with code) without redoing the layout.
Result screen: who won or whether it was a draw, with the reason (line, repetition or move cap), plus "Revancha" and "Menú" buttons. It replaces the current end-of-match banner.
Rematch alternating who starts, per GDD §3.1. The first match starts with X; each rematch swaps.
Everything in portrait, respecting the safe area, with the same layout-group discipline as UI part 1. No fixed pixel positions.

The Core is not touched. The scene builder must stay idempotent and build the new screens.

Verification: EditMode and PlayMode tests green, plus a new PlayMode test that walks the full flow (menu → play → finish the match → rematch → verify that the starting player was swapped). Simulator in the usual two profiles. Also, the HUD must be verified outside the punch-hole on a real device, not only in the Simulator — it is exactly the kind of bug the Simulator doesn't reproduce the same way as the hardware (see the known issue in the ai-log).

Documentation: ai-log entry, and update GDD §4.3 if the flow ends up differing from what is written.