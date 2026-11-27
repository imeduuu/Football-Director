using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Sim;

/// <summary>Tracks elapsed on-pitch time as the match clock advances, independently of event timestamps.</summary>
internal sealed class MatchAppearanceTracker
{
    private readonly Dictionary<string, int> _minutes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _starters = new(StringComparer.Ordinal);
    private readonly HashSet<string> _appeared = new(StringComparer.Ordinal);
    private int _durationMinutes;

    public MatchAppearanceTracker(MatchInput input)
    {
        foreach (var player in input.HomeTeam.Starters.Concat(input.AwayTeam.Starters))
        {
            _minutes.Add(player.PlayerId, 0);
            _starters.Add(player.PlayerId);
            _appeared.Add(player.PlayerId);
        }
    }

    public void Advance(MatchTeamInput home, MatchTeamInput away, IReadOnlyList<MatchEvent> events, int elapsedMinutes)
    {
        if (elapsedMinutes < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMinutes));
        _durationMinutes += elapsedMinutes;
        var dismissed = new HashSet<string>(events.Where(e => e.Type == MatchEventType.RedCard).Select(e => e.PlayerId!), StringComparer.Ordinal);
        foreach (var player in home.Starters.Concat(away.Starters))
        {
            if (dismissed.Contains(player.PlayerId)) continue;
            _minutes[player.PlayerId] = _minutes.GetValueOrDefault(player.PlayerId) + elapsedMinutes;
        }
    }

    public void RegisterSubstitutions(IEnumerable<MatchEvent> substitutions)
    {
        foreach (var substitution in substitutions)
            if (substitution.Type == MatchEventType.Substitution && substitution.RelatedPlayerId is not null)
            {
                _appeared.Add(substitution.RelatedPlayerId);
                _minutes.TryAdd(substitution.RelatedPlayerId, 0);
            }
    }

    public IReadOnlyDictionary<string, int> Minutes => _minutes;
    public IReadOnlyCollection<string> Appeared => _appeared;
    public IReadOnlyCollection<string> Starters => _starters;
    public int DurationMinutes => _durationMinutes;
}
