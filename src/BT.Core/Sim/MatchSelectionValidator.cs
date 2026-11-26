using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Sim;

/// <summary>Validates a match selection against the registered roster without creating players.</summary>
public static class MatchSelectionValidator
{
    public static IReadOnlyList<string> Validate(IReadOnlyList<MatchPlayerInput> registeredPlayers,
        IReadOnlyList<string> starterIds, IReadOnlyList<string> substituteIds,
        BT.Core.CompetitionRulesConfig competitionRules)
    {
        if (competitionRules is null) throw new ArgumentNullException(nameof(competitionRules));
        return Validate(registeredPlayers, starterIds, substituteIds,
            competitionRules.Quotas.MaximumExtraCommunityRegistered,
            competitionRules.Quotas.MaximumExtraCommunityStarters);
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<MatchPlayerInput> registeredPlayers,
        IReadOnlyList<string> starterIds, IReadOnlyList<string> substituteIds,
        int maximumExtraCommunityRegistered = 5, int maximumExtraCommunityStarters = 3)
    {
        if (registeredPlayers is null) throw new ArgumentNullException(nameof(registeredPlayers));
        if (starterIds is null) throw new ArgumentNullException(nameof(starterIds));
        if (substituteIds is null) throw new ArgumentNullException(nameof(substituteIds));
        if (maximumExtraCommunityRegistered < 0) throw new ArgumentOutOfRangeException(nameof(maximumExtraCommunityRegistered));
        if (maximumExtraCommunityStarters < 0) throw new ArgumentOutOfRangeException(nameof(maximumExtraCommunityStarters));

        var issues = new List<string>();
        if (registeredPlayers.Any(player => player is null)) throw new ArgumentException("Registered roster cannot contain null players.", nameof(registeredPlayers));
        var roster = registeredPlayers.GroupBy(player => player.PlayerId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var ids = starterIds.Concat(substituteIds).ToArray();
        if (starterIds.Count != 11) issues.Add("A match selection must contain exactly eleven starters.");
        if (substituteIds.Count > 7) issues.Add("A match selection can contain at most seven substitutes.");
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length) issues.Add("A player can appear only once in a selection.");
        var selected = new List<MatchPlayerInput>();
        foreach (var id in ids)
        {
            if (!roster.TryGetValue(id, out var player)) issues.Add($"Player '{id}' is not in the registered roster.");
            else selected.Add(player);
        }
        if (roster.Count != registeredPlayers.Count) issues.Add("Registered roster contains duplicate player ids.");
        var shirtNumbers = registeredPlayers.Where(player => player.ShirtNumber is not null)
            .GroupBy(player => player.ShirtNumber!.Value).Where(group => group.Count() > 1);
        if (registeredPlayers.Any(player => player.ShirtNumber is null or < 1 or > 99))
            issues.Add("Every registered player must have a shirt number from 1 to 99.");
        if (shirtNumbers.Any()) issues.Add("Shirt numbers must be unique within the registered roster.");
        if (registeredPlayers.Count(player => player.IsExtraCommunity) > maximumExtraCommunityRegistered)
            issues.Add("Registered roster exceeds the configured extra-community player quota.");
        if (selected.Any(player => player.IsInjured || player.IsSuspended))
            issues.Add("Injured or suspended players cannot be selected.");
        if (selected.Count(player => player.IsExtraCommunity && starterIds.Contains(player.PlayerId, StringComparer.Ordinal)) > maximumExtraCommunityStarters)
            issues.Add("Starting lineup exceeds the configured extra-community player quota.");
        var starterGoalkeepers = starterIds.Where(roster.ContainsKey).Select(id => roster[id]).Count(player => player.Position == BT.Core.Domain.Position.Goalkeeper);
        if (starterGoalkeepers != 1) issues.Add("Starting lineup must include exactly one goalkeeper.");
        if (ids.Length == 18 && substituteIds.Where(roster.ContainsKey).Select(id => roster[id]).All(player => player.Position != BT.Core.Domain.Position.Goalkeeper))
            issues.Add("A full seven-player selection must include a substitute goalkeeper.");
        return issues.AsReadOnly();
    }
}
