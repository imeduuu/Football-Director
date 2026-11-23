using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BT.Core.Rng;

namespace BT.Core.Domain;

/// <summary>Deterministically creates clubs from explicit, versioned content and balance inputs.</summary>
public sealed class ClubGenerator
{
    private readonly ClubPool _pool;
    private readonly ClubGenerationParameters _parameters;

    public ClubGenerator(ClubPool pool, ClubGenerationParameters parameters)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public IReadOnlyList<Club> Generate(int count, string nationality, ulong seed, ulong sequence = 0,
        IReadOnlyDictionary<string, long>? weeklyWageBills = null)
    {
        if (count < 1 || count > _pool.Names.Count || count > _pool.ColorPairs.Count)
            throw new ArgumentOutOfRangeException(nameof(count), "The club pool must contain enough unique names and color pairs.");
        if (string.IsNullOrWhiteSpace(nationality)) throw new ArgumentException("A nationality is required.", nameof(nationality));
        if (weeklyWageBills is not null && weeklyWageBills.Values.Any(x => x < 0))
            throw new ArgumentException("Weekly wage bills cannot be negative.", nameof(weeklyWageBills));

        var names = _pool.Names.ToArray();
        var colors = _pool.ColorPairs.ToArray();
        var nameRng = new Pcg32(seed, sequence).Fork(1);
        var colorRng = new Pcg32(seed, sequence).Fork(2);
        Shuffle(names, ref nameRng);
        Shuffle(colors, ref colorRng);
        var result = new List<Club>(count);
        for (var i = 0; i < count; i++)
        {
            var id = $"CLUB-{i + 1:000}";
            var profile = ProfileForOrdinal(i);
            var profileParams = _parameters.Profiles[profile];
            var rng = new Pcg32(seed, sequence).Fork((ulong)(100 + i));
            var reputation = rng.NextInt(profileParams.ReputationMinimum, checked(profileParams.ReputationMaximum + 1));
            var transferBudget = NextInt64(ref rng, profileParams.TransferBudgetMinimum, checked(profileParams.TransferBudgetMaximum + 1));
            var wageBill = weeklyWageBills is not null && weeklyWageBills.TryGetValue(id, out var bill) ? bill : 0;
            var weeklyBudgetMinimum = profileParams.WeeklyWageBudgetMinimum > wageBill
                ? profileParams.WeeklyWageBudgetMinimum : wageBill;
            if (weeklyBudgetMinimum > profileParams.WeeklyWageBudgetMaximum)
                throw new InvalidOperationException($"Club '{id}' wage bill exceeds its configured weekly budget maximum.");
            var weeklyBudget = NextInt64(ref rng, weeklyBudgetMinimum, checked(profileParams.WeeklyWageBudgetMaximum + 1));
            var capacity = rng.NextInt(_parameters.StadiumCapacityMinimum, checked(_parameters.StadiumCapacityMaximum + 1));
            var staff = new[] { new Staff($"{id}-MGR-01", $"Manager {names[i]}", StaffRole.Manager) };
            result.Add(new Club(id, names[i], nationality, profile, reputation, colors[i].Primary,
                colors[i].Secondary, capacity, transferBudget, weeklyBudget, staff));
        }
        return result.AsReadOnly();
    }

