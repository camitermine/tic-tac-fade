Menú y flujo de pantallas, con partida local completa.

Objetivo: que el juego deje de arrancar directo en el tablero y tenga el flujo del GDD §4.3: Menú principal → Partida → Resultado → (Revancha | Menú). Las opciones de sala online todavía no existen; el menú por ahora tiene solo "Jugar local" y queda preparado para sumarlas.

Máquina de estados de flujo en la capa Game, con los estados Menu, Playing y Result. Ninguna lógica de pantalla dentro de GameManager: él sigue ocupándose solo de la partida.
Pantalla de menú: título y botón "Jugar local". Preparada para dos botones más (crear sala, unirse con código) sin rehacer el layout.
Pantalla de resultado: quién ganó o si fue empate, con el motivo (línea, repetición o tope de jugadas), más botones "Revancha" y "Menú". Reemplaza al banner actual de fin de partida.
Revancha alternando quién empieza, según el GDD §3.1. La primera partida arranca X; cada revancha invierte.
Todo en vertical, respetando la safe area, con la misma disciplina de layout groups de la parte 1 de UI. Sin posiciones fijas en píxeles.

El Core no se toca. El scene builder tiene que seguir siendo idempotente y construir las pantallas nuevas.

Verificación: tests EditMode y PlayMode en verde, más un PlayMode test nuevo que recorra el flujo completo (menú → jugar → terminar la partida → revancha → verificar que el jugador inicial se invirtió). Simulator en los dos perfiles de siempre. Además, el HUD tiene que quedar verificado fuera del punch-hole en un device real, no solo en el Simulator — es justo la clase de bug que el Simulator no reproduce igual que el hardware (ver issue conocido en el ai-log).

Documentación: entrada en ai-log, y actualizar el GDD §4.3 si el flujo termina difiriendo de lo escrito.