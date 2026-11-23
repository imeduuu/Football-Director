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

## Semillas por partido

`SeedDeriver.MatchSeed(seedMundo, idCompeticion, temporada, jornada, idPartido)` devuelve la semilla independiente de un partido. Los identificadores se codifican como UTF-8 precedidos por su longitud en bytes; la semilla del mundo es `UInt64` y temporada/jornada son `Int32`, todos little-endian. El registro completo lleva el dominio `BT.MatchSeed.v1` para evitar que colisione con otros usos del hash.

La serialización se mezcla con xxHash64 estándar (seed cero), implementado en `StableHash64`; sus cargas de 32 y 64 bits se leen explícitamente en little-endian. Para simular cada partido se crea su propio `Pcg32` a partir de la semilla derivada y una secuencia fija elegida por el sistema consumidor. Así, el orden de simulación de los partidos no comparte ni avanza estado aleatorio. El hash no es criptográfico.

## Distribuciones deterministas

`BT.Core.Rng.RandomDistributions` ofrece muestreo sobre un `Pcg32` pasado por referencia. `TruncatedNormal` usa Box-Muller con `DetMath` y rechazo hasta obtener un valor dentro de los límites inclusivos; su desviación debe ser positiva salvo que la media ya esté dentro de los límites. `WeightedChoice` devuelve el índice elegido, ignora pesos cero y exige peso total positivo. `FisherYates` mezcla una `IList<T>` en sitio mediante `NextInt` sin sesgo modular. `Poisson` emplea el método producto de Knuth y admite lambda en `[0, 700]`; lambda cero devuelve cero. El orden de llamadas al generador forma parte del resultado reproducible, por lo que los consumidores deben asignar streams estables por tarea o entidad.
