using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Domain;

/// <summary>Stable hidden player attribute identifiers from GDD section 4.2.</summary>
public enum HiddenAttribute
{
    Potential,
    InjuryProneness,
    Adaptability,
    Loyalty,
    Professionalism
}

/// <summary>A player and their visible and hidden attribute values.</summary>
public sealed class Player
{
    public Player(string id, IReadOnlyDictionary<Attribute, int> visibleAttributes,
        IReadOnlyDictionary<HiddenAttribute, int> hiddenAttributes, int age, string nationality,
        IReadOnlyList<Position> positions, Contract? contract)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("A stable player id is required.", nameof(id)) : id;
        VisibleAttributes = ValidateAttributes(visibleAttributes, Enum.GetValues(typeof(Attribute)).Length, nameof(visibleAttributes));
        HiddenAttributes = ValidateAttributes(hiddenAttributes, Enum.GetValues(typeof(HiddenAttribute)).Length, nameof(hiddenAttributes));
        Age = age >= 0 ? age : throw new ArgumentOutOfRangeException(nameof(age));
        Nationality = string.IsNullOrWhiteSpace(nationality) ? throw new ArgumentException("A nationality is required.", nameof(nationality)) : nationality;
        Positions = positions is { Count: > 0 } ? Array.AsReadOnly(positions.Distinct().ToArray()) : throw new ArgumentException("At least one position is required.", nameof(positions));
        Contract = contract;
    }

    public string Id { get; }
    public IReadOnlyDictionary<Attribute, int> VisibleAttributes { get; }
    public IReadOnlyDictionary<HiddenAttribute, int> HiddenAttributes { get; }
    public int Age { get; }
    public string Nationality { get; }
    public IReadOnlyList<Position> Positions { get; }
    public Contract? Contract { get; }

    private static IReadOnlyDictionary<T, int> ValidateAttributes<T>(IReadOnlyDictionary<T, int> attributes, int expectedCount, string parameterName) where T : struct, Enum
    {
        if (attributes is null) throw new ArgumentNullException(parameterName);
        if (attributes.Count != expectedCount || attributes.Any(pair => !Enum.IsDefined(typeof(T), pair.Key)) || attributes.Values.Any(value => value is < 1 or > 99))
            throw new ArgumentException($"Expected {expectedCount} attribute values in the range 1–99.", parameterName);
        var copy = new SortedDictionary<T, int>();
        foreach (var pair in attributes) copy.Add(pair.Key, pair.Value);
        return new System.Collections.ObjectModel.ReadOnlyDictionary<T, int>(copy);
    }
}
