-- Boardroom & Touchline — esquema de guardado del Vertical Slice (v1).
-- Dialecto: SQLite 3. Incluido en cada save slot según GDD §§14.4–14.6.
-- Ejecutar al crear un save con PRAGMA foreign_keys = ON; journal_mode = WAL.
-- El llamador debe comprobar foreign_keys = 1; SQLite no activa FK por defecto.
--
-- Propiedad de datos: meta pertenece al save slot y es la autoridad de su versión,
-- campaña y estado de generación. clubs/players son el mundo ficticio de esa partida;
-- player_attributes pertenece al jugador persistido. lineups y fixtures son datos
-- de planificación de la campaña. standings es una proyección derivada de fixtures/
-- matches y se puede reconstruir. matches y match_events son el resultado histórico
-- autoritativo de una simulación ya confirmada. Ninguna tabla es compartida entre
-- ranuras; no se usa ID autoincremental como identidad de dominio.
--
-- IDs TEXT son claves opacas, estables y asignadas por el importador/generador
-- determinista (por ejemplo club:... / player:...). No generar IDs aleatorios durante
-- una carga ni usar rowid como referencia externa. Timestamps de pared no participan
-- en resultados deterministas.
--
-- Migraciones: meta.schema_version es un entero monotónico; la primera versión es 1.
-- Migrar dentro de BEGIN IMMEDIATE, aplicar migraciones consecutivas y actualizar la
-- versión al final; rollback completo ante cualquier fallo. Hacer copia consistente
-- del archivo antes de migrar, verificar PRAGMA foreign_key_check e integrity_check,
-- y conservar el original si falla. No editar migraciones ya publicadas. Una versión
-- superior no soportada se abre en modo solo lectura/error explícito. Cambios de
-- significado de IDs, orden o fórmulas requieren también versionar datos/generador
-- en meta; migrar estructura no autoriza recalcular partidos históricos.

PRAGMA foreign_keys = ON;

BEGIN IMMEDIATE;

CREATE TABLE meta (
    key   TEXT PRIMARY KEY NOT NULL,
    value TEXT NOT NULL,
    CHECK (length(key) BETWEEN 1 AND 80)
) WITHOUT ROWID;

-- Immutable generation identity for this save's world (GDD 14.5).
CREATE TABLE world_generation (
    world_id          INTEGER PRIMARY KEY CHECK (world_id = 1),
    seed_world        TEXT NOT NULL CHECK (seed_world NOT GLOB '*[^0-9]*' AND length(seed_world) BETWEEN 1 AND 20),
    generator_version TEXT NOT NULL CHECK (length(trim(generator_version)) BETWEEN 1 AND 40),
    CHECK (seed_world = '0' OR substr(seed_world, 1, 1) BETWEEN '1' AND '9'),
    CHECK (length(seed_world) < 20 OR seed_world <= '18446744073709551615')
);

-- Claves iniciales recomendadas: schema_version='1', save_id, season_id,
-- current_round (0..18), world_seed, generator_version, rules_version.
-- La versión del esquema no se infiere del nombre del archivo.

CREATE TABLE clubs (
    club_id     TEXT PRIMARY KEY NOT NULL,
    name        TEXT NOT NULL,
    nationality TEXT NOT NULL,
    CHECK (length(club_id) BETWEEN 1 AND 120),
    CHECK (length(trim(name)) > 0),
    CHECK (length(trim(nationality)) > 0)
) WITHOUT ROWID;

