using System;
using System.IO;
using System.Linq;
using BT.Core.Domain;
using Xunit;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Tests;

public sealed class RoleProfilesTests
{
    [Fact]
    public void LoadsAllFifteenExternalProfilesWithNormalizedKnownAttributes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config", "roles.json");
        var profiles = RoleProfiles.Load(path);

        Assert.Equal(15, profiles.Profiles.Count);
        Assert.Equal(15, profiles.Profiles.Select(profile => profile.Role).Distinct().Count());
        foreach (var profile in profiles.Profiles)
        {
            Assert.NotEmpty(profile.Weights);
            Assert.All(profile.Weights.Keys, attribute => Assert.True(Enum.IsDefined(typeof(PlayerAttribute), attribute)));
            Assert.InRange(Math.Abs(profile.Weights.Values.Sum() - 1.0), 0.0, RoleProfiles.WeightSumTolerance);
        }
    }

    [Fact]
    public void SliceProfilesRenormalizeRetainedAttributesAndPutRoundingRemainderOnLast()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "config", "roles.json");
        var profiles = RoleProfiles.Load(path);

        AssertWeights(profiles.GetSliceProfile(Role.Goalkeeper), (PlayerAttribute.Goalkeeping, 0.692), (PlayerAttribute.Positioning, 0.308));
        AssertWeights(profiles.GetSliceProfile(Role.CentreBackMarker), (PlayerAttribute.Tackling, 0.357), (PlayerAttribute.Marking, 0.357), (PlayerAttribute.Aerial, 0.286));
        AssertWeights(profiles.GetSliceProfile(Role.DefensiveFullBack), (PlayerAttribute.Tackling, 0.25), (PlayerAttribute.Marking, 0.25), (PlayerAttribute.Pace, 0.2), (PlayerAttribute.Stamina, 0.15), (PlayerAttribute.Positioning, 0.15));
        AssertWeights(profiles.GetSliceProfile(Role.DefensiveMidfielderPlaymaker), (PlayerAttribute.Passing, 0.4), (PlayerAttribute.Stamina, 0.267), (PlayerAttribute.Positioning, 0.2), (PlayerAttribute.Tackling, 0.133));
        AssertWeights(profiles.GetSliceProfile(Role.BoxToBoxMidfielder), (PlayerAttribute.Stamina, 0.294), (PlayerAttribute.Passing, 0.235), (PlayerAttribute.Tackling, 0.235), (PlayerAttribute.Positioning, 0.236));
        AssertWeights(profiles.GetSliceProfile(Role.Winger), (PlayerAttribute.Pace, 0.375), (PlayerAttribute.Dribbling, 0.375), (PlayerAttribute.Passing, 0.25));
        AssertWeights(profiles.GetSliceProfile(Role.AdvancedForward), (PlayerAttribute.Finishing, 0.35), (PlayerAttribute.Pace, 0.25), (PlayerAttribute.Positioning, 0.2), (PlayerAttribute.Stamina, 0.1), (PlayerAttribute.Dribbling, 0.1));

        foreach (var role in profiles.Profiles.Select(profile => profile.Role))
        {
            var slice = profiles.GetSliceProfile(role);
            Assert.InRange(Math.Abs(slice.Weights.Values.Sum() - 1.0), 0.0, RoleProfiles.WeightSumTolerance);
            Assert.All(slice.Weights.Keys, attribute => Assert.Contains(attribute, new[]
            {
                PlayerAttribute.Goalkeeping, PlayerAttribute.Tackling, PlayerAttribute.Marking,
                PlayerAttribute.Aerial, PlayerAttribute.Passing, PlayerAttribute.Dribbling,
                PlayerAttribute.Finishing, PlayerAttribute.Pace, PlayerAttribute.Stamina,
                PlayerAttribute.Positioning
            }));
        }
    }

    private static void AssertWeights(RoleProfile profile, params (PlayerAttribute Attribute, double Weight)[] expected)
    {
        Assert.Equal(expected.Length, profile.Weights.Count);
        foreach (var (attribute, weight) in expected)
            Assert.Equal(weight, profile.Weights[attribute], 3);
    }

    [Theory]
    [InlineData("{\"profiles\":[{\"role\":\"Goalkeeper\",\"weights\":{\"Goalkeeping\":0.9}}]}")]
    [InlineData("{\"profiles\":[{\"role\":\"Goalkeeper\",\"weights\":{\"UnknownAttribute\":1.0}}]}")]
    public void RejectsInvalidWeightVectorsAndUnknownAttributes(string json)
    {
        Assert.Throws<InvalidDataException>(() => RoleProfiles.LoadJson(json));
    }
}
