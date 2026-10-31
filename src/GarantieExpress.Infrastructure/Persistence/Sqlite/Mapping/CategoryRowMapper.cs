using Microsoft.Data.Sqlite;
using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Common;
using GarantieExpress.Domain;
using GarantieExpress.Infrastructure.Persistence.Payloads;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

internal static class CategoryRowMapper
{
    internal static OperationResult<Category> Read(SqliteDataReader reader)
    {
        try
        {
            var key = reader.GetString(0);
            var state = CategoryCatalogState.Create(
                reader.GetInt64(2) != 0,
                reader.GetInt64(3) != 0,
                reader.GetInt32(4),
                reader.GetInt64(5));
            if (state is DomainResult<CategoryCatalogState, CategoryError>.Failed stateFailure)
                return Invalid(stateFailure.Error.ToString());

            var catalogState =
                ((DomainResult<CategoryCatalogState, CategoryError>.Succeeded)state).Value;
            if (reader.IsDBNull(1)) return ReadBuiltIn(key, catalogState);

            var payload = SqliteStoredData.Deserialize<CategoryPayload>(
                reader.GetString(1));
            return CategoryPayloadMapper.ToDomain(
                    payload,
                    key,
                    catalogState.IsArchived,
                    catalogState.ShowInBar,
                    catalogState.SortOrder,
                    catalogState.Version)
                .Match(
                    OperationResult<Category>.Success,
                    error => Invalid(error.ToString()));
        }
        catch (Exception exception) when (
            exception is FormatException
                or InvalidCastException
                or OverflowException
                or InvalidStoredDataException)
        {
            return Invalid("DecodeFailed");
        }
    }

    private static OperationResult<Category> ReadBuiltIn(
        string key,
        CategoryCatalogState state)
    {
        var category = BuiltInCategories.All.FirstOrDefault(item => item.Key == key);
        return category is null
            ? Invalid("UnknownBuiltIn")
            : OperationResult<Category>.Success(category.WithCatalogState(state));
    }

    private static OperationResult<Category> Invalid(string detail) =>
        OperationResult<Category>.Failure(new OperationError(
            $"Storage.InvalidCategory.{detail}",
            "A stored category is invalid."));
}
