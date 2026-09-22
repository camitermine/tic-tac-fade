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

**Pendiente:** cerrado, ver entrada [4].

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

## [4] 2026-09-22 — Reorganización de Assets/Scenes/ y fix de escena stale

**Objetivo:** cerrar el pendiente de la entrada [1]: mover `Assets/Scenes/` a `Assets/_Project/Scenes/`, actualizar Build Settings y sacar el path hardcodeado de `TicTacFadeSceneBuilder.cs`.

**Pedido / prompt:** Cami pidió mover `Assets/Scenes/` (con sus `.meta`) a `Assets/_Project/Scenes/`, actualizar Build Settings, eliminar el path hardcodeado del builder (que las rutas de escena salgan de un único lugar, constante o Build Settings), y verificar que la escena abre, que el juego corre y que el builder sigue funcionando. Alcance cerrado: sin tocar scripts de gameplay ni UI. Sobre el plan propuesto, Cami pidió dos cambios: (1) `SampleScene.unity` (leftover del template, sin referencias, primera en el orden de build) no se migra: se borra junto con su entrada en Build Settings, que queda con una sola entrada; (2) verificar al final con `git status --porcelain` que la escena figura como rename y no como delete+add.

**Qué hizo la IA:**
- Movió `Assets/Scenes/TicTacFadeGame.unity` a `Assets/_Project/Scenes/TicTacFadeGame.unity` con `manage_asset(action="move")` (preserva GUID y `.meta` vía `AssetDatabase.MoveAsset`, no filesystem a mano).
- Borró `Assets/Scenes/SampleScene.unity` (+ `.meta`) con `manage_asset(action="delete")`, y borró la carpeta `Assets/Scenes/` ya vacía.
- Actualizó Build Settings a una sola entrada (`Assets/_Project/Scenes/TicTacFadeGame.unity`) con `manage_build(action="scenes")`.
- Actualizó `TicTacFadeSceneBuilder.cs` línea 18: `ScenePath` ahora apunta a `Assets/_Project/Scenes/TicTacFadeGame.unity` (sigue siendo la única constante de la que salen los 3 usos: `SaveScene`, el chequeo de Build Settings y el log).
- Al probar Play Mode encontró 2 `NullReferenceException` (`BoardView.cs:27`, `GameHud.cs:25`, ambas en `Awake` al hacer `gameManager.StateChanged += ...`). Diagnosticó la causa leyendo el YAML de la escena (solo lectura): la escena estaba **stale**, construida antes del refactor de Core — el `GameManager` serializado tenía `m_EditorClassIdentifier: TicTacFade.Gameplay.GameManager` con campos viejos `boardView`/`hud` que ya no existen en la clase actual (`TicTacFade.Game.GameManager`), y `BoardView`/`GameHud` no tenían el campo `gameManager` serializado en absoluto (solo lo tiene el `GameManager.cs` actual la relación inversa). No era un bug causado por el move: el contenido de la escena no se toca al moverla, así que ya estaba rota desde el 17/9, sin testear en Play mode hasta ahora.
- Re-ejecutó el builder (`Tic-Tac-Fade/Build Placeholder Scene (1v1)`), que regenera la escena desde las clases actuales y resolvió el problema: 0 errores en Play mode después.
- Verificó Build Settings después del re-run del builder: sigue en 1 sola entrada (no duplicó), confirmando que el chequeo `alreadyInBuild` funciona con la constante corregida.
- Corrió `git status --porcelain` (con `git add -A` para que se calcule similitud, después revertido con `git reset` para no dejar nada stageado): `TicTacFadeGame.unity`, su `.meta` y `Assets/Scenes.meta`→`Assets/_Project/Scenes.meta` figuran como **R** (rename); `SampleScene.unity` + `.meta` figuran como **D** (deleted). El `.meta` viajó junto al archivo, como corresponde.
- No tocó `GameManager.cs`, `BoardView.cs`, `CellView.cs`, `GameHud.cs` ni `RestartButton.cs`.
- Dejó sin tocar `ProjectSettings/ProjectSettings.asset` (`templateDefaultScene: Assets/Scenes/SampleScene.unity`): metadata inerte del template original, no la lee ni el juego ni el build, y no hay tool de MCP que exponga ese campo puntual (editar ese YAML a mano violaría la regla de CLAUDE.md).

**Revisión humana:** pendiente (a validar por Cami).

**Verificación:** escena carga sin errores (jerarquía con `EventSystem`/`Canvas`/`GameManager` intacta); Play mode sin excepciones tras regenerar la escena; builder recreó la escena en la ruta nueva sin duplicar la entrada de Build Settings; `git status --porcelain` confirma rename (no delete+add) en la escena movida.

**Aprendizajes:** una escena `.unity` puede quedar desincronizada en silencio de un refactor de scripts (los campos serializados viejos simplemente se pierden al deserializar bajo la clase nueva, sin error de compilación) — vale la pena probar Play mode después de un refactor de Core/Game/UI, no solo compilar y correr tests de EditMode. Mover un asset con `manage_asset(move)` no reescribe su contenido, así que un bug de este tipo sobrevive intacto al move y hay que diagnosticarlo aparte. El commit de la extracción del Core quedó con la escena serializada desactualizada (NullReference en Play mode), detectado recién en la iteración siguiente. El smoke test manual en Play Mode es obligatorio antes de cerrar cualquier tarea que toque tipos serializados, y regenerar la escena con el builder es parte de esa verificación.

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
