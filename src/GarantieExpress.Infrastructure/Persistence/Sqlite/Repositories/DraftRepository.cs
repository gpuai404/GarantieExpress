using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Infrastructure.Persistence.Payloads;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Repositories;

internal sealed class DraftRepository(
    SqliteConnectionFactory connections,
    SqliteOperationRunner<DraftRepository> operations) : IDraftRepository
{
    public async Task<OperationResult<DraftWarranty?>> GetAsync(CancellationToken cancellationToken = default)
    {
        return await operations.ExecuteAsync("load current draft", cancellationToken, async () =>
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Version, PayloadJson
                FROM Drafts
                WHERE Id = 1;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return OperationResult<DraftWarranty?>.Success(null);

            var row = SqliteStoredData.Decode(() => (
                Version: reader.GetInt64(0),
                Payload: reader.GetString(1)));
            var draft = DraftWarrantyPayloadMapper.ToDomain(
                row.Version,
                SqliteStoredData.Deserialize<DraftWarrantyPayload>(row.Payload));
            return OperationResult<DraftWarranty?>.Success(draft);
        });
    }

    public Task<OperationResult<DraftWarranty>> SaveAsync(
        DraftWarranty draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        return operations.ExecuteAsync("save draft", cancellationToken, async () =>
        {
            var payload = SqliteStoredData.Serialize(DraftWarrantyPayloadMapper.FromDomain(draft));
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = draft.Version == 0
                ? """
                    INSERT INTO Drafts (Id, PayloadJson, Version, UpdatedAtUtc)
                    VALUES (1, $payload, 1, $updated)
                    ON CONFLICT(Id) DO NOTHING
                    RETURNING Version;
                    """
                : """
                    UPDATE Drafts
                    SET PayloadJson = $payload, Version = Version + 1, UpdatedAtUtc = $updated
                    WHERE Id = 1 AND Version = $expectedVersion
                    RETURNING Version;
                    """;
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$updated", draft.SavedAt.ToString("O"));
            command.Parameters.AddWithValue("$expectedVersion", draft.Version);
            var version = await command.ExecuteScalarAsync(cancellationToken);
            return version is null
                ? OperationResult<DraftWarranty>.Failure(new OperationError(
                    "Storage.StaleDraft", "This draft was changed or consumed by another session."))
                : OperationResult<DraftWarranty>.Success(draft with
                {
                    Version = SqliteStoredData.Decode(() => Convert.ToInt64(version))
                });
        });
    }

    public Task<OperationResult> ClearAsync(
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        if (expectedVersion == 0) return Task.FromResult(OperationResult.Success());

        return operations.ExecuteAsync("clear draft", cancellationToken, async () =>
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM Drafts
                WHERE Id = 1 AND Version = $expectedVersion;
                """;
            command.Parameters.AddWithValue("$expectedVersion", expectedVersion);
            var affected = await command.ExecuteNonQueryAsync(cancellationToken);
            return affected == 1
                ? OperationResult.Success()
                : OperationResult.Failure(new OperationError(
                    "Storage.StaleDraft", "This draft was changed or consumed by another session."));
        });
    }
}
