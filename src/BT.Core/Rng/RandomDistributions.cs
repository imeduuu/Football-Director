using System;
using System.Collections.Generic;
using BT.Core.Sim;

namespace BT.Core.Rng;

/// <summary>Deterministic sampling algorithms built on <see cref="Pcg32"/>.</summary>
public static class RandomDistributions
{
    /// <summary>Samples a normal distribution by Box-Muller, rejecting values outside [minInclusive, maxInclusive].</summary>
    public static double TruncatedNormal(ref Pcg32 rng, double mean, double standardDeviation, double minInclusive, double maxInclusive)
    {
        if (!IsFinite(mean)) throw new ArgumentOutOfRangeException(nameof(mean));
        if (!IsFinite(standardDeviation) || standardDeviation < 0.0) throw new ArgumentOutOfRangeException(nameof(standardDeviation));
        if (!IsFinite(minInclusive) || !IsFinite(maxInclusive) || minInclusive > maxInclusive)
            throw new ArgumentOutOfRangeException(nameof(maxInclusive), "Bounds must be finite and ordered.");
        if (standardDeviation == 0.0)
        {
            if (mean < minInclusive || mean > maxInclusive)
                throw new ArgumentOutOfRangeException(nameof(mean), "A zero-deviation normal must lie within its bounds.");
            return mean;
        }

        while (true)
        {
            var u1 = rng.NextDouble();
            while (u1 == 0.0) u1 = rng.NextDouble();
            var u2 = rng.NextDouble();
            var radius = DetMath.Sqrt(-2.0 * DetMath.Ln(u1));
            var z = radius * DetMath.Cos(6.283185307179586476925286766559 * u2);
            var sample = mean + standardDeviation * z;
            if (IsFinite(sample) && sample >= minInclusive && sample <= maxInclusive)
                return sample;
        }
    }

    /// <summary>Chooses an index in proportion to its finite, nonnegative weight.</summary>
    public static int WeightedChoice(ref Pcg32 rng, IReadOnlyList<double> weights)
    {
        if (weights is null) throw new ArgumentNullException(nameof(weights));
        if (weights.Count == 0) throw new ArgumentException("At least one weight is required.", nameof(weights));
        var total = 0.0;
        for (var i = 0; i < weights.Count; i++)
        {
            var weight = weights[i];
            if (!IsFinite(weight) || weight < 0.0) throw new ArgumentOutOfRangeException(nameof(weights), "Weights must be finite and nonnegative.");
            total += weight;
            if (!IsFinite(total)) throw new ArgumentOutOfRangeException(nameof(weights), "The total weight must be finite.");
        }
        if (total <= 0.0) throw new ArgumentException("At least one weight must be positive.", nameof(weights));

        var target = rng.NextDouble() * total;
        var cumulative = 0.0;
        for (var i = 0; i < weights.Count; i++)
        {
            cumulative += weights[i];
            if (target < cumulative) return i;
        }
        // Rounding can only put the target on the upper endpoint.
        for (var i = weights.Count - 1; i >= 0; i--)
            if (weights[i] > 0.0) return i;
        throw new InvalidOperationException("A positive weight was not found.");
    }

    /// <summary>Shuffles a list in place with the unbiased Fisher-Yates algorithm.</summary>
    public static void FisherYates<T>(ref Pcg32 rng, IList<T> items)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(0, i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>Samples a Poisson count using Knuth's product method; lambda must be in [0, 700].</summary>
    public static int Poisson(ref Pcg32 rng, double lambda)
    {
        if (!IsFinite(lambda) || lambda < 0.0 || lambda > 700.0)
            throw new ArgumentOutOfRangeException(nameof(lambda), "Lambda must be finite and in [0, 700].");
        if (lambda == 0.0) return 0;

        var threshold = DetMath.Exp(-lambda);
        var product = 1.0;
        var count = 0;
        do
        {
            count++;
            product *= rng.NextDouble();
        } while (product > threshold);
        return count - 1;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
