using System;
using System.Linq;
using BT.Core.Collections;
using BT.Core.Rng;
using BT.Core.Sim;
using Xunit;

namespace BT.Invariants;

// Deterministic generators keep failures reproducible without adding a property-testing package.
internal static class Samples
{
    public static readonly int[] Integers = Enumerable.Range(0, 512)
        .Select(i => unchecked((int)new Pcg32((ulong)i + 17, 91).NextUInt32())).ToArray();

    public static readonly double[] UnitValues = Enumerable.Range(0, 512)
        .Select(i => new Pcg32((ulong)i + 101, 37).NextDouble()).ToArray();
}

public sealed class ImplementedInvariantsTests
{
    [Fact]
    public void ClampAlwaysReturnsAValueWithinItsBounds()
    {
        foreach (var sample in Samples.Integers)
        {
            var minimum = sample % 100;
            var maximum = minimum + 100;
            var value = (long)sample * 7919;
            var clamped = SimulationFormulas.Clamp(value, minimum, maximum);
            Assert.InRange(clamped, minimum, maximum);
        }
    }

    [Fact]
    public void ExpectedMatchPointsStayWithinTheirDocumentedProbabilityDerivedRange()
    {
        foreach (var club in Samples.UnitValues)
        foreach (var rival in Samples.UnitValues.Take(32))
        foreach (var home in new[] { false, true })
        {
            var points = SimulationFormulas.ExpectedPoints(club * 100, rival * 100, home);
            Assert.InRange(points, 0.37, 2.37);
        }
    }

    [Fact]
    public void MatchSeedsAndRandomStreamsRepeatForTheSameCoordinates()
    {
        foreach (var season in Samples.Integers.Take(100))
        {
            var seedA = SeedDeriver.MatchSeed(42, "league", season, 3, "match-1");
            var seedB = SeedDeriver.MatchSeed(42, "league", season, 3, "match-1");
            Assert.Equal(seedA, seedB);
            var first = new Pcg32(seedA, 7);
            var second = new Pcg32(seedB, 7);
            for (var i = 0; i < 12; i++) Assert.Equal(first.NextUInt32(), second.NextUInt32());
        }
    }

    [Fact]
    public void RandomProbabilitiesAndWeightedChoicesStayInTheirValidDomains()
    {
        foreach (var seed in Samples.Integers.Take(100))
        {
            var rng = new Pcg32(unchecked((ulong)(uint)seed), 23);
            var probability = rng.NextDouble();
            Assert.InRange(probability, 0.0, Math.BitDecrement(1.0));
            var choice = RandomDistributions.WeightedChoice(ref rng, new[] { 0.0, probability + 0.01, 1.0 });
            Assert.InRange(choice, 0, 2);
        }
    }

    [Fact]
    public void OrderedCollectionsProduceCanonicalOrderRegardlessOfInputOrder()
    {
        foreach (var offset in Samples.Integers.Take(64))
        {
            var items = Enumerable.Range(0, 24).Select(x => x + offset).ToArray();
            var shuffled = items.Reverse().ToArray();
            Assert.Equal(items.OrderBy(x => x), new OrderedSet<int>(shuffled));
            var pairs = shuffled.Select(x => new System.Collections.Generic.KeyValuePair<int, int>(x, -x));
            Assert.Equal(items.OrderBy(x => x), new OrderedDictionary<int, int>(pairs).Keys);
        }
    }

    [Fact]
    public void FisherYatesPreservesEveryInputItem()
    {
        foreach (var seed in Samples.Integers.Take(100))
        {
            var input = Enumerable.Range(0, 32).ToArray();
            var expected = input.OrderBy(x => x).ToArray();
            var rng = new Pcg32(unchecked((ulong)(uint)seed), 19);
            RandomDistributions.FisherYates(ref rng, input);
            Assert.Equal(expected, input.OrderBy(x => x));
        }
    }
}
