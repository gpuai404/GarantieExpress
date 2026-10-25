using Microsoft.Data.Sqlite;
using GarantieExpress.Application.Initialization;
using GarantieExpress.Infrastructure.Persistence.Payloads;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Initialization;

internal static class SqliteInitialDataWriter
{
    internal static async Task SynchronizeAsync(
        SqliteCommand command,
        InitialApplicationData initialData,
        bool isEstablished,
        CancellationToken cancellationToken)
    {
        if (isEstablished)
            await EnsureSettingsExistAsync(command, cancellationToken);
        else
            await SeedSettingsAsync(command, initialData, cancellationToken);

        await SynchronizeBuiltInCategoriesAsync(command, initialData, cancellationToken);
    }

    private static async Task EnsureSettingsExistAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        command.CommandText = "SELECT COUNT(*) FROM StoreSettings WHERE Id = 1;";
        command.Parameters.Clear();
        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
        {
            throw new InvalidOperationException(
                "Store settings are missing from the established GarantieExpress database.");
        }
    }

    private static async Task SeedSettingsAsync(
        SqliteCommand command,
        InitialApplicationData initialData,
        CancellationToken cancellationToken)
    {
        command.CommandText = """
            INSERT INTO StoreSettings (Id, PayloadJson, Version)
            VALUES (1, $payload, 1);
            """;
        command.Parameters.Clear();
        command.Parameters.AddWithValue(
            "$payload",
            SqliteStoredData.Serialize(
                StoreSettingsPayloadMapper.FromDomain(initialData.Settings)));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SynchronizeBuiltInCategoriesAsync(
        SqliteCommand command,
        InitialApplicationData initialData,
        CancellationToken cancellationToken)
    {
        var keyParameters = initialData.Categories
            .Select((_, index) => $"$builtIn{index}")
            .ToList();
        command.CommandText = keyParameters.Count == 0
            ? "DELETE FROM Categories WHERE PayloadJson IS NULL;"
            : $"""
                DELETE FROM Categories
                WHERE PayloadJson IS NULL
                  AND Key NOT IN ({string.Join(", ", keyParameters)});
                """;
        command.Parameters.Clear();
        for (var index = 0; index < initialData.Categories.Count; index++)
        {
            command.Parameters.AddWithValue(
                keyParameters[index],
                initialData.Categories[index].Key);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);

        foreach (var category in initialData.Categories)
        {
            command.CommandText = """
                INSERT INTO Categories (
                    Key, PayloadJson, IsArchived, ShowInBar, SortOrder, Version)
                VALUES ($key, NULL, $archived, $shown, $order, 1)
                ON CONFLICT(Key) DO NOTHING;
                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$key", category.Key);
            command.Parameters.AddWithValue("$archived", category.IsArchived);
            command.Parameters.AddWithValue("$shown", category.ShowInBar);
            command.Parameters.AddWithValue("$order", category.SortOrder);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
