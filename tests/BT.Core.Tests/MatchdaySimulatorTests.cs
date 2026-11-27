using System;
using System.IO;
using System.Linq;
using BT.Core;
using BT.Core.Domain;
using BT.Core.Sim;
using Xunit;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Tests;

public sealed class MatchdaySimulatorTests
{
    private static readonly string Config = Path.Combine(AppContext.BaseDirectory, "config");

    [Fact]
    public void SameInputSeedAndEngineProduceIdenticalAuthoritativeReport()
    {
        var simulator = CreateSimulator();
        var input = CreateInput(1729, EngineVersion.V10_10);

        var first = simulator.SimulateFull(input);
        var second = simulator.SimulateFull(input);

        AssertEquivalent(first, second);
    }

    [Fact]
    public void FullAndHeadlessUseTheSameCoreAndRandomFlow()
    {
        var simulator = CreateSimulator();
        var input = CreateInput(987654321, EngineVersion.V10_10);

        var full = simulator.SimulateFull(input);
        var headless = simulator.SimulateHeadless(input);

        AssertEquivalent(full, headless.Report);
        Assert.Equal(full.Score.HomeGoals, headless.Score.HomeGoals);
        Assert.Equal(full.Score.AwayGoals, headless.Score.AwayGoals);
        Assert.Equal(full.Goalscorers.Count(g => g.AssisterId is not null), headless.Assists.Count);
        Assert.Equal(full.Cards.Select(EventKey), headless.Cards.Select(EventKey));
        Assert.Equal(full.Injuries.Select(EventKey), headless.Injuries.Select(EventKey));
    }

    [Fact]
    public void SimulatorProducesIndividualAppearancesAndMeasuredStatisticsOnly()
    {
        var report = CreateSimulator().SimulateFull(CreateInput(1729, EngineVersion.V10_10));
        var home = report.Home.Ratings;
        Assert.Equal(11, home.Count);
        Assert.All(home, player =>
        {
            Assert.True(player.Started);
            Assert.Equal(report.DurationMinutes, player.MinutesPlayed);
            Assert.InRange(player.Rating, 3.0, 10.0);
        });
        Assert.Equal(report.Home.Shots, home.Sum(player => player.Shots));
        Assert.Equal(report.Home.ShotsOnTarget, home.Sum(player => player.ShotsOnTarget));
        Assert.Equal(report.Goalscorers.Count(goal => goal.TeamId == "home" && !goal.IsOwnGoal), home.Sum(player => player.Goals));
        Assert.Equal(report.Goalscorers.Count(goal => goal.TeamId == "home" && goal.AssisterId is not null), home.Sum(player => player.Assists));
        Assert.Equal(report.Home.PassesAttempted, home.Sum(player => player.PassesAttempted));
        Assert.Equal(report.Home.PassesCompleted, home.Sum(player => player.PassesCompleted));
        Assert.Equal(report.Home.DefensiveActionsWon, home.Sum(player => player.DefensiveActionsWon));
        Assert.Equal(report.Home.TacklesWon, home.Sum(player => player.TacklesWon));
        Assert.Equal(report.Home.InterceptionsWon, home.Sum(player => player.InterceptionsWon));
        Assert.Equal(report.Events.Count(e => e.TeamId == "home" && e.Type == MatchEventType.Save), home.Sum(player => player.Saves));
        Assert.Equal(report.Events.Count(e => e.TeamId == "home" && e.Type == MatchEventType.ClearChance), report.Home.ClearChances);
        Assert.Equal(report.Events.Count(e => e.TeamId == "home" && e.Type == MatchEventType.Foul), report.Home.Fouls);
        Assert.Equal(report.Events.Count(e => e.TeamId == "home" && e.Type == MatchEventType.Corner), report.Home.Corners);
        Assert.Equal(report.Home.TacklesWon, report.Events.Count(e => e.TeamId == "home" && e.Type == MatchEventType.TackleWon));
        Assert.Equal(report.Home.InterceptionsWon, report.Events.Count(e => e.TeamId == "home" && e.Type == MatchEventType.InterceptionWon));
        Assert.True(report.Home.PassesAttempted > 0);
        Assert.True(report.Home.DefensiveActionsWon > 0);
        Assert.True(report.Home.ClearChances >= 0);
        Assert.InRange(report.Home.PassesCompleted, 0, report.Home.PassesAttempted);
    }

    [Fact]
    public void BenchPlayersWithoutEffectiveEntryHaveNoAppearance()
    {
        var team = Team("home", "home", PassingStyle.Short);
        var bench = new MatchPlayerInput("home-bench", Position.CentralMidfielder,
            team.Starters[1].EffectiveAttributes, 80, 60);
        var changedTeam = new MatchTeamInput(team.TeamId, team.Starters, new[] { bench }, team.Tactics);
        var away = Team("away", "away", PassingStyle.Direct);
        var result = CreateSimulator().SimulateFull(new MatchInput(changedTeam, away, 987654321, EngineVersion.V10_10));
        Assert.DoesNotContain(result.Home.Ratings, rating => rating.PlayerId == bench.PlayerId);
    }

