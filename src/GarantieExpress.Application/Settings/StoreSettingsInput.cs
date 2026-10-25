namespace GarantieExpress.Application.Settings;

public sealed record StoreSettingsInput(
    long Version,
    string Name,
    string Slogan,
    IReadOnlyList<string> Services,
    string? LogoDataUri,
    string Address,
    string Phone,
    string Vat,
    ThemeSettings Theme,
    IReadOnlyList<SavedThemePreset> SavedThemePresets,
    string DefaultTerms,
    string ShortTerms,
    IReadOnlyList<int> OfferedDurationsMonths,
    string DefaultTemplate,
    bool InkSaver,
    string NumberPrefix);
