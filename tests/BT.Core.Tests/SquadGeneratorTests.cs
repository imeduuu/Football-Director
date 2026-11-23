using System;
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
        var squad = CreateGenerator().Generate(size, "club-1", 1, 50.5, "Lumenia", 1234);
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
        var first = generator.Generate(25, "club-1", 1, 50.5, "Lumenia", 9182, 7);
        var second = generator.Generate(25, "club-1", 1, 50.5, "Lumenia", 9182, 7);

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
    [InlineData(17)]
    [InlineData(23)]
    [InlineData(24)]
    [InlineData(26)]
    public void RejectsUnsupportedSquadSizes(int size) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateGenerator().Generate(size, "club-1", 1, 50.5, "Lumenia", 1));

    private SquadGenerator CreateGenerator() => new(new AttributeGenerator(_parameters.AttributeGeneration,
        new OvrCalculator(_profiles)));
}
