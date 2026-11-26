using System;
using System.Collections.Generic;
using BT.Core.Domain;

namespace BT.Core.Sim;

/// <summary>One player's effective strength and team-relative location for zonal aggregation.</summary>
public sealed class ZonalStrengthContribution
{
    public ZonalStrengthContribution(Position position, double x, double y, double strength)
    {
        Position = Enum.IsDefined(typeof(Position), position)
            ? position
            : throw new ArgumentOutOfRangeException(nameof(position));
        ValidateCoordinate(x, nameof(x));
        ValidateCoordinate(y, nameof(y));
        if (double.IsNaN(strength) || double.IsInfinity(strength) || strength < 0.0)
            throw new ArgumentOutOfRangeException(nameof(strength), "Strength must be finite and non-negative.");

        X = x;
        Y = y;
        Strength = strength;
    }

    public Position Position { get; }
    public double X { get; }
    public double Y { get; }
    public double Strength { get; }

    private static void ValidateCoordinate(double value, string parameterName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 || value > 100.0)
            throw new ArgumentOutOfRangeException(parameterName, "Pitch coordinates must be finite and in [0, 100].");
    }
}

/// <summary>Strength totals in the nine pitch zones, ordered defence-to-attack and left-to-right.</summary>
public sealed class ZoneStrengthGrid
{
    private readonly IReadOnlyList<double> _cells;

    internal ZoneStrengthGrid(IReadOnlyList<double> cells)
    {
        if (cells is null) throw new ArgumentNullException(nameof(cells));
        if (cells.Count != 9) throw new ArgumentException("A zone strength grid must contain nine cells.", nameof(cells));
        var snapshot = new double[cells.Count];
        for (var index = 0; index < cells.Count; index++)
        {
            var value = cells[index];
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
                throw new ArgumentOutOfRangeException(nameof(cells), "Zone strengths must be finite and non-negative.");
            snapshot[index] = value;
        }
        _cells = Array.AsReadOnly(snapshot);
    }

    public IReadOnlyList<double> Cells => _cells;

    public double this[MatchZone zone]
    {
        get
        {
            if (!Enum.IsDefined(typeof(MatchZone), zone)) throw new ArgumentOutOfRangeException(nameof(zone));
            return _cells[(int)zone];
        }
    }
}
