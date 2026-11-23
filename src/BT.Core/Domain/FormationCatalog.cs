using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BT.Core.Domain;

/// <summary>Validated, externally configured football formations.</summary>
public sealed class FormationCatalog
{
    private static readonly string[] SliceFormationIds = { "4-4-2", "4-3-3" };
    private readonly IReadOnlyDictionary<string, FormationDefinition> _byId;

    private FormationCatalog(IReadOnlyList<FormationDefinition> formations)
    {
        Formations = formations;
        _byId = formations.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<FormationDefinition> Formations { get; }
    public IReadOnlyList<FormationDefinition> SliceFormations =>
        Array.AsReadOnly(SliceFormationIds.Select(id => _byId[id]).ToArray());

    public FormationDefinition this[string id] => _byId.TryGetValue(id, out var formation)
        ? formation
        : throw new KeyNotFoundException($"Formation '{id}' is not configured.");

    public static FormationCatalog Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
        return LoadJson(File.ReadAllText(path));
    }

    public static FormationCatalog LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON content is required.", nameof(json));

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("formations", out var formationsElement) ||
                formationsElement.ValueKind != JsonValueKind.Array || !root.TryGetProperty("sliceActiveFormations", out var activeElement) ||
                activeElement.ValueKind != JsonValueKind.Array)
                throw Invalid("formations and sliceActiveFormations arrays are required.");

            var activeIds = activeElement.EnumerateArray().Select(item =>
            {
                if (item.ValueKind != JsonValueKind.String) throw Invalid("Slice formation ids must be strings.");
                return item.GetString()!;
            }).ToArray();
            if (!activeIds.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(
                    SliceFormationIds.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
                throw Invalid("The Slice must activate exactly 4-4-2 and 4-3-3.");

            var formations = new List<FormationDefinition>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in formationsElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("id", out var idElement) ||
                    idElement.ValueKind != JsonValueKind.String || !item.TryGetProperty("activeInSlice", out var activeInSliceElement) ||
                    activeInSliceElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                    !item.TryGetProperty("points", out var pointsElement) || pointsElement.ValueKind != JsonValueKind.Array)
                    throw Invalid("Each formation must contain id, activeInSlice and points.");

                var id = idElement.GetString()!;
                if (string.IsNullOrWhiteSpace(id) || !seenIds.Add(id)) throw Invalid($"Formation id '{id}' is empty or duplicated.");
                var isActive = activeInSliceElement.GetBoolean();
                if (isActive != activeIds.Contains(id, StringComparer.Ordinal))
                    throw Invalid($"Formation '{id}' does not match sliceActiveFormations.");

                var points = new List<FormationPoint>();
                foreach (var point in pointsElement.EnumerateArray())
                {
                    if (point.ValueKind != JsonValueKind.Object || !point.TryGetProperty("position", out var positionElement) ||
                        positionElement.ValueKind != JsonValueKind.String || !point.TryGetProperty("x", out var xElement) ||
                        !xElement.TryGetInt32(out var x) || !point.TryGetProperty("y", out var yElement) || !yElement.TryGetInt32(out var y))
                        throw Invalid($"Formation '{id}' contains a malformed point.");
                    var positionName = positionElement.GetString()!;
                    if (!Enum.TryParse(positionName, false, out Position position) ||
                        !string.Equals(Enum.GetName(typeof(Position), position), positionName, StringComparison.Ordinal))
                        throw Invalid($"Formation '{id}' references unknown position '{positionName}'.");
                    if (x is < 0 or > 100 || y is < 0 or > 100)
                        throw Invalid($"Formation '{id}' coordinates must be in [0,100].");
                    points.Add(new FormationPoint(position, x, y));
                }

                if (points.Count != 11) throw Invalid($"Formation '{id}' must contain exactly 11 points.");
                if (points.Count(point => point.Position == Position.Goalkeeper) != 1)
                    throw Invalid($"Formation '{id}' must contain exactly one goalkeeper.");
                formations.Add(new FormationDefinition(id, isActive, points.AsReadOnly()));
            }

            if (formations.Count != 12) throw Invalid($"Expected 12 formations, found {formations.Count}.");
            if (SliceFormationIds.Any(id => !seenIds.Contains(id))) throw Invalid("Both Slice formations must be defined.");
            return new FormationCatalog(formations.AsReadOnly());
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The formation document is not valid JSON or has an invalid shape.", exception);
        }
    }

    private static InvalidDataException Invalid(string message) => new($"Invalid formation configuration: {message}");
}

/// <summary>A single player position in normalized team-relative field coordinates.</summary>
public sealed class FormationPoint
{
    internal FormationPoint(Position position, int x, int y)
    {
        Position = position;
        X = x;
        Y = y;
    }

    public Position Position { get; }
    public int X { get; }
    public int Y { get; }
}

/// <summary>A named formation with eleven validated player positions.</summary>
public sealed class FormationDefinition
{
    internal FormationDefinition(string id, bool activeInSlice, IReadOnlyList<FormationPoint> points)
    {
        Id = id;
        ActiveInSlice = activeInSlice;
        Points = points;
    }

    public string Id { get; }
    public bool ActiveInSlice { get; }
    public IReadOnlyList<FormationPoint> Points { get; }
}
