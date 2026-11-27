using System;
using System.IO;
using System.Linq;
using BT.Core;
using BT.Core.Competition;
using BT.Core.Domain;
using BT.Core.Rng;
using BT.Core.Sim;
using BT.Data;
using PlayerAttribute = BT.Core.Domain.Attribute;
using Xunit;

namespace BT.Core.Tests;

public sealed class SqliteMatchdayStoreTests
{
    [Fact]
    public void ReplacesStandingsInsideSqliteTransactionAndPersistsRows()
    {
        WithSave(save =>
        {
            Seed(save);
            var table = LeagueStandings.Calculate(new[] { "home", "away" }, new[] { new LeagueResult("home", "away", 2, 0) },
                new PointsRules(3, 1, 0), "league", "season");
            new SqliteMatchdayStore(save.Connection).ExecuteTransaction(tx => { tx.ReplaceStandings("league", "season", table); return 0; });
            using var q = save.Connection.CreateCommand(); q.CommandText = "SELECT club_id,played,points FROM standings WHERE season_id='season' ORDER BY club_id;";
            using var r = q.ExecuteReader(); Assert.True(r.Read()); Assert.Equal("away", r.GetString(0)); Assert.Equal(1, r.GetInt32(1)); Assert.Equal(0, r.GetInt32(2));
            Assert.True(r.Read()); Assert.Equal("home", r.GetString(0)); Assert.Equal(3, r.GetInt32(2)); Assert.False(r.Read());
        });
    }

    [Fact]
    public void RollsBackEarlierWritesWhenStandingsReplacementFails()
    {
        WithSave(save =>
        {
            Seed(save);
            var store = new SqliteMatchdayStore(save.Connection);
            var invalid = LeagueStandings.Calculate(new[] { "missing-club" }, Array.Empty<LeagueResult>(),
                new PointsRules(3, 1, 0), "league", "season");
            Assert.ThrowsAny<Exception>(() => store.ExecuteTransaction(tx =>
            {
                // DELETE succeeds; inserting a missing club violates the real schema FK.
                tx.ReplaceStandings("league", "season", invalid); return 0;
            }));
            using var verify = save.Connection.CreateCommand(); verify.CommandText = "SELECT count(*) FROM standings WHERE season_id='season' AND club_id='home';";
            Assert.Equal(1L, verify.ExecuteScalar());
        });
    }

    [Fact]
    public void RollsBackAllWorkWhenAnErrorOccursAfterAClosureWrite()
    {
        WithSave(save =>
        {
            Seed(save); var store = new SqliteMatchdayStore(save.Connection);
            Assert.Throws<InvalidOperationException>(() => store.ExecuteTransaction<int>(tx =>
            {
                tx.ReplaceStandings("league", "season", Array.Empty<LeagueStanding>());
                throw new InvalidOperationException("injected mid-close failure");
            }));
            using var q = save.Connection.CreateCommand(); q.CommandText = "SELECT count(*) FROM standings WHERE season_id='season';";
            Assert.Equal(2L, q.ExecuteScalar());
        });
    }

