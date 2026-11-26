using System;
using System.Collections.Generic;
using System.Linq;
using BT.Core;
using BT.Core.Domain;
using BT.Core.Rng;
using PlayerAttribute = BT.Core.Domain.Attribute;

namespace BT.Core.Sim;

/// <summary>Deterministic formulas used by world and career simulation systems.</summary>
public static class SimulationFormulas
{
    private const double ZoneBlendHalfWidth = 5.0;
    private const double TacticalTensionEffectCap = 0.12;

    /// <summary>Resolves whether an attack creates a shot, with a symmetric random adjustment to the defensive threshold.</summary>
    public static bool ProgressionCreatesShot(double progression, double defensiveThreshold,
        double variability, ref Pcg32 random)
    {
        EnsureNonNegativeFinite(progression, nameof(progression));
        EnsureNonNegativeFinite(defensiveThreshold, nameof(defensiveThreshold));
        EnsureNonNegativeFinite(variability, nameof(variability));
        var adjustedThreshold = defensiveThreshold + (random.NextDouble() * 2.0 - 1.0) * variability;
        return progression > adjustedThreshold;
    }

    /// <summary>Estimates pre-finisher shot quality from geometry and context (GDD 10.5).</summary>
    public static double CalculateExpectedGoals(ShotOpportunity opportunity)
    {
        if (opportunity is null) throw new ArgumentNullException(nameof(opportunity));
        // Distance and angle establish the baseline; context modifies it before final clamping.
        var distanceFactor = Math.Exp(-opportunity.DistanceMetres / 18.0);
        var angleFactor = 0.35 + 0.65 * Math.Cos(opportunity.AngleDegrees * Math.PI / 180.0);
        var assistFactor = opportunity.AssistType switch
        {
            ShotAssistType.ThroughBall => 1.12,
            ShotAssistType.Cross => 0.82,
            ShotAssistType.Cutback => 1.20,
            ShotAssistType.LongPass => 0.90,
            ShotAssistType.Rebound => 1.08,
            _ => 1.0
        };
        var pressureFactor = 1.0 - 0.55 * opportunity.Pressure;
        var situationFactor = opportunity.Situation switch
        {
            ShotSituation.CounterAttack => 1.08,
            ShotSituation.SetPiece => 0.92,
            ShotSituation.Penalty => 2.0,
            _ => 1.0
        };
        var xg = 0.65 * distanceFactor * angleFactor * assistFactor * pressureFactor * situationFactor;
        return Clamp(xg, 0.03, 0.65);
    }

    /// <summary>Calculates goal probability from pre-finisher xG and the FIN/SER and POR/AGI attributes (GDD 10.6).</summary>
    public static double CalculateGoalProbability(double expectedGoals, int finishing, int composure,
        int goalkeeping, int agility, ParamsConfig parameters)
    {
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        EnsureFinite(expectedGoals, nameof(expectedGoals));
        if (expectedGoals < 0.0) throw new ArgumentOutOfRangeException(nameof(expectedGoals));
        ValidateGoalAttribute(finishing, nameof(finishing));
        ValidateGoalAttribute(composure, nameof(composure));
        ValidateGoalAttribute(goalkeeping, nameof(goalkeeping));
        ValidateGoalAttribute(agility, nameof(agility));

        var settings = parameters.MatchSimulation;
        var finishingModifier = Clamp(1.0 + ((finishing + composure) / 2.0 - 50.0) * settings.GoalAttributeEffectPerPoint,
            settings.GoalModifierMinimum, settings.GoalModifierMaximum);
        var goalkeeperModifier = Clamp(1.0 - ((goalkeeping + agility) / 2.0 - 50.0) * settings.GoalAttributeEffectPerPoint,
            settings.GoalModifierMinimum, settings.GoalModifierMaximum);
        return Clamp(expectedGoals * finishingModifier * goalkeeperModifier, 0.0, 1.0);
    }

    private static void ValidateGoalAttribute(int value, string name)
    {
        if (value is < 1 or > 99) throw new ArgumentOutOfRangeException(name, "Attributes must be in [1,99].");
    }

