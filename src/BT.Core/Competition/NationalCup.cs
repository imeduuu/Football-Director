using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core.Rng;

namespace BT.Core.Competition;

/// <summary>Creates the deterministic 40-club National Cup defined by GDD section 23.2.</summary>
public static class NationalCup
{
    private const string CompetitionId = "national-cup";
    private static readonly int[] RoundWeeks = { 9, 14, 20, 32, 38, 45 };

    public static IReadOnlyList<CupClub> SelectPreliminaryEntrants(IEnumerable<CupClub> clubs, bool firstSeason)
    {
        var all = ValidateClubs(clubs);
        var first = all.Where(c => c.Division == 1).ToArray();
        var second = all.Where(c => c.Division == 2).ToArray();
        if (first.Length != 20 || second.Length != 20)
            throw new ArgumentException("The National Cup requires 20 clubs in each division.", nameof(clubs));

        var ordered = firstSeason
            ? second.OrderBy(c => c.Reputation).ThenBy(c => c.ClubId, StringComparer.Ordinal).ToArray()
            : second.OrderBy(c => c.PreviousPosition).ThenBy(c => c.ClubId, StringComparer.Ordinal).ToArray();
        return Array.AsReadOnly(ordered.Skip(4).ToArray());
    }

    public static CupRound CreatePreliminary(IEnumerable<CupClub> clubs, bool firstSeason, ulong worldSeed, int season)
    {
        var all = ValidateClubs(clubs);
        var preliminary = SelectPreliminaryEntrants(all, firstSeason);
        return Draw(CupRoundKind.Preliminary, preliminary, worldSeed, season);
    }

    /// <summary>Combines the 24 exempt clubs and eight preliminary winners for the round of 32.</summary>
    public static CupRound CreateRoundOf32(IEnumerable<CupClub> clubs, IEnumerable<string> preliminaryWinners, ulong worldSeed, int season)
    {
        var all = ValidateClubs(clubs);
        if (preliminaryWinners == null) throw new ArgumentNullException(nameof(preliminaryWinners));
        var winners = preliminaryWinners.ToArray();
        if (winners.Length != 8 || winners.Distinct(StringComparer.Ordinal).Count() != 8)
            throw new ArgumentException("Exactly eight distinct preliminary winners are required.", nameof(preliminaryWinners));
        var winnerSet = new HashSet<string>(winners, StringComparer.Ordinal);
        var exempt = all.Where(c => c.Division == 1 || c.PreviousPosition <= 4)
            .Where(c => !winnerSet.Contains(c.ClubId)).ToArray();
        if (exempt.Length != 24 || winners.Any(id => !all.Any(c => c.ClubId == id && c.Division == 2 && c.PreviousPosition >= 5)))
            throw new ArgumentException("Winners must be preliminary entrants and 24 clubs must be exempt.", nameof(preliminaryWinners));
        return Draw(CupRoundKind.RoundOf32, exempt.Concat(all.Where(c => winnerSet.Contains(c.ClubId))), worldSeed, season);
    }

