# Boardroom & Touchline

> La pizarra gana campeonatos. Tus decisiones escriben la historia.

Simulador estratégico de gestión futbolística para móviles, centrado en las decisiones desde los despachos, el entrenamiento y la pizarra táctica.
Es offline-first: la carrera y sus sistemas se diseñan para funcionar sin conexión.
Los partidos se simulan de forma instantánea mediante eventos y zonas, sin partido visual ni control en tiempo real.

**Estado:** En desarrollo — preproducción. Especificación: GDD v1.3.0.

## Pilares

- La pizarra supera al azar: preparación, roles, compatibilidad y lectura del rival producen ventajas medibles.
- Fricción cero: navegación directa, acciones frecuentes accesibles y avance temporal sin esperas artificiales.
- Consecuencias sistémicas: cada decisión tiene costes deportivos, económicos, físicos o sociales.
- Transparencia analítica: el usuario puede comprender los resultados con métricas, eventos y explicaciones.
- Mundo persistente: clubes, jugadores y entrenadores evolucionan aunque el usuario no los controle.
- Carreras extensas: los datos históricos y la generación de nuevos futbolistas sostienen décadas de juego.

## Plataformas objetivo

- Android API 26+.
- iOS 15.0+.

## Stack

La tecnología de implementación sigue **pendiente de ADR (docs/adr/0001-stack.md)**. El GDD contempla opciones, pero no registra una decisión final.

## Estructura

| Carpeta | Propósito |
| --- | --- |
| `src/BT.Core/` (planificado) | Núcleo de simulación y reglas del juego. |
| `src/BT.Data/` (planificado) | Persistencia y acceso a datos. |
| `tests/` (planificado) | Pruebas automatizadas. |
| `tools/BT.Balance/` (planificado) | Herramientas de balance. |
| `config/` (planificado) | Parámetros y configuración editable. |
| `data/` (planificado) | Datos del juego. |
| `docs/` | GDD y documentación del proyecto. |
| `unity/` (planificado) | Proyecto de cliente Unity. |

## Compilar y probar

Disponible tras la tarea de scaffolding.

## Documentación

- [GDD v1.3](docs/GDD_v1_3.md)
- [Instrucciones del proyecto](CLAUDE.md)

## Contribución

- Usar [Conventional Commits](https://www.conventionalcommits.org/).
- Mantener el determinismo: misma semilla + misma versión = mismo resultado.
- Sin mecánicas Pay-to-Win ni telemetría por defecto.

## Datos

El juego usa datos ficticios por defecto.

## Licencia: pendiente de definir
