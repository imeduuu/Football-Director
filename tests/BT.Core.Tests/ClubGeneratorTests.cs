using System.Collections.Generic;
using System.IO;
using System.Linq;
using BT.Core.Domain;
using Xunit;

namespace BT.Core.Tests;

public sealed class ClubGeneratorTests
{
    [Fact]
    public void GeneratesReproducibleClubsWithDistinctColorsAndManagerStubs()
    {
        var generator = CreateGenerator();
        var first = generator.Generate(4, "Lumenia", 1234, weeklyWageBills: new Dictionary<string, long>
        {
            ["CLUB-001"] = 1000, ["CLUB-002"] = 2000
        });
        var second = generator.Generate(4, "Lumenia", 1234, weeklyWageBills: new Dictionary<string, long>
        {
            ["CLUB-001"] = 1000, ["CLUB-002"] = 2000
        });

        Assert.Equal(first.Select(x => (x.Id, x.Name, x.Reputation, x.PrimaryColor, x.SecondaryColor, x.StadiumCapacity, x.TransferBudget, x.WeeklyWageBudget)),
            second.Select(x => (x.Id, x.Name, x.Reputation, x.PrimaryColor, x.SecondaryColor, x.StadiumCapacity, x.TransferBudget, x.WeeklyWageBudget)));
        Assert.Equal(4, first.Select(x => x.Name).Distinct().Count());
        Assert.Equal(8, first.SelectMany(x => new[] { x.PrimaryColor, x.SecondaryColor }).Distinct().Count());
        Assert.All(first, club =>
        {
            Assert.InRange(club.Reputation, 1, 100);
            Assert.InRange(club.StadiumCapacity, 5000, 85000);
            Assert.Single(club.Staff);
            Assert.Equal(StaffRole.Manager, club.Staff[0].Role);
            Assert.Equal($"{club.Id}-MGR-01", club.Staff[0].Id);
        });
        Assert.True(first[0].WeeklyWageBudget >= 1000);
        Assert.True(first[1].WeeklyWageBudget >= 2000);
    }

    [Fact]
    public void RejectsWageBillAboveConfiguredMaximum()
    {
        var generator = CreateGenerator();
        Assert.Throws<System.InvalidOperationException>(() => generator.Generate(1, "Lumenia", 1,
            weeklyWageBills: new Dictionary<string, long> { ["CLUB-001"] = 10000 }));
    }

    [Fact]
    public void ReferenceSliceUsesVersionedPoolAndBalancedProfiles()
    {
        var pool = ClubPool.Load(Path.Combine("data", "clubs", "lumenia.v1.json"));
        var profileParams = new ClubProfileGenerationParameters(1, 100, 0, 1000, 0, 1000);
        var profiles = new Dictionary<SquadProfile, ClubProfileGenerationParameters>
        {
            [SquadProfile.Favorite] = profileParams, [SquadProfile.UpperHalf] = profileParams,
            [SquadProfile.LowerHalf] = profileParams, [SquadProfile.RelegationCandidate] = profileParams
        };
        var clubs = new ClubGenerator(pool, new ClubGenerationParameters(5000, 85000, profiles))
            .Generate(10, "Lumenia", 42);

        Assert.Equal(2, clubs.Count(x => x.Profile == SquadProfile.Favorite));
        Assert.Equal(3, clubs.Count(x => x.Profile == SquadProfile.UpperHalf));
        Assert.Equal(3, clubs.Count(x => x.Profile == SquadProfile.LowerHalf));
        Assert.Equal(2, clubs.Count(x => x.Profile == SquadProfile.RelegationCandidate));
        Assert.All(clubs, club => Assert.Equal(ClubStatus.Active, club.Status));
    }

    private static ClubGenerator CreateGenerator()
    {
        var pool = ClubPool.LoadJson("""
        {"version":"test-1","names":["Aurora","Boreal","Cumbre","Delta"],"colorPairs":[
          {"primary":"#110001","secondary":"#220001"},{"primary":"#110002","secondary":"#220002"},
          {"primary":"#110003","secondary":"#220003"},{"primary":"#110004","secondary":"#220004"}]}
        """);
        var profileParams = new ClubProfileGenerationParameters(1, 100, 100, 500, 500, 3000);
        var allProfiles = new Dictionary<SquadProfile, ClubProfileGenerationParameters>
        {
            [SquadProfile.Favorite] = profileParams, [SquadProfile.UpperHalf] = profileParams,
            [SquadProfile.LowerHalf] = profileParams, [SquadProfile.RelegationCandidate] = profileParams
        };
        return new ClubGenerator(pool, new ClubGenerationParameters(5000, 85000, allProfiles));
    }
}
