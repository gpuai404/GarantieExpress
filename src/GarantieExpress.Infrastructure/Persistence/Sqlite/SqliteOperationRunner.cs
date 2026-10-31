using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using GarantieExpress.Application.Common;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;
using GarantieExpress.Infrastructure.Storage;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite;

internal sealed class SqliteOperationRunner<TOwner>(
    DatabaseOperationGate operationGate,
    ILogger<TOwner> logger)
    where TOwner : class
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(75),
        TimeSpan.FromMilliseconds(200)
    ];

    internal Task<OperationResult<T>> ExecuteAsync<T>(
        string operationName,
        CancellationToken cancellationToken,
        Func<Task<OperationResult<T>>> operation) =>
        ExecuteCoreAsync(
            operationName,
            cancellationToken,
            operation,
            OperationResult<T>.Failure);

    internal Task<OperationResult> ExecuteAsync(
        string operationName,
        CancellationToken cancellationToken,
        Func<Task<OperationResult>> operation) =>
        ExecuteCoreAsync(
            operationName,
            cancellationToken,
            operation,
            OperationResult.Failure);

    private async Task<TResult> ExecuteCoreAsync<TResult>(
        string operationName,
        CancellationToken cancellationToken,
        Func<Task<TResult>> operation,
        Func<OperationError, TResult> failure)
    {
        using var lease = operationGate.TryEnter();
        if (lease is null)
        {
            return failure(new OperationError(
                "Storage.ShuttingDown",
                "The local application is shutting down."));
        }

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (SqliteException exception) when (
                IsBusy(exception) && attempt < RetryDelays.Length)
            {
                logger.LogWarning(
                    exception,
                    "SQLite operation {OperationName} was busy; retrying attempt {Attempt}.",
                    operationName,
                    attempt + 1);
                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
            catch (Exception exception) when (IsExpectedStorageFailure(exception))
            {
                logger.LogError(
                    exception,
                    "SQLite operation {OperationName} failed.",
                    operationName);
                return failure(ToOperationError(exception));
            }
        }
    }

    private static bool IsExpectedStorageFailure(Exception exception) =>
        exception is SqliteException or InvalidStoredDataException;

    private static bool IsBusy(SqliteException exception) =>
        exception.SqliteErrorCode is 5 or 6;

    private static OperationError ToOperationError(Exception exception) => exception switch
    {
        SqliteException { SqliteErrorCode: 5 or 6 } =>
            new("Storage.Busy", "The local database is busy. Please try again."),
        SqliteException { SqliteErrorCode: 19 } =>
            new("Storage.Constraint", "The local database rejected conflicting data."),
        SqliteException =>
            new("Storage.Unavailable", "The local database is unavailable."),
        InvalidStoredDataException =>
            new("Storage.InvalidData", "The local database contains invalid data."),
        _ => throw new ArgumentOutOfRangeException(
            nameof(exception),
            exception,
            "Unsupported storage exception.")
    };
}
