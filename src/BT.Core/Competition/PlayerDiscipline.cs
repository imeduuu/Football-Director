using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Competition;

/// <summary>Tracks a player's cards and bans. Call ProcessMatch before building the next squad.</summary>
public sealed class PlayerDiscipline
{
    private readonly Dictionary<MatchCompetition, int> _yellowCards = new();
    private readonly Dictionary<MatchCompetition, int> _bans = new();

    public PlayerDiscipline(string playerId) => PlayerId = string.IsNullOrWhiteSpace(playerId)
        ? throw new ArgumentException("A player id is required.", nameof(playerId)) : playerId;

    public string PlayerId { get; }

    public int YellowCards(MatchCompetition competition) => _yellowCards.TryGetValue(competition, out var count) ? count : 0;
    public int RemainingBanMatches(MatchCompetition competition) => _bans.TryGetValue(competition, out var count) ? count : 0;
    public bool IsSuspended(MatchCompetition competition) => RemainingBanMatches(competition) > 0;

    /// <summary>Records the match's cards, then serves a ban only when the player appears in that competition.</summary>
    public void ProcessMatch(MatchCompetition competition, IEnumerable<PlayerCard> cards,
        IEnumerable<ScheduledMatch> upcomingMatches, string clubId, DisciplineRules rules)
    {
        if (!Enum.IsDefined(typeof(MatchCompetition), competition)) throw new ArgumentOutOfRangeException(nameof(competition));
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        if (upcomingMatches is null) throw new ArgumentNullException(nameof(upcomingMatches));
        if (string.IsNullOrWhiteSpace(clubId)) throw new ArgumentException("A club id is required.", nameof(clubId));
        if (rules is null) throw new ArgumentNullException(nameof(rules));

        // A ban already in force is served by this match before new cards are assessed.
        Serve(competition);
        var fixtures = upcomingMatches.Where(match =>
        {
            if (match is null) throw new ArgumentException("Upcoming matches cannot contain null entries.", nameof(upcomingMatches));
            return string.Equals(match.HomeClubId, clubId, StringComparison.Ordinal) ||
                   string.Equals(match.AwayClubId, clubId, StringComparison.Ordinal);
        }).ToArray();

        foreach (var card in cards)
        {
            if (card is null) throw new ArgumentException("Cards cannot contain null entries.", nameof(cards));
            if (!string.Equals(card.PlayerId, PlayerId, StringComparison.Ordinal)) continue;
            if (card.Kind == PlayerCardKind.Yellow)
            {
                var scope = competition == MatchCompetition.League ? MatchCompetition.League : MatchCompetition.Cup;
                var count = YellowCards(scope) + 1;
                if (count >= rules.YellowThreshold)
                {
                    _yellowCards[scope] = 0;
                    AddBan(scope, rules.YellowSuspensionMatches);
                }
                else _yellowCards[scope] = count;
            }
            else
            {
                var destination = fixtures.Any(match => match.Competition == competition)
                    ? competition
                    : competition == MatchCompetition.League || fixtures.Length == 0
                        ? MatchCompetition.League
                        : MatchCompetition.League;
                AddBan(destination, checked(rules.DirectRedMinimum + rules.DirectRedAdditional));
            }
        }
    }

    private void Serve(MatchCompetition competition)
    {
        if (RemainingBanMatches(competition) > 0) _bans[competition]--;
    }

    private void AddBan(MatchCompetition competition, int matches) =>
        _bans[competition] = checked(RemainingBanMatches(competition) + matches);
}

public enum PlayerCardKind { Yellow, DirectRed }

public sealed class PlayerCard
{
    public PlayerCard(string playerId, PlayerCardKind kind)
    {
        PlayerId = string.IsNullOrWhiteSpace(playerId) ? throw new ArgumentException("A player id is required.", nameof(playerId)) : playerId;
        if (!Enum.IsDefined(typeof(PlayerCardKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
    }
    public string PlayerId { get; }
    public PlayerCardKind Kind { get; }
}
