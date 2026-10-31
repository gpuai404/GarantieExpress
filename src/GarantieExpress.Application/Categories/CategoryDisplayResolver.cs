using GarantieExpress.Application.Localization;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Categories;

public sealed class CategoryDisplayResolver(ILocalizationService localization)
{
    public string DisplayName(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return category.IsBuiltIn && !string.IsNullOrWhiteSpace(category.NameResourceKey)
            ? localization[category.NameResourceKey]
            : category.Name;
    }

    public string FieldLabel(Category category, FieldSpec field)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(field);
        return category.IsBuiltIn && !string.IsNullOrWhiteSpace(field.LabelResourceKey)
            ? localization[field.LabelResourceKey]
            : field.Label;
    }

    public FieldSpec PrimaryIdentifier(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        return category.Fields.FirstOrDefault(field =>
                   field.Kind is WarrantyFieldKind.Imei or WarrantyFieldKind.SerialNumber)
               ?? category.Fields[0];
    }

    public string ResolveIllustration(Category category, IllustrationVariant preferred)
    {
        ArgumentNullException.ThrowIfNull(category);
        var resolved = ResolveIllustration(category.Svgs, preferred);
        if (!string.IsNullOrWhiteSpace(resolved)) return resolved;

        var fallback = BuiltInCategories.All.FirstOrDefault(item => item.Key == category.Key)
            ?? BuiltInCategories.All.First(item => item.Key == "smartphone");
        return ResolveIllustration(fallback.Svgs, preferred);
    }

    public string ResolveIllustration(CategorySnapshot category, IllustrationVariant preferred)
    {
        ArgumentNullException.ThrowIfNull(category);
        var resolved = ResolveIllustration(category.Svgs, preferred);
        if (!string.IsNullOrWhiteSpace(resolved)) return resolved;

        var fallback = BuiltInCategories.All.FirstOrDefault(item => item.Key == category.Key)
            ?? BuiltInCategories.All.First(item => item.Key == "smartphone");
        return ResolveIllustration(fallback.Svgs, preferred);
    }

    public static string ResolveIllustration(
        IReadOnlyDictionary<IllustrationVariant, string> variants,
        IllustrationVariant preferred)
    {
        ArgumentNullException.ThrowIfNull(variants);
        foreach (var variant in FallbackOrder(preferred))
        {
            if (variants.TryGetValue(variant, out var svg)
                && !string.IsNullOrWhiteSpace(svg))
            {
                return svg;
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<IllustrationVariant> FallbackOrder(
        IllustrationVariant preferred) => preferred switch
        {
            IllustrationVariant.Detailed =>
                [IllustrationVariant.Detailed, IllustrationVariant.Simple, IllustrationVariant.Minimal],
            IllustrationVariant.Simple =>
                [IllustrationVariant.Simple, IllustrationVariant.Minimal, IllustrationVariant.Detailed],
            _ =>
                [IllustrationVariant.Minimal, IllustrationVariant.Simple, IllustrationVariant.Detailed]
        };
}
