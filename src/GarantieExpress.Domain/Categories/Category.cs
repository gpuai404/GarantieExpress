using System.Collections.ObjectModel;

namespace GarantieExpress.Domain;

public sealed class Category
{
    public const int MaximumKeyLength = CategoryKey.MaximumLength;
    public const int MaximumNameLength = 80;
    public const int MaximumFieldLabelLength = FieldSpec.MaximumLabelLength;
    public const int MaximumResourceKeyLength = FieldSpec.MaximumResourceKeyLength;
    public const int MaximumTermsLength = 1_000;
    public const int MaximumDurationCount = 4;
    public const int MaximumFieldCount = 3;
    public const int MaximumIllustrationCount = 3;

    private readonly CategoryCatalogState _catalogState;

    private Category(
        CategoryKey identity,
        string name,
        string? nameResourceKey,
        bool isBuiltIn,
        IReadOnlyDictionary<IllustrationVariant, string> svgs,
        IReadOnlyList<WarrantyDuration> defaultDurations,
        IReadOnlyList<FieldSpec> fields,
        CategoryIdentifierMode identifierMode,
        string? termsOverride,
        CategoryCatalogState catalogState)
    {
        Identity = identity;
        Name = name;
        NameResourceKey = nameResourceKey;
        IsBuiltIn = isBuiltIn;
        Svgs = new ReadOnlyDictionary<IllustrationVariant, string>(
            svgs.ToDictionary(item => item.Key, item => item.Value));
        DefaultDurations = Array.AsReadOnly(defaultDurations.ToArray());
        DefaultDurationsMonths = Array.AsReadOnly(
            defaultDurations.Select(duration => duration.Months).ToArray());
        Fields = Array.AsReadOnly(fields.ToArray());
        IdentifierMode = identifierMode;
        TermsOverride = termsOverride;
        _catalogState = catalogState;
    }

    public CategoryKey Identity { get; }
    public string Key => Identity.Value;
    public string Name { get; }
    public string? NameResourceKey { get; }
    public bool IsBuiltIn { get; }
    public bool IsArchived => _catalogState.IsArchived;
    public bool ShowInBar => _catalogState.ShowInBar;
    public int SortOrder => _catalogState.SortOrder;
    public IReadOnlyDictionary<IllustrationVariant, string> Svgs { get; }
    public IReadOnlyList<WarrantyDuration> DefaultDurations { get; }
    public IReadOnlyList<int> DefaultDurationsMonths { get; }
    public IReadOnlyList<FieldSpec> Fields { get; }
    public CategoryIdentifierMode IdentifierMode { get; }
    public string? TermsOverride { get; }
    public long Version => _catalogState.Version;

    public static DomainResult<Category, CategoryError> CreateBuiltIn(
        string? key,
        string? name,
        string? nameResourceKey,
        int sortOrder,
        bool showInBar,
        IReadOnlyList<int>? defaultDurationsMonths,
        CategoryIdentifierMode identifierMode,
        IReadOnlyDictionary<IllustrationVariant, string>? svgs,
        IReadOnlyList<FieldSpec>? fields)
    {
        var identity = CreateIdentity(key);
        if (identity is DomainResult<CategoryKey, CategoryError>.Failed identityFailure)
            return Failure(identityFailure.Error);

        var durations = CreateDurations(defaultDurationsMonths);
        if (durations is DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Failed durationFailure)
            return Failure(durationFailure.Error);

        var state = CategoryCatalogState.Create(
            isArchived: false,
            showInBar,
            sortOrder);
        if (state is DomainResult<CategoryCatalogState, CategoryError>.Failed stateFailure)
            return Failure(stateFailure.Error);

        return CreateCore(
            ((DomainResult<CategoryKey, CategoryError>.Succeeded)identity).Value,
            name,
            nameResourceKey,
            isBuiltIn: true,
            svgs,
            ((DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Succeeded)durations).Value,
            fields,
            identifierMode,
            termsOverride: null,
            ((DomainResult<CategoryCatalogState, CategoryError>.Succeeded)state).Value);
    }

