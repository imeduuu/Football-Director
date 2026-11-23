# BOARDROOM & TOUCHLINE

## GAME DESIGN DOCUMENT · Especificación Maestra

Versión 1.3.0 · 22 de noviembre de 2026

BOARDROOM
& TOUCHLINE
GAME DESIGN DOCUMENT · Especificación Maestra
Versión 1.3.0 · 22 de noviembre de 2026
Título provisional alternativo: Pitch Architect: Football Director
Plataformas objetivo: Android (API 26+) e iOS (iOS 15.0+)
Consolida la v1.2.0 y cierra sus pendientes: química y familiaridad,
valor y salario, copa nacional, zonas del campo, regens, rating y erratas.
Incluye la revisión crítica y un registro de cambios (Anexo D).
Estado: especificación de diseño. Las cifras de balance marcadas como
hipótesis deben validarse con simulaciones reproducibles; las reglas de
alcance y aceptación sí son decisiones de producción.
«La pizarra gana campeonatos. Tus decisiones escriben la historia.»



Control del documento y criterios de lectura
Propósito
Este documento define la visión, reglas, sistemas de juego, arquitectura de datos, experiencia de usuario y
criterios de validación de Boardroom & Touchline. Consolida el diseño original y todas las ampliaciones
acordadas: fórmulas de rendimiento, personalidad, química, familiaridad táctica, instrucciones individuales,
balón parado, noticias, historial, reputación especializada del mánager, balance estadístico y arquitectura
ampliada. La v1.3.0 cierra los pendientes de la v1.2.0 (fórmulas de química/familiaridad, valor y salario,
estructura de la copa, zonas del campo, OVR de regens, rating y erratas) y añade las correcciones de la
revisión crítica.
Convenciones
MVP: contenido indispensable para una primera versión jugable. Post-MVP: sistemas diseñados para
incorporarse después sin alterar los fundamentos. Provisional: valor inicial sujeto a calibración. Los sistemas
futuros no deben bloquear la entrega de una temporada completa funcional.
[v1.3·A] = contenido incorporado a petición del autor (pendientes de la v1.2.0). [v1.3·R] = contenido añadido
o modificado por la revisión crítica. El listado completo está en el Anexo D (Registro de cambios v1.3.0). Las
marcas pueden retirarse al aprobar la versión.
Principios de preservación
Se conserva la identidad offline-first, la simulación instantánea sin partido visual en vivo, la gestión deportiva
profunda, la transparencia matemática, la progresión multitemporada, la ausencia de energía artificial y la
prohibición de mecánicas Pay-to-Win.
### 1. Información del proyecto
### 1.1 Identidad

Título provisional: Boardroom & Touchline. Alternativa: Pitch Architect: Football Director. Tagline: «La pizarra
gana campeonatos. Tus decisiones escriben la historia.» Público objetivo: aficionados de 18 a 45 años
interesados en fútbol táctico y gestión, especialmente seguidores de experiencias clásicas como Cyberfoot, Be
the Manager, True Football 3 y Championship Manager. Hábitos esperados: sesiones frecuentes de 5 a 15
minutos y sesiones de planificación de 30 a 60 minutos.

### 1.2 Plataformas y requisitos objetivo

Android API 26+ (Android 8.0 Oreo) e iOS 15.0+. Modo principal vertical y adaptación horizontal para tabletas.
Requisitos de referencia: SoC Quad-Core 1,8 GHz, 3 GB RAM, 150 MB libres y aceleración gráfica OpenGL ES
3.0 / Metal. Los límites de memoria, tamaño y rendimiento son objetivos de ingeniería, no garantías hasta

medir una compilación funcional en hardware representativo.

### 1.3 Pilares

- La pizarra supera al azar: la preparación, los roles, la compatibilidad y la lectura del rival producen
ventajas medibles.
- Fricción cero: navegación directa, acciones frecuentes accesibles y avance temporal sin esperas
artificiales.
- Consecuencias sistémicas: cada decisión tiene costes deportivos, económicos, físicos o sociales.

- Transparencia analítica: el usuario puede comprender el resultado con métricas, eventos y explicaciones.

- Mundo persistente: clubes, jugadores y entrenadores evolucionan aunque el usuario no los controle.

- Carreras extensas: los datos históricos y la generación de nuevos futbolistas sostienen décadas de juego.

### 2. Visión general y concepto core
### 2.1 Elevator pitch

Boardroom & Touchline es un simulador estratégico de gestión futbolística para móviles. El usuario dirige un
club desde los despachos, el entrenamiento y la pizarra táctica. No controla futbolistas en tiempo real ni
observa una retransmisión jugable: prepara el partido, pulsa Simular y recibe en menos de un segundo un
informe estadístico completo. El motor resuelve el encuentro por zonas y eventos discretos, combinando
atributos, roles, instrucciones, cohesión, forma física y variabilidad controlada.




### 2.2 Diferenciación

El juego busca superar una simulación basada casi exclusivamente en la media global. La fuerza se calcula por
sector, carril, rol y contexto. La táctica no garantiza la victoria, pero una ventaja estructural debe traducirse en
una mayor probabilidad de generar ocasiones de calidad.

### 2.3 Bucle principal

- 1. Planificar plantilla, objetivos, presupuesto y dorsales.

- 2. Explorar mercado, asignar ojeadores y negociar incorporaciones o salidas.

- 3. Entrenar colectivamente e individualmente, gestionando carga y recuperación.

- 4. Analizar al rival y configurar once, formación, roles e instrucciones.

- 5. Gestionar moral, promesas, charla y reglas de sustitución.

- 6. Simular el partido y revisar resultado, eventos, xG, métricas y calificaciones.

- 7. Procesar finanzas, noticias, sanciones, lesiones y avance al siguiente evento.


### 2.4 Alcance por versión

Módulo
MVP v1.0
Post-lanzamiento
Competiciones

1 país, 2 divisiones de 20 clubes y 1 copa; datos

ficticios editables
15+ países, competiciones continentales y
selecciones
Motor
Simulación zonal, xG, eventos discretos y ratings
Clima, árbitros con tendencias y rivalidades
avanzadas
Mercado
Traspasos, libres, cesiones simples, salarios y
cláusulas
Opciones de compra, intercambios, porcentajes
de futura venta y agentes complejos
Cantera
Plantilla Sub-21 abstracta y generación anual de
regens
Ojeo juvenil regional, torneos y mentoría
Editor
Nombres, atributos y escudos básicos
Paquetes comunitarios JSON/SQLite y editor de
formatos
### 3. Modos de juego y progresión
### 3.1 Carrera

Modo Club Directo: selección de cualquier equipo disponible. Cada club expone expectativas, presupuesto,
reputación y estabilidad directiva. Modo Desempleado: el mánager comienza con reputación 15/100 y
Licencia Nacional C. Solo clubes con vacantes, crisis o bajo prestigio pueden ofrecer entrevistas iniciales.

### 3.2 Dificultad

Parámetro
Amateur
Profesional
Élite / Realista
Presupuesto inicial
+25% sobre base
100% base
-15% sobre base
Paciencia directiva
Alta; tolera desviación de -15
puntos
Media
Baja
IA de mercado
Ofertas espaciadas; puede
sobrepagar
Equilibrada
Agresiva y negocia al límite
Velocidad de ojeo
+50%
Base
-25%
Riesgo de lesiones
0,75×
1,00×
1,20×
[v1.3·R] Aclaración de coherencia. Los parámetros de esta tabla son públicos, se muestran al elegir
dificultad y se aplican únicamente al club del usuario y a su entorno de negociación. No constituyen «ventajas
ocultas» ni modifican atributos, dinero o lesiones de los clubes controlados por la IA (secciones 21 y 28). El
riesgo de lesiones de la tabla es un multiplicador visible sobre el riesgo diario de la sección 22.

### 3.3 Tiempo y calendario

El tiempo avanza en unidades de un día. El usuario puede avanzar hasta el siguiente evento crítico: partido,
cierre de mercado, vencimiento contractual, respuesta a oferta o fecha financiera.
[v1.3·R] Corrección de calendario. Una liga de 20 equipos a doble vuelta requiere 38 jornadas (19 por
vuelta). La v1.2.0 asignaba 18 semanas por vuelta (7–24 y 29–46), es decir, 36 jornadas. La v1.3.0 recoloca las
vueltas en 19 semanas cada una, deja un parón invernal de dos semanas y sitúa las rondas de copa entre
semana.



Semanas
Actividad
1–6
Pretemporada, mercado de verano y amistosos
7–25
Primera vuelta de liga (jornadas 1–19, fin de semana) y rondas iniciales de copa (miércoles)
26–27
Parón invernal: sin jornada de liga; amistosos opcionales; ventana invernal abierta (semanas 25–28)
28–46
Segunda vuelta de liga (jornadas 20–38) y fases finales de copa (miércoles)
47–52
Cierre fiscal, premios, retiros, regens, ascensos/descensos y reputación
Regla de calendario: la liga se juega en fin de semana y la copa en miércoles; el generador de calendario debe
garantizar al menos 3 días entre dos partidos de un mismo club y registrar cualquier excepción. El Vertical
Slice (10 clubes, 18 jornadas) usa una jornada por semana en las semanas 7–24 y 29–46, sin copa. El
calendario detallado de la copa está en la sección 23.2.

### 3.4 Confianza directiva y despido

La Confianza Directiva Cd pertenece a [0,100] y comienza en 60. Se actualiza después de cada partido oficial
mediante puntos obtenidos frente a puntos esperados, ponderación de competición y modificadores
financieros y de estilo. Se limita a [0,100].
ΔCd = (Pobtenidos − Pesperados) × Wcomp + Mfinanzas + Mestilo
Los puntos esperados se estiman a partir de reputación, localía y contexto competitivo, no solo del OVR.
Despido: Cd inferior a 20 durante dos jornadas consecutivas o incumplimiento matemático del objetivo
mínimo. La reputación del mánager se conserva con penalización provisional de 10 puntos. Ofertas superiores:
pueden aparecer con Cd superior a 80 y reputación del mánager al menos 10 puntos por encima de la
reputación del club, siempre que exista una vacante y el club pueda asumir la contratación.
[v1.3·R] Calibración inicial provisional de Cd. Para que el desarrollador no invente coeficientes:
- Puntos esperados: d = Repclub − Reprival + 6 (local) o −6 (visitante); s = 1 / (1 + 10−d/40); Pesperados =
0,37 + 2,0 × s. Rango [0,37; 2,37]. Con reputaciones iguales: 1,54 puntos de local y 1,20 de visitante; la
suma por partido (2,74) coincide con el total de puntos repartidos con ~27% de empates.
- Pesos: Wliga = 2,0; Wcopa = 1,2 (ganar la eliminatoria = 3 puntos, perderla = 0, incluida la tanda de
penaltis); amistosos = 0.
- Mestilo ∈ [−1, +1] por partido (coherencia entre el estilo prometido a la directiva y el mostrado). Mfinanzas
∈ [−1, +1] por partido; los eventos de la sección 8.4 (−15 por balance negativo) son penalizaciones
puntuales aparte.
- Tope por partido: ΔCd ∈ [−8, +8]. Ejemplos verificados: derrota en casa ante un rival de igual reputación
= −3,08; victoria como visitante ante un favorito con +30 de reputación = +4,81.
Los pesos son hipótesis: la prueba de aceptación es que un club de expectativa media tarde entre 12 y 20
partidos de rendimiento muy inferior al esperado para caer bajo 20 puntos, y que un rendimiento superior
sostenido alcance 80 en torno a 20 partidos.
### 4. Jugadores, atributos y rendimiento
### 4.1 Atributos visibles

Escala 1–99. Los 18 atributos originales se mantienen: técnicos (PAS Pase, REG Regate, FIN Finalización, TIL
Tiro lejano, ENT Entrada, MAR Marcaje, AER Juego aéreo, POR Parada); físicos (VEL Velocidad, RES Resistencia,
FUE Fuerza, AGI Agilidad); mentales (VIS Visión, POS Posicionamiento, CON Concentración, AGR Agresividad,
LID Liderazgo, SER Serenidad). POR es específico de porteros; en jugadores de campo se mantiene en un rango
bajo de referencia.

### 4.2 Atributos ocultos

POT Potencial máximo; LES propensión a lesiones; ADA adaptabilidad; LEA lealtad; PRO profesionalidad. POT
nunca es inferior al OVR actual. Los atributos ocultos se muestran inicialmente como desconocidos y se revelan
por scouting o convivencia deportiva.

### 4.3 Revelado de atributos

Un informe estándar revela el 100% de los atributos visibles tras 7 días. Tras 21 días puede mostrar etiquetas
de atributos ocultos, no necesariamente valores exactos: Profesional Modélico, Propenso a lesiones, Leal,
Adaptación lenta, etc. Los jugadores propios revelan tendencias de forma orgánica tras 10 partidos oficiales,
con información gradual.




### 4.4 OVR, rendimiento efectivo y rating: separación obligatoria

