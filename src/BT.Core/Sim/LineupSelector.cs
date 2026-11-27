using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core.Domain;

namespace BT.Core.Sim;

/// <summary>Deterministic greedy assignment of eligible players to formation positions.</summary>
public sealed class LineupSelector
{
    private readonly OvrCalculator _ovr;
    private readonly LineupSelectionWeights _weights;

    public LineupSelector(OvrCalculator ovr, LineupSelectionWeights weights)
    {
        _ovr = ovr ?? throw new ArgumentNullException(nameof(ovr));
        _weights = weights ?? throw new ArgumentNullException(nameof(weights));
    }

    public LineupSelection Select(IReadOnlyList<LineupCandidate> candidates,
        IReadOnlyList<LineupSlot> slots)
    {
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (slots is null) throw new ArgumentNullException(nameof(slots));
        if (slots.Count == 0 || slots.Any(slot => slot is null)) throw new ArgumentException("At least one valid slot is required.", nameof(slots));
        if (candidates.Any(candidate => candidate is null)) throw new ArgumentException("Candidates cannot contain null entries.", nameof(candidates));
        if (candidates.Select(candidate => candidate.Player.Id).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
            throw new ArgumentException("Candidate player ids must be unique.", nameof(candidates));
        if (slots.Count(candidate => candidate.Position == Position.Goalkeeper) != 1)
            throw new ArgumentException("The formation must contain exactly one goalkeeper slot.", nameof(slots));

        var edges = new List<ScoredEdge>();
        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        foreach (var candidate in candidates)
        {
            if (candidate.IsInjured || candidate.IsSuspended || !candidate.Player.Positions.Contains(slots[slotIndex].Position)) continue;
            var familiarity = candidate.GetFamiliarity(slots[slotIndex].Position);
            var ovr = _ovr.Calculate(candidate.Player, slots[slotIndex].Role, familiarity);
            var calculatedFitFactor = 1.0 - (_weights.FitnessNeutral - candidate.Fitness) * _weights.FitnessDeclinePerPoint;
            var fitFactor = candidate.Fitness >= _weights.FitnessNeutral ? 1.0 :
                calculatedFitFactor < _weights.FitnessMinimumFactor ? _weights.FitnessMinimumFactor : calculatedFitFactor;
            var moraleFactor = _weights.MoraleBaseFactor + candidate.Morale / 100.0 * _weights.MoraleRangeFactor;
            var ovrPoints = ovr * _weights.OvrWeight;
            var familiarityPoints = familiarity * _weights.FamiliarityWeight;
            var fitPoints = ovr * (fitFactor - 1.0) * _weights.FitnessWeight;
            var moralePoints = ovr * (moraleFactor - 1.0) * _weights.MoraleWeight;
            var loadPoints = candidate.RecentLoad * _weights.RecentLoadWeight;
            edges.Add(new ScoredEdge(slotIndex, candidate, ovr, familiarity, ovrPoints,
                familiarityPoints, fitPoints, moralePoints, loadPoints,
                ovrPoints + familiarityPoints + fitPoints + moralePoints + loadPoints));
        }

        // Greedy edge selection is stable: descending score, then ordinal player id, then slot index.
        var ordered = edges.OrderByDescending(edge => edge.Total)
            .ThenBy(edge => edge.Candidate.Player.Id, StringComparer.Ordinal)
            .ThenBy(edge => edge.SlotIndex).ToArray();
        var assignments = new ScoredEdge[slots.Count];
        var usedPlayers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in ordered)
        {
            if (assignments[edge.SlotIndex] is not null || !usedPlayers.Add(edge.Candidate.Player.Id)) continue;
            assignments[edge.SlotIndex] = edge;
        }
        if (assignments.Any(edge => edge is null))
        {
            var missing = Enumerable.Range(0, slots.Count).Where(index => assignments[index] is null)
                .Select(index => slots[index].Position.ToString());
            throw new InvalidOperationException("Insufficient eligible positional coverage for: " + string.Join(", ", missing));
        }

        var decisions = assignments.Select((edge, index) => new LineupDecision(index, slots[index], edge.Candidate.Player.Id,
            edge.Ovr, edge.Familiarity, edge.OvrPoints, edge.FamiliarityPoints, edge.FitPoints,
            edge.MoralePoints, edge.LoadPoints, edge.Total)).ToArray();
        return new LineupSelection(Array.AsReadOnly(decisions), decisions.Sum(item => item.TotalScore));
    }

    private sealed class ScoredEdge
    {
        public ScoredEdge(int slotIndex, LineupCandidate candidate, int ovr, int familiarity,
            double ovrPoints, double familiarityPoints, double fitPoints, double moralePoints,
            double loadPoints, double total)
        { SlotIndex = slotIndex; Candidate = candidate; Ovr = ovr; Familiarity = familiarity; OvrPoints = ovrPoints;
          FamiliarityPoints = familiarityPoints; FitPoints = fitPoints; MoralePoints = moralePoints; LoadPoints = loadPoints; Total = total; }
        public int SlotIndex { get; } public LineupCandidate Candidate { get; } public int Ovr { get; }
        public int Familiarity { get; } public double OvrPoints { get; } public double FamiliarityPoints { get; }
        public double FitPoints { get; } public double MoralePoints { get; } public double LoadPoints { get; } public double Total { get; }
    }
}

