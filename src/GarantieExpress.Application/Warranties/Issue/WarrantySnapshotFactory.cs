using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Warranties;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Warranties.Issue;

public sealed class WarrantySnapshotFactory(
    WarrantyDurationFormatter durationFormatter,
    ILocalizationService localization,
    CategoryDisplayResolver categoryDisplay)
{
    public OperationResult<WarrantySnapshot> Create(
        StoreSettings settings,
        Category category,
        IReadOnlyList<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(categories);

        var catalog = new CategorySelection(categories);

        var terms = string.IsNullOrWhiteSpace(category.TermsOverride) ? settings.DefaultTerms : category.TermsOverride;
        var resolvedFields = new List<FieldSpec>(category.Fields.Count);
        foreach (var field in category.Fields)
        {
            var resolved = field.WithResolvedLabel(categoryDisplay.FieldLabel(category, field));
            if (resolved is DomainResult<FieldSpec, FieldSpecError>.Failed)
            {
                return OperationResult<WarrantySnapshot>.Failure(new OperationError(
                    $"Warranty.InvalidSnapshot.{WarrantySnapshotError.CategoryFieldsInvalid}",
                    localization["Error_InvalidData"]));
            }

            resolvedFields.Add(
                ((DomainResult<FieldSpec, FieldSpecError>.Succeeded)resolved).Value);
        }

        var snapshot = WarrantySnapshot.CreateNew(
            new StoreDetails(settings.Name, settings.Slogan, settings.Services.ToList(), settings.Address, settings.Phone, settings.Vat),
            settings.Theme,
            settings.LogoDataUri,
            terms,
            string.IsNullOrWhiteSpace(category.TermsOverride) ? settings.ShortTerms : category.TermsOverride,
            new CategorySnapshot(
                category.Key,
                categoryDisplay.DisplayName(category),
                resolvedFields,
                category.Svgs.OrderBy(item => item.Key).ToDictionary(item => item.Key, item => item.Value)),
            catalog.BarItems()
                .Select(categoryDisplay.DisplayName)
                .ToList(),
            settings.OfferedDurationsMonths
                .Select(months => new DurationLabelSnapshot(
                    months,
                    durationFormatter.FormatDuration(months),
                    durationFormatter.BuildBadgeText(months)))
                .ToList(),
            WarrantyLabelFactory.Create(localization),
            localization.CurrentLanguage);

        return snapshot
            .Bind(value => value.AssignId(WarrantySnapshotHasher.Compute(value)))
            .Match(
                OperationResult<WarrantySnapshot>.Success,
                error => OperationResult<WarrantySnapshot>.Failure(new OperationError(
                    $"Warranty.InvalidSnapshot.{error}",
                    localization["Error_InvalidData"])));
    }
}
