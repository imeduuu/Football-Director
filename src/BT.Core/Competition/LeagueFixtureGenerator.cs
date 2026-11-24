using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BT.Core.Competition;

/// <summary>A league fixture with the home and away clubs in playing order.</summary>
public sealed class LeagueFixture
{
    internal LeagueFixture(int round, string homeClubId, string awayClubId)
    {
        Round = round;
        HomeClubId = homeClubId;
        AwayClubId = awayClubId;
    }

    public int Round { get; }
    public string HomeClubId { get; }
    public string AwayClubId { get; }
}

/// <summary>Builds deterministic double round-robin fixtures using the Berger circle method.</summary>
public static class LeagueFixtureGenerator
{
    public static IReadOnlyList<IReadOnlyList<LeagueFixture>> Generate(IReadOnlyList<string> clubIds)
    {
        if (clubIds == null) throw new ArgumentNullException(nameof(clubIds));
        if (clubIds.Count < 2 || clubIds.Count % 2 != 0)
            throw new ArgumentException("The Berger circle method requires an even number of at least two clubs.", nameof(clubIds));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < clubIds.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(clubIds[i]))
                throw new ArgumentException("Club IDs must be non-empty.", nameof(clubIds));
            if (!seen.Add(clubIds[i]))
                throw new ArgumentException("Club IDs must be unique.", nameof(clubIds));
        }

        var teamCount = clubIds.Count;
        var firstLegRounds = teamCount - 1;
        var rotation = new string[teamCount];
        for (var i = 0; i < teamCount; i++) rotation[i] = clubIds[i];

        var rounds = new List<IReadOnlyList<LeagueFixture>>(firstLegRounds * 2);
        var firstLeg = new List<IReadOnlyList<LeagueFixture>>(firstLegRounds);
        for (var round = 0; round < firstLegRounds; round++)
        {
            var fixtures = new List<LeagueFixture>(teamCount / 2);
            for (var pair = 0; pair < teamCount / 2; pair++)
            {
                var first = rotation[pair];
                var second = rotation[teamCount - 1 - pair];
                // Alternate venue assignment around the circle to distribute home dates.
                var firstIsHome = (round + pair) % 2 == 0;
                fixtures.Add(firstIsHome
                    ? new LeagueFixture(round + 1, first, second)
                    : new LeagueFixture(round + 1, second, first));
            }
            firstLeg.Add(new ReadOnlyCollection<LeagueFixture>(fixtures));

            // Keep the first club fixed and rotate the remainder by one position.
            var last = rotation[teamCount - 1];
            for (var i = teamCount - 1; i > 1; i--) rotation[i] = rotation[i - 1];
            rotation[1] = last;
        }

        rounds.AddRange(firstLeg);
        for (var i = 0; i < firstLeg.Count; i++)
        {
            var source = firstLeg[i];
            var mirror = new List<LeagueFixture>(source.Count);
            foreach (var fixture in source)
                mirror.Add(new LeagueFixture(firstLegRounds + i + 1, fixture.AwayClubId, fixture.HomeClubId));
            rounds.Add(new ReadOnlyCollection<LeagueFixture>(mirror));
        }

        return new ReadOnlyCollection<IReadOnlyList<LeagueFixture>>(rounds);
    }
}
