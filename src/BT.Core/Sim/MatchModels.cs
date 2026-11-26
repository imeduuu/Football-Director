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
    Foul,
    PenaltyAwarded,
    Corner,
    Other,
    SubstitutionFailed
}

public enum AutomaticSubstitutionMode { Slice, Mvp }
public enum AutomaticSubstitutionFailureReason
{
    NoEligibleOutgoingPlayer, NoAvailableSubstitute, PlayerExpelled, PlayerInjured,
    SubstitutionLimitReached, InvalidPosition, MissingEmergencyGoalkeeper
}

/// <summary>A pre-match automatic substitution instruction. Incoming players are selected only from the named bench player or matching position.</summary>
public sealed class AutomaticSubstitutionRule
{
    public AutomaticSubstitutionRule(int minute, Position outgoingPosition, int? maximumFitness = null,
        string? incomingPlayerId = null, string? emergencyGoalkeeperId = null)
    {
        Minute = minute is >= 60 and <= 120 ? minute : throw new ArgumentOutOfRangeException(nameof(minute));
        OutgoingPosition = Enum.IsDefined(typeof(Position), outgoingPosition) ? outgoingPosition : throw new ArgumentOutOfRangeException(nameof(outgoingPosition));
        MaximumFitness = maximumFitness is null or >= 0 and <= 100 ? maximumFitness : throw new ArgumentOutOfRangeException(nameof(maximumFitness));
        IncomingPlayerId = incomingPlayerId;
        EmergencyGoalkeeperId = emergencyGoalkeeperId;
        if (incomingPlayerId is not null && string.IsNullOrWhiteSpace(incomingPlayerId)) throw new ArgumentException("Player id cannot be empty.", nameof(incomingPlayerId));
        if (emergencyGoalkeeperId is not null && string.IsNullOrWhiteSpace(emergencyGoalkeeperId)) throw new ArgumentException("Player id cannot be empty.", nameof(emergencyGoalkeeperId));
    }
    public int Minute { get; }
    public Position OutgoingPosition { get; }
    public int? MaximumFitness { get; }
    public string? IncomingPlayerId { get; }
    /// <summary>Explicitly named outfield player permitted to take goal; no emergency keeper is inferred.</summary>
    public string? EmergencyGoalkeeperId { get; }
}

public sealed class AutomaticSubstitutionFailure
{
    public AutomaticSubstitutionFailure(int minute, int ruleIndex, AutomaticSubstitutionFailureReason reason)
    { Minute = minute; RuleIndex = ruleIndex; Reason = reason; }
    public int Minute { get; }
    public int RuleIndex { get; }
    public AutomaticSubstitutionFailureReason Reason { get; }
}

