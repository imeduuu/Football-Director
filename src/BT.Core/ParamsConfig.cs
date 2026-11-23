using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BT.Core;

/// <summary>Typed, validated balance parameters loaded from a versioned JSON document.</summary>
public sealed class ParamsConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow
    };

    private ParamsConfig(string version, FitnessParams fitness, MoraleParams morale,
        IReadOnlyList<AgeRecoveryFactor> ageRecoveryFactors, BoardConfidenceParams boardConfidence)
    {
        Version = version;
        Fitness = fitness;
        Morale = morale;
        AgeRecoveryFactors = ageRecoveryFactors;
        BoardConfidence = boardConfidence;
    }

    public string Version { get; }
    public FitnessParams Fitness { get; }
    public MoraleParams Morale { get; }
    public IReadOnlyList<AgeRecoveryFactor> AgeRecoveryFactors { get; }
    public BoardConfidenceParams BoardConfidence { get; }

    public static ParamsConfig Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
        return LoadJson(File.ReadAllText(path));
    }

    public static ParamsConfig LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON content is required.", nameof(json));
        Dto dto;
        try
        {
            using var document = JsonDocument.Parse(json);
            EnsureRequiredProperties(document.RootElement, "version", "fitness", "morale", "ageRecoveryFactors", "boardConfidence");
            var root = document.RootElement;
            EnsureRequiredProperties(root.GetProperty("fitness"), "minimum", "maximum", "preseasonTarget");
            EnsureRequiredProperties(root.GetProperty("morale"), "minimum", "maximum");
            foreach (var band in root.GetProperty("ageRecoveryFactors").EnumerateArray())
                EnsureRequiredProperties(band, "maximumAge", "factor");
            EnsureRequiredProperties(root.GetProperty("boardConfidence"), "minimum", "maximum", "initial",
                "leagueWeight", "cupWeight", "friendlyWeight", "matchChangeMinimum", "matchChangeMaximum");
            dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions)
                ?? throw new InvalidDataException("The parameter document is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The parameter document is not valid JSON or has an invalid shape.", exception);
        }

        Validate(dto);
        return new ParamsConfig(dto.Version!,
            new FitnessParams(dto.Fitness!.Minimum, dto.Fitness.Maximum, dto.Fitness.PreseasonTarget),
            new MoraleParams(dto.Morale!.Minimum, dto.Morale.Maximum),
            Array.AsReadOnly(dto.AgeRecoveryFactors!.Select(x => new AgeRecoveryFactor(x.MaximumAge, x.Factor)).ToArray()),
            new BoardConfidenceParams(dto.BoardConfidence!.Minimum, dto.BoardConfidence.Maximum,
                dto.BoardConfidence.Initial, dto.BoardConfidence.LeagueWeight, dto.BoardConfidence.CupWeight,
                dto.BoardConfidence.FriendlyWeight, dto.BoardConfidence.MatchChangeMinimum,
                dto.BoardConfidence.MatchChangeMaximum));
    }

    private static void Validate(Dto dto)
    {
        Require(!string.IsNullOrWhiteSpace(dto.Version), "version must be a non-empty string.");
        Require(dto.Fitness is not null, "fitness is required.");
        Require(dto.Morale is not null, "morale is required.");
        Require(dto.AgeRecoveryFactors is { Count: > 0 }, "ageRecoveryFactors must not be empty.");
        Require(dto.BoardConfidence is not null, "boardConfidence is required.");

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

        Require(confidence.Minimum <= confidence.Maximum, "boardConfidence.minimum must not exceed boardConfidence.maximum.");
        Require(InRange(confidence.Initial, confidence.Minimum, confidence.Maximum), "boardConfidence.initial must be within its range.");
        Require(confidence.LeagueWeight >= 0 && double.IsFinite(confidence.LeagueWeight), "leagueWeight must be finite and non-negative.");
        Require(confidence.CupWeight >= 0 && double.IsFinite(confidence.CupWeight), "cupWeight must be finite and non-negative.");
        Require(confidence.FriendlyWeight >= 0 && double.IsFinite(confidence.FriendlyWeight), "friendlyWeight must be finite and non-negative.");
        Require(confidence.MatchChangeMinimum <= confidence.MatchChangeMaximum, "match change minimum must not exceed maximum.");
    }

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
    }

    private sealed class FitnessDto { public double Minimum { get; set; } public double Maximum { get; set; } public double PreseasonTarget { get; set; } }
    private sealed class MoraleDto { public double Minimum { get; set; } public double Maximum { get; set; } }
    private sealed class AgeRecoveryDto { public int? MaximumAge { get; set; } public double Factor { get; set; } }
    private sealed class BoardConfidenceDto
    {
        public double Minimum { get; set; } public double Maximum { get; set; } public double Initial { get; set; }
        public double LeagueWeight { get; set; } public double CupWeight { get; set; } public double FriendlyWeight { get; set; }
        public double MatchChangeMinimum { get; set; } public double MatchChangeMaximum { get; set; }
    }
}

public sealed class FitnessParams
{
    internal FitnessParams(double minimum, double maximum, double preseasonTarget) { Minimum = minimum; Maximum = maximum; PreseasonTarget = preseasonTarget; }
    public double Minimum { get; } public double Maximum { get; } public double PreseasonTarget { get; }
}
public sealed class MoraleParams
{
    internal MoraleParams(double minimum, double maximum) { Minimum = minimum; Maximum = maximum; }
    public double Minimum { get; } public double Maximum { get; }
}
public sealed class AgeRecoveryFactor
{
    internal AgeRecoveryFactor(int? maximumAge, double factor) { MaximumAge = maximumAge; Factor = factor; }
    public int? MaximumAge { get; } public double Factor { get; }
}
public sealed class BoardConfidenceParams
{
    internal BoardConfidenceParams(double minimum, double maximum, double initial, double leagueWeight, double cupWeight,
        double friendlyWeight, double matchChangeMinimum, double matchChangeMaximum)
    {
        Minimum = minimum; Maximum = maximum; Initial = initial; LeagueWeight = leagueWeight; CupWeight = cupWeight;
        FriendlyWeight = friendlyWeight; MatchChangeMinimum = matchChangeMinimum; MatchChangeMaximum = matchChangeMaximum;
    }
    public double Minimum { get; } public double Maximum { get; } public double Initial { get; }
    public double LeagueWeight { get; } public double CupWeight { get; } public double FriendlyWeight { get; }
    public double MatchChangeMinimum { get; } public double MatchChangeMaximum { get; }
}
