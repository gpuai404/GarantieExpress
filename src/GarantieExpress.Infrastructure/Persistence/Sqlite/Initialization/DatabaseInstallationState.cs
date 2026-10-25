using Microsoft.Data.Sqlite;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;

internal sealed class DatabaseInstallationState
{
    private DatabaseInstallationState(bool markerExists, bool isEstablished)
    {
        MarkerExists = markerExists;
        IsEstablished = isEstablished;
    }

    internal bool MarkerExists { get; }
    internal bool IsEstablished { get; }

    internal static async Task<DatabaseInstallationState> InspectAsync(
        DatabaseFileState files,
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(connection);

        var hasApplicationMetadata = await TableExistsAsync(
            connection,
            "ApplicationMetadata",
            cancellationToken);
        var userTableCount = await GetUserTableCountAsync(connection, cancellationToken);
        EnsureRecognizedDatabase(files, hasApplicationMetadata, userTableCount);
        return new(
            files.MarkerExists,
            files.MarkerExists || hasApplicationMetadata);
    }

    private static void EnsureRecognizedDatabase(
        DatabaseFileState files,
        bool hasApplicationMetadata,
        int userTableCount)
    {
        if (files.MarkerExists && !hasApplicationMetadata)
        {
            throw new InvalidOperationException(
                "The established GarantieExpress database does not contain its application identity.");
        }

        if (files.DatabaseFileExisted && !hasApplicationMetadata && userTableCount > 0)
        {
            throw new InvalidOperationException(
                "The database file is not a recognized GarantieExpress database.");
        }
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name = $table;
            """;
        command.Parameters.AddWithValue("$table", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) == 1;
    }

    private static async Task<int> GetUserTableCountAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%';
            """;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }
}
