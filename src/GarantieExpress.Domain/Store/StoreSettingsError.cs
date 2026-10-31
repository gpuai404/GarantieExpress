namespace GarantieExpress.Domain;

public enum StoreSettingsError
{
    VersionInvalid,
    NameRequired,
    NameTooLong,
    SloganTooLong,
    AddressTooLong,
    ContactValueTooLong,
    InvalidServiceCount,
    ServiceTooLong,
    InvalidLogo,
    InvalidTheme,
    InvalidSavedThemePreset,
    TooManySavedThemePresets,
    TermsRequired,
    TermsTooLong,
    ShortTermsRequired,
    ShortTermsTooLong,
    InvalidDurations,
    InvalidTemplate,
    InvalidNumberPrefix
}
