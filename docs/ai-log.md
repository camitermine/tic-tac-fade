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

## [5] 2026-09-22 — UI parte 1: escena y HUD sanos (cámara, safe area, layout)

**Objetivo:** `docs/promps/ui-parte-1.md` — hacer idempotente el builder de escena, agregar una Main Camera, y rehacer el layout del HUD con anchors/layout groups + safe area (portrait, Canvas Scaler 1080x1920 match 0.5), verificado con el Simulator en 16:9 y 20:9. Fuera de alcance: indicadores de vida, ghost piece y línea ganadora (parte 2).

**Pedido / prompt:** Cami reportó HUD con textos superpuestos y una hipótesis propia: `TicTacFadeSceneBuilder` no sería idempotente y al re-ejecutarlo duplicaría objetos del HUD, dejando labels viejos sin actualizar encima de los vivos. Pidió verificar esa hipótesis por MCP antes de tocar nada. Aprobó el plan con 4 ajustes: (1) el lock de portrait no es opcional, lo pide GDD §2.3; (2) `SafeArea.Apply()` debía fijar también `offsetMin`/`offsetMax` a cero explícitamente; (3) agregar esta entrada de ai-log documentando el descarte de su hipótesis con evidencia; (4) anotar como deuda de parte 2 que el aviso de desvanecimiento sigue concatenado en el mismo `Text` que el contador.

**Qué hizo la IA:**
- **Verificó la hipótesis de duplicación (descartada con evidencia) antes de tocar nada:** inspeccionó la jerarquía viva por MCP (`get_hierarchy`, `find_gameobjects`) y el YAML de la escena guardada — exactamente 1 `TurnLabel`, 1 `CountX`, 1 `CountO`, sin huérfanos. Causa estructural: `BuildScene()` siempre arranca con `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)`, que borra la escena entera antes de reconstruir; no hay ningún camino de código que pueda duplicar objetos entre corridas. La causa real del "se pisan" era otra: `GameHud.cs` concatenaba el aviso de desvanecimiento dentro del mismo `Text` que el contador, en una caja fija sin overflow configurado.
- Agregó `CreateMainCamera()` a `TicTacFadeSceneBuilder.cs` (Camera + AudioListener + `UniversalAdditionalCameraData` para URP, tag `MainCamera`, `clearFlags=SolidColor`, fondo casi negro).
- Rehizo `CreateHud()`: `SafeAreaHUD` (usa el componente **built-in** `UnityEngine.UI.SafeArea` de Unity, no uno hecho a mano — se había escrito un `SafeArea.cs` propio primero, pero el compilador marcó ambigüedad de nombres contra `UnityEngine.UI.SafeArea` ya existente en el paquete, y al inspeccionarlo por reflection resultó ser una implementación más completa que la propia; se borró el script custom y se usó el de Unity) → `HUD` (`VerticalLayoutGroup` + `ContentSizeFitter` vertical) → `TurnLabel` + `CountsRow` (`HorizontalLayoutGroup`) → `CountX`/`CountO`, todo con `Wrap`/`Overflow` en vez de cajas fijas.
- Bloqueó orientación portrait vía `PlayerSettings` (API de Unity, no edición de YAML): `defaultInterfaceOrientation=Portrait`, autorotate a landscape/portrait-invertido deshabilitados. Es requerimiento de GDD §2.3, no una inferencia.
- Acortó el aviso de desvanecimiento en `GameHud.cs` de "¡Aviso de desvanecimiento!" a "¡Desvaneciendo!" (pedido de Cami tras ver el desborde en un dispositivo angosto) — reduce el ancho de texto necesario, no reemplaza el fix de layout.
- **Encontró y corrigió 2 bugs reales de layout anidado durante la verificación en el Simulator** (no estaban en el plan original, aparecieron al probar en `Wide Pill Cutout 1200x2640`, con captura de Cami mostrando el texto cortado y apilado verticalmente):
  1. `CountsRow` (un `HorizontalLayoutGroup`, hijo de `HUD`) no tenía `flexibleWidth` en su propio `LayoutElement`: al ser también un `ILayoutElement`, reportaba su propio ancho preferido (suma de sus hijos) al grupo externo en vez de expandirse a todo el ancho disponible, desbordando ~22px por lado. Fix: `flexibleWidth = 1f` en el `LayoutElement` de `CountsRow` (y de `TurnLabel`, por consistencia).
  2. `HUD` nunca fijó `sizeDelta` a `(0, alto)` al pasar a anchors de stretch: el `sizeDelta` residual de un `RectTransform` recién creado se sumaba como ancho extra por fuera de la safe area (desbordaba ~62px por lado). Fix: `hudRT.sizeDelta = new Vector2(0f, hudRT.sizeDelta.y);` explícito.
  - Ambos se diagnosticaron con mediciones exactas de `RectTransform.GetWorldCorners()` en Play Mode vía `execute_code`, no a ojo — la captura visual de Cami en `Wide Pill Cutout` fue la que disparó la investigación.
