# Generador aleatorio determinista

`BT.Core.Rng.Pcg32` implementa PCG-XSH-RR 64/32. Su estado interno es de 64 bits y cada llamada a `NextUInt32` entrega 32 bits. No depende de `System.Random` ni de estado global.

## Inicialización y salida

El constructor recibe una semilla (`seed`) y una secuencia (`sequence`). Inicializa el estado a cero, establece el incremento impar `(sequence << 1) | 1`, avanza una vez, suma la semilla y vuelve a avanzar. Cada avance aplica, con aritmética módulo 2⁶⁴:

```text
state = state * 6364136223846793005 + increment
```

La salida transforma el estado anterior al avance mediante XSH-RR: desplaza XOR el estado, conserva 32 bits y rota a la derecha según los cinco bits superiores del estado anterior. La combinación de semilla y secuencia selecciona una secuencia reproducible.

## Rangos y precisión

`NextInt(min, max)` usa un intervalo semiabierto `[min, max)`. Rechaza valores por debajo del umbral `2³² mod bound` antes de aplicar el módulo; así cada resultado posible tiene la misma cantidad de preimágenes y no hay sesgo modular. Lanza `ArgumentOutOfRangeException` cuando `min >= max`.

`NextDouble()` consume dos salidas: 27 bits de la primera y 26 de la segunda. Forma un entero uniforme de 53 bits y lo divide por 2⁵³, por lo que el resultado está en `[0, 1)`.

## Streams derivados

`Fork(streamId)` aplica una mezcla SplitMix64 a la semilla y secuencia originales junto con el identificador, y usa esos valores para inicializar un nuevo PCG32. El mismo constructor y `streamId` producen el mismo stream hijo, aunque el padre ya haya generado números. Identificadores distintos obtienen parámetros derivados distintos salvo posibles colisiones de la mezcla; esto no constituye una garantía criptográfica.

La reproducibilidad se garantiza para una misma implementación/versión del algoritmo y los mismos parámetros. No usar este generador para seguridad criptográfica.
