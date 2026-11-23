using System;
using System.IO;
using System.Linq;
using BT.Core.Domain;
using BT.Core.Rng;
using Xunit;

namespace BT.Core.Tests;

public sealed class NameGeneratorTests
{
    private static NameGenerator CreateGenerator()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "names", "lumenia.v1.json");
        return new NameGenerator(NamePool.Load(path));
    }

    [Fact]
    public void GeneratesRepeatableNamesAndKeepsCompetitionNamesUnique()
    {
        var firstGenerator = CreateGenerator();
        var secondGenerator = CreateGenerator();
        var firstCompetition = new CompetitionNameRegistry();
        var secondCompetition = new CompetitionNameRegistry();
        var firstRng = new Pcg32(2026, 31);
        var secondRng = new Pcg32(2026, 31);
        var firstNames = Enumerable.Range(0, 200).Select(_ => firstGenerator.Generate("LUM", "modern", firstCompetition, ref firstRng)).ToArray();
        var secondNames = Enumerable.Range(0, 200).Select(_ => secondGenerator.Generate("LUM", "modern", secondCompetition, ref secondRng)).ToArray();

        Assert.Equal(firstNames, secondNames);
        Assert.Equal(firstNames.Length, firstNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(200, firstCompetition.Count);
    }

    [Fact]
    public void SeparateCompetitionsCanReuseNamesAndUnsupportedErasFail()
    {
        var generator = CreateGenerator();
        var rng = new Pcg32(8, 3);
        var first = generator.Generate("LUM", "classic", new CompetitionNameRegistry(), ref rng);
        var other = generator.Generate("LUM", "classic", new CompetitionNameRegistry(), ref rng);
        Assert.NotNull(first);
        Assert.NotNull(other);
        Assert.Throws<ArgumentException>(() => generator.Generate("LUM", "future", new CompetitionNameRegistry(), ref rng));
    }
}
