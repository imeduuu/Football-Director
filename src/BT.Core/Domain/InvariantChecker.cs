using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Domain;

/// <summary>Reusable checks for player ratings, club registrations, and squad coverage.</summary>
public static class InvariantChecker
{
    /// <summary>Returns violations when a player's OVR or potential is outside the GDD scale or inconsistent.</summary>
    public static IReadOnlyList<string> CheckPlayerRating(Player player, int ovr)
    {
        if (player is null) throw new ArgumentNullException(nameof(player));
        var issues = new List<string>();
        var potential = player.HiddenAttributes[HiddenAttribute.Potential];
        if (ovr is < 1 or > 99) issues.Add($"Player '{player.Id}' OVR must be in [1,99].");
        if (potential < ovr) issues.Add($"Player '{player.Id}' potential must be at least OVR.");
        return issues.AsReadOnly();
    }

    /// <summary>
    /// Checks that each player is registered to one club, except for an explicit loan registration
    /// that names the other (owning) club.
    /// </summary>
    public static IReadOnlyList<string> CheckRegistrations(IEnumerable<ClubRegistration> registrations)
    {
        if (registrations is null) throw new ArgumentNullException(nameof(registrations));
        var issues = new List<string>();
        foreach (var group in registrations.GroupBy(item => item.PlayerId, StringComparer.Ordinal))
        {
            var entries = group.ToArray();
            var clubs = entries.Select(item => item.ClubId).Distinct(StringComparer.Ordinal).ToArray();
            if (clubs.Length <= 1) continue;
            var validLoan = entries.Length == 2 && clubs.Length == 2 &&
                entries.Count(item => item.LoanFromClubId is not null) == 1 &&
                entries.Any(item => item.LoanFromClubId is null) &&
                entries.Single(item => item.LoanFromClubId is not null).LoanFromClubId ==
                    entries.Single(item => item.LoanFromClubId is null).ClubId;
            if (!validLoan) issues.Add($"Player '{group.Key}' is registered to multiple clubs without one explicit loan.");
        }
        return issues.AsReadOnly();
    }

    /// <summary>Requires at least two goalkeepers and two players in each outfield line.</summary>
    public static IReadOnlyList<string> CheckMinimumSquad(IEnumerable<ClubRegistration> registrations, string clubId)
    {
        if (registrations is null) throw new ArgumentNullException(nameof(registrations));
        if (string.IsNullOrWhiteSpace(clubId)) throw new ArgumentException("A club id is required.", nameof(clubId));

        var squad = registrations.Where(item => string.Equals(item.ClubId, clubId, StringComparison.Ordinal)).ToArray();
        var issues = new List<string>();
        RequireMinimum(squad, item => item.Position == Position.Goalkeeper, "goalkeepers", clubId, issues);
        RequireMinimum(squad, item => item.Position is Position.CentreBack or Position.LeftBack or Position.RightBack, "defenders", clubId, issues);
        RequireMinimum(squad, item => item.Position is Position.DefensiveMidfielder or Position.CentralMidfielder or Position.AttackingMidfielder, "midfielders", clubId, issues);
        RequireMinimum(squad, item => item.Position is Position.LeftWinger or Position.RightWinger or Position.Striker, "forwards", clubId, issues);
        return issues.AsReadOnly();
    }

    private static void RequireMinimum(ClubRegistration[] squad, Func<ClubRegistration, bool> matches,
        string line, string clubId, ICollection<string> issues)
    {
        var count = squad.Where(matches).Select(item => item.PlayerId).Distinct(StringComparer.Ordinal).Count();
        if (count < 2) issues.Add($"Club '{clubId}' must have at least two {line}.");
    }
}

/// <summary>A player's club registration; LoanFromClubId marks the owning club for a loaned player.</summary>
public sealed class ClubRegistration
{
    public ClubRegistration(string playerId, string clubId, Position position, string? loanFromClubId = null)
    {
        PlayerId = string.IsNullOrWhiteSpace(playerId) ? throw new ArgumentException("A player id is required.", nameof(playerId)) : playerId;
        ClubId = string.IsNullOrWhiteSpace(clubId) ? throw new ArgumentException("A club id is required.", nameof(clubId)) : clubId;
        if (!Enum.IsDefined(typeof(Position), position)) throw new ArgumentOutOfRangeException(nameof(position));
        if (loanFromClubId is not null && string.IsNullOrWhiteSpace(loanFromClubId)) throw new ArgumentException("A loan owner id cannot be empty.", nameof(loanFromClubId));
        if (string.Equals(clubId, loanFromClubId, StringComparison.Ordinal)) throw new ArgumentException("A loan destination must differ from its owner.", nameof(loanFromClubId));
        Position = position;
        LoanFromClubId = loanFromClubId;
    }

    public string PlayerId { get; }
    public string ClubId { get; }
    public Position Position { get; }
    public string? LoanFromClubId { get; }
}
