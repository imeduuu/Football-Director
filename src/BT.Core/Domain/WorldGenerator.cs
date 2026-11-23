using System;
using BT.Core.Rng;

namespace BT.Core.Domain;

/// <summary>World-generation identity and deterministic sub-seed derivation.</summary>
public sealed class WorldGenerator
{
    private readonly ulong _seedWorld;
    public WorldGenerator(ulong seedMaestra, string version)
    {
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("A generator version is required.", nameof(version));
        _seedWorld = seedMaestra;
        SeedMundo = seedMaestra.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Version = version;
    }

    /// <summary>Canonical unsigned decimal representation for save metadata.</summary>
    public string SeedMundo { get; }
    public string Version { get; }

    public ulong Subseed(string entityId, string subsystem) =>
        SeedDeriver.WorldSubSeed(_seedWorld, Version, entityId, subsystem);

    public ulong ClubSubseed(string clubId, string subsystem) => Subseed(clubId, $"club:{subsystem}");

    public ulong PlayerSubseed(string playerId, string subsystem) => Subseed(playerId, $"player:{subsystem}");
}
