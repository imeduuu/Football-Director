using System;
using System.Collections.Generic;
using System.Linq;

namespace BT.Core.Competition;

/// <summary>Tracks a player's cards and bans. Call ProcessMatch before building the next squad.</summary>
public sealed class PlayerDiscipline
{
    private readonly Dictionary<string, int> _yellowCards = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _bans = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Cause, string OriginFixtureId, string RuleVersion)> _provenance = new(StringComparer.Ordinal);

    public PlayerDiscipline(string playerId) => PlayerId = string.IsNullOrWhiteSpace(playerId)
        ? throw new ArgumentException("A player id is required.", nameof(playerId)) : playerId;

    public string PlayerId { get; }

    public int YellowCards(MatchCompetition competition) => YellowCards("league:" + competition);
    public int YellowCards(string accumulationGroupId) => _yellowCards.TryGetValue(accumulationGroupId, out var count) ? count : 0;
    public int RemainingBanMatches(MatchCompetition competition) => RemainingBanMatches(competition.ToString());
    public int RemainingBanMatches(string serviceCompetitionId) => _bans.TryGetValue(serviceCompetitionId, out var count) ? count : 0;
    public bool IsSuspended(MatchCompetition competition) => RemainingBanMatches(competition) > 0;
    public bool IsSuspended(string serviceCompetitionId) => RemainingBanMatches(serviceCompetitionId) > 0;

    /// <summary>Records the match's cards, then serves a ban only when the player appears in that competition.</summary>
    public void ProcessMatch(MatchCompetition competition, IEnumerable<PlayerCard> cards,
        IEnumerable<ScheduledMatch> upcomingMatches, string clubId, DisciplineRules rules)
        => ProcessMatch(competition, "", "", cards, upcomingMatches, clubId, rules, "unversioned");

    public void ProcessMatch(MatchCompetition competition, string competitionId, string currentFixtureId,
        IEnumerable<PlayerCard> cards, IEnumerable<ScheduledMatch> upcomingMatches, string clubId,
        DisciplineRules rules, string ruleVersion)
    {
        if (!Enum.IsDefined(typeof(MatchCompetition), competition)) throw new ArgumentOutOfRangeException(nameof(competition));
        if (cards is null) throw new ArgumentNullException(nameof(cards));
        if (upcomingMatches is null) throw new ArgumentNullException(nameof(upcomingMatches));
        if (string.IsNullOrWhiteSpace(clubId)) throw new ArgumentException("A club id is required.", nameof(clubId));
        if (rules is null) throw new ArgumentNullException(nameof(rules));
        if (string.IsNullOrWhiteSpace(ruleVersion)) throw new ArgumentException("A rule version is required.", nameof(ruleVersion));

        // Consume prior eligible sanctions before reading cards from this fixture.
        var serviceCompetitionId = string.IsNullOrWhiteSpace(competitionId) ? competition.ToString() : competitionId;
        Serve(serviceCompetitionId);
        var fixtures = upcomingMatches.Where(match =>
        {
            if (match is null) throw new ArgumentException("Upcoming matches cannot contain null entries.", nameof(upcomingMatches));
            return match.Status is ScheduledMatchStatus.Scheduled or ScheduledMatchStatus.Postponed && match.MatchId != currentFixtureId &&
                   (string.Equals(match.HomeClubId, clubId, StringComparison.Ordinal) || string.Equals(match.AwayClubId, clubId, StringComparison.Ordinal));
        }).ToArray();
        var group = string.IsNullOrEmpty(competitionId) && rules.YellowScope == "perCompetition"
            ? rules.AccumulationGroupId + ":" + competition : rules.AccumulationGroupId;

        foreach (var card in cards)
        {
            if (card is null) throw new ArgumentException("Cards cannot contain null entries.", nameof(cards));
            if (!string.Equals(card.PlayerId, PlayerId, StringComparison.Ordinal)) continue;
            if (card.Kind == PlayerCardKind.Yellow)
            {
                var count = YellowCards(group) + 1;
                if (count >= rules.YellowThreshold)
                {
                    _yellowCards[group] = 0;
                    AddBan(serviceCompetitionId, rules.YellowSuspensionMatches, "yellowAccumulation", currentFixtureId, ruleVersion);
                }
                else _yellowCards[group] = count;
            }
            else
            {
                var hasIssuingFixture = fixtures.Any(match => string.IsNullOrEmpty(competitionId)
                    ? match.Competition == competition : string.Equals(match.CompetitionId, competitionId, StringComparison.Ordinal));
                var destination = hasIssuingFixture || rules.RedService != "issuingCompetitionThenLeagueIfNoFixturesRemain"
                    ? serviceCompetitionId : string.IsNullOrEmpty(competitionId) ? MatchCompetition.League.ToString() : rules.LeagueTransferCompetitionId;
                AddBan(destination, checked(rules.DirectRedMinimum + rules.DirectRedAdditional), "directRed", currentFixtureId, ruleVersion);
            }
        }
    }

    private void Serve(string competitionId)
    {
        if (RemainingBanMatches(competitionId) > 0) _bans[competitionId]--;
    }

    private void AddBan(string competitionId, int matches, string cause, string origin, string version)
    {
        _bans[competitionId] = checked(RemainingBanMatches(competitionId) + matches);
        _provenance[competitionId] = (cause, origin, version);
    }
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
