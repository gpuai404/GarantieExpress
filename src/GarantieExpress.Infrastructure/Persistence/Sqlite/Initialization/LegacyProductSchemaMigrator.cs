using GarantieExpress.Domain;
using Microsoft.Data.Sqlite;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;

internal static class LegacyProductSchemaMigrator
{
    private const string LegacyFunctionName = "qss_product_key";

    internal static async Task<bool> MigrateAsync(
        string databasePath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // SQLite must know the old deterministic function before it can parse the
        // persisted Products constraint. This connection exists only for migration.
        connection.CreateFunction(
            LegacyFunctionName,
            static (string? value) => Product.NormalizeKey(value),
            isDeterministic: true);
        SqliteFunctions.Register(connection);

        if (!await IsGarantieExpressDatabaseAsync(connection, cancellationToken))
            return false;

        var productsSql = await GetProductsSqlAsync(connection, cancellationToken);
        if (productsSql?.Contains(
                LegacyFunctionName,
                StringComparison.OrdinalIgnoreCase) != true)
        {
            return false;
        }

        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE Products_GarantieExpressMigration (
                Id TEXT PRIMARY KEY CHECK (length(Id) = 32),
                BrandModel TEXT NOT NULL CHECK (length(trim(BrandModel)) BETWEEN 1 AND 180),
                BrandModelKey TEXT NOT NULL UNIQUE
                    CHECK (BrandModelKey = garantie_express_product_key(BrandModel)),
                CategoryKey TEXT NOT NULL REFERENCES Categories(Key),
                PriceCents INTEGER NOT NULL CHECK (PriceCents BETWEEN 1 AND 9999999900),
                DurationMonths INTEGER NOT NULL CHECK (DurationMonths BETWEEN 1 AND 120)
            );

            INSERT INTO Products_GarantieExpressMigration (
                Id,
                BrandModel,
                BrandModelKey,
                CategoryKey,
                PriceCents,
                DurationMonths)
            SELECT
                Id,
                BrandModel,
                BrandModelKey,
                CategoryKey,
                PriceCents,
                DurationMonths
            FROM Products;

            DROP TABLE Products;
            ALTER TABLE Products_GarantieExpressMigration RENAME TO Products;

            CREATE INDEX IX_Products_Category
            ON Products(CategoryKey, BrandModel COLLATE NOCASE);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task<bool> IsGarantieExpressDatabaseAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM ApplicationMetadata
                WHERE Id = 1 AND ApplicationId = 'GarantieExpress'
            );
            """;

        try
        {
            return Convert.ToInt32(
                await command.ExecuteScalarAsync(cancellationToken)) == 1;
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 1)
        {
            return false;
        }
    }

    private static async Task<string?> GetProductsSqlAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT sql
            FROM sqlite_master
            WHERE type = 'table' AND name = 'Products';
            """;
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }
}
