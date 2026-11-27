using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core.Rng;
using BT.Core.Sim;

namespace BT.Core.Competition;

/// <summary>Coordinates deterministic simulation and atomic closure of one competition round.</summary>
public sealed class MatchdayProcessor
{
    private readonly IMatchdayStore _store;
    private readonly IMatchdaySimulator _simulator;

    public MatchdayProcessor(IMatchdayStore store, IMatchdaySimulator simulator)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    }

    /// <summary>
    /// Simulates every unprocessed fixture and commits reports, player effects and standings
    /// through one store transaction. Store implementations must make ApplyMatch atomic and
    /// enforce uniqueness by match id; a failed callback must roll the whole round back.
    /// </summary>
    public MatchdayResult Process(ulong worldSeed, string competitionId, int season, int round,
        string seasonId, string userClubId, PointsRules points)
    {
        if (string.IsNullOrWhiteSpace(competitionId)) throw new ArgumentException("Competition id is required.", nameof(competitionId));
        if (string.IsNullOrWhiteSpace(seasonId)) throw new ArgumentException("Season id is required.", nameof(seasonId));
        if (string.IsNullOrWhiteSpace(userClubId)) throw new ArgumentException("User club id is required.", nameof(userClubId));
        if (season < 0) throw new ArgumentOutOfRangeException(nameof(season));
        if (round < 1) throw new ArgumentOutOfRangeException(nameof(round));
        if (points is null) throw new ArgumentNullException(nameof(points));

        return _store.ExecuteTransaction(tx =>
        {
            var loadedFixtures = tx.GetFixtures(competitionId, seasonId, round).ToArray();
            if (loadedFixtures.Any(f => f is null)) throw new InvalidOperationException("Store returned a null fixture.");
            var fixtures = loadedFixtures.OrderBy(f => f.MatchId, StringComparer.Ordinal).ToArray();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var participatingClubs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in fixtures)
            {
                if (!ids.Add(f.MatchId)) throw new InvalidOperationException("Fixture ids must be unique in a matchday.");
                if (f.Competition != fixtures[0].Competition)
                    throw new InvalidOperationException("A matchday cannot contain fixtures from different competition types.");
                if (!participatingClubs.Add(f.HomeClubId) || !participatingClubs.Add(f.AwayClubId))
                    throw new InvalidOperationException("A club cannot appear in more than one fixture in a matchday.");
            }

            var processed = new List<ProcessedMatch>();
            foreach (var fixture in fixtures)
            {
                // The persisted result is the idempotency authority. Fixture status is only a
                // projection; a mismatch indicates broken persistence and must not silently
                // skip a match or apply its effects a second time.
                var hasPersistedResult = tx.IsMatchProcessed(fixture.MatchId);
                if (fixture.IsProcessed && !hasPersistedResult)
                    throw new InvalidOperationException("Fixture is marked processed but has no persisted match result.");
                if (hasPersistedResult) continue;
                var seed = SeedDeriver.MatchSeed(worldSeed, competitionId, season, round, fixture.MatchId);
                var input = tx.CreateMatchInput(fixture, seed, string.Equals(fixture.HomeClubId, userClubId, StringComparison.Ordinal) ||
                    string.Equals(fixture.AwayClubId, userClubId, StringComparison.Ordinal));
                if (input is null) throw new InvalidOperationException("Store returned a null match input.");
                if (input.Seed != seed) throw new InvalidOperationException("Match input seed does not match its deterministic fixture seed.");
                var userMatch = string.Equals(fixture.HomeClubId, userClubId, StringComparison.Ordinal) ||
                    string.Equals(fixture.AwayClubId, userClubId, StringComparison.Ordinal);
                if (!string.Equals(input.HomeTeam.TeamId, fixture.HomeClubId, StringComparison.Ordinal) ||
                    !string.Equals(input.AwayTeam.TeamId, fixture.AwayClubId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Match input participants do not match the fixture.");
                var result = userMatch
                    ? new ProcessedMatch(fixture, seed, true, input, _simulator.SimulateFull(input))
                    : new ProcessedMatch(fixture, seed, false, input, _simulator.SimulateHeadless(input));
                ValidateResult(result);
                // This operation records match/events, cards and bans, injury availability,
                // scorer stats, FIT/MOR, fixture completion and the idempotency key together.
                tx.ApplyMatch(result);
                processed.Add(result);
            }

            var table = Array.Empty<LeagueStanding>();
            if (fixtures.Length > 0 && fixtures[0].Competition == MatchCompetition.League)
            {
                var results = tx.GetCompletedLeagueResults(competitionId, seasonId);
                table = LeagueStandings.Calculate(tx.GetClubIds(competitionId, seasonId), results,
                    points, competitionId, seasonId).ToArray();
                tx.ReplaceStandings(competitionId, seasonId, table);
            }
            return new MatchdayResult(processed, table);
        });
    }

    private static void ValidateResult(ProcessedMatch match)
    {
        if (match.Score is null) throw new InvalidOperationException("Simulator returned a result without a score.");
        var home = match.Fixture.HomeClubId;
        var away = match.Fixture.AwayClubId;
        var homePlayers = match.Input.HomeTeam.Starters.Concat(match.Input.HomeTeam.Substitutes)
            .Select(player => player.PlayerId).ToHashSet(StringComparer.Ordinal);
        var awayPlayers = match.Input.AwayTeam.Starters.Concat(match.Input.AwayTeam.Substitutes)
            .Select(player => player.PlayerId).ToHashSet(StringComparer.Ordinal);
        bool IsParticipant(string teamId, string? playerId) => playerId is not null &&
            (teamId == home ? homePlayers.Contains(playerId) : teamId == away && awayPlayers.Contains(playerId));
        if (match.Report is not null)
        {
            var report = match.Report;
            if (report.Home.Ratings.Any(r => r is null) || report.Away.Ratings.Any(r => r is null) ||
                report.Goalscorers.Any(g => g is null) || report.Events.Any(e => e is null))
                throw new InvalidOperationException("Simulator returned null report data.");
            if (report.Goalscorers.Any(g => g.TeamId != home && g.TeamId != away) ||
                report.Events.Any(e => e.TeamId != home && e.TeamId != away))
                throw new InvalidOperationException("Simulator returned an event for a club outside the fixture.");
            if (report.Goalscorers.Any(g => !IsParticipant(g.IsOwnGoal ? (g.TeamId == home ? away : home) : g.TeamId, g.PlayerId) ||
                    (g.AssisterId is not null && !IsParticipant(g.TeamId, g.AssisterId))) ||
                report.Events.Any(e => e.PlayerId is not null && !IsParticipant(e.TeamId, e.PlayerId) ||
                    e.RelatedPlayerId is not null && !IsParticipant(e.TeamId, e.RelatedPlayerId)))
                throw new InvalidOperationException("Simulator returned a player event for a non-participant.");
            if (report.Home.Ratings.Any(r => !homePlayers.Contains(r.PlayerId)) ||
                report.Away.Ratings.Any(r => !awayPlayers.Contains(r.PlayerId)))
                throw new InvalidOperationException("Simulator returned a rating for a non-participant.");
            var homeGoals = report.Goalscorers.Count(g => (g.IsOwnGoal ? g.TeamId == away : g.TeamId == home));
            var awayGoals = report.Goalscorers.Count(g => (g.IsOwnGoal ? g.TeamId == home : g.TeamId == away));
            if (homeGoals != report.Score.HomeGoals || awayGoals != report.Score.AwayGoals)
                throw new InvalidOperationException("Simulator score and goalscorers disagree.");
        }
        else
        {
            var result = match.Headless ?? throw new InvalidOperationException("Simulator returned no match outcome.");
            if (result.Goalscorers.Any(g => g.TeamId != home && g.TeamId != away) ||
                result.Goalscorers.Any(g => !IsParticipant(g.IsOwnGoal ? (g.TeamId == home ? away : home) : g.TeamId, g.PlayerId) ||
                    (g.AssisterId is not null && !IsParticipant(g.TeamId, g.AssisterId))))
                throw new InvalidOperationException("Simulator returned an invalid headless goalscorer or assist.");
            var homeGoals = result.Goalscorers.Count(g => g.IsOwnGoal ? g.TeamId == away : g.TeamId == home);
            var awayGoals = result.Goalscorers.Count(g => g.IsOwnGoal ? g.TeamId == home : g.TeamId == away);
            if (homeGoals != result.Score.HomeGoals || awayGoals != result.Score.AwayGoals)
                throw new InvalidOperationException("Headless score and goalscorers disagree.");
            if (result.Cards.Concat(result.Injuries).Any(e => e.TeamId != home && e.TeamId != away ||
                    e.PlayerId is null || !IsParticipant(e.TeamId, e.PlayerId)))
                throw new InvalidOperationException("Simulator returned an invalid headless card or injury.");
            if (result.HomeForm.Any(r => !homePlayers.Contains(r.PlayerId)) ||
                result.AwayForm.Any(r => !awayPlayers.Contains(r.PlayerId)))
                throw new InvalidOperationException("Simulator returned headless form for a non-participant.");
        }
    }
}

