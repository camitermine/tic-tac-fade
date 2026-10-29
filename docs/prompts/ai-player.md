Modo contra la IA (GDD §8.2), usando la tabla del solver (ADR 0004).

Decisiones de diseño (Cami):

Empieza el humano en la primera partida contra la IA; cada revancha alterna, como en el resto del juego (GDD §3.1).
Tres niveles: Fácil, Normal y Perfecta. Todos parten de la tabla del solver. Cada nivel tiene una probabilidad de error: con esa probabilidad elige una jugada legal al azar en vez de la mejor. Valores iniciales en el asset de configuración (por ejemplo, Fácil 50 %, Normal 20 %, Perfecta 0 %), para ajustar jugando. Normal y Perfecta siempre toman una victoria inmediata si existe; Fácil puede dejarla pasar.
La IA espera un tiempo corto y variable antes de jugar (alrededor de 1 s), configurable. Cero en los tests.

Fase 1 — la IA, sin UI:

Tabla precalculada como asset binario compacto (posición → resultado y distancia), generada desde el solver con un menú del Editor y referenciada por un campo serializado, no desde Resources. El cálculo nunca corre durante una partida (ADR 0004). Un test de categoría Slow regenera la tabla y la compara con el asset, para que no se desincronice de las reglas.
AIPlayer en la capa Game, implementa IPlayerController con IsLocalHuman = false. Actualizá en el CLAUDE.md la dirección de dependencias: Game → Solver → Core.
Evaluación efectiva de cada jugada, no solo el valor crudo de la tabla:
una jugada que lleva a una posición que ya apareció dos veces en la partida es empate inmediato por repetición;
una victoria que no llega antes de MaxTotalMoves vale empate, y una derrota que no llega antes del tope también;
perdiendo, la IA prefiere cualquier camino al empate (repetición o tope) antes que la derrota; ganando, evita los empates por repetición.
La demora no puede bloquear ni romper el bucle de turnos del GameManager: verificá que funciona con un controller que responde más tarde, y que una demora pendiente se cancela si la partida termina o el jugador sale.

Tests EditMode/PlayMode de la fase 1, con azar y reloj inyectados:

la IA nunca elige una jugada ilegal;
Perfecta contra Perfecta: gana quien empieza, en 13 jugadas;
Perfecta empezando contra un oponente al azar: gana siempre, en muchas partidas con semillas distintas;
perdiendo, Perfecta elige la repetición o el tope cuando están disponibles; ganando, evita la tercera repetición;
cada nivel aplica su probabilidad de error de forma determinística con el azar inyectado.

Pará al terminar la fase 1 y reportá antes de seguir.

Fase 2 — UI:

Menú: botón “Jugar contra la IA” → elección de nivel (tres botones) → partida. Estados nuevos al final del enum de flujo, con valores explícitos, y cada uno con su “volver” definido.
HUD: “Tu turno” / “Turno de la IA”. Sin timer, como en local.
Resultado con revancha (alternando quién empieza) y volver al menú.
PlayMode test del flujo completo: menú → nivel → partida → resultado → revancha.

Documentación: ADR 0005 (IA: evaluación efectiva, niveles, asset precalculado), GDD §8.2 con el diseño final, CLAUDE.md, ai-log.