OVR es una medida estable de capacidad base en una posición/rol de referencia. No cambia directamente por
moral, fatiga o charla. El rendimiento efectivo es una estimación contextual para el partido. El rating
postpartido mide lo que el jugador hizo en ese encuentro. Estos tres conceptos no deben mezclarse.
OVR posicional: suma ponderada de los atributos definidos para cada posición/rol, con pesos que suman 1,00.
El resultado se redondea a entero y se calcula desde atributos base. Los pesos por rol de la especificación
original se conservan como perfiles iniciales (Anexo A). Ejemplo de central marcador: OVR = 0,25 ENT + 0,25
MAR + 0,20 AER + 0,20 FUE + 0,10 CON.
Posición principal: familiaridad 100%. Secundaria: 92% o 85% según afinidad definida en el editor. Posición
desconocida: penalización posicional inicial de hasta 30%, modulada por familiaridad entrenada. No se aplica
un −30% indiscriminado a todos los atributos; se aplica al componente posicional y de ejecución del rol.

### 4.5 Modificadores de forma física y moral

FIT y MOR se mantienen en [0,100]. La forma física inicial de pretemporada se ajusta a un objetivo de 75, con
variación por edad, descanso y carga. Un jugador descansado puede alcanzar 90–100. Los rangos son guías de
balance, no estados rígidos.
ΔFITpartido = − (Minutos / 90) × (28 − RES / 6) × Mintensidad
[v1.3·R] Recuperación diaria acotada. El rango de referencia +6 a +11 por día se formaliza para evitar
recuperación instantánea:
Recdía = clamp[ (6 + 5 × RES/99) × Fedad × (1 + 0,015 × Nvmédicas) × Fact , 2 , 11 ]     (× 0,5 si FIT > 90)
Factor
Valores (provisionales)
F_edad
≤24 años: 1,05 · 25–29: 1,00 · 30–33: 0,92 · 34+: 0,85
F_act (actividad del día)
Descanso 1,00 · entrenamiento suave 0,65 · normal 0,40 · doble sesión 0,20 · día de
partido 0
Caso (verificado por código)
FIT inicial
Recuperación/día
Veterano, RES 40, médicas 1, descanso

50

7,5
RES 70, médicas 5, descanso

60

10,3
RES 90, médicas 10, descanso

60

11,0
RES 70, médicas 5, entrenamiento suave

60

6,7
RES 70, médicas 5, FIT 95

95

5,1
Factor FIT: 1,00 si FIT ≥ 80; si FIT < 80, factor = max(0,70; 1 − (80 − FIT) × 0,006). Se aplica al rendimiento
efectivo, con intensidad proporcional y sin reducir OVR. El suelo de 0,70 se alcanza en FIT = 30.
FactorMOR = 0,90 + (MOR / 100 × 0,20)
MOR comienza en 70. Victoria +4, derrota −4, rendimiento individual superior a 7,5 +3 como valores base
sujetos a contexto, límites y prevención de acumulaciones excesivas. Moral afecta atributos técnicos y
mentales mediante un factor entre 0,90× y 1,10×.
[v1.3·R] Nota de calibración. El punto neutro de FactorMOR es MOR = 50; con MOR inicial 70 todo jugador
parte con ×1,04. Al ser simétrico entre clubes no desequilibra la liga, pero debe tenerse en cuenta al calibrar
goles por partido (sección 19): las bandas se miden con la moral inicial real, no con un factor teórico de 1,00.

### 4.6 Roles y ponderaciones

Se conservan los perfiles originales: portero clásico/líbero; central marcador/con salida; lateral
defensivo/carrilero; pivote destructor/organizador; box-to-box; enganche; extremo clásico/delantero interior;
delantero avanzado/objetivo/presionante. Cada perfil pondera atributos específicos y suma 1,00 (Anexo A;
invariante verificado por código para los 15 perfiles).

### 4.7 Personalidad y rasgos

Se agregan rasgos conductuales no redundantes con los atributos numéricos. Ejemplos: Líder (estabiliza
vestuario), Ambicioso (busca progresión y puede pedir salida), Disciplinado (mejor aprovechamiento del
entrenamiento), Temperamental (reacciones más intensas), Profesional, Leal, Competitivo, Adaptable y
Reservado. Un jugador puede tener varios rasgos con intensidad y contexto.



Los rasgos no deben convertirse en bonificaciones permanentes arbitrarias. Deben activar eventos o modificar
decisiones: negociación, moral, adaptación, respuesta a promesas y comportamiento en partidos. Los rasgos
Líder y Profesional describen conductas observables; no equivalen a un valor oculto que contradiga una
estadística numérica.

### 4.8 Química y relaciones

La cohesión colectiva depende de continuidad de alineación, resultados, liderazgo, idioma/adaptación,
relaciones y estabilidad de roles. Las relaciones individuales pueden ser positivas, neutras o tensas. Se
representan como modificadores pequeños y contextuales, no como una suma capaz de dominar los atributos.
La cohesión no debe premiar mantener siempre el mismo once sin coste: la rotación puede reducir familiaridad
temporal, pero mejora recuperación y satisfacción de suplentes.
[v1.3·A] Formalización numérica (provisional).
- Cohesión C ∈ [0,100]; inicio de pretemporada 50. Se actualiza tras cada partido oficial:

ΔC = clamp[ 4 × (K − 0,6) + 1,0 × R − 0,08 × (C − 50) , −5 , +5 ]
K = proporción de titulares repetidos respecto al partido anterior (0–1); R = +1 victoria, 0 empate, −1 derrota.
El último término devuelve C hacia 50, por lo que repetir el once no escala sin límite. Eventos puntuales:
cambio de entrenador −10; venta de un jugador clave −5; victoria en derbi +3.
FCOH = 0,97 + 0,06 × C / 100          (rango teórico 0,97–1,03)
K sostenido (R medio 0)
C de equilibrio
F_COH
1,00 (siempre el mismo once)

70

1,012
0,80

60

1,006
0,60 (rotación normal)

50

1,000
0,40 (rotación intensa)

40

0,994
1,00 con 30% de victorias netas
≈74
1,014
Resultado de diseño: el bono práctico máximo de cohesión es ≈ +1,4%, y el techo teórico de +3% requiere C =
100, que no se alcanza solo con continuidad. La rotación completa cuesta como máximo −0,6% frente a la
rotación normal, compensada por recuperación y satisfacción de suplentes.
- Relaciones: Rij ∈ {−1, 0, +1}. Cada relación activa entre dos jugadores del once en líneas adyacentes
aporta ±0,5% a ambos; el total por jugador se limita: FREL,i = 1 + clamp(0,005 × Σj Rij, −0,015, +0,015).

### 4.9 Cadena canónica de rendimiento efectivo [v1.3·R]

Para que cada sistema entre una sola vez y ningún modificador contextual domine los atributos, el rendimiento
efectivo se calcula siempre en este orden (OVR nunca se modifica):
aef = abase × FFIT × FMOR (solo técnicos y mentales) × Fsoft
Fsoft = clamp( FCOH × FREL,i × FTAC , 0,90 , 1,04 )
Rendef,rol = [ Σ wi × aef,i ] × Mfam          con Ftotal = FFIT × FMOR × Fsoft acotado a [0,60 ; 1,15]
FTAC (familiaridad con el esquema) y Mfam (familiaridad con el rol) están definidos en la sección 5.1. En el
Vertical Slice FCOH, FREL y FTAC se fijan en 1,00 mediante interruptores de característica (feature flags) hasta
que sus pruebas estén aprobadas (sección 16, Fase 2).
### 5. Posiciones, familiaridad y desarrollo
### 5.1 Familiaridad individual

Cada jugador mantiene familiaridad por posición, rol y esquema de instrucciones. La posición secundaria no
equivale automáticamente a dominar todos los roles de esa posición. Entrenamiento y minutos en una función
aumentan la familiaridad; cambios constantes pueden ralentizarla.
[v1.3·A] Formalización numérica (provisional).
- Familiaridad de rol Frol ∈ [70, 100] (%). Es el multiplicador Mfam de la sección 4.9. Inicio: posición
principal 100; secundaria 92 u 85 según el editor; posición desconocida 70 (equivale a la penalización
máxima de 30% de la sección 4.4).



- Crecimiento: +1,0 punto por 90 minutos jugados en ese rol (proporcional a los minutos) × Fadap, con Fadap
= clamp(ADA / 70; 0,6; 1,3); +0,5 por día de entrenamiento individual con ese rol como objetivo. Si el
jugador cambia de rol más de 3 veces en 10 partidos, el crecimiento se multiplica por 0,5.
- Decaimiento: −0,1 punto por día sin usar el rol tras 60 días, con suelo en el valor inicial de ese rol.

- Familiaridad de esquema (equipo) T ∈ [0,100], por configuración de formación + ejes: +2 por partido
jugado con ella; +0,5 por día con enfoque colectivo de cohesión táctica; −0,2 por día sin usarla; cambiar un
eje táctico resta 5, cambiar de formación parte de la familiaridad previa de la nueva formación.
- FTAC = 0,96 + 0,04 × T / 100 → rango 0,96–1,00: la falta de familiaridad penaliza hasta −4%, pero la
máxima familiaridad no otorga bonificación por encima de 1,00.

### 5.2 Progresión mensual

El crecimiento se procesa al cierre de cada mes. La fórmula original se conserva como modelo inicial, pero se
separa por familia de atributos y se limita para evitar saltos excesivos.
ΔAmes = ((POT − OVR) / 20) × Fedad × Fminutos × (PRO / 70) × Binstalaciones
Fminutos = 0,4 + 0,6 × (minutos_mes / 360), con límite superior configurable (valor inicial provisional:
minutos_mes/360 ≤ 1,0). El crecimiento se reparte entre atributos entrenados y relevantes para el rol; no
todos aumentan a la vez.

### 5.3 Curvas por edad

La edad no determina por sí sola el destino del jugador. POT, PRO, minutos, entrenamiento, lesiones y posición
producen variación. El potencial es techo estimado, no promesa.
Edad
Técnicos
Físicos
Mentales
16–20
Crecimiento alto
Crecimiento alto
Moderado
21–24
Alto
Pico / consolidación
Alto
25–29
Moderado
Estable
Alto
30–32
Bajo
Declive leve
Estable o moderado
33–35
Declive gradual
Declive
Declive leve
36+
Declive gradual
Declive
Declive gradual

### 5.4 Academia Sub-21 y regens

Plantilla reserva abstracta de hasta 18 jugadores. En semana 48, academia nivel 1–10 genera 3–5 candidatos
de 16–17 años. El mánager decide firmarlos o descartarlos. Cuando se retira un jugador del mundo, se genera
un nuevo futbolista de 16–17 años en un club del mismo país, ponderado por nivel de cantera, para estabilizar
la población y distribución de talento. La generación global debe controlar la inflación de OVR: distribución por
país, edad, reputación y academia; límites de talento excepcional; y seguimiento de población activa por
posición.
[v1.3·A] OVR inicial de los regens. Para evitar que un recién generado desplace a titulares adultos, el OVR
(calculado siempre desde atributos y pesos, nunca asignado) se acota a un rango global de 42–56 según el
nivel de academia Nv ∈ [1,10]:
- OVRregen ~ normal truncada con media μ = 45 + 5 × (Nv − 1)/9, σ = 2,5, límites [42 ; máxNv], con máxNv =
redondeo(50 + 6 × (Nv − 1)/9) (50 en Nv 1 y 56 en Nv 10).
- El OVR máximo (56) queda en el rango bajo de los titulares de un candidato al descenso (54–59, sección
20.3): solo un regen excepcional de una academia nivel 10 puede aspirar a un puesto en el once de ese
perfil de club.
[v1.3·R] POT inicial y control poblacional. POT = OVR + brecha, con brecha ~ normal truncada, media 17
+ 0,6 × (Nv − 1), σ = 4,5, límites [6 ; 32], y POT máximo 92. El valor «mundial» se vigila con un controlador
suave: cada temporada se compara la media de OVR de los jugadores de 25–29 años con su banda objetivo y,
si deriva más de ±1 punto, se aplica a la media de la brecha un ajuste ε ∈ [−3, +3] (en ambos sentidos: la
población puede deflacionar y no solo inflarse). Tabla de calibración verificada por simulación (300.000 regens
por nivel, semilla fija):
Nv academia
Rango OVR
OVR medio
POT medio
P(POT ≥ 80)
P(POT ≥ 85)

1

42–50
45,4
62,5
0,01%
0,000%

3

42–51
46,2
64,5
0,07%
0,000%



Nv academia
Rango OVR
OVR medio
POT medio
P(POT ≥ 80)
P(POT ≥ 85)

5

42–53
47,3
66,6
0,40%
0,000%

8

42–55
48,9
70,0
2,53%
0,044%

10

42–56
49,9
72,2
6,71%
0,281%
Rasgo de diseño: incluso en Nv 10, la probabilidad de un regen con POT ≥ 85 es inferior al 0,3%, coherente con
el control de talento excepcional de la sección 20.3.
### 6. Gestión deportiva, táctica y entrenamiento
### 6.1 Convocatoria

La convocatoria estándar contiene 18 jugadores: 11 titulares y 7 suplentes. Debe incluir al menos un portero
titular y uno suplente. Máximo de 5 extracomunitarios inscritos y 3 simultáneos en el once, sujeto a reglas
configurables por competición. Dorsales 1–99 manuales o autoasignados. Si una expulsión o lesión deja una
posición sin reemplazo, el motor debe aplicar una solución de contingencia: reubicar jugador compatible,
permitir jugar con menos futbolistas o dejar vacante la posición según reglas del deporte. Nunca debe crear un
suplente inexistente.

