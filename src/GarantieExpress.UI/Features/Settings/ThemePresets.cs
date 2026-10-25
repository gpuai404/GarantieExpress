using GarantieExpress.Application.Settings;

namespace GarantieExpress.UI.Features.Settings;

public sealed record ThemePreset(string Key, string NameKey, ThemeSettings Theme);

public static class ThemePresets
{
    public const string DefaultKey = "default";

    public static IReadOnlyList<ThemePreset> All { get; } =
    [
        new(DefaultKey, "Theme_PresetDefault", new ThemeSettings()),
        new("editorial", "Theme_PresetEditorial", new ThemeSettings
        {
            Primary = "#273947",
            Secondary = "#B14E3E",
            Accent = "#CF6A55",
            Surface = "#EFE9DC",
            Ink = "#283846",
            MutedInk = "#667078",
            Line = "#C8BEAD",
            Paper = "#FBF7EC"
        }),
        new("ocean", "Theme_PresetOcean", new ThemeSettings
        {
            Primary = "#12344D",
            Secondary = "#087EA4",
            Accent = "#39B5D4",
            Surface = "#E5F4F8",
            Ink = "#173042",
            MutedInk = "#526D7A",
            Line = "#8CB5C2",
            Paper = "#FFFFFF"
        }),
        new("monochrome", "Theme_PresetMono", new ThemeSettings
        {
            Primary = "#1F2933",
            Secondary = "#3E4C59",
            Accent = "#616E7C",
            Surface = "#E4E7EB",
            Ink = "#1F2933",
            MutedInk = "#52606D",
            Line = "#9AA5B1",
            Paper = "#FFFFFF"
        })
    ];
}
