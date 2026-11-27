using System;
using System.Collections.Generic;
using BT.Core.Competition;
using BT.Core.Sim;
using Xunit;

namespace BT.Core.Tests;

public sealed class MatchdayProcessorTests
{
    [Fact]
    public void AlreadyProcessedFixturesAreNotSimulatedAndStandingsAreRebuilt()
    {
        var tx = new FakeTransaction();
        var processor = new MatchdayProcessor(new FakeStore(tx), new ThrowingSimulator());

        var result = processor.Process(12, "league", 1, 1, "season-1", "club-a",
            new PointsRules(3, 1, 0));

        Assert.Empty(result.Matches);
        Assert.Equal(2, result.Standings.Count);
        Assert.True(tx.StandingsReplaced);
        Assert.Equal(0, tx.AppliedMatches);
    }

    [Fact]
    public void RejectsInconsistentMatchdayBeforeSimulatingAnyFixture()
    {
        var tx = new FakeTransaction
        {
            Fixtures = new[]
            {
                new MatchdayFixture("match-1", "club-a", "club-b", MatchCompetition.League),
                new MatchdayFixture("match-2", "club-c", "club-a", MatchCompetition.League)
            }
        };
        var processor = new MatchdayProcessor(new FakeStore(tx), new ThrowingSimulator());

        Assert.Throws<InvalidOperationException>(() => processor.Process(12, "league", 1, 1,
            "season-1", "club-a", new PointsRules(3, 1, 0)));
        Assert.Equal(0, tx.AppliedMatches);
    }

    private sealed class FakeStore : IMatchdayStore
    {
        private readonly IMatchdayTransaction _transaction;
        public FakeStore(IMatchdayTransaction transaction) => _transaction = transaction;
        public T ExecuteTransaction<T>(Func<IMatchdayTransaction, T> operation) => operation(_transaction);
    }

    private sealed class FakeTransaction : IMatchdayTransaction
    {
        public IReadOnlyList<MatchdayFixture> Fixtures { get; set; } =
            new[] { new MatchdayFixture("match-1", "club-a", "club-b", MatchCompetition.League, isProcessed: true) };
        public bool StandingsReplaced { get; private set; }
        public int AppliedMatches { get; private set; }
        public IReadOnlyList<MatchdayFixture> GetFixtures(string competitionId, string seasonId, int round) =>
            Fixtures;
        public bool IsMatchProcessed(string matchId) => false;
        public MatchInput CreateMatchInput(MatchdayFixture fixture, ulong seed, bool isUserMatch) =>
            throw new InvalidOperationException("Processed matches must not be prepared for simulation.");
        public void ApplyMatch(ProcessedMatch match) => AppliedMatches++;
        public IReadOnlyList<LeagueResult> GetCompletedLeagueResults(string competitionId, string seasonId) =>
            Array.Empty<LeagueResult>();
        public IReadOnlyList<string> GetClubIds(string competitionId, string seasonId) => new[] { "club-a", "club-b" };
        public void ReplaceStandings(string competitionId, string seasonId, IReadOnlyList<LeagueStanding> standings) =>
            StandingsReplaced = true;
    }

    private sealed class ThrowingSimulator : IMatchdaySimulator
    {
        public MatchReport SimulateFull(MatchInput input) => throw new InvalidOperationException("Must not simulate.");
        public HeadlessMatchResult SimulateHeadless(MatchInput input) => throw new InvalidOperationException("Must not simulate.");
    }
}
