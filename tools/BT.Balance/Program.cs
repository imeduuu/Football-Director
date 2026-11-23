using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BT.Core;
using BT.Core.Domain;
using BT.Core.Rng;
using PlayerAttribute = BT.Core.Domain.Attribute;

return Run(args);

static int Run(string[] args)
{
    try
    {
        if (args.Length == 0 || args[0] != "worldgen") return Usage();
        var options = ParseOptions(args.Skip(1).ToArray());
        if (!options.TryGetValue("seed", out var seedText) || !ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out var seed) ||
            !options.TryGetValue("clubs", out var clubsText) || !int.TryParse(clubsText, NumberStyles.None, CultureInfo.InvariantCulture, out var clubs) || !new[] { 10, 20, 40 }.Contains(clubs) ||
            !options.TryGetValue("export-csv", out var output)) return Usage();

        var root = FindRoot();
        var paramsPath = Path.Combine(root, "config", "params.json");
        var paramsJson = File.ReadAllText(paramsPath);
        var config = JsonDocument.Parse(paramsJson).RootElement.GetProperty("worldGeneration");
        var parameters = ParamsConfig.LoadJson(paramsJson);
        var generatorVersion = config.GetProperty("generatorVersion").GetString()!;
        var size = config.GetProperty("squadSize").GetInt32();
        var country = config.GetProperty("country").GetString()!;
        var nationality = config.GetProperty("nationality").GetString()!;
        var clubGeneration = ReadClubParameters(config.GetProperty("clubGeneration"));
        var contractConfig = config.GetProperty("contracts");
        var concentrationLimit = config.GetProperty("concentrationWarningThreshold").GetDouble();
        var clubPool = ClubPool.Load(Path.Combine(root, "data", "clubs", "lumenia.v1.json"));
        var namePool = NamePool.Load(Path.Combine(root, "data", "names", "lumenia.v1.json"));
        var nameGenerator = new NameGenerator(namePool);
        var nameRegistry = new CompetitionNameRegistry();
        var clubsGenerator = new ClubGenerator(clubPool, clubGeneration);
        var world = new WorldGenerator(seed, generatorVersion);
        var squadGenerator = new SquadGenerator(new AttributeGenerator(parameters.AttributeGeneration,
            new OvrCalculator(RoleProfiles.Load(Path.Combine(root, "config", "roles.json")), parameters.PositionalFamiliarity)),
            new OvrCalculator(RoleProfiles.Load(Path.Combine(root, "config", "roles.json")), parameters.PositionalFamiliarity), parameters.SquadGeneration);

        var clubSeeds = Enumerable.Range(0, clubs).Select(i => world.ClubSubseed($"CLUB-{i + 1:000}", "club" )).ToArray();
        var generatedClubs = clubsGenerator.Generate(clubs, country, clubSeeds[0]);
        var playerRows = new List<PlayerRow>();
        var squadReports = new List<object>();
        var wageBills = new Dictionary<string, long>(StringComparer.Ordinal);
        var roles = RoleProfiles.Load(Path.Combine(root, "config", "roles.json"));
        var calculator = new OvrCalculator(roles, parameters.PositionalFamiliarity);

        foreach (var club in generatedClubs.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var ordinal = int.Parse(club.Id.AsSpan(5), CultureInfo.InvariantCulture) - 1;
            var division = ordinal < clubs / 2 ? 1 : 2;
            var squadSeed = world.ClubSubseed(club.Id, "squad");
            var squad = squadGenerator.GenerateDetailed(size, club.Id, division, club.Reputation, nationality,
                club.Profile, squadSeed);
            var contractRng = new Pcg32(world.ClubSubseed(club.Id, "contracts"), 0);
            foreach (var player in squad.Players.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                var role = RoleFor(player.Positions[0]);
                var ovr = calculator.Calculate(player, role, 100);
                var hidden = player.HiddenAttributes.ToDictionary(x => x.Key, x => x.Value);
                var duration = contractRng.NextInt(contractConfig.GetProperty("durationMinimum").GetInt32(), contractConfig.GetProperty("durationMaximum").GetInt32() + 1);
                var wage = contractRng.NextInt(contractConfig.GetProperty("weeklyWageMinimum").GetInt32(), contractConfig.GetProperty("weeklyWageMaximum").GetInt32() + 1);
                var contract = new Contract(wage, duration, ContractRole.RegularStarter,
                    contractRng.NextInt(0, contractConfig.GetProperty("signingBonusMaximum").GetInt32() + 1),
                    contractRng.NextInt(0, contractConfig.GetProperty("goalBonusMaximum").GetInt32() + 1),
                    contractRng.NextInt(0, contractConfig.GetProperty("cleanSheetBonusMaximum").GetInt32() + 1));
                wageBills[club.Id] = wageBills.GetValueOrDefault(club.Id) + contract.WeeklyWage;
                var nameRng = new Pcg32(world.PlayerSubseed(player.Id, "name"), 0);
                var playerName = nameGenerator.Generate("LUM", "classic", nameRegistry, ref nameRng);
                playerRows.Add(new PlayerRow(player, playerName, ovr, hidden[HiddenAttribute.Potential], club.Id, contract));
            }
            squadReports.Add(new
            {
                clubId = club.Id, profile = club.Profile.ToString(), players = squad.Players.Count,
                meanAge = squad.Players.Average(p => p.Age), meanOvr = squad.MeanOvr,
                formation = squad.Formation, starterBandCoverage = squad.StarterBandCoverage,
                benchBandCoverage = squad.BenchBandCoverage,
                positions = PositionSummary(squad.Players),
                payroll = wageBills.GetValueOrDefault(club.Id),
                highTalent = squad.Players.Count(p => calculator.Calculate(p, RoleFor(p.Positions[0]), 100) >= 85)
            });
        }

        // The selected club profile budgets are generated after player contracts, so regenerate with wage bills.
        generatedClubs = clubsGenerator.Generate(clubs, country, clubSeeds[0], weeklyWageBills: wageBills);
        var errors = Validate(generatedClubs, playerRows, size, parameters, squadReports);
        var highTalentRows = playerRows.Where(p => p.Ovr >= 85).ToArray();
        var concentration = highTalentRows.Length == 0 ? 0 : highTalentRows.GroupBy(p => p.ClubId, StringComparer.Ordinal)
            .Sum(g => Math.Pow(g.Count() / (double)highTalentRows.Length, 2));
        var warnings = concentration > concentrationLimit
            ? new[] { $"High-talent concentration {concentration.ToString("F4", CultureInfo.InvariantCulture)} exceeds configured threshold {concentrationLimit.ToString("F4", CultureInfo.InvariantCulture)}." }
            : Array.Empty<string>();
        var status = errors.Count == 0 ? "PASS" : "FAIL";

        Directory.CreateDirectory(output);
        WriteCsv(Path.Combine(output, "clubs.csv"), ClubsCsv(generatedClubs, clubs));
        WriteCsv(Path.Combine(output, "players.csv"), PlayersCsv(playerRows));
        WriteCsv(Path.Combine(output, "staff.csv"), StaffCsv(generatedClubs));
        var report = new
        {
            status, errors, warnings,
            metadata = new { seed = world.SeedMundo, generatorVersion, paramsVersion = parameters.Version,
                paramsSha256 = Hash(paramsJson), clubPoolVersion = clubPool.Version, namePoolVersion = namePool.Version, clubs, squadSize = size },
            clubs = squadReports,
            summary = new { players = playerRows.Count, highTalent = highTalentRows.Length, concentration,
                positions = PositionSummary(playerRows.Select(p => p.Player)),
                byDivision = playerRows.GroupBy(p => int.Parse(p.ClubId.AsSpan(5), CultureInfo.InvariantCulture) <= clubs / 2 ? 1 : 2)
                    .OrderBy(g => g.Key).Select(g => new { division = g.Key, players = g.Count(), meanOvr = g.Average(p => p.Ovr), highTalent = g.Count(p => p.Ovr >= 85) }) }
        };
        File.WriteAllText(Path.Combine(output, "world-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
        Console.WriteLine($"{status}: exported {clubs} clubs and {playerRows.Count} players to {Path.GetFullPath(output)}");
        foreach (var issue in errors) Console.Error.WriteLine("ERROR: " + issue);
        foreach (var warning in warnings) Console.Error.WriteLine("WARNING: " + warning);
        return errors.Count == 0 ? 0 : 1;
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidDataException or IOException or InvalidOperationException or JsonException or OverflowException)
    {
        Console.Error.WriteLine(exception.Message);
        return 2;
    }
}

static int Usage()
{
    Console.Error.WriteLine("Usage: BT.Balance worldgen --seed <uint64> --clubs <10|20|40> --export-csv <directory>");
    return 2;
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i += 2)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length || !result.TryAdd(args[i][2..], args[i + 1]))
            throw new ArgumentException("Expected unique --option value arguments.");
    }
    if (result.Keys.Any(k => k is not ("seed" or "clubs" or "export-csv"))) throw new ArgumentException("Unknown worldgen option.");
    return result;
}

