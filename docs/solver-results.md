# Resultados del solver (3x3, buffer 3)

Resultados del análisis retrógrado de GDD §8.1. El solver es `TicTacFade.Solver.GameSolver`, y el criterio y las decisiones están en `docs/adr/0004-solver-assembly.md`. Los números se reproducen con el menú del Editor `Tic-Tac-Fade/Solver/Solve MVP and report`.

- **Configuración:** MVP (`GameConfig.Mvp()`): tablero 3x3, buffer 3, línea de 3, tope de 40 jugadas, repetición 3. Empieza X.
- **Unidades:** las distancias se cuentan en **jugadas individuales (plies)**, igual que `TotalMoves`. "Victoria en 13" significa que la partida termina en la 13.ª jugada contando las de los dos, y que la última la hace quien gana.
- **Fecha:** 2026-10-07.

## Respuesta

**Con juego perfecto de ambos lados, quien empieza tiene victoria forzada en 13 jugadas**: su 7.ª ficha, si el rival demora todo lo posible.

- La victoria forzada exige empezar en un **borde** (casillas 1, 3, 5 o 7).
- Empezando en una **esquina o en el centro**, ninguno puede forzar la victoria.
- Por simetría, el resultado es el mismo cuando empieza O (verificado: `Win in 13`).

Numeración de casillas:

```
0 | 1 | 2
3 | 4 | 5
6 | 7 | 8
```

## Verificación independiente

`tools/verify_solver.py` es un solver escrito en Python a partir de las reglas del GDD. Lo escribió Claude en la conversación de planificación del proyecto (no el agente de Claude Code que implementó el solver en C#), sin acceso al código en C#. No tiene dependencias y tarda unos 2 s:

```
python3 tools/verify_solver.py
```

Corrido el 2026-10-07, **reproduce exactamente todos los números de este documento**:
- 116.074 posiciones y 369.801 jugadas;
- `Win in 13` en la posición inicial;
- el resultado de las 9 aperturas;
- la distribución Win / Loss / NoForcedWin;
- las distancias máximas (17 y 16) y el histograma completo de distancias.

Dos implementaciones independientes en lenguajes distintos coinciden, además de los tests EditMode del solver en C# (equivalencia con `RulesEngine` en todas las posiciones y consistencia del retrógrado).

**Lo que el script no cubre:** la simetría cuando empieza O, porque no explora esa posición inicial. Eso lo verifica el test en C#.

**Ejemplo de línea forzada** (el script la reconstruye: el ganador elige la victoria más corta y el perdedor la defensa más larga). Son 13 jugadas, sin posiciones repetidas:

```
X1 O8 X4 O7 X6 O2 X0 O3 X8 O4 X5 O7 X2   ← X gana en la 13.ª jugada
```

## Primera jugada de X

| Casilla | Tipo | Resultado para X |
| :-: | :-- | :-- |
| 0 | esquina | Sin victoria forzada |
| 1 | borde | Victoria en 13 |
| 2 | esquina | Sin victoria forzada |
| 3 | borde | Victoria en 13 |
| 4 | centro | Sin victoria forzada |
| 5 | borde | Victoria en 13 |
| 6 | esquina | Sin victoria forzada |
| 7 | borde | Victoria en 13 |
| 8 | esquina | Sin victoria forzada |

Contraintuitivo respecto del ta-te-tí clásico: el centro, la mejor apertura sin FIFO, no fuerza nada acá.

## Tamaño y rendimiento

Medido en el Editor (Unity 6, Mono, PC). Todavía no se midió en dispositivo.

| Medida | Valor |
| :-- | --: |
| Cota superior del espacio de estados | 204.086 |
| Posiciones alcanzables desde el inicio | **116.074** |
| Jugadas legales (aristas, incluidas las victorias inmediatas) | 369.801 |
| Tiempo total (primera corrida) | ~0,5 s |
| · exploración hacia adelante | ~0,46 s |
| · análisis retrógrado | ~0,06 s |
| Memoria administrada retenida por la tabla completa (con el grafo) | ~30 MB |

La cifra de "unos 120 mil estados" que estimaba el GDD era correcta.

## Distribución de resultados

Sobre las 116.074 posiciones alcanzables, desde el punto de vista de quien mueve:

| Resultado | Posiciones | % |
| :-- | --: | --: |
| Victoria forzada | 78.613 | 67,7 % |
| Derrota forzada | 24.268 | 20,9 % |
| Sin victoria forzada | 13.193 | 11,4 % |

- Victoria forzada más larga: 17 jugadas.
- Derrota forzada más larga: 16 jugadas.

Posiciones con victoria o derrota forzada, por distancia:

| Distancia | Posiciones | | Distancia | Posiciones |
| --: | --: | :-: | --: | --: |
| 1 | 38.736 | | 10 | 1.504 |
| 2 | 7.520 | | 11 | 1.604 |
| 3 | 14.896 | | 12 | 612 |
| 4 | 7.000 | | 13 | 553 |
| 5 | 13.592 | | 14 | 432 |
| 6 | 3.760 | | 15 | 600 |
| 7 | 6.120 | | 16 | 48 |
| 8 | 3.392 | | 17 | 16 |
| 9 | 2.496 | | | |

Las distancias impares son victorias y las pares son derrotas, siempre para quien mueve.

## Efecto del tope de 40 jugadas

- **Desde el inicio de la partida, el tope no corta ninguna victoria forzada.** La línea forzada desde la posición inicial mide 13 jugadas, y la más larga desde cualquier posición alcanzable mide 17. Las dos están por debajo de 40.
- **A mitad de partida sí puede importar:** una victoria forzada a distancia `d` desde una posición con `t` jugadas ya hechas solo se cumple si `t + d ≤ 40`. Con `d ≤ 17`, eso pasa recién después de la jugada 23. La futura IA lo tiene que considerar (ADR 0004).
- **Repetición:** las líneas forzadas nunca repiten posiciones (la distancia baja en cada jugada), así que la regla de repetición no las corta. Puede cortarlas una posición que ya salió antes en la partida real (ADR 0004, nota para la IA).

## Implicancias (para decidir, no decididas)

- **El juego está resuelto a favor de quien empieza.** Con la alternancia de quién empieza en la revancha (GDD §3.1), una serie entre dos jugadores perfectos la ganaría siempre quien empieza cada partida. Entre humanos, 13 jugadas de precisión con fichas que desaparecen no es algo que se juegue de memoria. Igual, conviene tenerlo en cuenta para el balance y para el diseño de dificultades de la IA.
- **El tope de 40 no afecta el juego perfecto desde el inicio.** Para las "Opciones de sala" (GDD §8.3):
  - con un tope de 13 o más, se mantiene la victoria forzada desde la posición inicial;
  - con un tope menor que 13, esa victoria se convierte en empate por tope;
  - a mitad de partida, el efecto depende de `t + d` (jugadas hechas más distancia).

  Para elegir los valores del tope con datos falta un análisis aparte: una corrida del solver con el tope como parte del estado.
