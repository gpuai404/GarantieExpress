using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Payloads;

internal sealed class CategoryPayload
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<IllustrationVariant, string>? Svgs { get; set; } = [];
    public List<int>? DefaultDurationsMonths { get; set; } = [12];
    public List<FieldSpecPayload>? Fields { get; set; } = [];
    public CategoryIdentifierMode? IdentifierMode { get; set; }
    public string? TermsOverride { get; set; }
}

internal static class CategoryPayloadMapper
{
    public static DomainResult<Category, CategoryError> ToDomain(
        CategoryPayload? document,
        string key,
        bool isArchived,
        bool showInBar,
        int sortOrder,
        long version)
    {
        if (document is null)
            return DomainResult<Category, CategoryError>.Failure(CategoryError.DataRequired);

        var identity = CategoryKey.Create(key);
        if (identity is not DomainResult<CategoryKey, CategoryKeyError>.Succeeded identitySucceeded)
            return Failure(CategoryError.KeyInvalid);

        var fields = FieldSpecPayloadMapper.ToDomain(document.Fields);
        if (fields is not DomainResult<IReadOnlyList<FieldSpec>, FieldSpecError>.Succeeded fieldsSucceeded)
            return Failure(CategoryError.FieldInvalid);

        var durations = MapDurations(document.DefaultDurationsMonths);
        if (durations is not DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Succeeded durationsSucceeded)
            return Failure(((DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Failed)durations).Error);

        var state = CategoryCatalogState.Create(isArchived, showInBar, sortOrder, version);
        if (state is not DomainResult<CategoryCatalogState, CategoryError>.Succeeded stateSucceeded)
            return Failure(((DomainResult<CategoryCatalogState, CategoryError>.Failed)state).Error);

        if (document.IdentifierMode is not { } identifierMode)
            return Failure(CategoryError.IdentifierModeInvalid);

        return Category.CreateCustom(
            identitySucceeded.Value,
            document.Name,
            document.Svgs,
            durationsSucceeded.Value,
            fieldsSucceeded.Value,
            identifierMode,
            document.TermsOverride,
            stateSucceeded.Value);
    }

    public static CategoryPayload FromDomain(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        return new CategoryPayload
        {
            Name = category.Name,
            Svgs = category.Svgs.ToDictionary(item => item.Key, item => item.Value),
            DefaultDurationsMonths = category.DefaultDurationsMonths.ToList(),
            Fields = category.Fields.Select(FieldSpecPayloadMapper.FromDomain).ToList(),
            IdentifierMode = category.IdentifierMode,
            TermsOverride = category.TermsOverride
        };
    }

    private static DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError> MapDurations(
        IReadOnlyList<int>? durationMonths)
    {
        if (durationMonths is null)
            return DomainResult<IReadOnlyList<WarrantyDuration>, CategoryError>.Failure(
                CategoryError.DurationCountInvalid);

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
