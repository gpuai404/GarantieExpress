namespace GarantieExpress.Application.Localization;

public interface ILocalizationService
{
    string CurrentLanguage { get; }
    string this[string key] { get; }
    string Format(string key, params object[] arguments);
}
