using System;
using BT.Core.Rng;
using Xunit;

namespace BT.Core.Tests;

public sealed class Pcg32Tests
{
    [Fact]
    public void MatchesPcgReferenceSequence()
    {
        var rng = new Pcg32(42, 54);

        Assert.Equal(0xA15C02B7U, rng.NextUInt32());
        Assert.Equal(0x7B47F409U, rng.NextUInt32());
        Assert.Equal(0xBA1D3330U, rng.NextUInt32());
        Assert.Equal(0x83D2F293U, rng.NextUInt32());
        Assert.Equal(0xBFA4784BU, rng.NextUInt32());
        Assert.Equal(0xCBED606EU, rng.NextUInt32());
    }

    [Fact]
    public void NextIntSupportsFullSignedRangeAndRejectsInvalidBounds()
    {
        var rng = new Pcg32(12, 34);
        for (var i = 0; i < 1000; i++)
        {
            var value = rng.NextInt(int.MinValue, int.MaxValue);
            Assert.InRange(value, int.MinValue, int.MaxValue - 1);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(3, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(4, 3));
    }

    [Fact]
    public void NextDoubleIsInHalfOpenUnitInterval()
    {
        var rng = new Pcg32(123, 456);
        for (var i = 0; i < 1000; i++)
            Assert.InRange(rng.NextDouble(), 0.0, Math.BitDecrement(1.0));
    }

    [Fact]
    public void ForkIsStableAndIndependentOfParentPosition()
    {
        var firstParent = new Pcg32(789, 321);
        var secondParent = new Pcg32(789, 321);
        for (var i = 0; i < 20; i++) firstParent.NextUInt32();

        var firstChild = firstParent.Fork(99);
        var secondChild = secondParent.Fork(99);
        for (var i = 0; i < 20; i++)
            Assert.Equal(firstChild.NextUInt32(), secondChild.NextUInt32());
    }
}
