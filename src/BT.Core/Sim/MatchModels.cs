using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core.Domain;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Sim;

/// <summary>Stable engine versions used to reproduce saved match inputs.</summary>
public enum EngineVersion
{
    V10_2 = 102,
    V10_8 = 108,
    V10_10 = 1010
}

public enum TacticalMentality { VeryDefensive, Defensive, Balanced, Attacking, Overload }
public enum PassingStyle { VeryShort, Short, Mixed, Direct, Long }
public enum PressingStyle { LowBlock, Medium, High, Gegenpressing }
public enum DefensiveLine { Deep, Normal, High }
public enum TeamWidth { Narrow, Normal, Wide }

/// <summary>Intermediate tactical effects and UI alerts derived from Slice tactics.</summary>
public sealed class TacticalTension
{
    internal TacticalTension(double pressureEffectivenessMultiplier, double centralProgressionMultiplier,
        double backlineCoverageMultiplier, double throughPassMultiplier, IReadOnlyList<string> alerts)
    {
        PressureEffectivenessMultiplier = pressureEffectivenessMultiplier;
        CentralProgressionMultiplier = centralProgressionMultiplier;
        BacklineCoverageMultiplier = backlineCoverageMultiplier;
        ThroughPassMultiplier = throughPassMultiplier;
        Alerts = Array.AsReadOnly(alerts.ToArray());
    }

    public double PressureEffectivenessMultiplier { get; }
    public double CentralProgressionMultiplier { get; }
    public double BacklineCoverageMultiplier { get; }
    public double ThroughPassMultiplier { get; }
    public IReadOnlyList<string> Alerts { get; }
}

/// <summary>Immutable snapshot of the five collective tactical axes from GDD section 6.3.</summary>
public sealed class MatchTactics
{
    public MatchTactics(string formationId, TacticalMentality mentality, PassingStyle passing,
        PressingStyle pressing, DefensiveLine defensiveLine, TeamWidth width)
    {
        FormationId = string.IsNullOrWhiteSpace(formationId)
            ? throw new ArgumentException("A formation id is required.", nameof(formationId))
            : formationId;
        Mentality = Enum.IsDefined(typeof(TacticalMentality), mentality) ? mentality : throw new ArgumentOutOfRangeException(nameof(mentality));
        Passing = Enum.IsDefined(typeof(PassingStyle), passing) ? passing : throw new ArgumentOutOfRangeException(nameof(passing));
        Pressing = Enum.IsDefined(typeof(PressingStyle), pressing) ? pressing : throw new ArgumentOutOfRangeException(nameof(pressing));
        DefensiveLine = Enum.IsDefined(typeof(DefensiveLine), defensiveLine) ? defensiveLine : throw new ArgumentOutOfRangeException(nameof(defensiveLine));
        Width = Enum.IsDefined(typeof(TeamWidth), width) ? width : throw new ArgumentOutOfRangeException(nameof(width));
    }

    public string FormationId { get; }
    public TacticalMentality Mentality { get; }
    public PassingStyle Passing { get; }
    public PressingStyle Pressing { get; }
    public DefensiveLine DefensiveLine { get; }
    public TeamWidth Width { get; }
}

/// <summary>Player values captured for one match; later career changes cannot alter this snapshot.</summary>
public sealed class MatchPlayerInput
{
    public MatchPlayerInput(string playerId, Position position,
        IReadOnlyDictionary<PlayerAttribute, int> effectiveAttributes, int fitness, int morale)
    {
        PlayerId = string.IsNullOrWhiteSpace(playerId)
            ? throw new ArgumentException("A stable player id is required.", nameof(playerId))
            : playerId;
        Position = Enum.IsDefined(typeof(Position), position) ? position : throw new ArgumentOutOfRangeException(nameof(position));
        if (effectiveAttributes is null) throw new ArgumentNullException(nameof(effectiveAttributes));
        if (effectiveAttributes.Count != Enum.GetValues(typeof(PlayerAttribute)).Length ||
            effectiveAttributes.Any(pair => !Enum.IsDefined(typeof(PlayerAttribute), pair.Key) || pair.Value is < 1 or > 99))
            throw new ArgumentException("All effective attributes must be provided in the range 1–99.", nameof(effectiveAttributes));
        var attributes = new SortedDictionary<PlayerAttribute, int>();
        foreach (var pair in effectiveAttributes) attributes.Add(pair.Key, pair.Value);
        EffectiveAttributes = new System.Collections.ObjectModel.ReadOnlyDictionary<PlayerAttribute, int>(attributes);
        Fitness = fitness is >= 0 and <= 100 ? fitness : throw new ArgumentOutOfRangeException(nameof(fitness));
        Morale = morale is >= 0 and <= 100 ? morale : throw new ArgumentOutOfRangeException(nameof(morale));
    }

