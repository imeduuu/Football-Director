using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core.Rng;

namespace BT.Core.Domain;

public enum SquadProfile { Favorite, UpperHalf, LowerHalf, RelegationCandidate }

/// <summary>Builds deterministic Slice and MVP squads with age and formation coverage constraints.</summary>
public sealed class SquadGenerator
{
    private static readonly IReadOnlyDictionary<int, (int Goalkeepers, int Defenders, int Midfielders, int Forwards)> Sizes =
        new Dictionary<int, (int, int, int, int)>
        {
            [18] = (2, 6, 6, 4),
            [19] = (2, 6, 6, 5),
            [20] = (3, 6, 6, 5),
            [21] = (3, 6, 7, 5),
            [22] = (3, 7, 7, 5),
            [25] = (3, 7, 10, 5)
        };

    private readonly AttributeGenerator _attributes;
    private readonly OvrCalculator _ovrCalculator;
    private readonly BT.Core.SquadGenerationParams _parameters;

    public SquadGenerator(AttributeGenerator attributes, OvrCalculator ovrCalculator, BT.Core.SquadGenerationParams parameters)
    {
        _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        _ovrCalculator = ovrCalculator ?? throw new ArgumentNullException(nameof(ovrCalculator));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
    }

    public IReadOnlyList<Player> Generate(int size, string clubId, int divisionLevel, double clubReputation,
        string nationality, SquadProfile profile, ulong seed, ulong sequence = 0) =>
        GenerateDetailed(size, clubId, divisionLevel, clubReputation, nationality, profile, seed, sequence).Players;

    public GeneratedSquad GenerateDetailed(int size, string clubId, int divisionLevel, double clubReputation,
        string nationality, SquadProfile profile, ulong seed, ulong sequence = 0)
    {
        if (!Sizes.TryGetValue(size, out var composition))
            throw new ArgumentOutOfRangeException(nameof(size), "Squad size must be 18–22 or 25.");
        if (string.IsNullOrWhiteSpace(clubId)) throw new ArgumentException("A club id is required.", nameof(clubId));
        if (string.IsNullOrWhiteSpace(nationality)) throw new ArgumentException("A nationality is required.", nameof(nationality));

        if (!_parameters.Profiles.TryGetValue(profile, out var bands)) throw new ArgumentOutOfRangeException(nameof(profile));
        var (meanMin, meanMax, starterMin, starterMax, benchMin, benchMax) =
            (bands.MeanMinimum, bands.MeanMaximum, bands.StarterMinimum, bands.StarterMaximum, bands.BenchMinimum, bands.BenchMaximum);
        var ages = CreateAges(size);
        var targetMean = (meanMin + meanMax) / 2.0 +
            (clubReputation - _parameters.ReferenceReputation) * _parameters.ReputationMeanShift -
            (divisionLevel - _parameters.ReferenceDivisionLevel) * _parameters.DivisionMeanShift;
        targetMean = targetMean < meanMin ? meanMin : targetMean > meanMax ? meanMax : targetMean;
        var adjustment = targetMean - _parameters.ReferenceOvr;
        var lastMean = double.NaN;
        var lastStarterCoverage = 0.0;
        var lastBenchCoverage = 0.0;
        for (var attempt = 0; attempt < _parameters.MaxAttempts; attempt++)
        {
            var positions = CreatePositions(composition);
            var rng = new Pcg32(seed, unchecked(sequence + (ulong)attempt));
            Shuffle(positions, ref rng);
            var candidateAges = new List<int>(ages);
            Shuffle(candidateAges, ref rng);
            var squad = new List<Player>(size);
            var ovr = new int[size];
            var hasDefensiveMidfielderCover = false;
            for (var i = 0; i < size; i++)
            {
                var position = positions[i];
                var generated = _attributes.Generate(divisionLevel, clubReputation, candidateAges[i], RoleFor(position), ref rng,
                    meanAdjustment: adjustment);
                ovr[i] = generated.Ovr;
                var visible = new Dictionary<Attribute, int>(generated.Values);
                var hidden = Enum.GetValues(typeof(HiddenAttribute)).Cast<HiddenAttribute>()
                    .ToDictionary(x => x, _ => rng.NextInt(1, 100));
                var defensiveMidfielderCover = position == Position.CentralMidfielder && !hasDefensiveMidfielderCover;
                if (defensiveMidfielderCover) hasDefensiveMidfielderCover = true;
                squad.Add(new Player($"{clubId}-P{i + 1:00}", visible, hidden, candidateAges[i], nationality,
                    SecondaryPositions(position, defensiveMidfielderCover), null));
            }
            ValidateSquad(squad, size, composition);
            var lineup = SelectBestLineup(squad);
            var starterIndices = lineup.Select(x => x.PlayerIndex).ToHashSet();
            var reserves = Enumerable.Range(0, size).Where(i => !starterIndices.Contains(i)).Select(i => ovr[i]).ToArray();
            var starterRatings = lineup.Select(x => x.Ovr).ToArray();
            var starterCoverage = starterRatings.Count(value => value >= starterMin && value <= starterMax) / 11.0;
            var benchCoverage = reserves.Count(value => value >= benchMin && value <= benchMax) / (double)reserves.Length;
            var mean = ovr.Average();
            lastMean = mean;
            lastStarterCoverage = starterCoverage;
            lastBenchCoverage = benchCoverage;
            if (mean >= meanMin && mean <= meanMax && starterCoverage >= _parameters.MinimumBandCoverage && benchCoverage >= _parameters.MinimumBandCoverage)
                return new GeneratedSquad(squad.AsReadOnly(), profile, targetMean, mean,
                    lineup.Select(x => squad[x.PlayerIndex].Id).ToArray(), lineup.Formation, attempt + 1,
                    starterCoverage, benchCoverage);
            adjustment += targetMean - mean;
        }
        throw new InvalidOperationException($"Could not generate a {profile} squad for club '{clubId}' after {_parameters.MaxAttempts} attempts; target mean {targetMean:F2}, last mean {lastMean:F2}, starter band coverage {lastStarterCoverage:P1}, bench band coverage {lastBenchCoverage:P1}.");
    }

