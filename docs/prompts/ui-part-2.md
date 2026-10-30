UI iteration, part 2: visual feedback on the board (GDD §4.1 and §3.7).

Life indicator on each piece, for both players' pieces. Life = BufferSize − pieces placed afterwards by that player. Life 3: full opacity. Life 2: medium opacity. Life 1: on top of the transparency, a visible mark (outline or icon) — the GDD is explicit about not relying only on opacity, because on a screen in the sun it can't be told apart.
Separate the fade warning from the counter: its own element, not concatenated into the same Text. It pays off the debt noted in entry [5] of the ai-log.
Ghost piece with two-tap confirmation: the first tap selects the cell and previews the resulting board (new piece placed and the piece that goes marked); a second tap on the same cell confirms; tapping another cell changes the selection.
Winning line highlight at the end of the match.

The Core is not touched: all the needed information is already in GameState and in the events. If you find that something is missing in the Core, stop and tell before adding it.

Verification: Simulator in 16:9 and 20:9, full match including a win, and a capture of the three life states visible at the same time.