# GDD: Tic-Tac-Fade

**Versión:** 0.3
**Estado:** MVP definido — pre-producción
**Fecha:** Septiembre 2026
**Fuente de verdad:** este archivo (`docs/GDD.md`). Cualquier espejo externo (Google Docs) es secundario.

---

## 1. Historial de versiones

| Versión | Fecha | Descripción |
| :-- | :-- | :-- |
| 0.1 | Sept 2026 | Primer borrador: core gameplay, lógica FIFO, loop de turnos y roadmap. |
| 0.2 | Sept 2026 | Migración al repo. Regla de empate (repetición + tope), timer por turno con modo ausente, definición general de "vida", indicadores para ambos jugadores, invariante de jugada legal, alcance del MVP. |
| 0.3 | Sept 2026 | Lenguaje visual final del indicador de vida 1 (badge opaco teñido por dueño) y del ghost piece (marca de opacidad mínima sobre la ficha propia que el FIFO eliminaría, distinta de la marca crítica pasiva). |

---

## 2. Visión general

### 2.1 Concepto

*Tic-Tac-Fade* es una reinterpretación táctica del Tres en Línea. Cada jugador tiene un límite de **3 fichas activas** en un tablero 3x3. Al colocar la 4ª ficha, la más antigua de ese jugador desaparece. El tablero nunca se llena, así que no existe el empate por bloqueo: el juego pasa a ser un duelo de anticipación y control del espacio.

### 2.2 Características clave

- **Sin empates por bloqueo.** El tablero nunca se satura. Los empates solo ocurren por repetición o por tope de jugadas (ver 3.4).
- **Lectura del tablero.** Cada jugada exige anticipar qué casilla quedará libre, tanto propia como del rival.
- **Partidas rápidas.** Objetivo: 1 a 2 minutos por partida.
- **Base modular.** Reglas parametrizadas (tamaño de tablero, tamaño de buffer, largo de línea) para escalar a 4x4, vs IA y modificadores roguelike.

### 2.3 Género, plataformas y público

- **Género:** estrategia por turnos / puzzle táctico competitivo.
- **Plataforma MVP:** Android, distribuido como `.apk`, orientación vertical.
- **Plataformas futuras:** Web (WebGL, para portfolio e itch.io) y PC.
- **Público:** jugadores que buscan partidas competitivas cortas con profundidad táctica.

---

## 3. Mecánicas (core gameplay)

### 3.1 Tablero y turnos

- **Tablero:** 3x3 en el MVP. Parametrizable (`BoardSize`).
- **Jugadores:** 2 (Jugador 1: X, Jugador 2: O).
- **Turno:** alternado, una acción por turno: colocar una ficha en una casilla libre.
- **Quién empieza:** en la primera partida, el host (X). En cada revancha se alterna quién empieza.

### 3.2 Lógica FIFO

Cada jugador tiene una cola FIFO de tamaño máximo `BufferSize` (3 en el MVP).

1. **Fase inicial:** mientras la cola no está llena, las fichas se colocan normalmente.
2. **Fase de desvanecimiento:** con la cola llena, al colocar una ficha nueva la más antigua del jugador activo se elimina.
3. **Restricción de reemplazo:** no se puede colocar la ficha nueva en la casilla que ocupa la ficha que está por desaparecer. La casilla tiene que estar libre **antes** de la jugada.

### 3.3 Condición de victoria

- **Alineación:** gana quien tenga `WinLength` fichas (3 en el MVP) alineadas en horizontal, vertical o diagonal.
- **Orden de resolución de una jugada:**
  1. Validar la jugada.
  2. Colocar la ficha nueva.
  3. Aplicar FIFO (eliminar la más antigua si la cola se excedió).
  4. Evaluar victoria.
  5. Evaluar empate (3.4).
- Consecuencia: una línea que dependía de la ficha recién eliminada **no** cuenta como victoria.

### 3.4 Empates

