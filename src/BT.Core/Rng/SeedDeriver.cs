using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;

namespace BT.Core.Rng;

/// <summary>Derives deterministic, independent seeds for individual matches.</summary>
public static class SeedDeriver
{
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes("BT.MatchSeed.v1");
    private static readonly byte[] WorldDomain = Encoding.ASCII.GetBytes("BT.WorldSeed.v1");

    /// <summary>Derives an independent world-generation seed for a stable entity and subsystem.</summary>
    public static ulong WorldSubSeed(ulong seedWorld, string generatorVersion, string entityId, string subsystem)
    {
        if (string.IsNullOrWhiteSpace(generatorVersion)) throw new ArgumentException("A generator version is required.", nameof(generatorVersion));
        if (string.IsNullOrWhiteSpace(entityId)) throw new ArgumentException("An entity ID is required.", nameof(entityId));
        if (string.IsNullOrWhiteSpace(subsystem)) throw new ArgumentException("A subsystem label is required.", nameof(subsystem));

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(WorldDomain);
        writer.Write(seedWorld);
        WriteUtf8(writer, generatorVersion);
        WriteUtf8(writer, entityId);
        WriteUtf8(writer, subsystem);
        writer.Flush();
        return StableHash64.Compute(stream.ToArray());
    }

    private static void WriteUtf8(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }

    /// <summary>
    /// Derives a match seed from the world seed and its competition coordinates.
    /// Identifiers are UTF-8 strings; integers are signed 32-bit little-endian values.
    /// </summary>
    public static ulong MatchSeed(ulong seedMundo, string idCompeticion, int temporada, int jornada, string idPartido)
    {
        if (idCompeticion is null) throw new ArgumentNullException(nameof(idCompeticion));
        if (idPartido is null) throw new ArgumentNullException(nameof(idPartido));

        var competition = Encoding.UTF8.GetBytes(idCompeticion);
        var match = Encoding.UTF8.GetBytes(idPartido);
        var data = new byte[Domain.Length + 8 + 4 + competition.Length + 4 + 4 + 4 + match.Length];
        var offset = 0;
        Domain.CopyTo(data, offset);
        offset += Domain.Length;
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(offset, 8), seedMundo);
        offset += 8;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), competition.Length);
        offset += 4;
        competition.CopyTo(data, offset);
        offset += competition.Length;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), temporada);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), jornada);
        offset += 4;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), match.Length);
        offset += 4;
        match.CopyTo(data, offset);
        return StableHash64.Compute(data);
    }
}
