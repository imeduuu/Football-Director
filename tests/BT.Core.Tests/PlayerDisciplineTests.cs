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

    [Fact]
    public void PreviousBanIsConsumedBeforeCardsAndSuspendedPlayerNeedNotAppear()
    {
        var discipline = new PlayerDiscipline("p1");
        var future = new[] { new ScheduledMatch("cup-next", new DateTime(2030, 1, 2), "club", "other", MatchCompetition.Cup) };
        discipline.ProcessMatch(MatchCompetition.Cup, new[] { Card(PlayerCardKind.DirectRed) }, future, "club", Rules);
        Assert.Equal(1, discipline.RemainingBanMatches(MatchCompetition.Cup));
        discipline.ProcessMatch(MatchCompetition.Cup, Array.Empty<PlayerCard>(), Array.Empty<ScheduledMatch>(), "club", Rules);
        Assert.Equal(0, discipline.RemainingBanMatches(MatchCompetition.Cup));
    }

    [Fact]
    public void PostponedFixtureCountsForRedServiceButPlayedFixtureDoesNot()
    {
        var discipline = new PlayerDiscipline("p1");
        var postponed = new[] { new ScheduledMatch("cup-postponed", new DateTime(2030, 1, 2), "club", "other", MatchCompetition.Cup, ScheduledMatchStatus.Postponed) };
        discipline.ProcessMatch(MatchCompetition.Cup, new[] { Card(PlayerCardKind.DirectRed) }, postponed, "club", Rules);
        Assert.Equal(1, discipline.RemainingBanMatches(MatchCompetition.Cup));

        var playedOnly = new[] { new ScheduledMatch("cup-played", new DateTime(2030, 1, 1), "club", "other", MatchCompetition.Cup, ScheduledMatchStatus.Played) };
        var other = new PlayerDiscipline("p2");
        other.ProcessMatch(MatchCompetition.Cup, new[] { new PlayerCard("p2", PlayerCardKind.DirectRed) }, playedOnly, "club", Rules);
        Assert.Equal(1, other.RemainingBanMatches(MatchCompetition.League));
    }

    [Fact]
    public void StableAccumulationGroupCanBeSharedAcrossCompetitionIds()
    {
        var discipline = new PlayerDiscipline("p1");
        var sharedRules = new DisciplineRules(2, 1, 1, 0, "perCompetition", "issuingCompetitionThenLeagueIfNoFixturesRemain",
            "afterMatchBeforeNextSquadSelection", "national-cup-yellow", "league-main");
        discipline.ProcessMatch(MatchCompetition.Cup, "cup-north", "cup-1", new[] { Card(PlayerCardKind.Yellow) }, Array.Empty<ScheduledMatch>(), "club", sharedRules, "2.0.0");
        discipline.ProcessMatch(MatchCompetition.Cup, "cup-south", "cup-2", new[] { Card(PlayerCardKind.Yellow) }, Array.Empty<ScheduledMatch>(), "club", sharedRules, "2.0.0");
        Assert.Equal(1, discipline.RemainingBanMatches("cup-south"));
        Assert.Equal(0, discipline.YellowCards("national-cup-yellow"));
    }

    private static PlayerCard Card(PlayerCardKind kind) => new("p1", kind);
}
