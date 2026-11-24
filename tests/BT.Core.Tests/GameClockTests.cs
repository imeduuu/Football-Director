using System;
using BT.Core;
using Xunit;

public sealed class GameClockTests
{
    private static readonly DateTime Start = new DateTime(2030, 7, 1);

    [Fact]
    public void AdvancesOneDayAtATime()
    {
        var clock = new GameClock(Start);

        Assert.Equal(Start.AddDays(1), clock.AdvanceDay());
        Assert.Equal(Start.AddDays(1), clock.CurrentDate);
    }

    [Fact]
    public void AdvancesToNearestFutureCriticalEventAcrossAllKinds()
    {
        var clock = new GameClock(Start);
        var match = new CriticalGameEvent("match", Start.AddDays(4), CriticalGameEventKind.Match);
        var offer = new CriticalGameEvent("offer", Start.AddDays(2), CriticalGameEventKind.OfferResponse);
        var expiry = new CriticalGameEvent("contract", Start.AddDays(8), CriticalGameEventKind.ContractExpiry);

        Assert.True(clock.AdvanceToNextCriticalEvent(new[] { match, offer, expiry }, out var next));

        Assert.Same(offer, next);
        Assert.Equal(Start.AddDays(2), clock.CurrentDate);
    }

    [Fact]
    public void IgnoresCurrentAndPastEventsAndUsesStableTieBreak()
    {
        var clock = new GameClock(Start);
        var sameDayOther = new CriticalGameEvent("z-match", Start.AddDays(3), CriticalGameEventKind.Match);
        var sameDayFirst = new CriticalGameEvent("a-match", Start.AddDays(3), CriticalGameEventKind.Match);
        var past = new CriticalGameEvent("past", Start.AddDays(-1), CriticalGameEventKind.FinancialDate);

        Assert.True(clock.AdvanceToNextCriticalEvent(new[] { past, sameDayOther, sameDayFirst }, out var next));
        Assert.Same(sameDayFirst, next);
        Assert.Equal(Start.AddDays(3), clock.CurrentDate);
    }

    [Fact]
    public void ReturnsFalseAndLeavesDateUnchangedWhenNoFutureEventsRemain()
    {
        var clock = new GameClock(Start);

        Assert.False(clock.AdvanceToNextCriticalEvent(
            new[] { new CriticalGameEvent("today", Start, CriticalGameEventKind.FinancialDate) }, out var next));

        Assert.Null(next);
        Assert.Equal(Start, clock.CurrentDate);
    }

    [Fact]
    public void RejectsDatesWithTimeComponents()
    {
        Assert.Throws<ArgumentException>(() => new GameClock(Start.AddHours(1)));
        Assert.Throws<ArgumentException>(() => new CriticalGameEvent("event", Start.AddHours(1), CriticalGameEventKind.Match));
    }
}
