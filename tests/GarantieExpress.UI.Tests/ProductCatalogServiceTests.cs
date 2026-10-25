using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Products;
using GarantieExpress.Domain;
using GarantieExpress.UI.Localization;
using Xunit;

namespace GarantieExpress.UI.Tests;

public sealed class ProductCatalogServiceTests
{
    [Fact]
    public async Task SaveAsync_creates_and_updates_products_independently_by_id()
    {
        var repository = new InMemoryProductRepository();
        var service = new ProductCatalogService(repository, new LocalizationService());

        var created = Assert.IsType<OperationResult<Product>.Succeeded>(
            await service.SaveAsync("Acme Phone", "smartphone", 49_900, 24)).Value;
        var updated = Assert.IsType<OperationResult<Product>.Succeeded>(
            await service.SaveAsync("Acme Phone Plus", "smartphone", 59_900, 36, created.Id)).Value;

        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Acme Phone Plus", updated.BrandModel);
        Assert.Equal(59_900, updated.PriceCents);
        Assert.Equal(36, updated.DurationMonths);
        Assert.Equal(updated, Assert.Single(repository.Products));
    }

    [Fact]
    public async Task SaveAsync_rejects_duplicate_normalized_brand_and_model()
    {
        var repository = new InMemoryProductRepository();
        var service = new ProductCatalogService(repository, new LocalizationService());
        await service.SaveAsync("Acme  Phone", "smartphone", 49_900, 24);

        var result = await service.SaveAsync(" acme phone ", "smartphone", 59_900, 24);

        var failed = Assert.IsType<OperationResult<Product>.Failed>(result);
        Assert.Equal("Product.Duplicate", failed.Details.Code);
        Assert.Single(repository.Products);
    }

    [Fact]
    public void Create_normalizes_display_data_and_rejects_invalid_prices()
    {
        var product = Product.Create("  Acme   Phone ", " smartphone ", 49_900, 12)
            .Match(value => value, error => throw new InvalidOperationException(error.ToString()));

        Assert.Equal("Acme Phone", product.BrandModel);
        Assert.Equal("smartphone", product.CategoryKey);
        Assert.False(Product.Create("Acme Phone", "smartphone", 0, 12).IsSuccess);
        Assert.False(Product.Create("Acme Phone", "smartphone", 49_900, 0).IsSuccess);
    }

    private sealed class InMemoryProductRepository : IProductRepository
    {
        public List<Product> Products { get; } = [];

        public Task<OperationResult<IReadOnlyList<Product>>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<IReadOnlyList<Product>>.Success(Products.ToList()));

        public Task<OperationResult<Product>> SaveAsync(
            Product product,
            CancellationToken cancellationToken = default)
        {
            Products.RemoveAll(existing => existing.Id == product.Id);
            Products.Add(product);
            return Task.FromResult(OperationResult<Product>.Success(product));
        }

        public Task<OperationResult> DeleteAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            Products.RemoveAll(product => product.Id == id);
            return Task.FromResult(OperationResult.Success());
        }
    }
}
