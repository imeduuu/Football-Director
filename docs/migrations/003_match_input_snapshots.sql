-- Prepared match input snapshots (schema v3). Values are captured by the caller
-- after registration, availability, effective attributes and tactics are resolved.
CREATE TABLE fixture_match_context (
    fixture_id TEXT PRIMARY KEY NOT NULL,
    competition_id TEXT NOT NULL,
    competition_rules_json TEXT NOT NULL CHECK (length(trim(competition_rules_json)) > 0),
    seed TEXT NOT NULL CHECK (seed NOT GLOB '*[^0-9]*' AND length(seed) BETWEEN 1 AND 20),
    engine_version INTEGER NOT NULL,
    is_neutral_venue INTEGER NOT NULL CHECK (is_neutral_venue IN (0,1)),
    FOREIGN KEY (fixture_id) REFERENCES fixtures(fixture_id) ON DELETE CASCADE,
    CHECK (seed = '0' OR substr(seed,1,1) BETWEEN '1' AND '9'),
    CHECK (length(seed) < 20 OR seed <= '18446744073709551615')
) WITHOUT ROWID;

CREATE TABLE fixture_match_teams (
    fixture_id TEXT NOT NULL,
    club_id TEXT NOT NULL,
    is_home INTEGER NOT NULL CHECK (is_home IN (0,1)),
    formation_id TEXT NOT NULL,
    mentality TEXT NOT NULL,
    passing TEXT NOT NULL,
    pressing TEXT NOT NULL,
    defensive_line TEXT NOT NULL,
    width TEXT NOT NULL,
    PRIMARY KEY (fixture_id, club_id),
    UNIQUE (fixture_id, is_home),
    FOREIGN KEY (fixture_id) REFERENCES fixture_match_context(fixture_id) ON DELETE CASCADE,
    FOREIGN KEY (club_id) REFERENCES clubs(club_id) ON DELETE RESTRICT
) WITHOUT ROWID;

CREATE TABLE fixture_match_players (
    fixture_id TEXT NOT NULL,
    club_id TEXT NOT NULL,
    player_id TEXT NOT NULL,
    is_starter INTEGER NOT NULL CHECK (is_starter IN (0,1)),
    selection_order INTEGER NOT NULL CHECK (selection_order BETWEEN 1 AND 11),
    position TEXT NOT NULL,
    fitness INTEGER NOT NULL CHECK (fitness BETWEEN 0 AND 100),
    morale INTEGER NOT NULL CHECK (morale BETWEEN 0 AND 100),
    shirt_number INTEGER CHECK (shirt_number IS NULL OR shirt_number BETWEEN 1 AND 99),
    is_extra_community INTEGER NOT NULL CHECK (is_extra_community IN (0,1)),
    is_injured INTEGER NOT NULL CHECK (is_injured IN (0,1)),
    is_suspended INTEGER NOT NULL CHECK (is_suspended IN (0,1)),
    PRIMARY KEY (fixture_id, club_id, player_id),
    UNIQUE (fixture_id, club_id, is_starter, selection_order),
    FOREIGN KEY (fixture_id, club_id) REFERENCES fixture_match_teams(fixture_id, club_id) ON DELETE CASCADE,
    FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE RESTRICT
) WITHOUT ROWID;

CREATE TABLE fixture_match_player_attributes (
    fixture_id TEXT NOT NULL,
    club_id TEXT NOT NULL,
    player_id TEXT NOT NULL,
    attribute TEXT NOT NULL,
    value INTEGER NOT NULL CHECK (value BETWEEN 1 AND 99),
    PRIMARY KEY (fixture_id, club_id, player_id, attribute),
    FOREIGN KEY (fixture_id, club_id, player_id) REFERENCES fixture_match_players(fixture_id, club_id, player_id) ON DELETE CASCADE
) WITHOUT ROWID;
