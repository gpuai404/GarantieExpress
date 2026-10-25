using Microsoft.Data.Sqlite;
using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Common;
using GarantieExpress.Domain;
using GarantieExpress.Infrastructure.Persistence.Payloads;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Repositories;

internal sealed class CategoryRepository(
    SqliteConnectionFactory connections,
    SqliteOperationRunner<CategoryRepository> operations) : ICategoryRepository
{
    public async Task<OperationResult<IReadOnlyList<Category>>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await operations.ExecuteAsync("load categories", cancellationToken, async () =>
        {
            var categories = new List<Category>();
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT Key, PayloadJson, IsArchived, ShowInBar, SortOrder, Version
                FROM Categories
                ORDER BY SortOrder, Key;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var category = CategoryRowMapper.Read(reader);
                if (category is OperationResult<Category>.Failed failed)
                    return OperationResult<IReadOnlyList<Category>>.Failure(failed.Details);

                categories.Add(
                    ((OperationResult<Category>.Succeeded)category).Value);
            }
            return OperationResult<IReadOnlyList<Category>>.Success(categories);
        });
    }

    public Task<OperationResult<Category>> SaveAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        return operations.ExecuteAsync("save category", cancellationToken, async () =>
        {
            var payload = category.IsBuiltIn
                ? null
                : SqliteStoredData.Serialize(CategoryPayloadMapper.FromDomain(category));
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (category.ShowInBar && !category.IsArchived)
            {
                command.CommandText = """
                    SELECT COUNT(*) FROM Categories
                    WHERE ShowInBar = 1 AND IsArchived = 0 AND Key <> $key;
                    """;
                command.Parameters.AddWithValue("$key", category.Key);
                var visibleCountValue = await command.ExecuteScalarAsync(cancellationToken);
                var visibleCount = SqliteStoredData.Decode(
                    () => Convert.ToInt32(visibleCountValue));
                if (visibleCount >= CategorySelection.MaximumBarItems)
                {
                    return OperationResult<Category>.Failure(new OperationError(
                        "Category.BarLimit",
                        "No more than five categories can be shown in the category bar."));
                }
            }

            command.CommandText = category.Version == 0 ? """
                INSERT INTO Categories (Key, PayloadJson, IsArchived, ShowInBar, SortOrder, Version)
                VALUES ($key, $payload, $archived, $shown, $order, 1)
                ON CONFLICT(Key) DO NOTHING
                RETURNING Version;
                """ : """
                UPDATE Categories
                SET PayloadJson = $payload, IsArchived = $archived, ShowInBar = $shown,
                    SortOrder = $order, Version = Version + 1
                WHERE Key = $key AND Version = $expectedVersion
                RETURNING Version;
                """;
            command.Parameters.Clear();
            command.Parameters.AddWithValue("$key", category.Key);
            command.Parameters.AddWithValue("$payload", (object?)payload ?? DBNull.Value);
            command.Parameters.AddWithValue("$archived", category.IsArchived);
            command.Parameters.AddWithValue("$shown", category.ShowInBar);
            command.Parameters.AddWithValue("$order", category.SortOrder);
            command.Parameters.AddWithValue("$expectedVersion", category.Version);
            var version = await command.ExecuteScalarAsync(cancellationToken);
            if (version is null)
            {
                return OperationResult<Category>.Failure(new OperationError(
                    "Storage.StaleCategory", "This category was changed by another session."));
            }

            await transaction.CommitAsync(cancellationToken);
            return category.WithVersion(
                SqliteStoredData.Decode(() => Convert.ToInt64(version))).Match(
                OperationResult<Category>.Success,
                error => OperationResult<Category>.Failure(new OperationError(
                    $"Storage.InvalidCategory.{error}",
                    "The saved category version is invalid.")));
        });
    }
}
