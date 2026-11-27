using System;

namespace BT.Core.Sim;

/// <summary>Daily injury exposure with the applied difficulty multiplier available to the UI.</summary>
public sealed class DailyInjuryRisk
{
    private DailyInjuryRisk(double probability, double difficultyMultiplier, double lesMultiplier)
    {
        Probability = probability;
        DifficultyMultiplier = difficultyMultiplier;
        LesMultiplier = lesMultiplier;
    }

    /// <summary>Probability from zero to one.</summary>
    public double Probability { get; }
    /// <summary>Public difficulty setting applied to this player's risk.</summary>
    public double DifficultyMultiplier { get; }
    public double LesMultiplier { get; }

    public static DailyInjuryRisk Calculate(double baseRisk, double loadMultiplier,
        double fatigueMultiplier, double contactMultiplier, int injuryProneness,
        double difficultyMultiplier)
    {
        ValidateFactor(baseRisk, nameof(baseRisk));
        ValidateFactor(loadMultiplier, nameof(loadMultiplier));
        ValidateFactor(fatigueMultiplier, nameof(fatigueMultiplier));
        ValidateFactor(contactMultiplier, nameof(contactMultiplier));
        if (injuryProneness is < 1 or > 99) throw new ArgumentOutOfRangeException(nameof(injuryProneness));
        if (!double.IsFinite(difficultyMultiplier) || difficultyMultiplier <= 0)
            throw new ArgumentOutOfRangeException(nameof(difficultyMultiplier));

        // LES is bounded to ±25% around neutral, preventing hidden susceptibility from dominating.
        var lesMultiplier = 1.0 + (injuryProneness - 50) * 0.005;
        if (lesMultiplier < 0.75) lesMultiplier = 0.75;
        if (lesMultiplier > 1.25) lesMultiplier = 1.25;
        var probability = baseRisk * loadMultiplier * fatigueMultiplier * contactMultiplier * lesMultiplier * difficultyMultiplier;
        if (probability < 0) probability = 0;
        if (probability > 1) probability = 1;
        return new DailyInjuryRisk(probability, difficultyMultiplier, lesMultiplier);
    }

    private static void ValidateFactor(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentOutOfRangeException(name);
    }
}
