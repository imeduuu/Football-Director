using System.Collections.Generic;
using BT.Core.Collections;
using Xunit;

namespace BT.Core.Tests;

public class StableSortTests
{
    private sealed record Item(int Score, int Id, string Name);

    [Fact]
    public void By_UsesIdForTiesAndInputOrderForDuplicateIds()
    {
        var items = new[]
        {
            new Item(2, 9, "first"), new Item(1, 8, "low"),
            new Item(2, 3, "earlier"), new Item(2, 3, "later")
        };

        var sorted = StableSort.By(items, item => item.Score, item => item.Id);

        Assert.Equal(new[] { "low", "earlier", "later", "first" }, sorted.ConvertAll(item => item.Name));
    }

    [Fact]
    public void OrderedSet_EnumeratesDistinctItemsByComparer()
    {
        var ordered = new OrderedSet<int>(new[] { 3, 1, 2, 1 });

        Assert.Equal(new[] { 1, 2, 3 }, ordered);
    }

    [Fact]
    public void OrderedDictionary_EnumeratesEntriesByKey()
    {
        var ordered = new OrderedDictionary<string, int>(new[]
        {
            new KeyValuePair<string, int>("b", 2), new KeyValuePair<string, int>("a", 1)
        });

        Assert.Equal(new[] { "a", "b" }, ordered.Keys);
        Assert.Equal(new[] { 1, 2 }, ordered.Values);
    }
}
