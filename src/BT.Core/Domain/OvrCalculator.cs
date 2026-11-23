using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Domain;

/// <summary>Calculates a player's stable overall rating for a tactical role.</summary>
public sealed class OvrCalculator
{
    private readonly RoleProfiles _profiles;
    private readonly PositionalFamiliarityParams _familiarity;

    public OvrCalculator(RoleProfiles profiles, PositionalFamiliarityParams familiarity)
    {
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _familiarity = familiarity ?? throw new ArgumentNullException(nameof(familiarity));
    }

    /// <summary>
    /// Calculates OVR from base attributes, then applies positional familiarity.
    /// Familiarity values and allowed bounds come from the versioned parameters.
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
        if (positionalFamiliarityPercent < _familiarity.Minimum || positionalFamiliarityPercent > _familiarity.Maximum)
            throw new ArgumentOutOfRangeException(nameof(positionalFamiliarityPercent),
                "Positional familiarity is outside the configured bounds.");
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
