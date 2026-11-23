using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Domain;

/// <summary>A football club in the simulated world.</summary>
public sealed class Club
{
    public Club(string id, string name, string nationality, SquadProfile profile, int reputation,
        string primaryColor, string secondaryColor, int stadiumCapacity, long transferBudget,
        long weeklyWageBudget, IReadOnlyList<Staff> staff)
    {
        Id = string.IsNullOrWhiteSpace(id) ? throw new ArgumentException("A stable club id is required.", nameof(id)) : id;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("A club name is required.", nameof(name)) : name;
        Nationality = string.IsNullOrWhiteSpace(nationality) ? throw new ArgumentException("A nationality is required.", nameof(nationality)) : nationality;
        Profile = Enum.IsDefined(typeof(SquadProfile), profile) ? profile : throw new ArgumentOutOfRangeException(nameof(profile));
        Reputation = reputation is >= 1 and <= 100 ? reputation : throw new ArgumentOutOfRangeException(nameof(reputation));
        PrimaryColor = ValidateColor(primaryColor, nameof(primaryColor));
        SecondaryColor = ValidateColor(secondaryColor, nameof(secondaryColor));
        if (string.Equals(PrimaryColor, SecondaryColor, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Club colors must be distinct.", nameof(secondaryColor));
        StadiumCapacity = stadiumCapacity is >= 5000 and <= 85000 ? stadiumCapacity : throw new ArgumentOutOfRangeException(nameof(stadiumCapacity));
        TransferBudget = transferBudget >= 0 ? transferBudget : throw new ArgumentOutOfRangeException(nameof(transferBudget));
        WeeklyWageBudget = weeklyWageBudget >= 0 ? weeklyWageBudget : throw new ArgumentOutOfRangeException(nameof(weeklyWageBudget));
        Staff = Array.AsReadOnly((staff ?? throw new ArgumentNullException(nameof(staff))).ToArray());
        Status = ClubStatus.Active;
    }
    public string Id { get; }
    public string Name { get; }
    public string Nationality { get; }
    public SquadProfile Profile { get; }
    public int Reputation { get; }
    public string PrimaryColor { get; }
    public string SecondaryColor { get; }
    public int StadiumCapacity { get; }
    public long TransferBudget { get; }
    public long WeeklyWageBudget { get; }
    public IReadOnlyList<Staff> Staff { get; }
    public ClubStatus Status { get; }

    internal static string ValidateColor(string value, string parameterName)
    {
        if (value is null || value.Length != 7 || value[0] != '#' ||
            !value.Substring(1).All(Uri.IsHexDigit))
            throw new ArgumentException("A color must be an RGB hex value such as #12ABEF.", parameterName);
        return value.ToUpperInvariant();
    }
}

public enum ClubStatus { Active }
