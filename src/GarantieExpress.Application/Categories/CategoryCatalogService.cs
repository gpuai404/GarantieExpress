using System.Globalization;
using GarantieExpress.Domain;
using GarantieExpress.Application.Common;
using GarantieExpress.Application.Localization;

namespace GarantieExpress.Application.Categories;

public sealed class CategoryCatalogService(
    ICategoryRepository repository,
    SvgSanitizer svgSanitizer,
    ILocalizationService localization,
    CategoryDisplayResolver display)
{
    public async Task<OperationResult<IReadOnlyList<Category>>> LoadAsync(
        bool includeArchived = true,
        CancellationToken cancellationToken = default)
    {
        var stored = await repository.GetAllAsync(cancellationToken);
        return stored.Match(
            categories => OperationResult<IReadOnlyList<Category>>.Success(categories
                .Where(category => includeArchived || !category.IsArchived)
                .OrderBy(category => category.IsArchived)
                .ThenBy(category => category.SortOrder)
                .ThenBy(
                    display.DisplayName,
                    StringComparer.Create(
                        CultureInfo.GetCultureInfo(localization.CurrentLanguage),
                        ignoreCase: true))
                .ToList()),
            OperationResult<IReadOnlyList<Category>>.Failure);
    }

    public async Task<OperationResult<Category>> SaveCustomAsync(
        CustomCategoryInput input,
        IReadOnlyList<Category> currentCatalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(currentCatalog);

        var existing = input.IsEditing
            ? currentCatalog.FirstOrDefault(category =>
                string.Equals(category.Key, input.ExistingKey, StringComparison.Ordinal)
                && !category.IsBuiltIn)
            : null;
        if (input.IsEditing && existing is null)
        {
            return CategoryFailure(
                "Category.CustomNotFound",
                "Categories_CustomNotFound",
                nameof(CustomCategoryInput.ExistingKey));
        }

        if (existing is not null
            && (input.ExpectedVersion < 1 || existing.Version != input.ExpectedVersion))
        {
            return CategoryFailure("Storage.StaleCategory", "Categories_EditConflict");
        }

        return await BuildFields(input.IdentifierMode).Match(
            fields =>
            {
                var category = existing is null
                    ? Category.CreateCustom(
                        CategoryKeyGenerator.Generate(input.Name, currentCatalog.Select(item => item.Key)),
                        input.Name,
                        NextSortOrder(currentCatalog),
                        input.DefaultDurationMonths,
                        input.IdentifierMode,
                        input.TermsOverride,
                        false,
                        input.Svgs,
                        fields)
                    : existing.UpdateCustom(
                        input.Name,
                        input.DefaultDurationMonths,
                        input.IdentifierMode,
                        input.TermsOverride,
                        existing.ShowInBar,
                        input.Svgs,
                        fields);

                return category.Match(
                    value => SanitizeAndPersistAsync(value, cancellationToken),
                    error => Task.FromResult(
                        OperationResult<Category>.Failure(MapCategoryError(error))));
            },
            _ => Task.FromResult(OperationResult<Category>.Failure(
                MapCategoryError(CategoryError.FieldInvalid))));
    }

    public Task<OperationResult> SetArchivedAsync(
        Category category,
        bool isArchived,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        return PersistStateAsync(
            isArchived ? category.Archive() : category.Unarchive(),
            cancellationToken);
    }

    private Task<OperationResult<Category>> SanitizeAndPersistAsync(
        Category category,
        CancellationToken cancellationToken)
    {
        var sanitized = SanitizeIllustrations(category.Svgs);
        return sanitized.Match(
            illustrations => category.WithIllustrations(illustrations).Match(
                updated => PersistCategoryAsync(updated, cancellationToken),
                error => Task.FromResult(OperationResult<Category>.Failure(MapCategoryError(error)))),
            error => Task.FromResult(OperationResult<Category>.Failure(error)));
    }

    private OperationResult<IReadOnlyDictionary<IllustrationVariant, string>> SanitizeIllustrations(
        IReadOnlyDictionary<IllustrationVariant, string> illustrations)
    {
        var sanitizedIllustrations = new Dictionary<IllustrationVariant, string>();
        foreach (var (variant, svg) in illustrations)
        {
            var sanitized = svgSanitizer.Sanitize(svg, useThemeColor: false);
            if (sanitized is OperationResult<string>.Failed failed)
            {
                return OperationResult<IReadOnlyDictionary<IllustrationVariant, string>>.Failure(
                    failed.Details);
            }

            sanitizedIllustrations[variant] =
                ((OperationResult<string>.Succeeded)sanitized).Value;
        }

        return OperationResult<IReadOnlyDictionary<IllustrationVariant, string>>.Success(
            sanitizedIllustrations);
    }

    private async Task<OperationResult<Category>> PersistCategoryAsync(
        Category category,
        CancellationToken cancellationToken)
    {
        var saved = await repository.SaveAsync(category, cancellationToken);
        return saved.Match(
            OperationResult<Category>.Success,
            error => OperationResult<Category>.Failure(error.Code switch
            {
                "Storage.StaleCategory" => new OperationError(
                    error.Code,
                    localization["Categories_EditConflict"]),
                _ => error
            }));
    }

    private async Task<OperationResult> PersistStateAsync(
        Category category,
        CancellationToken cancellationToken)
    {
        var result = await repository.SaveAsync(category, cancellationToken);
        return result.Match(
            _ => OperationResult.Success(),
            OperationResult.Failure);
    }

    private DomainResult<IReadOnlyList<FieldSpec>, FieldSpecError> BuildFields(
        CategoryIdentifierMode mode)
    {
        var (kind, label) = mode switch
        {
            CategoryIdentifierMode.Imei =>
                (WarrantyFieldKind.Imei, localization["Field_ImeiOnly"]),
            CategoryIdentifierMode.SerialAndImei =>
                (WarrantyFieldKind.Imei, localization["Field_Imei"]),
            _ =>
                (WarrantyFieldKind.SerialNumber, localization["Field_SerialNumber"])
        };

        return FieldSpec.Create(kind, label).Map<IReadOnlyList<FieldSpec>>(field => [field]);
    }

    private static int NextSortOrder(IReadOnlyList<Category> categories) =>
        categories.Count == 0
            ? 10
            : ((categories.Max(category => category.SortOrder) / 10) + 1) * 10;

    private OperationError MapCategoryError(CategoryError error) => error switch
    {
        CategoryError.NameInvalid =>
            Error("Category.NameInvalid", "Categories_NameError", nameof(CustomCategoryInput.Name)),
        CategoryError.DurationInvalid or CategoryError.DurationCountInvalid =>
            Error(
                "Category.DurationInvalid",
                "Categories_DurationError",
                nameof(CustomCategoryInput.DefaultDurationMonths)),
        CategoryError.IllustrationInvalid or CategoryError.IllustrationCountInvalid =>
            Error(
                "Category.IllustrationInvalid",
                "Categories_IllustrationRequired",
                nameof(CustomCategoryInput.Svgs)),
        CategoryError.TermsInvalid =>
            Error(
                "Category.TermsInvalid",
                "Validation_TermsLength",
                nameof(CustomCategoryInput.TermsOverride)),
        CategoryError.BuiltInCategoryCannotBeEdited =>
            Error("Category.BuiltInReadOnly", "Categories_CustomNotFound"),
        _ => Error("Category.Invalid", "Error_InvalidData")
    };

    private OperationResult<Category> CategoryFailure(
        string code,
        string resourceKey,
        string? field = null) =>
        OperationResult<Category>.Failure(Error(code, resourceKey, field));

    private OperationError Error(string code, string resourceKey, string? field = null) =>
        new(code, localization[resourceKey], field);

}
