# Registro de decisiones

Registro operativo derivado de las secciones 17 y 28 del [GDD v1.3.0](GDD_v1_3.md). Se conserva el estado que indica el GDD. Cuando no define responsable o fecha de calendario, se marca **Por asignar** en vez de inventar un compromiso. La etapa de cierre indica la evidencia requerida y el hito en que debe quedar resuelta.

## Decisiones cerradas

| Decisión | Responsable | Fecha límite | Etapa que la cierra | Evidencia / alcance |
|---|---|---|---|---|
| El Vertical Slice es el primer entregable obligatorio y precede al MVP completo. | Producción / responsable de producto (por asignar) | Cerrada en GDD v1.3.0 (22-11-2026) | Definición de alcance del Vertical Slice | Orden de entregas establecido en §28. |
| La dificultad no concede ventajas ocultas al motor; la IA mejora por calidad de decisiones. | Diseño de juego (por asignar) | Cerrada en GDD v1.3.0 (22-11-2026) | Diseño de dificultad e IA | Parámetros visibles y aplicados solo al club del usuario, conforme a §3.2 y §28. |
| Los rasgos cualitativos no duplican atributos numéricos. | Diseño de juego (por asignar) | Cerrada en GDD v1.3.0 (22-11-2026) | Diseño de jugadores | Los rasgos activan eventos o decisiones contextuales (§4.7, §28). |
| La pantalla táctica principal es simple; la profundidad queda bajo Ajustes avanzados. | UX / diseño de juego (por asignar) | Cerrada en GDD v1.3.0 (22-11-2026) | Diseño de interfaz | Decisión registrada en §28. |
| Quedan cerradas en estructura la copa nacional de 40 equipos, las fórmulas base de valor y salario, los límites de sectores/carriles, el rango inicial de OVR de regens (42–56), los coeficientes provisionales del rating, el calendario de 38 jornadas y el determinismo multiplataforma. | Diseño de juego y responsables de cada sistema (por asignar) | Cerradas en GDD v1.3.0 (22-11-2026) | Especificación v1.3.0 | Los valores que el GDD califica como provisionales todavía requieren calibración; el cierre de estructura no aprueba su balance final (§28). |

## Decisiones provisionales

| Decisión / hipótesis | Responsable | Fecha límite | Etapa que la cierra | Criterio de cierre |
|---|---|---|---|---|
| Bandas estadísticas y OVR por división. | Balance / simulación (por asignar) | Por asignar; antes de aprobar balance del motor | Calibración previa al MVP | Simulaciones reproducibles dentro de las bandas de aceptación del GDD (§17, §19). |
| Duración de lesiones y parámetros económicos, incluidos valor y salario. | Balance / economía (por asignar) | Por asignar; antes de aprobar economía del MVP | Simulación económica y de temporadas | Cumplir criterios de aceptación de economía de §24.1 y los criterios del módulo en §26.3. |
| Fórmula base y rango inicial de OVR de regens (42–56): estructura cerrada, distribución y resultados sujetos a validación. | Balance / generación de población (por asignar) | Por asignar; antes de aprobar progresión multitemporada | Simulación de regens y población | Verificar distribución por academia y ausencia de inflación de OVR durante carreras largas (§5.4, §20.3, §26.3). |
| Decisión de stack: ADR 0001 recomienda núcleo C# `netstandard2.1` independiente de Unity y Unity como cliente/UI; estado aún propuesto. | Arquitectura / ingeniería (por asignar) | Por asignar; antes de producción | Prototipo comparativo y decisión de arquitectura | Perfil del motor real en Android/iOS representativos; mantener la recomendación o justificar alternativa con mediciones (§14.2–14.7; ADR 0001). |

## Decisiones abiertas

| Decisión | Responsable | Fecha límite | Etapa que la cierra | Criterio / dependencia |
|---|---|---|---|---|
| **Escala económica (§9.1):** reescalar W0 e ingresos o reducir coeficientes de mantenimiento. | Economía / balance (por asignar) | Por asignar; antes de cerrar economía del MVP | Simulación de economía y aprobación de §24.1 | Para un club medio: masa salarial entre 50% y 70% de ingresos y mantenimiento total ≤25% de ingresos; validar además 10–50 temporadas (§17, §24.1). |
| **Calibración conjunta de umbrales +10/+20 OVR:** fijar bandas de sorpresa y rendimiento para ambas diferencias de OVR. | Balance del motor (por asignar) | Por asignar; antes de congelar balance del motor | Simulación masiva previa al MVP | Evaluar conjuntamente con semillas y métricas; para duelo desigual definido como ventaja ≥20 OVR, la victoria del inferior debe quedar en 8%–15% (§10.1, §19, §28). El GDD no especifica criterio numérico separado para +10: definirlo al calibrar. |
| **Controlador poblacional de POT de regens (§5.4):** validar y cerrar el ajuste suave ε aplicado a la media de la brecha de POT según deriva de OVR adulto. | Balance / generación de población (por asignar) | Por asignar; antes de aprobar progresión multitemporada | Simulación poblacional por temporadas | Validar banda objetivo de OVR medio de jugadores de 25–29 años y ajuste ε ∈ [−3,+3] en ambos sentidos, sin inflación ni deflación sostenida. El GDD no fija la banda objetivo concreta. |
| **Stack definitivo:** mantener la recomendación del ADR o elegir alternativa con evidencia del prototipo comparativo. | Arquitectura / ingeniería (por asignar) | Por asignar; antes de producción | Prototipo comparativo y revisión de ADR 0001 | Medir el motor real en Android/iOS y contrastar con presupuestos de §14.3 y requisitos de determinismo §14.7; el microbenchmark sintético no basta. |
| Reglas exactas de una liga real: reglamento, calendario, cupos, ventanas, sanciones y desempates. | Diseño de competición / contenido (por asignar) | Antes de incorporar una liga real | Integración de cada paquete de competición | Validar contra el reglamento oficial de la competición elegida (§17, §28). |

## Convención de seguimiento

El GDD no asigna personas ni fechas de calendario para los pendientes. Esos campos quedan explícitamente **Por asignar** y deben completarse por el equipo; los hitos relativos indican el último momento de cierre previsto por el propio GDD, no una fecha aprobada. Al cerrar una decisión, registrar fecha, responsable efectivo, evidencia y referencia a la versión del GDD o ADR que actualiza.