CREATE TABLE players (
    player_id   TEXT PRIMARY KEY NOT NULL,
    club_id     TEXT NOT NULL,
    full_name   TEXT NOT NULL,
    age         INTEGER NOT NULL CHECK (age >= 0),
    nationality TEXT NOT NULL,
    position    TEXT NOT NULL CHECK (position IN (
        'GK','CB','LB','RB','DM','CM','AM','LW','RW','ST'
    )),
    ovr         INTEGER NOT NULL CHECK (ovr BETWEEN 1 AND 99),
    fit         INTEGER NOT NULL DEFAULT 100 CHECK (fit BETWEEN 0 AND 100),
    morale      INTEGER NOT NULL DEFAULT 50 CHECK (morale BETWEEN 0 AND 100),
    FOREIGN KEY (club_id) REFERENCES clubs(club_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (length(player_id) BETWEEN 1 AND 120),
    CHECK (length(trim(full_name)) > 0),
    CHECK (length(trim(nationality)) > 0)
) WITHOUT ROWID;

CREATE INDEX ix_players_club_position ON players(club_id, position, player_id);

-- Slice §18.2 attributes only: POR, ENT, MAR, AER, PAS, REG, FIN, VEL, RES, POS.
CREATE TABLE player_attributes (
    player_id   TEXT NOT NULL,
    attribute   TEXT NOT NULL CHECK (attribute IN (
        'POR','ENT','MAR','AER','PAS','REG','FIN','VEL','RES','POS'
    )),
    value       INTEGER NOT NULL CHECK (value BETWEEN 1 AND 99),
    PRIMARY KEY (player_id, attribute),
    FOREIGN KEY (player_id) REFERENCES players(player_id)
        ON UPDATE RESTRICT ON DELETE CASCADE
) WITHOUT ROWID;

CREATE TABLE fixtures (
    fixture_id    TEXT PRIMARY KEY NOT NULL,
    season_id     TEXT NOT NULL,
    round_no      INTEGER NOT NULL CHECK (round_no BETWEEN 1 AND 18),
    home_club_id  TEXT NOT NULL,
    away_club_id  TEXT NOT NULL,
    scheduled_day INTEGER NOT NULL CHECK (scheduled_day >= 0),
    status        TEXT NOT NULL DEFAULT 'scheduled'
                  CHECK (status IN ('scheduled','played','postponed')),
    UNIQUE (season_id, round_no, home_club_id),
    UNIQUE (season_id, round_no, away_club_id),
    CHECK (home_club_id <> away_club_id),
    CHECK (length(fixture_id) BETWEEN 1 AND 120),
    CHECK (length(trim(season_id)) > 0),
    FOREIGN KEY (home_club_id) REFERENCES clubs(club_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    FOREIGN KEY (away_club_id) REFERENCES clubs(club_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) WITHOUT ROWID;

CREATE INDEX ix_fixtures_round_status ON fixtures(season_id, round_no, status);
CREATE INDEX ix_fixtures_home ON fixtures(home_club_id, season_id, round_no);
CREATE INDEX ix_fixtures_away ON fixtures(away_club_id, season_id, round_no);

CREATE TABLE lineups (
    fixture_id TEXT NOT NULL,
    club_id    TEXT NOT NULL,
    player_id  TEXT NOT NULL,
    slot_no    INTEGER NOT NULL CHECK (slot_no BETWEEN 1 AND 11),
    role       TEXT NOT NULL CHECK (role IN (
        'GK_CLASSIC','CB_MARKER','FB_DEFENSIVE','DM_ORGANIZER',
        'CM_BOX_TO_BOX','WINGER','ST_ADVANCED'
    )),
    is_captain INTEGER NOT NULL DEFAULT 0 CHECK (is_captain IN (0,1)),
    PRIMARY KEY (fixture_id, club_id, player_id),
    UNIQUE (fixture_id, club_id, slot_no),
    FOREIGN KEY (fixture_id) REFERENCES fixtures(fixture_id)
        ON UPDATE RESTRICT ON DELETE CASCADE,
    FOREIGN KEY (club_id) REFERENCES clubs(club_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    FOREIGN KEY (player_id) REFERENCES players(player_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT
) WITHOUT ROWID;

CREATE INDEX ix_lineups_player ON lineups(player_id, fixture_id);

CREATE TABLE standings (
    season_id      TEXT NOT NULL,
    club_id        TEXT NOT NULL,
    played         INTEGER NOT NULL DEFAULT 0 CHECK (played BETWEEN 0 AND 18),
    won            INTEGER NOT NULL DEFAULT 0 CHECK (won BETWEEN 0 AND 18),
    drawn          INTEGER NOT NULL DEFAULT 0 CHECK (drawn BETWEEN 0 AND 18),
    lost           INTEGER NOT NULL DEFAULT 0 CHECK (lost BETWEEN 0 AND 18),
    goals_for      INTEGER NOT NULL DEFAULT 0 CHECK (goals_for >= 0),
    goals_against  INTEGER NOT NULL DEFAULT 0 CHECK (goals_against >= 0),
    points         INTEGER NOT NULL DEFAULT 0 CHECK (points BETWEEN 0 AND 54),
    PRIMARY KEY (season_id, club_id),
    FOREIGN KEY (club_id) REFERENCES clubs(club_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (length(trim(season_id)) > 0),
    CHECK (won + drawn + lost = played),
    CHECK (points = 3 * won + drawn)
) WITHOUT ROWID;

CREATE INDEX ix_standings_order ON standings(
    season_id, points DESC, (goals_for - goals_against) DESC, goals_for DESC, club_id
);

CREATE TABLE matches (
    match_id      TEXT PRIMARY KEY NOT NULL,
    fixture_id    TEXT NOT NULL UNIQUE,
    home_club_id  TEXT NOT NULL,
    away_club_id  TEXT NOT NULL,
    home_score    INTEGER NOT NULL CHECK (home_score >= 0),
    away_score    INTEGER NOT NULL CHECK (away_score >= 0),
    seed          TEXT NOT NULL,
    engine_version TEXT NOT NULL,
    rules_version  TEXT NOT NULL,
    home_xg       REAL NOT NULL CHECK (home_xg >= 0.0),
    away_xg       REAL NOT NULL CHECK (away_xg >= 0.0),
    FOREIGN KEY (fixture_id) REFERENCES fixtures(fixture_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    FOREIGN KEY (home_club_id) REFERENCES clubs(club_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    FOREIGN KEY (away_club_id) REFERENCES clubs(club_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (home_club_id <> away_club_id),
    CHECK (length(match_id) BETWEEN 1 AND 120),
    CHECK (length(trim(seed)) > 0),
    CHECK (length(trim(engine_version)) > 0),
    CHECK (length(trim(rules_version)) > 0)
) WITHOUT ROWID;

CREATE INDEX ix_matches_home ON matches(home_club_id, match_id);
CREATE INDEX ix_matches_away ON matches(away_club_id, match_id);

CREATE TABLE match_events (
    match_id   TEXT NOT NULL,
    event_no   INTEGER NOT NULL CHECK (event_no BETWEEN 1 AND 1000),
    minute     INTEGER NOT NULL CHECK (minute BETWEEN 0 AND 90),
    event_type TEXT NOT NULL CHECK (event_type IN (
        'goal','shot','shot_on_target','yellow_card','red_card','substitution','injury'
    )),
    club_id    TEXT NOT NULL,
    player_id  TEXT,
    related_player_id TEXT,
    xg         REAL CHECK (xg IS NULL OR xg BETWEEN 0.0 AND 1.0),
    detail_code TEXT,
    PRIMARY KEY (match_id, event_no),
    FOREIGN KEY (match_id) REFERENCES matches(match_id)
        ON UPDATE RESTRICT ON DELETE CASCADE,
    FOREIGN KEY (club_id) REFERENCES clubs(club_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    FOREIGN KEY (player_id) REFERENCES players(player_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    FOREIGN KEY (related_player_id) REFERENCES players(player_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (player_id IS NULL OR related_player_id IS NULL OR player_id <> related_player_id)
) WITHOUT ROWID;

CREATE INDEX ix_match_events_player ON match_events(player_id, match_id);
CREATE INDEX ix_match_events_club_type ON match_events(club_id, event_type, match_id);

-- Enforce club participation and Slice roster ownership at the persistence boundary.
CREATE TRIGGER fixtures_no_club_twice_insert
BEFORE INSERT ON fixtures
WHEN EXISTS (
    SELECT 1 FROM fixtures f
    WHERE f.season_id = NEW.season_id AND f.round_no = NEW.round_no
      AND (f.home_club_id IN (NEW.home_club_id, NEW.away_club_id)
           OR f.away_club_id IN (NEW.home_club_id, NEW.away_club_id))
)
BEGIN
    SELECT RAISE(ABORT, 'a club cannot play more than once in a round');
END;

CREATE TRIGGER fixtures_no_club_twice_update
BEFORE UPDATE OF season_id, round_no, home_club_id, away_club_id ON fixtures
WHEN EXISTS (
    SELECT 1 FROM fixtures f
    WHERE f.fixture_id <> OLD.fixture_id AND f.season_id = NEW.season_id AND f.round_no = NEW.round_no
      AND (f.home_club_id IN (NEW.home_club_id, NEW.away_club_id)
           OR f.away_club_id IN (NEW.home_club_id, NEW.away_club_id))
)
BEGIN
    SELECT RAISE(ABORT, 'a club cannot play more than once in a round');
END;

CREATE TRIGGER fixtures_insert_not_played
BEFORE INSERT ON fixtures
WHEN NEW.status = 'played'
BEGIN
    SELECT RAISE(ABORT, 'fixtures become played only after a match is inserted');
END;

CREATE TRIGGER fixtures_preserve_lineups_on_club_change
BEFORE UPDATE OF home_club_id, away_club_id ON fixtures
WHEN EXISTS (
    SELECT 1 FROM lineups l WHERE l.fixture_id = OLD.fixture_id
      AND l.club_id NOT IN (NEW.home_club_id, NEW.away_club_id)
) OR EXISTS (
    SELECT 1 FROM matches m WHERE m.fixture_id = OLD.fixture_id
      AND (m.home_club_id <> NEW.home_club_id OR m.away_club_id <> NEW.away_club_id)
)
BEGIN
    SELECT RAISE(ABORT, 'fixture club changes conflict with saved lineups or match');
END;

CREATE TRIGGER lineups_validate_insert
BEFORE INSERT ON lineups
WHEN NOT EXISTS (
        SELECT 1 FROM fixtures f
        WHERE f.fixture_id = NEW.fixture_id
          AND NEW.club_id IN (f.home_club_id, f.away_club_id)
    ) OR NOT EXISTS (
        SELECT 1 FROM players p WHERE p.player_id = NEW.player_id AND p.club_id = NEW.club_id
    )
BEGIN
    SELECT RAISE(ABORT, 'lineup club, fixture, and player ownership must agree');
END;

CREATE TRIGGER lineups_validate_update
BEFORE UPDATE OF fixture_id, club_id, player_id ON lineups
WHEN NOT EXISTS (
        SELECT 1 FROM fixtures f
        WHERE f.fixture_id = NEW.fixture_id
          AND NEW.club_id IN (f.home_club_id, f.away_club_id)
    ) OR NOT EXISTS (
        SELECT 1 FROM players p WHERE p.player_id = NEW.player_id AND p.club_id = NEW.club_id
    )
BEGIN
    SELECT RAISE(ABORT, 'lineup club, fixture, and player ownership must agree');
END;

CREATE TRIGGER matches_validate_fixture_insert
BEFORE INSERT ON matches
WHEN NOT EXISTS (
    SELECT 1 FROM fixtures f WHERE f.fixture_id = NEW.fixture_id
      AND f.home_club_id = NEW.home_club_id AND f.away_club_id = NEW.away_club_id
)
BEGIN
    SELECT RAISE(ABORT, 'match clubs must match its fixture');
END;

CREATE TRIGGER matches_validate_fixture_update
BEFORE UPDATE OF fixture_id, home_club_id, away_club_id ON matches
WHEN NOT EXISTS (
    SELECT 1 FROM fixtures f WHERE f.fixture_id = NEW.fixture_id
      AND f.home_club_id = NEW.home_club_id AND f.away_club_id = NEW.away_club_id
)
BEGIN
    SELECT RAISE(ABORT, 'match clubs must match its fixture');
END;

CREATE TRIGGER match_events_validate_participant_insert
BEFORE INSERT ON match_events
WHEN NOT EXISTS (
        SELECT 1 FROM matches m WHERE m.match_id = NEW.match_id
          AND NEW.club_id IN (m.home_club_id, m.away_club_id)
    )
BEGIN
    SELECT RAISE(ABORT, 'match event club must be a side in the match');
END;

CREATE TRIGGER match_events_validate_participant_update
BEFORE UPDATE OF match_id, club_id, player_id ON match_events
WHEN NOT EXISTS (
        SELECT 1 FROM matches m WHERE m.match_id = NEW.match_id
          AND NEW.club_id IN (m.home_club_id, m.away_club_id)
    )
BEGIN
    SELECT RAISE(ABORT, 'match event club must be a side in the match');
END;

CREATE TRIGGER fixtures_mark_played_after_match
AFTER INSERT ON matches
BEGIN
    UPDATE fixtures SET status = 'played' WHERE fixture_id = NEW.fixture_id;
END;

CREATE TRIGGER fixtures_require_match_before_played
BEFORE UPDATE OF status ON fixtures
WHEN NEW.status = 'played' AND NOT EXISTS (
    SELECT 1 FROM matches m WHERE m.fixture_id = NEW.fixture_id
)
BEGIN
    SELECT RAISE(ABORT, 'played fixtures require a persisted match');
END;

CREATE TRIGGER fixtures_reopen_after_match_delete
AFTER DELETE ON matches
BEGIN
    UPDATE fixtures SET status = 'scheduled' WHERE fixture_id = OLD.fixture_id;
END;

COMMIT;

-- standings se recalcula/actualiza en la misma transacción que el cierre de jornada;
-- la capa de aplicación debe confirmar toda la operación de jornada en una sola transacción.
