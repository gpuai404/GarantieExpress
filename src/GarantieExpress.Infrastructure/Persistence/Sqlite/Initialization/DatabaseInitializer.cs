using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using GarantieExpress.Application.Initialization;
using GarantieExpress.Infrastructure.Persistence.Sqlite;
using GarantieExpress.Infrastructure.Storage;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;

public sealed class DatabaseInitializer(
    SqliteConnectionFactory connections,
    ApplicationPaths paths,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(
        InitialApplicationData initialData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initialData);

        var files = DatabaseFileState.Capture(paths);
        files.EnsureDatabaseFileIsPresent();

        if (files.DatabaseFileExisted
            && await LegacyProductSchemaMigrator.MigrateAsync(
                paths.DatabasePath,
                cancellationToken))
        {
            logger.LogInformation(
                "Migrated the legacy product-key constraint to the GarantieExpress schema.");
        }

        await using var connection = await connections.OpenAsync(cancellationToken);
        var installation = await DatabaseInstallationState.InspectAsync(
            files,
            connection,
            cancellationToken);

        await SqliteDatabaseHealth.EnableWalAsync(connection, cancellationToken);
        if (installation.IsEstablished)
            await SqliteSchemaValidator.ValidateAsync(connection, cancellationToken);

        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;

        if (!installation.IsEstablished)
            await SqliteSchemaInstaller.InstallAsync(command, cancellationToken);
        await SqliteInitialDataWriter.SynchronizeAsync(
            command,
            initialData,
            installation.IsEstablished,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await SqliteSchemaValidator.ValidateAsync(connection, cancellationToken);
        await SqliteDatabaseHealth.ValidateAsync(connection, cancellationToken);

        if (!installation.MarkerExists) paths.MarkInitialized();
        logger.LogInformation("SQLite database initialized and validated.");
    }
}