    /// <summary>Calculates Slice tactical incompatibilities as bounded intermediate-variable multipliers.</summary>
    public static TacticalTension CalculateTacticalTension(MatchTactics tactics)
    {
        if (tactics is null) throw new ArgumentNullException(nameof(tactics));

        var pressureEffectiveness = 0.0;
        var centralProgression = 0.0;
        var backlineCoverage = 0.0;
        var throughPass = 0.0;
        var alerts = new List<string>();

        // I1: gegenpressing paired with a very deep line. Slice severity is 0.75.
        if (tactics.Pressing == PressingStyle.Gegenpressing && tactics.DefensiveLine == DefensiveLine.Deep)
        {
            const double severity = 0.75;
            pressureEffectiveness -= 0.08 * severity;
            centralProgression += 0.06 * severity;
            alerts.Add("Tensión táctica: I1 — gegenpressing con línea muy atrás.");
        }

        // I2: an advanced line paired with a low block. Slice severity is 0.5.
        if (tactics.Pressing == PressingStyle.LowBlock && tactics.DefensiveLine == DefensiveLine.High)
        {
            const double severity = 0.5;
            backlineCoverage -= 0.08 * severity;
            throughPass += 0.08 * severity;
            alerts.Add("Tensión táctica: I2 — línea adelantada con bloque bajo.");
        }

        // Keep each intermediate variable within the GDD's 12% cumulative relative-effect budget.
        pressureEffectiveness = Clamp(pressureEffectiveness, -TacticalTensionEffectCap, TacticalTensionEffectCap);
        centralProgression = Clamp(centralProgression, -TacticalTensionEffectCap, TacticalTensionEffectCap);
        backlineCoverage = Clamp(backlineCoverage, -TacticalTensionEffectCap, TacticalTensionEffectCap);
        throughPass = Clamp(throughPass, -TacticalTensionEffectCap, TacticalTensionEffectCap);

        return new TacticalTension(1.0 + pressureEffectiveness, 1.0 + centralProgression,
            1.0 + backlineCoverage, 1.0 + throughPass, alerts);
    }

    /// <summary>Clamps a finite value to an inclusive finite range.</summary>
    public static double Clamp(double value, double minimum, double maximum)
    {
        EnsureFinite(value, nameof(value));
        EnsureFinite(minimum, nameof(minimum));
        EnsureFinite(maximum, nameof(maximum));
        if (minimum > maximum) throw new ArgumentOutOfRangeException(nameof(maximum), "Bounds must be ordered.");
        if (value < minimum) return minimum;
        if (value > maximum) return maximum;
        return value;
    }

    /// <summary>Returns the bounded F_total for one attribute, applying morale only to technical and mental families.</summary>
    public static double CalculateEffectiveAttributeFactor(PlayerAttribute attribute, double fitness, double morale,
        ParamsConfig parameters)
    {
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (!Enum.IsDefined(typeof(PlayerAttribute), attribute)) throw new ArgumentOutOfRangeException(nameof(attribute));
        ValidateStatus(fitness, parameters.Fitness.Minimum, parameters.Fitness.Maximum, nameof(fitness));
        ValidateStatus(morale, parameters.Morale.Minimum, parameters.Morale.Maximum, nameof(morale));

        var settings = parameters.EffectivePerformance;
        var fitnessFactor = 1.0;
        if (fitness < settings.FitnessNeutral)
        {
            fitnessFactor = 1.0 - (settings.FitnessNeutral - fitness) * settings.FitnessDeclinePerPoint;
            if (fitnessFactor < settings.FitnessMinimumFactor)
                fitnessFactor = settings.FitnessMinimumFactor;
        }
        var moraleFactor = ParamsConfig.AttributeFamilies[attribute] == AttributeFamily.Physical
            ? 1.0
            : settings.MoraleBaseFactor + morale / 100.0 * settings.MoraleRangeFactor;
        var softFactor = Clamp(settings.CohesionFactor * settings.RelationsFactor *
            settings.TacticalFamiliarityFactor, settings.SoftFactorMinimum, settings.SoftFactorMaximum);
        return Clamp(fitnessFactor * moraleFactor * softFactor,
            settings.TotalFactorMinimum, settings.TotalFactorMaximum);
    }

