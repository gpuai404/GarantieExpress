using System.Text.Json.Serialization;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Payloads;

internal sealed class DraftWarrantyPayload
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }
    public WarrantyServiceType ServiceType { get; set; } = WarrantyServiceType.Sale;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RepairNote { get; set; }
    public string CategoryKey { get; set; } = "smartphone";
    public string BrandModel { get; set; } = string.Empty;
    public string Serial { get; set; } = string.Empty;
    public DateOnly PurchaseDate { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateOnly? ValidFrom { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? PriceCents { get; set; }
    public int DurationMonths { get; set; } = 24;
    public string TemplateKey { get; set; } = "a4-reference";
    public DateTimeOffset SavedAt { get; set; }
}

internal static class DraftWarrantyPayloadMapper
{
    public static DraftWarranty ToDomain(long version, DraftWarrantyPayload document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new DraftWarranty
        {
            Id = document.Id ?? string.Empty,
            Version = version,
            ServiceType = document.ServiceType,
            RepairNote = document.RepairNote,
            CategoryKey = document.CategoryKey,
            BrandModel = document.BrandModel,
            Serial = document.Serial,
            PurchaseDate = document.PurchaseDate,
            ValidFrom = document.ValidFrom,
            PriceCents = document.PriceCents,
            DurationMonths = document.DurationMonths,
            TemplateKey = document.TemplateKey,
            SavedAt = document.SavedAt
        };
    }

    public static DraftWarrantyPayload FromDomain(DraftWarranty draft)
    {
        ArgumentNullException.ThrowIfNull(draft);

        return new DraftWarrantyPayload
        {
            Id = string.IsNullOrEmpty(draft.Id) ? null : draft.Id,
            ServiceType = draft.ServiceType,
            RepairNote = draft.RepairNote,
            CategoryKey = draft.CategoryKey,
            BrandModel = draft.BrandModel,
            Serial = draft.Serial,
            PurchaseDate = draft.PurchaseDate,
            ValidFrom = draft.ValidFrom,
            PriceCents = draft.PriceCents,
            DurationMonths = draft.DurationMonths,
            TemplateKey = draft.TemplateKey,
            SavedAt = draft.SavedAt
        };
    }
}
