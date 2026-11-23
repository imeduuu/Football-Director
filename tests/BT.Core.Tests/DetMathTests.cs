using System;
using BT.Core.Sim;
using Xunit;

namespace BT.Core.Tests;

public sealed class DetMathTests
{
    [Fact]
    public void ApproximationsStayWithinDocumentedBounds()
    {
        for (var i = 1; i <= 10000; i++)
        {
            var x = i / 100.0;
            Assert.InRange(Math.Abs(DetMath.Ln(x) - Math.Log(x)), 0.0, 2e-14);
            Assert.InRange(Math.Abs(DetMath.Sqrt(x) / Math.Sqrt(x) - 1.0), 0.0, 2e-15);

            var exponent = -7.0 + i * 0.0014;
            var actualExp = DetMath.Exp(exponent);
            Assert.InRange(Math.Abs(actualExp / Math.Exp(exponent) - 1.0), 0.0, 2e-14);

            var angle = -64.0 + i * 0.0128;
            Assert.InRange(Math.Abs(DetMath.Sin(angle) - Math.Sin(angle)), 0.0, 3e-14);
            Assert.InRange(Math.Abs(DetMath.Cos(angle) - Math.Cos(angle)), 0.0, 3e-14);
        }
    }

    [Fact]
    public void PowMatchesPositiveRealPowers()
    {
        for (var i = 1; i <= 1000; i++)
        {
            var x = i / 100.0;
            var y = (i % 31 - 15) / 4.0;
            var actual = DetMath.Pow(x, y);
            Assert.InRange(Math.Abs(actual / Math.Pow(x, y) - 1.0), 0.0, 5e-14);
        }
    }

    [Fact]
    public void RejectsValuesOutsideDocumentedDomains()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DetMath.Ln(0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DetMath.Exp(701.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DetMath.Sqrt(-1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DetMath.Sin(65.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DetMath.Cos(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => DetMath.Pow(-2.0, 3.0));
    }
}