    public static DomainResult<Category, CategoryError> CreateCustom(
        string? key,
        string? name,
        int sortOrder,
        int defaultDurationMonths,
        CategoryIdentifierMode identifierMode,
        string? termsOverride,
        bool showInBar,
        IReadOnlyDictionary<IllustrationVariant, string>? svgs,
        IReadOnlyList<FieldSpec>? fields)
    {
        var identity = CreateIdentity(key);
        if (identity is DomainResult<CategoryKey, CategoryError>.Failed identityFailure)
            return Failure(identityFailure.Error);

        var durations = CreateDurations([defaultDurationMonths]);
        if (durations is DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Failed durationFailure)
            return Failure(durationFailure.Error);

        var state = CategoryCatalogState.Create(
            isArchived: false,
            showInBar,
            sortOrder);
        if (state is DomainResult<CategoryCatalogState, CategoryError>.Failed stateFailure)
            return Failure(stateFailure.Error);

        return CreateCore(
            ((DomainResult<CategoryKey, CategoryError>.Succeeded)identity).Value,
            name,
            nameResourceKey: null,
            isBuiltIn: false,
            svgs,
            ((DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Succeeded)durations).Value,
            fields,
            identifierMode,
            termsOverride,
            ((DomainResult<CategoryCatalogState, CategoryError>.Succeeded)state).Value);
    }

    public static DomainResult<Category, CategoryError> CreateCustom(
        CategoryKey? identity,
        string? name,
        IReadOnlyDictionary<IllustrationVariant, string>? svgs,
        IReadOnlyList<WarrantyDuration>? defaultDurations,
        IReadOnlyList<FieldSpec>? fields,
        CategoryIdentifierMode identifierMode,
        string? termsOverride,
        CategoryCatalogState? catalogState) =>
        CreateCore(
            identity,
            name,
            nameResourceKey: null,
            isBuiltIn: false,
            svgs,
            defaultDurations,
            fields,
            identifierMode,
            termsOverride,
            catalogState);

    public DomainResult<Category, CategoryError> UpdateCustom(
        string? name,
        int defaultDurationMonths,
        CategoryIdentifierMode identifierMode,
        string? termsOverride,
        bool showInBar,
        IReadOnlyDictionary<IllustrationVariant, string>? svgs,
        IReadOnlyList<FieldSpec>? fields)
    {
        if (IsBuiltIn) return Failure(CategoryError.BuiltInCategoryCannotBeEdited);

        var durations = CreateDurations([defaultDurationMonths]);
        if (durations is DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Failed durationFailure)
            return Failure(durationFailure.Error);

        var state = CategoryCatalogState.Create(IsArchived, showInBar, SortOrder, Version);
        if (state is DomainResult<CategoryCatalogState, CategoryError>.Failed stateFailure)
            return Failure(stateFailure.Error);

        return CreateCore(
            Identity,
            name,
            nameResourceKey: null,
            isBuiltIn: false,
            svgs,
            ((DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Succeeded)durations).Value,
            fields,
            identifierMode,
            termsOverride,
            ((DomainResult<CategoryCatalogState, CategoryError>.Succeeded)state).Value);
    }

    public Category Archive() => Copy(_catalogState.Archive());

    public Category Unarchive() => Copy(_catalogState.Unarchive());

    public DomainResult<Category, CategoryError> WithIllustrations(
        IReadOnlyDictionary<IllustrationVariant, string>? illustrations) =>
        CreateCore(
            Identity,
            Name,
            NameResourceKey,
            IsBuiltIn,
            illustrations,
            DefaultDurations,
            Fields,
            IdentifierMode,
            TermsOverride,
            _catalogState);

    public Category WithCatalogState(CategoryCatalogState catalogState)
    {
        ArgumentNullException.ThrowIfNull(catalogState);
        return Copy(catalogState);
    }

    public DomainResult<Category, CategoryError> WithVersion(long version) =>
        _catalogState.WithVersion(version).Map(Copy);

    private Category Copy(CategoryCatalogState catalogState) =>
        new(
            Identity,
            Name,
            NameResourceKey,
            IsBuiltIn,
            Svgs,
            DefaultDurations,
            Fields,
            IdentifierMode,
            TermsOverride,
            catalogState);

