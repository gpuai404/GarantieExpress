using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Initialization;
using GarantieExpress.Domain;
using GarantieExpress.Infrastructure.Persistence.Sqlite;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;
using GarantieExpress.Infrastructure.Storage;
using Xunit;

namespace GarantieExpress.Infrastructure.Tests;

public sealed class DatabaseInitializerTests : IDisposable
{
    private readonly string _dataDirectory = Path.Combine(
        Path.GetTempPath(),
        $"garantie-express-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task InitializeAsync_creates_the_clean_first_release_schema_and_is_idempotent()
    {
        var paths = new ApplicationPaths(_dataDirectory);
        var connections = new SqliteConnectionFactory(paths);
        var initializer = CreateInitializer(connections, paths);
        var initialData = CreateInitialData();

        await initializer.InitializeAsync(initialData);
        await initializer.InitializeAsync(initialData);

        Assert.True(paths.IsInitialized);
        Assert.True(File.Exists(paths.DatabasePath));

        await using var connection = await connections.OpenAsync();
        Assert.Equal("wal", await ScalarAsync<string>(connection, "PRAGMA journal_mode;"));
        Assert.Equal(1L, await ScalarAsync<long>(connection, "PRAGMA foreign_keys;"));
        Assert.Equal("ok", await ScalarAsync<string>(connection, "PRAGMA quick_check(1);"));
        Assert.Equal("test product", await ScalarAsync<string>(
            connection,
            "SELECT garantie_express_product_key(' Test  Product ');"));
        Assert.Equal(1L, await ScalarAsync<long>(
            connection,
            "SELECT COUNT(*) FROM StoreSettings;"));
        Assert.Equal(BuiltInCategories.All.Count, await ScalarAsync<long>(
            connection,
            "SELECT COUNT(*) FROM Categories WHERE PayloadJson IS NULL;"));
        Assert.Equal(
            ["Id", "PayloadJson"],
            await ReadNamesAsync(connection, "PRAGMA table_info(WarrantySnapshots);", ordinal: 1));

        var tables = await ReadNamesAsync(
            connection,
            """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """);
        Assert.Equal(
            [
                "ApplicationMetadata",
                "Categories",
                "Drafts",
                "IssuanceRequests",
                "NumberCounters",
                "Products",
                "StoreSettings",
                "Warranties",
                "WarrantySnapshots"
            ],
            tables);
    }

    [Fact]
    public async Task InitializeAsync_preserves_settings_for_an_established_installation()
    {
        var paths = new ApplicationPaths(_dataDirectory);
        var connections = new SqliteConnectionFactory(paths);
        var initializer = CreateInitializer(connections, paths);
        await initializer.InitializeAsync(CreateInitialData("First store"));

        string storedPayload;
        await using (var connection = await connections.OpenAsync())
        {
            storedPayload = await ScalarAsync<string>(
                connection,
                "SELECT PayloadJson FROM StoreSettings WHERE Id = 1;");
        }

        await initializer.InitializeAsync(CreateInitialData("Replacement store"));

        await using var reopened = await connections.OpenAsync();
        Assert.Equal(storedPayload, await ScalarAsync<string>(
            reopened,
            "SELECT PayloadJson FROM StoreSettings WHERE Id = 1;"));
    }

    [Fact]
    public async Task InitializeAsync_migrates_the_legacy_product_constraint_without_data_loss()
    {
        var paths = new ApplicationPaths(_dataDirectory);
        var connections = new SqliteConnectionFactory(paths);
        var initializer = CreateInitializer(connections, paths);
        await initializer.InitializeAsync(CreateInitialData());

        await using (var connection = await connections.OpenAsync())
        {
            await ExecuteAsync(
                connection,
                """
                INSERT INTO Products (
                    Id, BrandModel, BrandModelKey, CategoryKey, PriceCents, DurationMonths)
                SELECT
                    '11111111111111111111111111111111',
                    'Test product',
                    'test product',
                    Key,
                    12345,
                    24
                FROM Categories
                ORDER BY SortOrder, Key
                LIMIT 1;
                """);
        }

        SqliteConnection.ClearAllPools();
        await RewriteProductConstraintAsLegacyAsync(paths.DatabasePath);

        await initializer.InitializeAsync(CreateInitialData());

        await using var migrated = await connections.OpenAsync();
        Assert.Equal(1L, await ScalarAsync<long>(
            migrated,
            "SELECT COUNT(*) FROM Products WHERE Id = '11111111111111111111111111111111';"));
        var productsSql = await ScalarAsync<string>(
            migrated,
            "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'Products';");
        Assert.Contains("garantie_express_product_key", productsSql, StringComparison.Ordinal);
        Assert.DoesNotContain("qss_product_key", productsSql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitializeAsync_does_not_replace_a_missing_established_database()
    {
        var paths = new ApplicationPaths(_dataDirectory);
        paths.MarkInitialized();
        var connections = new SqliteConnectionFactory(paths);
        var initializer = CreateInitializer(connections, paths);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => initializer.InitializeAsync(CreateInitialData()));

        Assert.Contains("database is missing", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(paths.DatabasePath));
    }

    [Fact]
    public async Task InitializeAsync_rejects_an_unrecognized_database_without_modifying_it()
    {
        var paths = new ApplicationPaths(_dataDirectory);
        await using (var foreign = new SqliteConnection($"Data Source={paths.DatabasePath}"))
        {
            await foreign.OpenAsync();
            await using var command = foreign.CreateCommand();
            command.CommandText = "CREATE TABLE ForeignData (Id INTEGER PRIMARY KEY);";
            await command.ExecuteNonQueryAsync();
        }

        var connections = new SqliteConnectionFactory(paths);
        var initializer = CreateInitializer(connections, paths);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => initializer.InitializeAsync(CreateInitialData()));

        Assert.Contains("not a recognized", exception.Message, StringComparison.OrdinalIgnoreCase);
        await using var connection = await connections.OpenAsync();
        Assert.Equal(["ForeignData"], await ReadNamesAsync(
            connection,
            """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """));
    }

    [Fact]
    public async Task InitializeAsync_rejects_an_incomplete_recognized_schema()
    {
        var paths = new ApplicationPaths(_dataDirectory);
        await using (var incomplete = new SqliteConnection($"Data Source={paths.DatabasePath}"))
        {
            await incomplete.OpenAsync();
            await using var command = incomplete.CreateCommand();
            command.CommandText = """
                CREATE TABLE ApplicationMetadata (
                    Id INTEGER PRIMARY KEY,
                    ApplicationId TEXT NOT NULL
                );
                INSERT INTO ApplicationMetadata (Id, ApplicationId)
                VALUES (1, 'GarantieExpress');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var connections = new SqliteConnectionFactory(paths);
        var initializer = CreateInitializer(connections, paths);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => initializer.InitializeAsync(CreateInitialData()));

        Assert.Contains("schema is invalid", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(paths.IsInitialized);
    }

    [Fact]
    public async Task Installed_schema_enforces_json_foreign_keys_and_snapshot_immutability()
    {
        var paths = new ApplicationPaths(_dataDirectory);
        var connections = new SqliteConnectionFactory(paths);
        await CreateInitializer(connections, paths).InitializeAsync(CreateInitialData());

        await using var connection = await connections.OpenAsync();
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            connection,
            "UPDATE StoreSettings SET PayloadJson = 'invalid-json' WHERE Id = 1;"));
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            connection,
            """
            INSERT INTO Products (
                Id, BrandModel, BrandModelKey, CategoryKey, PriceCents, DurationMonths)
            VALUES (
                '11111111111111111111111111111111',
                'Test product',
                'test product',
                'missing-category',
                100,
                12);
            """));

        await ExecuteAsync(
            connection,
            $"INSERT INTO WarrantySnapshots (Id, PayloadJson) VALUES ('{new string('a', 64)}', '{{}}');");
        await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(
            connection,
            $"UPDATE WarrantySnapshots SET PayloadJson = '{{\"changed\":true}}' WHERE Id = '{new string('a', 64)}';"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dataDirectory)) Directory.Delete(_dataDirectory, recursive: true);
    }

    private static DatabaseInitializer CreateInitializer(
        SqliteConnectionFactory connections,
        ApplicationPaths paths) =>
        new(connections, paths, NullLogger<DatabaseInitializer>.Instance);

    private static InitialApplicationData CreateInitialData(string name = "Test store")
    {
        var profile = StoreProfile.Create(
                name,
                "Test slogan",
                ["Repairs"],
                null,
                string.Empty,
                string.Empty,
                string.Empty)
            .Match(value => value, error => throw new InvalidOperationException(error.ToString()));
        var theme = ThemePalette.Create(
                "#112233",
                "#223344",
                "#334455",
                "#445566",
                "#556677",
                "#667788",
                "#778899",
                "#FFFFFF")
            .Match(value => value, error => throw new InvalidOperationException(error.ToString()));
        var policy = WarrantyPolicy.Create(
                "Full warranty terms",
                "Short warranty terms",
                [6, 12, 24],
                "a4-reference",
                "TST")
            .Match(value => value, error => throw new InvalidOperationException(error.ToString()));
        var settings = StoreSettings.Create(profile, theme, [], policy, inkSaver: false)
            .Match(value => value, error => throw new InvalidOperationException(error.ToString()));
        return new InitialApplicationData(settings, BuiltInCategories.All);
    }

    private static async Task<T> ScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The query did not return a value."));
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RewriteProductConstraintAsLegacyAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA writable_schema = ON;
            UPDATE sqlite_master
            SET sql = replace(
                sql,
                'garantie_express_product_key',
                'qss_product_key')
            WHERE type = 'table' AND name = 'Products';
            PRAGMA schema_version = 2;
            PRAGMA writable_schema = OFF;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadNamesAsync(
        SqliteConnection connection,
        string sql,
        int ordinal = 0)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var names = new List<string>();
        while (await reader.ReadAsync()) names.Add(reader.GetString(ordinal));
        return names;
    }
}
