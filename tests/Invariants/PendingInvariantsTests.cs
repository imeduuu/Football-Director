using Xunit;

namespace BT.Invariants;

// Skip reasons name the GDD stage that introduces the system and makes the invariant executable.
public sealed class PendingInvariantsTests
{
    [Fact]
    public void MatchProbabilitiesStayInUnitIntervalAndXgIsNotGoalProbability()
    {
        var parameters = BT.Core.ParamsConfig.Load(
            System.IO.Path.Combine(System.AppContext.BaseDirectory, "config", "params.json"));
        foreach (var xg in new[] { 0.03, 0.15, 0.65 })
            foreach (var finishing in new[] { 1, 50, 99 })
                foreach (var goalkeeper in new[] { 1, 50, 99 })
                {
                    var probability = BT.Core.Sim.SimulationFormulas.CalculateGoalProbability(
                        xg, finishing, finishing, goalkeeper, goalkeeper, parameters);
                    Assert.InRange(probability, 0.0, 1.0);
                }
        Assert.NotEqual(0.15, BT.Core.Sim.SimulationFormulas.CalculateGoalProbability(
            0.15, 99, 99, 1, 1, parameters));
    }

    [Fact(Skip = "Stage 4 — Player attributes, fitness and morale")]
    public void OvrIsUnaffectedByFitnessAndMoraleWhileEffectivePerformanceChanges() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 9 — Club finances and event processing")]
    public void BudgetsAndBalancesApplyAnEventOnlyOnce() => throw new System.NotImplementedException();

    [Fact]
    public void PlayerCannotBeRegisteredToTwoActiveClubsOutsideAnExplicitLoan()
    {
        var registrations = new[]
        {
            new BT.Core.Domain.ClubRegistration("p1", "owner", BT.Core.Domain.Position.Striker),
            new BT.Core.Domain.ClubRegistration("p1", "borrower", BT.Core.Domain.Position.Striker, "owner")
        };
        Assert.Empty(BT.Core.Domain.InvariantChecker.CheckRegistrations(registrations));
        Assert.NotEmpty(BT.Core.Domain.InvariantChecker.CheckRegistrations(new[]
        {
            registrations[0], new BT.Core.Domain.ClubRegistration("p1", "other", BT.Core.Domain.Position.Striker)
        }));
    }

    [Fact(Skip = "Stage 10 — Match simulation and competition tables")]
    public void MatchIsProcessedOnceAndEventsAgreeWithDerivedTables() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 10 — Match simulation integration")]
    public void SameSeedAndEngineVersionReproduceTheSameMatchResult() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 4 — Positional role profiles")]
    public void EveryPositionalProfileHasNonnegativeWeightsSummingToOne() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 5 — Player population and regens")]
    public void PlayerAndRegenPopulationStaysWithinConfiguredBands() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 4 — Chemistry and tactical familiarity")]
    public void ChemistrySoftTotalAndFamiliarityStayWithinGddRanges() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 6 — Pitch zones and player zone membership")]
    public void ZoneMembershipWeightsAreNonnegativeAndSumToOneThroughoutThePitch() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 8 — Transfer market and player valuation")]
    public void MarketValueIsNonnegativeMonotoneInOvrAndExpiredContractsHaveZeroFee() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 23 — National cup competition")]
    public void FortyTeamCupHasThirtyNineMatchesSixRoundsAndOneChampion() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 5 — Regen generation and potential")]
    public void RegenOvrAndPotentialStayWithinGddBounds() => throw new System.NotImplementedException();

    [Fact(Skip = "Stage 10 — Post-match player ratings")]
    public void PlayerRatingsStayBoundedAndShortAppearancesRegressTowardSixPointFive() => throw new System.NotImplementedException();
}