/// <summary>Persistence boundary. ExecuteTransaction must rollback on any exception.</summary>
public interface IMatchdayStore
{
    T ExecuteTransaction<T>(Func<IMatchdayTransaction, T> operation);
}

/// <summary>All reads and writes used to close a round occur in the enclosing transaction.</summary>
public interface IMatchdayTransaction
{
    IReadOnlyList<MatchdayFixture> GetFixtures(string competitionId, string seasonId, int round);
    bool IsMatchProcessed(string matchId);
    MatchInput CreateMatchInput(MatchdayFixture fixture, ulong seed, bool isUserMatch);
    /// <summary>Atomically persists match and events, discipline, injuries, FIT/MOR, stats and fixture status.</summary>
    void ApplyMatch(ProcessedMatch match);
    IReadOnlyList<LeagueResult> GetCompletedLeagueResults(string competitionId, string seasonId);
    IReadOnlyList<string> GetClubIds(string competitionId, string seasonId);
    void ReplaceStandings(string competitionId, string seasonId, IReadOnlyList<LeagueStanding> standings);
}

/// <summary>The simulation engine uses the full event model for the user and compact mode for other fixtures.</summary>
public interface IMatchdaySimulator
{
    MatchReport SimulateFull(MatchInput input);
    HeadlessMatchResult SimulateHeadless(MatchInput input);
}

