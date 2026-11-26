using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core;

namespace BT.Core.Sim;

public enum MatchPhase
{
    FirstHalf,
    FirstHalfAddedTime,
    HalfTime,
    SecondHalf,
    SecondHalfAddedTime,
    Finished
}

/// <summary>Deterministic match clock, score and event state for the 18 regular intervals.</summary>
public sealed class MatchState
{
    private const int IntervalMinutes = 5;
    private const int IntervalsPerHalf = 9;
    private readonly string _homeTeamId;
    private readonly string _awayTeamId;
    private readonly MatchSimulationParams _parameters;
    private readonly List<MatchEvent> _events = new();
    private int _periodIntervals;
    private int _periodStartMinute;
    private int _periodAddedTime;
    private int _periodGoals;
    private int _periodInjuries;
    private int _periodSubstitutions;
    private int _periodOtherEvents;
    private int _homeGoals;
    private int _awayGoals;

    public MatchState(string homeTeamId, string awayTeamId, MatchSimulationParams parameters)
    {
        _homeTeamId = string.IsNullOrWhiteSpace(homeTeamId)
            ? throw new ArgumentException("A stable team id is required.", nameof(homeTeamId))
            : homeTeamId;
        _awayTeamId = string.IsNullOrWhiteSpace(awayTeamId)
            ? throw new ArgumentException("A stable team id is required.", nameof(awayTeamId))
            : awayTeamId;
        if (string.Equals(_homeTeamId, _awayTeamId, StringComparison.Ordinal))
            throw new ArgumentException("Home and away teams must be different.", nameof(awayTeamId));
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        Phase = MatchPhase.FirstHalf;
    }

    public MatchPhase Phase { get; private set; }
    public int CurrentMinute { get; private set; }
    public int IntervalsCompleted { get; private set; }
    public int FirstHalfAddedTime { get; private set; }
    public int SecondHalfAddedTime { get; private set; }
    public MatchScore Score => new(_homeGoals, _awayGoals);
    public IReadOnlyList<MatchEvent> Events => Array.AsReadOnly(_events.ToArray());

    public void AdvanceInterval()
    {
        if (Phase is not (MatchPhase.FirstHalf or MatchPhase.SecondHalf))
            throw new InvalidOperationException("Intervals can only advance during regular time.");
        if (_periodIntervals == IntervalsPerHalf)
            throw new InvalidOperationException("The regular period is complete; resolve its added time first.");

        _periodIntervals++;
        IntervalsCompleted++;
        CurrentMinute = _periodStartMinute + _periodIntervals * IntervalMinutes;
    }

    public void RecordEvent(MatchEvent matchEvent)
    {
        if (matchEvent is null) throw new ArgumentNullException(nameof(matchEvent));
        if (Phase is MatchPhase.HalfTime or MatchPhase.Finished)
            throw new InvalidOperationException("Events cannot be recorded during half-time or after the match.");
        if (!string.Equals(matchEvent.TeamId, _homeTeamId, StringComparison.Ordinal) &&
            !string.Equals(matchEvent.TeamId, _awayTeamId, StringComparison.Ordinal))
            throw new ArgumentException("The event team must participate in this match.", nameof(matchEvent));
        if (_events.Count > 0 && matchEvent.Minute < _events[_events.Count - 1].Minute)
            throw new ArgumentException("Events must be recorded in chronological order.", nameof(matchEvent));

        var isAddedTime = Phase is MatchPhase.FirstHalfAddedTime or MatchPhase.SecondHalfAddedTime;
        var maximumEventMinute = isAddedTime
            ? _periodStartMinute + IntervalsPerHalf * IntervalMinutes + _periodAddedTime
            : CurrentMinute;
        var minimumEventMinute = isAddedTime
            ? _periodStartMinute + IntervalsPerHalf * IntervalMinutes
            : _periodStartMinute;
        if (matchEvent.Minute < minimumEventMinute || matchEvent.Minute > maximumEventMinute)
            throw new ArgumentOutOfRangeException(nameof(matchEvent), "The event minute is outside the current match period.");

        _events.Add(matchEvent);
        switch (matchEvent.Type)
        {
            case MatchEventType.Goal:
                _periodGoals++;
                AddGoal(matchEvent.TeamId);
                break;
            case MatchEventType.OwnGoal:
                _periodGoals++;
                AddGoal(string.Equals(matchEvent.TeamId, _homeTeamId, StringComparison.Ordinal)
                    ? _awayTeamId
                    : _homeTeamId);
                break;
            case MatchEventType.Injury:
                _periodInjuries++;
                break;
            case MatchEventType.Substitution:
                _periodSubstitutions++;
                break;
            default:
                _periodOtherEvents++;
                break;
        }

        if (isAddedTime)
        {
            _periodAddedTime = CalculateAddedTime();
            if (Phase == MatchPhase.FirstHalfAddedTime)
                FirstHalfAddedTime = _periodAddedTime;
            else
                SecondHalfAddedTime = _periodAddedTime;
        }
    }

    public void CompleteRegularPeriod()
    {
        if (Phase is not (MatchPhase.FirstHalf or MatchPhase.SecondHalf) || _periodIntervals != IntervalsPerHalf)
            throw new InvalidOperationException("All nine regular intervals must finish before added time is calculated.");

        _periodAddedTime = CalculateAddedTime();
        if (Phase == MatchPhase.FirstHalf)
        {
            FirstHalfAddedTime = _periodAddedTime;
            Phase = MatchPhase.FirstHalfAddedTime;
        }
        else
        {
            SecondHalfAddedTime = _periodAddedTime;
            Phase = MatchPhase.SecondHalfAddedTime;
        }
    }

    public void CompleteAddedTime()
    {
        if (Phase is not (MatchPhase.FirstHalfAddedTime or MatchPhase.SecondHalfAddedTime))
            throw new InvalidOperationException("The match is not in added time.");

        CurrentMinute = _periodStartMinute + IntervalsPerHalf * IntervalMinutes + _periodAddedTime;
        if (Phase == MatchPhase.FirstHalfAddedTime)
        {
            Phase = MatchPhase.HalfTime;
            return;
        }

        Phase = MatchPhase.Finished;
    }

    public void StartSecondHalf()
    {
        if (Phase != MatchPhase.HalfTime)
            throw new InvalidOperationException("The second half can only start after half-time.");

        _periodStartMinute = CurrentMinute;
        _periodIntervals = 0;
        _periodAddedTime = 0;
        _periodGoals = 0;
        _periodInjuries = 0;
        _periodSubstitutions = 0;
        _periodOtherEvents = 0;
        Phase = MatchPhase.SecondHalf;
    }

    private int CalculateAddedTime()
    {
        var estimate = _periodGoals * _parameters.AddedTimeMinutesPerGoal +
            _periodInjuries * _parameters.AddedTimeMinutesPerInjury +
            _periodSubstitutions * _parameters.AddedTimeMinutesPerSubstitution +
            _periodOtherEvents * _parameters.AddedTimeMinutesPerOtherEvent;
        var maximum = _parameters.MaximumAddedTimeMinutesPerHalf;
        if (estimate >= maximum) return maximum;
        var wholeMinutes = (int)estimate;
        return estimate > wholeMinutes ? wholeMinutes + 1 : wholeMinutes;
    }

    private void AddGoal(string scoringTeamId)
    {
        if (string.Equals(scoringTeamId, _homeTeamId, StringComparison.Ordinal))
            _homeGoals++;
        else
            _awayGoals++;
    }
}
