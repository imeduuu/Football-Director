using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BT.Core.Rng;

namespace BT.Core.Domain;

/// <summary>Versioned fictional name pools and deterministic country/era sampling.</summary>
public sealed class NamePool
{
    private NamePool(string version, Dictionary<string, Dictionary<string, NameEntry[]>> givenNames,
        Dictionary<string, NameEntry[]> surnames)
    { Version = version; GivenNames = givenNames; Surnames = surnames; }

    public string Version { get; }
    internal Dictionary<string, Dictionary<string, NameEntry[]>> GivenNames { get; }
    internal Dictionary<string, NameEntry[]> Surnames { get; }

    public static NamePool Load(string path) => LoadJson(File.ReadAllText(path));

    public static NamePool LoadJson(string json)
    {
        var dto = JsonSerializer.Deserialize<PoolDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
        });
        if (dto is null || string.IsNullOrWhiteSpace(dto.Version) || dto.Countries is null || dto.Countries.Count == 0)
            throw new InvalidDataException("A name pool requires a version and at least one country.");
        var given = new Dictionary<string, Dictionary<string, NameEntry[]>>(StringComparer.Ordinal);
        var surnames = new Dictionary<string, NameEntry[]>(StringComparer.Ordinal);
        foreach (var country in dto.Countries.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(country.Key) || country.Value?.Eras is null || country.Value.Surnames is null)
                throw new InvalidDataException("Each country requires era-specific given names and surnames.");
            var eras = new Dictionary<string, NameEntry[]>(StringComparer.Ordinal);
            foreach (var era in country.Value.Eras.OrderBy(x => x.Key, StringComparer.Ordinal))
                eras.Add(era.Key, ValidateEntries(era.Value, $"{country.Key}/{era.Key}"));
            given.Add(country.Key, eras);
            surnames.Add(country.Key, ValidateEntries(country.Value.Surnames, country.Key));
        }
        return new NamePool(dto.Version, given, surnames);
    }

    private static NameEntry[] ValidateEntries(List<EntryDto>? entries, string label)
    {
        if (entries is null || entries.Count == 0) throw new InvalidDataException($"Name pool '{label}' is empty.");
        var result = entries.Select(x =>
        {
            if (string.IsNullOrWhiteSpace(x.Name) || !double.IsFinite(x.Weight) || x.Weight <= 0)
                throw new InvalidDataException($"Name pool '{label}' contains an invalid entry.");
            return new NameEntry(x.Name, x.Weight);
        }).ToArray();
        if (result.Select(x => x.Name).Distinct(StringComparer.Ordinal).Count() != result.Length)
            throw new InvalidDataException($"Name pool '{label}' contains duplicate entries.");
        return result;
    }

    private sealed class PoolDto { public string? Version { get; set; } public Dictionary<string, CountryDto>? Countries { get; set; } }
    private sealed class CountryDto { public Dictionary<string, List<EntryDto>>? Eras { get; set; } public List<EntryDto>? Surnames { get; set; } }
    private sealed class EntryDto { public string? Name { get; set; } public double Weight { get; set; } }
}

internal sealed class NameEntry
{
    public NameEntry(string name, double weight) { Name = name; Weight = weight; }
    public string Name { get; }
    public double Weight { get; }
}

/// <summary>Tracks names already assigned in a single competition.</summary>
public sealed class CompetitionNameRegistry
{
    private readonly HashSet<string> _used = new(StringComparer.OrdinalIgnoreCase);
    public int Count => _used.Count;
    internal bool Contains(string fullName) => _used.Contains(fullName);
    internal void Add(string fullName) => _used.Add(fullName);
}

/// <summary>Generates weighted fictional full names with competition-scoped uniqueness.</summary>
public sealed class NameGenerator
{
    private readonly NamePool _pool;
    public NameGenerator(NamePool pool) => _pool = pool ?? throw new ArgumentNullException(nameof(pool));

    public string Generate(string country, string era, CompetitionNameRegistry competition, ref Pcg32 rng)
    {
        if (competition is null) throw new ArgumentNullException(nameof(competition));
        if (!_pool.GivenNames.TryGetValue(country, out var eras) || !eras.TryGetValue(era, out var given) || !_pool.Surnames.TryGetValue(country, out var surnames))
            throw new ArgumentException($"No name pool exists for country '{country}' and era '{era}'.");

        var available = new List<(string FullName, double Weight)>();
        foreach (var first in given)
            foreach (var last in surnames)
            {
                var fullName = first.Name + " " + last.Name;
                if (!competition.Contains(fullName)) available.Add((fullName, first.Weight * last.Weight));
            }
        if (available.Count == 0) throw new InvalidOperationException("The competition has exhausted all unique names in this pool.");
        var index = RandomDistributions.WeightedChoice(ref rng, available.Select(x => x.Weight).ToArray());
        var result = available[index].FullName;
        competition.Add(result);
        return result;
    }
}