    private Lineup SelectBestLineup(IReadOnlyList<Player> squad)
    {
        var fourFourTwo = new[]
        {
            new LineupSlot(Position.Goalkeeper, Role.Goalkeeper), new LineupSlot(Position.LeftBack, Role.DefensiveFullBack),
            new LineupSlot(Position.CentreBack, Role.CentreBackMarker), new LineupSlot(Position.CentreBack, Role.CentreBackMarker),
            new LineupSlot(Position.RightBack, Role.DefensiveFullBack), new LineupSlot(Position.LeftWinger, Role.Winger),
            new LineupSlot(Position.CentralMidfielder, Role.BoxToBoxMidfielder), new LineupSlot(Position.CentralMidfielder, Role.BoxToBoxMidfielder),
            new LineupSlot(Position.RightWinger, Role.Winger), new LineupSlot(Position.Striker, Role.AdvancedForward),
            new LineupSlot(Position.Striker, Role.AdvancedForward)
        };
        var fourThreeThree = new[]
        {
            new LineupSlot(Position.Goalkeeper, Role.Goalkeeper), new LineupSlot(Position.LeftBack, Role.DefensiveFullBack),
            new LineupSlot(Position.CentreBack, Role.CentreBackMarker), new LineupSlot(Position.CentreBack, Role.CentreBackMarker),
            new LineupSlot(Position.RightBack, Role.DefensiveFullBack), new LineupSlot(Position.DefensiveMidfielder, Role.DefensiveMidfielderDestroyer),
            new LineupSlot(Position.CentralMidfielder, Role.BoxToBoxMidfielder), new LineupSlot(Position.CentralMidfielder, Role.BoxToBoxMidfielder),
            new LineupSlot(Position.LeftWinger, Role.Winger), new LineupSlot(Position.RightWinger, Role.Winger),
            new LineupSlot(Position.Striker, Role.AdvancedForward)
        };
        var best442 = BestAssignment(squad, fourFourTwo, "4-4-2");
        var best433 = BestAssignment(squad, fourThreeThree, "4-3-3");
        return best433.Sum(x => x.Ovr) > best442.Sum(x => x.Ovr) ? new Lineup("4-3-3", best433) : new Lineup("4-4-2", best442);
    }

