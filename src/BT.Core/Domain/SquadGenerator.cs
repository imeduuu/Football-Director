using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core.Rng;

namespace BT.Core.Domain;

/// <summary>Builds deterministic Slice and MVP squads with age and formation coverage constraints.</summary>
public sealed class SquadGenerator
{
    private static readonly IReadOnlyDictionary<int, (int Goalkeepers, int Defenders, int Midfielders, int Forwards)> Sizes =
        new Dictionary<int, (int, int, int, int)>
        {
            [18] = (2, 6, 6, 4), [19] = (2, 6, 6, 5), [20] = (3, 6, 6, 5),
            [21] = (3, 6, 7, 5), [22] = (3, 7, 7, 5), [25] = (3, 7, 10, 5)
        };

    private readonly AttributeGenerator _attributes;

    public SquadGenerator(AttributeGenerator attributes) =>
        _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));

    public IReadOnlyList<Player> Generate(int size, string clubId, int divisionLevel, double clubReputation,
        string nationality, ulong seed, ulong sequence = 0)
    {
        if (!Sizes.TryGetValue(size, out var composition))
            throw new ArgumentOutOfRangeException(nameof(size), "Squad size must be 18–22 or 25.");
        if (string.IsNullOrWhiteSpace(clubId)) throw new ArgumentException("A club id is required.", nameof(clubId));
        if (string.IsNullOrWhiteSpace(nationality)) throw new ArgumentException("A nationality is required.", nameof(nationality));

        var positions = CreatePositions(composition);
        var ages = CreateAges(size);
        var rng = new Pcg32(seed, sequence);
        Shuffle(positions, ref rng);
        Shuffle(ages, ref rng);

        var squad = new List<Player>(size);
        for (var i = 0; i < size; i++)
        {
            var position = positions[i];
            var role = RoleFor(position);
            var generated = _attributes.Generate(divisionLevel, clubReputation, ages[i], role, ref rng);
            var visible = new Dictionary<Attribute, int>(generated.Values);
            var hidden = Enum.GetValues(typeof(HiddenAttribute)).Cast<HiddenAttribute>()
                .ToDictionary(x => x, _ => rng.NextInt(1, 100));
            var eligiblePositions = SecondaryPositions(position);
            squad.Add(new Player($"{clubId}-P{i + 1:00}", visible, hidden, ages[i], nationality,
                eligiblePositions, null));
        }

        ValidateSquad(squad, size, composition);
        return squad.AsReadOnly();
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

    private static IReadOnlyList<Position> SecondaryPositions(Position primary) => primary switch
    {
        Position.LeftBack or Position.RightBack => new[] { primary, Position.CentreBack },
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
}
