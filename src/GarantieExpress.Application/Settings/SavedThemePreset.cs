namespace GarantieExpress.Application.Settings;

public sealed class SavedThemePreset
{
    public required string Name { get; set; }
    public ThemeSettings Theme { get; set; } = new();
}
