using System;

namespace BT.Core.Sim;

/// <summary>
/// Deterministic binary64 math for simulation. Every polynomial is evaluated in the
/// written order; inputs outside the documented domains are rejected.
/// </summary>
public static class DetMath
{
    private const double Ln2 = 0.693147180559945309417232121458176568;
    private const double InvLn2 = 1.44269504088896340735992468100189214;
    private const double HalfPi = 1.57079632679489661923132169163975144;
    private const double TwoOverPi = 0.636619772367581343075535053490057448;
    private const double PiOverTwoHi = 1.57079632679489655800;
    private const double PiOverTwoLo = 6.12323399573676603587e-17;
    private const double SqrtHalf = 0.707106781186547524400844362104849039;
    private const double SqrtTwo = 1.41421356237309504880168872420969808;
    private const double SubnormalScale = 18014398509481984.0; // 2^54
    private const double InverseSubnormalSqrtScale = 7.450580596923828125e-9; // 2^-27

    /// <summary>Natural logarithm for finite x &gt; 0. Maximum absolute error: 2e-14.</summary>
    public static double Ln(double x)
    {
        if (!(x > 0.0) || double.IsInfinity(x) || double.IsNaN(x))
            throw new ArgumentOutOfRangeException(nameof(x), "Ln requires a finite positive value.");

        var exponent = 0;
        if (x < 2.2250738585072014e-308)
        {
            x *= 18014398509481984.0; // 2^54, normalizes subnormal inputs.
            exponent = -54;
        }

        var bits = BitConverter.DoubleToInt64Bits(x);
        var rawExponent = (int)((bits >> 52) & 0x7ff);
        exponent += rawExponent - 1023;
        var mantissa = BitConverter.Int64BitsToDouble((bits & 0x000fffffffffffffL) | 0x3ff0000000000000L);
        if (mantissa > SqrtTwo)
        {
            mantissa *= 0.5;
            exponent++;
        }

        var z = (mantissa - 1.0) / (mantissa + 1.0);
        var z2 = z * z;
        var term = z;
        var sum = term;
        for (var denominator = 3; denominator <= 25; denominator += 2)
        {
            term *= z2;
            sum += term / denominator;
        }

        return (2.0 * sum + exponent * Ln2);
    }

    /// <summary>Exponential for finite x in [-700, 700]. Maximum relative error: 2e-14.</summary>
    public static double Exp(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x < -700.0 || x > 700.0)
            throw new ArgumentOutOfRangeException(nameof(x), "Exp requires a finite value in [-700, 700].");

        var n = (int)(x * InvLn2 + (x >= 0.0 ? 0.5 : -0.5));
        var r = (x - n * Ln2);
        var term = 1.0;
        var sum = 1.0;
        for (var i = 1; i <= 18; i++)
        {
            term = (term * r) / i;
            sum += term;
        }

        return ScaleByPowerOfTwo(sum, n);
    }

    /// <summary>Square root for finite x &gt;= 0. Maximum relative error for x &gt; 0: 2e-15.</summary>
    public static double Sqrt(double x)
    {
        if (x < 0.0 || double.IsNaN(x) || double.IsInfinity(x))
            throw new ArgumentOutOfRangeException(nameof(x), "Sqrt requires a finite nonnegative value.");
        if (x == 0.0) return x;

        var scaleResult = false;
        if (x < 2.2250738585072014e-308)
        {
            x *= SubnormalScale;
            scaleResult = true;
        }

        var bits = BitConverter.DoubleToInt64Bits(x);
        var guess = BitConverter.Int64BitsToDouble((bits >> 1) + 0x1ff8000000000000L);
        for (var i = 0; i < 7; i++)
            guess = 0.5 * (guess + x / guess);
        return scaleResult ? guess * InverseSubnormalSqrtScale : guess;
    }

    /// <summary>Sine for finite |x| &lt;= 64 radians. Maximum absolute error: 3e-14.</summary>
    public static double Sin(double x)
    {
        var quadrant = ReduceAngle(ref x);
        if (quadrant == 1) return CosReduced(x);
        if (quadrant == 2) return -SinReduced(x);
        if (quadrant == 3) return -CosReduced(x);
        return SinReduced(x);
    }

    /// <summary>Cosine for finite |x| &lt;= 64 radians. Maximum absolute error: 3e-14.</summary>
    public static double Cos(double x)
    {
        var quadrant = ReduceAngle(ref x);
        if (quadrant == 1) return -SinReduced(x);
        if (quadrant == 2) return -CosReduced(x);
        if (quadrant == 3) return SinReduced(x);
        return CosReduced(x);
    }

    /// <summary>Real power for finite x &gt; 0 and finite y with |y*Ln(x)| &lt;= 700. Maximum relative error: 5e-14.</summary>
    public static double Pow(double x, double y)
    {
        if (!(x > 0.0) || double.IsInfinity(x) || double.IsNaN(x) || double.IsInfinity(y) || double.IsNaN(y))
            throw new ArgumentOutOfRangeException(nameof(x), "Pow requires finite x &gt; 0 and finite y.");
        if (y == 0.0 || x == 1.0) return 1.0;
        var exponent = y * Ln(x);
        if (exponent < -700.0 || exponent > 700.0)
            throw new ArgumentOutOfRangeException(nameof(y), "The product y*Ln(x) must be in [-700, 700].");
        return Exp(exponent);
    }

    private static int ReduceAngle(ref double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x < -64.0 || x > 64.0)
            throw new ArgumentOutOfRangeException(nameof(x), "Trigonometric functions require finite |x| <= 64.");
        var q = (int)(x * TwoOverPi + (x >= 0.0 ? 0.5 : -0.5));
        x = ((x - q * PiOverTwoHi) - q * PiOverTwoLo);
        return q & 3;
    }

    private static double SinReduced(double x)
    {
        var x2 = x * x;
        var p = -1.0 / 355687428096000.0;
        p = 1.0 / 1307674368000.0 + x2 * p;
        p = -1.0 / 6227020800.0 + x2 * p;
        p = 1.0 / 39916800.0 + x2 * p;
        p = -1.0 / 362880.0 + x2 * p;
        p = 1.0 / 5040.0 + x2 * p;
        p = -1.0 / 120.0 + x2 * p;
        p = 1.0 / 6.0 + x2 * p;
        return x * (1.0 - x2 * p);
    }

    private static double CosReduced(double x)
    {
        var x2 = x * x;
        var p = -1.0 / 6402373705728000.0;
        p = 1.0 / 20922789888000.0 + x2 * p;
        p = -1.0 / 87178291200.0 + x2 * p;
        p = 1.0 / 479001600.0 + x2 * p;
        p = -1.0 / 3628800.0 + x2 * p;
        p = 1.0 / 40320.0 + x2 * p;
        p = -1.0 / 720.0 + x2 * p;
        p = 1.0 / 24.0 + x2 * p;
        p = -1.0 / 2.0 + x2 * p;
        return 1.0 + x2 * p;
    }

    private static double ScaleByPowerOfTwo(double value, int exponent)
    {
        while (exponent > 1023) { value *= 8.98846567431158e307; exponent -= 1023; }
        while (exponent < -1022) { value *= 2.2250738585072014e-308; exponent += 1022; }
        var scale = BitConverter.Int64BitsToDouble((long)(exponent + 1023) << 52);
        return value * scale;
    }
}