- Probó un primer intento de fix para el bug (1) usando `childControlWidth=false` + anchors de stretch manuales en vez de dejar que el `VerticalLayoutGroup` controle el ancho; no funcionó: Unity colapsa los anchors de los hijos a un punto en `SetLayoutHorizontal()` sin importar los flags de control, porque el layout group siempre controla la *posición* en ambos ejes aunque no controle el *tamaño*. Se revirtió ese intento (se borró el helper `StretchHorizontal` que quedó sin uso) y se aplicó el fix real (`flexibleWidth`).
- Verificó idempotencia corriendo el builder 3 veces en distintos momentos de la tarea (antes y después de los fixes de layout): siempre 1 sola instancia de cada GameObject, sin duplicados.

**Revisión humana:**
- Los 4 ajustes al plan (ver Pedido/prompt) ya están incorporados arriba.
- Cami verificó visualmente en el Simulator con `iOS Classic (750x1334)` y `Wide Pill Cutout (1200x2640)`, en el peor caso real (X=3/3, O=3/3, aviso visible en ambos simultáneamente) — confirmó "anda perfecto" después de los 2 fixes de layout.

**Verificación:** compila sin errores/warnings; builder idempotente (3 corridas, sin duplicados); Play mode sin excepciones; mediciones exactas de `RectTransform` en runtime confirman 0 overlaps y 0 desborde de la safe area en `Wide Pill Cutout (1200x2640)`; confirmación visual de Cami en `iOS Classic (750x1334)` y `Wide Pill Cutout (1200x2640)`.

**Aprendizajes:**
- Una hipótesis de bug puede estar equivocada y el pedido de "verificalo antes de tocar nada" es exactamente lo que evita perseguir la causa falsa (duplicación) en vez de la real (caja fija + overflow no configurado).
- Los layout groups de Unity anidados (`HorizontalLayoutGroup` dentro de `VerticalLayoutGroup`) necesitan `flexibleWidth` explícito en el `LayoutElement` del hijo-que-también-es-grupo, porque ese hijo reporta su propio tamaño preferido como cualquier `ILayoutElement` y puede pisar el intento de expansión del padre.
- Un `RectTransform` recién creado retiene su `sizeDelta` default al cambiarle los anchors a stretch por código; hay que fijarlo a `(0,0)` (o al valor que corresponda) explícitamente, si no el valor residual se filtra como tamaño extra.
- `UnityEngine.UI` ya trae un componente `SafeArea` propio (más completo que una implementación casera): conviene chequear con `unity_reflect` antes de escribir un componente que suena "estándar" — el compilador lo señaló solo (ambigüedad de nombres), pero convenía buscarlo antes de escribir el propio.
- El Simulator de Unity 6 viene integrado en el Editor (sin paquete) y ya trae perfiles genéricos con notch/punch-hole/pill-cutout suficientes para probar safe area sin instalar `com.unity.device-simulator.devices`.
- La captura de `manage_camera(action="screenshot")` no sirve para depurar UI `ScreenSpaceOverlay`: renderiza solo por cámara y excluye los canvases overlay. Medir `RectTransform.GetWorldCorners()` en runtime vía `execute_code` es más confiable que perseguir una captura de pantalla automatizada.

**Deuda conocida para la parte 2 (no resuelta acá, ver `GameHud.cs`):** el aviso de desvanecimiento sigue concatenado dentro del mismo `Text` que el contador (`UpdateActiveCounts`). Funciona con el wrap y el texto acortado, pero cuando se agreguen los indicadores de vida de las fichas debería pasar a ser su propio elemento de UI.

**Commit(s):** — (sin commitear; Cami revisa y commitea).

---

## [6] 2026-09-22 — UI parte 2: vida por ficha, ghost piece completo, aviso separado

**Objetivo:** `docs/prompts/ui-parte-2.md` — indicador de vida por ficha (GDD §4.1, ambos jugadores), separar el aviso de desvanecimiento del contador (cancela la deuda de la entrada [5]), ghost piece con confirmación en dos toques que previsualice el tablero resultante completo (GDD §3.7), y chequeo de regresión del resaltado de línea ganadora. Restricción explícita: no tocar Core.

