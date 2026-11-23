using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core.Rng;
using Xunit;

namespace BT.Core.Tests;

public sealed class RandomDistributionsTests
{
    [Fact]
    public void TruncatedNormalMatchesRegenBoundsAndIsRepeatable()
    {
        // GDD 5.4: Nv=1 gives mean 45, deviation 2.5, and bounds [42, 50].
        var first = new Pcg32(1, 2);
        var second = new Pcg32(1, 2);
        for (var i = 0; i < 1000; i++)
        {
            var a = RandomDistributions.TruncatedNormal(ref first, 45.0, 2.5, 42.0, 50.0);
            var b = RandomDistributions.TruncatedNormal(ref second, 45.0, 2.5, 42.0, 50.0);
            Assert.Equal(a, b);
            Assert.InRange(a, 42.0, 50.0);
        }
    }

    [Fact]
    public void TruncatedNormalSupportsZeroDeviationOnlyWhenMeanIsInBounds()
    {
        var rng = new Pcg32(1, 2);
        Assert.Equal(5.0, RandomDistributions.TruncatedNormal(ref rng, 5.0, 0.0, 1.0, 99.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => RandomDistributions.TruncatedNormal(ref rng, 0.0, 0.0, 1.0, 99.0));
    }

    [Fact]
    public void WeightedChoiceRespectsWeightsAndRepeatability()
    {
        var first = new Pcg32(3, 4);
        var second = new Pcg32(3, 4);
        IReadOnlyList<double> weights = new[] { 0.0, 1.0, 0.0 };
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(1, RandomDistributions.WeightedChoice(ref first, weights));
            Assert.Equal(1, RandomDistributions.WeightedChoice(ref second, weights));
        }
        Assert.Throws<ArgumentException>(() => RandomDistributions.WeightedChoice(ref first, new[] { 0.0, 0.0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => RandomDistributions.WeightedChoice(ref first, new[] { 1.0, -1.0 }));
    }

    [Fact]
    public void FisherYatesPreservesItemsAndIsRepeatable()
    {
        var firstItems = new List<int> { 1, 2, 3, 4, 5, 6 };
        var secondItems = new List<int>(firstItems);
        var first = new Pcg32(5, 6);
        var second = new Pcg32(5, 6);
        RandomDistributions.FisherYates(ref first, firstItems);
        RandomDistributions.FisherYates(ref second, secondItems);
        Assert.Equal(firstItems, secondItems);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, firstItems.OrderBy(x => x));
    }

    [Fact]
    public void PoissonZeroLambdaReturnsZeroAndCountsAreRepeatable()
    {
        var first = new Pcg32(7, 8);
        var second = new Pcg32(7, 8);
        Assert.Equal(0, RandomDistributions.Poisson(ref first, 0.0));
        for (var i = 0; i < 100; i++)
        {
            var a = RandomDistributions.Poisson(ref first, 4.5);
            var b = RandomDistributions.Poisson(ref second, 4.5);
            Assert.Equal(a, b);
            Assert.True(a >= 0);
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => RandomDistributions.Poisson(ref first, -1.0));
    }
}
