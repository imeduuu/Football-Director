-- Discipline identity and rule provenance (schema v6).
-- Applied transactionally by SqliteSaveDatabase; version is advanced after this script.
ALTER TABLE player_discipline ADD COLUMN discipline_id TEXT NOT NULL DEFAULT '';
ALTER TABLE player_discipline ADD COLUMN source_competition_id TEXT NOT NULL DEFAULT '';
ALTER TABLE player_discipline ADD COLUMN rules_package_id TEXT NOT NULL DEFAULT '';

UPDATE player_discipline
SET discipline_id = player_id || ':' || accumulation_group || ':' || coalesce(service_competition_id, 'league'),
    source_competition_id = coalesce(service_competition_id, 'league'),
    rules_package_id = rule_version;

CREATE UNIQUE INDEX ux_player_discipline_id ON player_discipline(discipline_id);
CREATE INDEX ix_discipline_source ON player_discipline(source_competition_id, player_id);
