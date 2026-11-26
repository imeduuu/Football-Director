using System;
using System.Collections.Generic;
using BT.Core.Domain;
using BT.Core.Rng;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Sim;

/// <summary>Bounded, seeded resolution of the secondary incidents covered by the Vertical Slice.</summary>
public static class SliceIncidentResolver
{
    /// <summary>Resolve one contested action. Probabilities are bounded away from zero and one.</summary>
    public static IReadOnlyList<MatchEvent> Resolve(int minute, MatchTeamInput attackingTeam,
        MatchTeamInput defendingTeam, MatchZone zone, int attackerIndex, int defenderIndex,
        int refereeStrictness, int contact, ref Pcg32 random)
    {
        if (attackingTeam is null) throw new ArgumentNullException(nameof(attackingTeam));
        if (defendingTeam is null) throw new ArgumentNullException(nameof(defendingTeam));
        if (!Enum.IsDefined(typeof(MatchZone), zone)) throw new ArgumentOutOfRangeException(nameof(zone));
        if (attackerIndex is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(attackerIndex));
        if (defenderIndex is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(defenderIndex));
        if (refereeStrictness is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(refereeStrictness));
        if (contact is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(contact));

        var attacker = attackingTeam.Starters[attackerIndex];
        var defender = defendingTeam.Starters[defenderIndex];
        var events = new List<MatchEvent>(3);
        var aggression = defender.EffectiveAttributes[PlayerAttribute.Aggression];
        var tackling = defender.EffectiveAttributes[PlayerAttribute.Tackling];
        var agility = defender.EffectiveAttributes[PlayerAttribute.Agility];
        var dribbling = attacker.EffectiveAttributes[PlayerAttribute.Dribbling];
        var pressure = defendingTeam.Tactics.Pressing switch
        {
            PressingStyle.Gegenpressing => 1.0, PressingStyle.High => 0.75,
            PressingStyle.Medium => 0.45, _ => 0.2
        };
        var advanced = zone is MatchZone.AttackLeft or MatchZone.AttackCentre or MatchZone.AttackRight;
        var foulP = Bound(0.08 + (aggression - 50) * 0.0012 + pressure * 0.035 +
            (tackling - 50) * 0.0005 - (agility - 50) * 0.0006 - (dribbling - 50) * 0.0005 +
            (refereeStrictness - 50) * 0.0007 + (contact - 50) * 0.0004, 0.02, 0.32);
        if (random.NextDouble() < foulP)
        {
            events.Add(new MatchEvent(minute, MatchEventType.Foul, defendingTeam.TeamId, defender.PlayerId));
            var cardP = Bound(0.035 + aggression * 0.0011 + pressure * 0.035 +
                (refereeStrictness - 50) * 0.0012 + contact * 0.0004, 0.02, 0.24);
            if (random.NextDouble() < cardP)
                events.Add(new MatchEvent(minute, random.NextDouble() < 0.025 ? MatchEventType.RedCard : MatchEventType.YellowCard,
                    defendingTeam.TeamId, defender.PlayerId));
            if (advanced && random.NextDouble() < Bound(0.045 + (refereeStrictness - 50) * 0.0004, 0.02, 0.08))
                events.Add(new MatchEvent(minute, MatchEventType.PenaltyAwarded, attackingTeam.TeamId));
        }
        else if (random.NextDouble() < Bound(0.07 + pressure * 0.045 + (dribbling - 50) * 0.0003, 0.02, 0.18))
            events.Add(new MatchEvent(minute, MatchEventType.Corner, attackingTeam.TeamId));

        var fitnessRisk = (100 - attacker.Fitness) * 0.0007;
        var injuryP = Bound(0.004 + fitnessRisk + contact * 0.00012 + pressure * 0.006 -
            attacker.EffectiveAttributes[PlayerAttribute.Stamina] * 0.000025, 0.001, 0.035);
        if (random.NextDouble() < injuryP)
        {
            var days = random.NextInt(1, contact >= 65 ? 8 : 5);
            events.Add(new MatchEvent(minute, MatchEventType.Injury, attackingTeam.TeamId, attacker.PlayerId,
                injuryDays: days));
        }
        return events.AsReadOnly();
    }

    public static PlayerInjuryState StateFor(MatchEvent injury)
    {
        if (injury is null) throw new ArgumentNullException(nameof(injury));
        if (injury.Type != MatchEventType.Injury) throw new ArgumentException("Event must be an injury.", nameof(injury));
        return new PlayerInjuryState(injury.InjuryDays <= 3 ? PlayerAvailability.Tocando : PlayerAvailability.Lesionado,
            injury.InjuryDays);
    }

    private static double Bound(double value, double minimum, double maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;
}