### 6.2 Formaciones y coordenadas

El campo utiliza coordenadas normalizadas x,y entre 0 y 100. Se ofrecen 12 formaciones predefinidas (4-3-3,
4-4-2, 4-2-3-1, 3-5-2, 5-3-2, 4-1-4-1 y otras) y edición libre dentro de zonas válidas. La ubicación influye en
ocupación espacial, distancias y coberturas, no solo en la etiqueta de formación.
[v1.3·A] Sistema de referencia. Siempre desde la perspectiva del equipo propietario: y = eje longitudinal (0
= su propia portería, 100 = portería rival); x = eje transversal (0 = su banda izquierda, 100 = su banda
derecha). Los cortes se definen con intervalos semiabiertos, sin solapamientos:
Eje
Región
Intervalo
y (sector)
Defensa

0 ≤ y < 35

y (sector)
Mediocampo

35 ≤ y < 70

y (sector)
Ataque

70 ≤ y ≤ 100

x (carril)
Izquierdo

0 ≤ x < 33

x (carril)
Central

33 ≤ x < 67

x (carril)
Derecho

67 ≤ x ≤ 100

Los límites completos (cuadrícula de 9 celdas sector × carril) se usan para estadísticas y el mapa de zonas del
MatchReport. Para el cálculo de fuerza zonal se usa pertenencia suave (ver sección 10.4) para evitar saltos
discretos cuando un jugador se mueve un solo punto. Los carriles se reflejan al enfrentar equipos: el carril
derecho de A (x ≥ 67) se enfrenta al carril izquierdo de B (x' = 100 − x). El portero no entra en las sumas por
sector: su aportación se resuelve mediante POR/AGI en la conversión de gol (sección 10.6).

### 6.3 Cinco ejes tácticos originales

Eje
Opciones
Efecto
Mentalidad
Muy defensiva, defensiva, equilibrada, ofensiva,
sobrecarga
Desplaza altura media del bloque y modifica
riesgo ofensivo/defensivo
Pase
Muy corto, corto, mixto, directo, largo
Afecta progresión, pérdidas, posesión y transición
Presión
Bloque bajo, media, alta, gegenpressing
Aumenta recuperaciones y desgaste/faltas
Línea defensiva
Muy atrás, normal, adelantada
Comprime espacios o expone la espalda
Amplitud
Estrecha, normal, abierta
Redistribuye ocupación entre carriles

### 6.4 Instrucciones colectivas ampliadas

Se agregan ritmo (lento/normal/rápido), transición (conservar/contraatacar), construcción (desde
atrás/mixta/directa), tipo de centro (bajo/mixto/elevado), marcaje (zonal/individual), trampa del fuera de juego
(activada/desactivada), libertad creativa (restringida/equilibrada/libre) y temporización (normal/reducir ritmo).
No todas las combinaciones son compatibles. El motor debe modelar tensiones: presión alta con bloque muy
bajo, línea adelantada sin presión coordinada, ritmo alto con pase muy corto bajo presión, o amplitud abierta
sin jugadores capaces de ocupar bandas. Las incompatibilidades generan riesgos, no bloqueos arbitrarios.



[v1.3·A] Penalizaciones numéricas de incompatibilidad (provisionales). Los efectos se aplican a
variables intermedias del motor (no directamente al xG) multiplicados por la severidad s; el acumulado sobre
una misma variable no supera 12% relativo. La UI muestra una alerta «Tensión táctica» con la causa.
ID
Condición
Severidad s
Variable afectada (efecto a s = 1)
I1
Presión gegenpressing + línea Muy atrás (alta
+ Muy atrás)
1,0 (0,75)
Eficacia de presión −8%; progresión rival por
carril central +6%
I2
Línea adelantada + presión Bloque bajo
(adelantada + media)
1,0 (0,5)
Cobertura de la espalda −8%; probabilidad de
pase al espacio rival +8%
I3
Ritmo rápido + pase Muy corto (corto) ante
presión rival alta/gegenpressing
1,0 (0,5)
Pérdidas en salida de balón +10% relativo
I4
Amplitud abierta con ocupantes de banda de
(VEL+RES)/2 < 55 (55–62)
1,0 (0,5)
Cobertura del carril lateral −6%
Criterio de prueba: cada incompatibilidad severa, aislada en escenarios diseñados, debe mover el xG o los tiros
del equipo afectado entre 3% y 8% relativo y en la dirección esperada (coherente con la sección 19).

### 6.5 Instrucciones individuales

Cada posición puede tener instrucciones contextualizadas. Laterales: quedarse, incorporarse, solaparse o
cerrarse. Mediocentros: mantener, cubrir espacios, presionar organizador o progresar pase. Delanteros: atacar
espacio, jugar de espaldas, caer a banda o iniciar presión. Extremos: abrir campo, diagonal interior o asistir
desde línea de fondo. Las instrucciones individuales modifican decisiones y zonas de influencia; no deben ser
simples multiplicadores acumulativos. Se limita la cantidad de instrucciones simultáneas para conservar
legibilidad.

### 6.6 Balón parado

Asignación de lanzadores de penaltis, faltas directas, faltas laterales y córners. En ataque: primer palo,
segundo palo, punto de penalti, borde del área y transición defensiva. En defensa: marcaje
zonal/individual/mixto y jugadores asignados. AER, FIN, PAS, SER, POS, FUE y altura influyen según la jugada.

### 6.7 Sustituciones automáticas

Hasta tres reglas prepartido en el MVP, ampliables según reglamento. Ejemplos: minuto 65, sustituir FIT < 60
por suplente de misma posición; minuto 70, si voy perdiendo, retirar un MCD e introducir un DC. Las reglas se
evalúan en orden, validando elegibilidad, disponibilidad, lesiones, expulsiones y límite reglamentario.

### 6.8 Entrenamiento

Carga colectiva 70% e individual 30%. Enfoques colectivos: cohesión táctica, acondicionamiento físico,
técnica/posesión y defensa/balón parado. Enfoque individual: rol concreto y atributos objetivo. Intensidad
suave/normal/doble sesión altera desarrollo, FIT y riesgo de lesión. Se deben establecer días de recuperación y
límites de carga acumulada.

### 6.9 Vestuario y objetivos individuales

Charlas: calma/sin presión, exigir victoria, motivación épica para derbis/finales y elogiar racha. Sus efectos
dependen de reputación, personalidad, PRO, LID, contexto y resultado; ningún discurso debe garantizar un
bonus. Estatus contractual: jugador clave, titular habitual, rotación o promesa. Si un jugador clave disputa
menos del 60% de minutos en un bloque de cinco partidos, puede generar queja. El mánager puede explicar,
prometer minutos, renegociar rol o declararlo transferible. Se añaden objetivos personales: delantero que
busca una cifra de goles, juvenil que quiere debutar, veterano que busca un título o mediocampista que quiere
minutos. Las metas son negociables, visibles y afectan moral/confianza si se incumplen.
### 7. Informe del rival y scouting
### 7.1 Informe del rival

El segundo entrenador genera formación y once probable a partir de los últimos tres partidos, jugador
peligroso, medias por línea, vulnerabilidades y contramedidas. La precisión depende del nivel del staff y de la
cantidad/calidad de datos disponibles. El informe debe expresar incertidumbre: probable, observado o
confirmado. Ejemplos de diagnóstico: centrales lentos vulnerables a pases al espacio; fatiga por calendario;
debilidad en defensa de centros; dependencia de un creador; espacio entre mediocampo y defensa.




### 7.2 Scouting

Filtros: posición, rol, edad, valor, contrato, atributos conocidos y situación contractual. Alcance: división actual,
país, continente o mundo según red de ojeo nivel 1–4. Cada ojeador sigue hasta tres jugadores o explora una
región/perfil. La incertidumbre de atributos se muestra como rangos. El informe de 7 días revela atributos
visibles y el de 21 días revela etiquetas de atributos ocultos. El jefe de ojeadores modifica velocidad,
simultaneidad y precisión.
### 8. Mercado, contratos y economía
### 8.1 Operaciones

Traspaso directo con pago al contado o hasta tres plazos anuales. Una cláusula de rescisión ejecutable impide
que el club rechace una oferta que cumpla íntegramente sus condiciones. Cesión simple por una temporada
con porcentaje salarial de 0–100% y prima opcional. Agentes libres disponibles sin coste de traspaso. Contrato
del jugador: salario semanal, duración de 1–5 años, rol prometido, prima de fichaje, bonos por goles/porterías a
cero y cláusula de rescisión.

### 8.2 Agentes con personalidad

Los agentes dejan de ser intermediarios neutros. Tipos: exigente (maximiza salario/primas), ambicioso (busca
prestigio), leal (prioriza estabilidad), negociador (maximiza valor) y desarrollador (prioriza minutos/proyecto
juvenil). Un agente puede representar a varios futbolistas y la relación con el club puede facilitar o dificultar
futuras negociaciones.

### 8.3 IA de mercado

Al inicio de cada ventana, la IA detecta hasta dos posiciones débiles, reemplaza salidas/retiradas y filtra
candidatos por presupuesto, edad, reputación y necesidades. Clubes formadores priorizan edad ≤ 27;
aspirantes priorizan rendimiento inmediato. Clubes endeudados pueden listar veteranos con salarios altos y
pocos minutos.

### 8.4 Finanzas

Se mantienen dos bolsas: presupuesto de fichajes y presupuesto salarial semanal. Un deslizador transfiere
fondos entre ellas con comisión del 5%. Ingresos: taquilla, patrocinios, TV y ventas. Gastos: salarios, cuotas,
mantenimiento y amortización. Los ingresos por venta liberan 70–90% al presupuesto de fichajes según salud
financiera. Si el balance cae bajo cero, la directiva puede activar préstamo automático al 8% anual, bloquear
fichajes pagados y reducir Confianza Directiva en 15 puntos. Si deuda supera 150% del valor patrimonial, la
regla original contempla sanción de −9 puntos; debe configurarse por reglamento y no aplicarse
silenciosamente.

### 8.5 Patrocinios y objetivos

Patrocinio fijo anual más bonos por campeonato, ascenso o permanencia; duración de 1–3 temporadas. La
reputación, división, tamaño de afición y rendimiento influyen en ofertas y renovaciones.

### 8.6 Valor de mercado y salario esperado (fórmulas base) [v1.3·A]

Fórmulas iniciales provisionales, en unidades monetarias enteras del juego, que traducen OVR, edad, POT y reputación en un precio de referencia y un sueldo semanal de referencia. Los parámetros V0 y W0 son configurables y deben calibrarse con la prueba económica de 20 temporadas (sección 24.1).

**Valor**

$$
Valor = V_0 \times r^{(OVR-50)} \times F_{edad} \times F_{pot} \times F_{pos} \times F_{rep} \times F_{cont}
$$

Parámetros: V0 = 250.000 · r = 1,16 · OVR acotado a [30, 95] · valor mínimo 5.000.

- Fedad (interpolación lineal entre nodos): 16→1,00 · 18→1,10 · 21→1,10 · 24→1,05 · 27→1,00 · 29→0,85 · 31→0,62 · 33→0,38 · 35→0,20 · 38+→0,10.
- Fpot = min(2,0 ; 1 + 0,04 × (POT − OVR) × wedad), con wedad = 1 hasta 21 años, 1 − (edad − 21)/6 entre 21 y 27, y 0 desde 27.
- Fpos: POR 0,85 · DEF 0,95 · MED 1,00 · DEL 1,10.
- Frep = 0,95 + 0,10 × Repclub vendedor/100.
- Fcont (años restantes y): 0 si y ≤ 0 (agente libre, sin coste de traspaso); si no, 0,25 + 0,75 × min(y, 3)/3.
- Precio pedido por la IA = Valor × Mclub, con Mclub ∈ [0,85 ; 1,35] según salud financiera y necesidad deportiva.
- Cláusula de rescisión sugerida: 2,0×–3,0× el valor.

**Salario**

$$
Salario_{sem} = W_0 \times k^{(OVR-50)} \times S_{edad} \times S_{pot} \times S_{rep} \times S_{rol}
$$

Parámetros: W0 = 800 · k = 1,14 · salario mínimo 150 por semana.

- Sedad: ≤20 → 0,70 · 21–23 → 0,90 · 24–29 → 1,00 · 30–32 → 0,95 · 33+ → 0,80.
- Spot = min(1,25 ; 1 + 0,012 × (POT − OVR) × wedad).
- Srep = 0,85 + 0,30 × Repclub/100.
- Srol (rol prometido): clave 1,15 · titular 1,00 · rotación 0,88 · promesa 0,75.
- Salario pedido = Salariosem × Magente: exigente 1,10 · ambicioso 1,05 · negociador 1,05 · leal 0,95 · desarrollador 0,95 (hipótesis).
- Prima de fichaje de referencia: 8 semanas de salario pedido.

| OVR | Edad | POT | Pos | Rep | Años | Rol | Valor | Sueldo/sem | Valor / sueldo anual |
|---:|---:|---:|---|---:|---:|---|---:|---:|---:|
| 52 | 31 | 52 | DEF | 35 | 2 | rotacion | 146.376 | 830 | 3,4 |
| 60 | 28 | 61 | MED | 50 | 2 | titular | 765.108 | 2.966 | 5,0 |
| 66 | 24 | 70 | DEL | 55 | 3 | titular | 3.368.524 | 6.766 | 9,6 |
| 70 | 27 | 71 | DEF | 60 | 3 | titular | 4.668.150 | 11.325 | 7,9 |
| 72 | 19 | 88 | MED | 50 | 4 | promesa | 11.810.065 | 8.942 | 25,4 |
| 78 | 26 | 80 | DEL | 75 | 3 | clave | 18.527.259 | 38.928 | 9,2 |
| 85 | 28 | 85 | DEL | 90 | 4 | clave | 47.702.088 | 101.082 | 9,1 |
| 70 | 33 | 70 | MED | 60 | 1 | rotacion | 933.630 | 7.973 | 2,3 |

Verificaciones por código: (1) el valor es estrictamente creciente en OVR en todo el dominio 30–95; (2) para jugadores de 22–28 años con OVR 55–90 la razón valor / sueldo anual queda entre 6,1 y 14,8; (3) los veteranos con poco contrato restante tienen razones bajas (2–4) y las promesas muy jóvenes razones altas (≈25), como es esperable.

### 9. Club, staff e infraestructura

### 9.1 Instalaciones

Las fórmulas económicas originales se conservan como parámetros iniciales. Debe verificarse que el coste
crezca de manera sostenible y que los niveles 1–10 sean alcanzables en carreras largas.
Instalación
Efecto
Coste de mejora / mantenimiento
Estadio
Aforo de 5.000 a 85.000; taquilla semanal
Fórmula original: 1,5 M × 1,6Nv; 15 k × Nv semanal
Entrenamiento
Bonus desarrollo 1,00×–1,45×

800 k × 1,5Nv; 10 k × Nv semanal

Academia
Potencial y candidatos juveniles

600 k × 1,5Nv; 8 k × Nv semanal

Médicas
Baja hasta −35%; recuperación FIT

500 k × 1,45Nv; 7 k × Nv semanal

[v1.3·R] Alerta de escala económica (decisión pendiente). El mantenimiento original suma 40 k × Nv por
semana si todas las instalaciones tienen el mismo nivel (p. ej. 120 k semanales en nivel 3). Con las fórmulas
base de la sección 8.6, un jugador titular de OVR 62 cobra ≈ 3.854 por semana, es decir, ≈ 96.358 semanales
para una plantilla de 25. Con esa escala el mantenimiento superaría a la masa salarial. Antes de cerrar la
economía hay que decidir una de dos vías: reescalar W0 y los ingresos, o reducir los coeficientes de
mantenimiento. Criterio de aceptación sugerido (sección 24.1): masa salarial entre 50% y 70% de los ingresos
y mantenimiento total ≤ 25% de los ingresos para un club medio.

### 9.2 Staff

Cuatro puestos clave: segundo entrenador (informe rival y familiaridad), preparador físico (fatiga y riesgo de
lesión), jefe de ojeadores (revelado y simultaneidad) y jefe de servicios médicos (rehabilitación y diagnóstico).
Calificación 1–99 y salario semanal.

### 9.3 Filosofía deportiva del club

Post-MVP: el club puede definir una política de desarrollo: cantera, fichajes jóvenes, experiencia, talento
nacional, venta de promesas o equilibrio. Esta filosofía orienta decisiones de IA, presupuesto, reputación y
expectativas, pero no debe impedir fichajes válidos.



10. Motor de simulación de partidos

### 10. Motor de partido

### 10.1 Filosofía

Motor estadístico discreto, sin representación en vivo. Objetivo de diseño: aproximadamente 75%
planificación/atributos y 25% variabilidad controlada. La tasa de sorpresas en duelos desiguales se calibra
mediante simulación masiva; el rango original aproximado de 12–15% es una hipótesis de balance, no una
garantía universal.
[v1.3·R] Definición operativa. «Duelo desigual» = diferencia de OVR medio del once ≥ +20 a favor del
favorito, en campo neutral; «sorpresa» = victoria del equipo inferior. La banda de aceptación está en la sección

19 (victoria del inferior entre 8% y 15%).


### 10.2 Entradas

- Atributos efectivos de los once titulares y suplentes (cadena de la sección 4.9).

- Tácticas, roles, familiaridad, cohesión y relaciones relevantes.

- Localía, asistencia, moral de afición y distancia de viaje.

- Charla, liderazgo, árbitro, estado del terreno si se incorpora y reglas de sustitución.

- Semilla aleatoria y versión del motor.


### 10.3 Resolución por intervalos

El partido se divide en 18 intervalos de cinco minutos, más una fase de tiempo añadido calculada a partir de
goles, lesiones, sustituciones y eventos. Cada intervalo actualiza fuerza zonal, carriles, posesión, progresión,
ocasiones, eventos, FIT y sustituciones.
[v1.3·R] Prórroga y penaltis (competiciones de eliminatoria). Si persiste el empate en partido único, se
añaden 6 intervalos de cinco minutos (dos tiempos de 15) con el mismo núcleo; el FIT se desgasta con la
fórmula de la sección 4.5 usando los minutos reales (hasta 120) y se permite una sustitución adicional si el
reglamento lo indica. Si sigue el empate, tanda de penaltis: 5 lanzadores por equipo y muerte súbita; la
probabilidad de cada penalti usa FIN/SER del lanzador frente a POR/AGI, acotada con la misma función de la
sección 10.6. Los goles de la tanda no cuentan en estadísticas individuales de goles.

### 10.4 Zonas y posesión

Se agregan atributos efectivos en defensa, mediocampo y ataque, y en carril izquierdo, central y derecho. La
posesión inicial puede estimarse con la fuerza de mediocampo y el estilo de pase:
Ppos(A) = [ Zmed(A) × Mpase(A) ] / [ Zmed(A) × Mpase(A) + Zmed(B) × Mpase(B) ]
La posesión no debe determinar por sí sola la calidad de las ocasiones. La progresión compara fuerza de
ataque en el carril elegido contra defensa, cobertura, presión y espacio.
[v1.3·A] Pertenencia suave a zonas. Con los cortes de la sección 6.2 (sectores en y = 35 y y = 70; carriles
en x = 33 y x = 67) y semiancho de mezcla h = 5, cada jugador de campo aporta a la fuerza de cada zona con
pesos que suman exactamente 1,00:
wdef(y) = clamp[(35 + 5 − y)/10, 0, 1]       wata(y) = clamp[(y − (70 − 5))/10, 0, 1]       wmed(y) = 1 − wdef −
wata
wizq(x) = clamp[(33 + 5 − x)/10, 0, 1]       wder(x) = clamp[(x − (67 − 5))/10, 0, 1]       wcen(x) = 1 − wizq −
wder
Ejemplos verificados: y = 25 → defensa 100%; y = 35 → 50% defensa / 50% mediocampo; y = 38 → 20% / 80%;
y = 70 → 50% mediocampo / 50% ataque; y = 78 → ataque 100%. Los puntos habituales de las formaciones
(defensas y ≈ 25, medios ≈ 50, delanteros ≈ 78) caen fuera de las zonas de mezcla; solo los pivotes
retrasados o mediapuntas (y ≈ 33–38 o 62–72) reparten su aporte. Las pruebas de propiedad verifican que los
pesos sumen 1 y no sean negativos en todo el dominio 0–100 (invariante nuevo, sección 15.4).

### 10.5 Generación de ocasiones y xG

Si la progresión supera un umbral defensivo modulado por variabilidad, se genera un tiro. El xG representa la
calidad de la ocasión antes de incorporar la identidad del rematador: distancia, ángulo, tipo de asistencia,
presión, posición y situación de juego. Rango de referencia inicial 0,03–0,65.




### 10.6 Conversión de gol

La fórmula original xG × ratio FIN/SER frente a POR/AGI puede superar 1 y no es válida como probabilidad
directa. Se reemplaza por modificadores acotados:
P(Gol) = clamp( xG × Mfinalización × Mportero , 0 , 1 )
Los modificadores se calibran alrededor de 1,00 y tienen límites definidos por pruebas. FIN y SER del
rematador alteran conversión; POR y AGI del arquero alteran la probabilidad de atajada. El xG se conserva
como métrica independiente de la calidad individual.

### 10.7 Eventos y comportamiento

Se resuelven pases filtrados, centros, contraataques, pérdidas en salida, recuperaciones altas, segundas
jugadas, faltas, tarjetas, penaltis, córners, lesiones y sustituciones. Los estilos (posesión, gegenpressing,
bloque bajo, contraataque, juego directo, ataque por bandas o interior) emergen de las instrucciones, no de
bonificaciones mágicas. Las faltas y tarjetas dependen de AGR, presión, entrada, agilidad, regate, zona, árbitro
y contexto. Lesiones dependen de carga, FIT, LES, contacto y preparación física; la probabilidad aumenta con
fatiga, pero no debe volverse inevitable.

### 10.8 Semilla reproducible

Cada partido registra semilla aleatoria, versión del motor y snapshot de entradas relevantes. Con iguales
entradas, semilla y versión, el resultado debe ser reproducible. Esto facilita depuración, pruebas de regresión y
análisis de balance.
[v1.3·R] Derivación de semillas. seedpartido = hash(seedmundo, idcompetición, temporada, jornada, idpartido).
Cada partido usa su propia secuencia, de modo que el orden en que se simulan los partidos de una jornada no
altera ningún resultado. La reproducibilidad entre plataformas se especifica en la sección 14.7.

### 10.9 Sustituciones y contingencias

Las reglas se evalúan desde el minuto 60 o el minuto configurado. El motor valida suplentes disponibles,
expulsiones, lesiones, posición y reglamento. Si una regla no puede ejecutarse, se registra la razón y se
continúa sin cambio.

### 10.10 MatchReport y rating

Salida: marcador, goleadores, asistentes, cronología, tarjetas, lesiones, cambios, posesión, tiros, tiros a puerta,
xG, ocasiones claras, pases completados, entradas ganadas, faltas, córners, mapa de zonas (cuadrícula de 9
celdas de la sección 6.2), calificaciones individuales y cambios de FIT/MOR/sanciones.
[v1.3·A] Coeficientes provisionales del rating. Rating inicial 6,5. Rating = clamp(6,5 + s × Σ
contribuciones, 3,0, 10,0), con s = clamp(minutos/60, 0,25, 1) para que un suplente de pocos minutos no
obtenga calificaciones extremas. Cada familia de contribuciones tiene un tope para que ninguna estadística
aislada domine el resultado:
Concepto
Contribución
Tope / nota
Gol
+0,9 por gol
Tope de goles: +2,0
Asistencia
+0,5 por asistencia
Tope: +1,0
Tiro a puerta
+0,05 por tiro
Tope: +0,2
Precisión de pase
1,0 × (pct − 0,78)
Acotado a [−0,3 ; +0,3]
Entradas/intercepciones ganadas
+0,04 por acción
Tope: +0,4
Paradas (portero)
+0,12 por parada
Tope: +0,8
Portería a cero
POR +0,5 · DEF +0,3 · MED +0,1 · DEL 0
Gol encajado (por gol)
POR −0,20 · DEF −0,10 · MED −0,05 · DEL 0
Error que origina tiro / gol
−0,3 / −0,8
Tarjetas
Amarilla −0,3 · roja −1,5
Penalti cometido / fallado
−0,5 / −0,6
Resultado del equipo
Victoria +0,2 · derrota −0,2
No se aplica en empate
Estos coeficientes son hipótesis para que el Vertical Slice tenga una fórmula lista. Verificación numérica
preliminar (Monte Carlo con 60.000 apariciones sintéticas de 90 minutos y tasas de eventos plausibles; no
sustituye a la calibración con el motor real): media global 6,67; desviación típica 0,52; rating ≥ 7,5 en el 6,4%
de las apariciones; rating < 5,0 en el 0,5%; > 8,5 en el 0,4%; medias por posición DEL 6,83, MED 6,67, DEF



6,61, POR 6,54. Bandas de aceptación: media global 6,5–6,9; desviación 0,45–0,65; ≥ 7,5 entre 5% y 10%
(activa el +3 de moral de la sección 4.5); < 5,0 y > 8,5 cada uno ≤ 2%; diferencia entre medias de posiciones
≤ 0,35.

### 10.11 Simulación headless

Partidos ajenos al usuario usan el mismo núcleo estadístico con menos detalle de registro. Conservan
marcador, goleadores, asistentes, tarjetas, lesiones, sanciones y evolución de forma. El resultado debe ser
coherente con el motor completo.
11. Mundo, competiciones y carrera larga

### 11. Mundo persistente y carrera

### 11.1 Mundo persistente

Los partidos paralelos se resuelven sin logs extensos. Los clubes gestionan plantillas, presupuestos,
necesidades y entrenadores. El motor debe evitar que los clubes sin usuario acumulen plantillas inviables o
quiebren de forma sistemática.

### 11.2 Ascensos, descensos y formatos

El MVP define dos divisiones de 20 equipos y una copa nacional. Deben especificarse puntos, desempates,
calendario, cupos, descensos, ascensos, desempates de permanencia y reglas de copa (secciones 23.1 y 23.2).
El formato debe ser editable desde el editor pre-carrera en una versión posterior.

### 11.3 Reputación de clubes y mánager

La reputación del club [1,100] se actualiza mediante media móvil exponencial comparando rendimiento real y
esperado, títulos, ascensos, descensos y estabilidad. La reputación del mánager se amplía con dimensiones
internas: nacional, internacional, táctica, desarrollo juvenil y gestión de plantillas. La interfaz puede mostrar
etiquetas derivadas en lugar de todos los números.

### 11.4 Entrenadores IA

Los mánagers rivales pueden ser despedidos, contratados por otros clubes y cambiar la identidad táctica de los
equipos. La contratación considera reputación, estilo, objetivos, presupuesto y disponibilidad.

### 11.5 Retiro y legado

Jugadores de 33 años o más evalúan retiro según OVR, minutos, PRO, lesiones y ofertas. Leyendas con más de

200 partidos o títulos se incorporan al registro histórico. Se preservan estadísticas aunque el jugador se retire.


### 11.6 Historial individual y récords

Ficha histórica: clubes, temporadas, partidos, titularidades, minutos, goles, asistencias, títulos, premios, mejor
temporada, máximo valor de mercado, debut, retiro, selecciones futuras e historial resumido de lesiones.
Récords de club y competición persisten entre temporadas.

### 11.7 Noticias y prensa

Periódico interno semanal The Touchline Report. Noticias procedurales sobre resultados, sorpresas, rachas,
fichajes, lesiones, despidos, juveniles, crisis económicas y rivalidades. Cada noticia debe derivarse de hechos
del mundo y enlazar a la pantalla relevante. Post-MVP: declaraciones, conferencias y reacciones de
prensa/afición. No se requiere texto libre generado dinámicamente; se priorizan plantillas contextuales con
variables verificables.

### 11.8 Afición y rivalidades

La afición reacciona a resultados, derbis, fichajes, venta de referentes, cantera, situación financiera y
objetivos. La asistencia depende de capacidad, precio, reputación, rival, clima si se implementa y posición.
Rivalidades pueden modificar ambiente y presión, no resultados de forma determinista.
### 12. Editor y personalización
### 12.1 Editor pre-carrera

Permite clonar la base maestra y editar nombres de clubes/jugadores, colores, escudos básicos, edades,
posiciones, atributos y presupuestos. La partida nueva conserva una copia aislada para que cambios
posteriores no alteren carreras existentes.




### 12.2 Importación/exportación

Importación/exportación JSON validada por esquema. Post-MVP: paquetes .sqlite y formatos de liga
comunitarios. Cada paquete incluye versión, identificadores, país, reglas y metadatos. Validar duplicados,
rangos, relaciones, tamaños y referencias antes de importar.

### 12.3 Personalización visual

Colores, patrones, escudos, camisetas y nombres. La personalización debe respetar derechos de terceros: el
usuario es responsable de los datos que importe y el juego no debe distribuir material no autorizado.
### 13. Interfaz y experiencia de usuario
### 13.1 Navegación

Barra inferior persistente con cinco accesos principales y sub-vistas contextuales. Botón superior derecho
Avanzar/Simular, siempre visible cuando la acción sea válida. La arquitectura de pantallas incluye Dashboard,
Plantilla, Táctica, Informe del Rival, Calendario, Mercado/Ojeo, Finanzas, Instalaciones, Staff, Clasificación,
Historial y Noticias (Anexo B).
[v1.3·R] Los cinco accesos principales (propuesta). La v1.2.0 no los enumeraba:
Acceso
Contenido
Inicio
Dashboard, buzón urgente, Calendario y Noticias (The Touchline Report)
Plantilla
Jugadores, Sub-21, contratos, rasgos, forma e historial
Táctica
Formación, once, roles, ejes, sustituciones, Informe del Rival y Ajustes avanzados
Mercado
Búsqueda, Ojeo, ofertas y negociación
Club
Finanzas, Instalaciones, Staff, Clasificación e Historial

### 13.2 Dashboard

Próximo rival, acción principal, confianza directiva, buzón urgente, mini-tabla, estado físico, noticias y tareas
pendientes.

### 13.3 Plantilla

Lista ordenable por OVR, edad, posición, FIT, MOR, contrato, estado médico y salario; pestaña Sub-21; fichas
con atributos, rasgos, objetivos, historial y familiaridad.

### 13.4 Táctica

Vista principal móvil: formación, once, roles, cinco ejes tácticos y sustituciones automáticas. Instrucciones
individuales y balón parado se ubican en Ajustes avanzados colapsables, con valores automáticos sensatos
según formación y rol. El usuario casual puede simular sin abrirlos. El Vertical Slice expone solo dos
formaciones y tres ejes tácticos.

### 13.5 Diseño visual y accesibilidad

Tema oscuro OLED: fondo #0F172A y tarjetas #1E293B. Objetivos táctiles mínimos 48×48 dp; acciones
frecuentes en tercio inferior; fuentes sans-serif tabulares; escalado 100–125%; contraste adecuado y soporte
de lectores de pantalla. La UI renderiza por eventos y bajo demanda. El objetivo de 0 FPS en reposo se valida
según framework y sistema operativo; no debe comprometer notificaciones, animaciones esenciales ni
accesibilidad.
[v1.3·R] Semáforo funcional corregido: verde ≥ 80, ámbar 60–79, rojo < 60 para FIT, MOR y confianza, sin
depender exclusivamente del color (icono o texto adicional). La v1.2.0 decía «verde > 80» y «ámbar 60–79»,
dejando sin estado el valor exacto 80; además, ≥ 80 coincide con el umbral de FIT sin penalización de la
sección 4.5.
### 14. Arquitectura técnica y persistencia
### 14.1 Offline-first

Toda la simulación, datos y generación procedural residen localmente. No se requiere conexión para carrera.
Servicios online futuros no pueden ser dependencias de guardado, simulación o acceso a una partida.




### 14.2 Tecnología

Opciones de implementación: Unity/C#/.NET o motor nativo C++/Rust integrado mediante FFI. La decisión final
debe basarse en prototipo de rendimiento, facilidad de UI, persistencia, herramientas y experiencia del equipo.
Evitar elegir por intuición antes de medir.

### 14.3 Rendimiento

Objetivos: jornada de 10 partidos en menos de 150 ms y 100 partidos en menos de 800 ms en dispositivo
medio. Son metas sujetas a benchmark. Usar estructuras compactas, evitar asignaciones dentro del bucle
crítico, separar simulación de UI y usar hilos/isolates cuando aporte mejora medida. No paralelizar operaciones
pequeñas si el coste de coordinación supera el beneficio. Objetivo RAM inferior a 120 MB y paquete cercano a

150 MB: medir memoria pico, carga, guardado y uso prolongado en Android/iOS. Si Unity impide alcanzar el

objetivo, revisar el presupuesto antes de comprometer la plataforma.
[v1.3·R] Aclaración de presupuestos. El objetivo MVP equivale a ≈ 15 ms por partido en jornadas de 10
partidos y ≈ 8 ms por partido en lotes de 100 (headless). La puerta del Vertical Slice (sección 18.3: 5 partidos
en 150 ms, es decir 30 ms por partido) es deliberadamente más holgada porque incluye un partido con
MatchReport completo; ambos valores deben medirse con percentil 95 y no deben interpretarse como
contradictorios.

### 14.4 Guardados

Cada partida usa archivo SQLite independiente (save_slot_1.db), hasta cinco ranuras y una de autoguardado
semanal. WAL para concurrencia y escrituras seguras. Añadir transacciones, copias de seguridad, validación de
integridad, versionado de esquema y migraciones.

### 14.5 Esquema de datos ampliado

Tablas base originales: clubs, players y matches. Ampliación recomendada: competitions, divisions,
competition_teams, fixtures, standings; player_contracts, player_season_stats, player_attributes_history,
player_injuries, player_suspensions, player_development, training_plans; staff, scouting_assignments,
transfer_offers, sponsorships, club_finances, facility_history; manager_history, club_history, season_awards,
hall_of_fame, news_events, player_relationships y tactical_familiarity. No es obligatorio materializar cada tabla
en el primer prototipo. Sí es obligatorio definir claves, propiedad de datos, relaciones, índices y estrategia de
migración antes de cerrar el formato de guardado.
[v1.3·R] Con las nuevas fórmulas se añaden al esquema los campos: cohesión C del equipo, familiaridad de rol
por jugador (Frol), familiaridad de esquema T (tactical_familiarity), relación Rij (player_relationships),
parámetros de cup_rounds y cup_draws (copa), y seed_mundo con versión del generador.

### 14.6 Integridad y seguridad de datos

- Claves primarias estables y claves foráneas activadas.

- Transacciones para traspasos, renovaciones, pagos y cierre de jornada.

- Restricciones para rangos, estados y relaciones exclusivas.

- Migraciones versionadas y pruebas de migración desde cada versión soportada.

- Importaciones en staging: validar todo antes de modificar la partida.

- Autoguardado con recuperación ante cierre inesperado y verificación de integridad SQLite.


### 14.7 Determinismo multiplataforma [v1.3·R]

«Misma semilla + misma versión = mismo resultado» debe cumplirse también entre Android e iOS y entre
arquitecturas, no solo en un dispositivo. Requisitos:
- Generador pseudoaleatorio propio y documentado (p. ej. PCG32 o xoshiro) implementado en el núcleo;
prohibido usar el RNG de la plataforma o del motor gráfico dentro de la simulación.
- Orden de iteración determinista: sin recorrer diccionarios o conjuntos sin orden explícito; ordenaciones
estables con desempate por identificador.
- Aritmética reproducible: evitar funciones transcendentales dependientes de la plataforma (usar tablas o
aproximaciones propias) y fijar el orden de las operaciones en coma flotante, o usar punto fijo en el bucle
crítico.
- Prueba de regresión «golden»: una jornada y una temporada con semilla fija producen el mismo hash de
resultados en Android, iOS y en el entorno de CI; cualquier cambio de hash exige subir la versión del motor.
- Cada exportación de depuración (sección 26.1) incluye el hash para comparar plataformas.



### 15. Balance, pruebas y criterios de aceptación
### 15.1 Filosofía de balance

No fijar tasas definitivas sin datos. Crear una liga de prueba y simular miles de temporadas con semillas
registradas. Los objetivos estadísticos se definen como bandas configurables por competición, no como
resultados garantizados.

### 15.2 Métricas de calibración

- Goles por partido, distribución de marcadores y frecuencia de empates.

- Ventaja local y relación entre diferencia de OVR y probabilidad de victoria.

- Posesión, tiros, tiros a puerta, xG y conversión por estilo táctico.

- Frecuencia de lesiones, duración de bajas, recaídas y suspensiones.

- Progresión de OVR por edad, potencial, minutos y entrenamiento.

- Inflación salarial, transferencias, valores de mercado y solvencia de clubes.

- Distribución de talento por posición, país y edad tras 10, 25 y 50 temporadas.

- Rotación de entrenadores, despidos, ascensos y estabilidad de clubes.


### 15.3 Pruebas automatizadas

Pruebas unitarias para fórmulas, límites y transiciones; pruebas de integración para partido, mercado, finanzas
y guardado; pruebas de propiedad para invariantes; pruebas de regresión con semillas fijas; pruebas de carga
para jornadas; y pruebas de larga duración para carreras extensas.

### 15.4 Invariantes obligatorios

- Probabilidades siempre dentro de [0,1]; xG no equivale a probabilidad final de gol.

- OVR no cambia por FIT/MOR; rendimiento efectivo sí.

- Los presupuestos y balances no se actualizan dos veces por el mismo evento.

- Un jugador no puede estar inscrito simultáneamente en dos clubes activos, salvo estado de cesión
modelado explícitamente.
- Un partido se procesa una sola vez; los eventos y tablas derivadas son consistentes.

- Una semilla y versión iguales reproducen el mismo resultado.

- La suma de pesos de cada perfil posicional es 1,00.

- El total de jugadores/regens permanece dentro de bandas poblacionales definidas.

[v1.3·R] Invariantes añadidos en v1.3.0:
- FCOH ∈ [0,97 ; 1,03]; Fsoft ∈ [0,90 ; 1,04]; Ftotal ∈ [0,60 ; 1,15]; Mfam ∈ [0,70 ; 1,00].

- Los pesos de pertenencia a zonas de cada jugador suman 1,00 y no son negativos para todo (x, y) ∈
[0,100]².
- El valor de mercado es monótono creciente en OVR y nunca negativo; un jugador con contrato vencido tiene
coste de traspaso 0.
- La copa de 40 equipos produce exactamente 39 partidos en 6 rondas y un único campeón; ningún equipo
juega dos veces la misma ronda.
- OVR de regen ∈ [42, 56] y POT ≥ OVR; POT ≤ 92.

- Rating ∈ [3,0 ; 10,0] y rating de quien juega menos de 60 minutos se acerca a 6,5 en proporción a s.


### 15.5 Criterio de éxito del MVP

Una carrera de diez temporadas sin errores críticos, con calendario y clasificación correctos, fichajes
coherentes, economía estable, progresión razonable, IA funcional, guardado recuperable y resultados
estadísticamente creíbles. Antes de ampliar países, demostrar una temporada completa y luego diez
temporadas reproducibles.



### 16. Roadmap recomendado
Fase 1 — Vertical Slice y MVP incremental
- Vertical Slice previo: una división ficticia de 10 clubes, plantillas reducidas, 10 atributos visibles, dos
formaciones y tres ejes tácticos. Tras superar sus criterios se amplía al MVP: un país, dos divisiones, copa y
reglas completas.
- Vertical Slice: calendario de liga a doble vuelta (18 jornadas), clasificación, motor de partidos y
MatchReport. El MVP amplía el calendario a las competiciones definidas en el paquete de reglas.
- Vertical Slice: plantilla, OVR, roles, FIT/MOR, convocatoria y táctica base con 10 atributos visibles, dos
formaciones y tres ejes.
- Vertical Slice: mercado mínimo de prueba con ofertas directas, contratos y presupuesto simplificado; el
mercado completo y la economía multitemporada se incorporan después de validar el ciclo deportivo.
- Guardado, carga, autoguardado y una temporada completa.

Fase 2 — Profundidad deportiva
- Cantera, regens, entrenamiento individual, curvas de desarrollo.

- Scouting, staff, instalaciones, lesiones y suspensiones ampliadas. Lesiones básicas y seguras deben existir
desde el Vertical Slice solo como estados simples y recuperables; el subsistema clínico detallado se amplía
después.
- Personalidad, química, familiaridad, objetivos individuales y agentes. En el Vertical Slice, química y
familiaridad se mantienen fuera del cálculo de rendimiento (FCOH = FREL = FTAC = 1,00) hasta que sus
fórmulas (secciones 4.8 y 5.1) y pruebas estén aprobadas.
- Balón parado, instrucciones individuales y noticias semanales, siempre dentro de Ajustes avanzados y no en
la pantalla táctica principal.
Fase 3 — Mundo persistente avanzado
- IA de entrenadores, economía global, reputación especializada y afición.

- Historial completo, récords, salón de la fama y periódico avanzado.

- Editor de formatos, importación/exportación comunitaria y personalización.

Fase 4 — Expansión
- Múltiples países, competiciones continentales, selecciones y clima.

- Rivalidades históricas, árbitros con tendencias y sistemas avanzados de prensa.

### 17. Riesgos y decisiones pendientes
Riesgo / decisión
Tratamiento
Alcance excesivo
Separar MVP y fases; no desarrollar todo en paralelo.
Balance del motor
Simulación masiva, semillas y métricas antes de fijar tasas.
Rendimiento móvil
Prototipo temprano en hardware real; medir memoria y tiempos.
Complejidad de datos
Esquema versionado, migraciones e invariantes.
Economía inflacionaria
Simular 10–50 temporadas y ajustar ingresos, salarios y mercado.
IA de clubes
Definir prioridades, límites de plantilla y presupuesto por perfil.
Editor comunitario
Validación estricta y aislamiento de datos importados.
Licencias y marcas
Datos ficticios por defecto; contenido importado bajo responsabilidad del usuario.
[v1.3·R] Determinismo
multiplataforma
RNG propio, orden estable y hash «golden» en Android/iOS (sección 14.7).
[v1.3·R] Escala económica
Decidir reescalado de salarios/ingresos o mantenimiento (sección 9.1).
Decisiones que deben cerrarse antes de producción
- Motor/stack definitivo tras prototipo comparativo.

- Formato exacto de liga, copa, ascensos y desempates. Estado v1.3.0: estructura de la copa cerrada (sección
23.2); ascensos/descensos y desempates siguen siendo paquete de reglas.



- Pesos definitivos de OVR y perfiles de posición.

- Curvas de FIT, MOR, desarrollo, lesiones y economía. Estado v1.3.0: hay fórmulas provisionales de cohesión,
familiaridad, recuperación, valor y salario; falta calibrarlas.
- Presupuesto de memoria y tamaño validado en dispositivos.

- Modelo de IA de fichajes, entrenadores y finanzas.

- Política de compatibilidad de partidas y migraciones.

### 18. Decisiones v1.2.0: alcance, simulación y sistemas
Este capítulo incorpora la revisión crítica posterior a v1.1.0. Distingue decisiones obligatorias para evitar
retrabajo, sistemas que se implementan después de validar el núcleo y propuestas aplazadas. No todo lo
técnicamente posible pertenece al MVP.

### 18.1 Priorización de las nuevas propuestas

Propuesta
Decisión
Motivo
Umbrales de fallo del motor
INCLUIR antes de balance
Evita aceptar un motor estadísticamente roto.
Población inicial y generación
INCLUIR especificación; implementar
por etapas
Sin universo coherente no se puede probar
mercado ni progresión.
IA de once y táctica
INCLUIR versión básica en Vertical
Slice
La simulación necesita rivales funcionales, no
solo mercado.
Lesiones detalladas
INCLUIR modelo básico; detalle clínico
post-Slice
Evita bloquear el prototipo con un subsistema
grande.
Reglas de competición
CERRAR para cada paquete de
competición
El calendario y la tabla dependen de reglas
deterministas.
Inflación multitemporada
INCLUIR controles y pruebas desde el
modelo económico
La economía debe ser sostenible, pero no
requiere simulación compleja inicial.
Localización
PREPARAR arquitectura desde el día 1;
lanzar en español
Evita textos hardcodeados y costosas
migraciones.
Estados límite
INCLUIR reglas de fallback esenciales
Los casos raros no deben bloquear jornadas ni
guardados.
Canteranos y cupos
DISEÑAR como reglas configurables;
activar cuando la competición lo exija
No imponer cupos genéricos a ligas que no los
usan.
Pausas internacionales
APLAZAR
No son baratas si se modelan convocatorias,
calendarios y fatiga correctamente.
Exportación CSV/JSON de
balance
INCLUIR en herramientas de desarrollo
Acelera auditoría estadística y regresiones.
Salón de la Fama y retiro
Salón de la Fama post-MVP; retiro
opcional post-MVP
No afecta al bucle central de la primera versión.
Criterios de aceptación por
módulo
INCLUIR
Permite cerrar sistemas sin expansión indefinida.
Vertical Slice 4–6 semanas
ADOPTAR como puerta de validación
Reduce el riesgo de construir demasiado antes
de probar diversión y rendimiento.

### 18.2 Vertical Slice de 4–6 semanas

Objetivo: demostrar que el ciclo preparar → seleccionar once → elegir táctica → simular → entender el resultado
es divertido, legible, reproducible y rápido en un móvil. No es una demo de contenido ni una versión comercial.
- Universo: una división ficticia de 10 clubes; cada club tiene 18–22 jugadores para el prototipo, con roles
suficientes para formar un once.
- Plantilla del jugador: máximo 22 jugadores; 10 atributos visibles; rasgos de personalidad desactivados
salvo datos de prueba.
- Formaciones: 4-4-2 y 4-3-3. Tres ejes tácticos: mentalidad (defensiva/equilibrada/ofensiva), presión
(baja/media/alta) y altura de línea (baja/media/alta).
- Partidos: liga a doble vuelta, 18 jornadas. Sin copa, competiciones continentales, selecciones, prensa
avanzada ni editor.
- Gestión: alineación manual, sustituciones manuales y una regla automática simple; mercado de prueba
limitado a agentes libres y ofertas directas.



- IA: selección de once por adecuación posicional, condición física y OVR de rol; una táctica base por
entrenador y reacción simple al marcador.
- Interfaz: vertical, cinco acciones principales, táctica simple. Ajustes avanzados, balón parado e
instrucciones individuales quedan fuera.
- Persistencia: una ranura local y autoguardado; no se implementan aún cinco ranuras ni importación
comunitaria.
Plan orientativo: semana 1, datos, plantillas y calendario; semana 2, OVR, táctica e interfaz de once; semana 3,
simulador y MatchReport; semana 4, IA rival, guardado y bucle de jornada; semanas 5–6, balance,
accesibilidad, correcciones y prueba en dispositivos. Si el equipo no puede dedicar tiempo completo, el
calendario se amplía: la duración no justifica recortar pruebas.
[v1.3·R] Los 10 atributos visibles del Vertical Slice. La v1.2.0 fijaba «10 atributos visibles» sin
enumerarlos. Propuesta: POR, ENT, MAR, AER, PAS, REG, FIN, VEL, RES, POS. Con ellos se calculan los 7 roles
que necesitan 4-4-2 y 4-3-3; los perfiles se renormalizan (los atributos ausentes se eliminan y el resto se
reescala para sumar 1,00). El residuo de redondeo se asigna al último componente.
Rol del Vertical Slice
Pesos renormalizados
Peso original
retenido
Suma
POR clásico
POR 0,692, POS 0,308
0,65
1,000
DFC marcador
ENT 0,357, MAR 0,357, AER 0,286
0,70
1,000
LD/LI defensivo
ENT 0,25, MAR 0,25, VEL 0,2, RES 0,15, POS 0,15
1,00
1,000
MCD organizador
PAS 0,545, POS 0,273, ENT 0,182
0,55
1,000
MC box-to-box
RES 0,294, PAS 0,235, ENT 0,235, POS 0,236
0,85
1,000
ED/EI extremo
VEL 0,375, REG 0,375, PAS 0,25
0,80
1,000
DC avanzado
FIN 0,389, VEL 0,278, POS 0,222, REG 0,111
0,90
1,000
[v1.3·R] Correspondencia de ejes del Vertical Slice con los ejes completos. El Vertical Slice usa tres
niveles por eje; se mapean a las opciones de la sección 6.3 para que el MVP no cambie el significado de los
datos:
Eje del Slice
Nivel del Slice
Opción completa equivalente
Mentalidad
defensiva / equilibrada / ofensiva
Defensiva / Equilibrada / Ofensiva
Presión
baja / media / alta
Bloque bajo / Media / Alta
Altura de línea
baja / media / alta
Muy atrás / Normal / Adelantada
Pase y amplitud (fijos)
—
Mixto y Normal
De las incompatibilidades de la sección 6.4, el Slice activa I1 e I2 (solo usan presión y línea defensiva); I3 e I4
se activan en el MVP.

### 18.3 Puertas de salida del Vertical Slice

- Una temporada completa de 18 jornadas puede simularse y guardarse sin corrupción ni bloqueo.

- Una jornada de 5 partidos se procesa en menos de 150 ms en el dispositivo de referencia; medir también el
percentil 95.
- El usuario entiende por qué perdió o ganó mediante MatchReport, xG y tres factores explicativos.

- Los cambios de mentalidad, presión y altura de línea producen diferencias medibles y coherentes en xG,
tiros y zonas de recuperación.
- La IA siempre presenta once válido, sin duplicar jugadores ni asignar un jugador de campo como portero
salvo fallback explícito.
- El juego se puede usar sin abrir ajustes avanzados; el flujo básico de preparar y simular un partido requiere
pocas acciones.
### 19. Umbrales de aceptación estadística del motor

Los umbrales siguientes son alarmas iniciales de QA para una competición de referencia equilibrada, no leyes universales del fútbol. Ejecutar al menos 100.000 partidos por escenario con semillas registradas; aumentar a 1.000.000 para investigar desviaciones cercanas al límite. Separar por división, temporada, localía y diferencia de OVR.

| Métrica | Banda inicial de aceptación | Acción si falla |
|---|---|---|
| Goles totales por partido | 2,4–3,1; objetivo de calibración 2,6–2,9 | Revisar generación de ocasiones, xG y conversión; no compensar solo alterando finalización. |
| Victorias locales | 42–48% de partidos decididos; empates reportados aparte | Auditar localía, fatiga de viaje si existe y sesgo de orden de simulación. |
| Ventaja de +10 OVR | Gana al menos 55% en muestra neutral comparable; registrar empates por separado | Si queda por debajo, revisar curva OVR→fuerza de equipo y varianza; no aplicar bonus directo. |
| [v1.3·R] Duelo desigual (≥ +20 OVR) | Victoria del favorito ≥ 65%; victoria del inferior entre 8% y 15% (hipótesis de la sección 10.1) | Revisar la varianza de ocasiones y la cola de la curva OVR→fuerza; no aplicar bonus directo. |
| Efecto táctico | Cambio de táctica debe alterar xG o tiros en al menos 3% relativo en escenarios diseñados y con dirección esperada | Si no se detecta, revisar que instrucciones entren en la cadena causal; si altera demasiado, limitar el modificador. |
| Conversión de ocasiones | Gol/xG agregado entre 0,85 y 1,15 en muestras grandes | Auditar función de conversión y dependencia de calidad/atributos. |
| Empates | Registrar y comparar con objetivo de competición; alarma si > 35% o < 15% en liga equilibrada | Revisar dispersión de goles y correlación de fuerzas. |
| Distribución de marcadores | 0–0, 1–0, 1–1, 2–1, 2–0 deben dominar; marcadores extremos raros | Buscar errores de acumulación, bucles de eventos y colas de distribución. |
| Reproducibilidad | Misma semilla + misma versión = mismo resultado exacto | Fallo bloqueante: localizar aleatoriedad no controlada. |

Regla de diagnóstico: no declarar roto el motor por una sola muestra pequeña. Comparar intervalos de confianza y repetir con semillas independientes. Una desviación fuera de banda en dos ejecuciones grandes bloquea la aceptación. Los valores de +10 OVR se calculan en grupos comparables y no mezclan localía, expulsiones ni lesiones.

[v1.3·R] La banda de «duelo desigual» es una hipótesis inicial que cierra la ambigüedad entre la sección 10.1 (sorpresas de 12–15%) y el +10 OVR ≥ 55%: ambas aplican a diferencias distintas de OVR y deben calibrarse juntas con la misma curva.

### 20. Generación del mundo inicial
### 20.1 Población y nombres

Cada jugador generado recibe país de formación, nacionalidad, nombre y apellido de pools regionales
versionados. La frecuencia de nombres sigue una tabla de pesos por país y época; se evita repetir nombre
completo dentro de una misma competición cuando exista alternativa. Los pools se almacenan como datos
externos, no dentro del código. El prototipo puede usar nombres ficticios de una sola región.

### 20.2 Atributos y OVR

Generar atributos con distribución normal truncada entre 1 y 99. La media se deriva del nivel de la división y
reputación del club; la desviación estándar inicial sugerida es 7 puntos por atributo, ajustable por posición y
edad. El OVR se calcula siempre desde los pesos posicionales, nunca se asigna independientemente. La
reputación del club desplaza la media de su plantilla, no cada atributo de forma idéntica.
Estos rangos son calibración inicial para una escala de OVR de 1–99, no una equivalencia universal. La media
de cada división debe mantenerse separada por una distancia pequeña y coherente. Los clubes no reciben
plantillas idénticas: se generan perfiles de edad, posiciones y potencial diferentes.

### 20.3 Plantilla y talento excepcional

- Plantilla de referencia completa: 25 jugadores por club en el MVP; el Vertical Slice usa 18–22 para reducir
contenido.
- Distribución de edades inicial orientativa: 3 porteros; 7 defensas; 8 mediocampistas; 5 delanteros; edad
media entre 25 y 27 años.
- Cada club debe tener al menos dos porteros, dos jugadores por cada línea y cobertura mínima de roles para
una formación válida.
- Talento OVR 85+: no fijar una cuota arbitraria en una liga de nivel bajo. Generar desde una distribución
global condicionada por reputación y división; en el paquete de prueba, el control inicial será 0–2 jugadores



85+ por cada 20 clubes y se revisará contra la escala de OVR.
- El generador debe validar que no haya posiciones vacías sistémicas, atributos fuera de rango, contratos
imposibles ni concentración excesiva de talento.
Perfil de club
OVR medio objetivo de plantilla
Dispersión orientativa
Favorito de división
68–72 titulares
70–76; suplentes 62–70
Mitad alta
63–68 titulares
65–71; suplentes 58–66
Mitad baja
58–63 titulares
60–66; suplentes 53–62
Candidato al descenso
54–59 titulares
56–62; suplentes 48–58
La creación inicial usa una semilla maestra y subsemillas por club/jugador. Guardar la semilla y la versión del
generador para reproducir cualquier partida y poder migrar la población en versiones futuras.
### 21. IA de alineación y decisiones tácticas
La IA debe priorizar decisiones razonables, no hacer trampa. La dificultad modifica calidad de evaluación,
profundidad de planificación y capacidad de adaptación; no aumenta atributos, dinero ni reduce lesiones del
equipo controlado.
- Selección de once: puntuar cada jugador por OVR en rol, familiaridad posicional, FIT, sanciones/lesiones,
moral y carga reciente. Resolver la asignación maximizando adecuación total y respetando posiciones.
- Formación: elegir entre las formaciones desbloqueadas del club según disponibilidad de jugadores,
identidad del entrenador y fortalezas/debilidades del rival.
- Táctica inicial: deriva de filosofía del entrenador, localía, fuerza relativa y análisis rival; no debe cambiar
aleatoriamente cada jornada.
- Reacción al marcador: desde el minuto 60 si va perdiendo, evaluar aumento de mentalidad y presión;
desde el 75 aplicar cambio más agresivo si el partido sigue perdido. Si va ganando, puede bajar ritmo y
proteger espacios. No son cambios obligatorios: dependen de perfil del entrenador.
- Rotación: penalizar la selección de jugadores con FIT bajo y considerar importancia del siguiente partido,
profundidad de plantilla y riesgo de lesión.
- Dificultad: Normal: decisiones funcionales y estables. Difícil: mejor lectura de rival y rotación. Élite:
planificación más consistente y menos errores de rol, sin bonificaciones ocultas de atributos ni ventajas
económicas artificiales.
Para evitar búsqueda computacional excesiva, el Vertical Slice usa heurísticas deterministas con pesos
configurables. La IA debe exponer en logs por qué eligió cada once y táctica; la UI solo muestra una explicación
breve cuando sea útil.
### 22. Lesiones y disponibilidad
### 22.1 Modelo por etapas

Vertical Slice: estados Disponible, Tocando y Lesionado con días restantes; no se simulan lesiones
permanentes. MVP: tipos de lesión y rangos de recuperación; Post-MVP: recaídas detalladas, especialidades
médicas y rehabilitación avanzada.
Riesgo diario = riesgo_base_por_actividad × multiplicador_carga × multiplicador_fatiga ×
multiplicador_historial × multiplicador_contacto. LES modifica la susceptibilidad dentro de límites acotados.
Entrenamientos de alta carga e intensidad y dobles sesiones aumentan exposición; descanso y gestión de
minutos la reducen. Las lesiones de partido consideran contacto, entradas, fatiga y un componente aleatorio
reproducible.
Tipo
Baja orientativa
Notas de diseño
Golpe/contusión
1–7 días
Puede permitir jugar con restricción si el informe lo autoriza.
Molestia muscular
3–14 días
Aumenta con fatiga y carga elevada.
Lesión muscular
2–8 semanas
Recuperación gradual; riesgo de recaída si vuelve antes de tiempo.
Lesión ligamentosa
4–16 semanas
Mayor incertidumbre; requiere reevaluación.
Fractura
6–20 semanas
Baja larga; sin detalle gráfico ni secuelas automáticas.
No aplicar pérdida permanente de POT, VEL u otros atributos como consecuencia automática de una lesión
grave. Una lesión larga puede retrasar temporalmente la progresión y reducir FIT; una secuela excepcional solo



se introduce si el diseño futuro la justifica, con probabilidad muy baja, comunicación clara y sin convertirla en
castigo inevitable. El staff médico influye en precisión del diagnóstico, calidad del plan de recuperación y
detección de recaídas, no en curaciones instantáneas. Un mal jefe médico puede ofrecer intervalos de
recuperación más inciertos, pero nunca ocultar deliberadamente información esencial ni engañar al usuario. La
recaída depende de retorno prematuro, carga posterior y tipo de lesión.
### 23. Paquete de reglas de competición del MVP
Las reglas se almacenan como configuración versionada por competición. Para el Vertical Slice se usa una liga
ficticia a doble vuelta de 10 equipos, 3 puntos por victoria, 1 por empate y 0 por derrota; no hay copa ni
ascensos. El MVP comercial no debe inventar reglas de una liga real: cada país/competición se incorpora
mediante un paquete de reglas verificado y versionado.

### 23.1 Reglas de referencia para el paquete de prueba

- Desempates: 1) diferencia de goles; 2) goles a favor; 3) puntos en enfrentamientos directos; 4) diferencia
de goles en enfrentamientos directos; 5) partido de desempate solo si la competición lo exige; de lo
contrario, sorteo determinista documentado.
- Ascenso/descenso: no aplica al Vertical Slice. En el paquete MVP de dos divisiones se define por
competición antes de generar calendario; no se hardcodea en el motor. Plantilla de configuración inicial: dos
ascensos directos y dos descensos directos, sin playoff, sujeto a validación del formato elegido.
- Copa: excluida del Vertical Slice. Configuración propuesta para el primer MVP: eliminatoria a partido único,
prórroga y penaltis si persiste empate; sorteo abierto sin cabezas de serie salvo que el paquete de
competición indique lo contrario; final en sede neutral. La estructura de cruces está cerrada en la sección
23.2.
- Inscripción: el motor soporta ventanas de mercado configurables y fecha de inscripción separada. Para
una temporada ficticia de prueba: ventana principal 1 de julio–31 de agosto y ventana secundaria 1–31 de
enero; máximo 25 inscritos senior y hasta 5 juveniles elegibles fuera de ese cupo.
- Sanciones: acumulación de 5 amarillas en liga = 1 partido; roja directa = mínimo 1 partido, con sanción
adicional configurable por competición. Apelaciones quedan fuera del Vertical Slice y son opcionales
post-MVP.
- Tarjetas y sanciones se procesan después de cada partido y antes de generar la siguiente convocatoria. El
calendario nunca debe permitir al jugador alinear por error a un sancionado.
[v1.3·R] Sanciones entre competiciones (valor por defecto, configurable). Las amarillas de copa se
acumulan por separado de las de liga; una roja directa se cumple en la misma competición en la que se
produjo, y si el club no tiene más partidos de esa competición en la temporada, la sanción pasa a la liga. Esto
evita ambigüedad al introducir la copa.
Los valores propuestos de copa, ascensos, cupos y ventanas son un paquete ficticio de prueba, no una
afirmación sobre reglamentos oficiales. Antes de incluir una competición real se reemplazan por su reglamento
documentado.

