# AI Iteration Log

Registro del proceso de desarrollo asistido por IA. Una entrada por iteración, la más reciente arriba.

El objetivo es que quede visible **qué se le pidió a la IA, qué produjo y qué decidió o corrigió la supervisora**.

## Plantilla

```markdown
## [N] YYYY-MM-DD — Título corto

**Objetivo:** qué se quería lograr.
**Pedido / prompt:** resumen del pedido (o link al plan aprobado).
**Qué hizo la IA:** archivos, tests y cambios principales.
**Revisión humana:** qué se aceptó, qué se corrigió y por qué.
**Verificación:** compila / tests / probado en device.
**Aprendizajes:** qué cambiaría en las instrucciones, skills o CLAUDE.md a partir de esto.
**Commit(s):** hash o PR.
```

---

## [1] 2026-09-22 — Verificación de la extracción de Core y fix de test

**Objetivo:** retomar el trabajo en curso de la rama `feat/core-extraction` (migración de `Assets/Scripts` a `Assets/_Project/Scripts` con separación en asmdefs `TicTacFade.Core`/`Game`/`UI`, más `RulesEngine`, `GameState`, `GameConfig`, eventos y tests de Core) y verificar que compila y que los tests pasan antes de seguir.

**Pedido / prompt:** continuación de sesión sin instrucción nueva; se interpretó como "verificar el estado actual del trabajo pendiente en la rama".

**Qué hizo la IA:**
- Revisó la consola de Unity vía MCP: 0 errores/warnings de compilación.
- Corrió los 33 tests EditMode de `TicTacFade.Core.Tests`: 1 falla en `RulesEngineConfigurableTests.Apply_NonDefaultConfig_FifoStillWorks` (esperaba `ActiveCount(X) == 4`, dio 3).
- Usó `execute_code` (MCP) para reproducir la secuencia de jugadas del test fuera del test runner y aisló la causa: las celdas de O en el test (`12,13,14,15`) forman una línea horizontal ganadora (fila 3 de un tablero 4x4, `WinLength=3`), así que la partida terminaba por victoria de O antes de que X llegara a su 4ª ficha; las jugadas siguientes quedaban rechazadas (`GameAlreadyEnded`) y la cola de X nunca crecía a 4. El bug estaba en los datos del test, no en `RulesEngine`/`WinChecker`.
- Corrigió `RulesEngineConfigurableTests.cs`: reemplazó las celdas de O por `2,4,11,13` (patrón tipo "4 reinas no atacantes", sin 3 en línea posible) y agregó un comentario explicando por qué. Verificó la nueva secuencia con `execute_code` antes de tocar el archivo de test.
- Volvió a compilar y correr la suite completa: 33/33 tests OK, 0 errores/warnings.

**Revisión humana:**
- Decisiones tomadas en modo plan (sesión anterior, previa a conectar el MCP de Unity):
  - Banner de fin de partida genérico, basado en el enum `GameEndReason` (no un mensaje distinto hardcodeado por cada caso).
  - No crear todavía el asmdef/carpeta `Net/`.
  - No mover `Assets/Scenes/` en esta tarea.
- Cambios pedidos por Cami sobre el plan propuesto por la IA:
  - `PositionKey` como clave exacta (packing de bits), en vez de un hash FNV.
  - `RulesEngine.IsLegal` público.
  - `InternalsVisibleTo` para que los tests puedan acceder a `PositionCounts` sin exponerlo como público.
  - Agregar tests de inmutabilidad del estado y de orden de eventos.
  - Commitear antes de regenerar la escena.
- Test que falló al verificar: `RulesEngineConfigurableTests.Apply_NonDefaultConfig_FifoStillWorks`. Causa raíz: las celdas de O de la secuencia del test (`12,13,14,15`) formaban una línea horizontal ganadora en un tablero 4x4 con `WinLength=3`, así que la partida terminaba por victoria de O antes de que la secuencia llegara a la 4ª ficha de X. Se modificó el test (celdas de O reemplazadas por `2,4,11,13`); no se modificó `RulesEngine.cs` ni `WinChecker.cs`.

