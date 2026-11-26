using System;
using System.IO;
using BT.Core;
using BT.Core.Domain;
using BT.Core.Sim;
using Xunit;

namespace BT.Core.Tests;

public sealed class MatchSimulationTests
{
    private static ParamsConfig Parameters => ParamsConfig.Load(
        Path.Combine(AppContext.BaseDirectory, "config", "params.json"));

    [Fact]
    public void MatchStateAdvancesEighteenIntervalsAndCalculatesAddedTimePerHalf()
    {
        var state = new MatchState("home", "away", Parameters.MatchSimulation);
        Assert.Throws<InvalidOperationException>(state.CompleteRegularPeriod);

        for (var interval = 0; interval < 9; interval++)
        {
            state.AdvanceInterval();
            if (interval == 0)
                state.RecordEvent(new MatchEvent(5, MatchEventType.Goal, "home", "home-striker"));
            if (interval == 3)
                state.RecordEvent(new MatchEvent(20, MatchEventType.Injury, "away", "away-midfielder"));
        }

        state.CompleteRegularPeriod();
        Assert.Equal(MatchPhase.FirstHalfAddedTime, state.Phase);
        Assert.Equal(3, state.FirstHalfAddedTime);
        state.RecordEvent(new MatchEvent(46, MatchEventType.Injury, "away", "away-defender"));
        Assert.Equal(5, state.FirstHalfAddedTime);
        state.CompleteAddedTime();
        Assert.Equal(MatchPhase.HalfTime, state.Phase);
        state.StartSecondHalf();

        for (var interval = 0; interval < 9; interval++)
            state.AdvanceInterval();
        state.CompleteRegularPeriod();
        Assert.Equal(0, state.SecondHalfAddedTime);
        state.CompleteAddedTime();

        Assert.Equal(MatchPhase.Finished, state.Phase);
        Assert.Equal(18, state.IntervalsCompleted);
        Assert.Equal(95, state.CurrentMinute);
        Assert.Equal(1, state.Score.HomeGoals);
        Assert.Equal(0, state.Score.AwayGoals);
        Assert.Equal(3, state.Events.Count);
    }

    [Fact]
    public void MatchStateCountsOwnGoalsForTheOppositionAndCapsAddedTime()
    {
        var settings = Parameters.MatchSimulation;
        var state = new MatchState("home", "away", settings);
        for (var interval = 0; interval < 9; interval++)
        {
            state.AdvanceInterval();
            if (interval == 8)
            {
                state.RecordEvent(new MatchEvent(45, MatchEventType.OwnGoal, "home", "home-defender"));
                for (var injury = 0; injury < 8; injury++)
                    state.RecordEvent(new MatchEvent(45, MatchEventType.Injury, "away", "away-player-" + injury));
            }
        }

        state.CompleteRegularPeriod();
        Assert.Equal(settings.MaximumAddedTimeMinutesPerHalf, state.FirstHalfAddedTime);
        Assert.Equal(0, state.Score.HomeGoals);
        Assert.Equal(1, state.Score.AwayGoals);
    }

    [Fact]
    public void MatchStateRejectsEventsFromNonParticipantsAndEventsFromFutureMinutes()
    {
        var state = new MatchState("home", "away", Parameters.MatchSimulation);
        Assert.Throws<ArgumentException>(() =>
            state.RecordEvent(new MatchEvent(0, MatchEventType.Other, "visitor")));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            state.RecordEvent(new MatchEvent(1, MatchEventType.Other, "home")));
    }

    [Fact]
    public void PossessionUsesMidfieldStrengthAndConfiguredPassingStyleMultipliers()
    {
        var parameters = Parameters;
        var possession = SimulationFormulas.CalculatePossession(
            80, PassingStyle.Short, 60, PassingStyle.Direct, parameters);
        var reciprocal = SimulationFormulas.CalculatePossession(
            60, PassingStyle.Direct, 80, PassingStyle.Short, parameters);

        Assert.Equal(83.2 / (83.2 + 57.6) * 100.0, possession, 12);
        Assert.Equal(100.0 - possession, reciprocal, 12);
        Assert.Throws<ArgumentException>(() => SimulationFormulas.CalculatePossession(
            0, PassingStyle.Mixed, 0, PassingStyle.Mixed, parameters));
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(45, 71)]
    [InlineData(90, 62)]
    [InlineData(120, 56)]
    public void MatchFitnessLossUsesActualMinutesAndStamina(int minutes, double expectedFitness)
    {
        var updated = SimulationFormulas.UpdateMatchFitness(80, 60, minutes, Parameters);
        Assert.Equal(expectedFitness, updated, 12);
    }

    [Fact]
    public void MatchFitnessIsBoundedAndRejectsInvalidMinutes()
    {
        var parameters = Parameters;
        Assert.Equal(parameters.Fitness.Minimum,
            SimulationFormulas.UpdateMatchFitness(1, 1, 120, 2, parameters));
        Assert.Equal(75.5, SimulationFormulas.UpdateMatchFitness(80, 60, 45, 0.5, parameters), 12);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SimulationFormulas.UpdateMatchFitness(80, 60, 121, 1, parameters));
    }
}
