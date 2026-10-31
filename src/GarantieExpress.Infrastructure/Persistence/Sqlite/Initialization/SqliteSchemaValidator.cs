using Microsoft.Data.Sqlite;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;

internal static class SqliteSchemaValidator
{
    internal static async Task ValidateAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        await EnsureApplicationIdentityAsync(connection, cancellationToken);
        await EnsureExactObjectsAsync(
            connection,
            "table",
            SqliteSchema.TableNames,
            allowInternalObjects: true,
            cancellationToken);
        await EnsureExactObjectsAsync(
            connection,
            "index",
            SqliteSchema.IndexNames,
            allowInternalObjects: true,
            cancellationToken);
        await EnsureExactObjectsAsync(
            connection,
            "trigger",
            SqliteSchema.TriggerNames,
            allowInternalObjects: false,
            cancellationToken);
    }

    private static async Task EnsureApplicationIdentityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ApplicationId FROM ApplicationMetadata WHERE Id = 1;";
        var applicationId = await command.ExecuteScalarAsync(cancellationToken) as string;
        if (!string.Equals(applicationId, "GarantieExpress", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The database application identity is invalid.");
        }
    }

    private static async Task EnsureExactObjectsAsync(
        SqliteConnection connection,
        string type,
        IReadOnlySet<string> expected,
        bool allowInternalObjects,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = allowInternalObjects
            ? "SELECT name FROM sqlite_master WHERE type = $type AND name NOT LIKE 'sqlite_%';"
            : "SELECT name FROM sqlite_master WHERE type = $type;";
        command.Parameters.AddWithValue("$type", type);

        var actual = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) actual.Add(reader.GetString(0));

        if (!actual.SetEquals(expected))
        {
            throw new InvalidOperationException(
                $"The GarantieExpress database {type} schema is invalid.");
        }
    }
}
