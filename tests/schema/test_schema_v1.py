import sqlite3
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCHEMA = ROOT / "docs" / "schema_v1.sql"
MATCHDAY_MIGRATION = ROOT / "docs" / "migrations" / "002_matchday_closure.sql"


class SchemaV1Tests(unittest.TestCase):
    def setUp(self):
        self.connection = sqlite3.connect(":memory:")
        self.connection.executescript(SCHEMA.read_text(encoding="utf-8"))
        self.connection.execute("INSERT INTO clubs VALUES ('home', 'Home', 'CL')")
        self.connection.execute("INSERT INTO clubs VALUES ('away', 'Away', 'CL')")
        self.connection.execute("INSERT INTO clubs VALUES ('third', 'Third', 'CL')")
        self.connection.execute(
            "INSERT INTO players (player_id, club_id, full_name, age, nationality, position, ovr) "
            "VALUES ('p-home', 'home', 'Home Player', 22, 'CL', 'GK', 70)"
        )
        self.connection.execute(
            "INSERT INTO players (player_id, club_id, full_name, age, nationality, position, ovr) "
            "VALUES ('p-away', 'away', 'Away Player', 23, 'CL', 'GK', 70)"
        )
        self.connection.execute(
            "INSERT INTO fixtures (fixture_id, season_id, round_no, home_club_id, away_club_id, scheduled_day) "
            "VALUES ('fixture-1', 'season-1', 1, 'home', 'away', 1)"
        )

    def tearDown(self):
        self.connection.close()

    def test_installs_required_tables_and_enables_foreign_keys(self):
        tables = {
            row[0]
            for row in self.connection.execute(
                "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'"
            )
        }
        self.assertEqual(
            {
                "meta", "world_generation", "clubs", "players", "player_attributes", "lineups",
                "fixtures", "standings", "matches", "match_events",
            },
            tables,
        )
        self.assertEqual(1, self.connection.execute("PRAGMA foreign_keys").fetchone()[0])

    def test_world_generation_persists_uint64_seed_and_generator_version(self):
        self.connection.execute(
            "INSERT INTO world_generation (seed_world, generator_version) VALUES (?, ?)",
            ("18446744073709551615", "20.3"),
        )
        self.assertEqual(
            ("18446744073709551615", "20.3"),
            self.connection.execute("SELECT seed_world, generator_version FROM world_generation").fetchone(),
        )
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute(
                "INSERT INTO world_generation (seed_world, generator_version) VALUES (?, ?)", ("18446744073709551616", "20.3")
            )

    def test_constraints_and_triggers_protect_slice_relationships(self):
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute(
                "INSERT INTO player_attributes VALUES ('p-home', 'PAS', 100)"
            )
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute(
                "INSERT INTO lineups VALUES ('fixture-1', 'away', 'p-home', 1, 'GK_CLASSIC', 0)"
            )
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute(
                "INSERT INTO fixtures (fixture_id, season_id, round_no, home_club_id, away_club_id, scheduled_day) "
                "VALUES ('fixture-2', 'season-1', 1, 'third', 'home', 1)"
            )
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute("UPDATE fixtures SET status = 'played' WHERE fixture_id = 'fixture-1'")
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute(
                "INSERT INTO fixtures (fixture_id, season_id, round_no, home_club_id, away_club_id, scheduled_day, status) "
                "VALUES ('fixture-bad', 'season-1', 2, 'home', 'third', 2, 'played')"
            )
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute(
                "INSERT INTO matches (match_id, fixture_id, home_club_id, away_club_id, home_score, away_score, "
                "seed, engine_version, rules_version, home_xg, away_xg) "
                "VALUES ('match-bad', 'fixture-1', 'away', 'home', 0, 0, '1', '1.0', '1.0', 0.0, 0.0)"
            )

        self.connection.execute(
            "INSERT INTO matches (match_id, fixture_id, home_club_id, away_club_id, home_score, away_score, "
            "seed, engine_version, rules_version, home_xg, away_xg) "
            "VALUES ('match-1', 'fixture-1', 'home', 'away', 1, 0, '1', '1.0', '1.0', 0.8, 0.3)"
        )
        self.assertEqual(
            "played",
            self.connection.execute(
                "SELECT status FROM fixtures WHERE fixture_id = 'fixture-1'"
            ).fetchone()[0],
        )
        self.connection.execute(
            "INSERT INTO match_events (match_id, event_no, minute, event_type, club_id, player_id) "
            "VALUES ('match-1', 1, 10, 'goal', 'home', 'p-home')"
        )
        self.connection.execute("UPDATE players SET club_id = 'third' WHERE player_id = 'p-home'")
        self.assertEqual(1, self.connection.execute(
            "SELECT COUNT(*) FROM match_events WHERE match_id = 'match-1' AND player_id = 'p-home'"
        ).fetchone()[0])
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute(
                "INSERT INTO match_events (match_id, event_no, minute, event_type, club_id, player_id) "
                "VALUES ('match-1', 1, 10, 'goal', 'third', 'p-away')"
            )
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute(
                "INSERT INTO match_events (match_id, event_no, minute, event_type, club_id, xg) "
                "VALUES ('match-1', 2, 10, 'shot', 'home', 1.1)"
            )

    def test_schema_file_bootstraps_and_reopens_a_save(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "save.db"
            connection = sqlite3.connect(path)
            connection.executescript(SCHEMA.read_text(encoding="utf-8"))
            connection.execute("INSERT INTO meta (key, value) VALUES ('schema_version', '1')")
            connection.commit()
            connection.close()

            reopened = sqlite3.connect(path)
            self.assertEqual("1", reopened.execute(
                "SELECT value FROM meta WHERE key = 'schema_version'"
            ).fetchone()[0])
            self.assertEqual([], reopened.execute("PRAGMA foreign_key_check").fetchall())
            self.assertEqual("ok", reopened.execute("PRAGMA integrity_check").fetchone()[0])
            reopened.close()

    def test_matchday_migration_preserves_v1_and_adds_authoritative_history(self):
        self.connection.execute(
            "INSERT INTO matches (match_id, fixture_id, home_club_id, away_club_id, home_score, away_score, "
            "seed, engine_version, rules_version, home_xg, away_xg) "
            "VALUES ('match-1', 'fixture-1', 'home', 'away', 1, 0, '9', '1010', '1', 0.8, 0.2)"
        )
        self.connection.execute(
            "INSERT INTO match_events (match_id, event_no, minute, event_type, club_id, player_id) "
            "VALUES ('match-1', 1, 10, 'goal', 'home', 'p-home')"
        )
        self.connection.commit()
        self.connection.executescript(MATCHDAY_MIGRATION.read_text(encoding="utf-8"))

        self.assertEqual((1, 0), self.connection.execute(
            "SELECT home_score, away_score FROM matches WHERE fixture_id='fixture-1'"
        ).fetchone())
        self.assertEqual(90, self.connection.execute(
            "SELECT duration_minutes FROM matches WHERE fixture_id='fixture-1'"
        ).fetchone()[0])
        self.assertEqual("played", self.connection.execute(
            "SELECT status FROM fixtures WHERE fixture_id='fixture-1'"
        ).fetchone()[0])
        with self.assertRaises(sqlite3.IntegrityError):
            self.connection.execute(
                "INSERT INTO matches (match_id, fixture_id, home_club_id, away_club_id, home_score, away_score, "
                "seed, engine_version, rules_version, home_xg, away_xg) "
                "VALUES ('match-2', 'fixture-1', 'home', 'away', 0, 0, '10', '1010', '1', 0, 0)"
            )
        self.assertEqual([], self.connection.execute("PRAGMA foreign_key_check").fetchall())
        self.assertEqual("ok", self.connection.execute("PRAGMA integrity_check").fetchone()[0])


if __name__ == "__main__":
    unittest.main()
