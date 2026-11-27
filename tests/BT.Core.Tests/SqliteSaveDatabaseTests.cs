using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BT.Core.Domain;
using BT.Core.Sim;
using BT.Data;
using Microsoft.Data.Sqlite;
using PlayerAttribute = BT.Core.Domain.Attribute;
using Xunit;

namespace BT.Core.Tests;

public sealed class SqliteSaveDatabaseTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void ConsecutiveMigrationsPreserveWorldDataFromEverySupportedVersion(int startingVersion)
    {
        var path = Path.Combine(Path.GetTempPath(), "bt-migration-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, Pooling = false }.ToString()))
            {
                connection.Open();
                Execute(connection, ReadResource(typeof(SqliteSaveDatabaseTests).Assembly, "schema_v1.sql"));
                Execute(connection, "INSERT INTO meta(key,value) VALUES('schema_version','1'); INSERT INTO clubs(club_id,name,nationality) VALUES('keep-club','Keep Club','Lumenia'); INSERT INTO players(player_id,club_id,full_name,age,nationality,position,ovr) VALUES('keep-player','keep-club','Keep Player',20,'Lumenia','CM',60);");
                for (var next = 2; next <= startingVersion; next++)
                {
                    var script = ReadResource(typeof(SqliteSaveDatabase).Assembly, $"{next:000}_" + (next switch { 2 => "matchday_closure.sql", 3 => "match_input_snapshots.sql", 4 => "knockout_match_inputs.sql", 5 => "match_event_records.sql", _ => "versioned_discipline.sql" }));
                    using var transaction = connection.BeginTransaction();
                    Execute(connection, script, transaction);
                    using var update = connection.CreateCommand(); update.Transaction = transaction; update.CommandText = "UPDATE meta SET value=$v WHERE key='schema_version';"; update.Parameters.AddWithValue("$v", next.ToString()); update.ExecuteNonQuery();
                    transaction.Commit();
                }
                if (startingVersion >= 2) Execute(connection, "INSERT INTO player_discipline(player_id,accumulation_group,yellow_count,service_competition_id,pending_matches,cause,origin_fixture_id,rule_version) VALUES('keep-player','league-yellow',3,'legacy-league',1,'directRed',NULL,'r1');");
            }
            using (var save = SqliteSaveDatabase.Open(path))
            using (var check = save.Connection.CreateCommand())
            {
                check.CommandText = "SELECT value FROM meta WHERE key='schema_version';"; Assert.Equal("6", check.ExecuteScalar());
                check.CommandText = "SELECT name FROM clubs WHERE club_id='keep-club';"; Assert.Equal("Keep Club", check.ExecuteScalar());
                check.CommandText = "SELECT count(*) FROM players WHERE player_id='keep-player';"; Assert.Equal(1L, check.ExecuteScalar());
                if (startingVersion >= 2)
                {
                    check.CommandText = "SELECT yellow_count || ':' || pending_matches || ':' || discipline_id || ':' || rules_package_id FROM player_discipline WHERE player_id='keep-player';";
                    Assert.Equal("3:1:keep-player:league-yellow:legacy-league:r1", check.ExecuteScalar());
                }
                check.CommandText = "PRAGMA foreign_key_check;"; using (var rows = check.ExecuteReader()) Assert.False(rows.Read());
                check.CommandText = "PRAGMA integrity_check;"; Assert.Equal("ok", check.ExecuteScalar());
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
            if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
        }
    }

    private static string ReadResource(Assembly assembly, string suffix)
    {
        var name = assembly.GetManifestResourceNames().Single(x => x.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!; using var reader = new System.IO.StreamReader(stream, Encoding.UTF8); return reader.ReadToEnd();
    }
    private static void Execute(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    { using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql; command.ExecuteNonQuery(); }

    [Fact]
    public void OpenCreatesMigratedSaveWithForeignKeysAndIntegrityChecksEnabled()
    {
        var path = Path.Combine(Path.GetTempPath(), "bt-save-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            using var save = SqliteSaveDatabase.Open(path);
            using var command = save.Connection.CreateCommand();
            command.CommandText = "SELECT value FROM meta WHERE key='schema_version';";
            Assert.Equal("6", command.ExecuteScalar());
            command.CommandText = "SELECT is_knockout FROM fixture_match_context WHERE 0;";
            command.CommandText = "PRAGMA foreign_keys;";
            Assert.Equal(1L, command.ExecuteScalar());
            command.CommandText = "PRAGMA foreign_key_check;";
            using (var reader = command.ExecuteReader()) Assert.False(reader.Read());
            command.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", command.ExecuteScalar());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
            if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
        }
    }

    [Fact]
    public void PreparedMatchInputRoundTripsAllSnapshotsThroughSqlite()
    {
        var path = Path.Combine(Path.GetTempPath(), "bt-save-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            using var save = SqliteSaveDatabase.Open(path);
            using (var setup = save.Connection.CreateCommand())
            {
                setup.CommandText = @"INSERT INTO clubs VALUES ('home','Home','Lumenia'),('away','Away','Lumenia');
INSERT INTO fixtures(fixture_id,season_id,round_no,home_club_id,away_club_id,scheduled_day) VALUES ('fixture','season',1,'home','away',1);
INSERT INTO players(player_id,club_id,full_name,age,nationality,position,ovr) VALUES
('h0','home','Home 0',20,'Lumenia','GK',60),('h1','home','Home 1',20,'Lumenia','CB',60),('h2','home','Home 2',20,'Lumenia','CB',60),('h3','home','Home 3',20,'Lumenia','CB',60),('h4','home','Home 4',20,'Lumenia','CB',60),('h5','home','Home 5',20,'Lumenia','CB',60),('h6','home','Home 6',20,'Lumenia','CB',60),('h7','home','Home 7',20,'Lumenia','CB',60),('h8','home','Home 8',20,'Lumenia','CB',60),('h9','home','Home 9',20,'Lumenia','CB',60),('h10','home','Home 10',20,'Lumenia','CB',60),('hb','home','Home bench',20,'Lumenia','GK',60),
('a0','away','Away 0',20,'Lumenia','GK',60),('a1','away','Away 1',20,'Lumenia','CB',60),('a2','away','Away 2',20,'Lumenia','CB',60),('a3','away','Away 3',20,'Lumenia','CB',60),('a4','away','Away 4',20,'Lumenia','CB',60),('a5','away','Away 5',20,'Lumenia','CB',60),('a6','away','Away 6',20,'Lumenia','CB',60),('a7','away','Away 7',20,'Lumenia','CB',60),('a8','away','Away 8',20,'Lumenia','CB',60),('a9','away','Away 9',20,'Lumenia','CB',60),('a10','away','Away 10',20,'Lumenia','CB',60),('ab','away','Away bench',20,'Lumenia','GK',60);";
                setup.ExecuteNonQuery();
            }
            var attrs = Enum.GetValues(typeof(PlayerAttribute)).Cast<PlayerAttribute>().ToDictionary(attribute => attribute, attribute => (int)attribute + 31);
            MatchPlayerInput Player(string id, Position position, int shirt) => new MatchPlayerInput(id, position, attrs, 73, 64, shirt, shirt % 2 == 0);
            MatchTeamInput Team(string id, string prefix) => new MatchTeamInput(id,
                new[] { Player(prefix + "0", Position.Goalkeeper, 1) }.Concat(Enumerable.Range(1, 10).Select(i => Player(prefix + i, Position.CentreBack, i + 1))).ToArray(),
                new[] { Player(prefix + "b", Position.Goalkeeper, 12) },
                new MatchTactics("4-3-3", TacticalMentality.Attacking, PassingStyle.Direct, PressingStyle.Gegenpressing, DefensiveLine.High, TeamWidth.Wide));
            var expected = new MatchInput(Team("home", "h"), Team("away", "a"), 18446744073709551610UL, EngineVersion.V10_10, true, true);
            const string rules = "{\"id\":\"league\",\"version\":\"r7\",\"yellowThreshold\":4}";
            var store = new SqliteMatchInputStore(save.Connection);
            store.Save("fixture", "league", rules, expected);
            var prepared = store.LoadPrepared("fixture");
            var actual = prepared.Input;
            Assert.Equal(expected.Seed, actual.Seed);
            Assert.Equal(expected.EngineVersion, actual.EngineVersion);
            Assert.Equal(expected.IsNeutralVenue, actual.IsNeutralVenue);
            Assert.Equal(expected.IsKnockout, actual.IsKnockout);
            Assert.Equal("league", prepared.CompetitionId);
            Assert.Equal(rules, prepared.CompetitionRulesJson);
            Assert.Equal(rules, ReadRules(save.Connection));
            AssertTeam(expected.HomeTeam, actual.HomeTeam);
            AssertTeam(expected.AwayTeam, actual.AwayTeam);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
            if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
        }
    }

    private static string ReadRules(SqliteConnection connection)
    {
        using var command = connection.CreateCommand(); command.CommandText = "SELECT competition_rules_json FROM fixture_match_context WHERE fixture_id='fixture';";
        return (string)command.ExecuteScalar()!;
    }

    private static void AssertTeam(MatchTeamInput expected, MatchTeamInput actual)
    {
        Assert.Equal(expected.TeamId, actual.TeamId);
        Assert.Equal(expected.Tactics.FormationId, actual.Tactics.FormationId);
        Assert.Equal(expected.Tactics.Mentality, actual.Tactics.Mentality);
        Assert.Equal(expected.Tactics.Passing, actual.Tactics.Passing);
        Assert.Equal(expected.Tactics.Pressing, actual.Tactics.Pressing);
        Assert.Equal(expected.Tactics.DefensiveLine, actual.Tactics.DefensiveLine);
        Assert.Equal(expected.Tactics.Width, actual.Tactics.Width);
        AssertPlayers(expected.Starters, actual.Starters);
        AssertPlayers(expected.Substitutes, actual.Substitutes);
    }

    private static void AssertPlayers(System.Collections.Generic.IReadOnlyList<MatchPlayerInput> expected, System.Collections.Generic.IReadOnlyList<MatchPlayerInput> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].PlayerId, actual[i].PlayerId); Assert.Equal(expected[i].Position, actual[i].Position);
            Assert.Equal(expected[i].EffectiveAttributes, actual[i].EffectiveAttributes); Assert.Equal(expected[i].Fitness, actual[i].Fitness);
            Assert.Equal(expected[i].Morale, actual[i].Morale); Assert.Equal(expected[i].ShirtNumber, actual[i].ShirtNumber);
            Assert.Equal(expected[i].IsExtraCommunity, actual[i].IsExtraCommunity);
            Assert.Equal(expected[i].IsInjured, actual[i].IsInjured); Assert.Equal(expected[i].IsSuspended, actual[i].IsSuspended);
        }
    }

    [Fact]
    public void OpenRejectsUnsupportedFutureSchemaVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), "bt-save-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            using (var save = SqliteSaveDatabase.Open(path))
            using (var command = save.Connection.CreateCommand())
            {
                command.CommandText = "UPDATE meta SET value='99' WHERE key='schema_version';";
                command.ExecuteNonQuery();
            }
            Assert.Throws<InvalidDataException>(() => SqliteSaveDatabase.Open(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
            if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
        }
    }
}
