Online, iteration 3: turn timer, absent mode, disconnections and abandonment (GDD §3.5 and §5).

Cami's decision: it is confirmed that 3 consecutive timeouts count as a loss by abandonment. Update the GDD removing the "to be confirmed".

Turn timer, online only:
The host owns the timer. At the start of each turn it tells the client the duration; the client shows the countdown locally, but the only truth is the host's.
Durations from GameConfig: 30 s normal, 10 s in absent mode.
On expiry, the host picks a random legal move for that player and broadcasts it like any confirmed move. The randomness lives in the Game layer with an injectable source, never in the Core.
Absent mode: after a timeout, that player goes to 10 s per turn until they make a move of their own.
3 consecutive timeouts: loss by abandonment.
Abandonment: "Salir" in the middle of an online match stops being just closing the session and starts counting as a loss for whoever leaves. The one who stays sees "Ganaste: el rival abandonó". Abandonment is a match result, not a board rule: if modelling it requires touching the Core, stop and propose first.
Disconnections:
If the client drops, the match goes on at the host and the client's turns expire normally until abandonment. No reconnection in the MVP.
If the host drops, the client goes back to the menu with "Se perdió la conexión con el rival".
Pending from iteration 2: if a client proposal gets no answer in a reasonable time, the input is unlocked and it is treated as a host drop.
HUD: remaining time visible online, with a different state in absent mode, and an "absent" indicator on the corresponding player.

Tests: inject the time source, so no test waits 30 real seconds. With the in-memory transport cover: timeout with a random legal move, entering and leaving absent mode, abandonment on the third timeout, abandonment by "Salir", host drop, client drop, and proposal without answer.

Manual verification on two devices, one on wifi and the other on mobile data: let a turn expire, play again and leave absent mode, abandon with "Salir", and close the client app mid-match to see how the host ends up winning by abandonment.

Documentation: ai-log, GDD §3.5 and §5, and ADR 0002 if the network model changes.