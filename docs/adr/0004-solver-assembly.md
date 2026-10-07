# ADR 0004 — Solver en un assembly propio y criterio de "sin victoria forzada"

**Estado:** Aceptada
**Fecha:** 2026-10-07

## Contexto

El roadmap (GDD §8.1) pide resolver el juego 3x3 con buffer 3 por análisis retrógrado: saber si con juego perfecto la posición inicial es victoria forzada para quien empieza, para el segundo, o para ninguno, y alimentar la IA (GDD §8.2). El solver necesita las reglas exactas del Core, pero el Core no tiene por qué saber que existe un solver. Además, las reglas de empate (repetición y tope de jugadas) dependen del historial de la partida, que no forma parte de una "posición".

## Decisión

### Assembly
- **`TicTacFade.Solver`**, C# puro con `noEngineReferences: true`, que depende solo de `TicTacFade.Core`.
  - El Core no depende del solver.
  - El Core no se modificó: el solver usa la API pública que ya existía (`GameConfig`, `PositionKey.Compute`, `WinChecker`, `Occupant`).
- **Dirección de dependencias:** `Solver → Core`. La futura IA (`AIPlayer`, en Game) dependerá de `Solver`.
- **Tests:** van en el assembly de EditMode existente (`TicTacFade.Core.Tests`, que suma la referencia), sin un assembly de tests nuevo.
- **Informe:** un menú del Editor (`Tic-Tac-Fade/Solver/Solve MVP and report`, en `Scripts/Editor/SolverReport.cs`) mide y loguea los números. El assembly del solver no puede loguear porque no tiene Unity.

### Espacio de estados
- **Posición:** las dos colas con su orden (la más vieja primero) más el jugador de turno. Incluye la fase inicial con menos de `BufferSize` fichas. Es exactamente lo que identifica `PositionKey`, que se usa como identidad.
- **Una jugada ganadora termina la partida:** en el grafo es una arista hacia "ganó", no un nodo.
  - Solo puede ganar quien mueve, porque una jugada agrega una ficha propia y solo puede quitar una propia.
- **Generación de jugadas:** espejo de `RulesEngine`:
  - casilla libre; la ficha más vieja propia sigue ocupando su casilla durante la jugada, que es la restricción de reemplazo;
  - después, el FIFO;
  - después, la victoria evaluada sobre el tablero ya pasado por el FIFO.
  - Un test EditMode compara contra `RulesEngine.IsLegal` y `RulesEngine.Apply` en **todas** las posiciones alcanzables: legalidad por casilla, victoria inmediata y posición resultante.
- **Configuraciones:** el solver acepta un `GameConfig` genérico, pero rechaza con `ArgumentException` y un mensaje claro cuando la cota superior del espacio de estados supera **5 millones**.
  - La cota son las dos colas como secuencias ordenadas de casillas distintas, hasta `BufferSize` cada una, por los 2 jugadores de turno.
  - 3x3 con buffer 3 tiene una cota de 204.086; 4x4 con buffer 4, del orden de 10^9.
  - También rechaza configuraciones que no entran en `PositionKey` (64 bits).

### Resultado y criterio de "sin victoria forzada"
- **Cada posición vale `Win`, `Loss` o `NoForcedWin`** para quien mueve, con la distancia en **jugadas individuales (plies)**, las mismas unidades que `GameState.TotalMoves` y `MaxTotalMoves`.
  - El ganador minimiza la distancia y el perdedor la maximiza.
- **Retrógrado:**
  1. Una posición con jugada ganadora es `Win(1)`.
  2. Un predecesor de una `Loss(d)` es `Win(d+1)`.
  3. Una posición cuyas jugadas llevan todas a `Win` es `Loss(1 + máximo)`.
  4. Se procesa por distancia creciente, con una cola FIFO y un contador de jugadas sin resolver por posición.
- **Lo que queda sin resolver al final es `NoForcedWin`:** ninguno puede forzar la victoria, y con juego perfecto el juego cicla y termina por repetición (o por tope).
- **Por qué vale aunque el solver no modele la repetición ni el tope:**
  - **Repetición:** en una línea forzada (`Win`/`Loss` con juego óptimo) la distancia baja estrictamente en cada jugada, así que ninguna posición se repite dentro de la línea. Además, la repetición solo agrega empates: no puede convertir en victoria algo que en el juego sin límites no lo era.
  - **Tope:** una victoria forzada a distancia `d` desde una posición con `TotalMoves = t` solo se cumple si `t + d ≤ MaxTotalMoves`. Desde el inicio, la línea forzada más larga de cualquier posición alcanzable mide 17 jugadas, contra un tope de 40 (`docs/solver-results.md`). A mitad de partida puede no alcanzar, y lo tiene que tener en cuenta quien use la tabla (ver abajo).

### Para la iteración de la IA (anotado, no implementado)
- **Historial de repeticiones:** la victoria forzada asume que el camino no pasa por posiciones que ya aparecieron antes **en la partida**. Si una posición del camino ya salió dos veces, la tercera es empate (`RepetitionLimit = 3`), y la línea "forzada" se corta. Al elegir una jugada, la IA tiene que consultar el historial de repeticiones de la partida real (`GameState` lleva el conteo), no solo el valor de la tabla.
- **Tope a mitad de partida:** la IA tiene que comparar `TotalMoves + distancia` contra `MaxTotalMoves`.
- **Calcular al arrancar vs. asset precalculado.** La decisión final se toma en la iteración de la IA, con este criterio:
  - **El cálculo nunca corre durante una partida.**
  - **Medido en el editor (Mono, PC):** 116.074 posiciones, unos 0,5 s (exploración 0,46 s + retrógrado 0,06 s) y unos 30 MB de memoria administrada retenida por la tabla completa con su grafo. En un celular puede tardar varias veces más.
  - **WebGL (roadmap §8):** no hay hilos, así que un cálculo de uno o varios segundos congela el navegador. Ahí calcular al arrancar implica una pantalla de carga explícita, y aun así es una espera visible.
  - **Una tabla precalculada compacta** (clave de posición → resultado y distancia en un byte, sin el grafo) ocupa del orden de 1 MB. Se carga en milisegundos y no depende de la plataforma. El riesgo es que se desincronice con las reglas; se mitiga regenerándola desde el mismo solver y con un test que compare el asset contra un cálculo en EditMode.
  - **Inclinación con estos números:** un asset precalculado compacto para WebGL y móvil. Calcular en runtime es aceptable solo como alternativa en el editor o en los tests.

## Consecuencias

**A favor:**
- El Core no cambió y sigue sin saber del solver.
- El test de equivalencia garantiza que el solver y el juego aplican las mismas reglas: si cambia una regla del Core, ese test falla.
- La tabla responde en O(1) el valor de cualquier posición alcanzable y de cada una de sus jugadas, que es justo lo que necesita una IA con dificultades por ruido.

**En contra:**
- **El solver duplica la lógica de generación de jugadas** (casilla libre, FIFO, victoria) en vez de llamar a `RulesEngine.Apply`. Llamarlo obligaría a construir un `GameState` por jugada, con el diccionario de repeticiones, y sería mucho más lento. La duplicación queda cubierta por el test de equivalencia.
- **La tabla completa en memoria pesa unos 30 MB.** Para la IA convendrá una representación compacta.
- **El test de equivalencia recorre todas las posiciones con `RulesEngine`** y tarda unos 20 s en EditMode.
