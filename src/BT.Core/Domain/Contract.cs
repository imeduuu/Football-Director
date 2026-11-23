using System;

namespace BT.Core.Domain;

/// <summary>Player contract terms. Monetary values use whole in-game currency units.</summary>
public sealed class Contract
{
    public Contract(long weeklyWage, int durationYears, ContractRole promisedRole, long signingBonus = 0, long goalBonus = 0, long cleanSheetBonus = 0, long? releaseClause = null)
    {
        WeeklyWage = weeklyWage >= 0 ? weeklyWage : throw new ArgumentOutOfRangeException(nameof(weeklyWage));
        DurationYears = durationYears is >= 1 and <= 5 ? durationYears : throw new ArgumentOutOfRangeException(nameof(durationYears));
        PromisedRole = promisedRole;
        SigningBonus = signingBonus >= 0 ? signingBonus : throw new ArgumentOutOfRangeException(nameof(signingBonus));
        GoalBonus = goalBonus >= 0 ? goalBonus : throw new ArgumentOutOfRangeException(nameof(goalBonus));
        CleanSheetBonus = cleanSheetBonus >= 0 ? cleanSheetBonus : throw new ArgumentOutOfRangeException(nameof(cleanSheetBonus));
        ReleaseClause = releaseClause is null or >= 0 ? releaseClause : throw new ArgumentOutOfRangeException(nameof(releaseClause));
    }
    public long WeeklyWage { get; }
    public int DurationYears { get; }
    public ContractRole PromisedRole { get; }
    public long SigningBonus { get; }
    public long GoalBonus { get; }
    public long CleanSheetBonus { get; }
    public long? ReleaseClause { get; }
}

public enum ContractRole
{
    KeyPlayer,
    RegularStarter,
    Rotation,
    Prospect
}