    [Fact]
    public void EffectiveSubstitutionAndDismissalSetAppearanceMinutesAndRating()
    {
        var team = Team("home", "home", PassingStyle.Short);
        var outgoing = team.Starters[1];
        var dismissed = team.Starters[2];
        var bench = new MatchPlayerInput("home-bench", outgoing.Position, outgoing.EffectiveAttributes, 80, 60);
        var selected = new MatchTeamInput(team.TeamId, team.Starters, new[] { bench }, team.Tactics);
        var away = Team("away", "away", PassingStyle.Direct);
        var input = new MatchInput(selected, away, 1, EngineVersion.V10_10);
        var tracker = new MatchAppearanceTracker(input);
        var events = new System.Collections.Generic.List<MatchEvent>();
        tracker.Advance(team, away, events, 60);
        var substitution = new MatchEvent(10, MatchEventType.Substitution, team.TeamId,
            outgoing.PlayerId, bench.PlayerId, ruleIndex: 0);
        events.Add(substitution);
        tracker.RegisterSubstitutions(new[] { substitution });
        var active = team.Starters.ToArray();
        active[1] = bench;
        var changedTeam = new MatchTeamInput(team.TeamId, active, new[] { outgoing }, team.Tactics);
        var dismissal = new MatchEvent(15, MatchEventType.RedCard, team.TeamId, dismissed.PlayerId);
        events.Add(dismissal);
        tracker.Advance(changedTeam, away, events, 20);

        var appearances = CreateSimulator().RateTeam(selected, Array.Empty<MatchScorer>(), events,
            new MatchScore(0, 0), true, tracker).ToDictionary(player => player.PlayerId);

        Assert.Equal(12, appearances.Count);
        Assert.Equal(60, appearances[outgoing.PlayerId].MinutesPlayed);
        Assert.True(appearances[outgoing.PlayerId].Started);
        Assert.Equal(20, appearances[bench.PlayerId].MinutesPlayed);
        Assert.False(appearances[bench.PlayerId].Started);
        Assert.Equal(60, appearances[dismissed.PlayerId].MinutesPlayed);
        Assert.Equal(1, appearances[dismissed.PlayerId].RedCards);
        Assert.True(appearances[dismissed.PlayerId].Rating < 6.5);
    }

    [Fact]
    public void ExtraTimeDurationIsUsedForStarterMinutes()
    {
        var team = Team("home", "home", PassingStyle.Short);
        var away = Team("away", "away", PassingStyle.Direct);
        var tracker = new MatchAppearanceTracker(new MatchInput(team, away, 1, EngineVersion.V10_10));
        tracker.Advance(team, away, Array.Empty<MatchEvent>(), 90);
        for (var interval = 0; interval < 6; interval++) tracker.Advance(team, away, Array.Empty<MatchEvent>(), 5);
        var ratings = CreateSimulator().RateTeam(team, Array.Empty<MatchScorer>(), Array.Empty<MatchEvent>(),
            new MatchScore(0, 0), true, tracker);
        Assert.Equal(120, tracker.DurationMinutes);
        Assert.All(ratings, player => Assert.Equal(120, player.MinutesPlayed));
    }

    [Fact]
    public void ConfiguredSubstitutionIsAppliedToTheMatchAndCreatesIncomingAppearance()
    {
        var team = Team("home", "home", PassingStyle.Short);
        var outgoing = team.Starters[1];
        var bench = new MatchPlayerInput("home-bench", outgoing.Position, outgoing.EffectiveAttributes, 80, 60);
        team = new MatchTeamInput(team.TeamId, team.Starters, new[] { bench }, team.Tactics);
        var away = Team("away", "away", PassingStyle.Direct);
        var rule = new AutomaticSubstitutionRule(60, outgoing.Position, incomingPlayerId: bench.PlayerId);
        var input = new MatchInput(team, away, 51, EngineVersion.V10_10, homeSubstitutionRules: new[] { rule });

        var report = CreateSimulator().SimulateFull(input);

        Assert.Contains(report.Events, e => e.Type == MatchEventType.Substitution && e.PlayerId == outgoing.PlayerId && e.RelatedPlayerId == bench.PlayerId);
        Assert.Contains(report.Events, e => e.Type == MatchEventType.PassAttempt && e.PlayerId == bench.PlayerId);
        var appearances = report.Home.Ratings.ToDictionary(player => player.PlayerId);
        var substitutionMinute = report.Events.Single(e => e.Type == MatchEventType.Substitution && e.PlayerId == outgoing.PlayerId).Minute;
        Assert.InRange(substitutionMinute, 60, 65);
        Assert.Equal(substitutionMinute, appearances[outgoing.PlayerId].MinutesPlayed);
        Assert.True(appearances[outgoing.PlayerId].Started);
        Assert.Equal(report.DurationMinutes - substitutionMinute, appearances[bench.PlayerId].MinutesPlayed);
        Assert.False(appearances[bench.PlayerId].Started);
    }

