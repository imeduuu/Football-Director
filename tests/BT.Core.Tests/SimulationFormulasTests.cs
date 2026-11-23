using System;
using System.IO;
using BT.Core;
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
}
