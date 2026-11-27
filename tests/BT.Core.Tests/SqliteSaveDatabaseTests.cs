using System;
using System.IO;
using BT.Data;
using Xunit;

namespace BT.Core.Tests;

public sealed class SqliteSaveDatabaseTests
{
    [Fact]
    public void OpenCreatesMigratedSaveWithForeignKeysAndIntegrityChecksEnabled()
    {
        var path = Path.Combine(Path.GetTempPath(), "bt-save-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            using var save = SqliteSaveDatabase.Open(path);
            using var command = save.Connection.CreateCommand();
            command.CommandText = "SELECT value FROM meta WHERE key='schema_version';";
            Assert.Equal("2", command.ExecuteScalar());
            command.CommandText = "PRAGMA foreign_keys;";
            Assert.Equal(1L, command.ExecuteScalar());
            command.CommandText = "PRAGMA foreign_key_check;";
            using (var reader = command.ExecuteReader()) Assert.False(reader.Read());
            command.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", command.ExecuteScalar());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
            if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
        }
    }

    [Fact]
    public void OpenRejectsUnsupportedFutureSchemaVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), "bt-save-" + Guid.NewGuid().ToString("N") + ".sqlite");
        try
        {
            using (var save = SqliteSaveDatabase.Open(path))
            using (var command = save.Connection.CreateCommand())
            {
                command.CommandText = "UPDATE meta SET value='99' WHERE key='schema_version';";
                command.ExecuteNonQuery();
            }
            Assert.Throws<InvalidDataException>(() => SqliteSaveDatabase.Open(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + "-wal")) File.Delete(path + "-wal");
            if (File.Exists(path + "-shm")) File.Delete(path + "-shm");
        }
    }
}