    /// <summary>Calculates one contextual attribute value without changing its base value or OVR.</summary>
    public static double CalculateEffectiveAttribute(double baseAttribute, PlayerAttribute attribute,
        double fitness, double morale, ParamsConfig parameters)
    {
        EnsureFinite(baseAttribute, nameof(baseAttribute));
        if (baseAttribute < 0.0) throw new ArgumentOutOfRangeException(nameof(baseAttribute));
        return baseAttribute * CalculateEffectiveAttributeFactor(attribute, fitness, morale, parameters);
    }

    /// <summary>Calculates a role's weighted effective rating, then applies role familiarity.</summary>
    public static double CalculateEffectiveRoleRating(IReadOnlyDictionary<PlayerAttribute, int> baseAttributes,
        RoleProfile roleProfile, double fitness, double morale, double roleFamiliarityPercent,
        ParamsConfig parameters)
    {
        if (baseAttributes is null) throw new ArgumentNullException(nameof(baseAttributes));
        if (roleProfile is null) throw new ArgumentNullException(nameof(roleProfile));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        if (!IsFinite(roleFamiliarityPercent) ||
            roleFamiliarityPercent < parameters.PositionalFamiliarity.Minimum ||
            roleFamiliarityPercent > parameters.PositionalFamiliarity.Maximum)
            throw new ArgumentOutOfRangeException(nameof(roleFamiliarityPercent),
                "Role familiarity is outside the configured bounds.");

        var weightedRating = 0.0;
        foreach (var (attribute, weight) in roleProfile.Weights.OrderBy(pair => pair.Key))
        {
            if (!baseAttributes.TryGetValue(attribute, out var value) || value is < 1 or > 99)
                throw new ArgumentException($"Base attribute '{attribute}' must have a value in [1,99].", nameof(baseAttributes));
            weightedRating += CalculateEffectiveAttribute(value, attribute, fitness, morale, parameters) * weight;
        }

        return weightedRating * (roleFamiliarityPercent / 100.0);
    }

    /// <summary>Returns the smooth membership weight for the defensive sector from GDD 10.4.</summary>
    public static double WDef(double y)
    {
        ValidatePitchCoordinate(y, nameof(y));
        return Clamp((35.0 + ZoneBlendHalfWidth - y) / (2.0 * ZoneBlendHalfWidth), 0.0, 1.0);
    }

    /// <summary>Returns the smooth membership weight for the midfield sector from GDD 10.4.</summary>
    public static double WMed(double y)
    {
        ValidatePitchCoordinate(y, nameof(y));
        return 1.0 - WDef(y) - WAta(y);
    }

    /// <summary>Returns the smooth membership weight for the attacking sector from GDD 10.4.</summary>
    public static double WAta(double y)
    {
        ValidatePitchCoordinate(y, nameof(y));
        return Clamp((y - (70.0 - ZoneBlendHalfWidth)) / (2.0 * ZoneBlendHalfWidth), 0.0, 1.0);
    }

    /// <summary>Returns the smooth membership weight for the left lane from GDD 10.4.</summary>
    public static double WIzq(double x)
    {
        ValidatePitchCoordinate(x, nameof(x));
        return Clamp((33.0 + ZoneBlendHalfWidth - x) / (2.0 * ZoneBlendHalfWidth), 0.0, 1.0);
    }

    /// <summary>Returns the smooth membership weight for the centre lane from GDD 10.4.</summary>
    public static double WCen(double x)
    {
        ValidatePitchCoordinate(x, nameof(x));
        return 1.0 - WIzq(x) - WDer(x);
    }

    /// <summary>Returns the smooth membership weight for the right lane from GDD 10.4.</summary>
    public static double WDer(double x)
    {
        ValidatePitchCoordinate(x, nameof(x));
        return Clamp((x - (67.0 - ZoneBlendHalfWidth)) / (2.0 * ZoneBlendHalfWidth), 0.0, 1.0);
    }

