using GarantieExpress.Application.Common;
using GarantieExpress.Application.Localization;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Products;

public sealed class ProductCatalogService(
    IProductRepository repository,
    ILocalizationService localization)
{
    public Task<OperationResult<IReadOnlyList<Product>>> LoadAsync(
        CancellationToken cancellationToken = default) =>
        repository.GetAllAsync(cancellationToken);

    public async Task<OperationResult<Product>> SaveAsync(
        string? brandModel,
        string? categoryKey,
        long priceCents,
        int durationMonths,
        Product? expected = null,
        CancellationToken cancellationToken = default)
    {
        var productResult = Product.Create(
            brandModel,
            categoryKey,
            priceCents,
            durationMonths,
            expected?.Id);
        return await productResult.Match(
            product => SaveValidatedAsync(product, expected, cancellationToken),
            error => Task.FromResult(OperationResult<Product>.Failure(new OperationError(
                $"Product.{error}",
                localization[error switch
                {
                    ProductError.BrandModelInvalid => "Products_BrandModelRequired",
                    ProductError.CategoryInvalid => "Products_CategoryRequired",
                    ProductError.PriceInvalid => "Products_PriceInvalid",
                    ProductError.DurationInvalid => "Products_DurationInvalid",
                    _ => "Products_SaveFailed"
                }]))));
    }

    private async Task<OperationResult<Product>> SaveValidatedAsync(
        Product product,
        Product? expected,
        CancellationToken cancellationToken)
    {
        var currentProducts = await repository.GetAllAsync(cancellationToken);
        if (currentProducts is OperationResult<IReadOnlyList<Product>>.Failed failed)
            return OperationResult<Product>.Failure(failed.Details);

        if (((OperationResult<IReadOnlyList<Product>>.Succeeded)currentProducts).Value.Any(existing =>
                existing.Id != product.Id
                && existing.BrandModelKey == product.BrandModelKey))
        {
            return OperationResult<Product>.Failure(new OperationError(
                "Product.Duplicate",
                localization["Products_Duplicate"]));
        }

        var saved = expected is null
            ? await repository.AddAsync(product, cancellationToken)
            : await repository.UpdateAsync(product, expected, cancellationToken);
        return saved is OperationResult<Product>.Failed { Details.Code: "Storage.StaleProduct" }
            ? OperationResult<Product>.Failure(new OperationError(
                "Storage.StaleProduct",
                localization["Products_EditConflict"]))
            : saved;
    }

    public async Task<OperationResult> DeleteAsync(
        Product expected,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        var deleted = await repository.DeleteAsync(expected, cancellationToken);
        return deleted is OperationResult.Failed { Details.Code: "Storage.StaleProduct" }
            ? OperationResult.Failure(new OperationError(
                "Storage.StaleProduct",
                localization["Products_EditConflict"]))
            : deleted;
    }
}
