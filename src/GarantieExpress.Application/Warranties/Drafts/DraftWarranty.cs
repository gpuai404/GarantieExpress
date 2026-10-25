using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Drafts;

public sealed record DraftWarranty
{
    public long Version { get; init; }
    public WarrantyServiceType ServiceType { get; init; } = WarrantyServiceType.Sale;
    public string? RepairNote { get; init; }
    public string CategoryKey { get; init; } = "smartphone";
    public string BrandModel { get; init; } = string.Empty;
    public string Serial { get; init; } = string.Empty;
    public DateOnly PurchaseDate { get; init; }
    public DateOnly? ValidFrom { get; init; }
    public long? PriceCents { get; init; }
    public int DurationMonths { get; init; } = 24;
    public string TemplateKey { get; init; } = "a4-reference";
    public DateTimeOffset SavedAt { get; init; }
}