    /// <summary>Draws any open, unseeded single-elimination round from its current participants.</summary>
    public static CupRound Draw(CupRoundKind kind, IEnumerable<CupClub> participants, ulong worldSeed, int season)
    {
        if (!Enum.IsDefined(typeof(CupRoundKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (participants == null) throw new ArgumentNullException(nameof(participants));
        var teams = participants.ToArray();
        var expected = kind switch { CupRoundKind.Preliminary => 16, CupRoundKind.RoundOf32 => 32, CupRoundKind.RoundOf16 => 16, CupRoundKind.QuarterFinal => 8, CupRoundKind.SemiFinal => 4, CupRoundKind.Final => 2, _ => 0 };
        if (teams.Length != expected || teams.Any(t => t == null) || teams.Select(t => t.ClubId).Distinct(StringComparer.Ordinal).Count() != expected)
            throw new ArgumentException($"{kind} requires {expected} distinct clubs.", nameof(participants));

        var seed = SeedDeriver.WorldSubSeed(worldSeed, "23.2", CompetitionId, $"season:{season}:round:{(int)kind}");
        var rng = new Pcg32(seed, unchecked((ulong)(int)kind));
        var shuffled = teams.OrderBy(t => t.ClubId, StringComparer.Ordinal).ToArray();
        for (var i = shuffled.Length - 1; i > 0; i--)
        {
            var j = rng.NextInt(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        var matches = new CupMatch[shuffled.Length / 2];
        var week = RoundWeeks[(int)kind];
        for (var i = 0; i < matches.Length; i++)
            matches[i] = new CupMatch($"cup-{season}-{(int)kind}-{i + 1:D2}", week, shuffled[i * 2], shuffled[i * 2 + 1], kind == CupRoundKind.Final);
        return new CupRound(kind, week, Array.AsReadOnly(matches));
    }

    /// <summary>Returns the Wednesday of a season week, where weekOneMonday is the season's first Monday.</summary>
    public static DateTime WednesdayOfWeek(DateTime weekOneMonday, int week)
    {
        if (weekOneMonday.DayOfWeek != DayOfWeek.Monday || weekOneMonday.TimeOfDay != TimeSpan.Zero)
            throw new ArgumentException("The season week-one anchor must be a Monday at midnight.", nameof(weekOneMonday));
        if (week < 1 || week > 52) throw new ArgumentOutOfRangeException(nameof(week));
        return weekOneMonday.AddDays((week - 1) * 7 + 2);
    }

    private static CupClub[] ValidateClubs(IEnumerable<CupClub> clubs)
    {
        if (clubs == null) throw new ArgumentNullException(nameof(clubs));
        var all = clubs.ToArray();
        if (all.Any(c => c == null) || all.Length != 40 || all.Select(c => c.ClubId).Distinct(StringComparer.Ordinal).Count() != 40)
            throw new ArgumentException("Exactly 40 distinct clubs are required.", nameof(clubs));
        foreach (var division in new[] { 1, 2 })
        {
            var positions = all.Where(c => c.Division == division).Select(c => c.PreviousPosition).OrderBy(p => p).ToArray();
            if (!positions.SequenceEqual(Enumerable.Range(1, 20)))
                throw new ArgumentException("Each division must contain the unique previous positions 1 through 20.", nameof(clubs));
        }
        return all;
    }
}

public enum CupRoundKind { Preliminary, RoundOf32, RoundOf16, QuarterFinal, SemiFinal, Final }

public sealed class CupClub
{
    public CupClub(string clubId, int division, int previousPosition, int reputation)
    {
        ClubId = string.IsNullOrWhiteSpace(clubId) ? throw new ArgumentException("A club id is required.", nameof(clubId)) : clubId;
        if (division is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(division));
        if (previousPosition is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(previousPosition));
        if (reputation is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(reputation));
        Division = division; PreviousPosition = previousPosition; Reputation = reputation;
    }
    public string ClubId { get; }
    public int Division { get; }
    public int PreviousPosition { get; }
    public int Reputation { get; }
}

public sealed class CupRound
{
    internal CupRound(CupRoundKind kind, int week, IReadOnlyList<CupMatch> matches) { Kind = kind; Week = week; Matches = matches; }
    public CupRoundKind Kind { get; }
    public int Week { get; }
    public IReadOnlyList<CupMatch> Matches { get; }
}

public sealed class CupMatch
{
    internal CupMatch(string matchId, int week, CupClub home, CupClub away, bool neutralVenue)
    { MatchId = matchId; Week = week; Home = home; Away = away; NeutralVenue = neutralVenue; }
    public string MatchId { get; }
    public int Week { get; }
    public CupClub Home { get; }
    public CupClub Away { get; }
    public bool NeutralVenue { get; }
}
