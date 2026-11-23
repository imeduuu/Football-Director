using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using BT.Core.Domain;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core;

/// <summary>Typed, validated balance parameters loaded from a versioned JSON document.</summary>
public sealed class ParamsConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    private ParamsConfig(string version, FitnessParams fitness, MoraleParams morale,
        IReadOnlyList<AgeRecoveryFactor> ageRecoveryFactors, BoardConfidenceParams boardConfidence,
        AttributeGenerationParams attributeGeneration, SquadGenerationParams squadGeneration)
    {
        Version = version;
        Fitness = fitness;
        Morale = morale;
        AgeRecoveryFactors = ageRecoveryFactors;
        BoardConfidence = boardConfidence;
        AttributeGeneration = attributeGeneration;
        SquadGeneration = squadGeneration;
    }

    public string Version { get; }
    public FitnessParams Fitness { get; }
    public MoraleParams Morale { get; }
    public IReadOnlyList<AgeRecoveryFactor> AgeRecoveryFactors { get; }
    public BoardConfidenceParams BoardConfidence { get; }
    public AttributeGenerationParams AttributeGeneration { get; }
    public SquadGenerationParams SquadGeneration { get; }

    public static IReadOnlyDictionary<PlayerAttribute, AttributeFamily> AttributeFamilies { get; } =
        new ReadOnlyDictionary<PlayerAttribute, AttributeFamily>(new Dictionary<PlayerAttribute, AttributeFamily>
        {
            [PlayerAttribute.Passing] = AttributeFamily.Technical,
            [PlayerAttribute.Dribbling] = AttributeFamily.Technical,
            [PlayerAttribute.Finishing] = AttributeFamily.Technical,
            [PlayerAttribute.LongShots] = AttributeFamily.Technical,
            [PlayerAttribute.Tackling] = AttributeFamily.Technical,
            [PlayerAttribute.Marking] = AttributeFamily.Technical,
            [PlayerAttribute.Aerial] = AttributeFamily.Technical,
            [PlayerAttribute.Goalkeeping] = AttributeFamily.Technical,
            [PlayerAttribute.Vision] = AttributeFamily.Mental,
            [PlayerAttribute.Positioning] = AttributeFamily.Mental,
            [PlayerAttribute.Concentration] = AttributeFamily.Mental,
            [PlayerAttribute.Aggression] = AttributeFamily.Mental,
            [PlayerAttribute.Leadership] = AttributeFamily.Mental,
            [PlayerAttribute.Composure] = AttributeFamily.Mental,
            [PlayerAttribute.Pace] = AttributeFamily.Physical,
            [PlayerAttribute.Stamina] = AttributeFamily.Physical,
            [PlayerAttribute.Strength] = AttributeFamily.Physical,
            [PlayerAttribute.Agility] = AttributeFamily.Physical
        });

    public static ParamsConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
        return LoadJson(File.ReadAllText(path));
    }

    public static ParamsConfig LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON content is required.", nameof(json));
        Dto dto;
        SquadGenerationParams squadGeneration;
        try
        {
            using var document = JsonDocument.Parse(json);
            RejectDuplicateProperties(document.RootElement, "$");
            EnsureRequiredProperties(document.RootElement, "version", "fitness", "morale", "ageRecoveryFactors", "boardConfidence", "attributeGeneration", "squadGeneration");
            var root = document.RootElement;
            EnsureRequiredProperties(root.GetProperty("fitness"), "minimum", "maximum", "preseasonTarget");
            EnsureRequiredProperties(root.GetProperty("morale"), "minimum", "maximum");
            foreach (var band in root.GetProperty("ageRecoveryFactors").EnumerateArray())
                EnsureRequiredProperties(band, "maximumAge", "factor");
            EnsureRequiredProperties(root.GetProperty("boardConfidence"), "minimum", "maximum", "initial",
                "leagueWeight", "cupWeight", "friendlyWeight", "matchChangeMinimum", "matchChangeMaximum");
            var generation = root.GetProperty("attributeGeneration");
            var squad = root.GetProperty("squadGeneration");
            EnsureRequiredProperties(squad, "minimumBandCoverage", "maxAttempts", "reputationMeanShift", "divisionMeanShift", "referenceReputation", "referenceDivisionLevel", "referenceOvr", "profiles");
            foreach (var profile in squad.GetProperty("profiles").EnumerateArray())
                EnsureRequiredProperties(profile, "name", "meanMinimum", "meanMaximum", "starterMinimum", "starterMaximum", "benchMinimum", "benchMaximum");
            squadGeneration = ParseSquadGeneration(squad);
            EnsureRequiredProperties(generation, "minimum", "maximum", "standardDeviation", "referenceDivisionLevel", "referenceMean",
                "divisionMeanGap", "reputation", "ageCurve", "positionScale", "attributes", "positionAffinities");
            EnsureRequiredProperties(generation.GetProperty("reputation"), "minimum", "maximum", "neutral", "scale", "maximumShift", "attributeWeights");
            EnsureRequiredProperties(generation.GetProperty("ageCurve"), "growthStartAge", "growthEndAge", "declineStartAge", "declineEndAge",
                "growthAmplitudes", "declineAmplitudes");
            EnsureRequiredProperties(generation.GetProperty("ageCurve").GetProperty("growthAmplitudes"), "technical", "mental", "physical");
            EnsureRequiredProperties(generation.GetProperty("ageCurve").GetProperty("declineAmplitudes"), "technical", "mental", "physical");
            foreach (var profile in generation.GetProperty("positionAffinities").EnumerateArray())
                EnsureRequiredProperties(profile, "role", "values");
            dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions)
                ?? throw new InvalidDataException("The parameter document is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The parameter document is not valid JSON or has an invalid shape.", exception);
        }

        Validate(dto);
        var generationDto = dto.AttributeGeneration!;
        var generationParams = new AttributeGenerationParams(generationDto.Minimum, generationDto.Maximum,
            generationDto.StandardDeviation, generationDto.ReferenceDivisionLevel, generationDto.ReferenceMean,
            generationDto.DivisionMeanGap,
            new ReputationGenerationParams(generationDto.Reputation!.Minimum, generationDto.Reputation.Maximum,
                generationDto.Reputation.Neutral, generationDto.Reputation.Scale, generationDto.Reputation.MaximumShift,
                ToAttributeMap(generationDto.Reputation.AttributeWeights!)),
            new AttributeAgeCurveParams(generationDto.AgeCurve!.GrowthStartAge, generationDto.AgeCurve.GrowthEndAge,
                generationDto.AgeCurve.DeclineStartAge, generationDto.AgeCurve.DeclineEndAge,
                ToAmplitudes(generationDto.AgeCurve.GrowthAmplitudes!), ToAmplitudes(generationDto.AgeCurve.DeclineAmplitudes!)),
            generationDto.PositionScale,
            Array.AsReadOnly(generationDto.Attributes!.Select(ParseAttribute).ToArray()),
            Array.AsReadOnly(generationDto.PositionAffinities!.Select(profile =>
                new PositionAffinityProfile(ParseRole(profile.Role!), Array.AsReadOnly(profile.Values!.ToArray()))).ToArray()));

        return new ParamsConfig(dto.Version!,
            new FitnessParams(dto.Fitness!.Minimum, dto.Fitness.Maximum, dto.Fitness.PreseasonTarget),
            new MoraleParams(dto.Morale!.Minimum, dto.Morale.Maximum),
            Array.AsReadOnly(dto.AgeRecoveryFactors!.Select(x => new AgeRecoveryFactor(x.MaximumAge, x.Factor)).ToArray()),
            new BoardConfidenceParams(dto.BoardConfidence!.Minimum, dto.BoardConfidence.Maximum,
                dto.BoardConfidence.Initial, dto.BoardConfidence.LeagueWeight, dto.BoardConfidence.CupWeight,
                dto.BoardConfidence.FriendlyWeight, dto.BoardConfidence.MatchChangeMinimum,
                dto.BoardConfidence.MatchChangeMaximum), generationParams, squadGeneration);
    }

    private static SquadGenerationParams ParseSquadGeneration(JsonElement element)
    {
        var profiles = new Dictionary<SquadProfile, SquadProfileBands>();
        foreach (var item in element.GetProperty("profiles").EnumerateArray())
        {
            var name = item.GetProperty("name").GetString();
            var bands = new SquadProfileBands(item.GetProperty("meanMinimum").GetDouble(),
                item.GetProperty("meanMaximum").GetDouble(), item.GetProperty("starterMinimum").GetInt32(),
                item.GetProperty("starterMaximum").GetInt32(), item.GetProperty("benchMinimum").GetInt32(),
                item.GetProperty("benchMaximum").GetInt32());
            if (!double.IsFinite(bands.MeanMinimum) || !double.IsFinite(bands.MeanMaximum) ||
                bands.MeanMinimum < 1 || bands.MeanMaximum > 99 || bands.MeanMinimum > bands.MeanMaximum ||
                bands.StarterMinimum < 1 || bands.StarterMaximum > 99 || bands.StarterMinimum > bands.StarterMaximum ||
                bands.BenchMinimum < 1 || bands.BenchMaximum > 99 || bands.BenchMinimum > bands.BenchMaximum)
                throw new InvalidDataException($"Invalid parameter configuration: invalid OVR bands for squad profile '{name}'.");
            if (!Enum.TryParse(name, false, out SquadProfile profile) || !Enum.IsDefined(typeof(SquadProfile), profile) ||
                !profiles.TryAdd(profile, bands))
                throw new InvalidDataException($"Invalid parameter configuration: unknown or duplicate squad profile '{name}'.");
        }
        if (profiles.Count != Enum.GetValues(typeof(SquadProfile)).Length)
            throw new InvalidDataException("Invalid parameter configuration: squadGeneration.profiles must define every profile.");
        var coverage = element.GetProperty("minimumBandCoverage").GetDouble();
        var attempts = element.GetProperty("maxAttempts").GetInt32();
        var reputationShift = element.GetProperty("reputationMeanShift").GetDouble();
        var divisionShift = element.GetProperty("divisionMeanShift").GetDouble();
        var referenceReputation = element.GetProperty("referenceReputation").GetDouble();
        var referenceDivision = element.GetProperty("referenceDivisionLevel").GetInt32();
        var referenceOvr = element.GetProperty("referenceOvr").GetDouble();
        if (!double.IsFinite(coverage) || coverage is <= 0 or > 1 || attempts < 1 ||
            !double.IsFinite(reputationShift) || reputationShift < 0 || !double.IsFinite(divisionShift) || divisionShift < 0 ||
            !double.IsFinite(referenceReputation) || referenceReputation is < 1 or > 100 || referenceDivision < 1 ||
            !double.IsFinite(referenceOvr) || referenceOvr is < 1 or > 99)
            throw new InvalidDataException("Invalid parameter configuration: squad generation tuning values are invalid.");
        return new SquadGenerationParams(coverage, attempts, reputationShift, divisionShift,
            referenceReputation, referenceDivision, referenceOvr, profiles);
    }

    private static void Validate(Dto dto)
    {
        Require(!string.IsNullOrWhiteSpace(dto.Version), "version must be a non-empty string.");
        Require(dto.Fitness is not null, "fitness is required.");
        Require(dto.Morale is not null, "morale is required.");
        Require(dto.AgeRecoveryFactors is { Count: > 0 }, "ageRecoveryFactors must not be empty.");
        Require(dto.BoardConfidence is not null, "boardConfidence is required.");
        Require(dto.AttributeGeneration is not null, "attributeGeneration is required.");

        var fitness = dto.Fitness!;
        var morale = dto.Morale!;
        var confidence = dto.BoardConfidence!;
        Require(fitness.Minimum <= fitness.Maximum, "fitness.minimum must not exceed fitness.maximum.");
        Require(InRange(fitness.PreseasonTarget, fitness.Minimum, fitness.Maximum), "fitness.preseasonTarget must be within its range.");
        Require(morale.Minimum <= morale.Maximum, "morale.minimum must not exceed morale.maximum.");

        int? previousAge = null;
        var hasOpenEndedBand = false;
        foreach (var band in dto.AgeRecoveryFactors!)
        {
            Require(band.Factor > 0 && double.IsFinite(band.Factor), "age recovery factors must be finite and positive.");
            if (band.MaximumAge is null)
            {
                Require(!hasOpenEndedBand, "only one open-ended age band is allowed.");
                hasOpenEndedBand = true;
            }
            else
            {
                Require(!hasOpenEndedBand && (!previousAge.HasValue || band.MaximumAge > previousAge),
                    "age recovery bands must have strictly increasing maximumAge values, with any open-ended band last.");
                previousAge = band.MaximumAge;
            }
        }
        Require(dto.AgeRecoveryFactors!.Last().MaximumAge is null, "the final age recovery band must be open-ended.");

        Require(confidence.Minimum <= confidence.Maximum, "boardConfidence.minimum must not exceed its maximum.");
        Require(InRange(confidence.Initial, confidence.Minimum, confidence.Maximum), "boardConfidence.initial must be within its range.");
        Require(confidence.LeagueWeight >= 0 && double.IsFinite(confidence.LeagueWeight), "leagueWeight must be finite and non-negative.");
        Require(confidence.CupWeight >= 0 && double.IsFinite(confidence.CupWeight), "cupWeight must be finite and non-negative.");
        Require(confidence.FriendlyWeight >= 0 && double.IsFinite(confidence.FriendlyWeight), "friendlyWeight must be finite and non-negative.");
        Require(double.IsFinite(confidence.MatchChangeMinimum) && double.IsFinite(confidence.MatchChangeMaximum) &&
            confidence.MatchChangeMinimum <= confidence.MatchChangeMaximum, "board confidence match change bounds are invalid.");

        ValidateAttributeGeneration(dto.AttributeGeneration!);
    }

    private static void ValidateAttributeGeneration(AttributeGenerationDto generation)
    {
        Require(generation.Minimum == 1 && generation.Maximum == 99, "attributeGeneration bounds must be [1,99].");
        Require(generation.StandardDeviation > 0 && double.IsFinite(generation.StandardDeviation), "standardDeviation must be finite and positive.");
        Require(generation.ReferenceDivisionLevel >= 1, "referenceDivisionLevel must be at least 1.");
        Require(InRange(generation.ReferenceMean, generation.Minimum, generation.Maximum), "referenceMean must be finite and within attribute bounds.");
        Require(generation.DivisionMeanGap > 0 && double.IsFinite(generation.DivisionMeanGap), "divisionMeanGap must be finite and positive.");
        Require(generation.PositionScale >= 0 && generation.PositionScale <= 2 && double.IsFinite(generation.PositionScale), "positionScale must be finite and in [0,2].");

        var expectedAttributes = Enum.GetValues(typeof(PlayerAttribute)).Cast<PlayerAttribute>().OrderBy(x => x).ToArray();
        Require(generation.Attributes is { Count: > 0 } && generation.Attributes.Count == expectedAttributes.Length,
            "attributeGeneration.attributes must list every PlayerAttribute exactly once.");
        var attributes = generation.Attributes!.Select(ParseAttribute).ToArray();
        Require(attributes.Distinct().Count() == attributes.Length && attributes.SequenceEqual(expectedAttributes),
            "attributeGeneration.attributes must match the PlayerAttribute enum order exactly.");

        var reputation = generation.Reputation ?? throw new InvalidDataException("Invalid parameter configuration: reputation is required.");
        Require(reputation.Minimum == 1 && reputation.Maximum == 100 && reputation.Minimum <= reputation.Maximum,
            "reputation bounds must be [1,100].");
        Require(InRange(reputation.Neutral, reputation.Minimum, reputation.Maximum), "reputation.neutral must be within its bounds.");
        Require(reputation.Scale > 0 && double.IsFinite(reputation.Scale), "reputation.scale must be finite and positive.");
        Require(reputation.MaximumShift >= 0 && double.IsFinite(reputation.MaximumShift), "reputation.maximumShift must be finite and non-negative.");
        ValidateAttributeMap(reputation.AttributeWeights, "reputation.attributeWeights", 0, 1);

        var age = generation.AgeCurve ?? throw new InvalidDataException("Invalid parameter configuration: ageCurve is required.");
        Require(age.GrowthStartAge >= 0 && age.GrowthStartAge < age.GrowthEndAge && age.GrowthEndAge <= age.DeclineStartAge &&
            age.DeclineStartAge < age.DeclineEndAge, "ageCurve ages must satisfy growthStart < growthEnd <= declineStart < declineEnd.");
        ValidateAmplitudes(age.GrowthAmplitudes, "growthAmplitudes");
        ValidateAmplitudes(age.DeclineAmplitudes, "declineAmplitudes");
        ValidateFamilyCoverage();

        var roles = Enum.GetValues(typeof(Role)).Cast<Role>().OrderBy(x => x).ToArray();
        Require(generation.PositionAffinities is { Count: > 0 } && generation.PositionAffinities.Count == roles.Length,
            "positionAffinities must contain every Role exactly once.");
        var seenRoles = new HashSet<Role>();
        foreach (var profile in generation.PositionAffinities!)
        {
            var role = ParseRole(profile.Role);
            Require(seenRoles.Add(role), $"positionAffinities contains duplicate role '{profile.Role}'.");
            Require(profile.Values is { Count: 18 }, $"position affinity '{profile.Role}' must contain 18 values.");
            foreach (var value in profile.Values!)
                Require(double.IsFinite(value) && (value == -0.5 || value == 0 || value == 0.5 || value == 1),
                    $"position affinity '{profile.Role}' contains an invalid value.");
        }
        Require(seenRoles.SetEquals(roles), "positionAffinities must cover every Role exactly once.");
    }

    private static void ValidateFamilyCoverage()
    {
        var enumAttributes = Enum.GetValues(typeof(PlayerAttribute)).Cast<PlayerAttribute>().ToHashSet();
        Require(AttributeFamilies.Count == enumAttributes.Count && AttributeFamilies.Keys.ToHashSet().SetEquals(enumAttributes),
            "attribute family mapping must cover every PlayerAttribute exactly once.");
        Require(Enum.GetValues(typeof(AttributeFamily)).Cast<AttributeFamily>().All(family => AttributeFamilies.Values.Contains(family)),
            "attribute family mapping must include Technical, Mental, and Physical.");
    }

    private static void ValidateAmplitudes(AmplitudesDto? amplitudes, string name)
    {
        Require(amplitudes is not null, $"ageCurve.{name} is required.");
        Require(amplitudes!.Technical >= 0 && amplitudes.Technical <= 2 && double.IsFinite(amplitudes.Technical) &&
            amplitudes.Mental >= 0 && amplitudes.Mental <= 2 && double.IsFinite(amplitudes.Mental) &&
            amplitudes.Physical >= 0 && amplitudes.Physical <= 2 && double.IsFinite(amplitudes.Physical),
            $"ageCurve.{name} values must be finite and in [0,2].");
    }

    private static void ValidateAttributeMap(Dictionary<string, double>? map, string name, double minimum, double maximum)
    {
        Require(map is not null, $"{name} is required.");
        var parsed = map!.Select(pair => (PlayerAttribute: ParseAttribute(pair.Key), pair.Value)).ToArray();
        Require(parsed.Length == Enum.GetValues(typeof(PlayerAttribute)).Length && parsed.Select(x => x.PlayerAttribute).Distinct().Count() == parsed.Length,
            $"{name} must contain every PlayerAttribute exactly once.");
        foreach (var value in parsed)
            Require(double.IsFinite(value.Value) && value.Value >= minimum && value.Value <= maximum,
                $"{name} values must be finite and in [{minimum},{maximum}].");
    }

    private static IReadOnlyDictionary<PlayerAttribute, double> ToAttributeMap(Dictionary<string, double> source) =>
        new ReadOnlyDictionary<PlayerAttribute, double>(source.ToDictionary(x => ParseAttribute(x.Key), x => x.Value));

    private static PlayerAttribute ParseAttribute(string? name)
    {
        if (name is null || !Enum.TryParse(name, false, out PlayerAttribute attribute) || Enum.GetName(typeof(PlayerAttribute), attribute) != name)
            throw new InvalidDataException($"Invalid parameter configuration: unknown attribute '{name}'.");
        return attribute;
    }

    private static Role ParseRole(string? name)
    {
        if (name is null || !Enum.TryParse(name, false, out Role role) || Enum.GetName(typeof(Role), role) != name)
            throw new InvalidDataException($"Invalid parameter configuration: unknown role '{name}'.");
        return role;
    }

    private static AttributeFamilyAmplitudes ToAmplitudes(AmplitudesDto value) =>
        new(value.Technical, value.Mental, value.Physical);

    private static bool InRange(double value, double minimum, double maximum) =>
        double.IsFinite(value) && value >= minimum && value <= maximum;

    private static void EnsureRequiredProperties(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Invalid parameter configuration: expected an object.");
        foreach (var name in names)
            if (!element.TryGetProperty(name, out _))
                throw new InvalidDataException($"Invalid parameter configuration: required property '{name}' is missing.");
    }

    private static void RejectDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"Invalid parameter configuration: duplicate property '{property.Name}' at {path}.");
                RejectDuplicateProperties(property.Value, path + "." + property.Name);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item, path + "[" + index++ + "]");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException($"Invalid parameter configuration: {message}");
    }

    private sealed class Dto
    {
        public string? Version { get; set; }
        public FitnessDto? Fitness { get; set; }
        public MoraleDto? Morale { get; set; }
        public List<AgeRecoveryDto>? AgeRecoveryFactors { get; set; }
        public BoardConfidenceDto? BoardConfidence { get; set; }
        public AttributeGenerationDto? AttributeGeneration { get; set; }
        public JsonElement SquadGeneration { get; set; }
    }

    private sealed class FitnessDto { public double Minimum { get; set; } public double Maximum { get; set; } public double PreseasonTarget { get; set; } }
    private sealed class MoraleDto { public double Minimum { get; set; } public double Maximum { get; set; } }
    private sealed class AgeRecoveryDto { public int? MaximumAge { get; set; } public double Factor { get; set; } }
    private sealed class BoardConfidenceDto
    {
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double Initial { get; set; }
        public double LeagueWeight { get; set; }
        public double CupWeight { get; set; }
        public double FriendlyWeight { get; set; }
        public double MatchChangeMinimum { get; set; }
        public double MatchChangeMaximum { get; set; }
    }
    private sealed class AttributeGenerationDto
    {
        public int Minimum { get; set; }
        public int Maximum { get; set; }
        public double StandardDeviation { get; set; }
        public int ReferenceDivisionLevel { get; set; }
        public double ReferenceMean { get; set; }
        public double DivisionMeanGap { get; set; }
        public ReputationDto? Reputation { get; set; }
        public AgeCurveDto? AgeCurve { get; set; }
        public double PositionScale { get; set; }
        public List<string>? Attributes { get; set; }
        public List<PositionAffinityDto>? PositionAffinities { get; set; }
    }
    private sealed class ReputationDto
    {
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double Neutral { get; set; }
        public double Scale { get; set; }
        public double MaximumShift { get; set; }
        public Dictionary<string, double>? AttributeWeights { get; set; }
    }
    private sealed class AgeCurveDto
    {
        public int GrowthStartAge { get; set; }
        public int GrowthEndAge { get; set; }
        public int DeclineStartAge { get; set; }
        public int DeclineEndAge { get; set; }
        public AmplitudesDto? GrowthAmplitudes { get; set; }
        public AmplitudesDto? DeclineAmplitudes { get; set; }
    }
    private sealed class AmplitudesDto { public double Technical { get; set; } public double Mental { get; set; } public double Physical { get; set; } }
    private sealed class PositionAffinityDto { public string? Role { get; set; } public List<double>? Values { get; set; } }
}