public sealed class LineupSlot
{
    public LineupSlot(Position position, Role role)
    { Position = Enum.IsDefined(typeof(Position), position) ? position : throw new ArgumentOutOfRangeException(nameof(position));
      Role = Enum.IsDefined(typeof(Role), role) ? role : throw new ArgumentOutOfRangeException(nameof(role)); }
    public Position Position { get; } public Role Role { get; }
}

/// <summary>Match-day state required by the section 21 lineup score.</summary>
public sealed class LineupCandidate
{
    private readonly IReadOnlyDictionary<Position, int> _familiarity;
    public LineupCandidate(Player player, int fitness, int morale, int recentLoad,
        IReadOnlyDictionary<Position, int> positionalFamiliarity, bool isSuspended = false, bool isInjured = false)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Fitness = fitness is >= 0 and <= 100 ? fitness : throw new ArgumentOutOfRangeException(nameof(fitness));
        Morale = morale is >= 0 and <= 100 ? morale : throw new ArgumentOutOfRangeException(nameof(morale));
        RecentLoad = recentLoad is >= 0 and <= 100 ? recentLoad : throw new ArgumentOutOfRangeException(nameof(recentLoad));
        if (positionalFamiliarity is null) throw new ArgumentNullException(nameof(positionalFamiliarity));
        if (positionalFamiliarity.Any(pair => !Enum.IsDefined(typeof(Position), pair.Key) || pair.Value is < 0 or > 100))
            throw new ArgumentException("Positional familiarity must be in [0,100].", nameof(positionalFamiliarity));
        _familiarity = new System.Collections.ObjectModel.ReadOnlyDictionary<Position, int>(new Dictionary<Position, int>(positionalFamiliarity));
        IsSuspended = isSuspended; IsInjured = isInjured;
    }
    public Player Player { get; } public int Fitness { get; } public int Morale { get; } public int RecentLoad { get; }
    public bool IsSuspended { get; } public bool IsInjured { get; }
    internal int GetFamiliarity(Position position) => _familiarity.TryGetValue(position, out var value)
        ? value : throw new InvalidOperationException($"Player '{Player.Id}' has no familiarity value for '{position}'.");
}

/// <summary>Versioned lineup score weights. Values are score points per input unit.</summary>
public sealed class LineupSelectionWeights
{
    public LineupSelectionWeights(double ovrWeight, double familiarityWeight, double fitnessWeight,
        double moraleWeight, double recentLoadWeight, double fitnessNeutral, double fitnessDeclinePerPoint,
        double fitnessMinimumFactor, double moraleBaseFactor, double moraleRangeFactor)
    {
        var values = new[] { ovrWeight, familiarityWeight, fitnessWeight, moraleWeight, recentLoadWeight,
            fitnessNeutral, fitnessDeclinePerPoint, fitnessMinimumFactor, moraleBaseFactor, moraleRangeFactor };
        if (values.Any(value => !double.IsFinite(value)) || ovrWeight < 0 || familiarityWeight < 0 ||
            fitnessWeight < 0 || moraleWeight < 0 || recentLoadWeight > 0 || fitnessNeutral is < 0 or > 100 ||
            fitnessDeclinePerPoint < 0 || fitnessMinimumFactor is <= 0 or > 1 || moraleBaseFactor <= 0 || moraleRangeFactor < 0)
            throw new ArgumentOutOfRangeException(nameof(ovrWeight), "Lineup weights and modifiers are invalid.");
        OvrWeight = ovrWeight; FamiliarityWeight = familiarityWeight; FitnessWeight = fitnessWeight; MoraleWeight = moraleWeight;
        RecentLoadWeight = recentLoadWeight; FitnessNeutral = fitnessNeutral; FitnessDeclinePerPoint = fitnessDeclinePerPoint;
        FitnessMinimumFactor = fitnessMinimumFactor; MoraleBaseFactor = moraleBaseFactor; MoraleRangeFactor = moraleRangeFactor;
    }
    public double OvrWeight { get; } public double FamiliarityWeight { get; } public double FitnessWeight { get; }
    public double MoraleWeight { get; } public double RecentLoadWeight { get; } public double FitnessNeutral { get; }
    public double FitnessDeclinePerPoint { get; } public double FitnessMinimumFactor { get; }
    public double MoraleBaseFactor { get; } public double MoraleRangeFactor { get; }
}

/// <summary>Selection output and auditable, slot-ordered decision log.</summary>
public sealed class LineupSelection
{
    internal LineupSelection(IReadOnlyList<LineupDecision> decisions, double totalScore) { Decisions = decisions; TotalScore = totalScore; }
    public IReadOnlyList<LineupDecision> Decisions { get; } public double TotalScore { get; }
}

public sealed class LineupDecision
{
    internal LineupDecision(int slotIndex, LineupSlot slot, string playerId, int ovr, int familiarity,
        double ovrPoints, double familiarityPoints, double fitnessPoints, double moralePoints, double loadPoints, double totalScore)
    { SlotIndex = slotIndex; Slot = slot; PlayerId = playerId; Ovr = ovr; Familiarity = familiarity; OvrPoints = ovrPoints;
      FamiliarityPoints = familiarityPoints; FitnessPoints = fitnessPoints; MoralePoints = moralePoints; LoadPoints = loadPoints; TotalScore = totalScore; }
    public int SlotIndex { get; } public LineupSlot Slot { get; } public string PlayerId { get; } public int Ovr { get; }
    public int Familiarity { get; } public double OvrPoints { get; } public double FamiliarityPoints { get; }
    public double FitnessPoints { get; } public double MoralePoints { get; } public double LoadPoints { get; } public double TotalScore { get; }
}
