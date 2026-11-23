# Cobertura de pruebas por etapa GDD

Esta matriz enlaza las etapas E01–E08 del GDD v1.4 con las suites actuales. Las pruebas omitidas conservan la etapa que debe activarlas en `PendingInvariantsTests.cs`.

| Etapa | Cobertura automatizada | Pendiente para cerrar la etapa |
|---|---|---|
| E01 · Núcleo determinista | `Pcg32Tests`, `DetMathTests`, `SeedDeriverTests`, `RandomDistributionsTests`, `StableSortTests`, `SimulationFormulasTests`, `CoreGoldenTests`, `ImplementedInvariantsTests` | Ejecutar la matriz CI completa en Ubuntu, Windows y macOS; validar hashes golden en esas plataformas. |
| E02 · Dominio y datos | `OvrCalculatorTests`, `RoleProfilesTests`, `FormationCatalogTests`, `InvariantCheckerTests`, `ParamsConfigTests`, `SmokeTests` | Mantener alineada la semántica de familiaridad del OVR con cualquier aclaración futura del GDD. |
| E03 · Mundo inicial | `AttributeGeneratorTests`, `NameGeneratorTests`, `SquadGeneratorTests` | Completar generación y validar bandas poblacionales de jugadores/regens a escala de mundo. |
| E04 · Competiciones | La estructura de fixtures se verifica en `tests/schema/test_schema_v1.py`; existe un invariante omitido para copa nacional | Implementar calendario, clasificación y copa; activar el invariante de copa cuando ese sistema exista. |
| E05 · Motor de partidos | `SeedDeriverTests` comprueba streams independientes por partido; `PendingInvariantsTests` contiene pruebas omitidas para probabilidades, xG, eventos y reproducibilidad del resultado | Implementar motor y MatchReport; conectar `MatchSeed` al flujo de simulación y activar esos invariantes. |
| E06 · Gestión Slice | Cobertura parcial de dominio mediante `InvariantCheckerTests` y `SimulationFormulasTests` | Añadir suites de gestión para plantillas, alineaciones, finanzas y mercado cuando se implementen esos sistemas. |
| E07 · Calibración QA | Propiedades locales en `SimulationFormulasTests`; configuración validada en `ParamsConfigTests` | Ejecutar simulaciones estadísticas grandes con semillas registradas y cerrar bandas de aceptación del GDD. |
| E08 · Persistencia | `tests/schema/test_schema_v1.py` verifica creación, restricciones, relaciones, reapertura e integridad SQLite | Añadir migraciones versionadas reales y pruebas de actualización/rollback cuando exista una versión anterior soportada. |

Las etapas E01 y E02 aparecen cerradas en el plan del GDD, pero el estado de una etapa se confirma con sus criterios de aceptación completos y CI verde, no solo por la existencia de estas suites.
