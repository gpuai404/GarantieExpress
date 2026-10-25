using Microsoft.Data.Sqlite;
using GarantieExpress.Infrastructure.Storage;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite;

public sealed class SqliteConnectionFactory(ApplicationPaths paths)
{
    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(CreateConnectionString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            SqliteFunctions.Register(connection);
            await ExecutePragmaAsync(connection, "PRAGMA foreign_keys = ON;", cancellationToken);
            await ExecutePragmaAsync(connection, "PRAGMA busy_timeout = 5000;", cancellationToken);
            await ExecutePragmaAsync(connection, "PRAGMA synchronous = FULL;", cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private string CreateConnectionString() => new SqliteConnectionStringBuilder
    {
        DataSource = paths.DatabasePath,
        Mode = paths.IsInitialized
            ? SqliteOpenMode.ReadWrite
            : SqliteOpenMode.ReadWriteCreate,
        Pooling = true
    }.ToString();

    private static async Task ExecutePragmaAsync(
        SqliteConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
