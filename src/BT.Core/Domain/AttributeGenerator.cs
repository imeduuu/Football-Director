using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using BT.Core.Rng;

namespace BT.Core.Domain;

/// <summary>Generates visible attributes and their role OVR from configured player-generation parameters.</summary>
public sealed class AttributeGenerator
{
    private readonly AttributeGenerationParams _parameters;
    private readonly OvrCalculator _ovrCalculator;

    public AttributeGenerator(AttributeGenerationParams parameters, OvrCalculator ovrCalculator)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _ovrCalculator = ovrCalculator ?? throw new ArgumentNullException(nameof(ovrCalculator));
    }

    public GeneratedAttributes Generate(int divisionLevel, double clubReputation, int age, Role role,
        ref Pcg32 rng, int positionalFamiliarityPercent = 100)
    {
        if (divisionLevel < 1) throw new ArgumentOutOfRangeException(nameof(divisionLevel));
        if (!double.IsFinite(clubReputation) || clubReputation < _parameters.Reputation.Minimum ||
            clubReputation > _parameters.Reputation.Maximum)
            throw new ArgumentOutOfRangeException(nameof(clubReputation));
        if (age < 0) throw new ArgumentOutOfRangeException(nameof(age));

        var affinity = FindAffinity(role);
        var values = new Dictionary<Attribute, int>(_parameters.Attributes.Count);
        var baseMean = _parameters.ReferenceMean -
            (divisionLevel - _parameters.ReferenceDivisionLevel) * _parameters.DivisionMeanGap;
        var reputationDelta = Clamp((clubReputation - _parameters.Reputation.Neutral) /
            _parameters.Reputation.Scale, -_parameters.Reputation.MaximumShift, _parameters.Reputation.MaximumShift);

        for (var i = 0; i < _parameters.Attributes.Count; i++)
        {
            var attribute = _parameters.Attributes[i];
            var repWeight = _parameters.Reputation.AttributeWeights[attribute];
            var mean = baseMean + reputationDelta * repWeight + affinity.Values[i] * _parameters.PositionScale + AgeAdjustment(age, ParamsConfig.AttributeFamilies[attribute]);
            var sample = RandomDistributions.TruncatedNormal(ref rng, mean, _parameters.StandardDeviation,
                _parameters.Minimum, _parameters.Maximum);
            values.Add(attribute, Clamp((int)(sample + 0.5), _parameters.Minimum, _parameters.Maximum));
        }

        var readOnlyValues = new ReadOnlyDictionary<Attribute, int>(values);
        return new GeneratedAttributes(readOnlyValues,
            _ovrCalculator.CalculateAttributes(readOnlyValues, role, positionalFamiliarityPercent));
    }

    private PositionAffinityProfile FindAffinity(Role role)
    {
        foreach (var profile in _parameters.PositionAffinities)
            if (profile.Role == role) return profile;
        throw new ArgumentOutOfRangeException(nameof(role));
    }

    private double AgeAdjustment(int age, AttributeFamily family)
    {
        var curve = _parameters.AgeCurve;
        var growthAmplitude = GetAmplitude(curve.GrowthAmplitudes, family);
        var declineAmplitude = GetAmplitude(curve.DeclineAmplitudes, family);
        if (age < curve.GrowthEndAge)
        {
            var progress = age <= curve.GrowthStartAge ? 0.0 :
                (double)(age - curve.GrowthStartAge) / (curve.GrowthEndAge - curve.GrowthStartAge);
            return -growthAmplitude * (1.0 - progress);
        }
        if (age > curve.DeclineStartAge)
        {
            var progress = (double)(age - curve.DeclineStartAge) /
                (curve.DeclineEndAge - curve.DeclineStartAge);
            if (progress > 1.0) progress = 1.0;
            return -declineAmplitude * progress;
        }
        return 0.0;
    }

    private static double GetAmplitude(AttributeFamilyAmplitudes amplitudes, AttributeFamily family) => family switch
    {
        AttributeFamily.Technical => amplitudes.Technical,
        AttributeFamily.Mental => amplitudes.Mental,
        AttributeFamily.Physical => amplitudes.Physical,
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static double Clamp(double value, double minimum, double maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    private static int Clamp(int value, int minimum, int maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;
}

/// <summary>Generated visible attributes and the OVR calculated for the requested role.</summary>
public sealed class GeneratedAttributes
{
    internal GeneratedAttributes(IReadOnlyDictionary<Attribute, int> values, int ovr)
    {
        Values = values;
        Ovr = ovr;
    }

    public IReadOnlyDictionary<Attribute, int> Values { get; }
    public int Ovr { get; }
}
