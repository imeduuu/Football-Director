# ADR 0001: Stack de simulación y cliente

- Estado: Propuesto
- Fecha: 2026-11-23
- Decisores: equipo de Boardroom & Touchline

## Contexto

El juego es offline-first, simula partidos instantáneamente y no necesita representación en vivo. El GDD deja abierta la elección entre Unity/C# y un motor C++/Rust conectado por FFI (§14.2), con objetivos de menos de 800 ms para 100 partidos headless y menos de 150 ms para una jornada de 10 partidos (§14.3). También exige determinismo entre Android, iOS y CI (§14.7), un RNG propio y pruebas golden. La UI debe poder iterar sin acoplarse al motor de reglas.

## Prototipo y medición

Se preparó un benchmark desechable en `tools/StackPrototype`: .NET 8, BenchmarkDotNet, 100 partidos × 18 intervalos, PCG32 fijo por partido, operaciones `double`, suma checksum y `MemoryDiagnoser`. Se mide el lote completo; la tabla registra tiempo medio y asignaciones administradas por operación.

| Entorno | Tiempo por 100 partidos | Asignaciones administradas | Estado |
|---|---:|---:|---|
| Windows 11 x64, .NET SDK 8.0.428, runtime 8.0.34, RyuJIT AVX2 | 14,04 µs (σ = 0,81 µs; rango 13,45–15,36 µs; 6 iteraciones) | 0 B/op (BenchmarkDotNet muestra `-` en ambas ejecuciones) | Dos `ShortRun` de 3 iteraciones, BenchmarkDotNet 0.14.0; procesador reportado como desconocido. |

Este microbenchmark prueba coste de ejecución y asignaciones de un bucle sintético; no representa el motor final ni permite inferir latencia p95 móvil, consumo pico, guardado, carga o tamaño de paquete. `ShortRun` usa pocas iteraciones y el equipo reportó el procesador como desconocido, así que estos datos son una referencia inicial, no una comparación de plataformas ni una puerta de rendimiento. El perfil de dispositivo real debe acompañar cualquier comparación futura.

## Alternativa: núcleo Rust/C++ vía FFI

No se implementó. Un núcleo nativo podría ser útil si el perfil del motor real muestra que C# no cumple presupuestos en dispositivos objetivo. A cambio, añade una frontera ABI, empaquetado y depuración por plataforma, gestión de memoria y errores, marshalling de datos y una matriz adicional de pruebas Android/iOS. FFI no garantiza determinismo: RNG, orden de operaciones, coma flotante y funciones transcendentales también deben especificarse y probarse. Sin una medición comparable del mismo algoritmo en C#/Rust/C++, no hay evidencia para asumir una mejora concreta.

## Decisión recomendada

Usar un núcleo de reglas y simulación en **C# `netstandard2.1`**, independiente de Unity y sin referencias a `UnityEngine`; usar **Unity únicamente como cliente/UI**. Mantener el motor headless y con dependencias mínimas, RNG propio, orden explícito y pruebas golden entre plataformas. `netstandard2.1` permite que el núcleo no dependa del ciclo de vida ni de APIs de Unity, mientras que Unity concentra presentación e integración móvil.

Revisar esta decisión solo con perfiles del motor real en dispositivos Android/iOS representativos. Considerar FFI si el núcleo medido incumple de forma repetida los presupuestos de §14.3 y un prototipo nativo equivalente demuestra una mejora suficiente para compensar su coste de integración. El benchmark sintético de este ADR no sirve por sí solo para justificar FFI ni para validar el objetivo móvil.

## Consecuencias

- El núcleo podrá ejecutarse en CI y herramientas headless sin iniciar Unity.
- La UI y la simulación tendrán contratos explícitos, facilitando pruebas y sustitución del cliente.
- La compatibilidad del framework no resuelve por sí sola las diferencias de coma flotante entre arquitecturas; el motor deberá cumplir las reglas de determinismo del GDD y validar hashes golden.
- Si la simulación supera el presupuesto, se perfila primero el código crítico antes de valorar una frontera nativa.
