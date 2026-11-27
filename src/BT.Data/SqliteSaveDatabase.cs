using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace BT.Data;

/// <summary>Opens and migrates an offline SQLite save slot.</summary>
public sealed class SqliteSaveDatabase : IDisposable
{
    private const int CurrentSchemaVersion = 5;
    private readonly SqliteConnection _connection;

    private SqliteSaveDatabase(SqliteConnection connection) => _connection = connection;

    public SqliteConnection Connection => _connection;

    /// <summary>Opens a save slot with foreign keys enabled and WAL journaling.</summary>
    public static SqliteSaveDatabase Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A database path is required.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory is not null) Directory.CreateDirectory(directory);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            Pooling = false
        };
        var connection = new SqliteConnection(builder.ToString());
        try
        {
            connection.Open();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA foreign_keys;";
                if (Convert.ToInt32(command.ExecuteScalar()) != 1)
                    throw new InvalidDataException("SQLite foreign key enforcement could not be enabled.");
                command.CommandText = "PRAGMA journal_mode=WAL;";
                command.ExecuteScalar();
            }
            Migrate(connection);
            return new SqliteSaveDatabase(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static void Migrate(SqliteConnection connection)
    {
        if (!TableExists(connection, "meta"))
        {
            ExecuteScript(connection, ReadResource("schema_v1.sql"));
            using var initialize = connection.CreateCommand();
            initialize.CommandText = "INSERT INTO meta(key,value) VALUES ('schema_version','1');";
            initialize.ExecuteNonQuery();
        }

        var version = ReadVersion(connection);
        if (version > CurrentSchemaVersion)
            throw new InvalidDataException($"Save schema version {version} is newer than supported version {CurrentSchemaVersion}.");
        while (version < CurrentSchemaVersion)
        {
            if (version is not (1 or 2 or 3 or 4)) throw new InvalidDataException($"No migration is available from schema version {version}.");
            using var transaction = connection.BeginTransaction(deferred: false);
            try
            {
                var nextVersion = version + 1;
                var migration = nextVersion switch
                {
                    2 => "002_matchday_closure.sql",
                    3 => "003_match_input_snapshots.sql",
                    4 => "004_knockout_match_inputs.sql",
                    _ => "005_match_event_records.sql"
                };
                ExecuteScript(connection, ReadResource(migration), transaction);
                using (var update = connection.CreateCommand())
                {
                    update.Transaction = transaction;
                    update.CommandText = "UPDATE meta SET value=$version WHERE key='schema_version';";
                    update.Parameters.AddWithValue("$version", nextVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (update.ExecuteNonQuery() != 1)
                        throw new InvalidDataException("The save schema version row is missing or duplicated.");
                }
                CheckIntegrity(connection, transaction);
                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
            version = ReadVersion(connection);
        }
        CheckIntegrity(connection, null);
    }

    private static void CheckIntegrity(SqliteConnection connection, SqliteTransaction? transaction)
    {
        using (var foreignKeys = connection.CreateCommand())
        {
            foreignKeys.Transaction = transaction;
            foreignKeys.CommandText = "PRAGMA foreign_key_check;";
            using var reader = foreignKeys.ExecuteReader();
            if (reader.Read()) throw new InvalidDataException("SQLite foreign_key_check reported a violation.");
        }
        using (var integrity = connection.CreateCommand())
        {
            integrity.Transaction = transaction;
            integrity.CommandText = "PRAGMA integrity_check;";
            if (!string.Equals(Convert.ToString(integrity.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("SQLite integrity_check failed.");
        }
    }

    private static int ReadVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM meta WHERE key='schema_version';";
        var raw = Convert.ToString(command.ExecuteScalar());
        if (!int.TryParse(raw, out var version) || version < 1)
            throw new InvalidDataException("Save schema_version is missing or invalid.");
        return version;
    }

    private static bool TableExists(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() is not null;
    }

    private static void ExecuteScript(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string ReadResource(string suffix)
    {
        var assembly = typeof(SqliteSaveDatabase).GetTypeInfo().Assembly;
        var name = assembly.GetManifestResourceNames().Single(resource => resource.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Embedded SQLite schema resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public void Dispose() => _connection.Dispose();
}