**Verificación:** compila sin errores (consola Unity vía MCP) y los 33 tests EditMode de `TicTacFade.Core.Tests` pasan.

**Aprendizajes:** al escribir tests con `GameConfig` no-default (winLength distinto del boardSize), conviene elegir a mano las celdas del jugador "de relleno" para que no queden colineales, o agregar un helper de test que arme secuencias garantizadas sin línea ganadora (p. ej. patrón de N-reinas) para evitar este tipo de falso negativo.

**Commit(s):** — (sin commitear; Cami revisa y commitea).

**Pendiente:**
- Mover `Assets/Scenes/` a `Assets/_Project/Scenes/`.
- Actualizar Build Settings con la nueva ruta de las escenas.
- Actualizar el path hardcodeado en `TicTacFadeSceneBuilder.cs` (sigue apuntando a la ubicación vieja de la escena).

---

## [2] 2026-09-22 — Helper para endurecer tests de Core contra secuencias contaminadas

**Objetivo:** evitar que se repita el problema de la entrada [1] (una jugada de "relleno" que sin querer completa una línea o termina la partida, y arruina en silencio la posición que el test quería armar).

**Pedido / prompt:** Cami pidió (1) un helper que aplique una secuencia de jugadas y falle si alguna produce `MoveRejectedEvent` o si la partida termina antes de lo previsto, (2) usarlo en los tests que arman posiciones por secuencia, y (3) comentar en `RulesEngineConfigurableTests` por qué las celdas de O son `2,4,11,13`. Restricción explícita: no cambiar ninguna regla ni ninguna aserción existente que hoy pase.

**Qué hizo la IA:**
- Creó `Assets/_Project/Tests/EditMode/MoveSequence.cs` (clase interna `MoveSequence`, método `Apply(GameState, params Move[])`): aplica las jugadas en orden y usa `Assert.Fail` si alguna intermedia devuelve `MoveRejectedEvent`, o si `state.IsOver` se vuelve `true` antes de la última jugada de la secuencia (la última sí puede terminar la partida a propósito, para no romper tests que arman una posición y ganan en el mismo golpe).
- Lo usó en los tramos de "armado de posición" (secuencias de `RulesEngine.Apply` sin aserciones intercaladas, seguidas de un Act/Assert separado) de: `RulesEngineFifoTests` (2 tests), `RulesEngineWinTests` (2 tests), `RulesEngineMoveValidationTests` (2 tests) y `RulesEngineConfigurableTests` (2 tests).
- **No** lo aplicó donde la secuencia ya tenía aserciones intercaladas jugada por jugada (`RulesEngineImmutabilityAndEventsTests.Apply_EventOrder_...`, los tests de repetición de `RulesEngineDrawTests`, `GameInvariantTests.PlayLongMatch_...`, `RulesEngineFifoTests.GetLife_...`): colapsar esas secuencias en el helper habría significado borrar aserciones que hoy pasan, algo que Cami pidió explícitamente no hacer. Intentó una vez en `RulesEngineImmutabilityAndEventsTests` y lo revirtió al notar que quitaba 6 `Assert.IsFalse(state.IsOver)` existentes.
- Amplió el comentario en `RulesEngineConfigurableTests.Apply_NonDefaultConfig_FifoStillWorks` explicando el patrón de 4 reinas no atacantes de las celdas de O (2,4,11,13) y por qué hacía falta (referencia al bug de la entrada [1]).
- No tocó `RulesEngine.cs`, `WinChecker.cs` ni ninguna regla del Core.

**Revisión humana:** pendiente (a validar por Cami).

**Verificación:** compila sin errores/warnings (consola Unity vía MCP) y los 33 tests EditMode de `TicTacFade.Core.Tests` siguen pasando 33/33 después de los cambios.

**Aprendizajes:** cuando un test "arma una posición" con una secuencia de jugadas y el foco real está en la jugada final, conviene separar explícitamente el armado (que no debería poder terminar la partida ni ser rechazado) del Act — así una contaminación como la de la entrada [1] falla ruidosamente en el armado en lugar de producir una aserción confusa más adelante. Un test con datos mal elegidos puede disparar otra regla sin querer y fallar con un mensaje que no apunta a la causa; los tests que arman escenarios por secuencia de jugadas deben verificar primero su propia premisa.

