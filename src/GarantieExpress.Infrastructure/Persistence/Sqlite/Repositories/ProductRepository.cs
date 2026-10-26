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

    public Task<OperationResult<Product>> AddAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        return operations.ExecuteAsync(
            "add product",
            cancellationToken,
            async () =>
            {
                await using var connection = await connections.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO Products (
                        Id, BrandModel, BrandModelKey, CategoryKey, PriceCents, DurationMonths)
                    VALUES (
                        $id, $brandModel, $brandModelKey, $categoryKey, $priceCents, $durationMonths);
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

    public Task<OperationResult<Product>> UpdateAsync(
        Product product,
        Product expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(expected);
        if (product.Id != expected.Id)
            throw new ArgumentException(
                "The product identity cannot change during an update.",
                nameof(product));

        return operations.ExecuteAsync(
            "update product",
            cancellationToken,
            async () =>
            {
                await using var connection = await connections.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE Products
                    SET BrandModel = $brandModel,
                        BrandModelKey = $brandModelKey,
                        CategoryKey = $categoryKey,
                        PriceCents = $priceCents,
                        DurationMonths = $durationMonths
                    WHERE Id = $id
                      AND BrandModel = $expectedBrandModel
                      AND BrandModelKey = $expectedBrandModelKey
                      AND CategoryKey = $expectedCategoryKey
                      AND PriceCents = $expectedPriceCents
                      AND DurationMonths = $expectedDurationMonths;
                    """;
                AddCurrentParameters(command, product);
                AddExpectedParameters(command, expected);
                var updated = await command.ExecuteNonQueryAsync(cancellationToken);
                return updated == 1
                    ? OperationResult<Product>.Success(product)
                    : StaleProduct<Product>();
            });
    }

    public Task<OperationResult> DeleteAsync(
        Product expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return operations.ExecuteAsync(
            "delete product",
            cancellationToken,
            async () =>
            {
                await using var connection = await connections.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    DELETE FROM Products
                    WHERE Id = $id
                      AND BrandModel = $expectedBrandModel
                      AND BrandModelKey = $expectedBrandModelKey
                      AND CategoryKey = $expectedCategoryKey
                      AND PriceCents = $expectedPriceCents
                      AND DurationMonths = $expectedDurationMonths;
                    """;
                command.Parameters.AddWithValue("$id", expected.Id.ToString("N"));
                AddExpectedParameters(command, expected);
                var deleted = await command.ExecuteNonQueryAsync(cancellationToken);
                return deleted == 1
                    ? OperationResult.Success()
                    : StaleProduct();
            });
    }

    private static void AddCurrentParameters(SqliteCommand command, Product product)
    {
        command.Parameters.AddWithValue("$id", product.Id.ToString("N"));
        command.Parameters.AddWithValue("$brandModel", product.BrandModel);
        command.Parameters.AddWithValue("$brandModelKey", product.BrandModelKey);
        command.Parameters.AddWithValue("$categoryKey", product.CategoryKey);
        command.Parameters.AddWithValue("$priceCents", product.PriceCents);
        command.Parameters.AddWithValue("$durationMonths", product.DurationMonths);
    }

    private static void AddExpectedParameters(SqliteCommand command, Product expected)
    {
        command.Parameters.AddWithValue("$expectedBrandModel", expected.BrandModel);
        command.Parameters.AddWithValue("$expectedBrandModelKey", expected.BrandModelKey);
        command.Parameters.AddWithValue("$expectedCategoryKey", expected.CategoryKey);
        command.Parameters.AddWithValue("$expectedPriceCents", expected.PriceCents);
        command.Parameters.AddWithValue("$expectedDurationMonths", expected.DurationMonths);
    }

    private static OperationResult StaleProduct() => OperationResult.Failure(
        new OperationError(
            "Storage.StaleProduct",
            "This product changed or was deleted by another session."));

    private static OperationResult<T> StaleProduct<T>() => OperationResult<T>.Failure(
        new OperationError(
            "Storage.StaleProduct",
            "This product changed or was deleted by another session."));
}
