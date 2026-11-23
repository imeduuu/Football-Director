using System;

namespace BT.Core.Rng;

/// <summary>Stable xxHash64 implementation using the standard seed-zero variant.</summary>
public static class StableHash64
{
    private const ulong Prime1 = 11400714785074694791UL;
    private const ulong Prime2 = 14029467366897019727UL;
    private const ulong Prime3 = 1609587929392839161UL;
    private const ulong Prime4 = 9650029242287828579UL;
    private const ulong Prime5 = 2870177450012600261UL;

    /// <summary>Hashes the supplied bytes with xxHash64 and the given seed.</summary>
    public static ulong Compute(ReadOnlySpan<byte> data, ulong seed = 0)
    {
        var index = 0;
        ulong hash;
        if (data.Length >= 32)
        {
            var v1 = unchecked(seed + Prime1 + Prime2);
            var v2 = unchecked(seed + Prime2);
            var v3 = seed;
            var v4 = unchecked(seed - Prime1);
            var limit = data.Length - 32;
            do
            {
                v1 = Round(v1, Read64(data, index)); index += 8;
                v2 = Round(v2, Read64(data, index)); index += 8;
                v3 = Round(v3, Read64(data, index)); index += 8;
                v4 = Round(v4, Read64(data, index)); index += 8;
            } while (index <= limit);

            hash = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
            hash = MergeRound(hash, v1);
            hash = MergeRound(hash, v2);
            hash = MergeRound(hash, v3);
            hash = MergeRound(hash, v4);
        }
        else
        {
            hash = unchecked(seed + Prime5);
        }

        hash = unchecked(hash + (ulong)data.Length);
        while (index <= data.Length - 8)
        {
            var lane = Round(0, Read64(data, index));
            hash ^= lane;
            hash = unchecked(RotateLeft(hash, 27) * Prime1 + Prime4);
            index += 8;
        }
        if (index <= data.Length - 4)
        {
            hash ^= unchecked((ulong)Read32(data, index) * Prime1);
            hash = unchecked(RotateLeft(hash, 23) * Prime2 + Prime3);
            index += 4;
        }
        while (index < data.Length)
        {
            hash ^= unchecked(data[index] * Prime5);
            hash = unchecked(RotateLeft(hash, 11) * Prime1);
            index++;
        }

        hash ^= hash >> 33;
        hash = unchecked(hash * Prime2);
        hash ^= hash >> 29;
        hash = unchecked(hash * Prime3);
        return hash ^ (hash >> 32);
    }

    private static ulong Round(ulong accumulator, ulong input)
    {
        accumulator = unchecked(accumulator + input * Prime2);
        accumulator = RotateLeft(accumulator, 31);
        return unchecked(accumulator * Prime1);
    }

    private static ulong MergeRound(ulong hash, ulong value)
    {
        hash ^= Round(0, value);
        return unchecked(hash * Prime1 + Prime4);
    }

    private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));

    private static ulong Read64(ReadOnlySpan<byte> data, int index) =>
        (ulong)data[index] | ((ulong)data[index + 1] << 8) | ((ulong)data[index + 2] << 16) |
        ((ulong)data[index + 3] << 24) | ((ulong)data[index + 4] << 32) | ((ulong)data[index + 5] << 40) |
        ((ulong)data[index + 6] << 48) | ((ulong)data[index + 7] << 56);

    private static uint Read32(ReadOnlySpan<byte> data, int index) =>
        (uint)data[index] | ((uint)data[index + 1] << 8) | ((uint)data[index + 2] << 16) | ((uint)data[index + 3] << 24);
}
