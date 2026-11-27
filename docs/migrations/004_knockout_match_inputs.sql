-- Persist the knockout rule used to determine whether a tied match enters extra time.
ALTER TABLE fixture_match_context
    ADD COLUMN is_knockout INTEGER NOT NULL DEFAULT 0 CHECK (is_knockout IN (0,1));
