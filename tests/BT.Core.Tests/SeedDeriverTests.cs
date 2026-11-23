using System;
using System.Text;
using BT.Core.Rng;
using Xunit;

namespace BT.Core.Tests;

public sealed class SeedDeriverTests
{
    [Fact]
    public void StableHashMatchesXxHash64ReferenceVectors()
    {
        Assert.Equal(0xEF46DB3751D8E999UL, StableHash64.Compute(ReadOnlySpan<byte>.Empty));
        Assert.Equal(0xD24EC4F1A98C6E5BUL, StableHash64.Compute(Encoding.UTF8.GetBytes("a")));
        Assert.Equal(0x44BC2CF5AD770999UL, StableHash64.Compute(Encoding.UTF8.GetBytes("abc")));
    }

    [Fact]
    public void MatchSeedIsStableAndChangesWithEveryMatchCoordinate()
    {
        const ulong worldSeed = 0x123456789ABCDEF0UL;
        var seed = SeedDeriver.MatchSeed(worldSeed, "league-1", 2030, 12, "match-42");

        Assert.Equal(seed, SeedDeriver.MatchSeed(worldSeed, "league-1", 2030, 12, "match-42"));
        Assert.NotEqual(seed, SeedDeriver.MatchSeed(worldSeed + 1, "league-1", 2030, 12, "match-42"));
        Assert.NotEqual(seed, SeedDeriver.MatchSeed(worldSeed, "league-2", 2030, 12, "match-42"));
        Assert.NotEqual(seed, SeedDeriver.MatchSeed(worldSeed, "league-1", 2031, 12, "match-42"));
        Assert.NotEqual(seed, SeedDeriver.MatchSeed(worldSeed, "league-1", 2030, 13, "match-42"));
        Assert.NotEqual(seed, SeedDeriver.MatchSeed(worldSeed, "league-1", 2030, 12, "match-43"));
    }

    [Fact]
    public void MatchStreamsDoNotDependOnMatchSimulationOrder()
    {
        var first = new Pcg32(SeedDeriver.MatchSeed(99, "league", 2030, 1, "A"), 0);
        var second = new Pcg32(SeedDeriver.MatchSeed(99, "league", 2030, 1, "B"), 0);
        var expectedFirst = first.NextUInt32();
        var expectedSecond = second.NextUInt32();

        var reverseSecond = new Pcg32(SeedDeriver.MatchSeed(99, "league", 2030, 1, "B"), 0);
        var reverseFirst = new Pcg32(SeedDeriver.MatchSeed(99, "league", 2030, 1, "A"), 0);
        Assert.Equal(expectedSecond, reverseSecond.NextUInt32());
        Assert.Equal(expectedFirst, reverseFirst.NextUInt32());
    }
}
