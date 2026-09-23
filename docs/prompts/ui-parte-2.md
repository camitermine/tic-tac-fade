Iteración de UI, parte 2: feedback visual del tablero (GDD §4.1 y §3.7).

Indicador de vida en cada ficha, para las de ambos jugadores. Vida = BufferSize − fichas colocadas después por ese jugador. Vida 3: opacidad plena. Vida 2: opacidad media. Vida 1: además de la transparencia, una marca visible (contorno o ícono) — el GDD es explícito en no depender solo de la opacidad, porque en pantalla al sol no se distingue.
Separar el aviso de desvanecimiento del contador: su propio elemento, no concatenado en el mismo Text. Cancela la deuda anotada en la entrada [5] del ai-log.
Ghost piece con confirmación en dos toques: el primer toque selecciona la celda y previsualiza el tablero resultante (ficha nueva puesta y ficha que se va marcada); un segundo toque en la misma celda confirma; tocar otra celda cambia la selección.
Resaltado de la línea ganadora al terminar la partida.

El Core no se toca: toda la información necesaria ya está en GameState y en los eventos. Si detectás que falta algo en el Core, paralo y avisá antes de agregarlo.

Verificación: Simulator en 16:9 y 20:9, partida completa incluyendo una victoria, y captura de los tres estados de vida visibles a la vez.