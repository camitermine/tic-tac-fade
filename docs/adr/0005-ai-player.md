# ADR 0005 — IA: tabla precalculada, evaluación efectiva y niveles

**Estado:** Aceptada (fase 1: la IA; fase 2: la UI y el flujo).
**Fecha:** 2026-10-29

## Contexto

El roadmap (GDD §8.2) pide un modo contra la IA con dificultades "basadas en el solver más ruido". El solver (ADR 0004) da el valor de cada posición sin tener en cuenta el historial de la partida ni el tope de jugadas. Además, deja pendiente elegir entre calcular la tabla al arrancar o guardarla como asset.

## Decisión

### Tabla precalculada
- **Asset binario compacto** en `Assets/_Project/Config/AI/SolverTable.bytes`, que el juego carga desde un campo serializado (`TextAsset`), nunca desde `Resources`. **El solver nunca corre durante una partida.**
- **Generación:** menú `Tic-Tac-Fade/Solver/Generate AI table`, con las reglas del `GameConfigAsset` del proyecto.
- **Formato** (`CompactSolverTable`, en el assembly del solver):
  - header: magic, versión del formato, `BoardSize`, `BufferSize` y `WinLength`;
  - las claves de posición ordenadas (`ulong`);
  - un byte por posición: resultado (2 bits) y distancia (6 bits).
  - Las reglas de empate no se guardan, porque el solver las ignora.
- **Contiene las posiciones de los dos posibles iniciadores.** La revancha alterna quién empieza, y la fase inicial con O empezando no aparece en la tabla de X. Son 133.364 posiciones y 1,17 MB.
  - Guardar las claves como `uint` (alcanzan 25 bits en 3x3) la dejaría en unos 650 KB. No se hizo, para no atar el formato a 3x3.
- **Desincronización con las reglas:**
  - al cargar, si las reglas del tablero no coinciden, falla con un mensaje que pide regenerar;
  - un test de categoría Slow regenera la tabla y la compara byte por byte con el asset.
- **Dependencias:** `Game → Solver → Core`. El Core no cambió.

### Evaluación efectiva de cada jugada
La IA juega cada jugada legal con `RulesEngine.Apply` sobre el estado real de la partida. Así, el tercer paso por una posición y el tope aparecen como los empates que son, sin exponer el conteo de repeticiones del Core. Después busca la posición resultante en la tabla. El orden, de mejor a peor:

1. **Victoria inmediata.**
2. **Victoria forzada sin escape:** llega antes de `MaxTotalMoves` y ninguna respuesta del rival produce una tercera repetición. La más corta es mejor.
3. **Victoria forzada con escape:** el rival tiene una respuesta que empata por repetición.
4. **Partida viva (`NoForcedWin`):** ninguno fuerza la victoria, o la victoria o derrota forzada no llega antes del tope.
5. **Empate inmediato,** por repetición o por tope.
6. **Derrota forzada antes del tope.** La más larga es mejor.

Consecuencias del orden:
- **Perdiendo**, la IA prefiere cualquier empate antes que la derrota.
- **Ganando**, evita la tercera repetición, que pasa a valer empate.
- **Entre empates** (decisión de Cami), prefiere mantener viva la partida para que el humano pueda equivocarse. Toma el empate inmediato solo cuando la alternativa es perder.
- **Entre jugadas igual de buenas** elige al azar.
- **Profundidad:** el análisis de escape mira **una sola respuesta del rival**. Más profundo no se analiza. Una línea forzada puede cortarse más adelante por una repetición que no se ve a dos jugadas, y en ese caso la IA lo descubre cuando llega.

### Niveles
- **Fácil, Normal y Perfecta**, con probabilidad de error de 50 %, 20 % y 0 %, más dos flags. Los cuatro valores de cada nivel se editan en `GameConfigAsset`:
  - **tomar la victoria inmediata:** si existe, se juega sin tirar el error;
  - **bloquear la derrota inmediata:** la jugada al azar de un error nunca le deja al rival una victoria inmediata, si alguna jugada lo evita.