    public string PlayerId { get; }
    public Position Position { get; }
    public IReadOnlyDictionary<PlayerAttribute, int> EffectiveAttributes { get; }
    public int Fitness { get; }
    public int Morale { get; }
}

/// <summary>Immutable squad and tactical snapshot for one side of a match.</summary>
public sealed class MatchTeamInput
{
    public MatchTeamInput(string teamId, IReadOnlyList<MatchPlayerInput> starters,
        IReadOnlyList<MatchPlayerInput> substitutes, MatchTactics tactics)
    {
        TeamId = string.IsNullOrWhiteSpace(teamId)
            ? throw new ArgumentException("A stable team id is required.", nameof(teamId))
            : teamId;
        if (starters is null) throw new ArgumentNullException(nameof(starters));
        if (substitutes is null) throw new ArgumentNullException(nameof(substitutes));
        Tactics = tactics ?? throw new ArgumentNullException(nameof(tactics));

        var starterSnapshot = starters.ToArray();
        var substituteSnapshot = substitutes.ToArray();
        if (starterSnapshot.Length != 11 || starterSnapshot.Any(player => player is null) ||
            starterSnapshot.Count(player => player.Position == Position.Goalkeeper) != 1)
            throw new ArgumentException("A starting lineup must contain eleven players and exactly one goalkeeper.", nameof(starters));
        if (substituteSnapshot.Any(player => player is null))
            throw new ArgumentException("Substitutes cannot contain null players.", nameof(substitutes));
        if (starterSnapshot.Concat(substituteSnapshot).Select(player => player.PlayerId)
            .Distinct(StringComparer.Ordinal).Count() != starterSnapshot.Length + substituteSnapshot.Length)
            throw new ArgumentException("A player can appear only once in a team selection.", nameof(substitutes));

        Starters = Array.AsReadOnly(starterSnapshot);
        Substitutes = Array.AsReadOnly(substituteSnapshot);
    }

    public string TeamId { get; }
    public IReadOnlyList<MatchPlayerInput> Starters { get; }
    public IReadOnlyList<MatchPlayerInput> Substitutes { get; }
    public MatchTactics Tactics { get; }
}

/// <summary>Complete immutable input snapshot; HomeTeam/AwayTeam encode match localía.</summary>
public sealed class MatchInput
{
    public MatchInput(MatchTeamInput homeTeam, MatchTeamInput awayTeam, ulong seed,
        EngineVersion engineVersion, bool isNeutralVenue = false)
    {
        HomeTeam = homeTeam ?? throw new ArgumentNullException(nameof(homeTeam));
        AwayTeam = awayTeam ?? throw new ArgumentNullException(nameof(awayTeam));
        if (string.Equals(HomeTeam.TeamId, AwayTeam.TeamId, StringComparison.Ordinal))
            throw new ArgumentException("Home and away teams must be different.", nameof(awayTeam));
        if (!Enum.IsDefined(typeof(EngineVersion), engineVersion))
            throw new ArgumentOutOfRangeException(nameof(engineVersion));
        var homePlayerIds = HomeTeam.Starters.Concat(HomeTeam.Substitutes).Select(player => player.PlayerId);
        var awayPlayerIds = AwayTeam.Starters.Concat(AwayTeam.Substitutes).Select(player => player.PlayerId);
        if (homePlayerIds.Intersect(awayPlayerIds, StringComparer.Ordinal).Any())
            throw new ArgumentException("A player cannot be selected for both teams.", nameof(awayTeam));

        Seed = seed;
        EngineVersion = engineVersion;
        IsNeutralVenue = isNeutralVenue;
    }

