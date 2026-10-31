using GarantieExpress.Application.Categories;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Settings;
using GarantieExpress.Application.Templates;
using GarantieExpress.Domain;

namespace GarantieExpress.Application.Initialization;

public sealed class InitialApplicationDataFactory(
    ILocalizationService localization,
    ITemplateKeyResolver templates)
{

    public InitialApplicationData Create()
    {
        var theme = ThemeSettingsMapper.ToDomain(new ThemeSettings()).Match(
            value => value,
            error => throw new InvalidOperationException(
                $"The initial theme is invalid: {error}."));
        var profile = StoreProfile.Create(
            localization["Default_StoreName"],
            localization["Default_Slogan"],
            localization["Default_Services"].Split(
                '|',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            null,
            string.Empty,
            string.Empty,
            string.Empty);
        var policy = WarrantyPolicy.Create(
            localization["Default_Terms"],
            localization["Default_ShortTerms"],
            [6, 12, 24],
            templates.ResolveKey("a4-reference"),
            "GE");

        var settings = profile.Bind(validProfile => policy.Bind(validPolicy =>
            StoreSettings.Create(
                validProfile,
                theme,
                [],
                validPolicy,
                inkSaver: false)));

        return settings.Match(
            value => new InitialApplicationData(value, BuiltInCategories.All),
            error => throw new InvalidOperationException(
                $"The initial store settings are invalid: {error}."));
    }
}
