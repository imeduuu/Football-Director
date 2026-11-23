using System;
using System.IO;
using BT.Core;
using Xunit;

namespace BT.Core.Tests;

public sealed class CompetitionRulesConfigTests
{
    [Theory]
    [InlineData("slice-league.v1.json", 10, 0, 0)]
    [InlineData("test-division-one.v1.json", 20, 0, 2)]
    [InlineData("test-division-two.v1.json", 20, 2, 0)]
    public void LoadsVersionedCompetitionRulePackages(string file, int teams, int promoted, int relegated)
    {
        var config=CompetitionRulesConfig.Load(Path.Combine(AppContext.BaseDirectory,"config","competitions",file));
        Assert.Equal(teams,config.Teams); Assert.Equal(3,config.Points.Win); Assert.Equal(1,config.Points.Draw); Assert.Equal(0,config.Points.Loss);
        Assert.Equal(promoted,config.Movement.PromotedDirect); Assert.Equal(relegated,config.Movement.RelegatedDirect);
        Assert.Equal(5,config.Discipline.YellowThreshold); Assert.Equal(2,config.Windows.Count);
    }

    [Fact]
    public void UsesConfiguredPointsAndRejectsInvalidPointOrder()
    {
        var json=File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"config","competitions","slice-league.v1.json"));
        Assert.Equal(4,CompetitionRulesConfig.LoadJson(json.Replace("\"win\": 3","\"win\": 4")).Points.Win);
        Assert.Throws<InvalidDataException>(()=>CompetitionRulesConfig.LoadJson(json.Replace("\"win\": 3","\"win\": 1")));
    }
}
