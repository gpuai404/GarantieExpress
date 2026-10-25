using System.Collections.ObjectModel;
using System.Globalization;
using WarrantyCategory = GarantieExpress.Domain.Category;

namespace GarantieExpress.Domain;

public sealed class WarrantySnapshot
{
    public const int MaximumIdLength = WarrantyRecord.MaximumSnapshotIdLength;
    public const int MaximumBarItemCount = CategorySelection.MaximumBarItems;
    public const int MaximumBarItemLength = WarrantyCategory.MaximumNameLength;
    public const int MaximumDurationLabelLength = 80;
    public const int MaximumLabelLength = 160;
    public const int MaximumCultureNameLength = 32;

    private readonly ThemePalette _theme;

    private WarrantySnapshot(
        string id,
        StoreDetails store,
        ThemePalette theme,
        string? logoDataUri,
        string fullTerms,
        string shortTerms,
        CategorySnapshot category,
        IReadOnlyList<string> categoryBarItems,
        IReadOnlyList<DurationLabelSnapshot> durations,
        WarrantyLabels labels,
        string cultureName)
    {
        Id = id;
        Store = new StoreDetails(
            store.Name,
            store.Slogan,
            Array.AsReadOnly(store.Services.ToArray()),
            store.Address,
            store.Phone,
            store.Vat);
        _theme = theme;
        LogoDataUri = logoDataUri;
        FullTerms = fullTerms;
        ShortTerms = shortTerms;
        Category = new CategorySnapshot(
            category.Key,
            category.Name,
            Array.AsReadOnly(category.Fields.ToArray()),
            new ReadOnlyDictionary<IllustrationVariant, string>(
                category.Svgs.ToDictionary(item => item.Key, item => item.Value)));
        CategoryBarItems = Array.AsReadOnly(categoryBarItems.ToArray());
        Durations = Array.AsReadOnly(durations.ToArray());
        Labels = labels;
        CultureName = cultureName;
    }

    public string Id { get; }
    public StoreDetails Store { get; }
    public ThemePalette Theme => _theme;
    public string? LogoDataUri { get; }
    public string FullTerms { get; }
    public string ShortTerms { get; }
    public CategorySnapshot Category { get; }
    public IReadOnlyList<string> CategoryBarItems { get; }
    public IReadOnlyList<DurationLabelSnapshot> Durations { get; }
    public WarrantyLabels Labels { get; }
    public string CultureName { get; }

    public static DomainResult<WarrantySnapshot, WarrantySnapshotError> CreateNew(
        StoreDetails? store,
        ThemePalette? theme,
        string? logoDataUri,
        string? fullTerms,
        string? shortTerms,
        CategorySnapshot? category,
        IReadOnlyList<string>? categoryBarItems,
        IReadOnlyList<DurationLabelSnapshot>? durations,
        WarrantyLabels? labels,
        string? cultureName) =>
        CreateCore(
            store,
            theme,
            logoDataUri,
            fullTerms,
            shortTerms,
            category,
            categoryBarItems,
            durations,
            labels,
            cultureName);

    public DomainResult<WarrantySnapshot, WarrantySnapshotError> AssignId(string? id)
    {
        var normalizedId = id?.Trim();
        if (normalizedId is not { Length: >= 1 and <= MaximumIdLength })
            return Failure(WarrantySnapshotError.IdInvalid);

        return DomainResult<WarrantySnapshot, WarrantySnapshotError>.Success(new WarrantySnapshot(
            normalizedId,
            Store,
            Theme,
            LogoDataUri,
            FullTerms,
            ShortTerms,
            Category,
            CategoryBarItems,
            Durations,
            Labels,
            CultureName));
    }

    private static DomainResult<WarrantySnapshot, WarrantySnapshotError> CreateCore(
        StoreDetails? store,
        ThemePalette? theme,
        string? logoDataUri,
        string? fullTerms,
        string? shortTerms,
        CategorySnapshot? category,
        IReadOnlyList<string>? categoryBarItems,
        IReadOnlyList<DurationLabelSnapshot>? durations,
        WarrantyLabels? labels,
        string? cultureName)
    {
        if (!IsValidStore(store)) return Failure(WarrantySnapshotError.StoreInvalid);

        if (theme is null) return Failure(WarrantySnapshotError.ThemeInvalid);
        if (!StoreProfile.IsValidLogoDataUri(logoDataUri))
            return Failure(WarrantySnapshotError.LogoInvalid);

        var validatedFullTerms = fullTerms ?? string.Empty;
        var validatedShortTerms = shortTerms ?? string.Empty;
        if (string.IsNullOrWhiteSpace(validatedFullTerms)
            || validatedFullTerms.Length > WarrantyPolicy.MaximumFullTermsLength
            || string.IsNullOrWhiteSpace(validatedShortTerms)
            || validatedShortTerms.Length > WarrantyPolicy.MaximumShortTermsLength)
        {
            return Failure(WarrantySnapshotError.TermsInvalid);
        }

        if (!IsValidCategory(category)) return Failure(WarrantySnapshotError.CategoryInvalid);
        if (!IsValidFields(category!.Fields))
            return Failure(WarrantySnapshotError.CategoryFieldsInvalid);
        if (!IsValidIllustrations(category.Svgs))
            return Failure(WarrantySnapshotError.IllustrationsInvalid);
        if (!IsValidCategoryBar(categoryBarItems))
            return Failure(WarrantySnapshotError.CategoryBarInvalid);
        if (!IsValidDurations(durations)) return Failure(WarrantySnapshotError.DurationsInvalid);
        if (!IsValidLabels(labels)) return Failure(WarrantySnapshotError.LabelsInvalid);

        var normalizedCulture = cultureName?.Trim() ?? string.Empty;
        if (!IsValidCulture(normalizedCulture)) return Failure(WarrantySnapshotError.CultureInvalid);

        return DomainResult<WarrantySnapshot, WarrantySnapshotError>.Success(new WarrantySnapshot(
            string.Empty,
            store!,
            theme,
            logoDataUri,
            validatedFullTerms,
            validatedShortTerms,
            category,
            categoryBarItems!,
            durations!,
            labels!,
            normalizedCulture));
    }