### 23.2 Estructura de la Copa Nacional (40 equipos) [v1.3·A]

Participan los 20 equipos de la primera división y los 20 de la segunda. Para llegar a un cuadro exacto de 32 equipos se juega una ronda preliminar entre los 16 peores clasificados de la segunda división de la temporada anterior (puestos 5 a 20; en la primera temporada se usa el orden de reputación). Los 24 equipos restantes (20 de primera y los 4 mejores de segunda) pasan directamente a la ronda de 32.

| Ronda | Equipos | Partidos | Semana (miércoles) | Notas |
|---|---:|---:|---:|---|
| Preliminar | 16 | 8 | 9 | Solo puestos 5–20 de segunda; 8 clasificados |
| Dieciseisavos (32) | 32 | 16 | 14 | 8 clasificados + 24 exentos |
| Octavos | 16 | 8 | 20 | |
| Cuartos | 8 | 4 | 32 | Segunda vuelta de liga |
| Semifinales | 4 | 2 | 38 | |
| Final | 2 | 1 | 45 | Sede neutral, sin ventaja local |

- Total: 39 partidos (40 equipos − 1 campeón); 6 rondas. Un finalista que parta de la ronda de 32 disputa 5 partidos de copa; el máximo para un equipo de la preliminar es 6.
- Sorteo: abierto y sin cabezas de serie, realizado con subsemilla determinista antes de cada ronda; el primer equipo sorteado es local (configurable). Ningún equipo puede enfrentarse a sí mismo ni jugar dos veces la misma ronda.
- Empate: prórroga y penaltis según la sección 10.3. Una derrota por penaltis cuenta como derrota a efectos de Confianza Directiva (Wcopa).
- Premios y reputación: el pago por ronda, la reputación y los bonos de patrocinio se configuran en el paquete de competición.
- Calendario: partido entre semana (miércoles); mínimo 3 días respecto a la jornada de liga más cercana (sección 3.3).

