using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Categories;
using GarantieExpress.Domain;
using GarantieExpress.UI.Localization;

namespace GarantieExpress.UI.Features.Categories.Models;

public sealed class CustomCategoryDraftFactory(
    CategoryDisplayResolver categoryDisplay,
    ILocalizationService localization)
{
    public CustomCategoryDraft CreateForEdit(Category category) => new()
    {
        ExistingKey = category.Key,
        ExpectedVersion = category.Version,
        Name = category.Name,
        IdentifierMode = category.IdentifierMode,
        DefaultDurationMonths = category.DefaultDurationsMonths.FirstOrDefault(12),
        TermsOverride = category.TermsOverride,
        Svgs = category.Svgs.ToDictionary(item => item.Key, item => item.Value)
    };

    public CustomCategoryDraft CreateDuplicate(Category category) => new()
    {
        Name = localization.Format("Categories_CopyName", categoryDisplay.DisplayName(category)),
        IdentifierMode = category.IdentifierMode,
        DefaultDurationMonths = category.DefaultDurationsMonths.FirstOrDefault(12),
        TermsOverride = category.TermsOverride,
        Svgs = category.Svgs.ToDictionary(item => item.Key, item => item.Value)
    };
}