    public MatchTeamInput HomeTeam { get; }
    public MatchTeamInput AwayTeam { get; }
    public ulong Seed { get; }
    public EngineVersion EngineVersion { get; }
    public bool IsNeutralVenue { get; }
}

public enum MatchEventType
{
    Goal,
    OwnGoal,
    YellowCard,
    RedCard,
    Injury,
    Substitution,
    PenaltyMiss,
    Shot,
    Other
}

/// <summary>A single event in match chronology; text should be localized by consumers.</summary>
public sealed class MatchEvent
{
    public MatchEvent(int minute, MatchEventType type, string teamId,
        string? playerId = null, string? relatedPlayerId = null)
    {
        Minute = minute is >= 0 and <= 120 ? minute : throw new ArgumentOutOfRangeException(nameof(minute));
        Type = Enum.IsDefined(typeof(MatchEventType), type) ? type : throw new ArgumentOutOfRangeException(nameof(type));
        TeamId = string.IsNullOrWhiteSpace(teamId)
            ? throw new ArgumentException("A stable team id is required.", nameof(teamId))
            : teamId;
        PlayerId = playerId;
        RelatedPlayerId = relatedPlayerId;
        if (type is MatchEventType.Goal or MatchEventType.OwnGoal or MatchEventType.YellowCard or
            MatchEventType.RedCard or MatchEventType.Injury && string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("This event requires a player id.", nameof(playerId));
        if (type == MatchEventType.Substitution &&
            (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(relatedPlayerId)))
            throw new ArgumentException("A substitution requires outgoing and incoming player ids.", nameof(relatedPlayerId));
    }

    public int Minute { get; }
    public MatchEventType Type { get; }
    public string TeamId { get; }
    public string? PlayerId { get; }
    public string? RelatedPlayerId { get; }
}

public sealed class MatchScorer
{
    public MatchScorer(string teamId, string playerId, int minute, bool isOwnGoal = false)
    {
        TeamId = string.IsNullOrWhiteSpace(teamId) ? throw new ArgumentException("A stable team id is required.", nameof(teamId)) : teamId;
        PlayerId = string.IsNullOrWhiteSpace(playerId) ? throw new ArgumentException("A stable player id is required.", nameof(playerId)) : playerId;
        Minute = minute is >= 0 and <= 120 ? minute : throw new ArgumentOutOfRangeException(nameof(minute));
        IsOwnGoal = isOwnGoal;
    }

    public string TeamId { get; }
    public string PlayerId { get; }
    public int Minute { get; }
    public bool IsOwnGoal { get; }
}

public sealed class MatchScore
{
    public MatchScore(int homeGoals, int awayGoals)
    {
        HomeGoals = homeGoals >= 0 ? homeGoals : throw new ArgumentOutOfRangeException(nameof(homeGoals));
        AwayGoals = awayGoals >= 0 ? awayGoals : throw new ArgumentOutOfRangeException(nameof(awayGoals));
    }

    public int HomeGoals { get; }
    public int AwayGoals { get; }
}

public enum MatchZone
{
    DefenceLeft,
    DefenceCentre,
    DefenceRight,
    MidfieldLeft,
    MidfieldCentre,
    MidfieldRight,
    AttackLeft,
    AttackCentre,
    AttackRight
}

/// <summary>Nine-cell zone map ordered defence-to-attack, left-to-right.</summary>
public sealed class MatchZoneMap
{
    public MatchZoneMap(IReadOnlyList<int> cells)
    {
        if (cells is null) throw new ArgumentNullException(nameof(cells));
        if (cells.Count != 9 || cells.Any(value => value < 0))
            throw new ArgumentException("A zone map must contain nine non-negative cell values.", nameof(cells));
        Cells = Array.AsReadOnly(cells.ToArray());
    }

    public IReadOnlyList<int> Cells { get; }
}

public sealed class MatchPlayerRating
{
    public MatchPlayerRating(string playerId, double rating)
    {
        PlayerId = string.IsNullOrWhiteSpace(playerId) ? throw new ArgumentException("A stable player id is required.", nameof(playerId)) : playerId;
        if (double.IsNaN(rating) || double.IsInfinity(rating) || rating < 3.0 || rating > 10.0)
            throw new ArgumentOutOfRangeException(nameof(rating));
        Rating = rating;
    }

