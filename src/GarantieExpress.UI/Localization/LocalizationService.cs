using System.Globalization;
using Microsoft.Extensions.Logging;
using GarantieExpress.Application.Localization;
using GarantieExpress.Application.Common;
using GarantieExpress.UI.Abstractions;

namespace GarantieExpress.UI.Localization;

public sealed record SupportedLanguage(string Code, string Name);

public sealed class LocalizationService(
    ILanguagePreferenceStore? languagePreferenceStore = null,
    ILogger<LocalizationService>? logger = null) : ILocalizationService
{
    public const string DefaultLanguage = "nl-BE";

    public static IReadOnlyList<SupportedLanguage> SupportedLanguages { get; } =
    [
        new("en", "English"),
        new("nl-BE", "Nederlands"),
        new("fr-BE", "Français")
    ];

    public string CurrentLanguage { get; private set; } = DefaultLanguage;

    public string this[string key] => Translations.Get(key, CurrentLanguage);

    public string Format(string key, params object[] arguments) =>
        string.Format(CultureInfo.GetCultureInfo(CurrentLanguage), this[key], arguments);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var language = DefaultLanguage;

        if (languagePreferenceStore is not null)
        {
            var result = await languagePreferenceStore.GetLanguageAsync(cancellationToken);
            if (result is OperationResult<string?>.Succeeded { Value: not null } succeeded)
                language = succeeded.Value;
            else if (result is OperationResult<string?>.Failed failed)
                logger?.LogWarning("Could not load the language preference: {Error}", failed.Error);
        }

        await ApplyAsync(language, persist: false, cancellationToken);
    }

    public Task SetLanguageAsync(string language, CancellationToken cancellationToken = default) =>
        ApplyAsync(language, persist: true, cancellationToken);

    private async Task ApplyAsync(string? language, bool persist, CancellationToken cancellationToken)
    {
        CurrentLanguage = Normalize(language);

        if (languagePreferenceStore is not null)
        {
            var result = await languagePreferenceStore.ApplyLanguageAsync(CurrentLanguage, persist, cancellationToken);
            if (result is OperationResult.Failed failed)
                logger?.LogWarning("Could not apply the language preference: {Error}", failed.Error);
        }
    }

    private static string Normalize(string? language)
    {
        if (language?.StartsWith("fr", StringComparison.OrdinalIgnoreCase) == true) return "fr-BE";
        if (language?.StartsWith("nl", StringComparison.OrdinalIgnoreCase) == true) return "nl-BE";
        return "en";
    }
}
