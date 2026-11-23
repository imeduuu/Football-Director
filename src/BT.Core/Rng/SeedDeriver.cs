using System;
using System.Buffers.Binary;
using System.Text;

namespace BT.Core.Rng;

/// <summary>Derives deterministic, independent seeds for individual matches.</summary>
public static class SeedDeriver
{
    private static readonly byte[] Domain = Encoding.ASCII.GetBytes("BT.MatchSeed.v1");

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
