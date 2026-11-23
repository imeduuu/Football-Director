namespace BT.Core.Domain;

/// <summary>Placeholder for club staff domain data.</summary>
public sealed class Staff
{
    public Staff(string id, string name, StaffRole role) { Id = id; Name = name; Role = role; }
    public string Id { get; }
    public string Name { get; }
    public StaffRole Role { get; }
}

public enum StaffRole
{
    Manager,
    AssistantManager,
    Coach,
    Scout,
    Physio
}