El juego puede ciclar indefinidamente entre dos jugadores que defienden bien. Hay dos reglas de corte:

- **Repetición:** si la misma **posición** aparece por 3ª vez, la partida termina en empate.
  - Una posición es: las colas de ambos jugadores **con su orden** + a quién le toca jugar.
  - Dos tableros visualmente iguales con colas en distinto orden son posiciones **distintas**, porque el futuro del juego es distinto.
- **Tope de jugadas:** al llegar a `MaxTotalMoves` jugadas totales (sumando ambos jugadores; valor inicial: 40), la partida termina en empate. Es una red de seguridad.

### 3.5 Timer de turno y modo ausente (online)

- **Timer normal:** `TurnTime` = 30 s por turno.
- **Vencimiento:** si el timer llega a 0, se juega automáticamente una **jugada legal aleatoria** por ese jugador, y el turno pasa al rival. Esto le da margen a un jugador con problemas de conexión para volver.
- **Modo ausente:** a partir de un vencimiento, el jugador queda marcado como *ausente* y sus turnos siguientes duran `AbsentTurnTime` = 10 s.
- **Regreso:** en cuanto el jugador ausente hace una jugada propia, deja de estar ausente y vuelve al timer normal.
- **Abandono (propuesta, a confirmar):** 3 vencimientos consecutivos cuentan como derrota por abandono.
- **Autoridad:** el host lleva el timer y genera la jugada aleatoria. Esa jugada se transmite como cualquier otra, así ambos clientes quedan sincronizados.

### 3.6 Invariante: siempre hay jugada legal

Con 3x3 y buffer 3 hay como máximo 6 casillas ocupadas, así que siempre quedan al menos 3 libres. **Cualquier regla o modificador futuro debe preservar este invariante.** Si una variante lo rompe (fichas ancla, buffer 4 en 3x3, etc.), hay que definir antes qué pasa cuando un jugador no tiene jugada legal.

### 3.7 Controles

- **Mobile (touch):** el primer toque selecciona una casilla y muestra la *ghost piece* (previsualización del resultado). Un segundo toque en la misma casilla confirma. Tocar otra casilla cambia la selección.
  - **Previsualización del resultado (v0.3):** la *ghost piece* no muestra solo la ficha nueva. Si la cola del jugador activo está llena, también marca su propia ficha más vieja (la que el FIFO eliminaría por esa jugada) con una caída a opacidad mínima. Esta marca es distinta de la marca crítica pasiva de vida 1 (§4.1): esa última puede estar visible en la ficha de cualquiera de los dos jugadores en cualquier momento y no identifica cuál se va por la jugada que se está previsualizando ahora.
- **PC (futuro):** el hover previsualiza y el clic confirma.

---

## 4. UI / UX

### 4.1 Vida de las fichas

**Definición:** `vida = BufferSize − (cantidad de fichas que ese jugador colocó después de esta)`.

- Vida 1 = desaparece en la **próxima** jugada de su dueño (cuando la cola está llena).
- La definición funciona también en la fase inicial y con buffers de otro tamaño.

Representación, aplicada a las fichas de **ambos jugadores**:

| Vida | Apariencia |
| :-- | :-- |
| 3 (nueva) | Opacidad plena, bordes definidos. |
| 2 | Opacidad media, pulso lento. |
| 1 (crítica) | Contorno o ícono de "vence" **además** de la transparencia. No depender solo de la opacidad, porque en pantallas con sol no se distingue. |

**Implementación MVP (v0.3):** el ícono de vida 1 es un badge opaco (no otra capa de transparencia) en la esquina de la ficha, **teñido con el color del dueño** — no un color fijo compartido entre jugadores, porque con las dos colas llenas puede haber un badge de cada jugador visible a la vez y tienen que distinguirse entre sí.

**Decisión MVP:** ayudas visuales completas activadas. Un modo sin ayudas ("modo memoria") queda como posible modificador futuro.

### 4.2 HUD de partida