public enum AttributeFamily { Technical, Mental, Physical }
public sealed class SquadGenerationParams
{
    internal SquadGenerationParams(double minimumBandCoverage, int maxAttempts, double reputationMeanShift,
        double divisionMeanShift, double referenceReputation, int referenceDivisionLevel, double referenceOvr,
        IReadOnlyDictionary<SquadProfile, SquadProfileBands> profiles)
    { MinimumBandCoverage = minimumBandCoverage; MaxAttempts = maxAttempts; ReputationMeanShift = reputationMeanShift; DivisionMeanShift = divisionMeanShift; ReferenceReputation = referenceReputation; ReferenceDivisionLevel = referenceDivisionLevel; ReferenceOvr = referenceOvr; Profiles = new ReadOnlyDictionary<SquadProfile, SquadProfileBands>(new Dictionary<SquadProfile, SquadProfileBands>(profiles)); }
    public double MinimumBandCoverage { get; }
    public int MaxAttempts { get; }
    public double ReputationMeanShift { get; }
    public double DivisionMeanShift { get; }
    public double ReferenceReputation { get; }
    public int ReferenceDivisionLevel { get; }
    public double ReferenceOvr { get; }
    public IReadOnlyDictionary<SquadProfile, SquadProfileBands> Profiles { get; }
}
public sealed class SquadProfileBands
{
    internal SquadProfileBands(double meanMinimum, double meanMaximum, int starterMinimum, int starterMaximum, int benchMinimum, int benchMaximum)
    { MeanMinimum = meanMinimum; MeanMaximum = meanMaximum; StarterMinimum = starterMinimum; StarterMaximum = starterMaximum; BenchMinimum = benchMinimum; BenchMaximum = benchMaximum; }
    public double MeanMinimum { get; }
    public double MeanMaximum { get; }
    public int StarterMinimum { get; }
    public int StarterMaximum { get; }
    public int BenchMinimum { get; }
    public int BenchMaximum { get; }
}

