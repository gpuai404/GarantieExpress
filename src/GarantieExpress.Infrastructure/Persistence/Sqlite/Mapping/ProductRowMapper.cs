using Microsoft.Data.Sqlite;
using GarantieExpress.Application.Common;
using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

internal static class ProductRowMapper
{
    internal static OperationResult<Product> Read(SqliteDataReader reader)
    {
        try
        {
            if (!Guid.TryParseExact(reader.GetString(0), "N", out var id))
                return Invalid("IdInvalid");

            var storedKey = reader.GetString(2);
            return Product.Create(
                    reader.GetString(1),
                    reader.GetString(3),
                    reader.GetInt64(4),
                    reader.GetInt32(5),
                    id)
                .Match(
                    product => string.Equals(
                        product.BrandModelKey,
                        storedKey,
                        StringComparison.Ordinal)
                        ? OperationResult<Product>.Success(product)
                        : Invalid("BrandModelKeyInvalid"),
                    error => Invalid(error.ToString()));
        }
        catch (Exception exception) when (
            exception is FormatException or InvalidCastException or OverflowException)
        {
            return Invalid("DecodeFailed");
        }
    }

    private static OperationResult<Product> Invalid(string detail) =>
        OperationResult<Product>.Failure(new OperationError(
            $"Storage.InvalidProduct.{detail}",
            "A stored product is invalid."));
}
