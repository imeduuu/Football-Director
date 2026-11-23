using System;
using System.Collections.Generic;
using System.IO;
using BT.Core.Domain;
using Xunit;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Tests;

public sealed class OvrCalculatorTests
{
    private readonly OvrCalculator _calculator;

    public OvrCalculatorTests()
    {
        var profiles = RoleProfiles.Load(Path.Combine(AppContext.BaseDirectory, "config", "roles.json"));
        _calculator = new OvrCalculator(profiles);
    }

    [Fact]
    public void CalculatesGddCentreBackMarkerExampleFromBaseAttributes()
    {
        var player = CreatePlayer(new Dictionary<PlayerAttribute, int>
        {
            [PlayerAttribute.Tackling] = 80,
            [PlayerAttribute.Marking] = 70,
            [PlayerAttribute.Aerial] = 90,
            [PlayerAttribute.Strength] = 60,
            [PlayerAttribute.Concentration] = 50
        });

        // 0.25*80 + 0.25*70 + 0.20*90 + 0.20*60 + 0.10*50 = 72.5.
        Assert.Equal(73, _calculator.Calculate(player, Role.CentreBackMarker, 100));
    }

    [Fact]
    public void RoundsWeightedResultAtHalfPointUpToNextInteger()
    {
        var player = CreatePlayer(new Dictionary<PlayerAttribute, int>
        {
            [PlayerAttribute.Tackling] = 74,
            [PlayerAttribute.Marking] = 72,
            [PlayerAttribute.Aerial] = 72,
            [PlayerAttribute.Strength] = 72,
            [PlayerAttribute.Concentration] = 72
        });

        Assert.Equal(73, _calculator.Calculate(player, Role.CentreBackMarker, 100));
    }

    [Theory]
    [InlineData(100, 80)]
    [InlineData(92, 74)]
    [InlineData(85, 68)]
    public void AppliesSpecifiedPositionalFamiliarityMultipliers(int familiarity, int expected)
    {
        Assert.Equal(expected, _calculator.Calculate(CreateUniformPlayer(80), Role.CentreBackMarker, familiarity));
    }

    [Fact]
    public void UnknownPositionAtInitialSeventyPercentFamiliarityAppliesThirtyPercentPenalty()
    {
        Assert.Equal(56, _calculator.Calculate(CreateUniformPlayer(80), Role.CentreBackMarker, 70));
    }

    [Fact]
    public void AttributesOutsideTheRoleProfileDoNotAffectOvr()
    {
        var baseline = CreateUniformPlayer(80);
        var varied = CreateUniformPlayer(80);
        var attributes = new Dictionary<PlayerAttribute, int>(varied.VisibleAttributes);
        foreach (var attribute in Enum.GetValues<PlayerAttribute>())
            if (attribute is not (PlayerAttribute.Tackling or PlayerAttribute.Marking or PlayerAttribute.Aerial or PlayerAttribute.Strength or PlayerAttribute.Concentration))
                attributes[attribute] = 1;

        varied = CreatePlayer(attributes);
        Assert.Equal(_calculator.Calculate(baseline, Role.CentreBackMarker, 100),
            _calculator.Calculate(varied, Role.CentreBackMarker, 100));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(99, 99)]
    public void HandlesAttributeScaleExtremes(int value, int expected)
    {
        Assert.Equal(expected, _calculator.Calculate(CreateUniformPlayer(value), Role.CentreBackMarker, 100));
    }

    [Theory]
    [InlineData(69)]
    [InlineData(101)]
    public void RejectsFamiliarityOutsideSupportedRange(int familiarity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _calculator.Calculate(CreateUniformPlayer(80), Role.CentreBackMarker, familiarity));
    }

    [Fact]
    public void RejectsNullPlayer()
    {
        Assert.Throws<ArgumentNullException>(() => _calculator.Calculate(null!, Role.CentreBackMarker, 100));
    }

    private static Player CreateUniformPlayer(int value)
    {
        var attributes = new Dictionary<PlayerAttribute, int>();
        foreach (var attribute in Enum.GetValues<PlayerAttribute>()) attributes.Add(attribute, value);
        return CreatePlayer(attributes);
    }

    private static Player CreatePlayer(IDictionary<PlayerAttribute, int> overrides)
    {
        var attributes = new Dictionary<PlayerAttribute, int>();
        foreach (var attribute in Enum.GetValues<PlayerAttribute>()) attributes.Add(attribute, 50);
        foreach (var (attribute, value) in overrides) attributes[attribute] = value;
        var hidden = new Dictionary<HiddenAttribute, int>();
        foreach (var attribute in Enum.GetValues<HiddenAttribute>()) hidden.Add(attribute, 50);
        return new Player("ovr-test", attributes, hidden, 24, "Lumenian",
            new[] { Position.CentreBack }, null);
    }
}