    public string PlayerId { get; }
    public double Rating { get; }
}

public sealed class MatchTeamReport
{
    public MatchTeamReport(double possession, int shots, int shotsOnTarget, double expectedGoals,
        MatchZoneMap zones, IReadOnlyList<MatchPlayerRating> ratings)
    {
        if (double.IsNaN(possession) || double.IsInfinity(possession) || possession < 0.0 || possession > 100.0)
            throw new ArgumentOutOfRangeException(nameof(possession));
        Possession = possession;
        Shots = shots >= 0 ? shots : throw new ArgumentOutOfRangeException(nameof(shots));
        ShotsOnTarget = shotsOnTarget >= 0 && shotsOnTarget <= shots
            ? shotsOnTarget
            : throw new ArgumentOutOfRangeException(nameof(shotsOnTarget));
        if (double.IsNaN(expectedGoals) || double.IsInfinity(expectedGoals) || expectedGoals < 0.0)
            throw new ArgumentOutOfRangeException(nameof(expectedGoals));
        ExpectedGoals = expectedGoals;
        Zones = zones ?? throw new ArgumentNullException(nameof(zones));
        if (ratings is null) throw new ArgumentNullException(nameof(ratings));
        var ratingSnapshot = ratings.ToArray();
        if (ratingSnapshot.Any(rating => rating is null) ||
            ratingSnapshot.Select(rating => rating.PlayerId).Distinct(StringComparer.Ordinal).Count() != ratingSnapshot.Length)
            throw new ArgumentException("Player ratings must be non-null and unique per player.", nameof(ratings));
        Ratings = Array.AsReadOnly(ratingSnapshot);
    }

    public double Possession { get; }
    public int Shots { get; }
    public int ShotsOnTarget { get; }
    public double ExpectedGoals { get; }
    public MatchZoneMap Zones { get; }
    public IReadOnlyList<MatchPlayerRating> Ratings { get; }
}

/// <summary>Immutable match result, including statistics, ratings and chronological events.</summary>
public sealed class MatchReport
{
    public MatchReport(MatchScore score, IReadOnlyList<MatchScorer> goalscorers,
        IReadOnlyList<MatchEvent> events, MatchTeamReport home, MatchTeamReport away)
    {
        Score = score ?? throw new ArgumentNullException(nameof(score));
        if (goalscorers is null) throw new ArgumentNullException(nameof(goalscorers));
        if (events is null) throw new ArgumentNullException(nameof(events));
        Home = home ?? throw new ArgumentNullException(nameof(home));
        Away = away ?? throw new ArgumentNullException(nameof(away));

        var scorerSnapshot = goalscorers.ToArray();
        if (scorerSnapshot.Any(scorer => scorer is null))
            throw new ArgumentException("Goalscorers cannot contain null entries.", nameof(goalscorers));
        var eventSnapshot = events.ToArray();
        if (eventSnapshot.Any(matchEvent => matchEvent is null))
            throw new ArgumentException("Events cannot contain null entries.", nameof(events));
        for (var index = 1; index < eventSnapshot.Length; index++)
            if (eventSnapshot[index].Minute < eventSnapshot[index - 1].Minute)
                throw new ArgumentException("Events must be in chronological order.", nameof(events));

        Goalscorers = Array.AsReadOnly(scorerSnapshot);
        Events = Array.AsReadOnly(eventSnapshot);
        Cards = Array.AsReadOnly(eventSnapshot.Where(matchEvent =>
            matchEvent.Type is MatchEventType.YellowCard or MatchEventType.RedCard).ToArray());
        Injuries = Array.AsReadOnly(eventSnapshot.Where(matchEvent => matchEvent.Type == MatchEventType.Injury).ToArray());
    }

    public MatchScore Score { get; }
    public IReadOnlyList<MatchScorer> Goalscorers { get; }
    public IReadOnlyList<MatchEvent> Events { get; }
    public IReadOnlyList<MatchEvent> Cards { get; }
    public IReadOnlyList<MatchEvent> Injuries { get; }
    public MatchTeamReport Home { get; }
    public MatchTeamReport Away { get; }
}
