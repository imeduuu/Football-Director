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
