using System.Linq;
using BT.Core;
using BT.Core.Competition;
using Xunit;

namespace BT.Core.Tests;

public sealed class LeagueStandingsTests
{
    private static readonly PointsRules Rules = new PointsRules(3, 1, 0);

    [Fact]
    public void AccumulatesPointsAndGoalStatisticsAndOrdersByPointsThenGoalDifferenceThenGoalsFor()
    {
        var table = LeagueStandings.Calculate(new[] { "A", "B", "C", "D" }, new[]
        {
            new LeagueResult("A", "D", 3, 0), // A: 3 points, +3, 3 GF
            new LeagueResult("B", "D", 2, 0), // B: 3 points, +2, 2 GF
            new LeagueResult("C", "D", 4, 2), // C: 3 points, +2, 4 GF
        }, Rules, "league", "season");

        Assert.Equal(new[] { "A", "C", "B", "D" }, table.Select(row => row.ClubId));
        Assert.Equal((3, 1, 1, 0, 3, 0, 3),
            (table[0].Points, table[0].Played, table[0].Won, table[0].Drawn, table[0].GoalsFor, table[0].GoalsAgainst, table[0].GoalDifference));
    }

    [Fact]
    public void UsesHeadToHeadPointsAfterOverallPointsGoalDifferenceAndGoalsFor()
    {
        var table = LeagueStandings.Calculate(new[] { "A", "B", "C", "D" }, new[]
        {
            new LeagueResult("A", "B", 1, 0),
            new LeagueResult("A", "B", 1, 0),
            new LeagueResult("A", "C", 0, 1),
            new LeagueResult("A", "C", 0, 1),
            new LeagueResult("B", "C", 1, 0),
            new LeagueResult("B", "C", 1, 0),
            new LeagueResult("C", "D", 1, 0),
            new LeagueResult("C", "D", 1, 0),
        }, Rules, "league", "season");

        Assert.Equal(6, table.Single(row => row.ClubId == "A").Points);
        Assert.Equal(6, table.Single(row => row.ClubId == "B").Points);
        Assert.True(table.ToList().FindIndex(row => row.ClubId == "A") < table.ToList().FindIndex(row => row.ClubId == "B"));
    }

    [Fact]
    public void DeterministicDrawDoesNotDependOnInputClubOrder()
    {
        var forward = LeagueStandings.Calculate(new[] { "A", "B", "C" }, Enumerable.Empty<LeagueResult>(), Rules, "league", "season");
        var reverse = LeagueStandings.Calculate(new[] { "C", "B", "A" }, Enumerable.Empty<LeagueResult>(), Rules, "league", "season");

        Assert.Equal(forward.Select(row => row.ClubId), reverse.Select(row => row.ClubId));
        Assert.Equal(Enumerable.Range(1, 3), forward.Select(row => row.Position));
    }
}
