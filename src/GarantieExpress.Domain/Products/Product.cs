using System.Text;

namespace GarantieExpress.Domain;

public sealed class Product
{
    public const int MaximumBrandModelLength = 180;

    private Product(
        Guid id,
        string brandModel,
        CategoryKey category,
        WarrantyPrice price,
        WarrantyDuration duration)
    {
        Id = id;
        BrandModel = brandModel;
        BrandModelKey = NormalizeKey(brandModel);
        CategoryIdentity = category;
        Price = price;
        Duration = duration;
    }

    public Guid Id { get; }
    public string BrandModel { get; }
    public string BrandModelKey { get; }
    public CategoryKey CategoryIdentity { get; }
    public string CategoryKey => CategoryIdentity.Value;
    public WarrantyPrice Price { get; }
    public WarrantyDuration Duration { get; }
    public long PriceCents => Price.Cents;
    public int DurationMonths => Duration.Months;

    public static DomainResult<Product, ProductError> Create(
        string? brandModel,
        string? categoryKey,
        long priceCents,
        int durationMonths,
        Guid? id = null)
    {
        if (id == Guid.Empty)
            return Failure(ProductError.IdInvalid);

        var normalizedBrandModel = NormalizeDisplayName(brandModel);
        if (normalizedBrandModel is not { Length: >= 1 and <= MaximumBrandModelLength })
            return Failure(ProductError.BrandModelInvalid);

        var categoryResult = global::GarantieExpress.Domain.CategoryKey.Create(categoryKey);
        if (categoryResult is not DomainResult<CategoryKey, CategoryKeyError>.Succeeded categorySucceeded)
            return Failure(ProductError.CategoryInvalid);
        return WarrantyPrice.Create(priceCents).Match(
            price => WarrantyDuration.Create(durationMonths).Match(
                duration => DomainResult<Product, ProductError>.Success(new Product(
                    id ?? Guid.NewGuid(),
                    normalizedBrandModel,
                    categorySucceeded.Value,
                    price,
                    duration)),
                _ => Failure(ProductError.DurationInvalid)),
            _ => Failure(ProductError.PriceInvalid));
    }

    public static string NormalizeKey(string? value) =>
        NormalizeDisplayName(value)?.Normalize(NormalizationForm.FormKC).ToLowerInvariant()
        ?? string.Empty;

    private static string? NormalizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var result = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = result.Length > 0;
                continue;
            }

            if (pendingSpace) result.Append(' ');
            result.Append(character);
            pendingSpace = false;
        }

        return result.ToString();
    }

    private static DomainResult<Product, ProductError> Failure(ProductError error) =>
        DomainResult<Product, ProductError>.Failure(error);
}