**Confirmado antes de tocar nada:** toda la información necesaria ya era pública en `GameState`/`RulesEngine` (`GetLife`, `QueueX`/`QueueO`, `Config.BufferSize`, `GetActiveCount`, `IsLegal`, `WinningLine`) — no hizo falta ningún cambio en Core. El resaltado de línea ganadora ya estaba implementado end-to-end desde antes; esta tarea solo lo re-verificó.

**Corrección propia durante el plan, señalada por Cami:** la primera versión de mi plan asumía que la marca crítica de vida 1 ya alcanzaba para cubrir "la ficha que se va, marcada" del ghost piece, porque matemáticamente son la misma celda (la más vieja de una cola llena siempre tiene vida 1). Cami lo objetó con razón: esa marca aparece en las fichas de **ambos** jugadores a la vez si las dos colas están llenas, así que durante el preview no distingue cuál de los dos badges corresponde a la jugada actual. Se corrigió agregando `CellView.SetGhostVictim(bool)`, una marca aparte (opacidad al mínimo, 0.12) aplicada solo a la ficha propia del jugador activo que el FIFO eliminaría por la jugada previsualizada — independiente de la marca crítica pasiva. Verificado en Play mode: con ambas colas llenas, la ficha vieja del rival muestra el badge crítico pero **no** cae a opacidad mínima; solo la del jugador que previsualiza sí.

**Qué hizo la IA:**
- `CellView.cs`: nuevos campos `criticalMark` (badge opaco, teñido con el color del dueño — no rojo fijo, para que se distingan los badges de X y O) y `ghostOverlay` (ficha nueva translúcida del preview). Pulso lento en vida 2 vía `Update()` (alfa modulada con seno, sin tocar vida 1/3). `SetGhostVictim(bool)` nuevo (ver corrección arriba).
- `BoardView.cs`: estado de selección (`selectedCell`) y reescritura de `OnCellClicked` para el flujo de dos toques; `ClearGhostPreview()` limpia ghost + marca de víctima en las 9 celdas antes de cada nueva selección o al confirmar.
- `GameHud.cs`: `UpdateActiveCounts` deja de concatenar el aviso; nuevo campo `fadeWarningLabel` y `BuildFadeWarning()` arma un mensaje compartido ("¡X desvaneciendo!" / "¡O desvaneciendo!" / "¡X y O desvaneciendo!").
- `TicTacFadeSceneBuilder.cs`: `CreateCell` agrega `CriticalMark`/`GhostOverlay` (orden de hijos: Label → CriticalMark → GhostOverlay → Highlight, para que el borde de victoria quede siempre arriba). `CreateHud` agrega `FadeWarning` como tercer hijo de `HUD`; `CountsRow.minHeight` baja de 110 a 60 (ya no necesita lugar para 2 líneas en el mismo Text).
- **Test PlayMode nuevo** (pedido explícito de Cami, primera vez que este repo tiene uno): `Assets/_Project/Tests/PlayMode/` con su propio asmdef (`TicTacFade.PlayModeTests`, `defineConstraints: ["UNITY_INCLUDE_TESTS"]` para que no viaje en el build de Android) y `SceneWiringAndInputTests.cs` con 2 tests: (1) carga la escena y verifica por reflection que ninguna referencia serializada de `CellView`/`BoardView`/`GameHud` quedó sin asignar; (2) simula dos toques reales sobre el `Button` de una celda (`onClick.Invoke()`, no `GameManager.OnCellClicked` directo) y confirma que el primero no aplica la jugada y el segundo sí. Espera activamente a que `GameManager.CurrentState` exista (con timeout y mensaje claro) en vez de un `yield return null` fijo, para no dejar un test intermitente.
- Actualizó `docs/GDD.md` (v0.2 → v0.3): §4.1 documenta el badge de vida 1 teñido por dueño; §3.7 documenta que el preview marca también la ficha propia que el FIFO eliminaría, distinta de la marca crítica pasiva. Fila nueva en el historial de versiones.

**Revisión humana (dos rondas de ajustes sobre el plan, ambas incorporadas):**
- Ronda 1: la marca de ghost victim tiene que ser distinta de la marca crítica pasiva (ver corrección arriba); el badge de vida 1 tiene que ir teñido por dueño, no rojo fijo; agregar el test PlayMode descripto arriba.
- Ronda 2: sumar la actualización del GDD (hecha arriba); limpiar una frase del plan que había quedado desactualizada tras la ronda 1; cambiar el `yield return null` fijo del test por una espera activa con timeout.

