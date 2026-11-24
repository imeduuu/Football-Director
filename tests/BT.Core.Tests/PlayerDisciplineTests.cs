using System;
using BT.Core;
using BT.Core.Competition;
using Xunit;

namespace BT.Core.Tests;

public sealed class PlayerDisciplineTests
{
    private static readonly DisciplineRules Rules = new(5, 1, 1, 0,
        "perCompetition", "issuingCompetitionThenLeagueIfNoFixturesRemain", "afterMatchBeforeNextSquadSelection");

    [Fact]
    public void FifthLeagueYellowCreatesOneMatchBanAndCupYellowsStaySeparate()
    {
        var discipline = new PlayerDiscipline("p1");
        var noFixtures = Array.Empty<ScheduledMatch>();
        for (var i = 0; i < 4; i++) discipline.ProcessMatch(MatchCompetition.League,
            new[] { Card(PlayerCardKind.Yellow) }, noFixtures, "club", Rules);
        discipline.ProcessMatch(MatchCompetition.Cup, new[] { Card(PlayerCardKind.Yellow) }, noFixtures, "club", Rules);
        Assert.Equal(4, discipline.YellowCards(MatchCompetition.League));
        Assert.Equal(1, discipline.YellowCards(MatchCompetition.Cup));

        discipline.ProcessMatch(MatchCompetition.League, new[] { Card(PlayerCardKind.Yellow) }, noFixtures, "club", Rules);
        Assert.Equal(0, discipline.YellowCards(MatchCompetition.League));
        Assert.Equal(1, discipline.RemainingBanMatches(MatchCompetition.League));
        Assert.Equal(0, discipline.RemainingBanMatches(MatchCompetition.Cup));
    }

    [Fact]
    public void DirectRedServesInCupWhenCupFixtureRemainsAndFallsBackToLeagueOtherwise()
    {
        var discipline = new PlayerDiscipline("p1");
        var cupFixture = new[] { new ScheduledMatch("cup-2", new DateTime(2030, 1, 2), "club", "other", MatchCompetition.Cup) };
        discipline.ProcessMatch(MatchCompetition.Cup, new[] { Card(PlayerCardKind.DirectRed) }, cupFixture, "club", Rules);
        Assert.Equal(1, discipline.RemainingBanMatches(MatchCompetition.Cup));
        Assert.Equal(0, discipline.RemainingBanMatches(MatchCompetition.League));

        var leagueOnly = new[] { new ScheduledMatch("league-2", new DateTime(2030, 1, 5), "club", "other", MatchCompetition.League) };
        discipline.ProcessMatch(MatchCompetition.Cup, new[] { Card(PlayerCardKind.DirectRed) }, leagueOnly, "club", Rules);
        Assert.Equal(1, discipline.RemainingBanMatches(MatchCompetition.League));
    }

    private static PlayerCard Card(PlayerCardKind kind) => new("p1", kind);
}