/// <summary>Deterministic rule evaluator over current match events; each rule is attempted once when its configured minute is reached.</summary>
public static class AutomaticSubstitutionEvaluator
{
    public static IReadOnlyList<MatchEvent> Evaluate(MatchTeamInput team, IReadOnlyList<AutomaticSubstitutionRule> rules,
        IReadOnlyList<MatchEvent> events, int minute, AutomaticSubstitutionMode mode, int substitutionLimit = 5)
    {
        if (team is null) throw new ArgumentNullException(nameof(team));
        if (rules is null) throw new ArgumentNullException(nameof(rules));
        if (events is null) throw new ArgumentNullException(nameof(events));
        if (minute is < 0 or > 120) throw new ArgumentOutOfRangeException(nameof(minute));
        if (!Enum.IsDefined(typeof(AutomaticSubstitutionMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (substitutionLimit < 0) throw new ArgumentOutOfRangeException(nameof(substitutionLimit));
        var ruleLimit = mode == AutomaticSubstitutionMode.Slice ? 1 : 3;
        if (rules.Count > ruleLimit) throw new ArgumentException($"This mode permits at most {ruleLimit} automatic rule(s).", nameof(rules));
        var result = new List<MatchEvent>();
        var processed = new HashSet<int>(events.Where(e => e.TeamId == team.TeamId && e.RuleIndex is not null).Select(e => e.RuleIndex!.Value));
        var ejected = new HashSet<string>(events.Where(e => e.TeamId == team.TeamId && e.Type == MatchEventType.RedCard).Select(e => e.PlayerId!), StringComparer.Ordinal);
        var injured = new HashSet<string>(events.Where(e => e.TeamId == team.TeamId && e.Type == MatchEventType.Injury).Select(e => e.PlayerId!), StringComparer.Ordinal);
        var active = new Dictionary<string, MatchPlayerInput>(team.Starters.ToDictionary(p => p.PlayerId, StringComparer.Ordinal));
        var used = new HashSet<string>(StringComparer.Ordinal);
        var changes = events.Count(e => e.TeamId == team.TeamId && e.Type == MatchEventType.Substitution);
        foreach (var existing in events.Where(e => e.TeamId == team.TeamId && e.Type == MatchEventType.Substitution))
        { active.Remove(existing.PlayerId!); used.Add(existing.RelatedPlayerId!); }
        foreach (var (rule, index) in rules.Select((rule, index) => (rule, index)))
        {
            if (rule is null) throw new ArgumentException("Rules cannot contain null.", nameof(rules));
            if (rule.Minute > minute || processed.Contains(index)) continue;
            processed.Add(index);
            var candidates = active.Values.Where(p => p.Position == rule.OutgoingPosition &&
                (rule.MaximumFitness is null || p.Fitness <= rule.MaximumFitness)).ToArray();
            if (candidates.Length == 0) { result.Add(Failure(team.TeamId, minute, index, AutomaticSubstitutionFailureReason.NoEligibleOutgoingPlayer)); continue; }
            var outgoing = candidates.FirstOrDefault(p => !ejected.Contains(p.PlayerId) && !injured.Contains(p.PlayerId));
            if (outgoing is null)
            {
                var reason = candidates.All(p => ejected.Contains(p.PlayerId)) ? AutomaticSubstitutionFailureReason.PlayerExpelled : AutomaticSubstitutionFailureReason.PlayerInjured;
                result.Add(Failure(team.TeamId, minute, index, reason)); continue;
            }
            if (changes >= substitutionLimit) { result.Add(Failure(team.TeamId, minute, index, AutomaticSubstitutionFailureReason.SubstitutionLimitReached)); continue; }
            MatchPlayerInput? incoming = null;
            if (rule.IncomingPlayerId is not null) incoming = team.Substitutes.FirstOrDefault(p => p.PlayerId == rule.IncomingPlayerId && !used.Contains(p.PlayerId) && !ejected.Contains(p.PlayerId) && !injured.Contains(p.PlayerId));
            else if (outgoing.Position == Position.Goalkeeper && rule.EmergencyGoalkeeperId is not null)
                incoming = team.Substitutes.FirstOrDefault(p => p.PlayerId == rule.EmergencyGoalkeeperId && !used.Contains(p.PlayerId) && !ejected.Contains(p.PlayerId) && !injured.Contains(p.PlayerId));
            else incoming = team.Substitutes.FirstOrDefault(p => p.Position == outgoing.Position && !used.Contains(p.PlayerId) && !ejected.Contains(p.PlayerId) && !injured.Contains(p.PlayerId));
            if (incoming is null)
            {
                var failure = outgoing.Position == Position.Goalkeeper && rule.EmergencyGoalkeeperId is null
                    ? AutomaticSubstitutionFailureReason.MissingEmergencyGoalkeeper : AutomaticSubstitutionFailureReason.NoAvailableSubstitute;
                result.Add(Failure(team.TeamId, minute, index, failure)); continue;
            }
            if (incoming.Position != outgoing.Position && !(outgoing.Position == Position.Goalkeeper && incoming.PlayerId == rule.EmergencyGoalkeeperId))
            { result.Add(Failure(team.TeamId, minute, index, incoming.Position == Position.Goalkeeper ? AutomaticSubstitutionFailureReason.InvalidPosition : AutomaticSubstitutionFailureReason.MissingEmergencyGoalkeeper)); continue; }
            var substitution = new MatchEvent(minute, MatchEventType.Substitution, team.TeamId, outgoing.PlayerId, incoming.PlayerId, ruleIndex: index);
            result.Add(substitution); active.Remove(outgoing.PlayerId); used.Add(incoming.PlayerId); changes++;
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static MatchEvent Failure(string teamId, int minute, int index, AutomaticSubstitutionFailureReason reason) =>
        new(minute, MatchEventType.SubstitutionFailed, teamId, ruleIndex: index, failureReason: reason);
}

/// <summary>A single event in match chronology; text should be localized by consumers.</summary>
public sealed class MatchEvent
{
    public MatchEvent(int minute, MatchEventType type, string teamId,
        string? playerId = null, string? relatedPlayerId = null, int injuryDays = 0,
        int? ruleIndex = null, AutomaticSubstitutionFailureReason? failureReason = null)
    {
        Minute = minute is >= 0 and <= 120 ? minute : throw new ArgumentOutOfRangeException(nameof(minute));
        Type = Enum.IsDefined(typeof(MatchEventType), type) ? type : throw new ArgumentOutOfRangeException(nameof(type));
        TeamId = string.IsNullOrWhiteSpace(teamId)
            ? throw new ArgumentException("A stable team id is required.", nameof(teamId))
            : teamId;
        PlayerId = playerId;
        RelatedPlayerId = relatedPlayerId;
        InjuryDays = type == MatchEventType.Injury
            ? injuryDays == 0 ? 1 : injuryDays is >= 1 and <= 30 ? injuryDays : throw new ArgumentOutOfRangeException(nameof(injuryDays))
            : injuryDays == 0 ? 0 : throw new ArgumentException("Only injuries can carry recovery days.", nameof(injuryDays));
        RuleIndex = type is MatchEventType.SubstitutionFailed or MatchEventType.Substitution
            ? ruleIndex is >= 0 ? ruleIndex : throw new ArgumentOutOfRangeException(nameof(ruleIndex))
            : ruleIndex is null ? null : throw new ArgumentException("Only failed substitutions carry a rule index.", nameof(ruleIndex));
        FailureReason = type == MatchEventType.SubstitutionFailed
            ? failureReason is not null && Enum.IsDefined(typeof(AutomaticSubstitutionFailureReason), failureReason.Value) ? failureReason : throw new ArgumentOutOfRangeException(nameof(failureReason))
            : failureReason is null ? null : throw new ArgumentException("Only failed substitutions carry a failure reason.", nameof(failureReason));
        if (type is MatchEventType.Goal or MatchEventType.OwnGoal or MatchEventType.YellowCard or
            MatchEventType.RedCard or MatchEventType.Injury && string.IsNullOrWhiteSpace(playerId))
            throw new ArgumentException("This event requires a player id.", nameof(playerId));
        if (type == MatchEventType.Substitution &&
            (string.IsNullOrWhiteSpace(playerId) || string.IsNullOrWhiteSpace(relatedPlayerId)))
            throw new ArgumentException("A substitution requires outgoing and incoming player ids.", nameof(relatedPlayerId));
        if (type == MatchEventType.SubstitutionFailed && (playerId is not null || relatedPlayerId is not null))
            throw new ArgumentException("A failed substitution does not identify players.");
    }

    public int Minute { get; }
    public MatchEventType Type { get; }
    public string TeamId { get; }
    public string? PlayerId { get; }
    public string? RelatedPlayerId { get; }
    /// <summary>Recovery days for a simple Slice injury; zero for other events.</summary>
    public int InjuryDays { get; }
    public int? RuleIndex { get; }
    public AutomaticSubstitutionFailureReason? FailureReason { get; }
}

public enum PlayerAvailability { Disponible, Tocando, Lesionado }

/// <summary>Simple Slice availability state with calendar-day recovery.</summary>
public sealed class PlayerInjuryState
{
    public PlayerInjuryState(PlayerAvailability availability = PlayerAvailability.Disponible, int daysRemaining = 0)
    {
        if (!Enum.IsDefined(typeof(PlayerAvailability), availability)) throw new ArgumentOutOfRangeException(nameof(availability));
        if (daysRemaining < 0 || daysRemaining > 30 || (availability == PlayerAvailability.Disponible) != (daysRemaining == 0))
            throw new ArgumentOutOfRangeException(nameof(daysRemaining));
        Availability = availability;
        DaysRemaining = daysRemaining;
    }
    public PlayerAvailability Availability { get; }
    public int DaysRemaining { get; }
    public PlayerInjuryState AdvanceDay() => DaysRemaining <= 1
        ? new PlayerInjuryState()
        : new PlayerInjuryState(Availability, DaysRemaining - 1);
}

public sealed class MatchScorer
{
    public MatchScorer(string teamId, string playerId, int minute, bool isOwnGoal = false, string? assisterId = null)
    {
        TeamId = string.IsNullOrWhiteSpace(teamId) ? throw new ArgumentException("A stable team id is required.", nameof(teamId)) : teamId;
        PlayerId = string.IsNullOrWhiteSpace(playerId) ? throw new ArgumentException("A stable player id is required.", nameof(playerId)) : playerId;
        Minute = minute is >= 0 and <= 120 ? minute : throw new ArgumentOutOfRangeException(nameof(minute));
        IsOwnGoal = isOwnGoal;
        if (isOwnGoal && assisterId is not null) throw new ArgumentException("An own goal cannot have an assister.", nameof(assisterId));
        if (assisterId is not null && (string.IsNullOrWhiteSpace(assisterId) || assisterId == playerId))
            throw new ArgumentException("An assister must be a different player with a stable id.", nameof(assisterId));
        AssisterId = assisterId;
    }

    public string TeamId { get; }
    public string PlayerId { get; }
    public int Minute { get; }
    public bool IsOwnGoal { get; }
    /// <summary>Player credited with the final pass; null for unassisted goals and own goals.</summary>
    public string? AssisterId { get; }
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

/// <summary>Penalty shootout tally, kept separate from match goals and player scoring statistics.</summary>
public sealed class PenaltyShootoutState
{
    private int _homeKicks, _awayKicks, _homeGoals, _awayGoals;
    public int HomeKicks => _homeKicks;
    public int AwayKicks => _awayKicks;
    public int HomeGoals => _homeGoals;
    public int AwayGoals => _awayGoals;
    public bool IsComplete { get; private set; }
    public string? WinnerTeamId { get; private set; }

    public void RecordKick(string teamId, bool scored, string homeTeamId, string awayTeamId)
    {
        if (IsComplete) throw new InvalidOperationException("The shootout is complete.");
        if (string.IsNullOrWhiteSpace(homeTeamId) || string.IsNullOrWhiteSpace(awayTeamId) || homeTeamId == awayTeamId)
            throw new ArgumentException("Distinct home and away team ids are required.");
        if (teamId != homeTeamId && teamId != awayTeamId) throw new ArgumentException("The team is not in this shootout.", nameof(teamId));
        var homeTurn = _homeKicks == _awayKicks;
        if ((homeTurn && teamId != homeTeamId) || (!homeTurn && teamId != awayTeamId))
            throw new InvalidOperationException("Shootout kicks must alternate, with the home team first.");

        if (teamId == homeTeamId) { _homeKicks++; if (scored) _homeGoals++; }
        else { _awayKicks++; if (scored) _awayGoals++; }

        if (_homeKicks >= 5 && _awayKicks >= 5 && _homeKicks == _awayKicks && _homeGoals != _awayGoals)
        { Finish(homeTeamId, awayTeamId); return; }
        if (_homeKicks < 5 || _awayKicks < 5)
        {
            var homeRemaining = 5 - _homeKicks;
            var awayRemaining = 5 - _awayKicks;
            if (_homeGoals > _awayGoals + awayRemaining) Finish(homeTeamId, awayTeamId);
            else if (_awayGoals > _homeGoals + homeRemaining) Finish(homeTeamId, awayTeamId);
        }
    }

    private void Finish(string homeTeamId, string awayTeamId)
    { IsComplete = true; WinnerTeamId = _homeGoals > _awayGoals ? homeTeamId : awayTeamId; }
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

/// <summary>How the shooter received the ball before the attempt.</summary>
public enum ShotAssistType { None, ThroughBall, Cross, Cutback, LongPass, Rebound }

/// <summary>Open-play context for an expected-goals estimate.</summary>
public enum ShotSituation { OpenPlay, CounterAttack, SetPiece, Penalty }

/// <summary>Geometry and context of a shot; deliberately contains no shooter identity or attributes.</summary>
public sealed class ShotOpportunity
{
    public ShotOpportunity(double distanceMetres, double angleDegrees, ShotAssistType assistType,
        double pressure, ShotSituation situation)
    {
        if (!double.IsFinite(distanceMetres) || distanceMetres < 0 || distanceMetres > 60)
            throw new ArgumentOutOfRangeException(nameof(distanceMetres));
        if (!double.IsFinite(angleDegrees) || angleDegrees < 0 || angleDegrees > 90)
            throw new ArgumentOutOfRangeException(nameof(angleDegrees));
        if (!double.IsFinite(pressure) || pressure < 0 || pressure > 1)
            throw new ArgumentOutOfRangeException(nameof(pressure));
        if (!Enum.IsDefined(typeof(ShotAssistType), assistType)) throw new ArgumentOutOfRangeException(nameof(assistType));
        if (!Enum.IsDefined(typeof(ShotSituation), situation)) throw new ArgumentOutOfRangeException(nameof(situation));
        DistanceMetres = distanceMetres; AngleDegrees = angleDegrees; AssistType = assistType;
        Pressure = pressure; Situation = situation;
    }
    public double DistanceMetres { get; }
    public double AngleDegrees { get; }
    public ShotAssistType AssistType { get; }
    public double Pressure { get; }
    public ShotSituation Situation { get; }
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

    /// <summary>Creates the compact result used for simulated matches outside the user's fixture.</summary>
    public HeadlessMatchResult ToHeadlessResult() => new(this);
}

/// <summary>Compact match outcome without the full event chronology or team statistics.</summary>
public sealed class HeadlessMatchResult
{
    internal HeadlessMatchResult(MatchReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));
        Score = report.Score;
        Goalscorers = report.Goalscorers;
        Assists = Array.AsReadOnly(report.Goalscorers.Where(goal => goal.AssisterId is not null)
            .Select(goal => new MatchAssist(goal.TeamId, goal.AssisterId!, goal.Minute)).ToArray());
        Cards = report.Cards;
        Injuries = report.Injuries;
        HomeForm = report.Home.Ratings;
        AwayForm = report.Away.Ratings;
    }

    public MatchScore Score { get; }
    public IReadOnlyList<MatchScorer> Goalscorers { get; }
    public IReadOnlyList<MatchAssist> Assists { get; }
    public IReadOnlyList<MatchEvent> Cards { get; }
    public IReadOnlyList<MatchEvent> Injuries { get; }
    /// <summary>Player match ratings consumed by the career layer to update form.</summary>
    public IReadOnlyList<MatchPlayerRating> HomeForm { get; }
    public IReadOnlyList<MatchPlayerRating> AwayForm { get; }
}

public sealed class MatchAssist
{
    public MatchAssist(string teamId, string playerId, int minute)
    {
        TeamId = string.IsNullOrWhiteSpace(teamId) ? throw new ArgumentException("A stable team id is required.", nameof(teamId)) : teamId;
        PlayerId = string.IsNullOrWhiteSpace(playerId) ? throw new ArgumentException("A stable player id is required.", nameof(playerId)) : playerId;
        Minute = minute is >= 0 and <= 120 ? minute : throw new ArgumentOutOfRangeException(nameof(minute));
    }
    public string TeamId { get; }
    public string PlayerId { get; }
    public int Minute { get; }
}