    private static void Shuffle<T>(T[] values, ref Pcg32 rng)
    {
        for (var i = values.Length - 1; i > 0; i--)
        {
            var j = rng.NextInt(0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private SquadProfile ProfileForOrdinal(int ordinal)
    {
        // The ten-club reference slice has the GDD's 2/3/3/2 profile split;
        // repeating it preserves the required 20- and 40-club proportions.
        var slice = new[] { SquadProfile.Favorite, SquadProfile.UpperHalf, SquadProfile.LowerHalf,
            SquadProfile.RelegationCandidate, SquadProfile.UpperHalf, SquadProfile.LowerHalf,
            SquadProfile.Favorite, SquadProfile.UpperHalf, SquadProfile.LowerHalf, SquadProfile.RelegationCandidate };
        var profile = slice[ordinal % slice.Length];
        if (!_parameters.Profiles.ContainsKey(profile)) throw new ArgumentException($"Missing club-generation parameters for profile '{profile}'.");
        return profile;
    }

    private static long NextInt64(ref Pcg32 rng, long minimumInclusive, long maximumExclusive)
    {
        if (minimumInclusive < 0 || minimumInclusive >= maximumExclusive || maximumExclusive > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(maximumExclusive), "Budget bounds must be non-negative, ordered and fit the supported game currency range.");
        return rng.NextInt((int)minimumInclusive, (int)maximumExclusive);
    }
}

public sealed class ClubPool
{
    private ClubPool(string version, string[] names, ClubColorPair[] colors)
    { Version = version; Names = Array.AsReadOnly(names); ColorPairs = Array.AsReadOnly(colors); }
    public string Version { get; }
    public IReadOnlyList<string> Names { get; }
    public IReadOnlyList<ClubColorPair> ColorPairs { get; }

    public static ClubPool Load(string path) => LoadJson(File.ReadAllText(path));
    public static ClubPool LoadJson(string json)
    {
        var dto = JsonSerializer.Deserialize<PoolDto>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
        });
        if (dto is null || string.IsNullOrWhiteSpace(dto.Version) || dto.Names is not { Count: > 0 } || dto.ColorPairs is not { Count: > 0 })
            throw new InvalidDataException("A club pool requires a version, names and color pairs.");
        if (dto.Names.Any(string.IsNullOrWhiteSpace) || dto.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != dto.Names.Count)
            throw new InvalidDataException("Club names must be non-empty and unique.");
        var colors = dto.ColorPairs.Select(x => new ClubColorPair(x.Primary ?? "", x.Secondary ?? "")).ToArray();
        if (colors.SelectMany(x => new[] { x.Primary, x.Secondary }).Distinct(StringComparer.OrdinalIgnoreCase).Count() != colors.Length * 2)
            throw new InvalidDataException("Every club color must be distinct across the pool.");
        return new ClubPool(dto.Version, dto.Names.ToArray(), colors);
    }

    private sealed class PoolDto { public string? Version { get; set; } public List<string>? Names { get; set; } public List<ColorDto>? ColorPairs { get; set; } }
    private sealed class ColorDto { public string? Primary { get; set; } public string? Secondary { get; set; } }
}

public sealed class ClubColorPair
{
    public ClubColorPair(string primary, string secondary)
    {
        // Club performs canonical RGB validation and rejects identical colors.
        Primary = Club.ValidateColor(primary, nameof(primary));
        Secondary = Club.ValidateColor(secondary, nameof(secondary));
        if (string.Equals(Primary, Secondary, StringComparison.Ordinal))
            throw new ArgumentException("Club colors must be distinct.", nameof(secondary));
    }
    public string Primary { get; }
    public string Secondary { get; }
}

public sealed class ClubGenerationParameters
{
    public ClubGenerationParameters(int stadiumCapacityMinimum, int stadiumCapacityMaximum,
        IReadOnlyDictionary<SquadProfile, ClubProfileGenerationParameters> profiles)
    {
        if (stadiumCapacityMinimum < 5000 || stadiumCapacityMaximum > 85000 || stadiumCapacityMinimum > stadiumCapacityMaximum)
            throw new ArgumentOutOfRangeException(nameof(stadiumCapacityMinimum));
        if (profiles is null || Enum.GetValues(typeof(SquadProfile)).Cast<SquadProfile>().Any(x => !profiles.ContainsKey(x)))
            throw new ArgumentException("Generation parameters must define every squad profile.");
        StadiumCapacityMinimum = stadiumCapacityMinimum; StadiumCapacityMaximum = stadiumCapacityMaximum;
        Profiles = profiles;
    }
    public int StadiumCapacityMinimum { get; }
    public int StadiumCapacityMaximum { get; }
    public IReadOnlyDictionary<SquadProfile, ClubProfileGenerationParameters> Profiles { get; }
}

public sealed class ClubProfileGenerationParameters
{
    public ClubProfileGenerationParameters(int reputationMinimum, int reputationMaximum,
        long transferBudgetMinimum, long transferBudgetMaximum, long weeklyWageBudgetMinimum, long weeklyWageBudgetMaximum)
    {
        if (reputationMinimum < 1 || reputationMaximum > 100 || reputationMinimum > reputationMaximum)
            throw new ArgumentOutOfRangeException(nameof(reputationMinimum));
        if (transferBudgetMinimum < 0 || transferBudgetMaximum < transferBudgetMinimum || weeklyWageBudgetMinimum < 0 || weeklyWageBudgetMaximum < weeklyWageBudgetMinimum)
            throw new ArgumentOutOfRangeException(nameof(transferBudgetMinimum));
        ReputationMinimum = reputationMinimum; ReputationMaximum = reputationMaximum;
        TransferBudgetMinimum = transferBudgetMinimum; TransferBudgetMaximum = transferBudgetMaximum;
        WeeklyWageBudgetMinimum = weeklyWageBudgetMinimum; WeeklyWageBudgetMaximum = weeklyWageBudgetMaximum;
    }
    public int ReputationMinimum { get; }
    public int ReputationMaximum { get; }
    public long TransferBudgetMinimum { get; }
    public long TransferBudgetMaximum { get; }
    public long WeeklyWageBudgetMinimum { get; }
    public long WeeklyWageBudgetMaximum { get; }
}

