using System;
using System.IO;
using BT.Core;
using BT.Core.Domain;
using BT.Core.Sim;
using Xunit;

namespace BT.Core.Tests;

public sealed class SimulationFormulasTests
{
    [Fact]
    public void ClampAlwaysStaysWithinBoundsAndPreservesInteriorValues()
    {
        for (var i = -200; i <= 200; i++)
        {
            var value = i / 4.0;
            var clamped = SimulationFormulas.Clamp(value, -10.0, 10.0);
            Assert.InRange(clamped, -10.0, 10.0);
            Assert.Equal(value < -10.0 ? -10.0 : value > 10.0 ? 10.0 : value, clamped);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationFormulas.Clamp(1.0, 2.0, 0.0));
    }

    [Fact]
    public void LinearInterpolationIsBoundedByAdjacentNodeValuesAndHitsNodes()
    {
        var ageFactors = new (double X, double Y)[]
        {
            (16, 1.00), (18, 1.10), (21, 1.10), (24, 1.05), (27, 1.00),
            (29, 0.85), (31, 0.62), (33, 0.38), (35, 0.20), (38, 0.10)
        };
        foreach (var node in ageFactors)
            Assert.Equal(node.Y, SimulationFormulas.LinearInterpolate(ageFactors, node.X));
        Assert.Equal(1.0, SimulationFormulas.LinearInterpolate(ageFactors, 15.0));
        Assert.Equal(0.10, SimulationFormulas.LinearInterpolate(ageFactors, 40.0));

        for (var age = 16.0; age <= 38.0; age += 0.125)
        {
            var result = SimulationFormulas.LinearInterpolate(ageFactors, age);
            Assert.InRange(result, 0.10, 1.10);
        }
        Assert.Throws<ArgumentException>(() => SimulationFormulas.LinearInterpolate(new[] { (1.0, 1.0), (1.0, 2.0) }, 1.0));
    }

    [Fact]
    public void ExpectedPointsStayInGddRangeAndMatchEqualReputationExamples()
    {
        var parameters = ParamsConfig.Load(Path.Combine(AppContext.BaseDirectory, "config", "params.json")).ExpectedPoints;
        Assert.Equal(1.54, SimulationFormulas.ExpectedPoints(50.0, 50.0, true, parameters), 2);
        Assert.Equal(1.20, SimulationFormulas.ExpectedPoints(50.0, 50.0, false, parameters), 2);
        for (var club = 0; club <= 100; club++)
            for (var rival = 0; rival <= 100; rival++)
                foreach (var home in new[] { false, true })
                    Assert.InRange(SimulationFormulas.ExpectedPoints(club, rival, home, parameters), 0.37, 2.37);
    }

