-- Preserve every simulator event, including event kinds not represented by the
-- compact, constrained v1 match_events projection.
CREATE TABLE match_event_records (
    match_id TEXT NOT NULL,
    event_no INTEGER NOT NULL CHECK (event_no BETWEEN 1 AND 1000),
    minute INTEGER NOT NULL CHECK (minute BETWEEN 0 AND 120),
    event_type TEXT NOT NULL CHECK (length(trim(event_type)) > 0),
    club_id TEXT NOT NULL,
    player_id TEXT,
    related_player_id TEXT,
    injury_days INTEGER NOT NULL DEFAULT 0 CHECK (injury_days BETWEEN 0 AND 30),
    rule_index INTEGER CHECK (rule_index IS NULL OR rule_index >= 0),
    failure_reason TEXT,
    PRIMARY KEY (match_id, event_no),
    FOREIGN KEY (match_id) REFERENCES matches(match_id) ON DELETE CASCADE,
    FOREIGN KEY (club_id) REFERENCES clubs(club_id) ON DELETE RESTRICT,
    FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE RESTRICT,
    FOREIGN KEY (related_player_id) REFERENCES players(player_id) ON DELETE RESTRICT,
    CHECK (player_id IS NULL OR related_player_id IS NULL OR player_id <> related_player_id)
) WITHOUT ROWID;
CREATE INDEX ix_match_event_records_player ON match_event_records(player_id, match_id);

CREATE TRIGGER match_event_records_validate_insert
BEFORE INSERT ON match_event_records
WHEN NOT EXISTS (SELECT 1 FROM matches m WHERE m.match_id=NEW.match_id AND NEW.club_id IN (m.home_club_id,m.away_club_id))
  OR (NEW.player_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM players p WHERE p.player_id=NEW.player_id AND p.club_id=NEW.club_id))
  OR (NEW.related_player_id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM players p WHERE p.player_id=NEW.related_player_id AND p.club_id=NEW.club_id))
BEGIN SELECT RAISE(ABORT,'match event participants must belong to the match side'); END;

CREATE TRIGGER match_appearances_validate_insert
BEFORE INSERT ON match_appearances
WHEN NOT EXISTS (SELECT 1 FROM matches m WHERE m.match_id=NEW.match_id AND NEW.club_id IN (m.home_club_id,m.away_club_id))
  OR NOT EXISTS (SELECT 1 FROM players p WHERE p.player_id=NEW.player_id AND p.club_id=NEW.club_id)
BEGIN SELECT RAISE(ABORT,'appearance player must belong to a match side'); END;

CREATE TRIGGER match_team_stats_validate_insert
BEFORE INSERT ON match_team_stats
WHEN NOT EXISTS (SELECT 1 FROM matches m WHERE m.match_id=NEW.match_id AND NEW.club_id IN (m.home_club_id,m.away_club_id))
BEGIN SELECT RAISE(ABORT,'team statistics must belong to a match side'); END;

CREATE TRIGGER player_match_injuries_validate_insert
BEFORE INSERT ON player_match_injuries
WHEN NOT EXISTS (SELECT 1 FROM players p WHERE p.player_id=NEW.player_id AND p.club_id=NEW.club_id)
BEGIN SELECT RAISE(ABORT,'injured player must belong to the reported club'); END;
