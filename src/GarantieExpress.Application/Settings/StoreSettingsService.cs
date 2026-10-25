using GarantieExpress.Application.Common;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Templates;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Settings;

public sealed class StoreSettingsService(
    ISettingsRepository repository,
    ILocalizationService localization,
    ITemplateKeyResolver templates)
{
    public async Task<OperationResult<StoreSettings>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var loaded = await repository.GetAsync(cancellationToken);
        if (loaded is OperationResult<StoreSettings?>.Failed failed)
            return OperationResult<StoreSettings>.Failure(failed.Details);

        var settings = ((OperationResult<StoreSettings?>.Succeeded)loaded).Value;
        return settings is null
            ? OperationResult<StoreSettings>.Failure(new OperationError(
                "Storage.SettingsNotFound",
                "Store settings could not be loaded."))
            : OperationResult<StoreSettings>.Success(settings);
    }

    public async Task<OperationResult<StoreSettings>> SaveAsync(
        StoreSettingsInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var settings = ToDomain(input);
        return await settings.Match(
            value => repository.SaveAsync(value, cancellationToken),
            error => Task.FromResult(OperationResult<StoreSettings>.Failure(MapError(error))));
    }

    private DomainResult<StoreSettings, StoreSettingsError> ToDomain(StoreSettingsInput input)
    {
        var themeResult = ThemeSettingsMapper.ToDomain(input.Theme);
        if (themeResult is not DomainResult<ThemePalette, ThemePaletteError>.Succeeded themeSucceeded)
            return DomainResult<StoreSettings, StoreSettingsError>.Failure(StoreSettingsError.InvalidTheme);

        var theme = themeSucceeded.Value;
        var savedThemes = new List<SavedThemePalette>();
        foreach (var preset in input.SavedThemePresets ?? [])
        {
            if (preset is null)
                return DomainResult<StoreSettings, StoreSettingsError>.Failure(StoreSettingsError.InvalidSavedThemePreset);

            var paletteResult = ThemeSettingsMapper.ToDomain(preset.Theme);
            if (paletteResult is not DomainResult<ThemePalette, ThemePaletteError>.Succeeded paletteSucceeded)
                return DomainResult<StoreSettings, StoreSettingsError>.Failure(StoreSettingsError.InvalidSavedThemePreset);

            var savedResult = SavedThemePalette.Create(preset.Name, paletteSucceeded.Value);
            if (savedResult is not DomainResult<SavedThemePalette, SavedThemePaletteError>.Succeeded savedSucceeded)
                return DomainResult<StoreSettings, StoreSettingsError>.Failure(StoreSettingsError.InvalidSavedThemePreset);
            savedThemes.Add(savedSucceeded.Value);
        }

        var profile = StoreProfile.Create(
            input.Name,
            input.Slogan,
            input.Services,
            input.LogoDataUri,
            input.Address,
            input.Phone,
            input.Vat);
        if (profile is DomainResult<StoreProfile, StoreSettingsError>.Failed profileFailure)
            return DomainResult<StoreSettings, StoreSettingsError>.Failure(profileFailure.Error);

        var policy = WarrantyPolicy.Create(
            input.DefaultTerms,
            input.ShortTerms,
            input.OfferedDurationsMonths,
            templates.ResolveKey(input.DefaultTemplate),
            input.NumberPrefix);
        if (policy is DomainResult<WarrantyPolicy, StoreSettingsError>.Failed policyFailure)
            return DomainResult<StoreSettings, StoreSettingsError>.Failure(policyFailure.Error);

        return StoreSettings.Create(
            ((DomainResult<StoreProfile, StoreSettingsError>.Succeeded)profile).Value,
            theme,
            savedThemes,
            ((DomainResult<WarrantyPolicy, StoreSettingsError>.Succeeded)policy).Value,
            input.InkSaver,
            input.Version);
    }

    private OperationError MapError(StoreSettingsError error) => error switch
    {
        StoreSettingsError.NameRequired =>
            Error("Settings.NameRequired", "Validation_NameRequired", "Name"),
        StoreSettingsError.NameTooLong =>
            Error("Settings.NameTooLong", "Validation_NameLength", "Name"),
        StoreSettingsError.SloganTooLong =>
            Error("Settings.SloganTooLong", "Validation_SloganLength", "Slogan"),
        StoreSettingsError.AddressTooLong =>
            Error("Settings.AddressTooLong", "Validation_AddressLength", "Address"),
        StoreSettingsError.ContactValueTooLong =>
            Error("Settings.ContactValueTooLong", "Validation_ShortValueLength"),
        StoreSettingsError.InvalidServiceCount =>
            Error("Settings.InvalidServiceCount", "Validation_ServiceCount", "Services"),
        StoreSettingsError.ServiceTooLong =>
            Error("Settings.ServiceTooLong", "Validation_ServiceLength", "Services"),
        StoreSettingsError.InvalidLogo =>
            Error("Settings.InvalidLogo", "Validation_LogoData", "LogoDataUri"),
        StoreSettingsError.InvalidTheme =>
            Error("Settings.InvalidTheme", "Error_InvalidTheme", "Theme"),
        StoreSettingsError.InvalidSavedThemePreset or StoreSettingsError.TooManySavedThemePresets =>
            Error("Settings.InvalidThemePreset", "Error_InvalidTheme", "SavedThemePresets"),
        StoreSettingsError.TermsRequired =>
            Error("Settings.TermsRequired", "Validation_TermsRequired", "DefaultTerms"),
        StoreSettingsError.TermsTooLong =>
            Error("Settings.TermsTooLong", "Validation_TermsLength", "DefaultTerms"),
        StoreSettingsError.ShortTermsRequired =>
            Error("Settings.ShortTermsRequired", "Validation_ShortTermsRequired", "ShortTerms"),
        StoreSettingsError.ShortTermsTooLong =>
            Error("Settings.ShortTermsTooLong", "Validation_TermsLength", "ShortTerms"),
        StoreSettingsError.InvalidDurations =>
            Error("Settings.InvalidDurations", "Error_InvalidDurations", "OfferedDurationsMonths"),
        StoreSettingsError.InvalidTemplate =>
            Error("Settings.InvalidTemplate", "Error_InvalidData", "DefaultTemplate"),
        StoreSettingsError.InvalidNumberPrefix =>
            Error("Settings.InvalidNumberPrefix", "Validation_NumberPrefix", "NumberPrefix"),
        StoreSettingsError.VersionInvalid =>
            Error("Settings.VersionInvalid", "Error_InvalidData"),
        _ => Error("Settings.Invalid", "Error_InvalidData")
    };

    private OperationError Error(string code, string resourceKey, string? field = null) =>
        new(code, localization[resourceKey], field);
}
