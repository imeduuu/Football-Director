using System;

namespace BT.Core.Rng;

/// <summary>A deterministic PCG-XSH-RR generator with a 64-bit state and 32-bit output.</summary>
public struct Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong SplitMixIncrement = 0x9E3779B97F4A7C15UL;
    private const ulong SplitMixMultiplier1 = 0xBF58476D1CE4E5B9UL;
    private const ulong SplitMixMultiplier2 = 0x94D049BB133111EBUL;

    private readonly ulong _seed;
    private readonly ulong _sequence;
    private ulong _state;
    private readonly ulong _increment;

    /// <summary>Creates a PCG32 stream from a seed and stream sequence.</summary>
    public Pcg32(ulong seed, ulong sequence)
    {
        _seed = seed;
        _sequence = sequence;
        _state = 0;
        _increment = unchecked((sequence << 1) | 1UL);
        NextUInt32();
        _state = unchecked(_state + seed);
        NextUInt32();
    }

    /// <summary>Returns the next pseudorandom 32-bit value.</summary>
    public uint NextUInt32()
    {
        var oldState = _state;
        _state = unchecked(oldState * Multiplier + _increment);

        var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rotation = (int)(oldState >> 59);
        return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
    }

    /// <summary>Returns a value in the half-open interval [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), "The upper bound must be greater than the lower bound.");

        var bound = (uint)((long)maxExclusive - minInclusive);
        var threshold = unchecked(0U - bound) % bound;
        uint value;
        do
        {
            value = NextUInt32();
        } while (value < threshold);

        return (int)((long)minInclusive + value % bound);
    }

    /// <summary>Returns a uniformly distributed double in [0, 1), using 53 random bits.</summary>
    public double NextDouble()
    {
        var high = NextUInt32() >> 5;
        var low = NextUInt32() >> 6;
        return (high * 67108864.0 + low) / 9007199254740992.0;
    }

    /// <summary>Creates a stable child stream for an identifier, independent of this stream's current state.</summary>
    public Pcg32 Fork(ulong streamId)
    {
        var childSeed = Mix(unchecked(_seed + streamId * SplitMixIncrement));
        var childSequence = Mix(unchecked(_sequence + streamId * SplitMixMultiplier1 + SplitMixIncrement));
        return new Pcg32(childSeed, childSequence);
    }

    private static ulong Mix(ulong value)
    {
        value = unchecked((value ^ (value >> 30)) * SplitMixMultiplier1);
        value = unchecked((value ^ (value >> 27)) * SplitMixMultiplier2);
        return value ^ (value >> 31);
    }
}
