# Preparar y persistir entradas de partido

`SqliteMatchInputStore` guarda un snapshot de los datos ya resueltos antes de
simular un fixture. La capa que prepara el partido debe resolver, usando el
estado vigente de la partida, los once titulares, suplentes elegibles, posición,
fitness, moral, dorsal, condición de jugador extra comunitario, estado de
disponibilidad/sanción, los 18 atributos efectivos que consume el motor, las
tácticas de ambos clubes, la semilla, la versión del motor y la condición de
cancha neutral. También entrega el identificador de competición y el JSON
exacto/versionado de sus reglas. No se recalculan atributos ni se sustituyen
valores ausentes durante la carga.

Guardar con `new SqliteMatchInputStore(connection).Save(fixtureId,
competitionId, competitionRulesJson, input)` dentro del flujo de preparación;
leer con `Load(fixtureId)`. `Save` reemplaza el snapshot completo en una
transacción SQLite. `Load` falla si falta contexto, equipo, jugador o cualquiera
de los 18 atributos de un jugador. La migración 003 añade estas tablas y eleva
el esquema a v3 sin cambiar las migraciones anteriores.