24. Economía multitemporada y localización
### 24. Economía multitemporada y localización

### 24.1 Controles antiinflación

- Ingresos de TV y patrocinio crecen por temporadas mediante una tasa nominal configurable y un factor de
reputación con rendimientos decrecientes; evitar multiplicadores exponenciales ilimitados.
- Salarios y renovaciones responden a OVR, edad, potencial, reputación, demanda y duración contractual
(fórmula base en la sección 8.6). El crecimiento salarial agregado se compara con crecimiento de ingresos y
productividad deportiva.
- Valores de traspaso usan bandas por OVR/edad/contrato/reputación y límites de plausibilidad por división;
no hay techo global rígido que impida operaciones excepcionales.
- Si un club acumula caja excesiva, no se le obliga a gastar: puede aumentar expectativas, inversión en
instalaciones, reservas o presión de accionistas según su perfil. La afición no protesta simplemente porque el
saldo sea alto.
- Prueba de economía: simular 20 temporadas y medir mediana, percentiles 10/90, clubes insolventes, masa
salarial/ingresos, transferencias y concentración de riqueza.

### 24.2 Localización

Desde el primer día, todos los textos visibles usan claves externas (por ejemplo, ui.tactics.pressure.high);
prohibido hardcodear cadenas de interfaz en lógica. El MVP se publica en español, pero el sistema admite
catálogos por idioma, pluralización, variables, fallback y actualización sin alterar partidas. Fechas, monedas,
separadores decimales y agrupación de miles se formatean por configuración regional y moneda de la
competición. Los datos monetarios se almacenan como enteros en unidades menores o como decimales
exactos, nunca como texto localizado. Elegir fuentes con soporte de tildes, ñ, diéresis, símbolos y alfabetos
futuros; validar fallback tipográfico.
### 25. Estados límite, cupos y cantera
- Sin portero disponible: el sistema propone el jugador de campo con mejor combinación de POR,
posicionamiento y familiaridad como portero de emergencia; muestra alerta y penalización explícita. Si no
existe ningún jugador elegible, el partido no se simula y se aplica la resolución administrativa configurada
por competición.
- Convocatoria incompleta: permitir convocatoria menor al máximo; no inventar jugadores. Si no se
alcanza el mínimo reglamentario, aplicar regla administrativa del paquete de competición y registrar el
motivo.
- Jugador en rebeldía: estado Transfer Request o No disponible por conflicto; puede entrenar y ser
seleccionado si acepta, pero el usuario recibe alternativas de mediación/venta. Nunca bloquear el avance del
calendario.
- Sin delantero centro sano: la IA o el usuario pueden usar un falso nueve o adaptar un
extremo/mediapunta; el juego indica la pérdida de familiaridad y no cancela automáticamente el partido.
- Cupos de canteranos: motor configurable, desactivado por defecto si la competición no lo exige.
Definición base propuesta: formado en el club si estuvo inscrito/entrenando en el club durante al menos 3
temporadas (consecutivas o no, según regla) entre las edades de 15 y 21.
- Los cupos se pueden aplicar a plantilla inscrita o convocatoria, y el paquete de competición define cuál;
nunca se asume que todas las ligas comparten la misma regla.