    [Fact]
    public void MatchdayPersistsResultsEventsAppearancesEffectsFixturesAndStandings()
    {
        WithSave(save =>
        {
            SeedMatchday(save.Connection);
            var result = RunMatchday(save.Connection);
            Assert.Equal(2, result.Matches.Count);
            Assert.Equal(4, result.Standings.Count);
            Assert.Equal(2L, Scalar(save.Connection, "SELECT count(*) FROM matches;"));
            Assert.Equal("1.0.0", Scalar(save.Connection, "SELECT min(rules_version) FROM matches;"));
            Assert.Equal(4L, Scalar(save.Connection, "SELECT count(*) FROM match_team_stats;"));
            Assert.True(Convert.ToInt64(Scalar(save.Connection, "SELECT count(*) FROM match_appearances;")) >= 22);
            Assert.Equal(44L, Scalar(save.Connection, "SELECT count(*) FROM player_match_stats;"));
            Assert.Equal(2L, Scalar(save.Connection, "SELECT count(*) FROM fixtures WHERE status='played';"));
            Assert.Equal(4L, Scalar(save.Connection, "SELECT count(*) FROM standings WHERE played=1;"));
            Assert.True(Convert.ToInt64(Scalar(save.Connection, "SELECT count(*) FROM match_events;")) > 0);
            Assert.Equal(result.Matches.Sum(match => (long)(match.Report ?? match.Headless!.Report).Events.Count), Convert.ToInt64(Scalar(save.Connection, "SELECT count(*) FROM match_event_records;")));
            var reports=result.Matches.Select(match=>match.Report ?? match.Headless!.Report).ToArray();
            Assert.Equal(reports.Sum(report=>(long)report.Injuries.Count),Convert.ToInt64(Scalar(save.Connection,"SELECT count(*) FROM player_match_injuries;")));
            Assert.Equal(reports.SelectMany(report=>report.Injuries).Select(injury=>injury.PlayerId).Distinct(StringComparer.Ordinal).Count(),Convert.ToInt32(Scalar(save.Connection,"SELECT count(*) FROM player_availability;")));
            Assert.Equal(0L, Scalar(save.Connection,"SELECT count(*) FROM player_availability a JOIN player_match_injuries i ON i.match_id=a.injury_match_id AND i.event_no=a.injury_event_no WHERE a.days_remaining<>i.recovery_days;"));
            Assert.Equal(reports.SelectMany(report=>report.Cards).Select(card=>card.PlayerId).Distinct(StringComparer.Ordinal).Count(),Convert.ToInt32(Scalar(save.Connection,"SELECT count(*) FROM player_discipline;")));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM player_discipline WHERE discipline_id='' OR source_competition_id='' OR rules_package_id='';"));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM player_discipline WHERE rules_package_id<>'slice-league' OR rule_version<>'1.0.0';"));
            Assert.True(Convert.ToInt32(Scalar(save.Connection,"SELECT min(fit) FROM players;"))<100);
        });
    }

    [Fact]
    public void MatchdayAppliesConfiguredFitnessAndResultAndRatingMoraleOnlyToAppearances()
    {
        WithSave(save =>
        {
            SeedMatchday(save.Connection);
            var result = RunMatchday(save.Connection);
            foreach (var match in result.Matches)
            {
                var report = match.Report ?? match.Headless!.Report;
                foreach (var (club, team, ownGoals, opponentGoals) in new[]
                {
                    (match.Fixture.HomeClubId, report.Home, report.Score.HomeGoals, report.Score.AwayGoals),
                    (match.Fixture.AwayClubId, report.Away, report.Score.AwayGoals, report.Score.HomeGoals)
                })
                {
                foreach (var appearance in team.Ratings)
                {
                    using var q = save.Connection.CreateCommand();
                    q.CommandText = @"SELECT p.fit,p.morale,p.ovr,a.value,s.fitness,s.morale FROM players p
JOIN fixture_match_player_attributes a ON a.fixture_id=$fixture AND a.club_id=p.club_id AND a.player_id=p.player_id AND a.attribute='Stamina'
JOIN fixture_match_players s ON s.fixture_id=a.fixture_id AND s.club_id=a.club_id AND s.player_id=a.player_id
WHERE p.player_id=$player AND p.club_id=$club;";
                    q.Parameters.AddWithValue("$fixture", match.Fixture.MatchId);
                    q.Parameters.AddWithValue("$player", appearance.PlayerId); q.Parameters.AddWithValue("$club", club);
                    using var reader = q.ExecuteReader(); Assert.True(reader.Read());
                    var fit = reader.GetInt32(0); var morale = reader.GetInt32(1); var ovr = reader.GetInt32(2); var stamina = reader.GetInt32(3);
                    var originalFit = reader.GetInt32(4); var originalMorale = reader.GetInt32(5);
                    var expectedFit = Math.Clamp((int)Math.Round(originalFit - appearance.MinutesPlayed / 90.0 * (28 - stamina / 6.0) * report.FitnessIntensity, MidpointRounding.AwayFromZero), 0, 100);
                    var delta = ownGoals > opponentGoals ? 4 : ownGoals < opponentGoals ? -4 : 0;
                    if (appearance.Rating > 7.5) delta += 3;
                    Assert.Equal(expectedFit, fit);
                    Assert.Equal(Math.Clamp(originalMorale + delta, 0, 100), morale);
                    Assert.Equal(60, ovr);
                    Assert.InRange(fit, 0, 100); Assert.InRange(morale, 0, 100);
                }
                Assert.DoesNotContain(team.Ratings, rating => rating.PlayerId.EndsWith("bench", StringComparison.Ordinal));
                }
            }
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM match_appearances a JOIN fixture_match_players p ON p.fixture_id=a.match_id AND p.player_id=a.player_id WHERE p.is_starter=0;"));
            Assert.Equal(4L, Scalar(save.Connection, "SELECT count(*) FROM fixture_match_players p JOIN players x ON x.player_id=p.player_id WHERE p.is_starter=0 AND x.fit=100 AND x.morale=60 AND NOT EXISTS(SELECT 1 FROM match_appearances a WHERE a.match_id=p.fixture_id AND a.player_id=p.player_id);"));
        });
    }

    [Fact]
    public void RollsBackWholeMatchdayWhenSecondMatchWriteFails()
    {
        WithSave(save =>
        {
            SeedMatchday(save.Connection);
            using (var trigger = save.Connection.CreateCommand())
            { trigger.CommandText = "CREATE TRIGGER fail_second_match BEFORE INSERT ON matches WHEN NEW.fixture_id='fixture-b' BEGIN SELECT RAISE(ABORT,'injected second-match failure'); END;"; trigger.ExecuteNonQuery(); }
            Assert.ThrowsAny<Exception>(() => RunMatchday(save.Connection));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM matches;"));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM match_appearances;"));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM match_events;"));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM fixtures WHERE status='played';"));
            Assert.Equal(4L, Scalar(save.Connection, "SELECT count(*) FROM standings WHERE played=0;"));
            Assert.Equal(100L, Scalar(save.Connection, "SELECT min(fit) FROM players;"));
        });
    }

    [Fact]
    public void RollsBackPersistedMatchesWhenStandingsRebuildFails()
    {
        WithSave(save =>
        {
            SeedMatchday(save.Connection);
            using (var trigger = save.Connection.CreateCommand())
            { trigger.CommandText = "CREATE TRIGGER fail_standings BEFORE INSERT ON standings WHEN NEW.season_id='season' BEGIN SELECT RAISE(ABORT,'injected standings failure'); END;"; trigger.ExecuteNonQuery(); }
            Assert.ThrowsAny<Exception>(() => RunMatchday(save.Connection));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM matches;"));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM match_appearances;"));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM match_events;"));
            Assert.Equal(0L, Scalar(save.Connection, "SELECT count(*) FROM fixtures WHERE status='played';"));
            Assert.Equal(4L, Scalar(save.Connection, "SELECT count(*) FROM standings WHERE played=0;"));
            Assert.Equal(100L, Scalar(save.Connection, "SELECT min(fit) FROM players;"));
        });
    }

    private static object? Scalar(Microsoft.Data.Sqlite.SqliteConnection connection, string sql)
    { using var q=connection.CreateCommand(); q.CommandText=sql; return q.ExecuteScalar(); }

    private static void SeedMatchday(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        using (var q=connection.CreateCommand())
        {
            q.CommandText=@"INSERT INTO clubs VALUES('home','Home','Lumenia'),('away','Away','Lumenia'),('home2','Home 2','Lumenia'),('away2','Away 2','Lumenia');
INSERT INTO fixtures(fixture_id,season_id,round_no,home_club_id,away_club_id,scheduled_day) VALUES('fixture-a','season',1,'home','away',1),('fixture-b','season',1,'home2','away2',1);
INSERT INTO standings(season_id,club_id,played,won,drawn,lost,goals_for,goals_against,points) VALUES('season','home',0,0,0,0,0,0,0),('season','away',0,0,0,0,0,0,0),('season','home2',0,0,0,0,0,0,0),('season','away2',0,0,0,0,0,0,0);";
            q.ExecuteNonQuery();
        }
        var formations=FormationCatalog.Load(Path.Combine(AppContext.BaseDirectory,"config","formations.json"));
        var points=formations["4-3-3"].Points;
        var attributes=Enum.GetValues(typeof(PlayerAttribute)).Cast<PlayerAttribute>().ToDictionary(a=>a,_=>60);
        var snapshots=new SqliteMatchInputStore(connection);
        foreach(var (fixture,home,away,prefix) in new[]{("fixture-a","home","away","a"),("fixture-b","home2","away2","b")})
        {
            MatchTeamInput Team(string club,string side)
            {
                var players=new MatchPlayerInput[11];
                for(var i=0;i<11;i++)
                {
                    var id=prefix+side+i; players[i]=new MatchPlayerInput(id,points[i].Position,attributes,i==0?0:85,100);
                    using var insert=connection.CreateCommand(); insert.CommandText="INSERT INTO players(player_id,club_id,full_name,age,nationality,position,ovr,fit,morale) VALUES($id,$club,$name,24,'Lumenia',$position,60,100,60);";
                    insert.Parameters.AddWithValue("$id",id);insert.Parameters.AddWithValue("$club",club);insert.Parameters.AddWithValue("$name",id);insert.Parameters.AddWithValue("$position",DbPosition(points[i].Position));insert.ExecuteNonQuery();
                }
                var substituteId=prefix+side+"bench";
                using(var insert=connection.CreateCommand()) { insert.CommandText="INSERT INTO players(player_id,club_id,full_name,age,nationality,position,ovr,fit,morale) VALUES($id,$club,$name,24,'Lumenia','ST',60,100,60);"; insert.Parameters.AddWithValue("$id",substituteId); insert.Parameters.AddWithValue("$club",club); insert.Parameters.AddWithValue("$name",substituteId); insert.ExecuteNonQuery(); }
                var substitute=new MatchPlayerInput(substituteId,Position.Striker,attributes,85,60);
                return new MatchTeamInput(club,players,new[]{substitute},new MatchTactics("4-3-3",TacticalMentality.Balanced,PassingStyle.Mixed,PressingStyle.Medium,DefensiveLine.Normal,TeamWidth.Normal));
            }
            var seed=SeedDeriver.MatchSeed(123,"league",1,1,fixture);
            var input=new MatchInput(Team(home,"h"),Team(away,"v"),seed,EngineVersion.V10_10);
            snapshots.Save(fixture,"league",File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","competitions","slice-league.v1.json")),input);
        }
    }

    private static string DbPosition(Position p) => p switch
    { Position.Goalkeeper=>"GK",Position.CentreBack=>"CB",Position.LeftBack=>"LB",Position.RightBack=>"RB",Position.DefensiveMidfielder=>"DM",Position.CentralMidfielder=>"CM",Position.AttackingMidfielder=>"AM",Position.LeftWinger=>"LW",Position.RightWinger=>"RW",Position.Striker=>"ST",_=>throw new ArgumentOutOfRangeException(nameof(p)) };

    private static MatchdayResult RunMatchday(Microsoft.Data.Sqlite.SqliteConnection connection)
    {
        var simulator=new MatchdaySimulator(ParamsConfig.Load(Path.Combine(AppContext.BaseDirectory,"config","params.json")),
            FormationCatalog.Load(Path.Combine(AppContext.BaseDirectory,"config","formations.json")));
        return new MatchdayProcessor(new SqliteMatchdayStore(connection),simulator).Process(123,"league",1,1,"season","home",new PointsRules(3,1,0));
    }

    private static void Seed(SqliteSaveDatabase save)
    {
        using var q = save.Connection.CreateCommand();
        q.CommandText = @"INSERT INTO clubs VALUES('home','Home','Lumenia'),('away','Away','Lumenia');
INSERT INTO standings(season_id,club_id,played,won,drawn,lost,goals_for,goals_against,points) VALUES('season','home',0,0,0,0,0,0,0),('season','away',0,0,0,0,0,0,0);";
        q.ExecuteNonQuery();
    }

    private static void WithSave(Action<SqliteSaveDatabase> test)
    {
        var path = Path.Combine(Path.GetTempPath(), "bt-matchday-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try { using var save = SqliteSaveDatabase.Open(path); test(save); }
        finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + "-wal")) File.Delete(path + "-wal"); if (File.Exists(path + "-shm")) File.Delete(path + "-shm"); }
    }
}
