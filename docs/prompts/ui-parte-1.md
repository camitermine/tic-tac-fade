Iteración de UI, parte 1: dejar la escena y el HUD sanos.

Bug 1: el HUD muestra textos superpuestos (el turno se lee con X y O encimadas, y los contadores se pisan con el aviso de desvanecimiento). Mi hipótesis: TicTacFadeSceneBuilder no es idempotente y al re-ejecutarlo duplicó los objetos del HUD, quedando labels viejos sin actualizar encima de los vivos. Verificalo inspeccionando la jerarquía por MCP antes de tocar nada.

Trabajo a hacer:

Hacer el builder idempotente: que limpie o reutilice los objetos existentes en vez de duplicarlos. Ejecutarlo dos veces seguidas tiene que dar exactamente la misma jerarquía.
Agregar una Main Camera a la escena (soluciona el "No cameras rendering" y el fondo negro).
Rehacer el layout del HUD para que sea imposible que se pisen: anchors y layout groups, no posiciones fijas. Orientación vertical (portrait), Canvas Scaler en Scale With Screen Size con 1080x1920 y match 0.5, y el HUD dentro de un contenedor que respete la safe area.

Verificá con el Simulator en al menos dos relaciones de aspecto (16:9 y 20:9) que nada se superponga ni se salga de pantalla. Fuera de alcance: indicadores de vida de las fichas, ghost piece y línea ganadora, que van en la parte 2.