static string FindRoot()
{
    for (var path = new DirectoryInfo(Directory.GetCurrentDirectory()); path is not null; path = path.Parent)
        if (File.Exists(Path.Combine(path.FullName, "BoardroomTouchline.sln"))) return path.FullName;
    throw new DirectoryNotFoundException("Run BT.Balance from the repository directory or one of its descendants.");
}

static ClubGenerationParameters ReadClubParameters(JsonElement element)
{
    var profiles = new Dictionary<SquadProfile, ClubProfileGenerationParameters>();
    foreach (var item in element.GetProperty("profiles").EnumerateArray())
    {
        var profile = Enum.Parse<SquadProfile>(item.GetProperty("name").GetString()!, false);
        profiles.Add(profile, new ClubProfileGenerationParameters(item.GetProperty("reputationMinimum").GetInt32(),
            item.GetProperty("reputationMaximum").GetInt32(), item.GetProperty("transferBudgetMinimum").GetInt64(),
            item.GetProperty("transferBudgetMaximum").GetInt64(), item.GetProperty("weeklyWageBudgetMinimum").GetInt64(),
            item.GetProperty("weeklyWageBudgetMaximum").GetInt64()));
    }
    return new ClubGenerationParameters(element.GetProperty("stadiumCapacityMinimum").GetInt32(),
        element.GetProperty("stadiumCapacityMaximum").GetInt32(), profiles);
}

