using GarantieExpress.Application.Settings;
using GarantieExpress.Domain;
using GarantieExpress.UI.Validation;

namespace GarantieExpress.UI.Features.Settings.Models;

public sealed class SettingsFormModel
{
    public long Version { get; set; }

    [LocalizedRequired("Validation_NameRequired"), LocalizedStringLength(StoreSettings.MaximumNameLength, "Validation_NameLength")]
    public string Name { get; set; } = string.Empty;

    [LocalizedStringLength(StoreSettings.MaximumSloganLength, "Validation_SloganLength")]
    public string Slogan { get; set; } = string.Empty;

    public List<string> Services { get; set; } = [];
    public string? LogoDataUri { get; set; }

    [LocalizedStringLength(StoreSettings.MaximumAddressLength, "Validation_AddressLength")]
    public string Address { get; set; } = string.Empty;

    [LocalizedStringLength(StoreSettings.MaximumContactLength, "Validation_ShortValueLength")]
    public string Phone { get; set; } = string.Empty;

    [LocalizedStringLength(StoreSettings.MaximumContactLength, "Validation_ShortValueLength")]
    public string Vat { get; set; } = string.Empty;

    public ThemeSettings Theme { get; set; } = new();
    public List<SavedThemePreset> SavedThemePresets { get; set; } = [];

    [LocalizedRequired("Validation_TermsRequired"), LocalizedStringLength(StoreSettings.MaximumFullTermsLength, "Validation_TermsLength")]
    public string DefaultTerms { get; set; } = string.Empty;

    [LocalizedRequired("Validation_ShortTermsRequired"), LocalizedStringLength(StoreSettings.MaximumShortTermsLength, "Validation_TermsLength")]
    public string ShortTerms { get; set; } = string.Empty;

    public List<int> OfferedDurationsMonths { get; set; } = [];
    public string DefaultTemplate { get; set; } = "a4-reference";
    public bool InkSaver { get; set; }

    [LocalizedNumberPrefix]
    public string NumberPrefix { get; set; } = "GE";

    public static SettingsFormModel FromSettings(StoreSettings settings) => new()
    {
        Version = settings.Version,
        Name = settings.Name,
        Slogan = settings.Slogan,
        Services = settings.Services.ToList(),
        LogoDataUri = settings.LogoDataUri,
        Address = settings.Address,
        Phone = settings.Phone,
        Vat = settings.Vat,
        Theme = ThemeSettingsMapper.FromDomain(settings.Theme),
        SavedThemePresets = settings.SavedThemePresets
            .Select(preset => new SavedThemePreset
            {
                Name = preset.Name,
                Theme = ThemeSettingsMapper.FromDomain(preset.Theme)
            })
            .ToList(),
        DefaultTerms = settings.DefaultTerms,
        ShortTerms = settings.ShortTerms,
        OfferedDurationsMonths = settings.OfferedDurationsMonths.ToList(),
        DefaultTemplate = settings.DefaultTemplate,
        InkSaver = settings.InkSaver,
        NumberPrefix = settings.NumberPrefix.Value
    };

    public StoreSettingsInput ToInput() => new(
        Version,
        Name,
        Slogan,
        Services.ToList(),
        LogoDataUri,
        Address,
        Phone,
        Vat,
        Theme.Clone(),
        SavedThemePresets
            .Select(preset => new SavedThemePreset { Name = preset.Name, Theme = preset.Theme.Clone() })
            .ToList(),
        DefaultTerms,
        ShortTerms,
        OfferedDurationsMonths.ToList(),
        DefaultTemplate,
        InkSaver,
        NumberPrefix);
}