**Verificación:**
- Compila sin errores/warnings.
- `run_tests(mode="PlayMode")`: 2/2 tests nuevos OK. `run_tests(mode="EditMode")`: 33/33 Core sin regresión.
- Play mode: secuencia de 6 jugadas confirmadas (dos toques cada una, vía `Button.onClick.Invoke()`, no `GameManager` directo) deja vida 1/2/3 visible en ambos jugadores a la vez; un toque más en una celda vacía muestra el ghost preview con la ficha nueva y, en simultáneo, la ficha vieja de X cae a opacidad mínima mientras la vieja de O (mismo momento, sin ser la jugada actual) se queda solo con el badge crítico normal — confirma que las dos marcas son independientes. Regresión de línea ganadora: partida corta forzando victoria de X, `highlightBorder` se prende en las 3 celdas y aparece el banner. 0 errores de consola en toda la sesión.
- **Confirmación visual en el Simulator (`iOS Classic`, `Wide Pill Cutout`): pendiente de Cami** — se le dejó el estado de peor caso armado en Play mode para que lo revise.

**Aprendizajes:** una lectura propia de "esto ya alcanza" puede estar matemáticamente bien y ser insuficiente en términos de UX/claridad — la corrección de Cami sobre el ghost piece es exactamente ese caso. Cuando el código termina divergiendo de una descripción implícita del GDD, corresponde actualizar el GDD explícitamente en vez de dejar la especificación real solo en el código (por eso se agregó la v0.3).

**Nota para el futuro, sin resolver ahora (pedido explícito de Cami):** `SceneWiringAndInputTests.AssertNoNullReferenceFields` asume que toda referencia serializada de `CellView`/`BoardView`/`GameHud` es obligatoria. El día que exista un campo legítimamente opcional, va a hacer falta una forma de excluirlo del chequeo (por ejemplo un atributo marcador o una lista de excepciones por nombre de campo).

**Commit(s):** — (sin commitear; Cami revisa y commitea).

---

## [7] 2026-09-22 — Configuración de PlayerSettings para el primer build de Android

**Objetivo:** dejar el proyecto configurado para el primer build de Android vía la API de `PlayerSettings` (nunca editando YAML a mano), en un método de Editor reproducible bajo el menú Tic-Tac-Fade — no builda nada, eso lo corre Cami.

**Pedido / prompt:** Company name y product name reales; package `com.<compañía>.tictacfade` en minúsculas; versión "0.1.0", bundleVersionCode 1; IL2CPP + ARM64; API mínima 23 o superior; confirmar orientación portrait y una sola escena en Build Settings; todo reproducible en un método de menú; documentar en el ai-log; no correr la build.

