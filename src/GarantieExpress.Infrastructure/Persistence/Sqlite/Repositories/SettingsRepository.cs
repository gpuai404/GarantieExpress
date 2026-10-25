using GarantieExpress.Application.Common;
using GarantieExpress.Application.Settings;
using GarantieExpress.Domain;
using GarantieExpress.Infrastructure.Persistence.Payloads;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Repositories;

internal sealed class SettingsRepository(
    SqliteConnectionFactory connections,
    SqliteOperationRunner<SettingsRepository> operations) : ISettingsRepository
{
    public async Task<OperationResult<StoreSettings?>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await operations.ExecuteAsync("load store settings", cancellationToken, async () =>
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT PayloadJson, Version FROM StoreSettings WHERE Id = 1;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return OperationResult<StoreSettings?>.Success(null);

            var row = SqliteStoredData.Decode(() => (
                Payload: reader.GetString(0),
                Version: reader.GetInt64(1)));
            var document = SqliteStoredData.Deserialize<StoreSettingsPayload>(row.Payload);
            return StoreSettingsPayloadMapper.ToDomain(document, row.Version).Match(
                value => OperationResult<StoreSettings?>.Success(value),
                error => OperationResult<StoreSettings?>.Failure(
                    new OperationError($"Storage.InvalidSettings.{error}", "Stored settings are invalid.")));
        });
    }

    public Task<OperationResult<StoreSettings>> SaveAsync(
        StoreSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return operations.ExecuteAsync("save store settings", cancellationToken, async () =>
        {
            var payload = SqliteStoredData.Serialize(StoreSettingsPayloadMapper.FromDomain(settings));
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = settings.Version == 0 ? """
                INSERT INTO StoreSettings (Id, PayloadJson, Version) VALUES (1, $payload, 1)
                ON CONFLICT(Id) DO NOTHING
                RETURNING Version;
                """ : """
                UPDATE StoreSettings
                SET PayloadJson = $payload, Version = Version + 1
                WHERE Id = 1 AND Version = $expectedVersion
                RETURNING Version;
                """;
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$expectedVersion", settings.Version);
            var version = await command.ExecuteScalarAsync(cancellationToken);
            if (version is null)
            {
                return OperationResult<StoreSettings>.Failure(new OperationError(
                    "Storage.StaleSettings", "Settings were changed by another session."));
            }

            return settings.WithVersion(
                SqliteStoredData.Decode(() => Convert.ToInt64(version))).Match(
                OperationResult<StoreSettings>.Success,
                error => OperationResult<StoreSettings>.Failure(new OperationError(
                    $"Storage.InvalidSettings.{error}",
                    "The saved settings version is invalid.")));
        });
    }
}