    /// <summary>Reflects a team-relative lane coordinate when comparing opposing teams.</summary>
    public static double MirrorLaneX(double x)
    {
        ValidatePitchCoordinate(x, nameof(x));
        return 100.0 - x;
    }

    /// <summary>
    /// Distributes each outfield player's effective strength over the nine pitch zones.
    /// Set <paramref name="mirrorLanes"/> when calculating the opposing team's perspective.
    /// </summary>
    public static ZoneStrengthGrid CalculateZonalStrengths(
        IReadOnlyList<ZonalStrengthContribution> contributions, bool mirrorLanes = false)
    {
        if (contributions is null) throw new ArgumentNullException(nameof(contributions));

        var cells = new double[9];
        var sectorWeights = new double[3];
        var laneWeights = new double[3];
        for (var index = 0; index < contributions.Count; index++)
        {
            var contribution = contributions[index];
            if (contribution is null)
                throw new ArgumentException("Strength contributions cannot contain null entries.", nameof(contributions));
            if (contribution.Position == Position.Goalkeeper) continue;

            var x = mirrorLanes ? MirrorLaneX(contribution.X) : contribution.X;
            sectorWeights[0] = WDef(contribution.Y);
            sectorWeights[1] = WMed(contribution.Y);
            sectorWeights[2] = WAta(contribution.Y);
            laneWeights[0] = WIzq(x);
            laneWeights[1] = WCen(x);
            laneWeights[2] = WDer(x);
            for (var sector = 0; sector < sectorWeights.Length; sector++)
                for (var lane = 0; lane < laneWeights.Length; lane++)
                    cells[sector * 3 + lane] += contribution.Strength * sectorWeights[sector] * laneWeights[lane];
        }

        return new ZoneStrengthGrid(cells);
    }

    /// <summary>Calculates home possession from midfield strength and passing styles using GDD 10.4.</summary>
    public static double CalculatePossession(double homeMidfieldStrength, PassingStyle homePassing,
        double awayMidfieldStrength, PassingStyle awayPassing, ParamsConfig parameters)
    {
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        EnsureNonNegativeFinite(homeMidfieldStrength, nameof(homeMidfieldStrength));
        EnsureNonNegativeFinite(awayMidfieldStrength, nameof(awayMidfieldStrength));
        if (!Enum.IsDefined(typeof(PassingStyle), homePassing)) throw new ArgumentOutOfRangeException(nameof(homePassing));
        if (!Enum.IsDefined(typeof(PassingStyle), awayPassing)) throw new ArgumentOutOfRangeException(nameof(awayPassing));

        var multipliers = parameters.MatchSimulation.PassingStyleMultipliers;
        var homeControl = homeMidfieldStrength * multipliers[homePassing];
        var awayControl = awayMidfieldStrength * multipliers[awayPassing];
        var totalControl = homeControl + awayControl;
        if (!(totalControl > 0.0) || double.IsInfinity(totalControl))
            throw new ArgumentException("Combined midfield control must be finite and positive.");
        return homeControl / totalControl * 100.0;
    }

    /// <summary>Applies the GDD 4.5 match fitness loss for actual minutes played.</summary>
    public static double UpdateMatchFitness(double fitness, double stamina, int minutesPlayed,
        double intensity, ParamsConfig parameters)
    {
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        ValidateStatus(fitness, parameters.Fitness.Minimum, parameters.Fitness.Maximum, nameof(fitness));
        if (!IsFinite(stamina) || stamina is < 1.0 or > 99.0)
            throw new ArgumentOutOfRangeException(nameof(stamina), "Stamina must be finite and in [1,99].");
        if (minutesPlayed is < 0 or > 120) throw new ArgumentOutOfRangeException(nameof(minutesPlayed));
        if (!IsFinite(intensity) || intensity < 0.0)
            throw new ArgumentOutOfRangeException(nameof(intensity), "Intensity must be finite and non-negative.");

        var loss = (minutesPlayed / 90.0) * (28.0 - stamina / 6.0) * intensity;
        return Clamp(fitness - loss, parameters.Fitness.Minimum, parameters.Fitness.Maximum);
    }

