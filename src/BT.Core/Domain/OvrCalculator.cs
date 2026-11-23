using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Domain;

/// <summary>Calculates a player's stable overall rating for a tactical role.</summary>
public sealed class OvrCalculator
{
    private readonly RoleProfiles _profiles;

    public OvrCalculator(RoleProfiles profiles)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
    }

    /// <summary>
    /// Calculates OVR from base attributes, then applies positional familiarity.
    /// Use 100 for the primary position, 92 or 85 for secondary positions, and
    /// a value from 70 to 100 for a trained unfamiliar position.
    /// </summary>
    public int Calculate(Player player, Role role, int positionalFamiliarityPercent)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));
        return CalculateAttributes(player.VisibleAttributes, role, positionalFamiliarityPercent);
    }

    /// <summary>Calculates OVR from a complete set of base attributes.</summary>
    public int CalculateAttributes(IReadOnlyDictionary<Attribute, int> attributes, Role role, int positionalFamiliarityPercent)
    {
        if (attributes is null) throw new ArgumentNullException(nameof(attributes));
        if (positionalFamiliarityPercent is < 70 or > 100)
            throw new ArgumentOutOfRangeException(nameof(positionalFamiliarityPercent),
                "Positional familiarity must be between 70 and 100 percent.");
        if (attributes.Count != Enum.GetValues(typeof(Attribute)).Length)
            throw new ArgumentException("A complete set of visible attributes is required.", nameof(attributes));

        var weightedBase = 0.0;
        foreach (var (attribute, weight) in _profiles[role].Weights.OrderBy(pair => pair.Key))
        {
            if (!attributes.TryGetValue(attribute, out var value) || value is < 1 or > 99)
                throw new ArgumentException($"Attribute '{attribute}' must have a value in [1,99].", nameof(attributes));
            weightedBase += value * weight;
        }

        var familiarOvr = weightedBase * positionalFamiliarityPercent / 100.0;
        // OVR is positive (attributes are in [1,99]); adding 0.5 gives
        // midpoint-away-from-zero rounding without platform math APIs.
        return checked((int)(familiarOvr + 0.5));
    }
}
