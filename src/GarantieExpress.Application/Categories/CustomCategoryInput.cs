using GarantieExpress.Domain;

namespace GarantieExpress.Application.Categories;

public sealed record CustomCategoryInput(
    string? ExistingKey,
    string Name,
    CategoryIdentifierMode IdentifierMode,
    int DefaultDurationMonths,
    string? TermsOverride,
    IReadOnlyDictionary<IllustrationVariant, string> Svgs,
    long ExpectedVersion = 0)
{
    public bool IsEditing => !string.IsNullOrWhiteSpace(ExistingKey);
}
