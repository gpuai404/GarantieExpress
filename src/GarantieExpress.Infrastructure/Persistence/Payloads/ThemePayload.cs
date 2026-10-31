using GarantieExpress.Domain;

namespace GarantieExpress.Infrastructure.Persistence.Payloads;

internal sealed class ThemePayload
{
    public string Primary { get; set; } = string.Empty;
    public string Secondary { get; set; } = string.Empty;
    public string Accent { get; set; } = string.Empty;
    public string Surface { get; set; } = string.Empty;
    public string Ink { get; set; } = string.Empty;
    public string MutedInk { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;
    public string Paper { get; set; } = string.Empty;
}

internal sealed class SavedThemePayload
{
    public string Name { get; set; } = string.Empty;
    public ThemePayload Theme { get; set; } = new();
}

internal static class ThemePayloadMapper
{
    public static ThemePalette? ToDomain(ThemePayload? payload) => payload is null
        ? null
        : ThemePalette.Create(
                payload.Primary,
                payload.Secondary,
                payload.Accent,
                payload.Surface,
                payload.Ink,
                payload.MutedInk,
                payload.Line,
                payload.Paper)
            .Match<ThemePalette?>(value => value, _ => null);

    public static ThemePayload FromDomain(ThemePalette palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        return new ThemePayload
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