static List<string> Validate(IReadOnlyList<Club> clubs, IReadOnlyList<PlayerRow> players, int size, ParamsConfig parameters, IReadOnlyList<object> reports)
{
    var errors = new List<string>();
    if (clubs.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != clubs.Count) errors.Add("Duplicate club IDs.");
    foreach (var club in clubs)
    {
        var squad = players.Where(p => p.ClubId == club.Id).ToArray();
        if (squad.Length != size) errors.Add($"{club.Id} has {squad.Length} players; expected {size}.");
        foreach (var position in new[] { Position.Goalkeeper, Position.LeftBack, Position.RightBack, Position.CentreBack, Position.CentralMidfielder, Position.DefensiveMidfielder, Position.LeftWinger, Position.RightWinger, Position.Striker })
            if (!squad.Any(p => p.Player.Positions.Contains(position))) errors.Add($"{club.Id} has no player covering {position}.");
        foreach (var row in squad)
        {
            if (row.Player.VisibleAttributes.Values.Any(v => v is < 1 or > 99) || row.Player.HiddenAttributes.Values.Any(v => v is < 1 or > 99)) errors.Add($"{row.Player.Id} has an attribute outside [1,99].");
            if (row.Contract.DurationYears is < 1 or > 5 || row.Contract.WeeklyWage < 0 || row.Contract.SigningBonus < 0 || row.Contract.GoalBonus < 0 || row.Contract.CleanSheetBonus < 0) errors.Add($"{row.Player.Id} has impossible contract terms.");
        }
        var payroll = squad.Sum(p => p.Contract.WeeklyWage);
        if (payroll > club.WeeklyWageBudget) errors.Add($"{club.Id} payroll exceeds its weekly wage budget.");
    }
    return errors;
}

static Dictionary<string, int> PositionSummary(IEnumerable<Player> players) => Enum.GetValues<Position>().OrderBy(p => p)
    .ToDictionary(p => p.ToString(), p => players.Count(x => x.Positions.Contains(p)), StringComparer.Ordinal);

