using System;
using System.IO;
using System.Linq;
using BT.Core;
using BT.Core.Domain;
using BT.Core.Rng;
using Xunit;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Tests;

public sealed class AttributeGeneratorTests
{
    private readonly ParamsConfig _parameters = ParamsConfig.Load(Path.Combine(AppContext.BaseDirectory, "config", "params.json"));
    private readonly RoleProfiles _profiles = RoleProfiles.Load(Path.Combine(AppContext.BaseDirectory, "config", "roles.json"));

    [Fact]
    public void GeneratesCompleteBoundedAttributesDeterministicallyAndCalculatesOvrFromRoleWeights()
    {
        var calculator = new OvrCalculator(_profiles);
        var generator = new AttributeGenerator(_parameters.AttributeGeneration, calculator);
        var firstRng = new Pcg32(20_200, 91);
        var secondRng = new Pcg32(20_200, 91);

        var first = generator.Generate(1, 75, 24, Role.CentreBackMarker, ref firstRng);
        var second = generator.Generate(1, 75, 24, Role.CentreBackMarker, ref secondRng);

        Assert.Equal(first.Values.OrderBy(pair => pair.Key), second.Values.OrderBy(pair => pair.Key));
        Assert.Equal(Enum.GetValues<PlayerAttribute>().Length, first.Values.Count);
        Assert.All(first.Values.Values, value => Assert.InRange(value, 1, 99));
        Assert.Equal(calculator.CalculateAttributes(first.Values, Role.CentreBackMarker, 100), first.Ovr);
    }

    [Fact]
    public void HigherDivisionLevelAndReputationShiftTheConfiguredMeanInExpectedDirections()
    {
        var generator = new AttributeGenerator(_parameters.AttributeGeneration, new OvrCalculator(_profiles));
        var lowDivisionTotal = 0.0;
        var highDivisionTotal = 0.0;
        var lowRepTotal = 0.0;
        var highRepTotal = 0.0;
        const int sampleCount = 500;

        for (var i = 0; i < sampleCount; i++)
        {
            var a = new Pcg32((ulong)i + 1, 13);
            var b = new Pcg32((ulong)i + 1, 13);
            var c = new Pcg32((ulong)i + 10_000, 13);
            var d = new Pcg32((ulong)i + 10_000, 13);
            lowDivisionTotal += generator.Generate(1, 50.5, 24, Role.CentreBackMarker, ref a).Values.Values.Average();
            highDivisionTotal += generator.Generate(2, 50.5, 24, Role.CentreBackMarker, ref b).Values.Values.Average();
            lowRepTotal += generator.Generate(1, 1, 24, Role.CentreBackMarker, ref c).Values.Values.Average();
            highRepTotal += generator.Generate(1, 100, 24, Role.CentreBackMarker, ref d).Values.Values.Average();
        }

        Assert.True(lowDivisionTotal > highDivisionTotal);
        Assert.True(highRepTotal > lowRepTotal);
    }

    [Fact]
    public void RejectsInvalidGenerationInputs()
    {
        var generator = new AttributeGenerator(_parameters.AttributeGeneration, new OvrCalculator(_profiles));
        var rng = new Pcg32(1, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(0, 50, 24, Role.Goalkeeper, ref rng));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(1, 101, 24, Role.Goalkeeper, ref rng));
        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Generate(1, 50, -1, Role.Goalkeeper, ref rng));
    }
}
