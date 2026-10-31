using Microsoft.Data.Sqlite;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;

internal static class SqliteDatabaseHealth
{
    internal static async Task EnableWalAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL;";
        var journalMode = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken));
        if (!string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The GarantieExpress database could not enable SQLite WAL mode.");
        }
    }

    internal static async Task ValidateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await EnsureIntegrityAsync(connection, cancellationToken);
        await EnsureForeignKeysAsync(connection, cancellationToken);
    }

    private static async Task EnsureIntegrityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check(1);";
        var result = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The SQLite database failed its integrity check.");
        }
    }

    private static async Task EnsureForeignKeysAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var violations = await command.ExecuteReaderAsync(cancellationToken);
        if (await violations.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "The SQLite database contains invalid foreign-key references.");
        }
    }
}