26. Herramientas de balance, historial y aceptación por
módulo

### 26. Herramientas de balance, historial y aceptación por módulo

### 26.1 Exportación de datos

Modo Debug exporta CSV/JSON de partidos, eventos, xG, OVR, tácticas, lesiones, economía y temporadas
simuladas. Cada exportación incluye versión del motor, semilla, configuración, timestamp lógico y hash del
conjunto. No se incluyen datos personales ni rutas privadas. Los archivos deben poder abrirse en Excel,
LibreOffice y Python.

### 26.2 Historial del mánager

El MVP conserva historial dentro de cada carrera. Salón de la Fama comparativo entre ranuras y retiro del
mánager se aplazan a Post-MVP. El retiro será voluntario; la edad no forzará el fin de una carrera salvo que el
modo futuro lo defina expresamente. La pantalla de legado resumirá clubes, títulos, ascensos, récords,
temporadas y reputación.

### 26.3 Criterios de aceptación por módulo

Módulo
Terminado cuando…
Simulación
Pasa umbrales de la sección 19, es determinista por semilla y no rompe invariantes.
Calendario/competición
Genera todas las jornadas, aplica desempates/sanciones y produce tablas reproducibles.
[v1.3·R] Copa
Genera el cuadro de 40 equipos con preliminar de 16, 39 partidos y un único campeón;
resuelve prórroga y penaltis; respeta el calendario de miércoles.
IA de alineación
Siempre genera once válido y respeta disponibilidad, posiciones y reglas; logs explican
decisiones.
Mercado
No duplica propiedad/contratos; valida presupuestos, salarios, ventanas y estados de
negociación.
[v1.3·R] Valor y salario
Valor monótono en OVR; razón valor/sueldo anual dentro de 6–15 para 22–28 años y OVR
55–90; el balance económico de 20 temporadas cumple la sección 24.1.
Entrenamiento
Carga, descanso y progresión se mantienen dentro de límites y no generan crecimiento
ilimitado.
[v1.3·R] Cohesión y
familiaridad
F_COH, F_REL, F_TAC y M_fam dentro de sus rangos; F_soft acotado; repetir el once no
escala sin límite (equilibrio C ≈ 70).
Lesiones
Riesgo y recuperación están acotados; no hay bloqueos por falta de plantilla; recaídas son
reproducibles.
Scouting
La incertidumbre se comunica, la precisión aumenta según tiempo/calidad y no revela
datos no observados.
Economía