**Qué hizo la IA:**
- Antes de tocar nada, buscó si "company name" ya estaba establecido en algún lado de la documentación del repo (por la carpeta raíz `E:\HeuMila\`); no encontró ninguna referencia en `docs/`/`CLAUDE.md`, así que **preguntó** en vez de asumir. Cami confirmó `HeuMila` y `Tic-Tac-Fade` (con guiones, como en el GDD) para product name.
- Revisó con `unity_reflect` la API real de `PlayerSettings` antes de escribir código: `AndroidApiLevel23` (y todo el rango 16–25) está **obsoleto** en esta versión de Unity (6000.6.1f1). Usó `AndroidApiLevel26`, el nivel no-obsoleto más bajo que igual cumple "23 o superior" — lo señala explícitamente en un comentario y en el log de la propia herramienta, para que no sea una sorpresa.
- Creó `Assets/_Project/Scripts/Editor/AndroidBuildConfigurator.cs`, menú `Tic-Tac-Fade/Configure Android Player Settings`, método `ConfigureForAndroid()`: `companyName`/`productName`, `SetApplicationIdentifier` (`com.heumila.tictacfade`), `bundleVersion`/`bundleVersionCode`, `SetScriptingBackend(IL2CPP)`, `Android.targetArchitectures = ARM64`, `Android.minSdkVersion`, re-aserta el lock de portrait (GDD §2.3) en vez de depender de que quedara de una sesión anterior, y loguea un warning si `EditorBuildSettings.scenes.Length != 1` en vez de tocarlo solo (no hay forma segura de "arreglar" eso sin asumir cuál escena querés).
- **No tocó nada de keystore/firma** — no estaba pedido, y generar o tocar un keystore es una acción sensible (credenciales) que no corresponde asumir.
- Ejecutó el menú dos veces (confirma que es idempotente) y verificó cada valor final por separado vía `execute_code`, no solo el log de la propia herramienta.

**Revisión humana:** pendiente (a validar por Cami).

**Verificación:** compila sin errores/warnings. Ejecutado 2 veces sin error ni cambio de resultado. Valores confirmados uno por uno: `companyName=HeuMila`, `productName=Tic-Tac-Fade`, `applicationIdentifier=com.heumila.tictacfade`, `bundleVersion=0.1.0`, `bundleVersionCode=1`, `scriptingBackend=IL2CPP`, `targetArchitectures=ARM64`, `minSdkVersion=AndroidApiLevel26`, `defaultInterfaceOrientation=Portrait`, autorotate a landscape/portrait-invertido deshabilitados, 1 sola escena en Build Settings (`TicTacFadeGame.unity`, enabled). No se corrió ninguna build.

**Aprendizajes:** un valor pedido textualmente ("API 23") puede ya no ser válido en la versión de Unity/Editor actual (deprecado); vale la pena chequear con `unity_reflect` antes de fijar un enum a mano, y dejar documentado por qué el valor final difiere del pedido literal en vez de aplicarlo a ciegas o cambiarlo en silencio.

**Commit(s):** — (sin commitear; Cami revisa y commitea).

---

## [8] 2026-09-22 — Auditoría de efectos secundarios de la config de Android (entrada [7])

**Objetivo:** Cami revisó los cambios sin commitear después de la entrada [7] y encontró dos cosas que `AndroidBuildConfigurator` no explicaba a simple vista: una carpeta `Assets/Resources/` nueva, y cambios en `Assets/Settings/UniversalRP.asset` / `UniversalRenderPipelineGlobalSettings.asset`. Pidió diagnosticar ambas antes de asumir que eran intencionales.

**Pedido / prompt:** primera pregunta — qué creó `Resources/`, qué hay adentro, si fue `AndroidBuildConfigurator`, y si los cambios de URP eran intencionales o un toque accidental de Unity al cambiar de plataforma. Mi primera respuesta propuso como causa un cambio de plataforma activa; Cami corrigió que la plataforma ya era Android desde antes de esta sesión (confirmado por capturas previas), y pidió específicamente revisar si los cambios de Player Settings del configurador (IL2CPP, ARM64, graphics API) eran el disparador real, y corregir el diagnóstico acá si correspondía. En una tercera vuelta, pidió: (1) verificar si el asset `InputSystem_Actions.inputactions` referenciado en `preloadedAssets` todavía existe o es una referencia colgada de un asset borrado, y sacarlo vía `PlayerSettings.SetPreloadedAssets` si corresponde; (2) cerrar el tema URP sin seguir investigando — registrar causa no determinada con los descartes documentados, tratarlo como re-serialización interna benigna.

**Qué hizo la IA:**
- `Assets/Resources/`: identificado por contenido (`PerformanceTestRunInfo.json`, `PerformanceTestRunSettings.json`) y por el paquete `com.unity.test-framework.performance` (confirmado en `packages-lock.json`) como un artefacto automático de Unity al correr tests PlayMode — no algo que `AndroidBuildConfigurator` cree (el script no tiene una sola línea que escriba a disco fuera de `PlayerSettings`/`EditorBuildSettings`). Cami ya lo borró y agregó la regla al `.gitignore`.
- URP — primera hipótesis (cambio de plataforma activa) **descartada**: Cami confirmó que la plataforma activa ya era Android antes de esta sesión.
- Segunda hipótesis pedida (IL2CPP/ARM64/graphics API) **descartada con evidencia**, no solo por lectura del script: comparé el diff real de `ProjectSettings/ProjectSettings.asset` campo por campo contra los valores actuales. `scriptingBackend`, `AndroidTargetArchitectures` y `AndroidMinSdkVersion` **no aparecen en el diff en absoluto** — ya tenían esos valores exactos (IL2CPP, ARM64, 26) antes de que corriera el configurador, así que no pudieron dispararlo. `m_BuildTargetGraphicsAPIs` sigue en `[]` (Auto), sin tocar. Lo único genuinamente nuevo del lado Android fue `applicationIdentifier.Android` (antes no existía esa entrada, solo `Standalone`).
- `preloadedAssets` (`InputSystem_Actions.inputactions`) — investigado a fondo en la tercera vuelta: el asset **existe** en disco (`Assets/Settings/InputSystem_Actions.inputactions`, GUID coincide, el `fileID` referenciado resuelve a un `InputActionAsset` real dentro del archivo, no es una referencia rota a nivel de asset). Pero **no es el que usa** `InputSystemUIInputModule` en la escena: ese componente, agregado sin asignarle explícitamente ningún `.inputactions` (`TicTacFadeSceneBuilder` solo hace `AddComponent<InputSystemUIInputModule>()`), termina usando un objeto autogenerado en runtime llamado `DefaultInputActions` — confirmado leyendo `inputModule.actionsAsset.name` con la escena abierta. Además, `PlayerSettings.GetPreloadedAssets()` en memoria ya devolvía 0 elementos pese a que el YAML en disco todavía tenía la entrada — un desincronismo real entre el estado vivo del Editor y lo persistido. No cumplía la condición que había puesto Cami para dejarlo ("si es el que usa el InputSystemUIInputModule"), así que se sacó vía `PlayerSettings.SetPreloadedAssets(new Object[0])` + `AssetDatabase.SaveAssets()` (API, no edición de YAML). Verificado después: `preloadedAssets: []` en disco.
- URP — **causa no determinada, tema cerrado por pedido explícito de Cami.** Tres hipótesis evaluadas, ninguna confirmada:
  1. Cambio de plataforma activa — descartada (la plataforma ya era Android desde antes de esta sesión).
  2. Cambios de Player Settings del configurador de Android (IL2CPP/ARM64/graphics API) — descartada con evidencia (esos campos no aparecen en el diff de `ProjectSettings.asset`, ya tenían esos valores antes de correr `AndroidBuildConfigurator`).
  3. La `Main Camera`/`UniversalAdditionalCameraData` agregada en la parte 1, recalculada en algún domain reload posterior — hipótesis con algo de sustento circunstancial (timestamps, contenido URP genuinamente nuevo) pero **no demostrable**, Unity no deja un log de esta recalculación interna.
  El diff de `UniversalRP.asset`/`UniversalRenderPipelineGlobalSettings.asset` se trata como re-serialización interna de prefiltering de shaders, benigna — no bloquea nada, no se sigue investigando.

**Revisión humana:** Cami corrigió mi primer diagnóstico (plataforma activa) con evidencia propia (capturas previas a la sesión) antes de que yo lo verificara con datos; eso llevó a la segunda vuelta, donde sí verifiqué con el diff real en vez de repetir una hipótesis plausible pero no chequeada. En la tercera vuelta, marcó el límite explícito de no seguir investigando URP con una causa no demostrable, y pidió resolver `preloadedAssets` con una regla concreta (existe + lo usa el módulo → dejarlo; si no → sacarlo por API).

**Verificación:** diff completo de `ProjectSettings/ProjectSettings.asset` línea por línea; grep de los campos puntuales pedidos contra el archivo actual vs. el diff; comparación de timestamps de archivo entre `AndroidBuildConfigurator.cs` y los dos `.asset` de URP (consistente con la hipótesis 3, no la prueba de forma concluyente); para `preloadedAssets`: `AssetDatabase.LoadAllAssetsAtPath` confirmó que el fileID referenciado resuelve a un asset real, `InputSystemUIInputModule.actionsAsset.name` confirmó que el módulo usa un objeto distinto, `GetPreloadedAssets()` confirmó el desincronismo memoria/disco, y una relectura del YAML después del fix confirmó `preloadedAssets: []`.

**Aprendizajes:** `git diff` contra el último commit mezcla los cambios de toda la sesión (varias tareas encima de una rama sin commitear), no solo los de la tarea puntual que se está reportando — antes de atribuir un cambio de asset a un script específico hay que confirmar que el campo relevante realmente cambió de valor en el diff, no solo que el archivo aparece modificado. Mi primera respuesta asumió una causa plausible (cambio de plataforma) sin verificarla contra el estado previo real; el diagnóstico correcto salió de comparar valores campo por campo, no de una explicación que sonaba razonable. Un asset puede existir en disco y aun así no ser el que un componente realmente usa en runtime — "¿el archivo existe?" y "¿está en uso?" son preguntas distintas, y solo la segunda importa para decidir si una entrada de `preloadedAssets` es correcta. No toda causa raíz es demostrable con las herramientas disponibles; cuando no lo es, hay que decirlo explícitamente en vez de presentar la hipótesis más plausible como si fuera la conclusión.

**Commit(s):** — (sin commitear; Cami revisa y commitea).

---

## [9] 2026-09-23 — Dirección visual en el GDD + sprites fuente sin cablear

**Objetivo:** dejar registrada en el GDD la dirección visual decidida para el pase de arte, y anotar que los sprites fuente ya están en el repo pero todavía no están conectados a ningún componente.

**Pedido / prompt:** Cami pidió agregar al "GDD §5" la dirección visual (neón sobre fondo oscuro, X cian/O magenta, formas blancas tintadas por código, glow por Bloom de URP nunca horneado en el sprite, desintegración por shader de disolución + partículas), la restricción técnica de que Bloom no afecta a un Canvas Screen Space - Overlay, y una fila nueva en el historial de versiones. También pidió anotar en el ai-log que los sprites fuente (`piece_x`, `piece_o`, `shard`, `cell_frame`, `badge`) están en el repo como material para el pase de arte pero `CellView` sigue con los labels de texto hasta esa iteración.

**Qué hizo la IA:**
- Detectó un desajuste antes de escribir: el §5 actual del GDD es "Online (MVP)" (networking), no dirección visual — ese contenido corresponde a §6 "Diseño audiovisual (post-MVP)", que ya tenía una entrada abierta ("Arte: minimalista de alto contraste. Candidatos: neón cyberpunk o flat vector") esperando justamente esta decisión. Lo señaló y lo puso en §6 en vez de asumir en silencio o escribirlo en el lugar equivocado.
- Actualizó `docs/GDD.md` §6: reemplazó los "candidatos" de arte por la dirección decidida (neón, cian/magenta, formas blancas tintadas por código, Bloom de URP), agregó la restricción técnica del Canvas (Screen Space - Overlay actual no recibe Bloom; el pase de arte va a necesitar pasar a Screen Space - Camera, cambio de arquitectura de UI, no cosmético) y precisó la desintegración como shader de disolución + partículas (antes solo decía "partículas"). v0.3 → v0.4, fila nueva en el historial.
- Verificó que los 5 sprites fuente mencionados realmente están en el repo antes de anotarlo (no tomó la palabra de Cami sin chequear): `Assets/_Project/Art/Source/{piece_x,piece_o,shard,cell_frame,badge}.svg` + sus export `@2x.png`, los 5 presentes.
- Confirmó que `CellView.cs` sigue sin cablear ningún sprite — sigue usando `label.text = "X"/"O"` sobre un `Image` de color plano (`XColor`/`OColor`), sin ninguna referencia a los sprites de `Art/Source/`. El cableado queda para una iteración futura de arte, que además requiere el cambio de Canvas a Screen Space - Camera documentado arriba.

**Revisión humana:** pendiente (a validar por Cami) — en particular, confirmar que §6 era el lugar correcto (no §5 como se pidió textualmente).

**Verificación:** lectura directa del GDD post-edición; listado de archivos en `Assets/_Project/Art/Source/` confirmando los 5 sprites; lectura de `CellView.cs` confirmando que no referencia ningún sprite todavía.

**Aprendizajes:** cuando un pedido especifica un número de sección que no coincide con el contenido real del documento, conviene señalar el desajuste y ubicar el contenido donde semánticamente corresponde, en vez de crear una sección nueva o forzarlo en el lugar pedido — el GDD ya tenía un "candidato" abierto en §6 esperando exactamente esta decisión.

**Commit(s):** — (sin commitear; Cami revisa y commitea).

---

## [10] 2026-09-23 — IPlayerController + GameConfig como ScriptableObject

**Objetivo:** `docs/prompts/player-controllers.md` — implementar dos reglas de arquitectura ya declaradas pero no construidas: CLAUDE.md regla 5 ("Players are interchangeable through IPlayerController...") y ADR 0001 ("La capa Game expone GameConfig como ScriptableObject"). `GameManager` deja de asumir que toda jugada viene de la UI y de llamar `GameConfig.Mvp()` hardcodeado.

**Pedido / prompt:** crear `IPlayerController` + `LocalHumanPlayer`, que `GameManager` reciba dos controllers sin ramificar por tipo; `GameConfig` como ScriptableObject (wrapper, Core sin tocar); scene builder crea/wirea la config nueva, idempotente; UI se adapta sin cambios visuales; tests EditMode/PlayMode existentes siguen pasando + partida local igual que antes + test nuevo con un controller no-humano; ai-log y ADR si corresponde.

**Qué hizo la IA:**
- Confirmó antes de escribir código que no hacía falta tocar Core: todo lo necesario ya era público (`Occupant`, `Move`, `GameState.GetFreeCellIndices()`).
- Creó `IPlayerController.cs` (`Player`, evento `MoveChosen`, `NotifyTurnStarted`, `NotifyCellSelected`), `LocalHumanPlayer.cs` (espera pasivamente el toque) y `GameConfigAsset.cs` (wrapper ScriptableObject de `GameConfig`, nombre distinto a propósito para no repetir la ambigüedad de nombres que ya pasó una vez con `SafeArea`).
- Reescribió `GameManager.cs`: `Initialize(IPlayerController, IPlayerController)` público y re-llamable, controllers por default (`LocalHumanPlayer` x2) creados en `Awake()` si nadie inyectó otros antes — no se pueden wirear desde el scene builder porque un campo de interfaz con `event` no sobrevive la serialización de Unity. `OnCellClicked`, `StartNewGame`, `CurrentState`, `StateChanged`, `GameEnded` mantienen exactamente la misma firma pública.
- **Confirmó y verificó que la UI no necesitaba ningún cambio**: `BoardView.cs`, `GameHud.cs`, `RestartButton.cs` quedaron intactos — cero líneas tocadas, todos siguen llamando a la misma API de `GameManager` que ya usaban.
- `TicTacFadeSceneBuilder.cs`: `GetOrCreateGameConfigAsset()` (busca en `Assets/_Project/Config/GameConfig.asset`, lo crea solo si falta) + wiring del campo `config` de `GameManager`. Verificado idempotente corriendo el builder 2 veces: el asset no se duplica.
- **Corrección de diseño encontrada por Cami en la revisión del plan, antes de escribir código:** el primer diseño notificaba el turno siguiente con una llamada recursiva desde dentro del manejo de la jugada anterior. Con dos controllers autónomos (el test pedido), eso apila un stack frame por jugada de toda la partida, y una jugada rechazada que no corta el ciclo produce recursión infinita → StackOverflow → cuelga el Editor sin excepción capturable. Se corrigió con un flag de re-entrada (`_isAdvancingTurns`) + bucle en un solo método (`AdvanceTurns`): una llamada anidada no abre un bucle nuevo, el de más afuera recoge el cambio de estado en su próxima iteración. `ApplyMove` corta en seco ante un `MoveRejectedEvent` (loguea y sale, no vuelve a notificar).
- Creó `FakeAutoPlayer.cs` (solo test, `Assets/_Project/Tests/PlayMode/`): controller no-humano que juega la primera celda libre apenas se le notifica su turno.
- Extendió `SceneWiringAndInputTests.cs`: sumó `GameManager` al chequeo de referencias serializadas sin asignar (protege el campo `config` nuevo). Agregó `GameManager_TwoFakeAutoPlayers_PlayFullMatchToCompletion` — dos `FakeAutoPlayer` (no un fake + un humano, pedido explícito de Cami en la segunda revisión) juegan una partida completa hasta `IsOver`, con un tope de 60 frames que falla el test si se excede (protección de test independiente, no confía solo en que el flag de re-entrada de producción esté bien) y verifica que terminó en un resultado válido (victoria con línea, o empate por repetición/tope de jugadas).
- Agregó `docs/adr/0003-player-controller-abstraction.md` — solo para la parte de controllers; la de `GameConfig` como ScriptableObject ya estaba decidida en ADR 0001, no le correspondía una ADR nueva.
- **Bug de tooling, no de código:** al crear `LocalHumanPlayer.cs`, `GameManager.cs` no podía resolver el tipo (`CS0246`) pese a que `IPlayerController.cs` y `GameConfigAsset.cs` —creados en el mismo lote— compilaban bien, y `validate_script` no encontraba ningún error en el archivo. Ni forzar `refresh_unity` varias veces ni reimportar el asset explícitamente lo resolvió. Se solucionó borrando el archivo (`delete_script`) y recreándolo idéntico — algo quedó mal registrado en el asset database para ese path/GUID específico en el primer intento. `unity_reflect search` (por texto) sigue sin encontrarlo después del fix, pero `unity_reflect get_type` (por nombre completo) sí lo confirma — el índice de búsqueda por texto tiene su propio caché, separado de la compilación real.

**Revisión humana:** Cami aprobó el plan con una corrección de fondo (la recursión/rechazo de arriba) antes de que se escribiera una sola línea, y amplió el test pedido para que fuera dos controllers autónomos jugando una partida entera (no un fake + un humano) con protección explícita contra loops.

**Verificación:** compila sin errores/warnings. `run_tests(EditMode)`: 33/33 Core, sin regresión. Scene builder corrido 2 veces: `GameConfig.asset` se crea una sola vez, no se duplica. `run_tests(PlayMode)`: 3/3 (wiring extendido a `GameManager`, doble-toque, partida completa con dos `FakeAutoPlayer`). Play mode manual con toques reales (`Button.onClick.Invoke()` doble por celda): partida local se juega idéntico a como jugaba antes del refactor.

**Aprendizajes:** un archivo nuevo puede fallar en resolver como tipo con un error de compilador engañoso (`CS0246`, "falta un using") aunque el archivo esté sintácticamente perfecto y sus hermanos creados en el mismo lote compilen bien — vale la pena borrar y recrear el archivo antes de perseguir una causa de código que no existe. Revisar un plan de arquitectura ANTES de escribir código (como hizo Cami acá) es mucho más barato que encontrar un StackOverflow después: la recursión disfrazada de "notificación de turno" no se ve como recursión hasta que se piensa específicamente en el caso de dos controllers autónomos encadenados.

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