    /// <summary>Updates match fitness using the configured match intensity.</summary>
    public static double UpdateMatchFitness(double fitness, double stamina, int minutesPlayed,
        ParamsConfig parameters)
    {
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        return UpdateMatchFitness(fitness, stamina, minutesPlayed,
            parameters.MatchSimulation.FitnessIntensity, parameters);
    }

    /// <summary>Linearly interpolates between strictly increasing (x,y) nodes, extending endpoint values outside the node range.</summary>
    public static double LinearInterpolate(IReadOnlyList<(double X, double Y)> nodes, double x)
    {
        if (nodes is null) throw new ArgumentNullException(nameof(nodes));
        EnsureFinite(x, nameof(x));
        if (nodes.Count == 0) throw new ArgumentException("At least one node is required.", nameof(nodes));

        EnsureFinite(nodes[0].X, nameof(nodes));
        EnsureFinite(nodes[0].Y, nameof(nodes));
        for (var i = 1; i < nodes.Count; i++)
        {
            EnsureFinite(nodes[i].X, nameof(nodes));
            EnsureFinite(nodes[i].Y, nameof(nodes));
            if (nodes[i].X <= nodes[i - 1].X)
                throw new ArgumentException("Node x values must be strictly increasing.", nameof(nodes));
        }
        if (x <= nodes[0].X) return nodes[0].Y;

        for (var i = 1; i < nodes.Count; i++)
        {
            var left = nodes[i - 1];
            var right = nodes[i];
            if (x <= right.X)
            {
                var fraction = (x - left.X) / (right.X - left.X);
                return left.Y + fraction * (right.Y - left.Y);
            }
        }

        return nodes[nodes.Count - 1].Y;
    }

    /// <summary>Calculates expected match points from GDD 3.4 using the specified club and rival reputations.</summary>
    public static double ExpectedPoints(double clubReputation, double rivalReputation, bool isHome,
        ExpectedPointsParams parameters)
    {
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        EnsureFinite(clubReputation, nameof(clubReputation));
        EnsureFinite(rivalReputation, nameof(rivalReputation));
        var d = clubReputation - rivalReputation + (isHome ? parameters.HomeAdvantage : -parameters.HomeAdvantage);
        var s = 1.0 / (1.0 + DetMath.Pow(10.0, -d / parameters.ReputationScale));
        return parameters.BasePoints + parameters.WinPoints * s;
    }

    /// <summary>Updates a value with an exponential moving average; alpha is in [0,1].</summary>
    public static double ExponentialMovingAverage(double previous, double observation, double alpha)
    {
        EnsureFinite(previous, nameof(previous));
        EnsureFinite(observation, nameof(observation));
        if (!IsFinite(alpha) || alpha < 0.0 || alpha > 1.0)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be finite and in [0, 1].");
        return previous + alpha * (observation - previous);
    }

    private static void EnsureFinite(double value, string parameterName)
    {
        if (!IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName, "Value must be finite.");
    }

    private static void EnsureNonNegativeFinite(double value, string parameterName)
    {
        EnsureFinite(value, parameterName);
        if (value < 0.0) throw new ArgumentOutOfRangeException(parameterName, "Value must be non-negative.");
    }

    private static void ValidatePitchCoordinate(double value, string parameterName)
    {
        EnsureFinite(value, parameterName);
        if (value < 0.0 || value > 100.0)
            throw new ArgumentOutOfRangeException(parameterName, "Pitch coordinates must be in [0, 100].");
    }

    private static void ValidateStatus(double value, double minimum, double maximum, string parameterName)
    {
        EnsureFinite(value, parameterName);
        if (value < minimum || value > maximum)
            throw new ArgumentOutOfRangeException(parameterName, "Status value is outside its configured bounds.");
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
