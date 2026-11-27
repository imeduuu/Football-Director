using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Competition;

/// <summary>Maps league rounds to season weeks and records short-rest scheduling exceptions.</summary>
public static class SeasonCalendar
{
    public const int MinimumRestDays = 3;

    /// <summary>Maps the 38-round, 20-club league: rounds 1–19 to weeks 7–25, 20–38 to 28–46.</summary>
    public static int CompleteLeagueWeek(int round)
    {
        if (round < 1 || round > 38) throw new ArgumentOutOfRangeException(nameof(round));
        return round <= 19 ? 6 + round : 8 + round;
    }

    /// <summary>Maps the 18-round Slice league across the two 18-week windows, without cup fixtures.</summary>
    public static int SliceLeagueWeek(int round)
    {
        if (round < 1 || round > 18) throw new ArgumentOutOfRangeException(nameof(round));
        return round <= 9 ? 7 + 2 * (round - 1) : 29 + 2 * (round - 10);
    }

    /// <summary>Returns every club's consecutive matches that violate the three-day rest rule.</summary>
    public static IReadOnlyList<CalendarException> FindRestExceptions(IEnumerable<ScheduledMatch> matches)
    {
        if (matches == null) throw new ArgumentNullException(nameof(matches));
        var input = matches.ToArray();
        if (input.Any(match => match == null)) throw new ArgumentException("Scheduled matches cannot contain null entries.", nameof(matches));
        var ordered = input.OrderBy(match => match.Date).ThenBy(match => match.MatchId, StringComparer.Ordinal).ToArray();
        var clubMatches = new SortedDictionary<string, List<ScheduledMatch>>(StringComparer.Ordinal);
        var exceptions = new List<CalendarException>();
        foreach (var match in ordered)
        {
            if (match.Competition == MatchCompetition.League && match.Date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                exceptions.Add(new CalendarException(match.HomeClubId, match.MatchId, match.MatchId, 0, "League match is outside the weekend."));
                exceptions.Add(new CalendarException(match.AwayClubId, match.MatchId, match.MatchId, 0, "League match is outside the weekend."));
            }
            if (match.Competition == MatchCompetition.Cup && match.Date.DayOfWeek != DayOfWeek.Wednesday)
            {
                exceptions.Add(new CalendarException(match.HomeClubId, match.MatchId, match.MatchId, 0, "Cup match is not on Wednesday."));
                exceptions.Add(new CalendarException(match.AwayClubId, match.MatchId, match.MatchId, 0, "Cup match is not on Wednesday."));
            }
            Add(match.HomeClubId, match);
            Add(match.AwayClubId, match);
        }

        foreach (var pair in clubMatches)
        {
            var clubSchedule = pair.Value.OrderBy(match => match.Date).ThenBy(match => match.MatchId, StringComparer.Ordinal).ToArray();
            for (var i = 1; i < clubSchedule.Length; i++)
            {
                var days = (clubSchedule[i].Date - clubSchedule[i - 1].Date).TotalDays;
                if (days < MinimumRestDays)
                    exceptions.Add(new CalendarException(pair.Key, clubSchedule[i - 1].MatchId, clubSchedule[i].MatchId, (int)days, "Club has fewer than three rest days."));
            }
        }
        return exceptions.AsReadOnly();

        void Add(string clubId, ScheduledMatch match)
        {
            if (!clubMatches.TryGetValue(clubId, out var list)) clubMatches.Add(clubId, list = new List<ScheduledMatch>());
            list.Add(match);
        }
    }
}

public sealed class ScheduledMatch
{
    public ScheduledMatch(string matchId, DateTime date, string homeClubId, string awayClubId, MatchCompetition competition, ScheduledMatchStatus status = ScheduledMatchStatus.Scheduled, string? competitionId = null)
    {
        MatchId = string.IsNullOrWhiteSpace(matchId) ? throw new ArgumentException("A match id is required.", nameof(matchId)) : matchId;
        if (date.Kind != DateTimeKind.Unspecified && date.TimeOfDay != TimeSpan.Zero)
            throw new ArgumentException("Match dates must be date-only values at midnight.", nameof(date));
        Date = date.Date;
        HomeClubId = string.IsNullOrWhiteSpace(homeClubId) ? throw new ArgumentException("A home club id is required.", nameof(homeClubId)) : homeClubId;
        AwayClubId = string.IsNullOrWhiteSpace(awayClubId) ? throw new ArgumentException("An away club id is required.", nameof(awayClubId)) : awayClubId;
        if (string.Equals(HomeClubId, AwayClubId, StringComparison.Ordinal)) throw new ArgumentException("A club cannot play itself.");
        if (!Enum.IsDefined(typeof(MatchCompetition), competition)) throw new ArgumentOutOfRangeException(nameof(competition));
        Competition = competition; Status = status; CompetitionId = string.IsNullOrWhiteSpace(competitionId) ? competition.ToString() : competitionId;
    }

    public string MatchId { get; }
    public DateTime Date { get; }
    public string HomeClubId { get; }
    public string AwayClubId { get; }
    public MatchCompetition Competition { get; }
    public ScheduledMatchStatus Status { get; }
    public string CompetitionId { get; }
}

public enum ScheduledMatchStatus { Scheduled, Postponed, Played, Cancelled }

public enum MatchCompetition
{
    League,
    Cup
}

public sealed class CalendarException
{
    internal CalendarException(string clubId, string previousMatchId, string nextMatchId, int restDays, string reason)
    {
        ClubId = clubId;
        PreviousMatchId = previousMatchId;
        NextMatchId = nextMatchId;
        RestDays = restDays;
        Reason = reason;
    }

    public string ClubId { get; }
    public string PreviousMatchId { get; }
    public string NextMatchId { get; }
    public int RestDays { get; }
    public string Reason { get; }
}
