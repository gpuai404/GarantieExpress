namespace GarantieExpress.Domain;

public sealed class ThemePalette
{
    private ThemePalette(
        string primary,
        string secondary,
        string accent,
        string surface,
        string ink,
        string mutedInk,
        string line,
        string paper)
    {
        Primary = primary;
        Secondary = secondary;
        Accent = accent;
        Surface = surface;
        Ink = ink;
        MutedInk = mutedInk;
        Line = line;
        Paper = paper;
    }

    public string Primary { get; }
    public string Secondary { get; }
    public string Accent { get; }
    public string Surface { get; }
    public string Ink { get; }
    public string MutedInk { get; }
    public string Line { get; }
    public string Paper { get; }

    public static DomainResult<ThemePalette, ThemePaletteError> Create(
        string? primary,
        string? secondary,
        string? accent,
        string? surface,
        string? ink,
        string? mutedInk,
        string? line,
        string? paper)
    {
        string?[] colors =
        [
            primary, secondary, accent, surface, ink, mutedInk, line, paper
        ];
        if (colors.Any(color => !IsValidHex(color))) return Failure();

        return DomainResult<ThemePalette, ThemePaletteError>.Success(new ThemePalette(
            primary!.ToUpperInvariant(),
            secondary!.ToUpperInvariant(),
            accent!.ToUpperInvariant(),
            surface!.ToUpperInvariant(),
            ink!.ToUpperInvariant(),
            mutedInk!.ToUpperInvariant(),
            line!.ToUpperInvariant(),
            paper!.ToUpperInvariant()));
    }

    public static bool IsValidHex(string? value)
    {
        if (value is not { Length: 7 } || value[0] != '#') return false;
        for (var index = 1; index < value.Length; index++)
        {
            if (!char.IsAsciiHexDigit(value[index])) return false;
        }

        return true;
    }

    private static DomainResult<ThemePalette, ThemePaletteError> Failure() =>
        DomainResult<ThemePalette, ThemePaletteError>.Failure(ThemePaletteError.InvalidColor);
}