- Normal y Perfecta tienen los dos flags activados; Fácil, ninguno.
- **Con error**, la IA juega una jugada legal al azar, uniforme entre todas (puede coincidir con la mejor). Sin error, juega la mejor según la evaluación de arriba.
- **Orden de las tiradas de azar,** para que los tests lo controlen:
  1. ninguna, si toma una victoria inmediata;
  2. la tirada de error, `Next(100) < ErrorPercent`;
  3. la jugada al azar o el desempate, solo si hay más de una opción;
  4. la demora, solo si su rango no es un valor único.

### Demora y cancelación
- **La IA espera una demora al azar** entre un mínimo y un máximo (por defecto entre 0,6 y 1,4 s; 0 en los tests). Decide al empezar su turno y entrega la jugada en `Tick()` cuando se cumple la demora, con un `IClock` inyectado.
- **Con demora 0 juega sincrónicamente,** como cualquier controller autónomo. El bucle de turnos de `GameManager` ya soporta las dos formas.
- **`IPlayerController.NotifyMatchStopped()`** (decisión de Cami):
  - `GameManager` lo llama cuando la partida termina, se descarta, se detiene (`Halt`), arranca otra o se reemplazan los controllers;
  - la IA descarta ahí la jugada pendiente, y los demás controllers no hacen nada.
- **Segunda red:** `GameManager` ignora, con un warning, una jugada que llega sin partida en curso o con la partida terminada. Antes, una jugada así tiraba una `NullReferenceException`.

### Flujo y UI (fase 2)
- **Un solo estado nuevo, `AILevelSelect = 5`,** al final del enum, con "volver" al menú.
  - La partida y el resultado reutilizan `Playing` y `Result`, como hace el online. Lo que cambia es qué controllers enchufa `MatchFlow`, y los textos que la UI lee de `MatchFlow.IsVsAI`.
  - Dos estados más (`PlayingVsAI`, `ResultVsAI`) habrían duplicado la visibilidad de las pantallas y las transiciones sin agregar comportamiento.
- **`MatchFlow`:**
  - es dueño del `AIPlayer` y lo hace avanzar desde su `Update`, igual que con `OnlineMatch`;
  - carga la tabla una sola vez, la primera vez que se juega contra la IA, desde el `TextAsset` serializado;
  - la tabla se valida al tocar "Jugar contra la IA", antes de ofrecer los niveles. Si falta o no coincide con las reglas:
    - el detalle va a la consola;
    - el jugador ve "La IA no está disponible en esta versión del juego" en la línea de errores del menú;
    - el flujo se queda en el menú.
  - **Decisión de Cami:** nunca un botón que no hace nada. El error usa el mismo canal que los demás errores del menú (`LastFailure`), con un valor nuevo, `SessionFailure.AIUnavailable`. El nombre del enum quedó corto ("session"); renombrarlo es un cambio aparte.
- **Al volver al menú** (desde el resultado o con "Salir"), `MatchFlow` vuelve a poner los dos `LocalHumanPlayer`, igual que al cerrar una partida online. "Salir" descarta la partida, y eso cancela la jugada pendiente de la IA (`NotifyMatchStopped`).
- **La escena la genera el builder** (`Tic-Tac-Fade/Build Placeholder Scene (1v1)`), que suma:
  - el botón "Jugar contra la IA";
  - la pantalla `AILevelScreen`;
  - la referencia a la tabla en `MatchFlow`.

## Consecuencias

**A favor:**
- El Core no cambió y sigue sin conocer al solver ni a la IA.
- La tabla se carga en milisegundos y no depende de la plataforma (WebGL incluido).
- Perfecta gana siempre que empieza: contra sí misma en 13 jugadas, y contra un rival al azar en todas las partidas probadas.
- Los niveles son datos, no código: ajustarlos jugando no requiere tocar nada.

**En contra:**
- Hay que regenerar el asset cuando cambia una regla del tablero. El test Slow lo detecta si alguien se olvida.
- Evaluar una posición cuesta hasta 81 `Apply` (cada jugada más cada respuesta), con copia del diccionario de repeticiones. Es despreciable una vez por turno, pero hace lentos los tests que evalúan miles de posiciones.
- Los escenarios de repetición de los tests no son posiciones fijas: los encuentran partidas con semilla que buscan volver a posiciones ya vistas. Con el límite de 3, el caso de "victoria con escape" casi no aparece en esas partidas, así que su test usa `RepetitionLimit = 2`. Es válido porque la evaluación solo lee el `DrawByRepetition` del Core, nunca el límite.
