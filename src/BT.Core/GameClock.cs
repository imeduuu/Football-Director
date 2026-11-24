using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core;

/// <summary>Tracks simulation time in whole days and advances to scheduled critical events.</summary>
public sealed class GameClock
{
    public GameClock(DateTime startDate)
    {
        ValidateDate(startDate, nameof(startDate));
        CurrentDate = startDate.Date;
    }

    public DateTime CurrentDate { get; private set; }

    /// <summary>Advances one simulation day.</summary>
    public DateTime AdvanceDay()
    {
        CurrentDate = CurrentDate.AddDays(1);
        return CurrentDate;
    }

    /// <summary>Advances to the earliest future critical event, or returns false when none remain.</summary>
    public bool AdvanceToNextCriticalEvent(IEnumerable<CriticalGameEvent> events, out CriticalGameEvent nextEvent)
    {
        if (events == null) throw new ArgumentNullException(nameof(events));
        var candidates = events.ToArray();
        if (candidates.Any(gameEvent => gameEvent == null))
            throw new ArgumentException("Critical events cannot contain null entries.", nameof(events));

        var next = candidates
            .Where(gameEvent => gameEvent.Date > CurrentDate)
            .OrderBy(gameEvent => gameEvent.Date)
            .ThenBy(gameEvent => gameEvent.Kind)
            .ThenBy(gameEvent => gameEvent.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (next == null)
        {
            nextEvent = null!;
            return false;
        }

        CurrentDate = next.Date;
        nextEvent = next;
        return true;
    }

    private static void ValidateDate(DateTime date, string parameterName)
    {
        if (date.TimeOfDay != TimeSpan.Zero)
            throw new ArgumentException("Game clock dates must be date-only values at midnight.", parameterName);
    }
}

/// <summary>A dated match, transfer, contract, offer-response, or financial deadline.</summary>
public sealed class CriticalGameEvent
{
    public CriticalGameEvent(string id, DateTime date, CriticalGameEventKind kind)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An event id is required.", nameof(id));
        if (date.TimeOfDay != TimeSpan.Zero)
            throw new ArgumentException("Critical event dates must be date-only values at midnight.", nameof(date));
        if (!Enum.IsDefined(typeof(CriticalGameEventKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Id = id;
        Date = date.Date;
        Kind = kind;
    }

    public string Id { get; }
    public DateTime Date { get; }
    public CriticalGameEventKind Kind { get; }
}

public enum CriticalGameEventKind
{
    Match,
    TransferWindowClose,
    ContractExpiry,
    OfferResponse,
    FinancialDate
}
