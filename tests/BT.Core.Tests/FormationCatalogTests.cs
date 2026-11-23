using System;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using BT.Core.Domain;
using Xunit;

namespace BT.Core.Tests;

public sealed class FormationCatalogTests
{
    private static string FormationsPath => Path.Combine(AppContext.BaseDirectory, "config", "formations.json");

    [Fact]
    public void LoadsTwelveValidFormationsAndActivatesOnlyTheSlicePair()
    {
        var catalog = FormationCatalog.Load(FormationsPath);

        Assert.Equal(12, catalog.Formations.Count);
        Assert.Equal(new[] { "4-3-3", "4-4-2" }, catalog.SliceFormations.Select(item => item.Id).OrderBy(id => id));
        Assert.All(catalog.Formations, formation =>
        {
            Assert.Equal(11, formation.Points.Count);
            Assert.Single(formation.Points, point => point.Position == Position.Goalkeeper);
            Assert.All(formation.Points, point =>
            {
                Assert.InRange(point.X, 0, 100);
                Assert.InRange(point.Y, 0, 100);
            });
        });
    }

    [Fact]
    public void RejectsMalformedFormationLineupsAndCoordinates()
    {
        var original = JsonNode.Parse(File.ReadAllText(FormationsPath))!;

        var missingGoalkeeper = original.DeepClone();
        missingGoalkeeper["formations"]![0]!["points"]![0]!["position"] = "Striker";
        Assert.Throws<InvalidDataException>(() => FormationCatalog.LoadJson(missingGoalkeeper.ToJsonString()));

        var badCoordinate = original.DeepClone();
        badCoordinate["formations"]![0]!["points"]![0]!["x"] = 101;
        Assert.Throws<InvalidDataException>(() => FormationCatalog.LoadJson(badCoordinate.ToJsonString()));

        var tooFewPlayers = original.DeepClone();
        tooFewPlayers["formations"]![0]!["points"]!.AsArray().RemoveAt(10);
        Assert.Throws<InvalidDataException>(() => FormationCatalog.LoadJson(tooFewPlayers.ToJsonString()));
    }
}
