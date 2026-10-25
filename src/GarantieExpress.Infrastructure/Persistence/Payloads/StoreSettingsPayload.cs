using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Payloads;

internal sealed class StoreSettingsPayload
{
    public string Name { get; set; } = "My store";
    public string Slogan { get; set; } = string.Empty;
    public List<string> Services { get; set; } = [];
    public string? LogoDataUri { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Vat { get; set; } = string.Empty;
    public ThemePayload Theme { get; set; } = new();
    public List<SavedThemePayload> SavedThemePresets { get; set; } = [];
    public string DefaultTerms { get; set; } = string.Empty;
    public string ShortTerms { get; set; } = string.Empty;
    public List<int> OfferedDurationsMonths { get; set; } = [6, 12, 24];
    public string DefaultTemplate { get; set; } = "a4-reference";
    public bool InkSaver { get; set; }
    public string NumberPrefix { get; set; } = "GE";
}

internal static class StoreSettingsPayloadMapper
{
    public static DomainResult<StoreSettings, StoreSettingsError> ToDomain(
        StoreSettingsPayload document,
        long version)
    {
        ArgumentNullException.ThrowIfNull(document);

        var theme = ThemePayloadMapper.ToDomain(document.Theme);
        if (theme is null) return Failure(StoreSettingsError.InvalidTheme);

        var savedPalettes = new List<SavedThemePalette>();
        foreach (var preset in document.SavedThemePresets ?? [])
        {
            if (preset is null) return Failure(StoreSettingsError.InvalidSavedThemePreset);

            var presetTheme = ThemePayloadMapper.ToDomain(preset.Theme);
            var saved = SavedThemePalette.Create(preset.Name, presetTheme);
            if (saved is not DomainResult<SavedThemePalette, SavedThemePaletteError>.Succeeded succeeded)
                return Failure(StoreSettingsError.InvalidSavedThemePreset);
            savedPalettes.Add(succeeded.Value);
        }

        var profile = StoreProfile.Create(
            document.Name,
            document.Slogan,
            document.Services,
            document.LogoDataUri,
            document.Address,
            document.Phone,
            document.Vat);
        if (profile is not DomainResult<StoreProfile, StoreSettingsError>.Succeeded profileSucceeded)
            return Failure(((DomainResult<StoreProfile, StoreSettingsError>.Failed)profile).Error);

        var policy = WarrantyPolicy.Create(
            document.DefaultTerms,
            document.ShortTerms,
            document.OfferedDurationsMonths,
            document.DefaultTemplate,
            document.NumberPrefix);
        if (policy is not DomainResult<WarrantyPolicy, StoreSettingsError>.Succeeded policySucceeded)
            return Failure(((DomainResult<WarrantyPolicy, StoreSettingsError>.Failed)policy).Error);

        return StoreSettings.Create(
            profileSucceeded.Value,
            theme,
            savedPalettes,
            policySucceeded.Value,
            document.InkSaver,
            version);
    }

    public static StoreSettingsPayload FromDomain(StoreSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new StoreSettingsPayload
        {
            Name = settings.Name,
            Slogan = settings.Slogan,
            Services = settings.Services.ToList(),
            LogoDataUri = settings.LogoDataUri,
            Address = settings.Address,
            Phone = settings.Phone,
            Vat = settings.Vat,
            Theme = ThemePayloadMapper.FromDomain(settings.Theme),
            SavedThemePresets = settings.SavedThemePresets
                .Select(preset => new SavedThemePayload
                {
                    Name = preset.Name,
                    Theme = ThemePayloadMapper.FromDomain(preset.Theme)
                })
                .ToList(),
            DefaultTerms = settings.DefaultTerms,
            ShortTerms = settings.ShortTerms,
            OfferedDurationsMonths = settings.OfferedDurationsMonths.ToList(),
            DefaultTemplate = settings.DefaultTemplate,
            InkSaver = settings.InkSaver,
            NumberPrefix = settings.NumberPrefix.Value
        };
    }

    private static DomainResult<StoreSettings, StoreSettingsError> Failure(
        StoreSettingsError error) =>
        DomainResult<StoreSettings, StoreSettingsError>.Failure(error);
}
