using Microsoft.Data.Sqlite;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Warranties.History;
using GarantieExpress.Domain;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Repositories;

internal sealed class WarrantyRepository(
    SqliteConnectionFactory connections,
    SqliteOperationRunner<WarrantyRepository> operations) : IWarrantyRepository
{
    private const string HistorySearchPredicate = """
        $searchTerm IS NULL
        OR garantie_express_contains(Number, $searchTerm, $cultureName) = 1
        OR garantie_express_contains(Serial, $searchTerm, $cultureName) = 1
        OR garantie_express_contains(BrandModel, $searchTerm, $cultureName) = 1
        """;

    public Task<OperationResult<WarrantyHistoryPage>> SearchAsync(
        WarrantyHistoryPageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return operations.ExecuteAsync("search warranty history", cancellationToken, async () =>
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction(deferred: true);
            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText = $"SELECT COUNT(*) FROM Warranties WHERE {HistorySearchPredicate};";
            AddSearchParameters(countCommand, request);
            var countValue = await countCommand.ExecuteScalarAsync(cancellationToken);
            var totalCount = SqliteStoredData.Decode(() => Convert.ToInt64(countValue));
            var lastPageIndex = totalCount == 0 ? 0 : (totalCount - 1) / request.PageSize;
            var pageIndex = (int)Math.Min(request.PageIndex, lastPageIndex);
            var offset = (long)pageIndex * request.PageSize;

            await using var pageCommand = connection.CreateCommand();
            pageCommand.Transaction = transaction;
            pageCommand.CommandText = $"""
                SELECT {WarrantyRowMapper.Columns}
                FROM Warranties
                WHERE {HistorySearchPredicate}
                ORDER BY julianday(IssuedAt) DESC, Number DESC
                LIMIT $pageSize OFFSET $offset;
                """;
            AddSearchParameters(pageCommand, request);
            pageCommand.Parameters.AddWithValue("$pageSize", request.PageSize);
            pageCommand.Parameters.AddWithValue("$offset", offset);

            var warranties = new List<WarrantyRecord>();
            await using (var reader = await pageCommand.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    var restored = WarrantyRowMapper.Read(reader);
                    if (restored is OperationResult<WarrantyRecord>.Failed failed)
                        return OperationResult<WarrantyHistoryPage>.Failure(failed.Details);
                    warranties.Add(((OperationResult<WarrantyRecord>.Succeeded)restored).Value);
                }
            }
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<WarrantyHistoryPage>.Success(
                new WarrantyHistoryPage(warranties, totalCount, pageIndex, request.PageSize));
        });
    }

    public Task<OperationResult<WarrantySnapshot?>> GetSnapshotAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return operations.ExecuteAsync(
            "load warranty snapshot",
            cancellationToken,
            async () =>
            {
                await using var connection = await connections.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT PayloadJson
                    FROM WarrantySnapshots
                    WHERE Id = $id;
                    """;
                command.Parameters.AddWithValue("$id", id);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return OperationResult<WarrantySnapshot?>.Success(null);

                var payload = SqliteStoredData.Decode(() => reader.GetString(0));
                return WarrantySnapshotReader.Read(payload, id).Match(
                    value => OperationResult<WarrantySnapshot?>.Success(value),
                    OperationResult<WarrantySnapshot?>.Failure);
            });
    }

    public Task<OperationResult<WarrantyRecord>> SaveStatusAsync(
        WarrantyRecord warranty,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(warranty);
        return operations.ExecuteAsync("update warranty status", cancellationToken, async () =>
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE Warranties
                SET Status = $status, Version = Version + 1
                WHERE Number = $number AND Version = $expectedVersion
                RETURNING Version;
                """;
            command.Parameters.AddWithValue("$status", (int)warranty.Status);
            command.Parameters.AddWithValue("$number", warranty.Number);
            command.Parameters.AddWithValue("$expectedVersion", warranty.Version);
            var version = await command.ExecuteScalarAsync(cancellationToken);
            if (version is null)
            {
                return OperationResult<WarrantyRecord>.Failure(new OperationError(
                    "Storage.StaleWarranty",
                    "This warranty was changed or removed by another session."));
            }

            return warranty.WithVersion(
                SqliteStoredData.Decode(() => Convert.ToInt64(version))).Match(
                OperationResult<WarrantyRecord>.Success,
                error => OperationResult<WarrantyRecord>.Failure(new OperationError(
                    $"Storage.InvalidWarranty.{error}",
                    "The saved warranty version is invalid.")));
        });
    }

    public Task<OperationResult> DeleteAsync(
        string number,
        long expectedVersion,
        CancellationToken cancellationToken = default)
    {
        return operations.ExecuteAsync("delete warranty", cancellationToken, async () =>
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                SELECT SnapshotId FROM Warranties
                WHERE Number = $number AND Version = $expectedVersion;
                """;
            command.Parameters.AddWithValue("$number", number);
            command.Parameters.AddWithValue("$expectedVersion", expectedVersion);
            var snapshotId = await command.ExecuteScalarAsync(cancellationToken) as string;
            if (snapshotId is null)
            {
                return OperationResult.Failure(new OperationError(
                    "Storage.StaleWarranty",
                    "This warranty was changed or removed by another session."));
            }

            command.CommandText = """
                DELETE FROM Warranties
                WHERE Number = $number AND Version = $expectedVersion;
                """;
            var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
            if (deleted != 1)
                return OperationResult.Failure(new OperationError(
                    "Storage.StaleWarranty",
                    "This warranty was changed or removed by another session."));

            command.CommandText = """
                DELETE FROM WarrantySnapshots WHERE Id = $snapshotId
                AND NOT EXISTS (SELECT 1 FROM Warranties WHERE SnapshotId = $snapshotId);
                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$snapshotId", snapshotId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult.Success();
        });
    }

    private static void AddSearchParameters(
        SqliteCommand command,
        WarrantyHistoryPageRequest request)
    {
        command.Parameters.AddWithValue("$searchTerm", (object?)request.SearchTerm ?? DBNull.Value);
        command.Parameters.AddWithValue("$cultureName", request.CultureName);
    }

}
