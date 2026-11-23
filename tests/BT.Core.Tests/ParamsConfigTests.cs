using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using BT.Core;
using BT.Core.Domain;
using Xunit;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Tests;

public sealed class ParamsConfigTests
{
    private static string ParamsPath => Path.Combine(AppContext.BaseDirectory, "config", "params.json");

    [Fact]
    public void LoadsProductionParamsAndApprovedAttributeGenerationSettings()
    {
        var config = ParamsConfig.Load(ParamsPath);
        var generation = config.AttributeGeneration;

        Assert.Equal(1, generation.Minimum);
        Assert.Equal(99, generation.Maximum);
        Assert.Equal(7, generation.StandardDeviation);
        Assert.Equal(2, generation.ReferenceDivisionLevel);
        Assert.Equal(60, generation.ReferenceMean); // Provisional calibration fixture, not final balance.
        Assert.Equal(2, generation.DivisionMeanGap);
        Assert.Equal(1, generation.PositionScale);
        Assert.Equal(50.5, generation.Reputation.Neutral);
        Assert.Equal(30, generation.Reputation.Scale);
        Assert.Equal(1, generation.Reputation.MaximumShift);
        Assert.Equal(15, generation.PositionAffinities.Count);
        Assert.Equal(18, generation.Attributes.Count);
        Assert.Equal(75, config.Fitness.PreseasonTarget);
        Assert.Equal(60, config.BoardConfidence.Initial);
    }

    [Fact]
    public void AttributeFamilyMappingCoversEnumExactlyAndIsReadOnly()
    {
        Assert.Equal(Enum.GetValues(typeof(PlayerAttribute)).Cast<PlayerAttribute>().OrderBy(x => x).ToArray(),
            ParamsConfig.AttributeFamilies.Keys.OrderBy(x => x).ToArray());
        Assert.Equal(8, ParamsConfig.AttributeFamilies.Count(x => x.Value == AttributeFamily.Technical));
        Assert.Equal(6, ParamsConfig.AttributeFamilies.Count(x => x.Value == AttributeFamily.Mental));
        Assert.Equal(4, ParamsConfig.AttributeFamilies.Count(x => x.Value == AttributeFamily.Physical));
        Assert.Equal(AttributeFamily.Technical, ParamsConfig.AttributeFamilies[PlayerAttribute.Goalkeeping]);
        Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IDictionary<PlayerAttribute, AttributeFamily>)ParamsConfig.AttributeFamilies)
            .Add((PlayerAttribute)999, AttributeFamily.Technical));
    }

    [Fact]
    public void PositionalAffinitiesCenterToZeroUsingConfiguredOvrWeights()
    {
        var config = ParamsConfig.Load(ParamsPath);
        var rolesPath = Path.Combine(AppContext.BaseDirectory, "config", "roles.json");
        var profiles = RoleProfiles.Load(rolesPath);

        foreach (var affinity in config.AttributeGeneration.PositionAffinities)
        {
            var profile = profiles[affinity.Role];
            var weightedAffinity = profile.Weights.Sum(pair => pair.Value *
                affinity.Values[(int)pair.Key]);
            var centeredSum = profile.Weights.Sum(pair => pair.Value *
                (affinity.Values[(int)pair.Key] - weightedAffinity));
            Assert.InRange(Math.Abs(centeredSum), 0, 1e-12);
        }
    }

    [Fact]
    public void RejectsDuplicateJsonPropertiesBeforeDeserialization()
    {
        var json = File.ReadAllText(ParamsPath).Replace("\"version\": \"1.2.0\",", "\"version\": \"1.2.0\",\"version\": \"duplicate\",");
        var error = Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(json));
        Assert.Contains("duplicate property", error.Message);
    }

    [Theory]
    [InlineData("standardDeviation", -1)]
    [InlineData("referenceDivisionLevel", 0)]
    [InlineData("positionScale", 2.1)]
    public void RejectsInvalidAttributeGenerationScalars(string property, double value)
    {
        var node = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        node["attributeGeneration"]![property] = value;
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(node.ToJsonString()));
    }

    [Fact]
    public void RejectsIncompleteAttributeWeightMap()
    {
        var node = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        node["attributeGeneration"]!["reputation"]!["attributeWeights"]!.AsObject().Remove("Passing");
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(node.ToJsonString()));
    }

    [Fact]
    public void RejectsUnknownAttributeAndMissingOrDuplicatedRoles()
    {
        var unknownAttribute = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        unknownAttribute["attributeGeneration"]!["reputation"]!["attributeWeights"]!["Unknown"] = 0.5;
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(unknownAttribute.ToJsonString()));

        var duplicateRole = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        duplicateRole["attributeGeneration"]!["positionAffinities"]![1]!["role"] = "Goalkeeper";
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(duplicateRole.ToJsonString()));

        var missingRole = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        missingRole["attributeGeneration"]!["positionAffinities"]!.AsArray().RemoveAt(0);
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(missingRole.ToJsonString()));
    }

    [Fact]
    public void RejectsIncompleteAffinityVectorAndInvalidAgeCurve()
    {
        var shortVector = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        shortVector["attributeGeneration"]!["positionAffinities"]![0]!["values"]!.AsArray().RemoveAt(0);
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(shortVector.ToJsonString()));

        var invalidCurve = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        invalidCurve["attributeGeneration"]!["ageCurve"]!["declineStartAge"] = 20;
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(invalidCurve.ToJsonString()));
    }

    [Fact]
    public void RejectsInvalidReputationWeightsAgeAmplitudesAndAttributeCoverage()
    {
        var invalidReputation = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        invalidReputation["attributeGeneration"]!["reputation"]!["scale"] = 0;
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(invalidReputation.ToJsonString()));

        var invalidWeight = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        invalidWeight["attributeGeneration"]!["reputation"]!["attributeWeights"]!["Passing"] = 1.5;
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(invalidWeight.ToJsonString()));

        var invalidAmplitude = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        invalidAmplitude["attributeGeneration"]!["ageCurve"]!["growthAmplitudes"]!["technical"] = 2.1;
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(invalidAmplitude.ToJsonString()));

        var invalidAttributeSet = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        invalidAttributeSet["attributeGeneration"]!["attributes"]![0] = "Unknown";
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(invalidAttributeSet.ToJsonString()));

        var invalidAffinityValue = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        invalidAffinityValue["attributeGeneration"]!["positionAffinities"]![0]!["values"]![0] = 0.25;
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(invalidAffinityValue.ToJsonString()));
    }

    [Fact]
    public void RejectsNonFiniteNumbersAndDuplicateNestedKeys()
    {
        var nonFinite = File.ReadAllText(ParamsPath).Replace("\"standardDeviation\": 7", "\"standardDeviation\": 1e999");
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(nonFinite));

        var duplicateNested = File.ReadAllText(ParamsPath).Replace("\"neutral\": 50.5,", "\"neutral\": 50.5,\"neutral\": 51,");
        var error = Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(duplicateNested));
        Assert.Contains("duplicate property", error.Message);
    }

    [Fact]
    public void RejectsUnknownPropertiesAndSchemaIsValidJson()
    {
        var unknownProperty = JsonNode.Parse(File.ReadAllText(ParamsPath))!;
        unknownProperty["attributeGeneration"]!["notAParameter"] = 1;
        Assert.Throws<InvalidDataException>(() => ParamsConfig.LoadJson(unknownProperty.ToJsonString()));

        using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "config", "params.schema.json")));
        Assert.Equal(JsonValueKind.Object, schema.RootElement.ValueKind);
    }
}
