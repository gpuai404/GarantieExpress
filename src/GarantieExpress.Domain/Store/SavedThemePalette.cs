namespace GarantieExpress.Domain;

public sealed record SavedThemePalette
{
    public const int MaximumNameLength = 40;

    private SavedThemePalette(string name, ThemePalette theme)
    {
        Name = name;
        Theme = theme;
    }

    public string Name { get; }
    public ThemePalette Theme { get; }

    public static DomainResult<SavedThemePalette, SavedThemePaletteError> Create(
        string? name,
        ThemePalette? theme)
    {
        var normalizedName = name?.Trim();
        if (normalizedName is not { Length: >= 1 and <= MaximumNameLength })
        {
            return DomainResult<SavedThemePalette, SavedThemePaletteError>.Failure(
                SavedThemePaletteError.NameInvalid);
        }

        return theme is null
            ? DomainResult<SavedThemePalette, SavedThemePaletteError>.Failure(
                SavedThemePaletteError.ThemeRequired)
            : DomainResult<SavedThemePalette, SavedThemePaletteError>.Success(
                new SavedThemePalette(normalizedName, theme));
    }
}