    private static bool IsValidStore(StoreDetails? store) =>
        store is not null
        && store.Name is { Length: > 0 and <= StoreProfile.MaximumNameLength }
        && store.Slogan is { Length: <= StoreProfile.MaximumSloganLength }
        && store.Address is { Length: <= StoreProfile.MaximumAddressLength }
        && store.Phone is { Length: <= StoreProfile.MaximumContactLength }
        && store.Vat is { Length: <= StoreProfile.MaximumContactLength }
        && store.Services is { Count: <= StoreProfile.MaximumServiceCount }
        && store.Services.All(service => service is { Length: > 0 and <= StoreProfile.MaximumServiceLength });

    private static bool IsValidCategory(CategorySnapshot? category) =>
        category is not null
        && category.Key is { Length: > 0 and <= WarrantyCategory.MaximumKeyLength }
        && category.Name is { Length: > 0 and <= WarrantyCategory.MaximumNameLength };

    private static bool IsValidFields(IReadOnlyList<FieldSpec>? fields) =>
        fields is { Count: >= 1 and <= WarrantyCategory.MaximumFieldCount }
        && fields.All(field => field is not null);

    private static bool IsValidIllustrations(
        IReadOnlyDictionary<IllustrationVariant, string>? illustrations) =>
        illustrations is { Count: >= 1 and <= WarrantyCategory.MaximumIllustrationCount }
        && illustrations.All(item => Enum.IsDefined(item.Key) && !string.IsNullOrWhiteSpace(item.Value));

    private static bool IsValidCategoryBar(IReadOnlyList<string>? items) =>
        items is { Count: <= MaximumBarItemCount }
        && items.All(item => item is { Length: > 0 and <= MaximumBarItemLength });

    private static bool IsValidDurations(IReadOnlyList<DurationLabelSnapshot>? durations) =>
        durations is { Count: >= 1 and <= WarrantyPolicy.MaximumDurationCount }
        && durations.Select(duration => duration?.Months).Distinct().Count() == durations.Count
        && durations.All(duration => duration is not null
            && WarrantyDuration.Create(duration.Months).IsSuccess
            && duration.Label is { Length: > 0 and <= MaximumDurationLabelLength }
            && duration.BadgeText is { Length: > 0 and <= MaximumDurationLabelLength });

    private static bool IsValidLabels(WarrantyLabels? labels)
    {
        if (labels is null) return false;

        string?[] required =
        [
            labels.CertificateEyebrow, labels.CertificateTitle, labels.ProductDetails,
            labels.RegisteredProduct, labels.ProductType, labels.BrandModel, labels.Purchase,
            labels.PurchaseDate, labels.Price, labels.Warranty, labels.ValidUntil, labels.Terms,
            labels.Customer, labels.Signature, labels.IssuedOn, labels.IdentifierHint
        ];
        string?[] optional =
        [
            labels.ValidFrom, labels.ServiceType, labels.ServiceSale,
            labels.ServiceRepair, labels.RepairNote
        ];

        return labels.CustomerName is { Length: <= MaximumLabelLength }
            && required.All(label => label is { Length: > 0 and <= MaximumLabelLength })
            && optional.All(label => label is null or { Length: > 0 and <= MaximumLabelLength });
    }

    private static bool IsValidCulture(string cultureName)
    {
        if (cultureName.Length is < 1 or > MaximumCultureNameLength) return false;
        try
        {
            _ = CultureInfo.GetCultureInfo(cultureName);
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static DomainResult<WarrantySnapshot, WarrantySnapshotError> Failure(
        WarrantySnapshotError error) =>
        DomainResult<WarrantySnapshot, WarrantySnapshotError>.Failure(error);
}
