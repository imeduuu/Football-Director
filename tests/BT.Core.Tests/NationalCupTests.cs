using System;
using System.Linq;
using BT.Core.Competition;
using Xunit;

namespace BT.Core.Tests;

public sealed class NationalCupTests
{
    [Fact]
    public void SelectsSecondDivisionPlacesFiveToTwentyAndRanksByReputationInFirstSeason()
    {
        var clubs = Clubs();
        var priorSeason = NationalCup.SelectPreliminaryEntrants(clubs, false);
        Assert.Equal(16, priorSeason.Count);
        Assert.Equal(Enumerable.Range(5, 16), priorSeason.Select(c => c.PreviousPosition));

        var firstSeason = NationalCup.SelectPreliminaryEntrants(clubs, true);
        Assert.Equal(Enumerable.Range(5, 16), firstSeason.Select(c => c.Reputation));
    }

    [Fact]
    public void PreliminaryIsDeterministicEightMatchesAndWeekNine()
    {
        var first = NationalCup.CreatePreliminary(Clubs(), false, 42, 1);
        var second = NationalCup.CreatePreliminary(Clubs(), false, 42, 1);
        Assert.Equal(8, first.Matches.Count);
        Assert.Equal(9, first.Week);
        Assert.Equal(first.Matches.Select(m => (m.Home.ClubId, m.Away.ClubId)), second.Matches.Select(m => (m.Home.ClubId, m.Away.ClubId)));
        Assert.All(first.Matches, m => Assert.NotEqual(m.Home.ClubId, m.Away.ClubId));
        Assert.NotEqual(first.Matches[0].Home.ClubId, NationalCup.CreatePreliminary(Clubs(), false, 43, 1).Matches[0].Home.ClubId);
    }

    [Fact]
    public void RoundOf32HasTwentyFourExemptAndEightWinnersThenRoundsUseRequiredWeeks()
    {
        var clubs = Clubs();
        var prelim = NationalCup.CreatePreliminary(clubs, false, 42, 3);
        var winners = prelim.Matches.Select(m => m.Home.ClubId).ToArray();
        var round32 = NationalCup.CreateRoundOf32(clubs, winners, 42, 3);
        Assert.Equal(16, NationalCup.SelectPreliminaryEntrants(clubs, false).Count);
        Assert.Equal(16, round32.Matches.Count);
        Assert.Equal(14, round32.Week);

        var participants = round32.Matches.SelectMany(m => new[] { m.Home, m.Away }).ToArray();
        var expected = new[] { (CupRoundKind.RoundOf16, 8, 20), (CupRoundKind.QuarterFinal, 4, 32), (CupRoundKind.SemiFinal, 2, 38), (CupRoundKind.Final, 1, 45) };
        foreach (var (kind, games, week) in expected)
        {
            var round = NationalCup.Draw(kind, participants.Take(games * 2), 42, 3);
            Assert.Equal(games, round.Matches.Count);
            Assert.Equal(week, round.Week);
            Assert.All(round.Matches, m => Assert.Equal(kind == CupRoundKind.Final, m.NeutralVenue));
            participants = round.Matches.SelectMany(m => new[] { m.Home, m.Away }).ToArray();
        }
    }

    [Fact]
    public void MapsWeeksToWednesdaysAndValidatesScheduleAnchor()
    {
        var anchor = new DateTime(2030, 1, 7);
        Assert.Equal(new DateTime(2030, 1, 9), NationalCup.WednesdayOfWeek(anchor, 1));
        Assert.Equal(DayOfWeek.Wednesday, NationalCup.WednesdayOfWeek(anchor, 45).DayOfWeek);
        Assert.Throws<ArgumentException>(() => NationalCup.WednesdayOfWeek(anchor.AddDays(1), 9));
    }

    private static CupClub[] Clubs() => Enumerable.Range(1, 40)
        .Select(i => i <= 20
            ? new CupClub($"F{i:D2}", 1, i, 100 - i)
            : new CupClub($"S{i - 20:D2}", 2, i - 20, i - 20))
        .ToArray();
}
