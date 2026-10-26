using GarantieExpress.Domain;
using GarantieExpress.Application.Categories;

namespace GarantieExpress.UI.Features.Categories.Models;

public sealed class CustomCategoryDraft
{
    public string? ExistingKey { get; set; }
    public long ExpectedVersion { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryIdentifierMode IdentifierMode { get; set; } = CategoryIdentifierMode.SerialNumber;
    public int DefaultDurationMonths { get; set; } = 12;
    public string? TermsOverride { get; set; }
    public Dictionary<IllustrationVariant, string> Svgs { get; set; } = [];

    public bool IsEditing => !string.IsNullOrWhiteSpace(ExistingKey);

    public CustomCategoryInput ToInput() => new(
        ExistingKey,
        Name,
        IdentifierMode,
        DefaultDurationMonths,
        TermsOverride,
        Svgs.ToDictionary(item => item.Key, item => item.Value),
        ExpectedVersion);
}
