using GarantieExpress.Domain;

namespace GarantieExpress.UI.Components.Warranty.Templates;

public sealed record WarrantyTemplateDesign(
    string Key,
    string NameResourceKey);

public sealed record WarrantyPageSize(
    string Key,
    string NameResourceKey,
    double WidthMm,
    double HeightMm);

public sealed record WarrantyTemplate(
    string Key,
    WarrantyTemplateDesign Design,
    WarrantyPageSize PageSize,
    IllustrationVariant Illustration,
    Type Component,
    IReadOnlyDictionary<string, object>? StaticParameters = null)
{
    public string DesignKey => Design.Key;
    public string PageSizeKey => PageSize.Key;
    public double WidthMm => PageSize.WidthMm;
    public double HeightMm => PageSize.HeightMm;
}
