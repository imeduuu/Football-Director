using System;
using System.IO;
using System.Linq;
using BT.Core.Domain;
using BT.Core.Sim;
using BT.Data;
using Microsoft.Data.Sqlite;
using PlayerAttribute = BT.Core.Domain.Attribute;
using Xunit;

namespace BT.Core.Tests;

public sealed class SqliteSaveDatabaseTests
{
    [Fact]
    public void OpenCreatesMigratedSaveWithForeignKeysAndIntegrityChecksEnabled()
    {
        var path = Path.Combine(Path.GetTempPath(), "bt-save-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            using var save = SqliteSaveDatabase.Open(path);
            using var command = save.Connection.CreateCommand();
            command.CommandText = "SELECT value FROM meta WHERE key='schema_version';";
            Assert.Equal("4", command.ExecuteScalar());
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
