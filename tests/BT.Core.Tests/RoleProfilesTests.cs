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

    [Theory]
    [InlineData("{\"profiles\":[{\"role\":\"Goalkeeper\",\"weights\":{\"Goalkeeping\":0.9}}]}")]
    [InlineData("{\"profiles\":[{\"role\":\"Goalkeeper\",\"weights\":{\"UnknownAttribute\":1.0}}]}")]
    public void RejectsInvalidWeightVectorsAndUnknownAttributes(string json)
    {
        Assert.Throws<InvalidDataException>(() => RoleProfiles.LoadJson(json));
    }
}
