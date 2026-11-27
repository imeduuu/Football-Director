-- Matchday closure history and rebuildable projections (schema v2).
-- Apply inside BEGIN IMMEDIATE; update meta.schema_version to 2 only after success.
-- Result identity is fixture_id. The unique index is intentionally repeated even
-- though schema v1 already declares UNIQUE, so migrated databases assert the invariant.
CREATE UNIQUE INDEX IF NOT EXISTS ux_matches_fixture_id ON matches(fixture_id);

ALTER TABLE matches ADD COLUMN duration_minutes INTEGER NOT NULL DEFAULT 90
    CHECK (duration_minutes BETWEEN 1 AND 120);

CREATE TABLE match_team_stats (
    match_id TEXT NOT NULL,
    club_id TEXT NOT NULL,
    possession REAL NOT NULL CHECK (possession BETWEEN 0 AND 100),
    shots INTEGER NOT NULL CHECK (shots >= 0),
    shots_on_target INTEGER NOT NULL CHECK (shots_on_target BETWEEN 0 AND shots),
    expected_goals REAL NOT NULL CHECK (expected_goals >= 0),
    zone_defence_left INTEGER NOT NULL CHECK (zone_defence_left >= 0),
    zone_defence_centre INTEGER NOT NULL CHECK (zone_defence_centre >= 0),
    zone_defence_right INTEGER NOT NULL CHECK (zone_defence_right >= 0),
    zone_midfield_left INTEGER NOT NULL CHECK (zone_midfield_left >= 0),
    zone_midfield_centre INTEGER NOT NULL CHECK (zone_midfield_centre >= 0),
    zone_midfield_right INTEGER NOT NULL CHECK (zone_midfield_right >= 0),
    zone_attack_left INTEGER NOT NULL CHECK (zone_attack_left >= 0),
    zone_attack_centre INTEGER NOT NULL CHECK (zone_attack_centre >= 0),
    zone_attack_right INTEGER NOT NULL CHECK (zone_attack_right >= 0),
    PRIMARY KEY (match_id, club_id),
    FOREIGN KEY (match_id) REFERENCES matches(match_id) ON DELETE CASCADE,
    FOREIGN KEY (club_id) REFERENCES clubs(club_id) ON DELETE RESTRICT
) WITHOUT ROWID;

CREATE TABLE match_appearances (
    match_id TEXT NOT NULL,
    player_id TEXT NOT NULL,
    club_id TEXT NOT NULL,
    minutes_played INTEGER NOT NULL CHECK (minutes_played BETWEEN 1 AND 120),
    started INTEGER NOT NULL CHECK (started IN (0,1)),
    rating REAL NOT NULL CHECK (rating BETWEEN 3 AND 10),
    PRIMARY KEY (match_id, player_id),
    FOREIGN KEY (match_id) REFERENCES matches(match_id) ON DELETE CASCADE,
    FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE RESTRICT,
    FOREIGN KEY (club_id) REFERENCES clubs(club_id) ON DELETE RESTRICT
) WITHOUT ROWID;

CREATE TABLE player_match_stats (
    match_id TEXT NOT NULL,
    player_id TEXT NOT NULL,
    goals INTEGER NOT NULL DEFAULT 0 CHECK (goals >= 0),
    assists INTEGER NOT NULL DEFAULT 0 CHECK (assists >= 0),
    shots INTEGER NOT NULL DEFAULT 0 CHECK (shots >= 0),
    shots_on_target INTEGER NOT NULL DEFAULT 0 CHECK (shots_on_target BETWEEN 0 AND shots),
    passes INTEGER NOT NULL DEFAULT 0 CHECK (passes >= 0),
    tackles_interceptions INTEGER NOT NULL DEFAULT 0 CHECK (tackles_interceptions >= 0),
    saves INTEGER NOT NULL DEFAULT 0 CHECK (saves >= 0),
    yellow_cards INTEGER NOT NULL DEFAULT 0 CHECK (yellow_cards >= 0),
    red_cards INTEGER NOT NULL DEFAULT 0 CHECK (red_cards >= 0),
    PRIMARY KEY (match_id, player_id),
    FOREIGN KEY (match_id, player_id) REFERENCES match_appearances(match_id, player_id) ON DELETE CASCADE
) WITHOUT ROWID;

CREATE TABLE player_match_injuries (
    match_id TEXT NOT NULL,
    event_no INTEGER NOT NULL,
    player_id TEXT NOT NULL,
    club_id TEXT NOT NULL,
    minute INTEGER NOT NULL CHECK (minute BETWEEN 0 AND 120),
    recovery_days INTEGER NOT NULL CHECK (recovery_days BETWEEN 1 AND 30),
    availability TEXT NOT NULL CHECK (availability IN ('tocando','lesionado')),
    PRIMARY KEY (match_id, event_no),
    FOREIGN KEY (match_id, event_no) REFERENCES match_events(match_id, event_no) ON DELETE CASCADE,
    FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE RESTRICT,
    FOREIGN KEY (club_id) REFERENCES clubs(club_id) ON DELETE RESTRICT
) WITHOUT ROWID;

CREATE TABLE player_availability (
    player_id TEXT PRIMARY KEY NOT NULL,
    availability TEXT NOT NULL CHECK (availability IN ('disponible','tocando','lesionado')),
    days_remaining INTEGER NOT NULL CHECK (days_remaining BETWEEN 0 AND 30),
    injury_match_id TEXT,
    injury_event_no INTEGER,
    CHECK ((availability = 'disponible' AND days_remaining = 0) OR
           (availability <> 'disponible' AND days_remaining > 0)),
    CHECK ((injury_match_id IS NULL) = (injury_event_no IS NULL)),
    FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE,
    FOREIGN KEY (injury_match_id, injury_event_no)
        REFERENCES player_match_injuries(match_id, event_no) ON DELETE SET NULL
) WITHOUT ROWID;

CREATE TABLE player_discipline (
    player_id TEXT NOT NULL,
    accumulation_group TEXT NOT NULL,
    yellow_count INTEGER NOT NULL DEFAULT 0 CHECK (yellow_count >= 0),
    service_competition_id TEXT,
    pending_matches INTEGER NOT NULL DEFAULT 0 CHECK (pending_matches >= 0),
    cause TEXT,
    origin_fixture_id TEXT,
    rule_version TEXT NOT NULL,
    PRIMARY KEY (player_id, accumulation_group, service_competition_id),
    FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE,
    FOREIGN KEY (origin_fixture_id) REFERENCES fixtures(fixture_id) ON DELETE RESTRICT
) WITHOUT ROWID;

CREATE INDEX ix_match_appearances_player ON match_appearances(player_id, match_id);
CREATE INDEX ix_injuries_player ON player_match_injuries(player_id, match_id);
CREATE INDEX ix_discipline_service ON player_discipline(service_competition_id, pending_matches, player_id);