public sealed class FitnessParams
{
    internal FitnessParams(double minimum, double maximum, double preseasonTarget) { Minimum = minimum; Maximum = maximum; PreseasonTarget = preseasonTarget; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double PreseasonTarget { get; }
}
public sealed class MoraleParams
{
    internal MoraleParams(double minimum, double maximum) { Minimum = minimum; Maximum = maximum; }
    public double Minimum { get; }
    public double Maximum { get; }
}
public sealed class AgeRecoveryFactor
{
    internal AgeRecoveryFactor(int? maximumAge, double factor) { MaximumAge = maximumAge; Factor = factor; }
    public int? MaximumAge { get; }
    public double Factor { get; }
}
public sealed class BoardConfidenceParams
{
    internal BoardConfidenceParams(double minimum, double maximum, double initial, double leagueWeight, double cupWeight,
        double friendlyWeight, double matchChangeMinimum, double matchChangeMaximum)
    {
        Minimum = minimum; Maximum = maximum; Initial = initial; LeagueWeight = leagueWeight; CupWeight = cupWeight;
        FriendlyWeight = friendlyWeight; MatchChangeMinimum = matchChangeMinimum; MatchChangeMaximum = matchChangeMaximum;
    }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Initial { get; }
    public double LeagueWeight { get; }
    public double CupWeight { get; }
    public double FriendlyWeight { get; }
    public double MatchChangeMinimum { get; }
    public double MatchChangeMaximum { get; }
}
public sealed class AttributeGenerationParams
{
    internal AttributeGenerationParams(int minimum, int maximum, double standardDeviation, int referenceDivisionLevel,
        double referenceMean, double divisionMeanGap, ReputationGenerationParams reputation, AttributeAgeCurveParams ageCurve,
        double positionScale, IReadOnlyList<PlayerAttribute> attributes, IReadOnlyList<PositionAffinityProfile> positionAffinities)
    {
        Minimum = minimum; Maximum = maximum; StandardDeviation = standardDeviation; ReferenceDivisionLevel = referenceDivisionLevel;
        ReferenceMean = referenceMean; DivisionMeanGap = divisionMeanGap; Reputation = reputation; AgeCurve = ageCurve;
        PositionScale = positionScale; Attributes = attributes; PositionAffinities = positionAffinities;
    }
    public int Minimum { get; }
    public int Maximum { get; }
    public double StandardDeviation { get; }
    public int ReferenceDivisionLevel { get; }
    /// <summary>Provisional calibration anchor; the configured value 60 is not final production balance.</summary>
    public double ReferenceMean { get; }
    public double DivisionMeanGap { get; }
    public ReputationGenerationParams Reputation { get; }
    public AttributeAgeCurveParams AgeCurve { get; }
    public double PositionScale { get; }
    public IReadOnlyList<PlayerAttribute> Attributes { get; }
    public IReadOnlyList<PositionAffinityProfile> PositionAffinities { get; }
}
public sealed class ReputationGenerationParams
{
    internal ReputationGenerationParams(double minimum, double maximum, double neutral, double scale, double maximumShift,
        IReadOnlyDictionary<PlayerAttribute, double> attributeWeights)
    { Minimum = minimum; Maximum = maximum; Neutral = neutral; Scale = scale; MaximumShift = maximumShift; AttributeWeights = attributeWeights; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Neutral { get; }
    public double Scale { get; }
    public double MaximumShift { get; }
    public IReadOnlyDictionary<PlayerAttribute, double> AttributeWeights { get; }
}
public sealed class AttributeAgeCurveParams
{
    internal AttributeAgeCurveParams(int growthStartAge, int growthEndAge, int declineStartAge, int declineEndAge,
        AttributeFamilyAmplitudes growthAmplitudes, AttributeFamilyAmplitudes declineAmplitudes)
    { GrowthStartAge = growthStartAge; GrowthEndAge = growthEndAge; DeclineStartAge = declineStartAge; DeclineEndAge = declineEndAge; GrowthAmplitudes = growthAmplitudes; DeclineAmplitudes = declineAmplitudes; }
    public int GrowthStartAge { get; }
    public int GrowthEndAge { get; }
    public int DeclineStartAge { get; }
    public int DeclineEndAge { get; }
    public AttributeFamilyAmplitudes GrowthAmplitudes { get; }
    public AttributeFamilyAmplitudes DeclineAmplitudes { get; }
}
public sealed class AttributeFamilyAmplitudes
{
    internal AttributeFamilyAmplitudes(double technical, double mental, double physical) { Technical = technical; Mental = mental; Physical = physical; }
    public double Technical { get; }
    public double Mental { get; }
    public double Physical { get; }
}
public sealed class PositionAffinityProfile
{
    internal PositionAffinityProfile(Role role, IReadOnlyList<double> values) { Role = role; Values = values; }
    public Role Role { get; }
    public IReadOnlyList<double> Values { get; }
}
