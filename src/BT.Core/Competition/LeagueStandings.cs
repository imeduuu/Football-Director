using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BT.Core.Competition;

/// <summary>Builds a league table from completed league matches using the configured 23.1 order.</summary>
public static class LeagueStandings
{
    public static IReadOnlyList<LeagueStanding> Calculate(
        IEnumerable<string> clubIds, IEnumerable<LeagueResult> results,
        PointsRules points, string competitionId, string seasonId)
    {
        if (clubIds == null) throw new ArgumentNullException(nameof(clubIds));
        if (results == null) throw new ArgumentNullException(nameof(results));
        if (points == null) throw new ArgumentNullException(nameof(points));
        if (string.IsNullOrWhiteSpace(competitionId)) throw new ArgumentException("Competition id is required.", nameof(competitionId));
        if (string.IsNullOrWhiteSpace(seasonId)) throw new ArgumentException("Season id is required.", nameof(seasonId));
        var ids = clubIds.ToArray();
        if (ids.Any(string.IsNullOrWhiteSpace) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
            throw new ArgumentException("Club ids must be non-empty and unique.", nameof(clubIds));
        var index = ids.ToDictionary(id => id, id => new MutableStanding(id), StringComparer.Ordinal);
        var matches = results.ToArray();
        foreach (var match in matches)
        {
            if (match == null) throw new ArgumentException("Results cannot contain null entries.", nameof(results));
            if (!index.ContainsKey(match.HomeClubId) || !index.ContainsKey(match.AwayClubId))
                throw new ArgumentException("Every result must refer to a listed club.", nameof(results));
            var home = index[match.HomeClubId]; var away = index[match.AwayClubId];
            home.Played++; away.Played++;
            home.GoalsFor += match.HomeGoals; home.GoalsAgainst += match.AwayGoals;
            away.GoalsFor += match.AwayGoals; away.GoalsAgainst += match.HomeGoals;
            if (match.HomeGoals > match.AwayGoals) { home.Won++; away.Lost++; home.Points += points.Win; away.Points += points.Loss; }
            else if (match.HomeGoals < match.AwayGoals) { away.Won++; home.Lost++; away.Points += points.Win; home.Points += points.Loss; }
            else { home.Drawn++; away.Drawn++; home.Points += points.Draw; away.Points += points.Draw; }
        }

        // Direct-match criteria apply only within groups tied on the overall table criteria.
        var ordered = ids.Select(id => index[id]).GroupBy(s => new { s.Points, Difference = s.GoalsFor - s.GoalsAgainst, s.GoalsFor })
            .OrderByDescending(g => g.Key.Points).ThenByDescending(g => g.Key.Difference).ThenByDescending(g => g.Key.GoalsFor)
            .SelectMany(group => OrderHeadToHead(group.ToArray(), matches, points, competitionId, seasonId))
            .Select((standing, position) => standing.ToPublic(position + 1)).ToArray();
        return Array.AsReadOnly(ordered);
    }

    private static IEnumerable<MutableStanding> OrderHeadToHead(MutableStanding[] tied, LeagueResult[] matches,
        PointsRules points, string competitionId, string seasonId)
    {
        if (tied.Length < 2) return tied;
        var tiedIds = new HashSet<string>(tied.Select(s => s.ClubId), StringComparer.Ordinal);
        var mini = tied.ToDictionary(s => s.ClubId, _ => new int[2], StringComparer.Ordinal); // points, goal difference
        foreach (var match in matches.Where(m => tiedIds.Contains(m.HomeClubId) && tiedIds.Contains(m.AwayClubId)))
        {
            if (match.HomeGoals > match.AwayGoals) mini[match.HomeClubId][0] += points.Win;
            else if (match.HomeGoals < match.AwayGoals) mini[match.AwayClubId][0] += points.Win;
            else { mini[match.HomeClubId][0] += points.Draw; mini[match.AwayClubId][0] += points.Draw; }
            mini[match.HomeClubId][1] += match.HomeGoals - match.AwayGoals;
            mini[match.AwayClubId][1] += match.AwayGoals - match.HomeGoals;
        }
        // Stable hash is the documented deterministic draw: same competition, season, and club set gives same order.
        return tied.OrderByDescending(s => mini[s.ClubId][0]).ThenByDescending(s => mini[s.ClubId][1])
            .ThenBy(s => DrawKey(competitionId, seasonId, s.ClubId), StringComparer.Ordinal);
    }

    private static string DrawKey(string competitionId, string seasonId, string clubId)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(competitionId + "\n" + seasonId + "\n" + clubId)));
    }

    private sealed class MutableStanding
    {
        public MutableStanding(string id) { ClubId = id; }
        public string ClubId { get; } public int Played, Won, Drawn, Lost, GoalsFor, GoalsAgainst, Points;
        public LeagueStanding ToPublic(int position) => new LeagueStanding(position, ClubId, Played, Won, Drawn, Lost, GoalsFor, GoalsAgainst, Points);
    }
}

/// <summary>A completed league result.</summary>
public sealed class LeagueResult
{
    public LeagueResult(string homeClubId, string awayClubId, int homeGoals, int awayGoals)
    {
        if (string.IsNullOrWhiteSpace(homeClubId) || string.IsNullOrWhiteSpace(awayClubId) || homeClubId == awayClubId)
            throw new ArgumentException("Distinct home and away club ids are required.");
        if (homeGoals < 0 || awayGoals < 0) throw new ArgumentOutOfRangeException(nameof(homeGoals), "Goals cannot be negative.");
        HomeClubId = homeClubId; AwayClubId = awayClubId; HomeGoals = homeGoals; AwayGoals = awayGoals;
    }
    public string HomeClubId { get; } public string AwayClubId { get; }
    public int HomeGoals { get; } public int AwayGoals { get; }
}

/// <summary>One row in the calculated league table.</summary>
public sealed class LeagueStanding
{
    internal LeagueStanding(int position, string clubId, int played, int won, int drawn, int lost, int goalsFor, int goalsAgainst, int points)
    { Position = position; ClubId = clubId; Played = played; Won = won; Drawn = drawn; Lost = lost; GoalsFor = goalsFor; GoalsAgainst = goalsAgainst; Points = points; }
    public int Position { get; } public string ClubId { get; } public int Played { get; }
    public int Won { get; } public int Drawn { get; } public int Lost { get; }
    public int GoalsFor { get; } public int GoalsAgainst { get; } public int GoalDifference => GoalsFor - GoalsAgainst;
    public int Points { get; }
}
