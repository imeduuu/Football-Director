using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Competition;

/// <summary>Applies configured direct movement between two divisions at season end.</summary>
public static class SeasonMovement
{
    public static SeasonMovementResult Apply(
        IReadOnlyList<LeagueStanding> upperDivision,
        CompetitionRulesConfig upperRules,
        IReadOnlyList<LeagueStanding> lowerDivision,
        CompetitionRulesConfig lowerRules)
    {
        if (upperDivision == null) throw new ArgumentNullException(nameof(upperDivision));
        if (lowerDivision == null) throw new ArgumentNullException(nameof(lowerDivision));
        if (upperRules == null) throw new ArgumentNullException(nameof(upperRules));
        if (lowerRules == null) throw new ArgumentNullException(nameof(lowerRules));

        var promoted = Select(lowerDivision, lowerRules.Movement.PromotedDirect, "promotedDirect");
        var relegated = Select(upperDivision, upperRules.Movement.RelegatedDirect, "relegatedDirect", fromBottom: true);
        if (promoted.Count != relegated.Count)
            throw new ArgumentException("Configured promotions and relegations must have equal counts to preserve division sizes.");

        var promotedIds = new HashSet<string>(promoted, StringComparer.Ordinal);
        var relegatedIds = new HashSet<string>(relegated, StringComparer.Ordinal);
        var nextUpper = upperDivision.Where(s => !relegatedIds.Contains(s.ClubId)).Select(s => s.ClubId)
            .Concat(promoted).ToArray();
        var nextLower = lowerDivision.Where(s => !promotedIds.Contains(s.ClubId)).Select(s => s.ClubId)
            .Concat(relegated).ToArray();
        return new SeasonMovementResult(promoted, relegated, nextUpper, nextLower);
    }

    private static IReadOnlyList<string> Select(IReadOnlyList<LeagueStanding> table, int count, string rule, bool fromBottom = false)
    {
        if (count < 0 || count > table.Count)
            throw new ArgumentException($"Configured {rule} count exceeds the available standings.", nameof(table));
        var ordered = table.OrderBy(s => s.Position);
        if (fromBottom) ordered = ordered.OrderByDescending(s => s.Position);
        return Array.AsReadOnly(ordered.Take(count).Select(s => s.ClubId).ToArray());
    }
}

public sealed class SeasonMovementResult
{
    internal SeasonMovementResult(IReadOnlyList<string> promoted, IReadOnlyList<string> relegated,
        IReadOnlyList<string> nextUpperDivision, IReadOnlyList<string> nextLowerDivision)
    { Promoted = promoted; Relegated = relegated; NextUpperDivision = nextUpperDivision; NextLowerDivision = nextLowerDivision; }
    public IReadOnlyList<string> Promoted { get; }
    public IReadOnlyList<string> Relegated { get; }
    public IReadOnlyList<string> NextUpperDivision { get; }
    public IReadOnlyList<string> NextLowerDivision { get; }
}