    [Fact]
    public void ExponentialMovingAverageIsConvexAndHasIdentityEndpoints()
    {
        for (var previous = -20; previous <= 20; previous++)
            for (var observation = -20; observation <= 20; observation++)
                for (var step = 0; step <= 100; step++)
                {
                    var alpha = step / 100.0;
                    var average = SimulationFormulas.ExponentialMovingAverage(previous, observation, alpha);
                    Assert.InRange(average, Math.Min(previous, observation), Math.Max(previous, observation));
                    Assert.Equal(previous, SimulationFormulas.ExponentialMovingAverage(previous, observation, 0.0));
                    Assert.Equal(observation, SimulationFormulas.ExponentialMovingAverage(previous, observation, 1.0));
                }
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationFormulas.ExponentialMovingAverage(0, 1, 1.1));
    }

    [Fact]
    public void ZoneMembershipMatchesGddExamplesAndSumsToOneAcrossThePitch()
    {
        Assert.Equal(1.0, SimulationFormulas.WDef(25));
        Assert.Equal(0.5, SimulationFormulas.WDef(35));
        Assert.Equal(0.5, SimulationFormulas.WMed(35));
        Assert.Equal(0.2, SimulationFormulas.WDef(38));
        Assert.Equal(0.8, SimulationFormulas.WMed(38));
        Assert.Equal(0.5, SimulationFormulas.WMed(70));
        Assert.Equal(0.5, SimulationFormulas.WAta(70));
        Assert.Equal(1.0, SimulationFormulas.WAta(78));

        Assert.Equal(1.0, SimulationFormulas.WIzq(25));
        Assert.Equal(0.5, SimulationFormulas.WIzq(33));
        Assert.Equal(0.5, SimulationFormulas.WCen(33));
        Assert.Equal(0.2, SimulationFormulas.WIzq(36));
        Assert.Equal(0.8, SimulationFormulas.WCen(36));
        Assert.Equal(0.5, SimulationFormulas.WCen(67));
        Assert.Equal(0.5, SimulationFormulas.WDer(67));
        Assert.Equal(1.0, SimulationFormulas.WDer(75));

        for (var coordinate = 0; coordinate <= 100; coordinate++)
        {
            var sectorWeights = new[]
            {
                SimulationFormulas.WDef(coordinate),
                SimulationFormulas.WMed(coordinate),
                SimulationFormulas.WAta(coordinate)
            };
            var laneWeights = new[]
            {
                SimulationFormulas.WIzq(coordinate),
                SimulationFormulas.WCen(coordinate),
                SimulationFormulas.WDer(coordinate)
            };
            Assert.All(sectorWeights, weight => Assert.InRange(weight, 0.0, 1.0));
            Assert.All(laneWeights, weight => Assert.InRange(weight, 0.0, 1.0));
            Assert.Equal(1.0, sectorWeights[0] + sectorWeights[1] + sectorWeights[2], 12);
            Assert.Equal(1.0, laneWeights[0] + laneWeights[1] + laneWeights[2], 12);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationFormulas.WDef(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SimulationFormulas.WDer(101));
    }

    [Fact]
    public void ZonalStrengthsMirrorLanesAndExcludeGoalkeepers()
    {
        var players = new[]
        {
            new ZonalStrengthContribution(Position.LeftBack, 15, 25, 80),
            new ZonalStrengthContribution(Position.Goalkeeper, 15, 25, 500)
        };

        var teamStrengths = SimulationFormulas.CalculateZonalStrengths(players);
        var mirroredStrengths = SimulationFormulas.CalculateZonalStrengths(players, mirrorLanes: true);

        Assert.Equal(80.0, teamStrengths[MatchZone.DefenceLeft]);
        Assert.Equal(0.0, teamStrengths[MatchZone.DefenceRight]);
        Assert.Equal(0.0, mirroredStrengths[MatchZone.DefenceLeft]);
        Assert.Equal(80.0, mirroredStrengths[MatchZone.DefenceRight]);
        Assert.Equal(80.0, Sum(teamStrengths.Cells), 12);
        Assert.Equal(80.0, Sum(mirroredStrengths.Cells), 12);
        Assert.Equal(80.0, Sum(SimulationFormulas.CalculateZonalStrengths(
            new[] { players[0] }).Cells), 12);
        Assert.Equal(85.0, SimulationFormulas.MirrorLaneX(15));
    }

    [Fact]
    public void ZonalStrengthsMultiplySectorAndLaneMembershipWeights()
    {
        var strengths = SimulationFormulas.CalculateZonalStrengths(new[]
        {
            new ZonalStrengthContribution(Position.CentralMidfielder, 33, 35, 100)
        });

        Assert.Equal(25.0, strengths[MatchZone.DefenceLeft]);
        Assert.Equal(25.0, strengths[MatchZone.DefenceCentre]);
        Assert.Equal(25.0, strengths[MatchZone.MidfieldLeft]);
        Assert.Equal(25.0, strengths[MatchZone.MidfieldCentre]);
        Assert.Equal(0.0, strengths[MatchZone.AttackLeft]);
        Assert.Equal(100.0, Sum(strengths.Cells), 12);
    }

    private static double Sum(System.Collections.Generic.IReadOnlyList<double> values)
    {
        var total = 0.0;
        for (var index = 0; index < values.Count; index++) total += values[index];
        return total;
    }
}
