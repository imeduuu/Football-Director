using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BT.Core;
using BT.Core.Domain;
using BT.Core.Rng;
using Xunit;

namespace BT.Core.Tests;

public sealed class SquadGeneratorTests
{
    private readonly ParamsConfig _parameters = ParamsConfig.Load(
        Path.Combine(AppContext.BaseDirectory, "config", "params.json"));
    private readonly RoleProfiles _profiles = RoleProfiles.Load(
        Path.Combine(AppContext.BaseDirectory, "config", "roles.json"));

    [Theory]
    [InlineData(18, 2, 6, 6, 4)]
    [InlineData(19, 2, 6, 6, 5)]
    [InlineData(20, 3, 6, 6, 5)]
    [InlineData(21, 3, 6, 7, 5)]
    [InlineData(22, 3, 7, 7, 5)]
    [InlineData(25, 3, 7, 10, 5)]
    public void GeneratesConfiguredCompositionAgeAndFormationCoverage(int size, int goalkeepers,
        int defenders, int midfielders, int forwards)
    {
        var squad = CreateGenerator().Generate(size, "club-1", 1, 50.5, "Lumenia", SquadProfile.LowerHalf, 1234);
        var primary = squad.GroupBy(x => x.Positions[0]).ToDictionary(x => x.Key, x => x.Count());
        int Count(Position position) => primary.TryGetValue(position, out var count) ? count : 0;

        Assert.Equal(size, squad.Count);
        Assert.Equal(goalkeepers, Count(Position.Goalkeeper));
        Assert.Equal(defenders, Count(Position.LeftBack) + Count(Position.RightBack) + Count(Position.CentreBack));
        Assert.Equal(midfielders, Count(Position.CentralMidfielder) + Count(Position.LeftWinger) + Count(Position.RightWinger));
        Assert.Equal(forwards, Count(Position.Striker));
        Assert.All(squad, player => Assert.InRange(player.Age, 18, 34));
        Assert.Equal(26, squad.Average(player => player.Age));
        Assert.Equal(size, squad.Select(player => player.Id).Distinct(StringComparer.Ordinal).Count());

        // Both Slice formations need these positions; the supply counts let every slot use a distinct player.
        Assert.True(squad.Count(x => x.Positions.Contains(Position.Goalkeeper)) >= 1);
        Assert.True(squad.Count(x => x.Positions.Contains(Position.LeftBack)) >= 1);
        Assert.True(squad.Count(x => x.Positions.Contains(Position.RightBack)) >= 1);
        Assert.True(squad.Count(x => x.Positions.Contains(Position.CentreBack)) >= 2);
        Assert.True(squad.Count(x => x.Positions.Contains(Position.CentralMidfielder)) >= 3);
        Assert.True(squad.Count(x => x.Positions.Contains(Position.LeftWinger)) >= 1);
        Assert.True(squad.Count(x => x.Positions.Contains(Position.RightWinger)) >= 1);
        Assert.True(squad.Count(x => x.Positions.Contains(Position.Striker)) >= 2);
    }

    [Fact]
    public void SameSeedProducesTheSameSquad()
    {
        var generator = CreateGenerator();
        var first = generator.Generate(25, "club-1", 1, 50.5, "Lumenia", SquadProfile.LowerHalf, 9182, 7);
        var second = generator.Generate(25, "club-1", 1, 50.5, "Lumenia", SquadProfile.LowerHalf, 9182, 7);

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].Id, second[i].Id);
            Assert.Equal(first[i].Age, second[i].Age);
            Assert.Equal(first[i].Positions, second[i].Positions);
            Assert.Equal(first[i].VisibleAttributes.OrderBy(x => x.Key), second[i].VisibleAttributes.OrderBy(x => x.Key));
            Assert.Equal(first[i].HiddenAttributes.OrderBy(x => x.Key), second[i].HiddenAttributes.OrderBy(x => x.Key));
        }
    }

    [Theory]
    [InlineData(SquadProfile.Favorite)]
    [InlineData(SquadProfile.UpperHalf)]
    [InlineData(SquadProfile.LowerHalf)]
    [InlineData(SquadProfile.RelegationCandidate)]
    public void GeneratesSquadInsideProfileMeanAndBandCoverage(SquadProfile profile)
    {
        var generated = CreateGenerator().GenerateDetailed(25, "club-profile", 2, 50.5, "Lumenia", profile, 77321);
        var squad = generated.Players;
        var bands = _parameters.SquadGeneration.Profiles[profile];
        Assert.InRange(generated.MeanOvr, bands.MeanMinimum, bands.MeanMaximum);
        Assert.True(generated.StarterBandCoverage >= _parameters.SquadGeneration.MinimumBandCoverage);
        Assert.True(generated.BenchBandCoverage >= _parameters.SquadGeneration.MinimumBandCoverage);
        Assert.Equal(11, generated.StarterIds.Count);
        Assert.Contains(generated.Formation, new[] { "4-4-2", "4-3-3" });
        Assert.InRange(generated.Attempts, 1, _parameters.SquadGeneration.MaxAttempts);
        Assert.InRange(generated.TargetMeanOvr, bands.MeanMinimum, bands.MeanMaximum);
    }

    private static System.Collections.Generic.HashSet<int> Select442StarterIds(
        System.Collections.Generic.IReadOnlyList<Player> squad, IReadOnlyList<int> ovr)
    {
        var selected = new System.Collections.Generic.HashSet<int>();
        void Pick(Position position, int count)
        {
            foreach (var index in Enumerable.Range(0, squad.Count).Where(i => squad[i].Positions[0] == position)
                         .OrderByDescending(i => ovr[i]).ThenBy(i => i).Take(count)) selected.Add(index);
        }
        Pick(Position.Goalkeeper, 1); Pick(Position.LeftBack, 1); Pick(Position.RightBack, 1);
        Pick(Position.CentreBack, 2); Pick(Position.CentralMidfielder, 2); Pick(Position.LeftWinger, 1);
        Pick(Position.RightWinger, 1); Pick(Position.Striker, 2);
        return selected;
    }

    [Theory]
    [InlineData(17)]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(26)]
    public void RejectsUnsupportedSquadSizes(int size) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateGenerator().Generate(size, "club-1", 1, 50.5, "Lumenia", SquadProfile.LowerHalf, 1));

    private SquadGenerator CreateGenerator()
    {
        var ovrCalculator = new OvrCalculator(_profiles);
        return new SquadGenerator(new AttributeGenerator(_parameters.AttributeGeneration, ovrCalculator), ovrCalculator,
            _parameters.SquadGeneration);
    }
}
