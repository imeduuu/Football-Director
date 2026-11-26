using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core.Domain;
using BT.Core.Sim;
using Xunit;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Tests;

public sealed class MatchModelsTests
{
    [Fact]
    public void MatchInputCopiesLineupsAndEffectiveAttributes()
    {
        var attributes = Enum.GetValues(typeof(PlayerAttribute)).Cast<PlayerAttribute>()
            .ToDictionary(attribute => attribute, _ => 60);
        var firstPlayer = CreatePlayer("home-0", Position.Goalkeeper, attributes);
        var starters = CreateStarters("home", attributes);
        starters[0] = firstPlayer;
        var team = new MatchTeamInput("home", starters, Array.Empty<MatchPlayerInput>(), CreateTactics());
        var input = new MatchInput(team, CreateTeam("away", attributes), 123UL, EngineVersion.V10_10);

        starters[0] = CreatePlayer("changed", Position.Goalkeeper, attributes);
        attributes[PlayerAttribute.Passing] = 1;

        Assert.Equal("home-0", input.HomeTeam.Starters[0].PlayerId);
        Assert.Equal(60, input.HomeTeam.Starters[0].EffectiveAttributes[PlayerAttribute.Passing]);
        Assert.Equal(123UL, input.Seed);
        Assert.Equal(EngineVersion.V10_10, input.EngineVersion);
        Assert.False(input.IsNeutralVenue);
    }

    [Fact]
    public void MatchTeamRequiresExactlyElevenStartersAndOneGoalkeeper()
    {
        var attributes = CreateAttributes();
        var invalidStarters = CreateStarters("home", attributes);
        invalidStarters[0] = CreatePlayer("home-0", Position.CentreBack, attributes);

        Assert.Throws<ArgumentException>(() =>
            new MatchTeamInput("home", invalidStarters, Array.Empty<MatchPlayerInput>(), CreateTactics()));
    }

    [Fact]
    public void MatchReportCopiesCollectionsAndExposesChronologicalCardsAndInjuries()
    {
        var events = new List<MatchEvent>
        {
            new(12, MatchEventType.YellowCard, "home", "home-1"),
            new(54, MatchEventType.Injury, "away", "away-2"),
            new(90, MatchEventType.Goal, "home", "home-3")
        };
        var scorers = new List<MatchScorer> { new("home", "home-3", 90) };
        var report = new MatchReport(
            new MatchScore(1, 0), scorers, events, CreateTeamReport(), CreateTeamReport());
        events.Clear();
        scorers.Clear();

        Assert.Equal(1, report.Score.HomeGoals);
        Assert.Single(report.Goalscorers);
        Assert.Equal(new[] { MatchEventType.YellowCard }, report.Cards.Select(matchEvent => matchEvent.Type));
        Assert.Equal(new[] { 54 }, report.Injuries.Select(matchEvent => matchEvent.Minute));
        Assert.Equal(9, report.Home.Zones.Cells.Count);
    }

    [Fact]
    public void MatchReportRejectsEventsOutsideChronologicalOrder()
    {
        var events = new[]
        {
            new MatchEvent(45, MatchEventType.Other, "home"),
            new MatchEvent(30, MatchEventType.Other, "away")
        };

        Assert.Throws<ArgumentException>(() => new MatchReport(
            new MatchScore(0, 0), Array.Empty<MatchScorer>(), events,
            CreateTeamReport(), CreateTeamReport()));
    }

    private static MatchTeamInput CreateTeam(string teamId, IReadOnlyDictionary<PlayerAttribute, int> attributes) =>
        new(teamId, CreateStarters(teamId, attributes), Array.Empty<MatchPlayerInput>(), CreateTactics());

    private static MatchPlayerInput[] CreateStarters(string teamId, IReadOnlyDictionary<PlayerAttribute, int> attributes)
    {
        var players = new MatchPlayerInput[11];
        players[0] = CreatePlayer(teamId + "-0", Position.Goalkeeper, attributes);
        for (var index = 1; index < players.Length; index++)
            players[index] = CreatePlayer(teamId + "-" + index, Position.CentreBack, attributes);
        return players;
    }

    private static MatchPlayerInput CreatePlayer(string id, Position position,
        IReadOnlyDictionary<PlayerAttribute, int> attributes) =>
        new(id, position, attributes, 80, 60);

    private static Dictionary<PlayerAttribute, int> CreateAttributes() =>
        Enum.GetValues(typeof(PlayerAttribute)).Cast<PlayerAttribute>()
            .ToDictionary(attribute => attribute, _ => 60);

    private static MatchTactics CreateTactics() =>
        new("4-4-2", TacticalMentality.Balanced, PassingStyle.Mixed,
            PressingStyle.Medium, DefensiveLine.Normal, TeamWidth.Normal);

    private static MatchTeamReport CreateTeamReport() =>
        new(50, 8, 3, 1.2, new MatchZoneMap(Enumerable.Repeat(0, 9).ToArray()),
            Array.Empty<MatchPlayerRating>());
}
