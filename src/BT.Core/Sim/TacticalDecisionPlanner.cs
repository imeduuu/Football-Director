using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core.Domain;

namespace BT.Core.Sim;

/// <summary>Deterministic section 21 tactical choices for one manager profile.</summary>
public sealed class TacticalDecisionPlanner
{
    public FormationDecision ChooseFormation(IReadOnlyList<FormationDefinition> unlockedFormations,
        IReadOnlyDictionary<string, double> squadSuitability, IReadOnlyList<string> managerPreferences)
    {
        if (unlockedFormations is null) throw new ArgumentNullException(nameof(unlockedFormations));
        if (squadSuitability is null) throw new ArgumentNullException(nameof(squadSuitability));
        if (managerPreferences is null) throw new ArgumentNullException(nameof(managerPreferences));
        if (unlockedFormations.Count == 0 || unlockedFormations.Any(f => f is null))
            throw new ArgumentException("At least one unlocked formation is required.", nameof(unlockedFormations));
        if (unlockedFormations.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != unlockedFormations.Count)
            throw new ArgumentException("Unlocked formation ids must be unique.", nameof(unlockedFormations));

        var preferenceRank = managerPreferences.Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index, StringComparer.Ordinal);
        var selected = unlockedFormations
            .OrderByDescending(f => squadSuitability.TryGetValue(f.Id, out var score) ? score : 0)
            .ThenBy(f => preferenceRank.TryGetValue(f.Id, out var rank) ? rank : int.MaxValue)
            .ThenBy(f => f.Id, StringComparer.Ordinal).First();
        var suitability = squadSuitability.TryGetValue(selected.Id, out var value) ? value : 0;
        var reason = $"Formación {selected.Id}: mayor adecuación de plantilla ({suitability:0.##}) entre las desbloqueadas; desempate estable por preferencia del entrenador y código.";
        return new FormationDecision(selected.Id, reason);
    }

    public IReadOnlyList<TacticalChange> PlanReactions(MatchTactics initial, int managerAggression,
        MatchScore scoreAt60, MatchScore scoreAt75, bool isHomeTeam)
    {
        if (initial is null) throw new ArgumentNullException(nameof(initial));
        if (managerAggression is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(managerAggression));
        if (scoreAt60 is null) throw new ArgumentNullException(nameof(scoreAt60));
        if (scoreAt75 is null) throw new ArgumentNullException(nameof(scoreAt75));
        var changes = new List<TacticalChange>(2);
        var tactics = initial;
        foreach (var checkpoint in new[] { (Minute: 60, Score: scoreAt60), (Minute: 75, Score: scoreAt75) })
        {
            var goalsFor = isHomeTeam ? checkpoint.Score.HomeGoals : checkpoint.Score.AwayGoals;
            var goalsAgainst = isHomeTeam ? checkpoint.Score.AwayGoals : checkpoint.Score.HomeGoals;
            var minute = checkpoint.Minute;
            if (goalsFor < goalsAgainst)
            {
                if (managerAggression >= 35)
                {
                    var mentality = Raise(tactics.Mentality, minute == 75 ? 2 : 1);
                    var pressing = Raise(tactics.Pressing, managerAggression >= 70 ? 2 : 1);
                    var next = new MatchTactics(tactics.FormationId, mentality, tactics.Passing, pressing,
                        tactics.DefensiveLine == DefensiveLine.Deep ? DefensiveLine.Normal : DefensiveLine.High, tactics.Width);
                    changes.Add(new TacticalChange(minute, next,
                        $"Minuto {minute}: va perdiendo; el perfil agresividad {managerAggression}/100 aumenta mentalidad y presión."));
                    tactics = next;
                }
            }
            else if (goalsFor > goalsAgainst && managerAggression <= 65)
            {
                var mentality = Lower(tactics.Mentality);
                var pressing = Lower(tactics.Pressing);
                var line = tactics.DefensiveLine == DefensiveLine.High ? DefensiveLine.Normal : DefensiveLine.Deep;
                var next = new MatchTactics(tactics.FormationId, mentality, tactics.Passing, pressing, line, tactics.Width);
                changes.Add(new TacticalChange(minute, next,
                    $"Minuto {minute}: va ganando; el perfil prudente (agresividad {managerAggression}/100) reduce presión y protege espacios."));
                tactics = next;
            }
        }
        return changes.AsReadOnly();
    }

    private static TacticalMentality Raise(TacticalMentality value, int steps) =>
        (int)value + steps > (int)TacticalMentality.Overload ? TacticalMentality.Overload : (TacticalMentality)((int)value + steps);
    private static TacticalMentality Lower(TacticalMentality value) =>
        value == TacticalMentality.VeryDefensive ? value : (TacticalMentality)((int)value - 1);
    private static PressingStyle Raise(PressingStyle value, int steps) =>
        (int)value + steps > (int)PressingStyle.Gegenpressing ? PressingStyle.Gegenpressing : (PressingStyle)((int)value + steps);
    private static PressingStyle Lower(PressingStyle value) =>
        value == PressingStyle.LowBlock ? value : (PressingStyle)((int)value - 1);
}

public sealed class FormationDecision
{
    internal FormationDecision(string formationId, string reason) { FormationId = formationId; Reason = reason; }
    public string FormationId { get; }
    public string Reason { get; }
}

public sealed class TacticalChange
{
    internal TacticalChange(int minute, MatchTactics tactics, string reason) { Minute = minute; Tactics = tactics; Reason = reason; }
    public int Minute { get; }
    public MatchTactics Tactics { get; }
    public string Reason { get; }
}
