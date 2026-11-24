using System;
using System.Linq;
using BT.Core;
using BT.Core.Competition;
using Xunit;

namespace BT.Core.Tests;

public sealed class SeasonMovementTests
{
    [Fact]
    public void AppliesPromotionAndRelegationCountsFromCompetitionConfiguration()
    {
        var configPath = System.IO.Path.Combine(AppContext.BaseDirectory, "config", "competitions");
        var upperRules = CompetitionRulesConfig.Load(System.IO.Path.Combine(configPath, "test-division-one.v1.json"));
        var lowerRules = CompetitionRulesConfig.Load(System.IO.Path.Combine(configPath, "test-division-two.v1.json"));
        var upper = Table("U1", "U2", "U3", "U4");
        var lower = Table("L1", "L2", "L3", "L4");

        var movement = SeasonMovement.Apply(upper, upperRules, lower, lowerRules);

        Assert.Equal(lowerRules.Movement.PromotedDirect, movement.Promoted.Count);
        Assert.Equal(upperRules.Movement.RelegatedDirect, movement.Relegated.Count);
        Assert.Equal(lower.Where(s => s.Position <= lowerRules.Movement.PromotedDirect).Select(s => s.ClubId), movement.Promoted);
        Assert.Equal(upper.OrderByDescending(s => s.Position).Take(upperRules.Movement.RelegatedDirect).Select(s => s.ClubId), movement.Relegated);
        Assert.Equal(upper.Count, movement.NextUpperDivision.Count);
        Assert.Equal(lower.Count, movement.NextLowerDivision.Count);
        Assert.All(movement.Promoted, id => Assert.Contains(id, movement.NextUpperDivision));
        Assert.All(movement.Relegated, id => Assert.Contains(id, movement.NextLowerDivision));
    }

    private static System.Collections.Generic.IReadOnlyList<LeagueStanding> Table(params string[] ids) =>
        LeagueStandings.Calculate(ids, Array.Empty<LeagueResult>(), new PointsRules(3, 1, 0), "test", "season");
}
