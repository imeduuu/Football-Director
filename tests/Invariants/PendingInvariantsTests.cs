using Xunit;

namespace BT.Invariants;

// Skip reasons name the GDD stage that introduces the system and makes the invariant executable.
public sealed class PendingInvariantsTests
{
    [Fact(Skip = "Stage 10 — Match simulation and xG model")]
    public void MatchProbabilitiesStayInUnitIntervalAndXgIsNotGoalProbability() => throw new System.NotImplementedException();

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