20 temporadas sin inflación explosiva ni insolvencia sistémica no prevista; se reportan

percentiles.
Guardado
Cierre inesperado recuperable, integridad verificada y migraciones probadas desde
versión anterior.
Interfaz móvil
Acciones principales accesibles, textos localizados por claves, controles táctiles y flujo sin
ajustes avanzados.
### 27. Reglas de monetización, onboarding y feedback
sensorial
Modelo recomendado para el primer lanzamiento: Premium de pago único, sin anuncios y sin ventajas
competitivas comprables. Si el presupuesto comercial exige una alternativa, evaluar una edición gratuita
limitada con desbloqueo único de carrera completa/editor, pero no diseñar el núcleo suponiendo anuncios
recompensados. Ningún pago acelera lesiones, scouting, partidos o progresión.
FTUE opcional y saltable de siete días guiados por el segundo entrenador: conocer club y plantilla; revisar
atributos; hacer un fichaje sencillo; configurar formación; preparar convocatoria; simular primer partido;
revisar informe y pasar a gestión libre. Cada paso explica solo un concepto nuevo y permite desactivar la guía.
La simulación ofrece revelación de resultado de 3–5 segundos, con opción de saltar. Microanimaciones de
marcador y eventos clave, háptica opcional en goles/expulsiones, sonidos de estadio y silbato activables por
separado. Respetar ajustes de accesibilidad, silencio del dispositivo y preferencias del usuario; ninguna
información depende solo de sonido o vibración.



