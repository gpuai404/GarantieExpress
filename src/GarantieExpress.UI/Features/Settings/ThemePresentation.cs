using System.Globalization;
using GarantieExpress.Application.Settings;
using GarantieExpress.Domain;

namespace GarantieExpress.UI.Features.Settings;

public sealed record ContrastCheck(string PairKey, double Ratio, double Minimum)
{
    public bool Passes => Ratio >= Minimum;
}

public sealed class ThemePresentation
{
    private const string LightForeground = "#FFFFFF";
    private const string DarkForeground = "#17212B";

    public string ToCssVariables(ThemeSettings theme)
    {
        var safeTheme = SafeTheme(theme);
        return string.Join(';', new Dictionary<string, string>
        {
            ["--primary"] = NormalizeHex(safeTheme.Primary),
            ["--on-primary"] = GetOnColor(safeTheme.Primary),
            ["--secondary"] = NormalizeHex(safeTheme.Secondary),
            ["--on-secondary"] = GetOnColor(safeTheme.Secondary),
            ["--accent"] = NormalizeHex(safeTheme.Accent),
            ["--on-accent"] = GetOnColor(safeTheme.Accent),
            ["--surface"] = NormalizeHex(safeTheme.Surface),
            ["--ink"] = NormalizeHex(safeTheme.Ink),
            ["--muted-ink"] = NormalizeHex(safeTheme.MutedInk),
            ["--line"] = NormalizeHex(safeTheme.Line),
            ["--paper"] = NormalizeHex(safeTheme.Paper)
        }.Select(pair => $"{pair.Key}:{pair.Value}")) + ';';
    }

    public string ToCssVariables(ThemePalette theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        return ToCssVariables(ThemeSettingsMapper.FromDomain(theme));
    }

    public IReadOnlyList<ContrastCheck> CheckContrast(ThemeSettings theme)
    {
        var safeTheme = SafeTheme(theme);
        return
        [
            new("Theme_ContrastInkPaper", ContrastRatio(safeTheme.Ink, safeTheme.Paper), 4.5),
            new("Theme_ContrastMutedPaper", ContrastRatio(safeTheme.MutedInk, safeTheme.Paper), 3.0),
            new("Theme_ContrastPrimary", ContrastRatio(GetOnColor(safeTheme.Primary), safeTheme.Primary), 4.5),
            new("Theme_ContrastSecondary", ContrastRatio(GetOnColor(safeTheme.Secondary), safeTheme.Secondary), 4.5),
            new("Theme_ContrastAccent", ContrastRatio(GetOnColor(safeTheme.Accent), safeTheme.Accent), 4.5)
        ];
    }

    private static ThemeSettings SafeTheme(ThemeSettings? theme) =>
        ThemeSettingsMapper.ToDomain(theme).IsSuccess
        ? theme!
        : new ThemeSettings();

    private static string GetOnColor(string hex)
    {
        var normalized = NormalizeHex(hex);
        var lightRatio = ContrastRatio(LightForeground, normalized);
        var darkRatio = ContrastRatio(DarkForeground, normalized);
        return lightRatio >= darkRatio ? LightForeground : DarkForeground;
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var lighter = Math.Max(RelativeLuminance(foreground), RelativeLuminance(background));
        var darker = Math.Min(RelativeLuminance(foreground), RelativeLuminance(background));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static string NormalizeHex(string value) => value.ToUpperInvariant();

    private static double RelativeLuminance(string hex)
    {
        var channels = new[] { hex[1..3], hex[3..5], hex[5..7] }
            .Select(value => int.Parse(
                value,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture) / 255d)
            .Select(value => value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4))
            .ToArray();

        return channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
    }
}
