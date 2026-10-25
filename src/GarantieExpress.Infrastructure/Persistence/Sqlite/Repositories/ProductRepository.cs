using Microsoft.Data.Sqlite;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Products;
using GarantieExpress.Domain;
using GarantieExpress.Infrastructure.Persistence.Sqlite.Mapping;

namespace GarantieExpress.Infrastructure.Persistence.Sqlite.Repositories;

internal sealed class ProductRepository(
    SqliteConnectionFactory connections,
    SqliteOperationRunner<ProductRepository> operations) : IProductRepository
{
    public Task<OperationResult<IReadOnlyList<Product>>> GetAllAsync(
        CancellationToken cancellationToken = default) =>
        operations.ExecuteAsync(
            "load products",
            cancellationToken,
            async () =>
            {
                await using var connection = await connections.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT Id, BrandModel, BrandModelKey, CategoryKey, PriceCents, DurationMonths
                    FROM Products
                    ORDER BY BrandModel COLLATE NOCASE, Id;
                    """;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var products = new List<Product>();
                while (await reader.ReadAsync(cancellationToken))
                {
                    var product = ProductRowMapper.Read(reader);
                    if (product is OperationResult<Product>.Failed failed)
                        return OperationResult<IReadOnlyList<Product>>.Failure(failed.Details);

                    products.Add(((OperationResult<Product>.Succeeded)product).Value);
                }

                return OperationResult<IReadOnlyList<Product>>.Success(products);
            });

    public Task<OperationResult<Product>> SaveAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        return operations.ExecuteAsync(
            "save product",
            cancellationToken,
            async () =>
            {
                await using var connection = await connections.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO Products (Id, BrandModel, BrandModelKey, CategoryKey, PriceCents, DurationMonths)
                    VALUES ($id, $brandModel, $brandModelKey, $categoryKey, $priceCents, $durationMonths)
                    ON CONFLICT(Id) DO UPDATE SET
                        BrandModel = excluded.BrandModel,
                        BrandModelKey = excluded.BrandModelKey,
                        CategoryKey = excluded.CategoryKey,
                        PriceCents = excluded.PriceCents,
                        DurationMonths = excluded.DurationMonths;
                    """;
                command.Parameters.AddWithValue("$id", product.Id.ToString("N"));
                command.Parameters.AddWithValue("$brandModel", product.BrandModel);
                command.Parameters.AddWithValue("$brandModelKey", product.BrandModelKey);
                command.Parameters.AddWithValue("$categoryKey", product.CategoryKey);
                command.Parameters.AddWithValue("$priceCents", product.PriceCents);
                command.Parameters.AddWithValue("$durationMonths", product.DurationMonths);
                await command.ExecuteNonQueryAsync(cancellationToken);
                return OperationResult<Product>.Success(product);
            });
    }

    public Task<OperationResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        operations.ExecuteAsync(
            "delete product",
            cancellationToken,
            async () =>
            {
                await using var connection = await connections.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM Products WHERE Id = $id;";
                command.Parameters.AddWithValue("$id", id.ToString("N"));
                var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
                return deleted == 1
                    ? OperationResult.Success()
                    : OperationResult.Failure(new OperationError(
                        "Product.NotFound",
                        "The product no longer exists."));
            });

}
