using GarantieExpress.Application.Common;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Templates;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed class WarrantyIssuancePolicy(
    ILocalizationService localization,
    ITemplateKeyResolver templates)
{
    public OperationResult<WarrantyIssuePlan> Validate(
        StoreSettings settings,
        Category category,
        DraftWarranty draft,
        IReadOnlyList<Category> catalog,
        DateTimeOffset issuedAt,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(catalog);

        if (draft.Version < 0)
            return Failure("Warranty.InvalidDraft", "Error_InvalidData");

        if (category.IsArchived
            || !string.Equals(category.Key, draft.CategoryKey, StringComparison.Ordinal)
            || catalog.All(item => item.IsArchived
                || !string.Equals(item.Key, category.Key, StringComparison.Ordinal)))
        {
            return Failure(
                "Warranty.CategoryRequired",
                "Validation_CategoryRequired",
                nameof(DraftWarranty.CategoryKey));
        }

        var issuance = WarrantyIssuance.Create(
            draft.ServiceType,
            draft.RepairNote,
            draft.CategoryKey,
            draft.BrandModel,
            draft.Serial,
            draft.PurchaseDate,
            draft.ValidFrom,
            draft.PriceCents ?? 0,
            draft.DurationMonths,
            templates.ResolveKey(draft.TemplateKey),
            issuedAt,
            today);

        return issuance.Match(
            value => ValidateConfiguration(settings, value, draft),
            MapIssuanceError);
    }

    private OperationResult<WarrantyIssuePlan> ValidateConfiguration(
        StoreSettings settings,
        WarrantyIssuance issuance,
        DraftWarranty draft)
    {
        if (!settings.OfferedDurationsMonths.Contains(issuance.Duration.Months))
        {
            return Failure(
                "Warranty.DurationNotOffered",
                "Validation_DurationRequired",
                nameof(DraftWarranty.DurationMonths));
        }

        return OperationResult<WarrantyIssuePlan>.Success(
            new WarrantyIssuePlan(issuance, settings.NumberPrefix, draft));
    }

    private OperationResult<WarrantyIssuePlan> MapIssuanceError(
        WarrantyIssuanceError error) => error switch
        {
            WarrantyIssuanceError.ServiceTypeRequired =>
                Failure("Warranty.ServiceTypeRequired", "Validation_ServiceTypeRequired", nameof(DraftWarranty.ServiceType)),
            WarrantyIssuanceError.RepairNoteTooLong =>
                Failure("Warranty.RepairNoteTooLong", "Validation_RepairNoteLength", nameof(DraftWarranty.RepairNote)),
            WarrantyIssuanceError.CategoryRequired =>
                Failure("Warranty.CategoryRequired", "Validation_CategoryRequired", nameof(DraftWarranty.CategoryKey)),
            WarrantyIssuanceError.BrandModelRequired =>
                Failure("Warranty.BrandModelRequired", "Validation_BrandModelRequired", nameof(DraftWarranty.BrandModel)),
            WarrantyIssuanceError.BrandModelTooLong =>
                Failure("Warranty.BrandModelTooLong", "Validation_BrandModelLength", nameof(DraftWarranty.BrandModel)),
            WarrantyIssuanceError.IdentifierRequired =>
                Failure("Warranty.IdentifierRequired", "Validation_SerialRequired", nameof(DraftWarranty.Serial)),
            WarrantyIssuanceError.IdentifierTooLong =>
                Failure("Warranty.IdentifierTooLong", "Validation_SerialLength", nameof(DraftWarranty.Serial)),
            WarrantyIssuanceError.PurchaseDateInvalid =>
                Failure("Warranty.InvalidPurchaseDate", "Validation_PurchaseDateFuture", nameof(DraftWarranty.PurchaseDate)),
            WarrantyIssuanceError.ValidFromRequired =>
                Failure("Warranty.ValidFromRequired", "Validation_ValidFromRequired", nameof(DraftWarranty.ValidFrom)),
            WarrantyIssuanceError.ValidFromOutOfRange =>
                Failure("Warranty.ValidFromOutOfRange", "Validation_ValidFromRange", nameof(DraftWarranty.ValidFrom)),
            WarrantyIssuanceError.PriceInvalid =>
                Failure("Warranty.InvalidPrice", "Validation_PricePositive", "Price"),
            WarrantyIssuanceError.DurationInvalid =>
                Failure("Warranty.InvalidDuration", "Validation_DurationRequired", nameof(DraftWarranty.DurationMonths)),
            WarrantyIssuanceError.NumberPrefixInvalid =>
                Failure("Warranty.InvalidNumberPrefix", "Validation_NumberPrefix", nameof(StoreSettings.NumberPrefix)),
            WarrantyIssuanceError.TemplateInvalid =>
                Failure("Warranty.InvalidTemplate", "Error_InvalidData", nameof(DraftWarranty.TemplateKey)),
            WarrantyIssuanceError.IssuedAtInvalid =>
                Failure("Warranty.InvalidIssuedAt", "Error_InvalidData"),
            _ => Failure("Warranty.InvalidInput", "Error_InvalidData")
        };

    private OperationResult<WarrantyIssuePlan> Failure(
        string code,
        string resourceKey,
        string? field = null) =>
        OperationResult<WarrantyIssuePlan>.Failure(
            new OperationError(code, localization[resourceKey], field));
}