    private IReadOnlyList<LineupPick> BestAssignment(IReadOnlyList<Player> squad, IReadOnlyList<LineupSlot> slots, string formation)
    {
        var stateCount = 1 << slots.Count;
        var scores = Enumerable.Repeat(int.MinValue, stateCount).ToArray();
        var paths = new int[stateCount][];
        scores[0] = 0;
        paths[0] = Enumerable.Repeat(-1, slots.Count).ToArray();
        for (var playerIndex = 0; playerIndex < squad.Count; playerIndex++)
        {
            var nextScores = (int[])scores.Clone();
            var nextPaths = (int[][])paths.Clone();
            for (var mask = 0; mask < stateCount; mask++)
            {
                if (scores[mask] == int.MinValue) continue;
                for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                {
                    var bit = 1 << slotIndex;
                    if ((mask & bit) != 0 || !squad[playerIndex].Positions.Contains(slots[slotIndex].Position)) continue;
                    var nextMask = mask | bit;
                    var rating = _ovrCalculator.Calculate(squad[playerIndex], slots[slotIndex].Role, 100);
                    var score = scores[mask] + rating;
                    if (score <= nextScores[nextMask]) continue;
                    var path = (int[])paths[mask].Clone();
                    path[slotIndex] = playerIndex;
                    nextScores[nextMask] = score;
                    nextPaths[nextMask] = path;
                }
            }
            scores = nextScores;
            paths = nextPaths;
        }
        var finalMask = stateCount - 1;
        if (scores[finalMask] == int.MinValue)
            throw new InvalidOperationException($"Generated squad cannot cover the {formation} formation.");
        return slots.Select((slot, index) => new LineupPick(paths[finalMask][index],
            _ovrCalculator.Calculate(squad[paths[finalMask][index]], slot.Role, 100))).ToArray();
    }

    private static List<Position> CreatePositions((int Goalkeepers, int Defenders, int Midfielders, int Forwards) c)
    {
        var result = Enumerable.Repeat(Position.Goalkeeper, c.Goalkeepers).ToList();
        result.Add(Position.LeftBack);
        result.Add(Position.RightBack);
        result.AddRange(Enumerable.Repeat(Position.CentreBack, c.Defenders - 2));
        result.AddRange(Enumerable.Repeat(Position.CentralMidfielder, c.Midfielders - 2));
        result.Add(Position.LeftWinger);
        result.Add(Position.RightWinger);
        result.AddRange(Enumerable.Repeat(Position.Striker, c.Forwards));
        return result;
    }

    private static List<int> CreateAges(int size)
    {
        var result = new List<int>(size);
        for (var pair = 0; pair < size / 2; pair++)
        {
            var offset = pair % 8 + 1;
            result.Add(26 - offset);
            result.Add(26 + offset);
        }
        if ((size & 1) != 0) result.Add(26);
        return result;
    }

    private static IReadOnlyList<Position> SecondaryPositions(Position primary, bool defensiveMidfielderCover = false) => primary switch
    {
        Position.LeftBack or Position.RightBack => new[] { primary, Position.CentreBack },
        Position.CentralMidfielder when defensiveMidfielderCover => new[] { primary, Position.DefensiveMidfielder },
        _ => new[] { primary }
    };

    private static Role RoleFor(Position position) => position switch
    {
        Position.Goalkeeper => Role.Goalkeeper,
        Position.CentreBack => Role.CentreBackMarker,
        Position.LeftBack or Position.RightBack => Role.DefensiveFullBack,
        Position.DefensiveMidfielder => Role.DefensiveMidfielderDestroyer,
        Position.CentralMidfielder => Role.BoxToBoxMidfielder,
        Position.AttackingMidfielder => Role.AttackingMidfielderPlaymaker,
        Position.LeftWinger or Position.RightWinger => Role.Winger,
        Position.Striker => Role.AdvancedForward,
        _ => throw new ArgumentOutOfRangeException(nameof(position))
    };