**Commit(s):** — (sin commitear; Cami revisa y commitea).

---

## [3] 2026-09-22 — Todo el texto de los .cs en inglés

**Objetivo:** cumplir la convención de `CLAUDE.md` de que el código C# va en inglés, que hasta ahora no se había respetado (comentarios, XML docs y mensajes de `Assert.Fail`/log en español en toda la rama).

**Pedido / prompt:** Cami pidió corregir, antes del commit, todo el texto en español que quedó en archivos `.cs` de la rama (comentarios, XML docs, mensajes de `Assert.Fail`/log), sin tocar lógica ni nombres; precisar la línea de convenciones de `CLAUDE.md` para que sea explícita sobre qué texto va en inglés; y registrar en el ai-log por qué se precisó la regla.

**Qué hizo la IA:**
- Tradujo al inglés todo comentario, XML doc y mensaje de `Assert.Fail`/`Debug.Log`/excepción en los 30 archivos `.cs` de `Assets/_Project/` (Core, Game, UI, Editor y Tests), sin cambiar lógica ni identificadores.
- Dejó sin tocar el texto de UI que ve el jugador dentro del juego (`GameHud.cs`: "Turno: Jugador X", "Empate por repetición de posición", etc., y el label "Reintentar" del botón de reinicio en `TicTacFadeSceneBuilder.cs`): es contenido del producto, no texto de desarrollador, y la instrucción de Cami listaba específicamente comentarios/XML docs/mensajes de aserción y log, no texto de UI.
- Verificó con `grep` (acentos, ñ, signos ¿¡, y palabras clave en español) que no quedó texto fuera de esas strings de UI.
- Precisó la línea de convenciones de `CLAUDE.md`: "All text inside `.cs` files goes in English: identifiers, comments, XML docs, and assertion/log messages. Design documentation (`docs/`) goes in Spanish. Commit messages are in English."
- Recompiló y corrió la suite completa: 33/33 tests EditMode de `TicTacFade.Core.Tests` OK, 0 errores/warnings.

**Revisión humana:** pendiente (a validar por Cami).

**Verificación:** compila sin errores/warnings (consola Unity vía MCP) y los 33 tests EditMode de `TicTacFade.Core.Tests` pasan.

**Aprendizajes:** la regla de idioma estaba en `CLAUDE.md` pero era ambigua sobre XML docs y mensajes de aserción; se precisó tras detectarse la desviación en el review.

**Commit(s):** — (sin commitear; Cami revisa y commitea).

---

## [0] 2026-09-22 — Definición del MVP y setup de documentación

**Objetivo:** revisar el GDD v0.1, cerrar las decisiones pendientes y preparar el repo para el trabajo con agentes.

**Pedido / prompt:** análisis del GDD (qué está bien, qué falta, riesgos de gameplay a largo plazo) y propuesta de arquitectura y de setup de IA.

**Qué hizo la IA:**
- Detectó los problemas del documento:
  - loop infinito entre buenos jugadores;
  - posible ventaja del primer jugador;
  - la definición de "vida" solo servía con la cola llena;
  - faltaba el indicador de la ficha que vence del rival;
  - accesibilidad de los indicadores por opacidad;
  - faltaba el invariante de jugada legal.
- Propuso la arquitectura: Core en C# puro, `IPlayerController`, `Move` como comando y `GameConfig` parametrizable.
- Redactó el GDD v0.2, `CLAUDE.md` y los ADR 0001 y 0002.

**Revisión humana (decisiones de Cami):**
- Empate: repetición + tope de jugadas.
- Timeout: jugada aleatoria + modo ausente con timer reducido hasta que el jugador vuelve a jugar (inspirado en Hearthstone), para dar chance de reconexión.
- MVP con ayudas visuales completas.
- El GDD pasa al repo como fuente de verdad.

**Verificación:** N/A (documentación).

**Aprendizajes:**
- Cerrar las reglas ambiguas antes de escribir código evita que el agente las decida solo.
- La regla "si el código y el GDD difieren, preguntar" quedó en `CLAUDE.md`.

**Commit(s):** —
