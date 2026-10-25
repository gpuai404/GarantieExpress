using Microsoft.Data.Sqlite;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;

internal static class SqliteSchemaInstaller
{
    internal static async Task InstallAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        command.CommandText = SqliteSchema.Create;
        command.Parameters.Clear();
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