    private static void ValidateSquad(IReadOnlyList<Player> squad, int expectedSize,
        (int Goalkeepers, int Defenders, int Midfielders, int Forwards) c)
    {
        if (squad.Count != expectedSize || squad.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() != expectedSize)
            throw new InvalidOperationException("Generated squad has an invalid size or duplicate player ids.");
        var meanAge = squad.Average(x => x.Age);
        if (meanAge is < 25 or > 27) throw new InvalidOperationException("Generated squad age mean is outside 25–27.");
        var primaryCounts = squad.GroupBy(x => x.Positions[0]).ToDictionary(x => x.Key, x => x.Count());
        int Count(Position p) => primaryCounts.TryGetValue(p, out var count) ? count : 0;
        if (Count(Position.Goalkeeper) != c.Goalkeepers ||
            Count(Position.LeftBack) + Count(Position.RightBack) + Count(Position.CentreBack) != c.Defenders ||
            Count(Position.CentralMidfielder) + Count(Position.LeftWinger) + Count(Position.RightWinger) != c.Midfielders ||
            Count(Position.Striker) != c.Forwards || Count(Position.Goalkeeper) < 2 ||
            c.Defenders < 2 || c.Midfielders < 2 || c.Forwards < 2)
            throw new InvalidOperationException("Generated squad does not match its required line composition.");

        var outfield = squad.Where(x => x.Positions[0] != Position.Goalkeeper).ToArray();
        bool CanCover(Position p, int required) => outfield.Count(x => x.Positions.Contains(p)) >= required;
        if (!CanCover(Position.LeftBack, 1) || !CanCover(Position.RightBack, 1) ||
            !CanCover(Position.CentreBack, 2) || !CanCover(Position.CentralMidfielder, 3) ||
            !CanCover(Position.DefensiveMidfielder, 1) ||
            !CanCover(Position.LeftWinger, 1) || !CanCover(Position.RightWinger, 1) ||
            !CanCover(Position.Striker, 2))
            throw new InvalidOperationException("Generated squad cannot cover a required Slice formation position.");
    }

    private static void Shuffle<T>(IList<T> values, ref Pcg32 rng)
    {
        for (var i = values.Count - 1; i > 0; i--)
        {
            var j = rng.NextInt(0, i + 1);
            (values[i], values[j]) = (values[j], values[i]);
        }
    }

    private sealed class LineupSlot
    {
        public LineupSlot(Position position, Role role) { Position = position; Role = role; }
        public Position Position { get; }
        public Role Role { get; }
    }
    private sealed class LineupPick
    {
        public LineupPick(int playerIndex, int ovr) { PlayerIndex = playerIndex; Ovr = ovr; }
        public int PlayerIndex { get; }
        public int Ovr { get; }
    }
    private sealed class Lineup : IReadOnlyList<LineupPick>
    {
        private readonly IReadOnlyList<LineupPick> _picks;
        public Lineup(string formation, IReadOnlyList<LineupPick> picks) { Formation = formation; _picks = picks; }
        public string Formation { get; }
        public int Count => _picks.Count;
        public LineupPick this[int index] => _picks[index];
        public IEnumerator<LineupPick> GetEnumerator() => _picks.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>Generated squad plus the generation metadata required for reproducibility and reporting.</summary>
public sealed class GeneratedSquad
{
    internal GeneratedSquad(IReadOnlyList<Player> players, SquadProfile profile, double targetMeanOvr,
        double meanOvr, IReadOnlyList<string> starterIds, string formation, int attempts,
        double starterBandCoverage, double benchBandCoverage)
    {
        Players = players; Profile = profile; TargetMeanOvr = targetMeanOvr; MeanOvr = meanOvr;
        StarterIds = Array.AsReadOnly(starterIds.ToArray()); Formation = formation; Attempts = attempts;
        StarterBandCoverage = starterBandCoverage; BenchBandCoverage = benchBandCoverage;
    }
    public IReadOnlyList<Player> Players { get; }
    public SquadProfile Profile { get; }
    public double TargetMeanOvr { get; }
    public double MeanOvr { get; }
    public IReadOnlyList<string> StarterIds { get; }
    public string Formation { get; }
    public int Attempts { get; }
    public double StarterBandCoverage { get; }
    public double BenchBandCoverage { get; }
}