- Indicador de turno, con quién juega y quién es cada uno.
- Timer del turno, con un estado visual distinto en modo ausente.
- Indicador de "ausente" sobre el jugador correspondiente.
- Resaltado de la línea ganadora.
- Mensaje de resultado: victoria, derrota o empate, indicando el motivo (repetición, tope, abandono, desconexión).

### 4.3 Flujo de pantallas (MVP)

`Menú principal → (Local | Crear sala | Unirse con código) → Sala de espera → Partida → Resultado → (Revancha | Menú)`

---

## 5. Online (MVP)

- **Tecnología:** Unity Multiplayer Services (Sessions), con Relay y códigos de sala. Ver `docs/adr/0002-online-multiplayer-services.md`.
- **Modelo:** host-cliente, 2 jugadores por sala. El host es autoritativo: valida las jugadas y lleva el timer.
- **Sincronización:** se transmiten **jugadas** (`Move`), no el tablero completo. Ambos clientes aplican las mismas reglas del Core y pueden comparar un hash del estado para detectar desincronizaciones.
- **Desconexiones:**
  - Si el **cliente** se desconecta, la partida sigue y su timer corre (3.5). La reconexión es *best effort* en el MVP.
  - Si el **host** se desconecta, la partida termina y el cliente vuelve al menú con un mensaje.

---

## 6. Diseño audiovisual (post-MVP)

- **Arte:** minimalista de alto contraste. Candidatos: neón cyberpunk o flat vector.
- **Animaciones:** partículas al desvanecerse una ficha y trazo luminoso en la línea ganadora.
- **SFX previstos:** colocar ficha, ficha entrando en vida 1, ficha disolviéndose, victoria, aviso de timer.
- En el MVP se admiten placeholders.

---

## 7. Alcance del MVP

**Entra:**

- Reglas 3x3 con FIFO, victoria y empates, implementadas en el Core y cubiertas por tests.
- Modo local en un dispositivo.
- Online 1v1: crear sala, mostrar el código, unirse con código.
- Timer de turno con modo ausente.
- Indicadores de vida para ambos jugadores y *ghost piece*.
- Línea ganadora resaltada y pantalla de resultado con revancha (alternando quién empieza).
- Manejo de desconexión.
- Menú mínimo.
- Build de Android en vertical.

**No entra:** arte final, música, IA, roguelike, 4x4, cuentas de usuario, matchmaking, PC/Web.

---

## 8. Roadmap post-MVP

1. **Solver por análisis retrógrado.** Unos 120 mil estados en 3x3 con buffer 3. Responde si el juego es victoria forzada para el primero o empate con juego perfecto, y alimenta la IA.
2. **Modo vs IA**, con dificultades basadas en el solver más ruido.
3. **Build WebGL** en itch.io.
4. **Tablero 4x4** y variantes de `BufferSize` / `WinLength`.
5. **Arte y audio.**
6. **Roguelike:**
   - Fichas especiales: pesadas (duran más), bomba, ancla.
   - Reliquias y pasivas: buffer variable, alterar el orden de desvanecimiento, robar turnos.
   - PvE con jefes y casillas con peligros.
   - Muerte súbita como alternativa al empate.

---

## 9. Parámetros (valores iniciales)

| Parámetro | Valor | Nota |
| :-- | :-- | :-- |
| `BoardSize` | 3 | Tablero NxN. |
| `BufferSize` | 3 | Fichas activas por jugador. |
| `WinLength` | 3 | Fichas en línea para ganar. |
| `MaxTotalMoves` | 40 | Tope de jugadas totales. |
| `RepetitionLimit` | 3 | Apariciones de una posición para declarar empate. |
| `TurnTime` | 30 s | Duración normal del turno. |
| `AbsentTurnTime` | 10 s | Duración del turno en modo ausente. |
| `MaxConsecutiveTimeouts` | 3 | Vencimientos seguidos para abandono (a confirmar). |
