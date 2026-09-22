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
