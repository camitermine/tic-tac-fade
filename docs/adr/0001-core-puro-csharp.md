# ADR 0001 — Reglas del juego en un Core de C# puro

**Estado:** Aceptada
**Fecha:** 2026-09-22

## Contexto

El prototipo inicial mezcla lógica de juego y presentación. Esa lógica tiene que servir, sin cambios, para varios usos:

- el modo local en un dispositivo;
- el online, donde ambos clientes validan las mismas jugadas;
- una IA y un solver futuros, que necesitan simular miles de estados por segundo;
- los modificadores roguelike del roadmap.

Si las reglas viven en `MonoBehaviour`, cada uno de esos usos obliga a instanciar escenas y objetos de Unity, y los tests se vuelven lentos y frágiles.

## Decisión

- Todas las reglas viven en el assembly `TicTacFade.Core`, con `noEngineReferences: true` en su asmdef.
- El estado del juego es inmutable: `RulesEngine.Apply(state, move) → (newState, events)`.
- Los parámetros (tamaño de tablero, tamaño de buffer, largo de línea, límites de empate, timers) se reciben por `GameConfig`, una clase de C# puro. La capa `Game` la expone como ScriptableObject para poder editarla desde el Editor.
- El Core expone un hash de posición, usado tanto para la regla de empate por repetición como para detectar desincronización en el online.

## Consecuencias

**A favor:**

- Tests EditMode rápidos, sin escenas ni Play Mode.
- El compilador impide acoplar Unity a las reglas: no depende de la disciplina de quien escribe, ni humano ni agente.
- La IA, el solver y el online reutilizan exactamente el mismo código de reglas.
- El 4x4 y los cambios de buffer son cambios de configuración, no de código.

**En contra:**

- Hace falta una capa de mapeo entre el estado y las vistas, o sea algo de código "puente".
- Los estados inmutables generan asignaciones de memoria. Es irrelevante en 3x3, y si el solver lo necesita se optimiza ahí (por ejemplo, con una representación empaquetada en un entero).