public sealed class MatchdayFixture
{
    public MatchdayFixture(string matchId, string homeClubId, string awayClubId,
        MatchCompetition competition, bool isProcessed = false)
    {
        MatchId = Required(matchId, nameof(matchId)); HomeClubId = Required(homeClubId, nameof(homeClubId));
        AwayClubId = Required(awayClubId, nameof(awayClubId));
        if (HomeClubId == AwayClubId) throw new ArgumentException("A club cannot play itself.");
        if (!Enum.IsDefined(typeof(MatchCompetition), competition)) throw new ArgumentOutOfRangeException(nameof(competition));
        Competition = competition; IsProcessed = isProcessed;
    }
    public string MatchId { get; } public string HomeClubId { get; } public string AwayClubId { get; }
    public MatchCompetition Competition { get; } public bool IsProcessed { get; }
    private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value;
}

public sealed class ProcessedMatch
{
    internal ProcessedMatch(MatchdayFixture fixture, ulong seed, bool isUserMatch, MatchInput input, MatchReport report)
    { Fixture = fixture; Seed = seed; IsUserMatch = isUserMatch; Input = input; Report = report ?? throw new InvalidOperationException("Simulator returned a null report."); }
    internal ProcessedMatch(MatchdayFixture fixture, ulong seed, bool isUserMatch, MatchInput input, HeadlessMatchResult headless)
    { Fixture = fixture; Seed = seed; IsUserMatch = isUserMatch; Input = input; Headless = headless ?? throw new InvalidOperationException("Simulator returned a null headless result."); }
    public MatchdayFixture Fixture { get; } public ulong Seed { get; } public bool IsUserMatch { get; }
    public MatchReport? Report { get; } public HeadlessMatchResult? Headless { get; }
    internal MatchInput Input { get; }
    public MatchScore Score => Report?.Score ?? Headless!.Score;
}

public sealed class MatchdayResult
{
    internal MatchdayResult(IReadOnlyList<ProcessedMatch> matches, IReadOnlyList<LeagueStanding> standings)
    { Matches = Array.AsReadOnly(matches.ToArray()); Standings = Array.AsReadOnly(standings.ToArray()); }
    public IReadOnlyList<ProcessedMatch> Matches { get; } public IReadOnlyList<LeagueStanding> Standings { get; }
    /// <summary>The user's full report, or null when their fixture was already processed or absent.</summary>
    public MatchReport? UserMatchReport => Matches.FirstOrDefault(match => match.IsUserMatch)?.Report;
}
