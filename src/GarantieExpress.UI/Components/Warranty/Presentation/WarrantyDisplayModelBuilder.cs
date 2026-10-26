using System.Globalization;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Settings;
using GarantieExpress.Application.Warranties;
using GarantieExpress.Application.Warranties.Drafts;
using GarantieExpress.Application.Warranties.Issue;
using GarantieExpress.Domain;
using GarantieExpress.UI.Localization;

namespace GarantieExpress.UI.Components.Warranty.Presentation;

public sealed class WarrantyDisplayModelBuilder(
    WarrantyDurationFormatter durationFormatter,
    ILocalizationService localization,
    CategoryDisplayResolver categoryDisplay,
    TimeProvider timeProvider)
{
    public WarrantyDisplayModel Build(
        StoreSettings settings,
        Category category,
        DraftWarranty draft,
        string number = "— CONCEPT —",
        IllustrationVariant illustration = IllustrationVariant.Detailed)
    {
        var duration = draft.DurationMonths > 0
            ? draft.DurationMonths
            : settings.OfferedDurationsMonths.FirstOrDefault(12);
        var identifier = categoryDisplay.PrimaryIdentifier(category);
        var terms = string.IsNullOrWhiteSpace(category.TermsOverride) ? settings.DefaultTerms : category.TermsOverride;
        var purchaseDate = draft.PurchaseDate;
        var endDate = WarrantyDateCalculator.CalculateEndDate(draft.ValidFrom, duration);
        var culture = GetSnapshotCulture(localization.CurrentLanguage);
        var labels = WarrantyLabelFactory.Create(localization);

        return new WarrantyDisplayModel(
            number,
            settings.Name,
            settings.Slogan,
            settings.Services,
            settings.LogoDataUri,
            settings.Address,
            settings.Phone,
            settings.Vat,
            categoryDisplay.DisplayName(category),
            categoryDisplay.ResolveIllustration(category, illustration),
            string.IsNullOrWhiteSpace(draft.BrandModel) ? localization["Template_PlaceholderBrandModel"] : draft.BrandModel.Trim(),
            categoryDisplay.FieldLabel(category, identifier),
            string.IsNullOrWhiteSpace(draft.Serial) ? localization["Template_PlaceholderSerial"] : draft.Serial.Trim(),
            purchaseDate.ToString("dd.MM.yyyy", culture),
            draft.PriceCents is { } priceCents
                ? $"€ {(priceCents / 100m).ToString("N2", culture)}"
                : "—",
            settings.OfferedDurationsMonths.Select(months => new DurationOption(months, durationFormatter.FormatDuration(months), months == duration)).ToList(),
            endDate?.ToString("dd.MM.yyyy", culture) ?? "—",
            durationFormatter.BuildBadgeText(duration),
            terms,
            string.IsNullOrWhiteSpace(category.TermsOverride) ? settings.ShortTerms : category.TermsOverride,
            DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime).ToString("dd.MM.yyyy", culture),
            settings.Theme,
            labels,
            draft.ValidFrom?.ToString("dd.MM.yyyy", culture) ?? "—",
            BuildServiceOptions(labels, draft.ServiceType),
            draft.ServiceType == WarrantyServiceType.Repair ? WarrantyRules.NormalizeRepairNote(draft.RepairNote) : null);
    }

    public WarrantyDisplayModel Build(
        WarrantyRecord warranty,
        WarrantySnapshot snapshot,
        IllustrationVariant illustration = IllustrationVariant.Detailed)
    {
        var culture = GetSnapshotCulture(snapshot.CultureName);
        var purchaseDate = warranty.PurchaseDate;
        var duration = snapshot.Durations.FirstOrDefault(item => item.Months == warranty.DurationMonths)
            ?? new DurationLabelSnapshot(warranty.DurationMonths, warranty.DurationMonths.ToString(culture), warranty.DurationMonths.ToString(culture));
        var identifier = snapshot.Category.Fields.FirstOrDefault(field => field.Kind is WarrantyFieldKind.Imei or WarrantyFieldKind.SerialNumber)
            ?? snapshot.Category.Fields[0];
        var endDate = warranty.ValidUntil
            ?? WarrantyDateCalculator.CalculateEndDate(purchaseDate, warranty.DurationMonths);

        return new WarrantyDisplayModel(
            warranty.Number,
            snapshot.Store.Name,
            snapshot.Store.Slogan,
            snapshot.Store.Services,
            snapshot.LogoDataUri,
            snapshot.Store.Address,
            snapshot.Store.Phone,
            snapshot.Store.Vat,
            snapshot.Category.Name,
            categoryDisplay.ResolveIllustration(snapshot.Category, illustration),
            warranty.BrandModel,
            identifier.Label,
            warranty.Serial,
            purchaseDate.ToString("dd.MM.yyyy", culture),
            $"€ {(warranty.PriceCents / 100m).ToString("N2", culture)}",
            snapshot.Durations.Select(item => new DurationOption(item.Months, item.Label, item.Months == warranty.DurationMonths)).ToList(),
            // Records issued before explicit coverage dates retain their original purchase-based expiry.
            endDate?.ToString("dd.MM.yyyy", culture) ?? "—",
            duration.BadgeText,
            snapshot.FullTerms,
            snapshot.ShortTerms,
            DateOnly.FromDateTime(warranty.IssuedAt.DateTime).ToString("dd.MM.yyyy", culture),
            snapshot.Theme,
            snapshot.Labels,
            warranty.ValidFrom?.ToString("dd.MM.yyyy", culture),
            BuildServiceOptions(snapshot.Labels, warranty.ServiceType),
            warranty.ServiceType == WarrantyServiceType.Repair ? WarrantyRules.NormalizeRepairNote(warranty.RepairNote) : null);
    }

    // Records issued before service types existed have no frozen service labels.
    private static IReadOnlyList<ServiceOption> BuildServiceOptions(WarrantyLabels labels, WarrantyServiceType? serviceType) =>
        serviceType is null || labels.ServiceSale is null || labels.ServiceRepair is null
            ? []
            :
            [
                new ServiceOption(labels.ServiceSale, serviceType == WarrantyServiceType.Sale),
                new ServiceOption(labels.ServiceRepair, serviceType == WarrantyServiceType.Repair)
            ];

    private static CultureInfo GetSnapshotCulture(string cultureName)
    {
        try { return CultureInfo.GetCultureInfo(cultureName); }
        catch (CultureNotFoundException) { return CultureInfo.InvariantCulture; }
    }
}
