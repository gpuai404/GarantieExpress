namespace GarantieExpress.Application.Settings;

public sealed class ThemeSettings
{
    public string Primary { get; set; } = "#172B38";
    public string Secondary { get; set; } = "#2168A8";
    public string Accent { get; set; } = "#F5D13D";
    public string Surface { get; set; } = "#E6F0F5";
    public string Ink { get; set; } = "#243A48";
    public string MutedInk { get; set; } = "#617683";
    public string Line { get; set; } = "#91A8B5";
    public string Paper { get; set; } = "#FFFEF9";

    public ThemeSettings Clone() => new()
    {
        Primary = Primary,
        Secondary = Secondary,
        Accent = Accent,
        Surface = Surface,
        Ink = Ink,
        MutedInk = MutedInk,
        Line = Line,
        Paper = Paper
    };
}
