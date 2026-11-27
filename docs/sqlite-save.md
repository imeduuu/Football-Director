# SQLite save database

`BT.Data.SqliteSaveDatabase.Open(path)` opens or creates a save slot. It enables
foreign keys, switches the file to WAL journaling, creates schema v1 for a new
file, and applies migrations through schema v5 before returning. The v5 migration
adds a lossless event record table for simulator event kinds that the compact v1
`match_events` projection cannot represent. The caller owns and disposes the
returned object; use `Connection` for database operations.

```csharp
using var save = SqliteSaveDatabase.Open(savePath);
var connection = save.Connection;
```

The database directory is created when needed. The schema scripts are embedded
in `BT.Data`, so callers do not need to ship SQL files separately. Newer
unsupported schema versions fail explicitly. Back up save files before
migration; SQLite integrity and foreign key checks run during opening.
