using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using BT.Core.Competition;
using BT.Core.Sim;
using Microsoft.Data.Sqlite;

namespace BT.Data;

/// <summary>SQLite implementation of the atomic matchday closure boundary.</summary>
public sealed class SqliteMatchdayStore : IMatchdayStore
{
    private readonly SqliteConnection _connection;
    public SqliteMatchdayStore(SqliteConnection connection) => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public T ExecuteTransaction<T>(Func<IMatchdayTransaction, T> operation)
    {
        if (operation == null) throw new ArgumentNullException(nameof(operation));
        // deferred:false maps to BEGIN IMMEDIATE: acquire the writer reservation before fixture reads.
        using var transaction = _connection.BeginTransaction(deferred: false);
        try { var result = operation(new Transaction(_connection, transaction)); transaction.Commit(); return result; }
        catch { transaction.Rollback(); throw; }
    }

    private sealed class Transaction : IMatchdayTransaction
    {
        private readonly SqliteConnection c; private readonly SqliteTransaction t;
        public Transaction(SqliteConnection connection, SqliteTransaction transaction) { c=connection; t=transaction; }
        private SqliteCommand Cmd(string sql) { var x=c.CreateCommand(); x.Transaction=t; x.CommandText=sql; return x; }
        private static void P(SqliteCommand x, string n, object? v) => x.Parameters.AddWithValue(n, v ?? DBNull.Value);
        public IReadOnlyList<MatchdayFixture> GetFixtures(string competitionId,string seasonId,int round)
        {
            using var q=Cmd("SELECT f.fixture_id,f.home_club_id,f.away_club_id,f.status,coalesce(ctx.is_knockout,0) FROM fixtures f LEFT JOIN fixture_match_context ctx ON ctx.fixture_id=f.fixture_id WHERE f.season_id=$season AND f.round_no=$round AND f.status IN ('scheduled','played') AND (ctx.competition_id=$competition OR ctx.fixture_id IS NULL) ORDER BY f.fixture_id;");
            P(q,"$competition",competitionId); P(q,"$season",seasonId); P(q,"$round",round); using var r=q.ExecuteReader(); var rows=new List<MatchdayFixture>();
            while(r.Read()) rows.Add(new MatchdayFixture(r.GetString(0),r.GetString(1),r.GetString(2), r.GetInt32(4)==1 ? MatchCompetition.Cup : MatchCompetition.League,r.GetString(3)=="played"));
            return rows;
        }
        public bool IsMatchProcessed(string matchId) { using var q=Cmd("SELECT 1 FROM matches WHERE fixture_id=$id;"); P(q,"$id",matchId); return q.ExecuteScalar()!=null; }
        public MatchInput CreateMatchInput(MatchdayFixture fixture,ulong seed,bool isUserMatch)
        {
            var prepared=new SqliteMatchInputStore(c,t).LoadPrepared(fixture.MatchId);
            if(prepared.CompetitionId != ReadCompetitionId(fixture.MatchId)) throw new InvalidOperationException("Prepared input belongs to a different competition.");
            if(prepared.Input.Seed!=seed) throw new InvalidOperationException("Persisted match seed differs from deterministic fixture seed.");
            return prepared.Input;
        }
        private string ReadCompetitionId(string fixtureId)
        { using var q=Cmd("SELECT competition_id FROM fixture_match_context WHERE fixture_id=$id;"); P(q,"$id",fixtureId); return Convert.ToString(q.ExecuteScalar(),CultureInfo.InvariantCulture) ?? throw new InvalidOperationException("Fixture has no prepared input context."); }
        public void ApplyMatch(ProcessedMatch m)
        {
            var report=m.Report ?? m.Headless!.Report; var f=m.Fixture;
            string rules; string comp;
            using(var q=Cmd("SELECT competition_id,competition_rules_json FROM fixture_match_context WHERE fixture_id=$id;"))
            { P(q,"$id",f.MatchId); using var r=q.ExecuteReader(); if(!r.Read()) throw new InvalidOperationException("Missing fixture match context."); comp=r.GetString(0); rules=r.GetString(1); }
            using var rulesDocument=JsonDocument.Parse(rules); var rulesVersion=rulesDocument.RootElement.GetProperty("version").GetString();
            if(string.IsNullOrWhiteSpace(rulesVersion)) throw new InvalidOperationException("Competition rules snapshot has no version.");
            using(var q=Cmd("INSERT INTO matches(match_id,fixture_id,home_club_id,away_club_id,home_score,away_score,seed,engine_version,rules_version,home_xg,away_xg,duration_minutes) VALUES($id,$fixture,$home,$away,$hs,$as,$seed,$engine,$rules,$hxg,$axg,$duration);"))
            { P(q,"$id",f.MatchId); P(q,"$fixture",f.MatchId); P(q,"$home",f.HomeClubId); P(q,"$away",f.AwayClubId); P(q,"$hs",m.Score.HomeGoals); P(q,"$as",m.Score.AwayGoals); P(q,"$seed",m.Seed.ToString(CultureInfo.InvariantCulture)); P(q,"$engine",report.EngineVersion.ToString()); P(q,"$rules",rulesVersion); P(q,"$hxg",report.Home.ExpectedGoals); P(q,"$axg",report.Away.ExpectedGoals); P(q,"$duration",Math.Min(120,report.DurationMinutes)); q.ExecuteNonQuery(); }
            for(int i=0;i<report.Events.Count;i++) SaveEvent(m,report.Events[i],i+1);
            SaveTeamStats(m,report,f.MatchId);
            SavePlayers(m,report,f.MatchId);
            SaveInjuries(m,report,f.MatchId);
            SaveForm(m,report);
            SaveDiscipline(m,report,comp,f.MatchId);
        }
        private void SaveEvent(ProcessedMatch m,MatchEvent e,int n)
        {
            var id=m.Fixture.MatchId;
            using(var full=Cmd("INSERT INTO match_event_records(match_id,event_no,minute,event_type,club_id,player_id,related_player_id,injury_days,rule_index,failure_reason) VALUES($m,$n,$minute,$type,$club,$p,$related,$days,$rule,$failure);"))
            { P(full,"$m",id);P(full,"$n",n);P(full,"$minute",e.Minute);P(full,"$type",e.Type.ToString());P(full,"$club",e.TeamId);P(full,"$p",e.PlayerId);P(full,"$related",e.RelatedPlayerId);P(full,"$days",e.InjuryDays);P(full,"$rule",e.RuleIndex);P(full,"$failure",e.FailureReason?.ToString());full.ExecuteNonQuery(); }
            var compact=e.Type switch { MatchEventType.Goal or MatchEventType.OwnGoal=>"goal", MatchEventType.YellowCard=>"yellow_card", MatchEventType.RedCard=>"red_card", MatchEventType.Injury=>"injury", MatchEventType.Substitution=>"substitution", MatchEventType.Shot=>"shot", MatchEventType.ShotOnTarget=>"shot_on_target", _=>null };
            if(compact is null) return;
            using var q=Cmd("INSERT INTO match_events(match_id,event_no,minute,event_type,club_id,player_id,related_player_id,xg,detail_code) VALUES($m,$n,$minute,$type,$club,$p,$related,NULL,$detail);");
            P(q,"$m",id); P(q,"$n",n); P(q,"$minute",Math.Min(90,e.Minute)); P(q,"$type",compact); P(q,"$club",e.TeamId); P(q,"$p",e.PlayerId); P(q,"$related",e.RelatedPlayerId); P(q,"$detail",e.Type.ToString()); q.ExecuteNonQuery();
        }
        private void SaveTeamStats(ProcessedMatch m,MatchReport r,string id)
        { SaveTeamStat(id,m.Fixture.HomeClubId,r.Home); SaveTeamStat(id,m.Fixture.AwayClubId,r.Away); }
        private void SaveTeamStat(string id,string club,MatchTeamReport x)
        {
            using var q=Cmd("INSERT INTO match_team_stats VALUES($m,$club,$pos,$shots,$sot,$xg,$d0,$d1,$d2,$m0,$m1,$m2,$a0,$a1,$a2);");
            P(q,"$m",id);P(q,"$club",club);P(q,"$pos",x.Possession);P(q,"$shots",x.Shots);P(q,"$sot",x.ShotsOnTarget);P(q,"$xg",x.ExpectedGoals);
            string[] names={"$d0","$d1","$d2","$m0","$m1","$m2","$a0","$a1","$a2"}; for(int i=0;i<9;i++) P(q,names[i],x.Zones.Cells[i]); q.ExecuteNonQuery();
        }
        private void SavePlayers(ProcessedMatch m,MatchReport r,string id)
        {
            var home=m.Fixture.HomeClubId; var away=m.Fixture.AwayClubId;
            var ratings=r.Home.Ratings.Select(x=>(x,home)).Concat(r.Away.Ratings.Select(x=>(x,away))).ToArray();
            foreach(var (x,club) in ratings)
            {
                using(var q=Cmd("INSERT INTO match_appearances VALUES($m,$p,$club,$minutes,$started,$rating);")) { P(q,"$m",id);P(q,"$p",x.PlayerId);P(q,"$club",club);P(q,"$minutes",Math.Max(1,Math.Min(120,x.MinutesPlayed)));P(q,"$started",x.Started?1:0);P(q,"$rating",x.Rating);q.ExecuteNonQuery(); }
                var goals=r.Goalscorers.Count(g=>!g.IsOwnGoal&&g.PlayerId==x.PlayerId); var assists=r.Goalscorers.Count(g=>g.AssisterId==x.PlayerId);
                using var s=Cmd("INSERT INTO player_match_stats(match_id,player_id,goals,assists,shots,shots_on_target,passes,tackles_interceptions,saves,yellow_cards,red_cards) VALUES($m,$p,$g,$a,$shots,$sot,$passes,$tackles,$saves,$yc,$rc);");
                P(s,"$m",id);P(s,"$p",x.PlayerId);P(s,"$g",goals);P(s,"$a",assists);P(s,"$shots",x.Shots);P(s,"$sot",x.ShotsOnTarget);P(s,"$passes",x.PassesCompleted);P(s,"$tackles",x.DefensiveActionsWon);P(s,"$saves",x.Saves);P(s,"$yc",x.YellowCards);P(s,"$rc",x.RedCards);s.ExecuteNonQuery();
            }
        }
        private void SaveInjuries(ProcessedMatch m,MatchReport r,string id)
        {
            foreach(var (e,n) in r.Injuries.Select((e,i)=>(e,i)))
            {
                var availability=e.InjuryDays<=3?"tocando":"lesionado"; int eventNo=Array.IndexOf(r.Events.ToArray(),e)+1;
                using(var q=Cmd("INSERT INTO player_match_injuries VALUES($m,$n,$p,$club,$minute,$days,$availability);")) { P(q,"$m",id);P(q,"$n",eventNo);P(q,"$p",e.PlayerId);P(q,"$club",e.TeamId);P(q,"$minute",e.Minute);P(q,"$days",e.InjuryDays);P(q,"$availability",availability);q.ExecuteNonQuery(); }
                using var a=Cmd("INSERT INTO player_availability(player_id,availability,days_remaining,injury_match_id,injury_event_no) VALUES($p,$availability,$days,$m,$n) ON CONFLICT(player_id) DO UPDATE SET availability=excluded.availability,days_remaining=excluded.days_remaining,injury_match_id=excluded.injury_match_id,injury_event_no=excluded.injury_event_no;"); P(a,"$p",e.PlayerId);P(a,"$availability",availability);P(a,"$days",e.InjuryDays);P(a,"$m",id);P(a,"$n",eventNo);a.ExecuteNonQuery();
            }
        }
        private void SaveForm(ProcessedMatch m,MatchReport r)
        {
            foreach(var (rating,club) in r.Home.Ratings.Select(x=>(x,m.Fixture.HomeClubId)).Concat(r.Away.Ratings.Select(x=>(x,m.Fixture.AwayClubId))))
            { int fit,morale,resistance; using(var q=Cmd("SELECT p.fitness,p.morale,a.value FROM fixture_match_players p JOIN fixture_match_player_attributes a ON a.fixture_id=p.fixture_id AND a.club_id=p.club_id AND a.player_id=p.player_id WHERE p.fixture_id=$fixture AND p.club_id=$club AND p.player_id=$p AND a.attribute='Stamina';")){P(q,"$fixture",m.Fixture.MatchId);P(q,"$club",club);P(q,"$p",rating.PlayerId);using var reader=q.ExecuteReader();if(!reader.Read())throw new InvalidOperationException("Appearance is missing its persisted player input or stamina attribute.");fit=reader.GetInt32(0);morale=reader.GetInt32(1);resistance=reader.GetInt32(2);} var result=club==m.Fixture.HomeClubId?r.Score.HomeGoals.CompareTo(r.Score.AwayGoals):r.Score.AwayGoals.CompareTo(r.Score.HomeGoals); var moraleDelta=result>0?4:result<0?-4:0; if(rating.Rating>7.5) moraleDelta+=3; var fitDelta=(rating.MinutesPlayed/90.0)*(28-resistance/6.0)*r.FitnessIntensity; int newFit=Math.Clamp((int)Math.Round(fit-fitDelta,MidpointRounding.AwayFromZero),0,100); morale=Math.Clamp(morale+moraleDelta,0,100);
              using var u=Cmd("UPDATE players SET fit=$fit,morale=$morale WHERE player_id=$p AND club_id=$club;");P(u,"$fit",newFit);P(u,"$morale",morale);P(u,"$p",rating.PlayerId);P(u,"$club",club); if(u.ExecuteNonQuery()!=1) throw new InvalidOperationException("Appearance player is not registered to fixture club."); }
        }
        private void SaveDiscipline(ProcessedMatch m,MatchReport r,string comp,string fixture)
        {
            using var context=Cmd("SELECT competition_rules_json FROM fixture_match_context WHERE fixture_id=$id;"); P(context,"$id",fixture); var json=Convert.ToString(context.ExecuteScalar(),CultureInfo.InvariantCulture) ?? throw new InvalidOperationException("Missing competition rules snapshot.");
            using var document=JsonDocument.Parse(json); var root=document.RootElement; var discipline=root.GetProperty("discipline");
            var yellow=discipline.GetProperty("yellowAccumulation"); var threshold=yellow.GetProperty("threshold").GetInt32(); var suspension=yellow.GetProperty("suspensionMatches").GetInt32();
            var red=discipline.GetProperty("directRed"); var redBan=red.GetProperty("minimumMatches").GetInt32()+red.GetProperty("additionalMatches").GetInt32();
            var version=root.GetProperty("version").GetString() ?? throw new InvalidOperationException("Competition rules snapshot has no version.");
            var groupBase=discipline.TryGetProperty("accumulationGroupId",out var configuredGroup)?configuredGroup.GetString():"league";
            if(string.IsNullOrWhiteSpace(groupBase)) throw new InvalidOperationException("Discipline accumulation group id is required.");
            var group=groupBase;
            var transferId=discipline.TryGetProperty("leagueTransferCompetitionId",out var configuredTransfer)?configuredTransfer.GetString():"league";
            if(string.IsNullOrWhiteSpace(transferId)) throw new InvalidOperationException("Discipline transfer competition id is required.");
            var packageId=root.TryGetProperty("id",out var package)?package.GetString():comp;
            if(string.IsNullOrWhiteSpace(packageId)) throw new InvalidOperationException("Competition rule package id is required.");
            var club1=m.Fixture.HomeClubId; var club2=m.Fixture.AwayClubId;
            using(var eligible=Cmd("SELECT d.player_id,d.accumulation_group,d.service_competition_id FROM player_discipline d JOIN players p ON p.player_id=d.player_id WHERE d.pending_matches>0 AND d.service_competition_id=$competition AND p.club_id IN ($home,$away) ORDER BY d.player_id,d.accumulation_group;"))
            { P(eligible,"$competition",comp);P(eligible,"$home",club1);P(eligible,"$away",club2);using var rows=eligible.ExecuteReader();var due=new List<(string Player,string Group,string Service)>();while(rows.Read())due.Add((rows.GetString(0),rows.GetString(1),rows.GetString(2)));rows.Close();foreach(var dueBan in due) MutateDiscipline(dueBan.Player,dueBan.Group,dueBan.Service,fixture,version,0,0,"service",true); }
            foreach(var e in r.Cards)
            {
                var isYellow=e.Type==MatchEventType.YellowCard; int count=0,ban=0; string cause;
                var serviceComp=comp;
                if(isYellow){count=ReadYellow(e.PlayerId!,group,comp)+1; if(count>=threshold){count=0;ban=suspension;} cause="yellowAccumulation";}
                else {ban=redBan;cause="directRed"; if(discipline.GetProperty("redService").GetString()=="issuingCompetitionThenLeagueIfNoFixturesRemain" && !HasFutureFixture(comp,fixture,m.Fixture.HomeClubId,m.Fixture.AwayClubId)) serviceComp=transferId!;}
                MutateDiscipline(e.PlayerId!,group,serviceComp,fixture,version,count,ban,cause,false,comp,packageId!);
            }
        }
        private bool HasFutureFixture(string competition,string currentFixture,string home,string away)
        { using var q=Cmd("SELECT 1 FROM fixtures f JOIN fixture_match_context c ON c.fixture_id=f.fixture_id WHERE c.competition_id=$competition AND f.season_id=(SELECT season_id FROM fixtures WHERE fixture_id=$current) AND f.fixture_id<>$current AND f.status IN ('scheduled','postponed') AND (f.home_club_id IN ($home,$away) OR f.away_club_id IN ($home,$away)) LIMIT 1;");P(q,"$competition",competition);P(q,"$current",currentFixture);P(q,"$home",home);P(q,"$away",away);return q.ExecuteScalar()!=null; }
        private int ReadYellow(string player,string group,string comp)
        { using var q=Cmd("SELECT yellow_count FROM player_discipline WHERE player_id=$p AND accumulation_group=$g AND service_competition_id=$s;"); P(q,"$p",player);P(q,"$g",group);P(q,"$s",comp); var value=q.ExecuteScalar(); return value==null?0:Convert.ToInt32(value,CultureInfo.InvariantCulture); }
        private void MutateDiscipline(string player,string group,string comp,string fixture,string version,int yellow,int addBan,string cause,bool serving,string? sourceComp=null,string? packageId=null)
        {
            if(serving)
            { using var served=Cmd("UPDATE player_discipline SET pending_matches=max(0,pending_matches-1),rule_version=$version WHERE player_id=$p AND accumulation_group=$g AND service_competition_id=$service;");P(served,"$version",version);P(served,"$p",player);P(served,"$g",group);P(served,"$service",comp);served.ExecuteNonQuery();return; }
            var id=player+":"+group+":"+comp;
            using var q=Cmd("INSERT INTO player_discipline(player_id,accumulation_group,yellow_count,service_competition_id,pending_matches,cause,origin_fixture_id,rule_version,discipline_id,source_competition_id,rules_package_id) VALUES($p,$g,$yellow,$service,$ban,$cause,$fixture,$version,$id,$source,$package) ON CONFLICT(player_id,accumulation_group,service_competition_id) DO UPDATE SET yellow_count=excluded.yellow_count,pending_matches=player_discipline.pending_matches+excluded.pending_matches,cause=CASE WHEN excluded.pending_matches>0 THEN excluded.cause ELSE player_discipline.cause END,origin_fixture_id=CASE WHEN excluded.pending_matches>0 THEN excluded.origin_fixture_id ELSE player_discipline.origin_fixture_id END,rule_version=excluded.rule_version,discipline_id=excluded.discipline_id,source_competition_id=CASE WHEN excluded.pending_matches>0 THEN excluded.source_competition_id ELSE player_discipline.source_competition_id END,rules_package_id=excluded.rules_package_id;");
            P(q,"$p",player);P(q,"$g",group);P(q,"$yellow",yellow);P(q,"$service",comp);P(q,"$ban",addBan);P(q,"$cause",cause);P(q,"$fixture",fixture);P(q,"$version",version);P(q,"$id",id);P(q,"$source",sourceComp??comp);P(q,"$package",packageId??comp);q.ExecuteNonQuery();
        }
        public IReadOnlyList<LeagueResult> GetCompletedLeagueResults(string competitionId,string seasonId)
        { using var q=Cmd("SELECT m.home_club_id,m.away_club_id,m.home_score,m.away_score FROM matches m JOIN fixtures f ON f.fixture_id=m.fixture_id JOIN fixture_match_context x ON x.fixture_id=f.fixture_id WHERE f.season_id=$season AND x.competition_id=$competition AND x.is_knockout=0 ORDER BY f.fixture_id;");P(q,"$season",seasonId);P(q,"$competition",competitionId);using var r=q.ExecuteReader();var list=new List<LeagueResult>();while(r.Read())list.Add(new LeagueResult(r.GetString(0),r.GetString(1),r.GetInt32(2),r.GetInt32(3)));return list; }
        public IReadOnlyList<string> GetClubIds(string competitionId,string seasonId)
        { using var x=Cmd("SELECT DISTINCT club_id FROM (SELECT f.home_club_id club_id FROM fixtures f JOIN fixture_match_context c ON c.fixture_id=f.fixture_id WHERE f.season_id=$season AND c.competition_id=$competition AND c.is_knockout=0 UNION SELECT f.away_club_id FROM fixtures f JOIN fixture_match_context c ON c.fixture_id=f.fixture_id WHERE f.season_id=$season AND c.competition_id=$competition AND c.is_knockout=0) ORDER BY club_id;");P(x,"$season",seasonId);P(x,"$competition",competitionId);using var rr=x.ExecuteReader();var list=new List<string>();while(rr.Read())list.Add(rr.GetString(0));return list; }
        public void ReplaceStandings(string competitionId,string seasonId,IReadOnlyList<LeagueStanding> standings)
        { using(var d=Cmd("DELETE FROM standings WHERE season_id=$s;")){P(d,"$s",seasonId);d.ExecuteNonQuery();} foreach(var s in standings){using var q=Cmd("INSERT INTO standings(season_id,club_id,played,won,drawn,lost,goals_for,goals_against,points) VALUES($season,$club,$played,$won,$drawn,$lost,$gf,$ga,$points);");P(q,"$season",seasonId);P(q,"$club",s.ClubId);P(q,"$played",s.Played);P(q,"$won",s.Won);P(q,"$drawn",s.Drawn);P(q,"$lost",s.Lost);P(q,"$gf",s.GoalsFor);P(q,"$ga",s.GoalsAgainst);P(q,"$points",s.Points);q.ExecuteNonQuery();} }
    }
}
