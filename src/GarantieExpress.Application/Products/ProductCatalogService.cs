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
        Guid? id = null,
        CancellationToken cancellationToken = default)
    {
        var productResult = Product.Create(brandModel, categoryKey, priceCents, durationMonths, id);
        return await productResult.Match(
            product => SaveValidatedAsync(product, cancellationToken),
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

        return await repository.SaveAsync(product, cancellationToken);
    }

    public Task<OperationResult> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        repository.DeleteAsync(id, cancellationToken);
}