    private static MatchdaySimulator CreateSimulator() => new(
        ParamsConfig.Load(Path.Combine(Config, "params.json")),
        FormationCatalog.Load(Path.Combine(Config, "formations.json")));

    private static MatchInput CreateInput(ulong seed, EngineVersion version) => new(
        Team("home", "home", PassingStyle.Short), Team("away", "away", PassingStyle.Direct),
        seed, version);

    private static MatchTeamInput Team(string teamId, string playerPrefix, PassingStyle passing)
    {
        var attributes = Enum.GetValues(typeof(PlayerAttribute)).Cast<PlayerAttribute>()
            .ToDictionary(attribute => attribute, _ => 60);
        var formation = teamId == "home" ? "4-4-2" : "4-3-3";
        var points = FormationCatalog.Load(Path.Combine(Config, "formations.json"))[formation].Points;
        var starters = points.Select((point, index) => new MatchPlayerInput(
            playerPrefix + "-" + index, point.Position, attributes, 80, 60)).ToArray();
        return new MatchTeamInput(teamId, starters, Array.Empty<MatchPlayerInput>(),
            new MatchTactics(formation, TacticalMentality.Balanced, passing,
                PressingStyle.Medium, DefensiveLine.Normal, TeamWidth.Normal));
    }

    private static void AssertEquivalent(MatchReport expected, MatchReport actual)
    {
        Assert.Equal(expected.Score.HomeGoals, actual.Score.HomeGoals);
        Assert.Equal(expected.Score.AwayGoals, actual.Score.AwayGoals);
        Assert.Equal(expected.RegulationScore.HomeGoals, actual.RegulationScore.HomeGoals);
        Assert.Equal(expected.RegulationScore.AwayGoals, actual.RegulationScore.AwayGoals);
        Assert.Equal(expected.DurationMinutes, actual.DurationMinutes);
        Assert.Equal(expected.Seed, actual.Seed);
        Assert.Equal(expected.EngineVersion, actual.EngineVersion);
        Assert.Equal(expected.Shootout?.HomeGoals, actual.Shootout?.HomeGoals);
        Assert.Equal(expected.Shootout?.AwayGoals, actual.Shootout?.AwayGoals);
        Assert.Equal(expected.Goalscorers.Select(GoalKey), actual.Goalscorers.Select(GoalKey));
        Assert.Equal(expected.Events.Select(EventKey), actual.Events.Select(EventKey));
        Assert.Equal(expected.Home.Possession, actual.Home.Possession);
        Assert.Equal(expected.Home.Shots, actual.Home.Shots);
        Assert.Equal(expected.Home.ShotsOnTarget, actual.Home.ShotsOnTarget);
        Assert.Equal(expected.Home.ExpectedGoals, actual.Home.ExpectedGoals);
        Assert.Equal(expected.Away.Possession, actual.Away.Possession);
        Assert.Equal(expected.Away.Shots, actual.Away.Shots);
        Assert.Equal(expected.Away.ShotsOnTarget, actual.Away.ShotsOnTarget);
        Assert.Equal(expected.Away.ExpectedGoals, actual.Away.ExpectedGoals);
        Assert.Equal(expected.Home.Ratings.Select(r => (r.PlayerId, r.Rating, r.MinutesPlayed, r.Started)), actual.Home.Ratings.Select(r => (r.PlayerId, r.Rating, r.MinutesPlayed, r.Started)));
        Assert.Equal(expected.Away.Ratings.Select(r => (r.PlayerId, r.Rating, r.MinutesPlayed, r.Started)), actual.Away.Ratings.Select(r => (r.PlayerId, r.Rating, r.MinutesPlayed, r.Started)));
    }

    private static string GoalKey(MatchScorer goal) =>
        $"{goal.TeamId}|{goal.PlayerId}|{goal.Minute}|{goal.IsOwnGoal}|{goal.AssisterId}";

    private static string EventKey(MatchEvent matchEvent) =>
        $"{matchEvent.TeamId}|{matchEvent.Type}|{matchEvent.Minute}|{matchEvent.PlayerId}|{matchEvent.RelatedPlayerId}|{matchEvent.InjuryDays}";
}
