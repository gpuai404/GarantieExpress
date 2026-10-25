using GarantieExpress.Domain;

namespace GarantieExpress.Application.Settings;

public static class ThemeSettingsMapper
{
    public static DomainResult<ThemePalette, ThemePaletteError> ToDomain(
        ThemeSettings? settings) =>
        settings is null
            ? DomainResult<ThemePalette, ThemePaletteError>.Failure(
                ThemePaletteError.InvalidColor)
            : ThemePalette.Create(
                settings.Primary,
                settings.Secondary,
                settings.Accent,
                settings.Surface,
                settings.Ink,
                settings.MutedInk,
                settings.Line,
                settings.Paper);

    public static ThemeSettings FromDomain(ThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);

        return new ThemeSettings
        {
            Primary = palette.Primary,
            Secondary = palette.Secondary,
            Accent = palette.Accent,
            Surface = palette.Surface,
            Ink = palette.Ink,
            MutedInk = palette.MutedInk,
            Line = palette.Line,
            Paper = palette.Paper
        };
    }
}
