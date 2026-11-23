using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BT.Core.Domain;

/// <summary>Loads and validates the externally configured positional role weights.</summary>
public sealed class RoleProfiles
{
    public const double WeightSumTolerance = 1e-9;
    private readonly IReadOnlyDictionary<Role, RoleProfile> _byRole;

    private RoleProfiles(IReadOnlyList<RoleProfile> profiles)
    {
        Profiles = profiles;
        _byRole = profiles.ToDictionary(profile => profile.Role);
    }

    public IReadOnlyList<RoleProfile> Profiles { get; }

    public RoleProfile this[Role role] => _byRole.TryGetValue(role, out var profile)
        ? profile
        : throw new KeyNotFoundException($"No positional profile is configured for role '{role}'.");

    public static RoleProfiles Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.", nameof(path));
        return LoadJson(File.ReadAllText(path));
    }

    public static RoleProfiles LoadJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("JSON content is required.", nameof(json));

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("profiles", out var profilesElement) ||
                profilesElement.ValueKind != JsonValueKind.Array)
                throw Invalid("profiles must be an array.");

            var profiles = new List<RoleProfile>();
            var seenRoles = new HashSet<Role>();
            foreach (var item in profilesElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("role", out var roleElement) ||
                    roleElement.ValueKind != JsonValueKind.String || !item.TryGetProperty("weights", out var weightsElement) ||
                    weightsElement.ValueKind != JsonValueKind.Object)
                    throw Invalid("Each profile must have a role and a weights object.");

                var roleName = roleElement.GetString()!;
                if (!Enum.TryParse(roleName, ignoreCase: false, out Role role) ||
                    !string.Equals(Enum.GetName(typeof(Role), role), roleName, StringComparison.Ordinal))
                    throw Invalid($"Unknown role '{roleName}'.");
                if (!seenRoles.Add(role)) throw Invalid($"Role '{roleName}' is configured more than once.");

                var weights = new Dictionary<Attribute, double>();
                foreach (var weightElement in weightsElement.EnumerateObject())
                {
                    if (!Enum.TryParse(weightElement.Name, ignoreCase: false, out Attribute attribute) ||
                        !string.Equals(Enum.GetName(typeof(Attribute), attribute), weightElement.Name, StringComparison.Ordinal))
                        throw Invalid($"Unknown attribute '{weightElement.Name}' in role '{roleName}'.");
                    if (weightElement.Value.ValueKind != JsonValueKind.Number || !weightElement.Value.TryGetDouble(out var weight) ||
                        !double.IsFinite(weight) || weight < 0)
                        throw Invalid($"Weight for '{weightElement.Name}' in role '{roleName}' must be finite and non-negative.");
                    weights.Add(attribute, weight);
                }

                var sum = 0.0;
                foreach (var weight in weights.OrderBy(pair => pair.Key)) sum += weight.Value;
                var sumError = sum - 1.0;
                if (sumError < 0) sumError = -sumError;
                if (weights.Count == 0 || sumError > WeightSumTolerance)
                    throw Invalid(string.Format(CultureInfo.InvariantCulture,
                        "Weights for role '{0}' sum to {1:R}; expected 1.0 ± {2:R}.", roleName, sum, WeightSumTolerance));

                profiles.Add(new RoleProfile(role, weights));
            }

            if (profiles.Count != 15)
                throw Invalid($"Expected 15 role profiles, found {profiles.Count}.");
            return new RoleProfiles(profiles.AsReadOnly());
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The role profile document is not valid JSON or has an invalid shape.", exception);
        }
    }

    private static InvalidDataException Invalid(string message) => new($"Invalid role profile configuration: {message}");
}

/// <summary>Immutable attribute weights for one tactical role.</summary>
public sealed class RoleProfile
{
    internal RoleProfile(Role role, IDictionary<Attribute, double> weights)
    {
        Role = role;
        Weights = new System.Collections.ObjectModel.ReadOnlyDictionary<Attribute, double>(
            new Dictionary<Attribute, double>(weights));
    }

    public Role Role { get; }
    public IReadOnlyDictionary<Attribute, double> Weights { get; }
}
