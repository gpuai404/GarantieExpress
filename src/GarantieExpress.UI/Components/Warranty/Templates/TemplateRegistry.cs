using GarantieExpress.Application.Templates;
using GarantieExpress.Domain;

namespace GarantieExpress.UI.Components.Warranty.Templates;

public sealed class TemplateRegistry : ITemplateKeyResolver
{
    public const string DefaultTemplateKey = "a4-reference";

    private static readonly WarrantyTemplateDesign OriginalDesign =
        new("original", "Template_OriginalName");

    private static readonly WarrantyTemplateDesign ClassicNavyDesign =
        new("classic-navy", "Template_ClassicNavyName");

    private static readonly WarrantyPageSize A4 =
        new("a4", "PageSize_A4Name", 210, 297);

    private static readonly WarrantyPageSize A5 =
        new("a5", "PageSize_A5Name", 148, 210);

    private static readonly WarrantyPageSize A6 =
        new("a6", "PageSize_A6Name", 105, 148);

    private readonly IReadOnlyList<WarrantyTemplate> _templates;
    private readonly IReadOnlyDictionary<string, WarrantyTemplate> _templatesByKey;
    private readonly IReadOnlyDictionary<(string DesignKey, string PageSizeKey), WarrantyTemplate> _templatesBySelection;

    public TemplateRegistry()
    {
        _templates =
        [
            new WarrantyTemplate(
                DefaultTemplateKey,
                OriginalDesign,
                A4,
                IllustrationVariant.Detailed,
                typeof(WarrantyA4)),
            new WarrantyTemplate(
                "a5-compact",
                OriginalDesign,
                A5,
                IllustrationVariant.Simple,
                typeof(WarrantyA5)),
            new WarrantyTemplate(
                "a6-essential",
                OriginalDesign,
                A6,
                IllustrationVariant.Minimal,
                typeof(WarrantyA6)),
            ClassicNavy("a4-classic-navy", A4, IllustrationVariant.Detailed),
            ClassicNavy("a5-classic-navy", A5, IllustrationVariant.Simple),
            ClassicNavy("a6-classic-navy", A6, IllustrationVariant.Minimal)
        ];
        _templatesByKey = _templates.ToDictionary(template => template.Key, StringComparer.Ordinal);
        _templatesBySelection = _templates.ToDictionary(template =>
            (template.DesignKey, template.PageSizeKey));
    }

    public IReadOnlyList<WarrantyTemplateDesign> Designs { get; } =
        [OriginalDesign, ClassicNavyDesign];

    public IReadOnlyList<WarrantyPageSize> PageSizes { get; } = [A4, A5, A6];

    public IReadOnlyList<WarrantyTemplate> All => _templates;

    public WarrantyTemplate Get(string? key) =>
        key is not null && _templatesByKey.TryGetValue(key, out var template)
        ? template
        : _templatesByKey[DefaultTemplateKey];

    public WarrantyTemplate WithDesign(string? currentTemplateKey, string? designKey)
    {
        var current = Get(currentTemplateKey);
        return Find(designKey, current.PageSizeKey) ?? current;
    }

    public WarrantyTemplate WithPageSize(string? currentTemplateKey, string? pageSizeKey)
    {
        var current = Get(currentTemplateKey);
        return Find(current.DesignKey, pageSizeKey) ?? current;
    }

    public string ResolveKey(string? key) => Get(key).Key;

    private WarrantyTemplate? Find(string? designKey, string? pageSizeKey) =>
        designKey is not null
        && pageSizeKey is not null
        && _templatesBySelection.TryGetValue((designKey, pageSizeKey), out var template)
            ? template
            : null;

    private static WarrantyTemplate ClassicNavy(
        string key,
        WarrantyPageSize pageSize,
        IllustrationVariant illustration) => new(
            key,
            ClassicNavyDesign,
            pageSize,
            illustration,
            typeof(WarrantyClassicNavy),
            new Dictionary<string, object>
            {
                [nameof(WarrantyClassicNavy.PageSizeKey)] = pageSize.Key,
                [nameof(WarrantyClassicNavy.TemplateKey)] = key
            });
}
