using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BT.Core.Rng;
using BT.Core.Sim;
using Xunit;

namespace BT.Core.Tests;

public sealed class CoreGoldenTests
{
    private const int IterationCount = 1_000_000;

    [Fact]
    public void FixedMillionStepCoreScriptMatchesGoldenHash()
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[8];
        var rng = new Pcg32(0xB07D00D5EEDUL, 0xC0DEFACE1234UL);
        var weights = new[] { 0.0, 0.15, 0.35, 0.5 };

        for (var i = 0; i < IterationCount; i++)
        {
            Append(hash, buffer, rng.NextUInt32());
            var unit = rng.NextDouble();
            Append(hash, buffer, DetMath.Ln(0.25 + unit));
            Append(hash, buffer, DetMath.Exp((unit - 0.5) * 8.0));
            Append(hash, buffer, DetMath.Sin((unit - 0.5) * 64.0));
            Append(hash, buffer, DetMath.Sqrt(unit));
            Append(hash, buffer, RandomDistributions.WeightedChoice(ref rng, weights));
            Append(hash, buffer, RandomDistributions.Poisson(ref rng, 4.5));
            Append(hash, buffer, RandomDistributions.TruncatedNormal(ref rng, 45.0, 2.5, 42.0, 50.0));
        }

        var actual = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        var expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "core.hash"), Encoding.ASCII).Trim();
        Assert.Equal(expected, actual);
    }

    private static void Append(IncrementalHash hash, byte[] buffer, uint value)
    {
        WriteUInt64(buffer, value);
        hash.AppendData(buffer);
    }

    private static void Append(IncrementalHash hash, byte[] buffer, int value)
    {
        WriteUInt64(buffer, unchecked((ulong)(long)value));
        hash.AppendData(buffer);
    }

    private static void Append(IncrementalHash hash, byte[] buffer, double value)
    {
        WriteUInt64(buffer, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        hash.AppendData(buffer);
    }

    private static void WriteUInt64(byte[] buffer, ulong value)
    {
        for (var i = 0; i < buffer.Length; i++) buffer[i] = (byte)(value >> (i * 8));
    }
}
