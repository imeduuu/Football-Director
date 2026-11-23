using System;
using System.Collections.Generic;
using BT.Core;

namespace BT.Core.Sim;

/// <summary>Deterministic formulas used by world and career simulation systems.</summary>
public static class SimulationFormulas
{
    /// <summary>Clamps a finite value to an inclusive finite range.</summary>
    public static double Clamp(double value, double minimum, double maximum)
    {
        EnsureFinite(value, nameof(value));
        EnsureFinite(minimum, nameof(minimum));
        EnsureFinite(maximum, nameof(maximum));
        if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(maximum), "Bounds must be ordered.");
        if (value < minimum) return minimum;
        if (value > maximum) return maximum;
        return value;
    }

    /// <summary>Linearly interpolates between strictly increasing (x,y) nodes, extending endpoint values outside the node range.</summary>
    public static double LinearInterpolate(IReadOnlyList<(double X, double Y)> nodes, double x)
    {
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));
        EnsureFinite(x, nameof(x));
        if (nodes.Count == 0) throw new ArgumentException("At least one node is required.", nameof(nodes));

        EnsureFinite(nodes[0].X, nameof(nodes));
        EnsureFinite(nodes[0].Y, nameof(nodes));
        for (var i = 1; i < nodes.Count; i++)
        {
            EnsureFinite(nodes[i].X, nameof(nodes));
            EnsureFinite(nodes[i].Y, nameof(nodes));
            if (nodes[i].X <= nodes[i - 1].X)
                throw new ArgumentException("Node x values must be strictly increasing.", nameof(nodes));
        }
        if (x <= nodes[0].X) return nodes[0].Y;

        for (var i = 1; i < nodes.Count; i++)
        {
            var left = nodes[i - 1];
            var right = nodes[i];
            if (x <= right.X)
            {
                var fraction = (x - left.X) / (right.X - left.X);
                return left.Y + fraction * (right.Y - left.Y);
            }
        }

        return nodes[nodes.Count - 1].Y;
    }

    /// <summary>Calculates expected match points from GDD 3.4 using the specified club and rival reputations.</summary>
    public static double ExpectedPoints(double clubReputation, double rivalReputation, bool isHome,
        ExpectedPointsParams parameters)
    {
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        EnsureFinite(clubReputation, nameof(clubReputation));
        EnsureFinite(rivalReputation, nameof(rivalReputation));
        var d = clubReputation - rivalReputation + (isHome ? parameters.HomeAdvantage : -parameters.HomeAdvantage);
        var s = 1.0 / (1.0 + DetMath.Pow(10.0, -d / parameters.ReputationScale));
        return parameters.BasePoints + parameters.WinPoints * s;
    }

    /// <summary>Updates a value with an exponential moving average; alpha is in [0,1].</summary>
    public static double ExponentialMovingAverage(double previous, double observation, double alpha)
    {
        EnsureFinite(previous, nameof(previous));
        EnsureFinite(observation, nameof(observation));
        if (!IsFinite(alpha) || alpha < 0.0 || alpha > 1.0)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be finite and in [0, 1].");
        return previous + alpha * (observation - previous);
    }

    private static void EnsureFinite(double value, string parameterName)
    {
        if (!IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
