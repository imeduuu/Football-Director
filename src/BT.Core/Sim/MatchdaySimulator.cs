using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core;
using BT.Core.Domain;
using BT.Core.Rng;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Sim;

/// <summary>Seeded interval simulator. Both public modes execute this same authoritative core.</summary>
public sealed class MatchdaySimulator : BT.Core.Competition.IMatchdaySimulator
{
    private readonly ParamsConfig _parameters;
    private readonly FormationCatalog _formations;

    public MatchdaySimulator(ParamsConfig parameters, FormationCatalog formations)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _formations = formations ?? throw new ArgumentNullException(nameof(formations));
    }

    public MatchReport SimulateFull(MatchInput input) => SimulateCore(input);

    public HeadlessMatchResult SimulateHeadless(MatchInput input) => SimulateCore(input).ToHeadlessResult();

    private MatchReport SimulateCore(MatchInput input)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        var rng = new Pcg32(input.Seed, (ulong)input.EngineVersion);
        var homePoints = GetPoints(input.HomeTeam);
        var awayPoints = GetPoints(input.AwayTeam);
        var homeMid = Strength(input.HomeTeam, homePoints, 35, 70);
        var awayMid = Strength(input.AwayTeam, awayPoints, 35, 70);
        var homePossession = SimulationFormulas.CalculatePossession(homeMid, input.HomeTeam.Tactics.Passing,
            awayMid, input.AwayTeam.Tactics.Passing, _parameters);
        var homeGrid = new int[9]; var awayGrid = new int[9];
        var shots = new int[2]; var onTarget = new int[2]; var xg = new double[2];
        var events = new List<MatchEvent>();
        var scorers = new List<MatchScorer>();
        var state = new MatchState(input.HomeTeam.TeamId, input.AwayTeam.TeamId, _parameters.MatchSimulation);

        for (var interval = 0; interval < 18; interval++)
        {
            state.AdvanceInterval();
            var minute = state.CurrentMinute;
            ResolveAttack(0, input.HomeTeam, input.AwayTeam, homePoints, awayPoints, homeGrid, shots, onTarget, xg, scorers, events, state, minute, ref rng);
            ResolveAttack(1, input.AwayTeam, input.HomeTeam, awayPoints, homePoints, awayGrid, shots, onTarget, xg, scorers, events, state, minute, ref rng);
            if (interval == 8)
            {
                state.CompleteRegularPeriod();
                ResolveAddedTime(state, 0, input, homePoints, awayPoints, homeGrid, awayGrid, shots, onTarget, xg, scorers, events, ref rng);
                state.CompleteAddedTime(); state.StartSecondHalf();
            }
        }
        state.CompleteRegularPeriod();
        ResolveAddedTime(state, 1, input, homePoints, awayPoints, homeGrid, awayGrid, shots, onTarget, xg, scorers, events, ref rng);
        state.CompleteAddedTime();
        var regulationScore = state.Score;

        PenaltyShootoutState? shootout = null;
        if (input.IsKnockout && state.Score.HomeGoals == state.Score.AwayGoals)
        {
            state.StartExtraTime();
            for (var interval = 0; interval < 6; interval++)
            {
                if (state.Phase == MatchPhase.ExtraTimeHalfTime) state.StartExtraTimeSecondHalf();
                var eventMinute = state.CurrentMinute + 5;
                ResolveAttack(0, input.HomeTeam, input.AwayTeam, homePoints, awayPoints, homeGrid, shots, onTarget, xg, scorers, events, state, eventMinute, ref rng);
                ResolveAttack(1, input.AwayTeam, input.HomeTeam, awayPoints, homePoints, awayGrid, shots, onTarget, xg, scorers, events, state, eventMinute, ref rng);
                state.AdvanceExtraTimeInterval();
            }
            if (state.Score.HomeGoals == state.Score.AwayGoals)
                shootout = ResolveShootout(input, state, ref rng, events);
        }

        var score = state.Score;
        var homeRatings = RateTeam(input.HomeTeam, scorers, events, score, true, state.CurrentMinute);
        var awayRatings = RateTeam(input.AwayTeam, scorers, events, score, false, state.CurrentMinute);
        var homeReport = new MatchTeamReport(homePossession, shots[0], onTarget[0], xg[0], new MatchZoneMap(homeGrid), homeRatings);
        var awayReport = new MatchTeamReport(100.0 - homePossession, shots[1], onTarget[1], xg[1], new MatchZoneMap(awayGrid), awayRatings);
        events = events.OrderBy(matchEvent => matchEvent.Minute)
            .ThenBy(matchEvent => matchEvent.TeamId, StringComparer.Ordinal)
            .ThenBy(matchEvent => matchEvent.Type).ToList();
        scorers.Sort((left, right) => left.Minute != right.Minute
            ? left.Minute.CompareTo(right.Minute) : StringComparer.Ordinal.Compare(left.PlayerId, right.PlayerId));
        return new MatchReport(score, scorers, events, homeReport, awayReport, state.CurrentMinute, shootout,
            input.Seed, input.EngineVersion, regulationScore);
    }

    private void ResolveAddedTime(MatchState state, int half, MatchInput input,
        IReadOnlyList<FormationPoint> homePoints, IReadOnlyList<FormationPoint> awayPoints,
        int[] homeGrid, int[] awayGrid, int[] shots, int[] onTarget, double[] xg,
        List<MatchScorer> scorers, List<MatchEvent> events, ref Pcg32 rng)
    {
        var added = half == 0 ? state.FirstHalfAddedTime : state.SecondHalfAddedTime;
        var initialMinute = state.CurrentMinute;
        while (state.CurrentMinute < initialMinute + added)
        {
            state.AdvanceAddedTimeMinute();
            ResolveAttack(0, input.HomeTeam, input.AwayTeam, homePoints, awayPoints, homeGrid, shots, onTarget, xg, scorers, events, state, state.CurrentMinute, ref rng);
            ResolveAttack(1, input.AwayTeam, input.HomeTeam, awayPoints, homePoints, awayGrid, shots, onTarget, xg, scorers, events, state, state.CurrentMinute, ref rng);
            added = half == 0 ? state.FirstHalfAddedTime : state.SecondHalfAddedTime;
        }
    }

    private void ResolveAttack(int side, MatchTeamInput attack, MatchTeamInput defend,
        IReadOnlyList<FormationPoint> attackPoints, IReadOnlyList<FormationPoint> defendPoints,
        int[] zoneMap, int[] shots, int[] onTarget, double[] xg, List<MatchScorer> scorers,
        List<MatchEvent> events, MatchState state, int minute, ref Pcg32 rng)
    {
        var attackerIndex = ChooseOutfieldIndex(attack, state, ref rng);
        var defenderIndex = ChooseOutfieldIndex(defend, state, ref rng);
        var attacker = attack.Starters[attackerIndex];
        var defender = defend.Starters[defenderIndex];
        var point = attackPoints[attackerIndex];
        var zone = Zone(point);
        zoneMap[(int)zone]++;
        var contact = rng.NextInt(0, 100);
        var incidents = SliceIncidentResolver.Resolve(minute, attack, defend, zone, attackerIndex, defenderIndex,
            50, contact, ref rng);
        foreach (var incident in incidents)
        {
            state.RecordEvent(incident);
            events.Add(incident);
        }
        var atk = (attacker.EffectiveAttributes[PlayerAttribute.Vision] + attacker.EffectiveAttributes[PlayerAttribute.Dribbling] +
                   attacker.EffectiveAttributes[PlayerAttribute.Pace] + attacker.EffectiveAttributes[PlayerAttribute.Finishing]) / 4.0;
        var def = (defender.EffectiveAttributes[PlayerAttribute.Marking] + defender.EffectiveAttributes[PlayerAttribute.Tackling] +
                   defender.EffectiveAttributes[PlayerAttribute.Positioning] + defender.EffectiveAttributes[PlayerAttribute.Strength]) / 4.0;
        var pressure = defend.Tactics.Pressing switch { PressingStyle.Gegenpressing => 1.12, PressingStyle.High => 1.06, PressingStyle.Medium => 1.0, _ => 0.94 };
        var defensiveStrength = def * pressure;
        var progression = atk / (defensiveStrength < 1.0 ? 1.0 : defensiveStrength);
        progression *= attack.Tactics.Mentality switch
        {
            TacticalMentality.VeryDefensive => 0.94, TacticalMentality.Defensive => 0.97,
            TacticalMentality.Balanced => 1.0, TacticalMentality.Attacking => 1.03,
            TacticalMentality.Overload => 1.06, _ => throw new ArgumentOutOfRangeException()
        };
        var lineFactor = defend.Tactics.DefensiveLine switch
        {
            DefensiveLine.Deep => 0.96, DefensiveLine.Normal => 1.0,
            DefensiveLine.High => 1.04, _ => throw new ArgumentOutOfRangeException()
        };
        progression /= lineFactor;
        if (!SimulationFormulas.ProgressionCreatesShot(progression, 1.15, 0.25, ref rng)) return;

        shots[side]++;
        var rawDistance = 30.0 - point.Y * 0.22 + rng.NextDouble() * 8.0;
        var distance = rawDistance < 7.0 ? 7.0 : rawDistance;
        var angle = rng.NextDouble() * 55.0;
        var assistType = rng.NextInt(0, 4) switch { 0 => ShotAssistType.ThroughBall, 1 => ShotAssistType.Cross, 2 => ShotAssistType.Cutback, _ => ShotAssistType.None };
        var quality = SimulationFormulas.CalculateExpectedGoals(new ShotOpportunity(distance, angle, assistType, pressure - 0.94, ShotSituation.OpenPlay));
        xg[side] += quality;
        var keeper = defend.Starters.First(player => player.Position == Position.Goalkeeper);
        var probability = SimulationFormulas.CalculateGoalProbability(quality,
            attacker.EffectiveAttributes[PlayerAttribute.Finishing], attacker.EffectiveAttributes[PlayerAttribute.Composure],
            keeper.EffectiveAttributes[PlayerAttribute.Goalkeeping], keeper.EffectiveAttributes[PlayerAttribute.Agility], _parameters);
        var scored = rng.NextDouble() < probability;
        // A shot counts on target when converted; otherwise its target classification is not inferred.
        if (scored) onTarget[side]++;
        if (!scored) return;
        var ownGoal = rng.NextDouble() < 0.015;
        var scorerTeam = attack.TeamId;
        var goalEvent = new MatchEvent(minute, ownGoal ? MatchEventType.OwnGoal : MatchEventType.Goal,
            ownGoal ? defend.TeamId : attack.TeamId, ownGoal ? defender.PlayerId : attacker.PlayerId);
        state.RecordEvent(goalEvent); events.Add(goalEvent);
        string? assist = null;
        if (!ownGoal)
        {
            assist = attack.Starters[ChooseOutfieldIndex(attack, state, ref rng)].PlayerId;
            if (assist == attacker.PlayerId) assist = null;
        }
        scorers.Add(new MatchScorer(scorerTeam, ownGoal ? defender.PlayerId : attacker.PlayerId, minute, ownGoal, assist));

    }

    private IReadOnlyList<FormationPoint> GetPoints(MatchTeamInput team)
    {
        var formation = _formations[team.Tactics.FormationId];
        var byPosition = formation.Points.GroupBy(point => point.Position).ToDictionary(group => group.Key, group => group.ToArray());
        var selectedCounts = team.Starters.GroupBy(player => player.Position)
            .ToDictionary(group => group.Key, group => group.Count());
        if (byPosition.Any(pair => !selectedCounts.TryGetValue(pair.Key, out var count) || count != pair.Value.Length) ||
            selectedCounts.Any(pair => !byPosition.TryGetValue(pair.Key, out var points) || points.Length != pair.Value))
            throw new InvalidOperationException($"The starting lineup does not match formation '{formation.Id}'.");
        var offsets = new Dictionary<Position, int>();
        var result = new List<FormationPoint>(11);
        foreach (var player in team.Starters)
        {
            var candidates = byPosition[player.Position];
            offsets.TryGetValue(player.Position, out var offset);
            var point = candidates[offset];
            var widthFactor = team.Tactics.Width switch
            {
                TeamWidth.Narrow => 0.85, TeamWidth.Normal => 1.0, TeamWidth.Wide => 1.15,
                _ => throw new ArgumentOutOfRangeException()
            };
            var adjustedX = 50 + (point.X - 50) * widthFactor;
            if (adjustedX < 0) adjustedX = 0;
            if (adjustedX > 100) adjustedX = 100;
            result.Add(new FormationPoint(point.Position, (int)adjustedX, point.Y));
            offsets[player.Position] = offset + 1;
        }
        return result;
    }

    private static double Strength(MatchTeamInput team, IReadOnlyList<FormationPoint> points, int minY, int maxY)
    {
        var values = team.Starters.Select((player, index) => (player, point: points[index]))
            .Where(item => item.point.Y >= minY && item.point.Y < maxY)
            .Select(item => (item.player.EffectiveAttributes[PlayerAttribute.Passing] + item.player.EffectiveAttributes[PlayerAttribute.Vision] + item.player.EffectiveAttributes[PlayerAttribute.Positioning]) / 3.0)
            .ToArray();
        return values.Length == 0 ? team.Starters.Where(player => player.Position != Position.Goalkeeper)
            .Average(player => player.EffectiveAttributes[PlayerAttribute.Passing]) : values.Average();
    }

    private static MatchZone Zone(FormationPoint point)
    {
        var row = point.Y < 35 ? 0 : point.Y < 70 ? 1 : 2;
        var col = point.X < 33 ? 0 : point.X < 67 ? 1 : 2;
        return (MatchZone)(row * 3 + col);
    }

    private PenaltyShootoutState ResolveShootout(MatchInput input, MatchState match,
        ref Pcg32 rng, List<MatchEvent> events)
    {
        match.StartPenaltyShootout();
        var tally = new PenaltyShootoutState();
        var keeperHome = input.HomeTeam.Starters.First(player => player.Position == Position.Goalkeeper);
        var keeperAway = input.AwayTeam.Starters.First(player => player.Position == Position.Goalkeeper);
        var attempts = 0;
        while (!tally.IsComplete)
        {
            if (++attempts > 10000) throw new InvalidOperationException("Penalty shootout exceeded its deterministic safety bound.");
            var homeKick = tally.HomeKicks == tally.AwayKicks;
            var team = homeKick ? input.HomeTeam : input.AwayTeam;
            var kickIndex = homeKick ? tally.HomeKicks : tally.AwayKicks;
            var expelled = events.Where(matchEvent => matchEvent.TeamId == team.TeamId && matchEvent.Type == MatchEventType.RedCard)
                .Select(matchEvent => matchEvent.PlayerId).ToHashSet(StringComparer.Ordinal);
            var outfield = team.Starters.Where(player => player.Position != Position.Goalkeeper && !expelled.Contains(player.PlayerId)).ToArray();
            if (outfield.Length == 0) throw new InvalidOperationException("A team without an eligible penalty taker cannot take part in a shootout.");
            var shooter = outfield[kickIndex % outfield.Length];
            var defendingKeeper = homeKick ? keeperAway : keeperHome;
            var scored = rng.NextDouble() < PenaltyProbability(shooter, defendingKeeper);
            tally.RecordKick(team.TeamId, scored, input.HomeTeam.TeamId, input.AwayTeam.TeamId);
            if (!scored) events.Add(new MatchEvent(120, MatchEventType.PenaltyMiss, team.TeamId, shooter.PlayerId));
        }
        match.CompletePenaltyShootout();
        return tally;
    }

    private double PenaltyProbability(MatchPlayerInput shooter, MatchPlayerInput goalkeeper)
    {
        var xg = SimulationFormulas.CalculateExpectedGoals(new ShotOpportunity(11, 0,
            ShotAssistType.None, 0, ShotSituation.Penalty));
        return SimulationFormulas.CalculateGoalProbability(xg,
            shooter.EffectiveAttributes[PlayerAttribute.Finishing], shooter.EffectiveAttributes[PlayerAttribute.Composure],
            goalkeeper.EffectiveAttributes[PlayerAttribute.Goalkeeping], goalkeeper.EffectiveAttributes[PlayerAttribute.Agility], _parameters);
    }

    private static int ChooseOutfieldIndex(MatchTeamInput team, MatchState state, ref Pcg32 rng)
    {
        var expelled = state.Events.Where(matchEvent => matchEvent.TeamId == team.TeamId &&
                matchEvent.Type == MatchEventType.RedCard).Select(matchEvent => matchEvent.PlayerId)
            .ToHashSet(StringComparer.Ordinal);
        var indices = team.Starters.Select((player, index) => (player, index))
            .Where(item => item.player.Position != Position.Goalkeeper && !expelled.Contains(item.player.PlayerId))
            .Select(item => item.index).ToArray();
        if (indices.Length == 0) throw new InvalidOperationException("A team without an eligible outfield player cannot continue the match.");
        return indices[rng.NextInt(0, indices.Length)];
    }

    private IReadOnlyList<MatchPlayerRating> RateTeam(MatchTeamInput team, IReadOnlyList<MatchScorer> goals,
        IReadOnlyList<MatchEvent> events, MatchScore score, bool home, int duration)
    {
        var difference = home ? score.HomeGoals - score.AwayGoals : score.AwayGoals - score.HomeGoals;
        var result = difference > 0 ? 1 : difference < 0 ? -1 : 0;
        return team.Starters.Select(player =>
        {
            var playerGoals = goals.Count(goal => !goal.IsOwnGoal && goal.TeamId == team.TeamId && goal.PlayerId == player.PlayerId);
            var assists = goals.Count(goal => goal.TeamId == team.TeamId && goal.AssisterId == player.PlayerId);
            var cardsYellow = events.Count(e => e.TeamId == team.TeamId && e.PlayerId == player.PlayerId && e.Type == MatchEventType.YellowCard);
            var cardsRed = events.Count(e => e.TeamId == team.TeamId && e.PlayerId == player.PlayerId && e.Type == MatchEventType.RedCard);
            var dismissedAt = events.Where(e => e.TeamId == team.TeamId && e.PlayerId == player.PlayerId && e.Type == MatchEventType.RedCard)
                .Select(e => e.Minute).DefaultIfEmpty(duration).Min();
            var minutes = dismissedAt < duration ? dismissedAt : duration;
            var rating = SimulationFormulas.CalculatePlayerRating(minutes, playerGoals, assists, 0, _parameters.PlayerRating.PassAccuracyTarget, 0, 0,
                home ? score.AwayGoals == 0 : score.HomeGoals == 0, home ? score.AwayGoals : score.HomeGoals,
                0, 0, cardsYellow, cardsRed, 0, 0, result, player.Position, _parameters.PlayerRating);
            return new MatchPlayerRating(player.PlayerId, rating, minutes, true);
        }).ToArray();
    }
}