    private static DomainResult<Category, CategoryError> CreateCore(
        CategoryKey? identity,
        string? name,
        string? nameResourceKey,
        bool isBuiltIn,
        IReadOnlyDictionary<IllustrationVariant, string>? svgs,
        IReadOnlyList<WarrantyDuration>? defaultDurations,
        IReadOnlyList<FieldSpec>? fields,
        CategoryIdentifierMode identifierMode,
        string? termsOverride,
        CategoryCatalogState? catalogState)
    {
        if (identity is null) return Failure(CategoryError.KeyInvalid);
        if (catalogState is null) return Failure(CategoryError.DataRequired);

        var normalizedName = name?.Trim();
        if (normalizedName is not { Length: >= 2 and <= MaximumNameLength })
            return Failure(CategoryError.NameInvalid);

        var normalizedResourceKey = string.IsNullOrWhiteSpace(nameResourceKey)
            ? null
            : nameResourceKey.Trim();
        if (normalizedResourceKey is { Length: > MaximumResourceKeyLength })
            return Failure(CategoryError.NameResourceKeyInvalid);
        if ((isBuiltIn && normalizedResourceKey is null)
            || (!isBuiltIn && normalizedResourceKey is not null))
        {
            return Failure(CategoryError.NameResourceKeyInvalid);
        }

        if (defaultDurations is not { Count: >= 1 and <= MaximumDurationCount }
            || defaultDurations.Any(duration => duration is null)
            || defaultDurations.Select(duration => duration.Months).Distinct().Count()
                != defaultDurations.Count)
        {
            return Failure(CategoryError.DurationCountInvalid);
        }

        if (fields is not { Count: >= 1 and <= MaximumFieldCount })
            return Failure(CategoryError.FieldCountInvalid);
        if (fields.Any(field => field is null)
            || fields.Select(field => field.Kind).Distinct().Count() != fields.Count)
        {
            return Failure(CategoryError.FieldInvalid);
        }
        if ((isBuiltIn && fields.Any(field => field.LabelResourceKey is null))
            || (!isBuiltIn && fields.Any(field => field.LabelResourceKey is not null)))
        {
            return Failure(CategoryError.FieldInvalid);
        }

        if (svgs is not { Count: >= 1 and <= MaximumIllustrationCount })
            return Failure(CategoryError.IllustrationCountInvalid);
        if (svgs.Any(item => !Enum.IsDefined(item.Key) || string.IsNullOrWhiteSpace(item.Value)))
            return Failure(CategoryError.IllustrationInvalid);

        if (!Enum.IsDefined(identifierMode))
            return Failure(CategoryError.IdentifierModeInvalid);
        var identifierKind = identifierMode == CategoryIdentifierMode.SerialNumber
            ? WarrantyFieldKind.SerialNumber
            : WarrantyFieldKind.Imei;
        if (fields.All(field => field.Kind != identifierKind))
            return Failure(CategoryError.IdentifierModeInvalid);

        var normalizedTerms = string.IsNullOrWhiteSpace(termsOverride) ? null : termsOverride.Trim();
        if (normalizedTerms is { Length: > MaximumTermsLength }
            || isBuiltIn && normalizedTerms is not null)
        {
            return Failure(CategoryError.TermsInvalid);
        }

        return DomainResult<Category, CategoryError>.Success(new Category(
            identity,
            normalizedName,
            normalizedResourceKey,
            isBuiltIn,
            svgs,
            defaultDurations,
            fields,
            identifierMode,
            normalizedTerms,
            catalogState));
    }

    private static DomainResult<CategoryKey, CategoryError> CreateIdentity(string? key) =>
        CategoryKey.Create(key).Match(
            DomainResult<CategoryKey, CategoryError>.Success,
            _ => DomainResult<CategoryKey, CategoryError>.Failure(CategoryError.KeyInvalid));

    private static DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError> CreateDurations(
        IReadOnlyList<int>? durationMonths)
    {
        if (durationMonths is not { Count: >= 1 and <= MaximumDurationCount }
            || durationMonths.Distinct().Count() != durationMonths.Count)
        {
            return DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Failure(
                CategoryError.DurationCountInvalid);
        }

        var durations = new List<WarrantyDuration>(durationMonths.Count);
        foreach (var months in durationMonths)
        {
            var duration = WarrantyDuration.Create(months);
            if (duration is not DomainResult<WarrantyDuration, WarrantyIssuanceError>.Succeeded succeeded)
            {
                return DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Failure(
                    CategoryError.DurationInvalid);
            }

            durations.Add(succeeded.Value);
        }

        return DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Success(durations);
    }

    private static DomainResult<Category, CategoryError> Failure(CategoryError error) =>
        DomainResult<Category, CategoryError>.Failure(error);
}
