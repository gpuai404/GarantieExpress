using System.ComponentModel.DataAnnotations;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Warranties;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;
using GarantieExpress.UI.Localization;
using GarantieExpress.UI.Validation;

namespace GarantieExpress.UI.Features.Issue.Models;

public sealed class IssueWarrantyForm : IValidatableObject
{
    public WarrantyServiceType ServiceType { get; set; } = WarrantyServiceType.Sale;

    [LocalizedStringLength(WarrantyRules.MaximumRepairNoteLength, "Validation_RepairNoteLength")]
    public string RepairNote { get; set; } = string.Empty;

    [LocalizedRequired("Validation_CategoryRequired")]
    public string CategoryKey { get; set; } = "smartphone";

    [LocalizedRequired("Validation_BrandModelRequired"), LocalizedStringLength(WarrantyIssuance.MaximumBrandModelLength, "Validation_BrandModelLength")]
    public string BrandModel { get; set; } = string.Empty;

    [LocalizedRequired("Validation_SerialRequired"), LocalizedStringLength(WarrantyIssuance.MaximumIdentifierLength, "Validation_SerialLength")]
    public string Serial { get; set; } = string.Empty;

    public DateOnly PurchaseDate { get; set; }

    [LocalizedRequired("Validation_ValidFromRequired")]
    public DateOnly? ValidFrom { get; set; }

    [LocalizedPositivePrice]
    public decimal? Price { get; set; }

    [LocalizedDuration]
    public int DurationMonths { get; set; } = 24;

    public string TemplateKey { get; set; } = "a4-reference";
    public DateOnly Today { get; set; }

    public static IssueWarrantyForm FromDraft(DraftWarranty draft, DateOnly today) => new()
    {
        ServiceType = draft.ServiceType,
        RepairNote = draft.RepairNote ?? string.Empty,
        CategoryKey = draft.CategoryKey,
        BrandModel = draft.BrandModel,
        Serial = draft.Serial,
        PurchaseDate = draft.PurchaseDate,
        ValidFrom = draft.ValidFrom ?? today,
        Price = draft.PriceCents / 100m,
        DurationMonths = draft.DurationMonths,
        TemplateKey = draft.TemplateKey,
        Today = today
    };

    public DraftWarranty ToDraft(DateTimeOffset savedAt)
    {
        long? priceCents = WarrantyRules.TryConvertPriceToCents(Price ?? 0, out var converted)
            ? converted
            : null;
        return new DraftWarranty
        {
            ServiceType = ServiceType,
            RepairNote = ServiceType == WarrantyServiceType.Repair
                ? WarrantyRules.NormalizeRepairNote(RepairNote)
                : null,
            CategoryKey = CategoryKey,
            BrandModel = BrandModel,
            Serial = Serial,
            PurchaseDate = PurchaseDate,
            ValidFrom = ValidFrom,
            PriceCents = priceCents,
            DurationMonths = DurationMonths,
            TemplateKey = TemplateKey,
            SavedAt = savedAt
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var localization = validationContext.GetService(typeof(ILocalizationService)) as ILocalizationService;

        if (!WarrantyRules.IsValidPurchaseDate(PurchaseDate, Today))
        {
            yield return new ValidationResult(
                GetMessage(localization, "Validation_PurchaseDateFuture"),
                [nameof(PurchaseDate)]);
        }

        if (ValidFrom is not null && DurationMonths is >= 1 and <= WarrantyDuration.MaximumMonths
            && !WarrantyDateCalculator.CanCalculateEndDate(ValidFrom, DurationMonths))
        {
            yield return new ValidationResult(
                GetMessage(localization, "Validation_ValidFromRange"),
                [nameof(ValidFrom)]);
        }
    }

    private static string GetMessage(ILocalizationService? localization, string key) =>
        localization?[key] ?? Translations.Get(key);
}