static Role RoleFor(Position position) => position switch
{
    Position.Goalkeeper => Role.Goalkeeper, Position.CentreBack => Role.CentreBackMarker,
    Position.LeftBack or Position.RightBack => Role.DefensiveFullBack,
    Position.DefensiveMidfielder => Role.DefensiveMidfielderDestroyer,
    Position.CentralMidfielder => Role.BoxToBoxMidfielder, Position.AttackingMidfielder => Role.AttackingMidfielderPlaymaker,
    Position.LeftWinger or Position.RightWinger => Role.Winger, Position.Striker => Role.AdvancedForward,
    _ => throw new ArgumentOutOfRangeException(nameof(position))
};

static string ClubsCsv(IReadOnlyList<Club> clubs, int total)
{
    var rows = new List<string> { "id,name,division,profile,status,country,reputation,primaryColor,secondaryColor,stadiumCapacity,transferBudget,weeklyWageBudget" };
    rows.AddRange(clubs.OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => Csv(c.Id, c.Name, ((int.Parse(c.Id.AsSpan(5), CultureInfo.InvariantCulture) - 1) < total / 2 ? "1" : "2"), c.Profile.ToString(), c.Status.ToString(), c.Nationality, I(c.Reputation), c.PrimaryColor, c.SecondaryColor, I(c.StadiumCapacity), I(c.TransferBudget), I(c.WeeklyWageBudget))));
    return string.Join("\n", rows) + "\n";
}

static string PlayersCsv(IReadOnlyList<PlayerRow> players)
{
    var attrs = Enum.GetValues<PlayerAttribute>().Select(a => a.ToString()).ToArray();
    var hidden = Enum.GetValues<HiddenAttribute>().Where(a => a != HiddenAttribute.Potential).Select(a => a.ToString()).ToArray();
    var header = new[] { "id", "clubId", "name", "nationality", "age", "positions", "role" }.Concat(attrs).Concat(hidden).Concat(new[] { "ovr", "potential", "weeklyWage", "durationYears", "promisedRole", "signingBonus", "goalBonus", "cleanSheetBonus", "releaseClause" });
    var rows = new List<string> { string.Join(',', header) };
    rows.AddRange(players.OrderBy(p => p.Player.Id, StringComparer.Ordinal).Select(p => Csv(new[] { p.Player.Id, p.ClubId, p.Name, p.Player.Nationality, I(p.Player.Age), string.Join(';', p.Player.Positions), RoleFor(p.Player.Positions[0]).ToString() }
        .Concat(attrs.Select(a => I(p.Player.VisibleAttributes[Enum.Parse<PlayerAttribute>(a)])))
        .Concat(hidden.Select(a => I(p.Player.HiddenAttributes[Enum.Parse<HiddenAttribute>(a)])))
        .Concat(new[] { I(p.Ovr), I(p.Potential), I(p.Contract.WeeklyWage), I(p.Contract.DurationYears), p.Contract.PromisedRole.ToString(), I(p.Contract.SigningBonus), I(p.Contract.GoalBonus), I(p.Contract.CleanSheetBonus), p.Contract.ReleaseClause is null ? "" : I(p.Contract.ReleaseClause.Value) }).ToArray())));
    return string.Join("\n", rows) + "\n";
}

static string StaffCsv(IReadOnlyList<Club> clubs) => "id,clubId,name,role\n" + string.Join("\n", clubs.OrderBy(c => c.Id, StringComparer.Ordinal).SelectMany(c => c.Staff.OrderBy(s => s.Id, StringComparer.Ordinal).Select(s => Csv(s.Id, c.Id, s.Name, s.Role.ToString())))) + "\n";
static string Csv(params string[] fields) => string.Join(',', fields.Select(v => "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""));
static string I(long value) => value.ToString(CultureInfo.InvariantCulture);
static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
static void WriteCsv(string path, string content) => File.WriteAllText(path, content, new UTF8Encoding(false));

sealed record PlayerRow(Player Player, string Name, int Ovr, int Potential, string ClubId, Contract Contract);
