using Microsoft.Data.Sqlite;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.Domain;
using GarantieExpress.Infrastructure.Persistence.Payloads;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Repositories;

internal sealed class WarrantyIssueRepository(
    SqliteConnectionFactory connections,
    SqliteOperationRunner<WarrantyIssueRepository> operations) : IWarrantyIssueRepository
{
    public Task<OperationResult<WarrantyIssueResult?>> GetResultAsync(
        string requestId,
        CancellationToken cancellationToken = default) =>
        operations.ExecuteAsync(
            "recover issuance result",
            cancellationToken,
            () => LoadResultAsync(requestId, cancellationToken));

    public Task<OperationResult<WarrantyRecord>> IssueAsync(
        WarrantyIssueCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var snapshotValidation = ValidateSnapshot(command);
        if (snapshotValidation is OperationResult.Failed failed)
            return Task.FromResult(OperationResult<WarrantyRecord>.Failure(failed.Details));

        return operations.ExecuteAsync(
            "issue warranty",
            cancellationToken,
            () => CommitIssuanceAsync(command, cancellationToken));
    }

    private async Task<OperationResult<WarrantyIssueResult?>> LoadResultAsync(
        string requestId,
        CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var sql = connection.CreateCommand();
        sql.CommandText = $"""
            SELECT {WarrantyRowMapper.Columns}, snapshot.PayloadJson
            FROM IssuanceRequests AS request
            INNER JOIN Warranties AS warranty
                ON warranty.Number = request.WarrantyNumber
            INNER JOIN WarrantySnapshots AS snapshot
                ON snapshot.Id = warranty.SnapshotId
            WHERE request.RequestId = $requestId;
            """;
        sql.Parameters.AddWithValue("$requestId", requestId);
        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return OperationResult<WarrantyIssueResult?>.Success(null);

        var warranty = WarrantyRowMapper.Read(reader);
        if (warranty is OperationResult<WarrantyRecord>.Failed warrantyFailure)
            return OperationResult<WarrantyIssueResult?>.Failure(warrantyFailure.Details);

        var record = ((OperationResult<WarrantyRecord>.Succeeded)warranty).Value;
        var snapshotPayload = SqliteStoredData.Decode(() => reader.GetString(18));
        return WarrantySnapshotReader.Read(snapshotPayload, record.SnapshotId)
            .Match(
                snapshot => OperationResult<WarrantyIssueResult?>.Success(
                    new WarrantyIssueResult(record, snapshot)),
                OperationResult<WarrantyIssueResult?>.Failure);
    }

    private async Task<OperationResult<WarrantyRecord>> CommitIssuanceAsync(
        WarrantyIssueCommand command,
        CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var transaction =
            (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var sql = connection.CreateCommand();
        sql.Transaction = transaction;

        var previous = await FindPreviousResultAsync(sql, command, cancellationToken);
        if (previous is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return previous;
        }

        var currentState = await ValidateCurrentStateAsync(sql, command, cancellationToken);
        if (currentState is OperationResult.Failed staleState)
            return OperationResult<WarrantyRecord>.Failure(staleState.Details);

        var record = await CreateIssuedRecordAsync(sql, command, cancellationToken);
        if (record is OperationResult<WarrantyRecord>.Failed recordFailure)
            return recordFailure;

        var issued = ((OperationResult<WarrantyRecord>.Succeeded)record).Value;
        var snapshot = await StoreSnapshotAsync(sql, command, cancellationToken);
        if (snapshot is OperationResult.Failed snapshotFailure)
            return OperationResult<WarrantyRecord>.Failure(snapshotFailure.Details);

        await InsertWarrantyAsync(sql, issued, cancellationToken);
        await InsertRequestAsync(sql, command, issued.Number, cancellationToken);

        var draft = await ConsumeDraftAsync(sql, command, cancellationToken);
        if (draft is OperationResult.Failed draftFailure)
            return OperationResult<WarrantyRecord>.Failure(draftFailure.Details);

        await transaction.CommitAsync(cancellationToken);
        return OperationResult<WarrantyRecord>.Success(issued);
    }

    private static OperationResult ValidateSnapshot(WarrantyIssueCommand command)
    {
        var isValid = string.Equals(
            WarrantySnapshotHasher.Compute(command.Snapshot),
            command.Snapshot.Id,
            StringComparison.Ordinal);
        return isValid ? OperationResult.Success() : InvalidSnapshot();
    }

    private static async Task<OperationResult<WarrantyRecord>> CreateIssuedRecordAsync(
        SqliteCommand sql,
        WarrantyIssueCommand command,
        CancellationToken cancellationToken)
    {
        sql.CommandText = """
            INSERT INTO NumberCounters (Year, HighestSequence) VALUES ($year, 1)
            ON CONFLICT(Year) DO UPDATE SET HighestSequence = HighestSequence + 1
            RETURNING HighestSequence;
            """;
        sql.Parameters.Clear();
        sql.Parameters.AddWithValue("$year", command.Year);
        var sequenceValue = await sql.ExecuteScalarAsync(cancellationToken);
        var sequence = SqliteStoredData.Decode(
            () => Convert.ToInt32(sequenceValue));

        return WarrantyRecord.CreateIssued(
                command.Warranty,
                command.Prefix,
                command.Year,
                sequence,
                command.Snapshot.Id)
            .Match(
                OperationResult<WarrantyRecord>.Success,
                error => OperationResult<WarrantyRecord>.Failure(new OperationError(
                    $"Storage.InvalidWarranty.{error}",
                    "The issued warranty could not be persisted.")));
    }

    private static async Task<OperationResult> StoreSnapshotAsync(
        SqliteCommand sql,
        WarrantyIssueCommand command,
        CancellationToken cancellationToken)
    {
        var payload = SqliteStoredData.Serialize(
            WarrantySnapshotPayloadMapper.FromDomain(command.Snapshot));
        sql.CommandText = """
            INSERT INTO WarrantySnapshots (Id, PayloadJson)
            VALUES ($snapshotId, $snapshot)
            ON CONFLICT(Id) DO NOTHING;
            """;
        sql.Parameters.Clear();
        sql.Parameters.AddWithValue("$snapshotId", command.Snapshot.Id);
        sql.Parameters.AddWithValue("$snapshot", payload);
        await sql.ExecuteNonQueryAsync(cancellationToken);

        return await ValidateStoredSnapshotAsync(
            sql,
            command.Snapshot.Id,
            cancellationToken);
    }

    private static async Task InsertWarrantyAsync(
        SqliteCommand sql,
        WarrantyRecord warranty,
        CancellationToken cancellationToken)
    {
        sql.CommandText = """
            INSERT INTO Warranties (
                Number, Year, Sequence, CategoryKey, BrandModel, Serial, PurchaseDate,
                PriceCents, DurationMonths, IssuedAt, Status, LastTemplateKey, SnapshotId,
                ValidFrom, ValidUntil, ServiceType, RepairNote, Version)
            VALUES ($number, $year, $sequence, $category, $brand, $serial, $purchase,
                $price, $duration, $issued, $status, $template, $snapshotId,
                $validFrom, $validUntil, $serviceType, $repairNote, 1);
            """;
        sql.Parameters.Clear();
        WarrantyRowMapper.AddParameters(sql, warranty);
        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertRequestAsync(
        SqliteCommand sql,
        WarrantyIssueCommand command,
        string warrantyNumber,
        CancellationToken cancellationToken)
    {
        sql.CommandText = """
            INSERT INTO IssuanceRequests (RequestId, InputHash, WarrantyNumber)
            VALUES ($requestId, $inputHash, $number);
            """;
        sql.Parameters.Clear();
        sql.Parameters.AddWithValue("$requestId", command.RequestId);
        sql.Parameters.AddWithValue("$inputHash", command.InputHash);
        sql.Parameters.AddWithValue("$number", warrantyNumber);
        await sql.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<OperationResult> ConsumeDraftAsync(
        SqliteCommand sql,
        WarrantyIssueCommand command,
        CancellationToken cancellationToken)
    {
        if (command.SourceDraft.Version == 0) return OperationResult.Success();

        sql.CommandText = """
            DELETE FROM Drafts
            WHERE Id = 1 AND Version = $draftVersion;
            """;
        sql.Parameters.Clear();
        sql.Parameters.AddWithValue("$draftVersion", command.SourceDraft.Version);
        return await sql.ExecuteNonQueryAsync(cancellationToken) == 1
            ? OperationResult.Success()
            : StaleDraft();
    }

    private static async Task<OperationResult> ValidateCurrentStateAsync(
        SqliteCommand sql,
        WarrantyIssueCommand command,
        CancellationToken cancellationToken)
    {
        sql.CommandText = "SELECT Version FROM StoreSettings WHERE Id = 1;";
        sql.Parameters.Clear();
        var settingsVersion = await sql.ExecuteScalarAsync(cancellationToken);
        if (settingsVersion is null
            || SqliteStoredData.Decode(() => Convert.ToInt64(settingsVersion))
                != command.SettingsVersion)
        {
            return StaleIssuanceState();
        }

        sql.CommandText = "SELECT Key, Version FROM Categories ORDER BY Key;";
        var actual = new List<CategoryVersionExpectation>();
        await using (var reader = await sql.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                actual.Add(SqliteStoredData.Decode(() =>
                    new CategoryVersionExpectation(reader.GetString(0), reader.GetInt64(1))));
            }
        }

        var expected = command.CategoryVersions
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToList();
        if (!actual.SequenceEqual(expected)) return StaleIssuanceState();

        sql.CommandText = "SELECT Version FROM Drafts WHERE Id = 1;";
        var draftVersion = await sql.ExecuteScalarAsync(cancellationToken);
        return command.SourceDraft.Version == 0
            ? draftVersion is null ? OperationResult.Success() : StaleDraft()
            : draftVersion is not null
              && SqliteStoredData.Decode(() => Convert.ToInt64(draftVersion))
                == command.SourceDraft.Version
                ? OperationResult.Success()
                : StaleDraft();
    }

    private static async Task<OperationResult<WarrantyRecord>?> FindPreviousResultAsync(
        SqliteCommand sql,
        WarrantyIssueCommand command,
        CancellationToken cancellationToken)
    {
        sql.CommandText = $"""
            SELECT request.InputHash, {WarrantyRowMapper.Columns}
            FROM IssuanceRequests AS request
            INNER JOIN Warranties AS warranty
                ON warranty.Number = request.WarrantyNumber
            WHERE request.RequestId = $requestId;
            """;
        sql.Parameters.Clear();
        sql.Parameters.AddWithValue("$requestId", command.RequestId);
        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        if (!string.Equals(reader.GetString(0), command.InputHash, StringComparison.Ordinal))
        {
            return OperationResult<WarrantyRecord>.Failure(new OperationError(
                "Storage.IssuanceRequestConflict",
                "This issuance request was already used with different input."));
        }

        return WarrantyRowMapper.Read(reader, offset: 1);
    }

    private static async Task<OperationResult> ValidateStoredSnapshotAsync(
        SqliteCommand sql,
        string expectedHash,
        CancellationToken cancellationToken)
    {
        sql.CommandText = """
            SELECT PayloadJson
            FROM WarrantySnapshots
            WHERE Id = $snapshotId;
            """;
        sql.Parameters.Clear();
        sql.Parameters.AddWithValue("$snapshotId", expectedHash);
        await using var reader = await sql.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return InvalidSnapshot();

        var payload = SqliteStoredData.Decode(() => reader.GetString(0));
        return WarrantySnapshotReader.Read(payload, expectedHash)
            .Match(_ => OperationResult.Success(), OperationResult.Failure);
    }

    private static OperationResult StaleIssuanceState() => OperationResult.Failure(
        new OperationError(
            "Storage.StaleIssuanceState",
            "Settings or categories changed while the warranty was being issued. Reload and try again."));

    private static OperationResult StaleDraft() => OperationResult.Failure(
        new OperationError(
            "Storage.StaleDraft",
            "This draft was changed or removed by another session."));

    private static OperationResult InvalidSnapshot() => OperationResult.Failure(
        new OperationError(
            "Storage.InvalidSnapshot",
            "A stored warranty snapshot is invalid."));
}