[v1.3·R] Privacidad y telemetría. Coherente con el principio offline-first: sin cuentas obligatorias y sin
telemetría por defecto. Si se añade analítica de errores o rendimiento, será opt-in, anónima, sin datos
personales ni contenido de partidas, y descrita en la política de privacidad exigida por las tiendas.
### 28. Registro de decisiones y riesgos abiertos
- Decisión cerrada: el Vertical Slice es el primer entregable obligatorio y precede al MVP completo.

- Decisión cerrada: el motor no recibe ventajas ocultas por dificultad; la IA mejora por calidad de decisiones.

- Decisión cerrada: los rasgos cualitativos no duplican atributos numéricos.

- Decisión cerrada: la pantalla táctica principal es simple; profundidad bajo Ajustes avanzados.

- Provisional: bandas estadísticas, OVR por división, duración de lesiones y parámetros económicos deben
calibrarse con pruebas.
- Pendiente antes de una liga real: reglamento oficial, calendario, cupos, ventanas, sanciones y criterios de
desempate de esa competición.
- Post-MVP: parones internacionales, editor completo, competiciones continentales, salón de la fama entre
carreras, retiro y lesiones con secuelas excepcionales.
[v1.3·R] Decisiones cerradas en v1.3.0: estructura de la copa de 40 equipos; fórmulas base de valor de
mercado y salario (provisionales en parámetros, cerradas en estructura); límites de sectores y carriles del
campo; rango de OVR de regens (42–56); coeficientes provisionales del rating; calendario de 38 jornadas;
determinismo multiplataforma como requisito.
[v1.3·R] Abiertos nuevos: escala económica (salarios vs. mantenimiento, sección 9.1); calibración conjunta
de los umbrales +10/+20 OVR; controlador poblacional de POT de regens (sección 5.4).

### Anexo A. Perfiles de rol y atributos

Los perfiles originales se conservan como pesos iniciales. Cada vector debe sumar 1,00 y puede ser ajustado
en balance. Los códigos corresponden a la lista de atributos del capítulo 4. (Verificado por código: los 15
perfiles suman 1,00.)
Posición / rol
Ponderación
POR clásico
POR .45, AGI .20, POS .20, CON .15
POR líbero
POR .35, PAS .20, VEL .15, AGI .15, SER .15
DFC marcador
ENT .25, MAR .25, AER .20, FUE .20, CON .10
DFC salida
MAR .20, ENT .20, PAS .25, SER .20, POS .15
LD/LI defensivo
ENT .25, MAR .25, VEL .20, RES .15, POS .15
LD/LI carrilero
VEL .25, RES .25, REG .20, PAS .15, ENT .15
MCD destructor
ENT .30, MAR .20, FUE .20, AGR .15, POS .15
MCD organizador
PAS .30, VIS .25, SER .20, POS .15, ENT .10
MC box-to-box
RES .25, PAS .20, ENT .20, POS .20, TIL .15
MCO enganche
VIS .30, PAS .25, REG .25, SER .10, FIN .10
ED/EI extremo
VEL .30, REG .30, AGI .20, PAS .20
ED/EI interior
REG .25, FIN .25, VEL .20, POS .15, TIL .15
DC avanzado
FIN .35, VEL .25, POS .20, SER .10, REG .10
DC objetivo
FUE .30, AER .30, FIN .20, PAS .10, POS .10
DC presionante
AGR .25, RES .25, FIN .20, VEL .15, ENT .15



Anexo B. Pantallas
Pantalla
Responsabilidad
Acceso (sección 13.1)
Dashboard
Centro de mando, siguiente evento, noticias y alertas
Inicio
Plantilla
Jugadores, Sub-21, contratos, rasgos, forma e historial
Plantilla
Táctica
Formación, once, roles, instrucciones y balón parado
Táctica
Informe rival
Once probable, amenazas, vulnerabilidades y contramedidas
Táctica
Calendario
Vista semanal/mensual, resultados y entrenamiento
Inicio
Mercado/Ojeo
Búsqueda, informes, ofertas y negociación
Mercado
Finanzas
Balance, presupuestos, salarios, patrocinios y deuda
Club
Instalaciones
Niveles, mejoras, costes y efectos
Club
Staff
Puestos, candidatos, salarios e impacto
Club
Clasificación
Tabla, rachas y rankings individuales
Club
Historial
Palmarés, trayectoria, récords y salón de la fama
Club
Noticias
Periódico semanal y eventos contextuales
Inicio

### Anexo C. Glosario

Término
Definición
OVR
Media general base por posición/rol
POT
Potencial máximo estimado
FIT
Forma física / disponibilidad
MOR
Moral
xG
Calidad de una ocasión antes de la conversión individual
MatchReport
Objeto con resultado, eventos y estadísticas del partido
Regens
Jugadores generados para renovar la población futbolística
Headless
Simulación sin generar presentación detallada
Familiaridad
Dominio de posición, rol y esquema (F_rol, T)
Cohesión
Compatibilidad colectiva y continuidad del equipo (C)
Semilla
Estado inicial reproducible del generador pseudoaleatorio
[v1.3·R] Cd
Confianza Directiva ∈ [0,100]
[v1.3·R] Rend. efectivo
Atributos tras la cadena F_FIT × F_MOR × F_soft; nunca modifica el OVR
[v1.3·R] F_soft
Producto acotado de cohesión, relaciones y familiaridad de esquema
[v1.3·R] Valor de mercado
Precio de referencia derivado de OVR, edad, POT, posición, reputación y contrato
[v1.3·R] Pertenencia
suave
Pesos de un jugador a cada zona del campo; suman 1,00

### Anexo D. Registro de cambios v1.3.0

Origen: A = pendiente indicado por el autor tras revisar la v1.2.0; R = propuesta de la revisión crítica.
ID
Sección
Origen
Cambio

1

4.8, 4.9, 5.1, 6.4
A
Química, familiaridad e incompatibilidades tácticas formalizadas con rangos acotados.

2

8.6, 24.1
A
Fórmula base de valor de mercado y salario esperado, con tabla de ejemplos verificada.

3

3.3, 10.3, 23.2
A
Estructura de la Copa Nacional de 40 equipos (preliminar de 16 → cuadro de 32) y
calendario de rondas.

4

2.4–26.3, Anexos
A
Tablas reubicadas en su sección; cierre único del documento al final.

5

6.2, 10.4
A
Límites numéricos de sectores y carriles; pertenencia suave y cuadrícula de 9 celdas.

6

5.4, 20.3
A
Rango de OVR inicial de regens (42–56 por nivel de academia).

7


10.10

A
Coeficientes provisionales del rating post-partido con topes.

8

3.4, 4.5, 5.2, 9.1, 10.4,

13.5

A
Erratas: ΔC_d, signo de ΔFIT_partido, B_instalaciones duplicado, 1,6^Nv, × entre
Z_med(B) y M_pase(B), «UI».



ID
Sección
Origen
Cambio

9


4.9

R
Cadena canónica de rendimiento efectivo y topes globales.

10


4.5

R
Recuperación diaria de FIT acotada y nota sobre el punto neutro de moral.

11


3.4

R
Calibración inicial provisional de Confianza Directiva.

12


3.3

R
Corrección del calendario: 38 jornadas por liga de 20 equipos.

13

10.8, 14.7
R
Derivación de semillas y determinismo multiplataforma.

14


10.3

R
Prórroga y penaltis en el motor.

15


5.4

R
POT de regens y controlador poblacional.

16


18.2

R

10 atributos visibles del Slice, perfiles renormalizados y correspondencia de ejes.


17

13.1, Anexo B
R
Cinco accesos principales definidos; Noticias añadida.

18


13.5

R
Semáforo corregido (verde ≥ 80).

19

10.1, 19
R
Definición de «duelo desigual» y banda de aceptación (+20 OVR).

20


3.2

R
Aclaración: parámetros de dificultad visibles y solo para el usuario.

21


23.1

R
Sanciones entre liga y copa.

22

9.1, 17, 28
R
Alerta de escala económica (salarios vs. mantenimiento).

23


14.3

R
Aclaración de presupuestos de rendimiento (Slice vs. MVP).

24

15.4, 26.3
R
Invariantes y criterios de aceptación nuevos.

25


27

R
Privacidad y telemetría opt-in.

26

17, 28, Glosario
R
Registro de decisiones actualizado y glosario ampliado.

### Cierre

Boardroom & Touchline debe ofrecer profundidad sin convertir cada jornada en una tarea pesada. El núcleo del
producto es un ciclo claro: comprender el contexto, tomar decisiones, simular y aprender del resultado. Las
cifras iniciales son hipótesis de diseño; el comportamiento final debe emerger de pruebas reproducibles y de
una economía futbolística sostenible.
Fin del documento — GDD v1.3.0.
